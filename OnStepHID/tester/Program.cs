using System;
using OnStepHID;

namespace OnStepHIDTester
{
    /// <summary>
    /// HID 转串口 + OnStep 硬件联调小工具（无需安装 ASCOM 即可先验证接线与协议）。
    /// 用法：
    ///   OnStepHIDTester list                    列出系统中的 HID 设备
    ///   OnStepHIDTester test <VID> <PID>        打开设备并做 OnStep 握手（:GVP# / :GVM#）
    ///   OnStepHIDTester cmd <VID> <PID> <命令>   发送任意 OnStep 命令并打印应答
    /// VID/PID 为十六进制，如 1A86 55D4
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0) { Usage(); return 1; }

                switch (args[0].ToLowerInvariant())
                {
                    case "list":
                        ListDevices();
                        return 0;
                    case "test":
                        Require(args, 3);
                        using (var hid = OpenHid(args))
                        {
                            Console.WriteLine("设备已打开：" + hid.ProductName + "  S/N: " +
                                (string.IsNullOrEmpty(hid.SerialNumber) ? "(空)" : hid.SerialNumber));
                            if (args.Length >= 4 && args[3].Length > 0 && args[3] != "*")
                            {
                                string expected = args[3];
                                string actual = (hid.SerialNumber ?? "").Trim();
                                if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                                    Console.WriteLine("序列号验证：通过（" + actual + "）");
                                else
                                {
                                    Console.WriteLine("序列号验证：失败（期望 " + expected + "，实际 " +
                                        (actual.Length == 0 ? "(空)" : actual) + "）");
                                    return 2;
                                }
                            }
                            var proto = new OnStepProtocol(hid);
                            Console.WriteLine("握手成功，固件：" + proto.Handshake());
                        }
                        return 0;
                    case "cmd":
                        Require(args, 4);
                        using (var hid = OpenHid(args))
                        {
                            var proto = new OnStepProtocol(hid);
                            proto.Handshake();
                            Console.WriteLine("应答: " + proto.RawCommand(args[3]));
                        }
                        return 0;
                    default:
                        Usage();
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("错误：" + ex.Message);
                Console.WriteLine(ex.InnerException == null ? "" : "  详情: " + ex.InnerException.Message);
                return 2;
            }
        }

        private static HidTransport OpenHid(string[] args)
        {
            int vid = ParseHex(args[1]);
            int pid = ParseHex(args[2]);
            var hid = new HidTransport();
            try { hid.Open(vid, pid); }
            catch { hid.Dispose(); throw; }
            return hid;
        }

        private static void ListDevices()
        {
            Console.WriteLine("系统 HID 设备：");
            foreach (var d in HidTransport.Scan(0))
            {
                string sn = HidTransport.GetSerialNumberSafe(d);
                Console.WriteLine("  VID=0x{0:X4} PID=0x{1:X4}  {2}  [S/N: {3}]",
                    d.VendorID, d.ProductID, HidTransport.GetProductNameSafe(d), sn.Length == 0 ? "-" : sn);
            }
            Console.WriteLine("提示：目标设备厂商 VID 通常为 1A86；序列号用于区分同 VID/PID 的多台设备。");
        }

        private static int ParseHex(string s)
        {
            int v;
            if (!int.TryParse(s, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out v))
                throw new ArgumentException("无法解析十六进制数：" + s);
            return v;
        }

        private static void Require(string[] args, int n)
        {
            if (args.Length < n) { Usage(); Environment.Exit(1); }
        }

        private static void Usage()
        {
            Console.WriteLine("用法：");
            Console.WriteLine("  OnStepHIDTester list");
            Console.WriteLine("  OnStepHIDTester test <VID> <PID> [期望序列号]");
            Console.WriteLine("  OnStepHIDTester cmd <VID> <PID> <命令>");
            Console.WriteLine("示例：");
            Console.WriteLine("  OnStepHIDTester test 1A86 55D4 A8-0001");
            Console.WriteLine("  OnStepHIDTester cmd 1A86 55D4 :GVP#");
        }
    }
}
