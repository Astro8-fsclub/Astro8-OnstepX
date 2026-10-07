using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using HidSharp;

namespace OnStepHID
{
    /// <summary>驱动设置对话框：选择/测试 HID 转串口设备，校验序列号，保存 VID/PID 与期望序列号。</summary>
    [ComVisible(false)]
    public sealed class SetupDialog : Form
    {
        private ComboBox _deviceList;
        private Button _scanBtn;
        private TextBox _vidBox, _pidBox, _serialBox;
        private Button _useVidPidBtn, _testBtn, _okBtn, _cancelBtn;
        private Label _resultLabel;
        private Label _vidLabel, _pidLabel, _serialLabel;

        public SetupDialog()
        {
            int vid = DriverSettings.GetInt("Vid", DriverSettings.DefaultVid);
            int pid = DriverSettings.GetInt("Pid", DriverSettings.DefaultPid);
            string serial = DriverSettings.GetString("SerialNumber", DriverSettings.DefaultSerial);

            Text = "ASTRO8-OnstepX 驱动设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(540, 310);

            _deviceList = new ComboBox { Left = 14, Top = 40, Width = 390, DropDownStyle = ComboBoxStyle.DropDownList };
            _scanBtn = new Button { Text = "扫描设备", Left = 414, Top = 38, Width = 110 };
            _scanBtn.Click += (s, e) => ScanDevices();

            _vidLabel = new Label { Text = "VID (十六进制)", Left = 14, Top = 78, Width = 110 };
            _vidBox = new TextBox { Left = 130, Top = 74, Width = 90, Text = vid.ToString("X4") };
            _pidLabel = new Label { Text = "PID (十六进制)", Left = 230, Top = 78, Width = 100 };
            _pidBox = new TextBox { Left = 336, Top = 74, Width = 90, Text = pid.ToString("X4") };
            _useVidPidBtn = new Button { Text = "使用此 VID/PID", Left = 434, Top = 72, Width = 90 };
            _useVidPidBtn.Click += (s, e) => ApplyVidPidToSelection();

            _serialLabel = new Label { Text = "序列号验证", Left = 14, Top = 108, Width = 110 };
            _serialBox = new TextBox { Left = 130, Top = 104, Width = 150, Text = serial };
            var _serialHint = new Label { Text = "留空或 * 表示跳过", Left = 288, Top = 108, Width = 150, ForeColor = Color.Gray };

            _testBtn = new Button { Text = "测试连接（握手 :GVP#）", Left = 14, Top = 140, Width = 210 };
            _testBtn.Click += (s, e) => TestConnection();

            _resultLabel = new Label { Left = 14, Top = 172, Width = 510, Height = 86, AutoSize = false, ForeColor = Color.FromArgb(40, 60, 80) };

            _okBtn = new Button { Text = "确定", Left = 340, Top = 266, Width = 80, DialogResult = DialogResult.OK };
            _cancelBtn = new Button { Text = "取消", Left = 434, Top = 266, Width = 80, DialogResult = DialogResult.Cancel };
            _okBtn.Click += (s, e) => Save();

            Controls.AddRange(new Control[] { _deviceList, _scanBtn, _vidLabel, _vidBox, _pidLabel, _pidBox, _useVidPidBtn, _serialLabel, _serialBox, _serialHint, _testBtn, _resultLabel, _okBtn, _cancelBtn });
            AcceptButton = _okBtn;
            CancelButton = _cancelBtn;
            ScanDevices();
        }

        /// <summary>按当前 VID/PID/序列号 过滤系统 HID 设备，只有完全匹配的才进入下拉列表。</summary>
        private void ScanDevices()
        {
            _deviceList.Items.Clear();
            int vid, pid;
            bool vidOk = TryParseHex(_vidBox.Text, out vid);
            bool pidOk = TryParseHex(_pidBox.Text, out pid);
            string expected = (_serialBox.Text ?? "").Trim();
            bool skipSerial = expected.Length == 0 || expected == "*";

            try
            {
                int count = 0;
                foreach (var d in HidTransport.Scan(0))
                {
                    if (vidOk && d.VendorID != vid) continue;
                    if (pidOk && d.ProductID != pid) continue;
                    if (!skipSerial)
                    {
                        string sn = HidTransport.GetSerialNumberSafe(d);
                        if (!string.Equals(sn, expected, StringComparison.OrdinalIgnoreCase)) continue;
                    }
                    string snShow = HidTransport.GetSerialNumberSafe(d);
                    string name = string.Format("0x{0:X4}:0x{1:X4}  {2}  [S/N: {3}]",
                        d.VendorID, d.ProductID, HidTransport.GetProductNameSafe(d), snShow.Length == 0 ? "-" : snShow);
                    _deviceList.Items.Add(name);
                    count++;
                }
                if (count == 0)
                {
                    _resultLabel.Text = "未找到 ASTRO8-OnstepX。请确认设备已连接，且 VID、PID、序列号 与设备实际值一致，然后点击“扫描设备”刷新重试。";
                }
                else
                {
                    _resultLabel.Text = string.Format("找到 {0} 台匹配的 ASTRO8-OnstepX 设备。", count);
                }
                if (_deviceList.Items.Count > 0) _deviceList.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                _resultLabel.Text = "扫描失败：" + ex.Message;
            }
        }

        /// <summary>按输入框里的 VID/PID/序列号 重新扫描过滤（与“扫描设备”相同）。</summary>
        private void ApplyVidPidToSelection()
        {
            ScanDevices();
        }

        private void TestConnection()
        {
            int vid, pid;
            if (!TryParseHex(_vidBox.Text, out vid) || !TryParseHex(_pidBox.Text, out pid))
            {
                _resultLabel.Text = "VID/PID 格式错误。";
                return;
            }
            string expected = (_serialBox.Text ?? "").Trim();
            _testBtn.Enabled = false;
            try
            {
                using (var hid = new HidTransport())
                {
                    hid.Open(vid, pid);
                    string actual = (hid.SerialNumber ?? "").Trim();

                    string serialLine;
                    if (expected.Length == 0 || expected == "*")
                    {
                        serialLine = "序列号验证：已跳过";
                    }
                    else if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        serialLine = string.Format("序列号验证：通过（{0}）", actual);
                    }
                    else
                    {
                        serialLine = string.Format("序列号验证：失败（期望 {0}，实际 {1}）",
                            expected, actual.Length == 0 ? "(空)" : actual);
                        throw new InvalidOperationException(serialLine);
                    }

                    var proto = new OnStepProtocol(hid);
                    string info = proto.Handshake();
                    _resultLabel.Text = "连接成功，固件：" + info +
                        Environment.NewLine + "设备：" + hid.ProductName + "  S/N: " + (actual.Length == 0 ? "(空)" : actual) +
                        Environment.NewLine + serialLine;
                }
            }
            catch (Exception ex)
            {
                _resultLabel.Text = "测试失败：" + ex.Message;
            }
            finally
            {
                _testBtn.Enabled = true;
            }
        }

        private void Save()
        {
            int vid, pid;
            if (TryParseHex(_vidBox.Text, out vid) && TryParseHex(_pidBox.Text, out pid))
            {
                DriverSettings.SetInt("Vid", vid);
                DriverSettings.SetInt("Pid", pid);
            }
            DriverSettings.SetString("SerialNumber", _serialBox.Text ?? "");
        }

        private static bool TryParseHex(string s, out int v)
        {
            return int.TryParse((s ?? "").Trim(), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out v);
        }
    }
}
