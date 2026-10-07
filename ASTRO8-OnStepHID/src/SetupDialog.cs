using System;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ASCOM.DeviceInterface;

namespace ASTRO8_OnStepHID
{
    /// <summary>
    /// 驱动属性设置对话框（ASCOM SetupDialog），界面中英双语、各占一排（两行显示），字体 8.25pt，窗体 800×785：
    /// 1. 连接方式：HID（VID/PID/序列号，精确匹配）或 WiFi（地址/端口，默认 192.168.0.1:9998）；
    /// 2. 站点与时间：经度、纬度、UTC offset、本地时间、本地日期（默认回读硬件当前设置，可手动修改）；
    /// 3. 限制：驱动侧 RA/Dec GOTO 软限制 + 硬件地平/天顶/子午线限制（回读/写入）；
    /// 4. 速度：当前 GOTO 速度（回读）、移动速率档、跟踪速率。
    /// "回读硬件设置"按钮：临时连接 OnStep，把所有可回读字段填进页面。
    /// </summary>
    [ComVisible(false)]
    public sealed class SetupDialog : Form
    {
        // 连接方式
        private RadioButton _hidRadio, _wifiRadio;
        private Panel _hidPanel, _wifiPanel;
        private TextBox _vidBox, _pidBox, _serialBox, _hostBox, _portBox;
        private ComboBox _deviceList;
        private Button _scanBtn, _testBtn;
        private Label _resultLabel;

        // 站点与时间
        private TextBox _lonBox, _latBox, _utcBox, _timeBox, _dateBox;
        private string _timeOriginal, _dateOriginal;

        // 限制（仅硬件：地平/天顶/子午线）
        private TextBox _horizonBox, _overheadBox, _eastMerBox, _westMerBox;

        // 速度
        private Label _slewSpeedLabel;
        private ComboBox _slewPresetCombo, _trackRateCombo;

        private Button _readBtn, _okBtn, _cancelBtn;

        public SetupDialog()
        {
            string mode = DriverSettings.GetConnectionMode();
            int vid = DriverSettings.GetInt("Vid", DriverSettings.DefaultVid);
            int pid = DriverSettings.GetInt("Pid", DriverSettings.DefaultPid);
            string serial = DriverSettings.GetString("SerialNumber", DriverSettings.DefaultSerial);
            string host = DriverSettings.GetHost();
            int port = DriverSettings.GetPort();

            Text = "ASTRO8-OnstepX 驱动设置 / Driver Setup";
            // 绝对像素布局：禁用 DPI 自动缩放，字号用像素单位，任何显示缩放下窗体都是 800×712、文字 11px，所见即所得。
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Microsoft Sans Serif", 11F, GraphicsUnit.Pixel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(800, 712);

            // ===== 连接方式 / Connection =====
            var connBox = new GroupBox { Text = "连接方式 / Connection", Left = 10, Top = 8, Width = 780, Height = 126 };
            _hidRadio = new RadioButton { Text = "HID（USB 转串口芯片 / USB-Serial chip）", Left = 15, Top = 20, Width = 350, Checked = mode != "WIFI" };
            _wifiRadio = new RadioButton { Text = "WiFi（TCP）", Left = 380, Top = 20, Width = 200, Checked = mode == "WIFI" };
            _hidRadio.CheckedChanged += (s, e) => SyncPanels();
            _wifiRadio.CheckedChanged += (s, e) => SyncPanels();

            _hidPanel = new Panel { Left = 10, Top = 44, Width = 760, Height = 78 };
            var vLabel = new Label { Text = "VID", Left = 15, Top = 6, Width = 45 };
            _vidBox = new TextBox { Left = 65, Top = 4, Width = 100, Text = vid.ToString("X4") };
            var pLabel = new Label { Text = "PID", Left = 180, Top = 6, Width = 45 };
            _pidBox = new TextBox { Left = 230, Top = 4, Width = 100, Text = pid.ToString("X4") };
            var sLabel = new Label { Text = "序列号\nSerial", Left = 345, Top = 4, Width = 110, Height = 28, AutoSize = false };
            _serialBox = new TextBox { Left = 460, Top = 6, Width = 285, Text = serial };
            _deviceList = new ComboBox { Left = 15, Top = 38, Width = 575, DropDownStyle = ComboBoxStyle.DropDownList };
            _scanBtn = new Button { Text = "扫描设备\nScan", Left = 600, Top = 34, Width = 150, Height = 38 };
            _scanBtn.Click += (s, e) => ScanDevices();
            _hidPanel.Controls.AddRange(new Control[] { vLabel, _vidBox, pLabel, _pidBox, sLabel, _serialBox, _deviceList, _scanBtn });

            _wifiPanel = new Panel { Left = 10, Top = 44, Width = 760, Height = 78 };
            var hLabel = new Label { Text = "地址\nHost", Left = 15, Top = 4, Width = 80, Height = 28, AutoSize = false };
            _hostBox = new TextBox { Left = 100, Top = 6, Width = 220, Text = host };
            var ptLabel = new Label { Text = "端口\nPort", Left = 350, Top = 4, Width = 80, Height = 28, AutoSize = false };
            _portBox = new TextBox { Left = 435, Top = 6, Width = 100, Text = port.ToString() };
            var wifiHint = new Label
            {
                Text = "OnStep WiFi 原始 TCP 转发；默认 192.168.0.1:9998。\r\nOnStep WiFi must forward raw TCP; default 192.168.0.1:9998.",
                Left = 15, Top = 38, Width = 740, Height = 36, AutoSize = false, ForeColor = Color.Gray
            };
            _wifiPanel.Controls.AddRange(new Control[] { hLabel, _hostBox, ptLabel, _portBox, wifiHint });
            connBox.Controls.AddRange(new Control[] { _hidRadio, _wifiRadio, _hidPanel, _wifiPanel });

            // ===== 测试连接 / Test =====
            _testBtn = new Button { Text = "测试连接（握手 :GVP#）\nTest Connection", Left = 10, Top = 142, Width = 320, Height = 44 };
            _testBtn.Click += (s, e) => TestConnection();
            _resultLabel = new Label { Left = 340, Top = 140, Width = 450, Height = 102, AutoSize = false, ForeColor = Color.FromArgb(40, 60, 80) };

            // ===== 站点与时间 / Site & Time =====
            var siteBox = new GroupBox
            {
                Text = "站点与时间 / Site & Time（默认回读硬件，可手动修改 / reads hardware, editable）",
                Left = 10, Top = 250, Width = 780, Height = 160
            };
            var lonLabel = new Label { Text = "经度\nLongitude (°)", Left = 15, Top = 24, Width = 135, Height = 30, AutoSize = false };
            _lonBox = new TextBox { Left = 155, Top = 28, Width = 140 };
            var latLabel = new Label { Text = "纬度\nLatitude (°)", Left = 330, Top = 24, Width = 135, Height = 30, AutoSize = false };
            _latBox = new TextBox { Left = 470, Top = 28, Width = 140 };
            var utcLabel = new Label { Text = "UTC offset", Left = 15, Top = 60, Width = 110 };
            _utcBox = new TextBox { Left = 130, Top = 58, Width = 110 };
            var timeLabel = new Label { Text = "本地时间\nLocal Time", Left = 270, Top = 58, Width = 150, Height = 30, AutoSize = false };
            _timeBox = new TextBox { Left = 425, Top = 60, Width = 140 };
            var dateLabel = new Label { Text = "本地日期\nLocal Date", Left = 15, Top = 94, Width = 135, Height = 30, AutoSize = false };
            _dateBox = new TextBox { Left = 155, Top = 96, Width = 110 };
            var siteHint = new Label
            {
                Text = "时间 HH:MM:SS、日期 MM/DD/YY（24 小时制）。\r\nTime HH:MM:SS, date MM/DD/YY (24h).",
                Left = 280, Top = 94, Width = 490, Height = 56, AutoSize = false, ForeColor = Color.Gray
            };
            siteBox.Controls.AddRange(new Control[] { lonLabel, _lonBox, latLabel, _latBox, utcLabel, _utcBox, timeLabel, _timeBox, dateLabel, _dateBox, siteHint });

            // ===== 限制 / Limits（仅硬件限制，回读/写入 OnStep；驱动侧不再设 RA/Dec 软限制） =====
            var limBox = new GroupBox { Text = "限制 / Limits", Left = 10, Top = 418, Width = 780, Height = 150 };
            var hwTitle = new Label
            {
                Text = "硬件限制（回读/写入 OnStep）\r\nHardware limits (read/write OnStep)",
                Left = 15, Top = 16, Width = 750, Height = 34, AutoSize = false,
                Font = new Font(Font, FontStyle.Bold), ForeColor = Color.FromArgb(60, 80, 100)
            };
            var hzLabel = new Label { Text = "地平\nHorizon°", Left = 30, Top = 52, Width = 100, Height = 30, AutoSize = false };
            _horizonBox = new TextBox { Left = 135, Top = 56, Width = 75 };
            var ovLabel = new Label { Text = "天顶\nOverhead°", Left = 245, Top = 52, Width = 110, Height = 30, AutoSize = false };
            _overheadBox = new TextBox { Left = 355, Top = 56, Width = 75 };
            var esLabel = new Label { Text = "东子午线\nE.Mer(min)", Left = 475, Top = 52, Width = 135, Height = 30, AutoSize = false };
            _eastMerBox = new TextBox { Left = 615, Top = 56, Width = 75 };
            var wsLabel = new Label { Text = "西子午线\nW.Mer(min)", Left = 30, Top = 86, Width = 135, Height = 30, AutoSize = false };
            _westMerBox = new TextBox { Left = 170, Top = 90, Width = 75 };
            var limHint = new Label
            {
                Text = "GEM: 东/西子午线=RA 范围; Dec 由 OnStep 限位控制\r\nE/W meridian = RA range; Dec governed by OnStep limits",
                Left = 310, Top = 86, Width = 460, Height = 38, AutoSize = false, ForeColor = Color.Gray
            };
            limBox.Controls.AddRange(new Control[] { hwTitle, hzLabel, _horizonBox, ovLabel, _overheadBox, esLabel, _eastMerBox, wsLabel, _westMerBox, limHint });

            // ===== 速度 / Speed =====
            var spdBox = new GroupBox { Text = "速度 / Speed", Left = 10, Top = 576, Width = 780, Height = 76 };
            var spdLabel = new Label { Text = "当前 GOTO 速度\nCurrent speed", Left = 15, Top = 10, Width = 150, Height = 30, AutoSize = false };
            _slewSpeedLabel = new Label { Left = 170, Top = 10, Width = 120, Height = 30, AutoSize = false, ForeColor = Color.Gray, Text = "(未回读)\n(not read)" };
            var presetLabel = new Label { Text = "移动速率档\nMove rate", Left = 320, Top = 10, Width = 120, Height = 30, AutoSize = false };
            _slewPresetCombo = new ComboBox { Left = 445, Top = 12, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            _slewPresetCombo.Items.AddRange(new object[] { "1x (导星/Guide)", "8x (居中/Center)", "20x (寻星/Find)", "48x (快速/Fast)", "半速/Half", "R0", "R1", "R2", "R3", "R4", "R5", "R6", "R7", "R8", "R9" });
            var trackLabel = new Label { Text = "跟踪速率\nTracking", Left = 15, Top = 42, Width = 130, Height = 30, AutoSize = false };
            _trackRateCombo = new ComboBox { Left = 150, Top = 44, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            _trackRateCombo.Items.AddRange(new object[] { "恒星 (Sidereal)", "月球 (Lunar)", "太阳 (Solar)", "King" });
            spdBox.Controls.AddRange(new Control[] { spdLabel, _slewSpeedLabel, presetLabel, _slewPresetCombo, trackLabel, _trackRateCombo });

            // ===== 底部按钮 / Bottom buttons =====
            _readBtn = new Button { Text = "回读硬件设置\nRead hardware", Left = 10, Top = 656, Width = 300, Height = 48 };
            _readBtn.Click += (s, e) => ReadHardwareSettings();
            _okBtn = new Button { Text = "确定\nOK", Left = 560, Top = 656, Width = 120, Height = 48, DialogResult = DialogResult.OK };
            _cancelBtn = new Button { Text = "取消\nCancel", Left = 680, Top = 656, Width = 120, Height = 48, DialogResult = DialogResult.Cancel };
            _okBtn.Click += (s, e) => Save();

            Controls.AddRange(new Control[] { connBox, _testBtn, _resultLabel, siteBox, limBox, spdBox, _readBtn, _okBtn, _cancelBtn });
            AcceptButton = _okBtn;
            CancelButton = _cancelBtn;

            LoadInitialValues();
            SyncPanels();
            ScanDevices();

            // ===== 徽标 / Logos（最后加入连接框，置于连接面板之上，且不与面板/控件重叠）=====
            // OnStep：官方 OnStep ASCOM 驱动属性页原生 logo（蓝齿轮 + OnStep 字标，180x54）
            // ASCOM：官方 OnStep ASCOM 驱动所用 ASCOM 官方标识（48x56，深蓝底 + 星形 + ASCOM）
            // 均提取自官方 ASCOM.OnStep.Telescope.dll 嵌入资源；加载失败时静默跳过，不影响布局。
            var onstepLogo = MakeLogo("ASTRO8_OnStepHID.Resources.onstep_logo.png", "OnStep");
            var ascomLogo = MakeLogo("ASTRO8_OnStepHID.Resources.ascom_logo.png", "ASCOM");
            if (onstepLogo != null) { onstepLogo.Location = new Point(585, 8); onstepLogo.Size = new Size(99, 30); }
            if (ascomLogo != null) { ascomLogo.Location = new Point(694, 6); ascomLogo.Size = new Size(32, 38); }
            if (onstepLogo != null || ascomLogo != null)
                connBox.Controls.AddRange(new Control[] { onstepLogo, ascomLogo });
        }

        /// <summary>从嵌入资源加载徽标图片；失败返回 null（不阻塞对话框）。</summary>
        private static PictureBox MakeLogo(string resourceName, string tooltip)
        {
            try
            {
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (s == null) return null;
                    using (var img = Image.FromStream(s))
                    {
                        // 深拷贝：Image.FromStream 依赖底层流，流释放后位图可能失效，
                        // new Bitmap(img) 生成独立副本，保证绘制正常。
                        var pb = new PictureBox { Image = new Bitmap(img), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
                        var tip = new ToolTip();
                        tip.SetToolTip(pb, tooltip);
                        return pb;
                    }
                }
            }
            catch { return null; }
        }

        /// <summary>从 Profile 载入上次保存值；无记录时给合理默认（UTC offset 用本机时区）。</summary>
        private void LoadInitialValues()
        {
            double lat = DriverSettings.GetLatitude();
            double lon = DriverSettings.GetLongitude();
            int utcMin = DriverSettings.GetUtcOffsetMinutes();
            _latBox.Text = double.IsNaN(lat) ? "" : lat.ToString("0.0###", CultureInfo.InvariantCulture);
            _lonBox.Text = double.IsNaN(lon) ? "" : lon.ToString("0.0###", CultureInfo.InvariantCulture);
            if (utcMin == int.MinValue)
                _utcBox.Text = FormatUtcOffset((int)DateTimeOffset.Now.Offset.TotalMinutes);
            else
                _utcBox.Text = FormatUtcOffset(utcMin);

            _horizonBox.Text = DriverSettings.GetInt(DriverSettings.KHorizonLimit, -1).ToString();
            _overheadBox.Text = DriverSettings.GetInt(DriverSettings.KOverheadLimit, -1).ToString();
            _eastMerBox.Text = DriverSettings.GetInt(DriverSettings.KEastMeridianMin, -1).ToString();
            _westMerBox.Text = DriverSettings.GetInt(DriverSettings.KWestMeridianMin, -1).ToString();

            string preset = DriverSettings.GetString(DriverSettings.KSlewRatePreset, "20x (寻星/Find)");
            int pi = _slewPresetCombo.Items.IndexOf(preset);
            _slewPresetCombo.SelectedIndex = pi >= 0 ? pi : 5; // 默认 R0
            string track = DriverSettings.GetString(DriverSettings.KTrackingRate, "恒星 (Sidereal)");
            int ti = _trackRateCombo.Items.IndexOf(track);
            _trackRateCombo.SelectedIndex = ti >= 0 ? ti : 0;

            _timeBox.Text = "";
            _dateBox.Text = "";
            _timeOriginal = _timeBox.Text;
            _dateOriginal = _dateBox.Text;
        }

        private void SyncPanels()
        {
            _hidPanel.Visible = _hidRadio.Checked;
            _wifiPanel.Visible = _wifiRadio.Checked;
        }

        /// <summary>按当前 VID/PID/序列号过滤系统 HID 设备，只有完全匹配的才进入下拉列表；WiFi 模式下不扫描。</summary>
        private void ScanDevices()
        {
            if (_wifiRadio.Checked) { _deviceList.Items.Clear(); return; }
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
                    _resultLabel.Text = "未找到匹配设备。连接时将使用上方手填的 VID/PID/序列号；序列号留空或填 * 可跳过验证直接连接。请确认设备已连接后点击“扫描设备”刷新。\r\n" +
                                       "No matching device. Connection uses the VID/PID/Serial typed above; leave serial empty (or use *) to skip verification and connect anyway. Click “Scan” to refresh.";
                }
                else
                {
                    _resultLabel.Text = string.Format("找到 {0} 台匹配的 ASTRO8-OnstepX 设备。\r\n{0} matching ASTRO8-OnstepX device(s) found.", count);
                }
                if (_deviceList.Items.Count > 0) _deviceList.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                _resultLabel.Text = "扫描失败 / Scan failed: " + ex.Message;
            }
        }

        /// <summary>按当前选择的连接方式打开一个临时传输层（测试/回读用）。</summary>
        private ITransport OpenTransportForTest()
        {
            if (_wifiRadio.Checked)
            {
                int port;
                if (!int.TryParse(_portBox.Text.Trim(), out port) || port < 1 || port > 65535)
                    throw new InvalidOperationException("端口无效（1-65535）/ Invalid port (1-65535).");
                string host = _hostBox.Text.Trim();
                if (host.Length == 0) throw new InvalidOperationException("WiFi 地址不能为空 / WiFi host must not be empty.");
                var t = new WifiTransport(host, port);
                t.Open();
                return t;
            }
            else
            {
                int vid, pid;
                if (!TryParseHex(_vidBox.Text, out vid) || !TryParseHex(_pidBox.Text, out pid))
                    throw new InvalidOperationException("VID/PID 格式错误（十六进制）/ Invalid VID/PID (hex).");
                string expected = (_serialBox.Text ?? "").Trim();
                var hid = new HidTransport();
                try
                {
                    hid.Open(vid, pid);
                }
                catch { hid.Dispose(); throw; }
                if (expected.Length > 0 && expected != "*")
                {
                    string actual = (hid.SerialNumber ?? "").Trim();
                    if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        hid.Dispose();
                        throw new InvalidOperationException(string.Format(
                            "序列号验证失败：期望 {0}，实际 {1}。 / Serial mismatch: expected {0}, actual {1}.",
                            expected, actual.Length == 0 ? "(空 / empty)" : actual));
                    }
                }
                return hid;
            }
        }

        private void TestConnection()
        {
            _testBtn.Enabled = false;
            try
            {
                using (ITransport t = OpenTransportForTest())
                {
                    var proto = new OnStepProtocol(t);
                    string info = proto.Handshake();
                    string where = _wifiRadio.Checked
                        ? string.Format("WiFi {0}:{1}", _hostBox.Text.Trim(), _portBox.Text.Trim())
                        : "HID " + t.Description;
                    _resultLabel.Text = "连接成功（" + where + "）\r\n固件 / Firmware: " + info;
                }
            }
            catch (Exception ex)
            {
                _resultLabel.Text = "测试失败 / Test failed: " + ex.Message;
            }
            finally
            {
                _testBtn.Enabled = true;
            }
        }

        /// <summary>
        /// 临时连接 OnStep，把所有可回读字段（经度/纬度/UTC offset/本地时间日期/地平/天顶/子午线/
        /// Axis2 Dec 限位/当前 GOTO 速度/跟踪速率）填进页面；失败的字段保留原值并计数提示。
        /// </summary>
        private void ReadHardwareSettings()
        {
            _readBtn.Enabled = false;
            int ok = 0, fail = 0;
            try
            {
                using (ITransport t = OpenTransportForTest())
                {
                    var proto = new OnStepProtocol(t);
                    string info = proto.Handshake();

                    // 站点与时间（本地日期时间用 OnStep 标准 :GL#/:GC# 回读，兼容 10.28q 固件）
                    SetIfOk(() => _lonBox.Text = proto.GetLongitude().ToString("0.0###", CultureInfo.InvariantCulture), ref ok, ref fail);
                    SetIfOk(() => _latBox.Text = proto.GetLatitude().ToString("0.0###", CultureInfo.InvariantCulture), ref ok, ref fail);
                    SetIfOk(() => _utcBox.Text = FormatUtcOffset((int)proto.GetUtcOffset().TotalMinutes), ref ok, ref fail);
                    SetIfOk(() =>
                    {
                        DateTime local = proto.GetLocalDateTime();
                        _timeBox.Text = local.ToString("HH:mm:ss");
                        _dateBox.Text = local.ToString("MM/dd/yy");
                    }, ref ok, ref fail);

                    // 硬件限制
                    SetIfOk(() => _horizonBox.Text = proto.GetHorizonLimit().ToString(), ref ok, ref fail);
                    SetIfOk(() => _overheadBox.Text = proto.GetOverheadLimit().ToString(), ref ok, ref fail);
                    SetIfOk(() => _eastMerBox.Text = proto.GetEastMeridianLimitMinutes().ToString(), ref ok, ref fail);
                    SetIfOk(() => _westMerBox.Text = proto.GetWestMeridianLimitMinutes().ToString(), ref ok, ref fail);

                    string axisRef = "";
                    try
                    {
                        axisRef = string.Format(
                            "Axis1(RA) 轴限位: min {0}° / max {1}h  |  Axis2(Dec) 限位: {2}°..{3}°\r\nAxis1(RA) limits: min {0}° / max {1}h  |  Axis2(Dec) limits: {2}°..{3}°",
                            proto.GetAxis1MinLimitDeg(),
                            proto.GetAxis1MaxLimitHours().ToString("0.#", CultureInfo.InvariantCulture),
                            proto.GetAxis2MinLimitDeg(), proto.GetAxis2MaxLimitDeg());
                    }
                    catch { }

                    // 速度
                    SetIfOk(() => _slewSpeedLabel.Text = proto.GetSlewSpeedDegPerSec().ToString("0.00", CultureInfo.InvariantCulture) + " deg/s", ref ok, ref fail);
                    SetIfOk(() =>
                    {
                        DriveRates r = proto.GetTrackingRate();
                        string s = r == DriveRates.driveLunar ? "月球 (Lunar)" : r == DriveRates.driveSolar ? "太阳 (Solar)" : r == DriveRates.driveKing ? "King" : "恒星 (Sidereal)";
                        _trackRateCombo.SelectedItem = s;
                    }, ref ok, ref fail);

                    _resultLabel.Text = string.Format("回读完成：成功 {0} 项，失败 {1} 项。固件 / Firmware: {2}\r\n" +
                        "Read-back done: {0} OK, {1} failed. {3}{4}",
                        ok, fail, info,
                        Environment.NewLine + "可修改后点“确定 / OK”保存（连接时自动下发）。 / Edit then click OK; applied on connect.",
                        axisRef.Length > 0 ? Environment.NewLine + axisRef : "");
                }
            }
            catch (Exception ex)
            {
                _resultLabel.Text = "回读失败 / Read-back failed: " + ex.Message;
            }
            finally
            {
                _readBtn.Enabled = true;
            }
        }

        private static void SetIfOk(Action act, ref int ok, ref int fail)
        {
            try { act(); ok++; }
            catch { fail++; }
        }

        /// <summary>保存全部字段到 Profile；若时间/日期被手动修改则尝试立即下发到硬件。</summary>
        private void Save()
        {
            string err = "";
            double lon, lat;
            int utcMin, horizon, overhead, east, west;
            if (!TryParseD(_lonBox.Text, out lon)) err += "经度无效 / invalid longitude; ";
            if (!TryParseD(_latBox.Text, out lat)) err += "纬度无效 / invalid latitude; ";
            if (!TryParseUtcOffset(_utcBox.Text, out utcMin)) err += "UTC offset 无效（如 +08:00）/ invalid (e.g. +08:00); ";
            if (!int.TryParse(_horizonBox.Text, out horizon)) err += "地平限制无效 / invalid horizon; ";
            if (!int.TryParse(_overheadBox.Text, out overhead)) err += "天顶限制无效 / invalid overhead; ";
            if (!int.TryParse(_eastMerBox.Text, out east)) err += "东子午线无效 / invalid E.meridian; ";
            if (!int.TryParse(_westMerBox.Text, out west)) err += "西子午线无效 / invalid W.meridian; ";
            if (err.Length > 0) { _resultLabel.Text = "保存失败 / Save failed: " + err; return; }

            // 时间/日期（可留空 = 不改）
            bool timeDirty = _timeBox.Text.Trim() != _timeOriginal.Trim() && _timeBox.Text.Trim().Length > 0;
            bool dateDirty = _dateBox.Text.Trim() != _dateOriginal.Trim() && _dateBox.Text.Trim().Length > 0;

            DriverSettings.SetString(DriverSettings.KConnectionMode, _wifiRadio.Checked ? "WIFI" : "HID");
            DriverSettings.SetString(DriverSettings.KHost, _hostBox.Text.Trim());
            int port;
            if (!int.TryParse(_portBox.Text.Trim(), out port)) port = DriverSettings.DefaultPort;
            DriverSettings.SetInt(DriverSettings.KPort, port);
            int vid, pid;
            if (TryParseHex(_vidBox.Text, out vid) && TryParseHex(_pidBox.Text, out pid))
            {
                DriverSettings.SetInt("Vid", vid);
                DriverSettings.SetInt("Pid", pid);
            }
            DriverSettings.SetString("SerialNumber", _serialBox.Text ?? "");
            DriverSettings.SetDouble(DriverSettings.KLongitude, lon);
            DriverSettings.SetDouble(DriverSettings.KLatitude, lat);
            DriverSettings.SetInt(DriverSettings.KUtcOffsetMin, utcMin);
            DriverSettings.SetInt(DriverSettings.KHorizonLimit, horizon);
            DriverSettings.SetInt(DriverSettings.KOverheadLimit, overhead);
            DriverSettings.SetInt(DriverSettings.KEastMeridianMin, east);
            DriverSettings.SetInt(DriverSettings.KWestMeridianMin, west);
            DriverSettings.SetString(DriverSettings.KSlewRatePreset, _slewPresetCombo.SelectedItem == null ? "R0" : _slewPresetCombo.SelectedItem.ToString());
            DriverSettings.SetString(DriverSettings.KTrackingRate, _trackRateCombo.SelectedItem == null ? "恒星 (Sidereal)" : _trackRateCombo.SelectedItem.ToString());

            // 时间/日期被手动修改 → 尝试立即下发到硬件（失败不阻止保存）
            if (timeDirty || dateDirty)
            {
                try
                {
                    using (ITransport t = OpenTransportForTest())
                    {
                        var proto = new OnStepProtocol(t);
                        proto.Handshake();
                        DateTime lt;
                        if (timeDirty && DateTime.TryParseExact(_timeBox.Text.Trim(), "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out lt))
                        {
                            DateTime d = dateDirty
                                ? ParseDate(_dateBox.Text.Trim(), lt)
                                : new DateTime(2000, 1, 1, lt.Hour, lt.Minute, lt.Second);
                            proto.SetLocalTime(d);
                        }
                        else if (dateDirty && !timeDirty)
                        {
                            DateTime now = DateTime.Now;
                            proto.SetLocalTime(ParseDate(_dateBox.Text.Trim(), new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second)));
                        }
                    }
                    _resultLabel.Text = "设置已保存；时间/日期已写入硬件。连接驱动时其余设置自动下发。\r\n" +
                                       "Saved; time/date written to hardware. Other settings are applied when the driver connects.";
                }
                catch (Exception ex)
                {
                    _resultLabel.Text = "设置已保存（未能连接硬件下发时间/日期：" + ex.Message + "）。驱动连接时会应用其余设置。\r\n" +
                                       "Saved (could not write time/date: " + ex.Message + "). Other settings apply on connect.";
                }
            }
            else
            {
                _resultLabel.Text = "设置已保存。连接驱动时：经度/纬度/UTC offset/限制/速率 自动下发到硬件。\r\n" +
                                   "Saved. On connect: longitude/latitude/UTC offset/limits/rates are applied to the hardware.";
            }
        }

        private static DateTime ParseDate(string s, DateTime timePart)
        {
            string[] p = s.Split('/');
            int m = 1, d = 1, y = 2000;
            if (p.Length > 0) int.TryParse(p[0], out m);
            if (p.Length > 1) int.TryParse(p[1], out d);
            if (p.Length > 2) int.TryParse(p[2], out y);
            if (y < 100) y += 2000;
            if (y < 2000) y = 2000;
            return new DateTime(y, m, d, timePart.Hour, timePart.Minute, timePart.Second);
        }

        private static bool TryParseD(string s, out double v)
        {
            return double.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        private static bool TryParseUtcOffset(string s, out int minutes)
        {
            minutes = 0;
            string t = (s ?? "").Trim();
            if (t.Length == 0) return false;
            int sign = 1;
            if (t[0] == '-') { sign = -1; t = t.Substring(1); }
            else if (t[0] == '+') { t = t.Substring(1); }
            string[] p = t.Split(':');
            int h, m;
            if (p.Length < 1 || !int.TryParse(p[0], out h) || h < 0 || h > 14) return false;
            m = 0;
            if (p.Length > 1 && (!int.TryParse(p[1], out m) || m < 0 || m > 59)) return false;
            minutes = sign * (h * 60 + m);
            return true;
        }

        private static string FormatUtcOffset(int minutes)
        {
            string sign = minutes < 0 ? "-" : "+";
            int a = Math.Abs(minutes);
            return string.Format("{0}{1:D2}:{2:D2}", sign, a / 60, a % 60);
        }

        private static bool TryParseHex(string s, out int v)
        {
            return int.TryParse((s ?? "").Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v);
        }
    }
}
