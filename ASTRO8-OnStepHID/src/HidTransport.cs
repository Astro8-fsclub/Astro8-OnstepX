using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using HidSharp;

namespace ASTRO8_OnStepHID
{
    /// <summary>
    /// HID 转串口芯片的 USB 传输层。
    /// 协议要点（厂商官方说明）：
    ///   - 芯片作为免驱 USB HID 设备出现，USB 包长度固定（通常 32 字节，可用工具配置）；
    ///   - 报文首字节 = 本次有效串口数据长度，其后才是真正的串口数据；
    ///   - 例如写 31 字节数据，组包为：0x1F, data[0..30]，共 32 字节。
    /// 读方向同理：收到的 HID 报表首字节为有效长度。
    /// </summary>
    [ComVisible(false)]
    public sealed class HidTransport : ITransport
    {
        private readonly object _sync = new object();
        private HidDevice _device;
        private HidStream _stream;
        private int _reportLen = 32; // 默认 32 字节报表；打开时按设备描述符校正

        /// <summary>当前打开的 VID。</summary>
        public int Vid { get; private set; }
        /// <summary>当前打开的 PID。</summary>
        public int Pid { get; private set; }
        /// <summary>设备产品名（用于显示）。</summary>
        public string ProductName { get; private set; }
        /// <summary>设备序列号（iSerialNumber 字符串描述符，用于区分同 VID/PID 的多台设备）。</summary>
        public string SerialNumber { get; private set; }
        /// <summary>是否已打开。</summary>
        public bool IsOpen { get { lock (_sync) { return _stream != null; } } }

        /// <summary>ITransport：连接描述（"HID VID:PID"）。</summary>
        public string Description
        {
            get
            {
                return _stream == null
                    ? string.Format("HID {0:X4}:{1:X4}", Vid, Pid)
                    : string.Format("HID {0:X4}:{1:X4} {2}", Vid, Pid, ProductName);
            }
        }

        /// <summary>ITransport：按驱动设置（Profile 中的 Vid/Pid）打开。</summary>
        public void Open()
        {
            Open(DriverSettings.GetInt("Vid", DriverSettings.DefaultVid),
                 DriverSettings.GetInt("Pid", DriverSettings.DefaultPid));
        }

        /// <summary>
        /// 扫描系统中的 HID 设备。vid 为 0 时返回全部。
        /// </summary>
        public static HidDevice[] Scan(int vid = 0)
        {
            var all = DeviceList.Local.GetHidDevices().ToArray();
            if (vid == 0) return all;
            return all.Where(d => d.VendorID == vid).ToArray();
        }

        /// <summary>
        /// 按 VID/PID 打开设备。
        /// </summary>
        public void Open(int vid, int pid)
        {
            Close();

            var device = DeviceList.Local.GetHidDeviceOrNull(vendorID: vid, productID: pid);
            if (device == null)
                throw new InvalidOperationException(
                    string.Format("未找到 HID 设备 VID=0x{0:X4} PID=0x{1:X4}。请确认：①设备已插入 USB；②VID/PID 是否被配置工具改过；③Windows 设备管理器中设备是否正常。", vid, pid));

            HidStream stream = null;
            try
            {
                stream = device.Open();
                stream.ReadTimeout = 1500;
                stream.WriteTimeout = 1500;
            }
            catch (Exception ex)
            {
                if (stream != null) stream.Dispose();
                throw new InvalidOperationException(
                    string.Format("无法打开 HID 设备 {0} ({1:X4}:{2:X4})：{3}。若提示被占用，请关闭其它正在使用该设备的软件。",
                        GetProductNameSafe(device), device.VendorID, device.ProductID, ex.Message), ex);
            }

            // 取设备实际报表长度（常见 32；部分固件 64）
            try
            {
                int inLen = device.GetMaxInputReportLength();
                int outLen = device.GetMaxOutputReportLength();
                int m = Math.Max(inLen, outLen);
                if (m >= 2 && m <= 512) _reportLen = m;
            }
            catch { /* 取不到就用默认 32 */ }

            _device = device;
            _stream = stream;
            Vid = vid;
            Pid = pid;
            ProductName = GetProductNameSafe(device);
            SerialNumber = GetSerialNumberSafe(device);
        }

        /// <summary>打开后立即调用，把芯片缓冲区内残留的旧数据排空（避免干扰握手）。</summary>
        public void FlushPending()
        {
            lock (_sync)
            {
                if (_stream == null) return;
                try
                {
                    byte[] buf = new byte[_reportLen];
                    DateTime deadline = DateTime.UtcNow.AddMilliseconds(300);
                    while (DateTime.UtcNow < deadline)
                    {
                        int rd = _stream.Read(buf, 0, buf.Length);
                        if (rd <= 0) break;
                    }
                }
                catch { /* 超时或没有数据，忽略 */ }
            }
        }

        /// <summary>
        /// 发送串口数据。自动按 HID 转串口帧格式打包：首字节=长度。
        /// </summary>
        public void Write(byte[] data)
        {
            if (data == null) throw new ArgumentNullException("data");
            int n = Math.Min(data.Length, _reportLen - 1);
            byte[] report = new byte[_reportLen];
            report[0] = (byte)n;
            Array.Copy(data, 0, report, 1, n);
            lock (_sync)
            {
                EnsureOpen();
                _stream.Write(report);
            }
        }

        /// <summary>
        /// 读取数据直到遇到 '#'（OnStep/LX200 应答结束符）或超时。
        /// 返回的字符串包含结尾 '#'。
        /// </summary>
        public string ReadUntilHash(int timeoutMs = 1500)
        {
            StringBuilder sb = new StringBuilder();
            byte[] buf = new byte[_reportLen];
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            lock (_sync)
            {
                EnsureOpen();
                while (DateTime.UtcNow < deadline)
                {
                    int rd = 0;
                    try { rd = _stream.Read(buf, 0, buf.Length); }
                    catch (TimeoutException) { break; }
                    catch (IOException) { break; }
                    if (rd <= 0) break;

                    // HID 转串口帧：首字节 = 有效数据长度
                    int len = buf[0];
                    if (len <= 0 || len > rd - 1) len = rd - 1;
                    for (int i = 1; i <= len; i++)
                    {
                        char c = (char)buf[i];
                        if (c == '\0') continue;
                        sb.Append(c);
                        if (c == '#') return sb.ToString();
                    }
                }
            }

            if (sb.Length == 0)
                throw new TimeoutException("HID 读取超时：未收到任何应答。请检查 HID 转串口芯片与 OnStep 的 TX/RX 交叉接线及波特率设置。");
            return sb.ToString();
        }

        private void EnsureOpen()
        {
            if (_stream == null) throw new InvalidOperationException("HID 设备未打开。");
        }

        /// <summary>兼容 HidSharp 2.1：GetProductName() 取代过时的 ProductName 属性。</summary>
        public static string GetProductNameSafe(HidDevice d)
        {
            try
            {
                string n = d.GetProductName();
                if (!string.IsNullOrEmpty(n)) return n;
            }
            catch { }
            try
            {
                string f = d.GetFriendlyName();
                if (!string.IsNullOrEmpty(f)) return f;
            }
            catch { }
            return string.Format("{0:X4}:{1:X4}", d.VendorID, d.ProductID);
        }

        /// <summary>兼容 HidSharp 2.1：GetSerialNumber() 取代过时的 SerialNumber 属性。</summary>
        public static string GetSerialNumberSafe(HidDevice d)
        {
            try { return d.GetSerialNumber() ?? ""; }
            catch { return ""; }
        }

        /// <summary>关闭设备。</summary>
        public void Close()
        {
            lock (_sync)
            {
                if (_stream != null) { try { _stream.Dispose(); } catch { } _stream = null; }
                _device = null;
            }
        }

        public void Dispose() { Close(); }
    }
}
