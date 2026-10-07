using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ASCOM.OnStep
{
    /// <summary>
    /// HID transport for the ASTRO8-OnstepHIDascom driver.
    /// Talks to the Rp2040HID firmware (vendor HID, no Report ID, 32-byte IN/OUT reports).
    /// Frame format: byte[0] = payload length (0..31), byte[1..] = serial data.
    /// This is an ADD-ON transport; the original serial / TCP logic of the official
    /// ASCOM.OnStep driver is left untouched.
    /// </summary>
    public static class HidTransport
    {
        // ---- Win32 / hid.dll / setupapi.dll P/Invoke ----
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const uint DIGCF_PRESENT = 0x00000002;
        private const uint DIGCF_DEVICEINTERFACE = 0x00000010;
        private const int INVALID_HANDLE_VALUE = -1;
        private const int REPORT_LEN = 32;

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public short VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public uint Flags;
            public IntPtr Reserved;
        }

        [DllImport("hid.dll", SetLastError = true)]
        private static extern void HidD_GetHidGuid(ref Guid hidGuid);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetAttributes(IntPtr device, ref HIDD_ATTRIBUTES attributes);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetSerialNumberString(IntPtr device, StringBuilder buffer, int bufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetProductString(IntPtr device, StringBuilder buffer, int bufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_SetOutputReport(IntPtr device, byte[] reportBuffer, int reportBufferLength);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize, ref uint requiredSize, IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, ref uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, ref uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CancelIoEx(IntPtr hFile, IntPtr lpOverlapped);

        /// <summary>An opened HID device with its read buffer.</summary>
        private sealed class HidDevice
        {
            public IntPtr Handle;
            public readonly object BufferLock = new object();
            public readonly List<byte> Buffer = new List<byte>(4096);
            public volatile bool Reading;
            public volatile bool Closing;
        }

        private static readonly Dictionary<string, HidDevice> devices = new Dictionary<string, HidDevice>();
        private static readonly object devicesLock = new object();

        public static bool IsHidDeviceId(string deviceId)
        {
            return deviceId != null && deviceId.StartsWith("HID:", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Parse "HID:VID:PID:SN" -> (vid, pid, serial). VID/PID hex, SN optional (empty = skip check).</summary>
        public static bool ParseDeviceId(string deviceId, out ushort vid, out ushort pid, out string serial)
        {
            vid = 0; pid = 0; serial = "";
            if (!IsHidDeviceId(deviceId)) return false;
            string[] parts = deviceId.Substring(4).Split(new[] { ':' }, 4);
            if (parts.Length < 2) return false;
            if (!ushort.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out vid)) return false;
            if (!ushort.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out pid)) return false;
            if (parts.Length >= 3) serial = parts[2];
            return true;
        }

        /// <summary>Open the HID device matching VID/PID (and serial if provided). Throws on failure.</summary>
        public static void Open(string deviceId)
        {
            if (!ParseDeviceId(deviceId, out ushort vid, out ushort pid, out string serial))
            {
                throw new InvalidOperationException("Invalid HID device id: " + deviceId + " (expected HID:VID:PID[:SN])");
            }

            lock (devicesLock)
            {
                if (devices.ContainsKey(deviceId))
                {
                    throw new InvalidOperationException("HID device already open: " + deviceId);
                }

                Guid hidGuid = Guid.Empty;
                HidD_GetHidGuid(ref hidGuid);
                IntPtr devInfoSet = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
                if (devInfoSet == new IntPtr(INVALID_HANDLE_VALUE) || devInfoSet == IntPtr.Zero)
                {
                    throw new InvalidOperationException("SetupDiGetClassDevs failed (HID class unavailable)");
                }

                IntPtr handle = IntPtr.Zero;
                string openedPath = "";
                try
                {
                    uint index = 0;
                    SP_DEVICE_INTERFACE_DATA ifData = new SP_DEVICE_INTERFACE_DATA();
                    ifData.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));

                    while (SetupDiEnumDeviceInterfaces(devInfoSet, IntPtr.Zero, ref hidGuid, index, ref ifData))
                    {
                        uint requiredSize = 0;
                        SetupDiGetDeviceInterfaceDetail(devInfoSet, ref ifData, IntPtr.Zero, 0, ref requiredSize, IntPtr.Zero);
                        if (requiredSize <= 0) { index++; continue; }

                        IntPtr detail = Marshal.AllocHGlobal((int)requiredSize);
                        try
                        {
                            Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 4); // cbSize
                            if (!SetupDiGetDeviceInterfaceDetail(devInfoSet, ref ifData, detail, requiredSize, ref requiredSize, IntPtr.Zero))
                            {
                                index++; continue;
                            }
                            string devicePath = Marshal.PtrToStringUni(IntPtr.Add(detail, IntPtr.Size));
                            if (string.IsNullOrEmpty(devicePath)) { index++; continue; }

                            IntPtr h = CreateFile(devicePath, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                            if (h == new IntPtr(INVALID_HANDLE_VALUE) || h == IntPtr.Zero) { index++; continue; }

                            HIDD_ATTRIBUTES attrs = new HIDD_ATTRIBUTES();
                            attrs.Size = Marshal.SizeOf(typeof(HIDD_ATTRIBUTES));
                            if (!HidD_GetAttributes(h, ref attrs))
                            {
                                CloseHandle(h);
                                index++; continue;
                            }
                            if (attrs.VendorID != vid || attrs.ProductID != pid)
                            {
                                CloseHandle(h);
                                index++; continue;
                            }

                            if (!string.IsNullOrEmpty(serial) && serial != "*")
                            {
                                StringBuilder sb = new StringBuilder(256);
                                HidD_GetSerialNumberString(h, sb, 256);
                                string sn = sb.ToString().TrimEnd('\0', ' ', '\t');
                                if (!string.Equals(sn, serial, StringComparison.OrdinalIgnoreCase))
                                {
                                    CloseHandle(h);
                                    index++; continue;
                                }
                            }

                            handle = h;
                            openedPath = devicePath;
                            break;
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(detail);
                        }
                    }
                }
                finally
                {
                    SetupDiDestroyDeviceInfoList(devInfoSet);
                }

                if (handle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("HID device not found: VID=0x" + vid.ToString("X4") + " PID=0x" + pid.ToString("X4")
                        + (string.IsNullOrEmpty(serial) ? "" : " SN=" + serial));
                }

                HidDevice dev = new HidDevice();
                dev.Handle = handle;
                dev.Reading = true;
                devices.Add(deviceId, dev);

                // Start the background reader thread.
                Thread t = new Thread(() => ReadLoop(deviceId, dev));
                t.IsBackground = true;
                t.Name = "HID-RX-" + deviceId;
                t.Start();
            }
        }

        private static void ReadLoop(string deviceId, HidDevice dev)
        {
            byte[] buf = new byte[REPORT_LEN];
            while (!dev.Closing && dev.Reading)
            {
                uint read = 0;
                if (!ReadFile(dev.Handle, buf, REPORT_LEN, ref read, IntPtr.Zero) || read == 0)
                {
                    if (dev.Closing) break;
                    // Device error (unplugged). Stop reading; SendMessage will surface a failure.
                    dev.Reading = false;
                    break;
                }
                if (read < 1) continue;
                uint n = (uint)Math.Min((int)buf[0], REPORT_LEN - 1);
                if (n > read - 1) n = read - 1;
                if (n > 0)
                {
                    lock (dev.BufferLock)
                    {
                        for (uint i = 0; i < n; i++) dev.Buffer.Add(buf[i + 1]);
                    }
                }
            }
        }

        /// <summary>Write raw bytes (no framing) over the HID OUT reports, chunked to 31 payload bytes.</summary>
        public static void Write(string deviceId, byte[] data)
        {
            HidDevice dev = GetOpen(deviceId);
            int offset = 0;
            while (offset < data.Length)
            {
                int n = Math.Min(data.Length - offset, REPORT_LEN - 1);
                byte[] frame = new byte[REPORT_LEN];
                frame[0] = (byte)n;
                Array.Copy(data, offset, frame, 1, n);
                offset += n;
                uint written = 0;
                bool ok = WriteFile(dev.Handle, frame, REPORT_LEN, ref written, IntPtr.Zero);
                if (!ok || written != REPORT_LEN)
                {
                    // Fall back to SET_REPORT control transfer (some hosts route writes there).
                    ok = HidD_SetOutputReport(dev.Handle, frame, REPORT_LEN);
                    if (!ok)
                    {
                        throw new InvalidOperationException("HID write failed (WriteFile / SetOutputReport)");
                    }
                }
            }
        }

        /// <summary>Read bytes until the terminator byte is seen (inclusive), or throw on timeout.</summary>
        public static byte[] ReadUntil(string deviceId, byte terminator, int timeoutMs)
        {
            HidDevice dev = GetOpen(deviceId);
            int waited = 0;
            int sleepStep = 10;
            for (;;)
            {
                lock (dev.BufferLock)
                {
                    int idx = dev.Buffer.IndexOf(terminator);
                    if (idx >= 0)
                    {
                        byte[] result = new byte[idx + 1];
                        dev.Buffer.CopyTo(0, result, 0, idx + 1);
                        dev.Buffer.RemoveRange(0, idx + 1);
                        return result;
                    }
                }
                if (waited >= timeoutMs)
                {
                    throw new TimeoutException("HID read timed out after " + timeoutMs + " ms (no '#' terminator received)");
                }
                if (!dev.Reading)
                {
                    throw new InvalidOperationException("HID read failed: device disconnected");
                }
                Thread.Sleep(sleepStep);
                waited += sleepStep;
            }
        }

        /// <summary>Read up to count bytes (non-blocking best effort, used by rOne).</summary>
        public static byte[] ReadCounted(string deviceId, int count, int timeoutMs)
        {
            HidDevice dev = GetOpen(deviceId);
            int waited = 0;
            int sleepStep = 10;
            for (;;)
            {
                lock (dev.BufferLock)
                {
                    if (dev.Buffer.Count >= count)
                    {
                        byte[] result = new byte[count];
                        dev.Buffer.CopyTo(0, result, 0, count);
                        dev.Buffer.RemoveRange(0, count);
                        return result;
                    }
                }
                if (waited >= timeoutMs)
                {
                    throw new TimeoutException("HID read timed out after " + timeoutMs + " ms");
                }
                if (!dev.Reading)
                {
                    throw new InvalidOperationException("HID read failed: device disconnected");
                }
                Thread.Sleep(sleepStep);
                waited += sleepStep;
            }
        }

        public static void Close(string deviceId)
        {
            lock (devicesLock)
            {
                if (!devices.TryGetValue(deviceId, out HidDevice dev)) return;
                devices.Remove(deviceId);
                dev.Closing = true;
                CancelIoEx(dev.Handle, IntPtr.Zero);
                CloseHandle(dev.Handle);
                dev.Handle = IntPtr.Zero;
            }
        }

        public static bool IsOpen(string deviceId)
        {
            lock (devicesLock)
            {
                return devices.ContainsKey(deviceId);
            }
        }

        private static HidDevice GetOpen(string deviceId)
        {
            lock (devicesLock)
            {
                if (devices.TryGetValue(deviceId, out HidDevice dev)) return dev;
            }
            throw new InvalidOperationException("HID device not connected: " + deviceId);
        }
    }
}
