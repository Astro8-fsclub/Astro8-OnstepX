using System;
using Microsoft.Win32;

namespace OnStepHID
{
    /// <summary>
    /// 驱动设置存储 + ASCOM Chooser 注册。
    /// 与官方驱动一致，设置在 ASCOM Profile 的注册表路径：
    ///   HKLM\SOFTWARE\ASCOM\Telescope Drivers\{ProgId}
    ///   （32 位视图 WOW6432Node 同样写入，ASCOM Chooser 为 32 位 COM 组件，读取 32 位视图）
    /// 键的默认值 = 驱动描述，Chooser 据此枚举出驱动。
    /// </summary>
    public static class DriverSettings
    {
        public const string ProgId = "ASCOM.OnStepHID.Telescope";
        public const string DriverDescription = "ASTRO8-OnstepX";
        private const string AscomRoot32 = @"SOFTWARE\WOW6432Node\ASCOM\Telescope Drivers\" + ProgId;
        private const string AscomRoot64 = @"SOFTWARE\ASCOM\Telescope Drivers\" + ProgId;

        public const int DefaultVid = 0x1A86; // 厂商 VID
        public const int DefaultPid = 0x55D4; // 常见出厂 PID；如被改过请在设置里改
        public const string DefaultSerial = "A8-0001"; // 期望的芯片序列号（防止同 VID/PID 多设备冲突）

        // ---------- 读写设置（双视图，优先 32 位） ----------

        public static string GetString(string name, string defaultValue)
        {
            object v = ReadValue(name);
            if (v == null) return defaultValue;
            return v.ToString();
        }

        public static void SetString(string name, string value)
        {
            WriteValue(name, value);
            WriteValue32(name, value);
        }

        public static int GetInt(string name, int defaultValue)
        {
            object v = ReadValue(name);
            if (v == null) return defaultValue;
            if (v is int) return (int)v;
            int i;
            if (int.TryParse(v.ToString(), out i)) return i;
            return defaultValue;
        }

        public static double GetDouble(string name, double defaultValue)
        {
            object v = ReadValue(name);
            if (v == null) return defaultValue;
            double d;
            if (double.TryParse(v.ToString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out d)) return d;
            return defaultValue;
        }

        public static void SetInt(string name, int value)
        {
            WriteValue(name, value);
            WriteValue32(name, value);
        }

        public static void SetDouble(string name, double value)
        {
            WriteValue(name, value);
            WriteValue32(name, value);
        }

        private static object ReadValue(string name)
        {
            // 优先 32 位视图（Chooser 写入的），再试 64 位视图
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(AscomRoot32, false))
                    if (k != null) return k.GetValue(name);
            }
            catch { }
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(AscomRoot64, false))
                    if (k != null) return k.GetValue(name);
            }
            catch { }
            return null;
        }

        private static void WriteValue(string name, object value)
        {
            try
            {
                using (var k = Registry.LocalMachine.CreateSubKey(AscomRoot64))
                    if (k != null) k.SetValue(name, value);
            }
            catch { /* 非管理员时静默，32 位视图仍会写入 */ }
        }

        private static void WriteValue32(string name, object value)
        {
            try
            {
                using (var k = Registry.LocalMachine.CreateSubKey(AscomRoot32))
                    if (k != null) k.SetValue(name, value);
            }
            catch { }
        }

        // ---------- ASCOM Profile 键写入/删除（由 Telescope 的 ComRegister/Unregister 回调调用） ----------
        // 注意：regasm 只会为"被注册的 COM 类"调用 [ComRegisterFunction]，静态类不会被调用，
        // 因此回调放在 Telescope 类上，这里只提供纯逻辑。

        public static void WriteProfileKeys()
        {
            try { WriteProfileKey(AscomRoot64); } catch { }
            try { WriteProfileKey(AscomRoot32); } catch { }
        }

        private static void WriteProfileKey(string path)
        {
            using (var k = Registry.LocalMachine.CreateSubKey(path))
            {
                if (k == null) return;
                k.SetValue(null, DriverDescription); // (默认) 值 = 描述，Chooser 枚举用
                if (k.GetValue("InterfaceVersion") == null) k.SetValue("InterfaceVersion", "3");
            }
        }

        public static void DeleteProfileKeys()
        {
            try { Registry.LocalMachine.DeleteSubKeyTree(AscomRoot64, false); } catch { }
            try { Registry.LocalMachine.DeleteSubKeyTree(AscomRoot32, false); } catch { }
        }
    }
}
