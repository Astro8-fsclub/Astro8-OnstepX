using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace ASTRO8_OnStepHID
{
    /// <summary>
    /// OnStep / OnStepX（LX200 兼容协议）命令编解码。
    /// 命令集依据 OnStepX 官方 COMMAND_REFERENCE.md。
    /// 所有命令形如 ":CC...参数#"，应答以 '#' 结尾。
    /// </summary>
    [ComVisible(false)]
    public sealed class OnStepProtocol
    {
        private readonly ITransport _transport;
        private readonly object _lock = new object();

        public OnStepProtocol(ITransport transport) { _transport = transport; }

        // ---------- 底层收发 ----------

        /// <summary>
        /// 发送一条命令并读取完整应答（含 '#'）。expectReply=false 用于无应答命令（如 :Te#、:Q# 等），
        /// 发送后做一次短时排空，不抛超时。
        /// </summary>
        public string Command(string cmd, bool expectReply = true, int timeoutMs = 1500)
        {
            lock (_lock)
            {
                if (!_transport.IsOpen) throw new InvalidOperationException("设备未连接，请先连接。");
                _transport.Write(Encoding.ASCII.GetBytes(cmd));
                if (!expectReply)
                {
                    try
                    {
                        DateTime deadline = DateTime.UtcNow.AddMilliseconds(120);
                        while (DateTime.UtcNow < deadline)
                        {
                            try { _transport.ReadUntilHash(120); }
                            catch (TimeoutException) { break; }
                        }
                    }
                    catch { }
                    return "";
                }
                string resp = _transport.ReadUntilHash(timeoutMs);
                return Clean(resp);
            }
        }

        /// <summary>去掉应答中的前导空白/控制字符与结尾 '#'，并做基本清理。</summary>
        private static string Clean(string resp)
        {
            if (resp == null) return "";
            // 去掉前导空格/回车/换行/0x7F（LX200 移动指示符）
            int start = 0;
            while (start < resp.Length && (resp[start] == ' ' || resp[start] == '\r' || resp[start] == '\n' || resp[start] == 0x7F))
                start++;
            string s = resp.Substring(start);
            int hash = s.IndexOf('#');
            if (hash >= 0) s = s.Substring(0, hash);
            return s.Trim();
        }

        // ---------- 握手与固件 ----------

        public string GetProductName() { return Command(":GVP#", timeoutMs: 2000); }
        public string GetVersion() { return Command(":GVM#", timeoutMs: 2000); }

        /// <summary>尝试与 OnStep 握手，返回固件描述；失败抛异常。</summary>
        public string Handshake()
        {
            _transport.FlushPending();
            string name = GetProductName();
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("握手失败：:GVP# 无应答。请检查 HID 转串口芯片与 OnStep 的 TX/RX 是否交叉连接、波特率是否匹配。");
            string ver = "";
            try { ver = GetVersion(); } catch { }
            return string.IsNullOrWhiteSpace(ver) ? name : name + " / " + ver;
        }

        // ---------- 位置 ----------

        public double GetRa()
        {
            string r = Command(":GR#");
            return ParseRaHours(r);
        }

        public double GetDec()
        {
            string r = Command(":GD#");
            return ParseDecDegrees(r);
        }

        public double GetTargetRa() { return ParseRaHours(Command(":Gr#")); }
        public double GetTargetDec() { return ParseDecDegrees(Command(":Gd#")); }

        public double GetAltitude() { return ParseDecDegrees(Command(":GA#"), allowNoSign: true); }
        public double GetAzimuth() { return ParseDecDegrees(Command(":GZ#"), allowNoSign: true); }

        public void SetTargetRa(double hours)
        {
            string r = Command(":Sr" + FormatRa(hours) + "#");
            if (r == "0") throw new InvalidOperationException("OnStep 拒绝设置目标 RA（:Sr 返回 0）。");
        }

        public void SetTargetDec(double degrees)
        {
            string r = Command(":Sd" + FormatDec(degrees) + "#");
            if (r == "0") throw new InvalidOperationException("OnStep 拒绝设置目标 Dec（:Sd 返回 0）。");
        }

        public void SetTargetAltAz(double alt, double az)
        {
            string r1 = Command(":Sa" + FormatDec(alt) + "#");
            string r2 = Command(":Sz" + FormatDec(az) + "#");
            if (r1 == "0" || r2 == "0") throw new InvalidOperationException("OnStep 拒绝设置目标 Alt/Az。");
        }

        // ---------- GOTO / 同步 ----------

        /// <summary>执行 GOTO（赤道坐标，需先 SetTargetRa/SetTargetDec）。返回 true 表示已接受。</summary>
        public bool GotoEquatorial()
        {
            string r = Command(":MS#");
            int code = ParseInt(r);
            if (code == 0) return true;
            throw new InvalidOperationException("GOTO 被拒绝：" + GotoErrorText(code));
        }

        public bool GotoAltAz()
        {
            string r = Command(":MA#");
            int code = ParseInt(r);
            if (code == 0) return true;
            throw new InvalidOperationException("GOTO(Alt/Az) 被拒绝：" + GotoErrorText(code));
        }

        /// <summary>同步到当前目标坐标（需先 SetTargetRa/SetTargetDec）。OnStepX 的 :CS# 无应答。</summary>
        public void Sync()
        {
            Command(":CS#", expectReply: false);
        }

        private static string GotoErrorText(int code)
        {
            switch (code)
            {
                case 1: return "目标在地平线以下（限位）";
                case 2: return "目标超过天顶限位";
                case 3: return "控制器处于待机";
                case 4: return "赤道仪已 Park";
                case 5: return "已有 GOTO 正在进行";
                case 6: return "超出软限位";
                case 7: return "硬件故障";
                case 8: return "赤道仪正在运动";
                case 9: return "未知错误";
                default: return "错误码 " + code;
            }
        }

        // ---------- 跟踪 ----------

        public void SetTracking(bool on) { Command(on ? ":Te#" : ":Td#", expectReply: false); }

        /// <summary>:GT# 返回当前跟踪速率(Hz)，0 表示未跟踪。</summary>
        public bool GetTracking()
        {
            string r = Command(":GT#");
            double v;
            if (double.TryParse(r, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v > 0.0;
            return false;
        }

        public void SetTrackingRate(ASCOM.DeviceInterface.DriveRates rate)
        {
            string cmd;
            switch (rate)
            {
                case ASCOM.DeviceInterface.DriveRates.driveLunar: cmd = ":TL#"; break;
                case ASCOM.DeviceInterface.DriveRates.driveSolar: cmd = ":TS#"; break;
                case ASCOM.DeviceInterface.DriveRates.driveKing: cmd = ":TK#"; break;
                default: cmd = ":TQ#"; break;
            }
            Command(cmd, expectReply: false);
        }

        /// <summary>从 :GU# 状态串判断当前跟踪速率档位。</summary>
        public ASCOM.DeviceInterface.DriveRates GetTrackingRate()
        {
            string gu = GetStatusString();
            if (gu.Contains("(")) return ASCOM.DeviceInterface.DriveRates.driveLunar;
            if (gu.Contains("O")) return ASCOM.DeviceInterface.DriveRates.driveSolar;
            if (gu.Contains("k")) return ASCOM.DeviceInterface.DriveRates.driveKing;
            return ASCOM.DeviceInterface.DriveRates.driveSidereal;
        }

        // ---------- 导星 / 手动移动 ----------

        /// <summary>脉冲导星：方向 + 时长(ms)。格式 :Mg{d}{n}#。</summary>
        public void PulseGuide(char dir, int ms)
        {
            if (ms < 0) ms = 0;
            if (ms > 9999) ms = 9999;
            Command(string.Format(":Mg{0}{1}#", dir, ms), expectReply: false);
        }

        /// <summary>连续移动（速率=0 表示停止该轴）。axis1=RA，axis2=Dec。</summary>
        public void MoveAxis(int axis, double rateDegPerSec)
        {
            if (axis == 1)
            {
                if (rateDegPerSec > 0) Command(":Me#", expectReply: false);
                else if (rateDegPerSec < 0) Command(":Mw#", expectReply: false);
                else Command(":Qe#", expectReply: false);
            }
            else
            {
                if (rateDegPerSec > 0) Command(":Mn#", expectReply: false);
                else if (rateDegPerSec < 0) Command(":Ms#", expectReply: false);
                else Command(":Qn#", expectReply: false);
            }
        }

        public void StopAll() { Command(":Q#", expectReply: false); }

        /// <summary>:GX90# 当前脉冲导星速率（deg/s）。</summary>
        public double GetGuideRate()
        {
            double v;
            if (double.TryParse(Command(":GX90#"), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return 0.0;
        }

        /// <summary>设置导星速率：:RAn.n#(轴1/RA) / :REn.n#(轴2/Dec)，单位 deg/s。</summary>
        public void SetGuideRate(int axis, double degPerSec)
        {
            if (degPerSec < 0) degPerSec = 0;
            if (degPerSec > 99.99) degPerSec = 99.99;
            Command((axis == 1 ? ":RA" : ":RE") + degPerSec.ToString("F2", CultureInfo.InvariantCulture) + "#", expectReply: false);
        }

        // ---------- Park / Home ----------

        public void Park() { Command(":hP#", expectReply: false); }
        public void Unpark() { Command(":hR#", expectReply: false); }
        public void SetPark() { Command(":hQ#", expectReply: false); }
        public void FindHome() { Command(":hC#", expectReply: false); }
        public void ResetHome() { Command(":hF#", expectReply: false); }

        // ---------- 状态 ----------

        /// <summary>:GU# 人类可读状态串。</summary>
        public string GetStatusString() { return Command(":GU#"); }

        /// <summary>
        /// 是否正在运动（GOTO/回中/Park 中）。基于本地标志 + :GU# 状态：
        /// 'N' = 无 GOTO 进行；'I' = Park 中；'h' = 回中。
        /// </summary>
        public bool IsBusy(ref bool gotoInProgress)
        {
            string gu = null;
            try { gu = GetStatusString(); } catch { }
            if (string.IsNullOrEmpty(gu)) return gotoInProgress; // 查询失败：沿用本地标志
            bool gotoActive = !gu.Contains("N");
            bool parkingOrHoming = gu.Contains("I") || gu.Contains("h");
            gotoInProgress = gotoActive;
            return gotoActive || parkingOrHoming;
        }

        public bool IsParked()
        {
            try { return GetStatusString().Contains("P"); } catch { return false; }
        }

        public bool IsAtHome()
        {
            try { return GetStatusString().Contains("H"); } catch { return false; }
        }

        /// <summary>侧位：:GU# 中 'T'=东,'W'=西；失败返回 -1(未知)。</summary>
        public int GetPierSide()
        {
            try
            {
                string gu = GetStatusString();
                if (gu.Contains("T")) return 0; // pierEast
                if (gu.Contains("W")) return 1; // pierWest
            }
            catch { }
            return -1;
        }

        // ---------- 站点 / 时间 ----------

        public double GetLatitude() { return ParseDecDegrees(Command(":Gt#")); }
        public double GetLongitude() { return ParseDecDegrees(Command(":Gg#"), allowNoSign: true); }
        public double GetElevation() { double v; return double.TryParse(Command(":Gv#"), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0.0; }

        public void SetLatitude(double deg) { Command(":St" + FormatDec(deg) + "#", expectReply: false); }
        public void SetLongitude(double deg) { Command(":Sg" + FormatDec(deg) + "#", expectReply: false); }
        public void SetElevation(double m) { Command(":Sv" + m.ToString("F1", CultureInfo.InvariantCulture) + "#", expectReply: false); }

        /// <summary>恒星时（小时）。</summary>
        public double GetSiderealTime() { return ParseRaHours(Command(":GS#")); }

        /// <summary>UT1 日期时间（:GX80# 时 :GX81# 日期）。</summary>
        public DateTime GetUt1()
        {
            string t = Command(":GX80#");   // HH:MM:SS.ss
            string d = Command(":GX81#");   // MM/DD/YY
            DateTime dt;
            if (DateTime.TryParseExact(t + " " + d, "HH:mm:ss.ff MM/dd/yy", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
                return dt;
            throw new InvalidOperationException("无法解析 UT1 时间：" + t + " / " + d);
        }

        /// <summary>设置本地时间与日期（:SL / :SC，使用 PC 本地时区换算）。</summary>
        public void SetLocalTime(DateTime local)
        {
            Command(":SL" + local.ToString("HH:mm:ss") + "#", expectReply: false);
            Command(":SC" + local.ToString("MM/dd/yy") + "#", expectReply: false);
        }

        /// <summary>回读本地标准时间（:GL#，24 小时制）。失败抛异常。</summary>
        public DateTime GetLocalTime()
        {
            string t = Command(":GL#");   // HH:MM:SS
            DateTime dt;
            if (DateTime.TryParseExact(t, "HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
                return dt;
            throw new InvalidOperationException("无法解析本地时间：" + t);
        }

        /// <summary>回读本地日期（:GC#，MM/DD/YY）。失败抛异常。</summary>
        public DateTime GetLocalDate()
        {
            string d = Command(":GC#");   // MM/DD/YY
            DateTime dt;
            if (DateTime.TryParseExact(d, "MM/dd/yy", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
                return dt;
            throw new InvalidOperationException("无法解析本地日期：" + d);
        }

        /// <summary>回读本地日期时间 = 本地时间 :GL# + 本地日期 :GC#（OnStep 标准命令）。</summary>
        public DateTime GetLocalDateTime()
        {
            string t = Command(":GL#");   // HH:MM:SS
            string d = Command(":GC#");   // MM/DD/YY
            DateTime dt;
            if (DateTime.TryParseExact(t + " " + d, "HH:mm:ss MM/dd/yy", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
                return dt;
            throw new InvalidOperationException("无法解析本地日期时间：" + t + " / " + d);
        }

        /// <summary>回读 UTC offset（:GG#，返回 "sHH:MM"），即加到此值可得 UT1。</summary>
        public TimeSpan GetUtcOffset()
        {
            string r = (Command(":GG#") ?? "").Trim();
            int sign = 1;
            if (r.StartsWith("-")) { sign = -1; r = r.Substring(1); }
            else if (r.StartsWith("+")) { r = r.Substring(1); }
            string[] p = r.Split(':');
            int h = 0, m = 0;
            if (p.Length > 0) int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out h);
            if (p.Length > 1) int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out m);
            return new TimeSpan(0, sign * (h * 60 + m), 0);
        }

        /// <summary>设置 UTC offset（:SGsHH:MM#，OnStep 建议 MM 为 00/30/45）。</summary>
        public void SetUtcOffset(TimeSpan offset)
        {
            int total = (int)offset.TotalMinutes;
            string sign = total < 0 ? "-" : "+";
            total = Math.Abs(total);
            Command(":SG" + sign + (total / 60).ToString("D2") + ":" + (total % 60).ToString("D2") + "#",
                expectReply: false);
        }

        // ---------- 硬件限制（地平 / 天顶 / 子午线） ----------

        /// <summary>:Gh# 地平线限制（度，带符号）。</summary>
        public int GetHorizonLimit() { return ParseInt(Command(":Gh#")); }

        /// <summary>:Go# 天顶限制（度）。</summary>
        public int GetOverheadLimit() { return ParseInt(Command(":Go#")); }

        /// <summary>:GXE9# 东子午线限制（分钟）。</summary>
        public int GetEastMeridianLimitMinutes() { return ParseInt(Command(":GXE9#")); }

        /// <summary>:GXEA# 西子午线限制（分钟）。</summary>
        public int GetWestMeridianLimitMinutes() { return ParseInt(Command(":GXEA#")); }

        /// <summary>:ShsDD# 设置地平线限制（度，带符号）。</summary>
        public void SetHorizonLimit(int deg)
        {
            Command(":Sh" + FormatSignedInt(deg) + "#", expectReply: false);
        }

        /// <summary>:SoDD# 设置天顶限制（度）。</summary>
        public void SetOverheadLimit(int deg)
        {
            Command(":So" + Math.Max(0, deg).ToString(CultureInfo.InvariantCulture) + "#", expectReply: false);
        }

        /// <summary>:SXE9,n# 设置东子午线限制（分钟）。</summary>
        public void SetEastMeridianLimitMinutes(int minutes)
        {
            Command(":SXE9," + Math.Max(0, minutes).ToString(CultureInfo.InvariantCulture) + "#", expectReply: false);
        }

        /// <summary>:SXEA,n# 设置西子午线限制（分钟）。</summary>
        public void SetWestMeridianLimitMinutes(int minutes)
        {
            Command(":SXEA," + Math.Max(0, minutes).ToString(CultureInfo.InvariantCulture) + "#", expectReply: false);
        }

        // ---------- 轴限制（GEM 上 axis1=RA、axis2=Dec；只读，供属性页参考） ----------

        /// <summary>:GXEe# Axis1 最小限制（度）。</summary>
        public int GetAxis1MinLimitDeg() { return ParseInt(Command(":GXEe#")); }

        /// <summary>:GXEB# Axis1 最大限制（小时，RA 轴）。</summary>
        public double GetAxis1MaxLimitHours() { return ParseDouble(Command(":GXEB#")); }

        /// <summary>:GXEC# Axis2 最小限制（度）。</summary>
        public int GetAxis2MinLimitDeg() { return ParseInt(Command(":GXEC#")); }

        /// <summary>:GXED# Axis2 最大限制（度）。</summary>
        public int GetAxis2MaxLimitDeg() { return ParseInt(Command(":GXED#")); }

        // ---------- Slew / 移动速率 ----------

        /// <summary>:GX97# 当前 GOTO 移动速度（deg/s）。</summary>
        public double GetSlewSpeedDegPerSec() { return ParseDouble(Command(":GX97#")); }

        /// <summary>
        /// 设置移动/导星速率档位：:RG#(1x) :RC#(8x) :RM#(20x) :RF#(48x) :RS#(半速) :R0#~:R9#。
        /// 后续 :Me#/:Mw#/:Mn#/:Ms# 连续移动即按此档位运行。
        /// </summary>
        public void SetSlewRatePreset(string preset)
        {
            string p = (preset ?? "").Trim().ToUpperInvariant();
            string cmd;
            switch (p)
            {
                case "1X": cmd = ":RG#"; break;
                case "8X": cmd = ":RC#"; break;
                case "20X": cmd = ":RM#"; break;
                case "48X": cmd = ":RF#"; break;
                case "HALF": cmd = ":RS#"; break;
                default:
                    if (p.Length == 2 && p[0] == 'R' && p[1] >= '0' && p[1] <= '9') cmd = ":" + p + "#";
                    else throw new ArgumentException("未知速率预设：" + preset);
                    break;
            }
            Command(cmd, expectReply: false);
        }

        // ---------- 挂载类型 ----------

        /// <summary>:GXEM# 挂载类型：1=GEM 2=FORK 3=ALTAZM ...；失败返回 1。</summary>
        public int GetMountType()
        {
            try
            {
                int v = ParseInt(Command(":GXEM#"));
                if (v >= 1 && v <= 9) return v;
            }
            catch { }
            return 1;
        }

        // ---------- 速率偏移（offset tracking） ----------

        /// <summary>:GXTR# RA 偏移（arcsec/恒星秒）。ASCOM RightAscensionRate 单位是 秒(RA)/恒星秒，换算 ÷15。</summary>
        public double GetRaRateOffset()
        {
            double v;
            if (double.TryParse(Command(":GXTR#"), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v / 15.0;
            return 0.0;
        }

        public void SetRaRateOffset(double ascomValue)
        {
            Command(":SXTR," + (ascomValue * 15.0).ToString("F4", CultureInfo.InvariantCulture) + "#", expectReply: false);
        }

        /// <summary>:GXTD# Dec 偏移（arcsec/恒星秒），与 ASCOM arcsec/SI秒 近似相等。</summary>
        public double GetDecRateOffset()
        {
            double v;
            if (double.TryParse(Command(":GXTD#"), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return 0.0;
        }

        public void SetDecRateOffset(double ascomValue)
        {
            Command(":SXTD," + ascomValue.ToString("F4", CultureInfo.InvariantCulture) + "#", expectReply: false);
        }

        // ---------- 原始命令透传 ----------

        /// <summary>透传任意 OnStep 命令（客户端调试用）。</summary>
        public string RawCommand(string raw)
        {
            string cmd = raw.Trim();
            if (!cmd.StartsWith(":")) cmd = ":" + cmd;
            if (!cmd.EndsWith("#")) cmd += "#";
            return Command(cmd);
        }

        // ---------- 格式化 / 解析 ----------

        /// <summary>RA(小时) → "HH:MM:SS"。</summary>
        public static string FormatRa(double hours)
        {
            hours = hours % 24.0;
            if (hours < 0) hours += 24.0;
            int h = (int)hours;
            double mf = (hours - h) * 60.0;
            int m = (int)mf;
            double sf = Math.Round((mf - m) * 60.0);
            if (sf >= 60.0) { sf = 0; m++; }
            if (m >= 60) { m = 0; h = (h + 1) % 24; }
            return string.Format("{0:D2}:{1:D2}:{2:D2}", h, m, (int)sf);
        }

        /// <summary>角度(度) → "±DD*MM:SS"。</summary>
        public static string FormatDec(double degrees)
        {
            if (double.IsNaN(degrees)) throw new ArgumentException("无效角度");
            string sign = degrees < 0 ? "-" : "+";
            double a = Math.Abs(degrees);
            int d = (int)a;
            double mf = (a - d) * 60.0;
            int m = (int)mf;
            double sf = Math.Round((mf - m) * 60.0);
            if (sf >= 60.0) { sf = 0; m++; }
            if (m >= 60) { m = 0; d++; }
            return string.Format("{0}{1:D2}*{2:D2}:{3:D2}", sign, d, m, (int)sf);
        }

        /// <summary>解析 "HH:MM:SS" 或 "HH:MM.T" → 小时。</summary>
        public static double ParseRaHours(string s)
        {
            s = (s ?? "").Trim().TrimEnd('#');
            if (s.Length == 0) throw new InvalidOperationException("空 RA 应答");
            string[] p = s.Split(':');
            if (p.Length < 2) throw new InvalidOperationException("RA 格式错误: " + s);
            double h, m = 0, sec = 0;
            if (!double.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out h)) throw new InvalidOperationException("RA 格式错误: " + s);
            if (!double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out m)) throw new InvalidOperationException("RA 格式错误: " + s);
            if (p.Length > 2 && !double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out sec)) sec = 0;
            double v = h + m / 60.0 + sec / 3600.0;
            if (v < 0) v += 24.0;
            return v;
        }

        /// <summary>解析 "sDD*MM'SS" / "sDD*MM:SS" / "sDD*MM" → 度。</summary>
        public static double ParseDecDegrees(string s, bool allowNoSign = false)
        {
            s = (s ?? "").Trim().TrimEnd('#');
            if (s.Length == 0) throw new InvalidOperationException("空 Dec 应答");
            int sign = 1;
            if (s[0] == '-') { sign = -1; s = s.Substring(1); }
            else if (s[0] == '+') { s = s.Substring(1); }
            else if (!allowNoSign) throw new InvalidOperationException("Dec 缺符号: " + s);

            // 分离度与分:秒：分隔符可能是 '*'、'\'' 或 ':' 
            int star = s.IndexOfAny(new char[] { '*', '\'' });
            if (star < 0) throw new InvalidOperationException("Dec 格式错误: " + s);
            double deg;
            if (!double.TryParse(s.Substring(0, star), NumberStyles.Float, CultureInfo.InvariantCulture, out deg)) throw new InvalidOperationException("Dec 格式错误: " + s);
            string rest = s.Substring(star + 1).Replace('\'', ':');
            string[] p = rest.Split(':');
            double m = 0, sec = 0;
            if (p.Length > 0 && p[0].Length > 0 && !double.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out m)) m = 0;
            if (p.Length > 1 && p[1].Length > 0 && !double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out sec)) sec = 0;
            return sign * (deg + m / 60.0 + sec / 3600.0);
        }

        private static int ParseInt(string s)
        {
            int v;
            return int.TryParse((s ?? "").Trim(), out v) ? v : -1;
        }

        private static double ParseDouble(string s)
        {
            double v;
            return double.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0.0;
        }

        /// <summary>带符号整数（OnStep :Sh 等命令的 sDD 格式）。</summary>
        private static string FormatSignedInt(int v)
        {
            return (v < 0 ? "-" : "+") + Math.Abs(v).ToString(CultureInfo.InvariantCulture);
        }
    }
}
