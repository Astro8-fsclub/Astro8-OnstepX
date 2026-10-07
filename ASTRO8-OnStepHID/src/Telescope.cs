using System;
using System.Collections;
using System.Runtime.InteropServices;
using ASCOM;
using ASCOM.DeviceInterface;

namespace ASTRO8_OnStepHID
{
    /// <summary>
    /// OnStep 望远镜 ASCOM 驱动 —— HID（USB 转串口芯片）或 WiFi（TCP）连接 OnStep。
    /// 同时实现 ITelescopeV3（兼容 ASCOM Platform 6.x 客户端）与 ITelescopeV4（Platform 7）。
    /// 注册方式：regasm（COM ProgID: ASCOM.OnStepAstro8.Telescope）+ ASCOM Profile 键。
    /// 属性页（SetupDialog）可回读/设置：经度、纬度、UTC offset、时间日期、RA/Dec GOTO 限制、
    /// 地平/天顶/子午线限制、移动速率档、跟踪速率；连接时自动下发到硬件。
    /// </summary>
    [ComVisible(true)]
    [Guid("74EDFF70-B739-4A59-9842-0DD94C305018")]
    [ProgId(DriverSettings.ProgId)]
    [ClassInterface(ClassInterfaceType.None)]
    public class Telescope : ITelescopeV3, ITelescopeV4, IDisposable
    {
        private readonly object _state = new object();

        private ITransport _transport;
        private OnStepProtocol _protocol;
        private bool _connected;
        private string _firmwareInfo = "";
        private AlignmentModes _alignmentMode = AlignmentModes.algGermanPolar;
        private bool _gotoActive;
        private bool _moveAxis1Active, _moveAxis2Active;
        private DateTime _pulseUntil = DateTime.MinValue;
        private bool _doesRefraction;
        private double _latCache, _lonCache, _elevCache;

        public Telescope() { }

        // ================= ASCOM 基础 =================

        public string Name { get { return "ASTRO8-OnstepX"; } }
        public string Description { get { return DriverSettings.DriverDescription; } }
        public string DriverInfo
        {
            get { return "ASTRO8-OnstepX 望远镜驱动（OnStepX 固件）。连接方式：①HID（经 USB 转串口免驱芯片，校验序列号 A8-0001，替代串口）；②WiFi（TCP，默认 192.168.0.1:9998，参考 ASCOM 串口驱动样式）。属性页可回读/设置经度、纬度、UTC offset、时间日期、RA/Dec 限制、地平/天顶/子午线限制、移动速率与跟踪速率。固件协议：LX200 兼容。"; }
        }
        public string DriverVersion { get { return "1.0.0"; } }
        public short InterfaceVersion { get { return 3; } }
        public ArrayList SupportedActions
        {
            get { return new ArrayList { "OnStepCommand", "GetFirmware" }; }
        }

        public string Action(string ActionName, string ActionParameters)
        {
            EnsureConnected();
            if (string.Equals(ActionName, "OnStepCommand", StringComparison.OrdinalIgnoreCase))
                return _protocol.RawCommand(ActionParameters ?? "");
            if (string.Equals(ActionName, "GetFirmware", StringComparison.OrdinalIgnoreCase))
                return _firmwareInfo;
            throw new InvalidValueException("未知的 Action：" + ActionName);
        }

        public void CommandBlind(string Command, bool Raw)
        {
            EnsureConnected();
            string frame = Raw ? Command : NormalizeFrame(Command);
            _protocol.Command(frame, expectReply: false);
        }

        public bool CommandBool(string Command, bool Raw)
        {
            EnsureConnected();
            string frame = Raw ? Command : NormalizeFrame(Command);
            string r = _protocol.Command(frame);
            return r.TrimStart() == "1";
        }

        public string CommandString(string Command, bool Raw)
        {
            EnsureConnected();
            string frame = Raw ? Command : NormalizeFrame(Command);
            return _protocol.Command(frame);
        }

        private static string NormalizeFrame(string cmd)
        {
            string c = (cmd ?? "").Trim();
            if (!c.StartsWith(":")) c = ":" + c;
            if (!c.EndsWith("#")) c += "#";
            return c;
        }

        // ================= 连接 =================

        public bool Connected
        {
            get { lock (_state) { return _connected; } }
            set
            {
                if (value) ConnectInternal();
                else DisconnectInternal();
            }
        }

        // ITelescopeV4：显式连接/断开
        public void Connect() { ConnectInternal(); }
        public void Disconnect() { DisconnectInternal(); }
        public bool Connecting { get { return false; } }
        public IStateValueCollection DeviceState { get { return new StateValueCollection(); } }

        private void ConnectInternal()
        {
            lock (_state)
            {
                if (_connected) return;

                string mode = DriverSettings.GetConnectionMode();
                ITransport transport = null;
                OnStepProtocol proto = null;
                try
                {
                    if (mode == "WIFI")
                    {
                        var wifi = new WifiTransport(DriverSettings.GetHost(), DriverSettings.GetPort());
                        transport = wifi;
                        wifi.Open();
                    }
                    else
                    {
                        var hid = new HidTransport();
                        transport = hid;
                        int vid = DriverSettings.GetInt("Vid", DriverSettings.DefaultVid);
                        int pid = DriverSettings.GetInt("Pid", DriverSettings.DefaultPid);
                        hid.Open(vid, pid);

                        // 序列号验证：防止同 VID/PID 的其它设备误连。留空或 "*" 表示跳过。
                        string expected = DriverSettings.GetString("SerialNumber", DriverSettings.DefaultSerial);
                        if (!string.IsNullOrWhiteSpace(expected) && expected.Trim() != "*")
                        {
                            string actual = (hid.SerialNumber ?? "").Trim();
                            if (!string.Equals(actual, expected.Trim(), StringComparison.OrdinalIgnoreCase))
                            {
                                transport.Dispose();
                                transport = null;
                                throw new DriverException(string.Format(
                                    "序列号验证失败：期望 \"{0}\"，实际为 \"{1}\"。请在 Setup 中核对期望序列号，或清空该字段以跳过验证。",
                                    expected, actual.Length == 0 ? "(空)" : actual));
                            }
                        }
                    }

                    proto = new OnStepProtocol(transport);
                    _firmwareInfo = proto.Handshake();
                    // 缓存挂载类型 → 对齐模式
                    int mt = proto.GetMountType();
                    _alignmentMode = mt == 2 ? AlignmentModes.algPolar
                                 : mt == 3 ? AlignmentModes.algAltAz
                                 : AlignmentModes.algGermanPolar;

                    // 应用属性页设置的站点/限制/速率（有值才下发，避免覆盖硬件当前值）
                    ApplyProfileSettings(proto);

                    _transport = transport;
                    _protocol = proto;
                    _connected = true;
                    _gotoActive = false;
                    _moveAxis1Active = _moveAxis2Active = false;
                }
                catch (Exception ex)
                {
                    if (transport != null) { try { transport.Dispose(); } catch { } }
                    throw new DriverException("连接 OnStep 失败：" + ex.Message, ex);
                }
            }
        }

        /// <summary>
        /// 连接时把属性页保存的设置下发到硬件：经度/纬度/UTC offset/地平/天顶/子午线限制/
        /// 移动速率档/跟踪速率。Profile 中无记录（NaN / -1 / 空）的项跳过，保留硬件当前值。
        /// </summary>
        private static void ApplyProfileSettings(OnStepProtocol proto)
        {
            double lat = DriverSettings.GetLatitude();
            if (!double.IsNaN(lat)) { try { proto.SetLatitude(lat); } catch { } }
            double lon = DriverSettings.GetLongitude();
            if (!double.IsNaN(lon)) { try { proto.SetLongitude(lon); } catch { } }
            int utcMin = DriverSettings.GetUtcOffsetMinutes();
            if (utcMin != int.MinValue) { try { proto.SetUtcOffset(TimeSpan.FromMinutes(utcMin)); } catch { } }

            int horizon = DriverSettings.GetInt(DriverSettings.KHorizonLimit, -1);
            if (horizon != -1) { try { proto.SetHorizonLimit(horizon); } catch { } }
            int overhead = DriverSettings.GetInt(DriverSettings.KOverheadLimit, -1);
            if (overhead != -1) { try { proto.SetOverheadLimit(overhead); } catch { } }
            int east = DriverSettings.GetInt(DriverSettings.KEastMeridianMin, -1);
            if (east != -1) { try { proto.SetEastMeridianLimitMinutes(east); } catch { } }
            int west = DriverSettings.GetInt(DriverSettings.KWestMeridianMin, -1);
            if (west != -1) { try { proto.SetWestMeridianLimitMinutes(west); } catch { } }

            string preset = DriverSettings.GetString(DriverSettings.KSlewRatePreset, "");
            if (!string.IsNullOrWhiteSpace(preset)) { try { proto.SetSlewRatePreset(preset); } catch { } }
            string track = DriverSettings.GetString(DriverSettings.KTrackingRate, "");
            if (!string.IsNullOrWhiteSpace(track))
            {
                try
                {
                    DriveRates r = track.StartsWith("月球") ? DriveRates.driveLunar
                        : track.StartsWith("太阳") ? DriveRates.driveSolar
                        : track.StartsWith("King") ? DriveRates.driveKing
                        : DriveRates.driveSidereal;
                    proto.SetTrackingRate(r);
                }
                catch { }
            }
        }

        private void DisconnectInternal()
        {
            lock (_state)
            {
                if (_transport != null) { try { _transport.Dispose(); } catch { } _transport = null; }
                _protocol = null;
                _connected = false;
                _gotoActive = false;
                _moveAxis1Active = _moveAxis2Active = false;
            }
        }

        public void Dispose() { DisconnectInternal(); }

        private void EnsureConnected()
        {
            if (!Connected) throw new NotConnectedException("设备未连接。");
        }

        // ================= 能力位 =================

        public bool CanFindHome { get { EnsureConnected(); return true; } }
        public bool CanPark { get { EnsureConnected(); return true; } }
        public bool CanPulseGuide { get { EnsureConnected(); return true; } }
        public bool CanSetDeclinationRate { get { EnsureConnected(); return true; } }
        public bool CanSetGuideRates { get { EnsureConnected(); return true; } }
        public bool CanSetPark { get { EnsureConnected(); return true; } }
        public bool CanSetPierSide { get { EnsureConnected(); return false; } }
        public bool CanSetRightAscensionRate { get { EnsureConnected(); return true; } }
        public bool CanSetTracking { get { EnsureConnected(); return true; } }
        public bool CanSlew { get { EnsureConnected(); return true; } }
        public bool CanSlewAltAz { get { EnsureConnected(); return true; } }
        public bool CanSlewAltAzAsync { get { EnsureConnected(); return true; } }
        public bool CanSlewAsync { get { EnsureConnected(); return true; } }
        public bool CanSync { get { EnsureConnected(); return true; } }
        public bool CanSyncAltAz { get { EnsureConnected(); return true; } }
        public bool CanUnpark { get { EnsureConnected(); return true; } }
        public bool CanMoveAxis(TelescopeAxes Axis) { EnsureConnected(); return true; }

        public IAxisRates AxisRates(TelescopeAxes Axis)
        {
            EnsureConnected();
            return new AxisRates();
        }

        public PierSide DestinationSideOfPier(double RightAscension, double Declination)
        {
            EnsureConnected();
            // 完整 GEM 侧位预测需要精确的时角/纬度模型；这里返回当前侧位。
            return SideOfPier;
        }

        // ================= 位置 =================

        public double RightAscension { get { EnsureConnected(); return _protocol.GetRa(); } }
        public double Declination { get { EnsureConnected(); return _protocol.GetDec(); } }
        public double Altitude { get { EnsureConnected(); return _protocol.GetAltitude(); } }
        public double Azimuth { get { EnsureConnected(); return _protocol.GetAzimuth(); } }
        public double SiderealTime { get { EnsureConnected(); return _protocol.GetSiderealTime(); } }

        public double TargetRightAscension
        {
            get { EnsureConnected(); return _protocol.GetTargetRa(); }
            set { EnsureConnected(); _protocol.SetTargetRa(value); }
        }

        public double TargetDeclination
        {
            get { EnsureConnected(); return _protocol.GetTargetDec(); }
            set { EnsureConnected(); _protocol.SetTargetDec(value); }
        }

        public AlignmentModes AlignmentMode { get { EnsureConnected(); return _alignmentMode; } }
        public EquatorialCoordinateType EquatorialSystem { get { EnsureConnected(); return EquatorialCoordinateType.equTopocentric; } }
        public bool DoesRefraction
        {
            get { EnsureConnected(); return _doesRefraction; }
            set { EnsureConnected(); _doesRefraction = value; }
        }

        // ================= 站点 / 时间 =================

        public double SiteLatitude
        {
            get
            {
                EnsureConnected();
                double p = DriverSettings.GetLatitude(); // 属性页手动设置优先
                if (!double.IsNaN(p)) return p;
                return _latCache != 0 ? _latCache : (_latCache = _protocol.GetLatitude());
            }
            set { EnsureConnected(); _latCache = value; _protocol.SetLatitude(value); }
        }

        public double SiteLongitude
        {
            get
            {
                EnsureConnected();
                double p = DriverSettings.GetLongitude(); // 属性页手动设置优先
                if (!double.IsNaN(p)) return p;
                return _lonCache != 0 ? _lonCache : (_lonCache = _protocol.GetLongitude());
            }
            set { EnsureConnected(); _lonCache = value; _protocol.SetLongitude(value); }
        }

        public double SiteElevation
        {
            get { EnsureConnected(); return _elevCache != 0 ? _elevCache : (_elevCache = _protocol.GetElevation()); }
            set { EnsureConnected(); _elevCache = value; _protocol.SetElevation(value); }
        }

        public DateTime UTCDate
        {
            get { EnsureConnected(); return _protocol.GetUt1(); }
            set
            {
                EnsureConnected();
                DateTime local = TimeZoneInfo.ConvertTimeFromUtc(value, TimeZoneInfo.Local);
                _protocol.SetLocalTime(local);
            }
        }

        // ================= 望远镜特征 =================

        public double ApertureArea { get { EnsureConnected(); return DriverSettings.GetDouble("ApertureArea", 0.0); } }
        public double ApertureDiameter { get { EnsureConnected(); return DriverSettings.GetDouble("ApertureDiameter", 0.0); } }
        public double FocalLength { get { EnsureConnected(); return DriverSettings.GetDouble("FocalLength", 0.0); } }

        // ================= 跟踪 =================

        public bool Tracking
        {
            get { EnsureConnected(); return _protocol.GetTracking(); }
            set { EnsureConnected(); _protocol.SetTracking(value); }
        }

        public DriveRates TrackingRate
        {
            get { EnsureConnected(); return _protocol.GetTrackingRate(); }
            set { EnsureConnected(); _protocol.SetTrackingRate(value); }
        }

        public ITrackingRates TrackingRates { get { EnsureConnected(); return new TrackingRates(); } }

        public double RightAscensionRate
        {
            get { EnsureConnected(); return _protocol.GetRaRateOffset(); }
            set { EnsureConnected(); _protocol.SetRaRateOffset(value); }
        }

        public double DeclinationRate
        {
            get { EnsureConnected(); return _protocol.GetDecRateOffset(); }
            set { EnsureConnected(); _protocol.SetDecRateOffset(value); }
        }

        // ================= 导星 / 移动 =================

        public void PulseGuide(GuideDirections Direction, int Duration)
        {
            EnsureConnected();
            char dir;
            switch (Direction)
            {
                case GuideDirections.guideNorth: dir = 'n'; break;
                case GuideDirections.guideSouth: dir = 's'; break;
                case GuideDirections.guideEast: dir = 'e'; break;
                default: dir = 'w'; break;
            }
            _protocol.PulseGuide(dir, Duration);
            lock (_state) { _pulseUntil = DateTime.UtcNow.AddMilliseconds(Duration + 50); }
        }

        public bool IsPulseGuiding { get { EnsureConnected(); return DateTime.UtcNow < _pulseUntil; } }

        public double GuideRateRightAscension
        {
            get { EnsureConnected(); return _protocol.GetGuideRate(); }
            set { EnsureConnected(); _protocol.SetGuideRate(1, value); }
        }

        public double GuideRateDeclination
        {
            get { EnsureConnected(); return _protocol.GetGuideRate(); }
            set { EnsureConnected(); _protocol.SetGuideRate(2, value); }
        }

        public void MoveAxis(TelescopeAxes Axis, double Rate)
        {
            EnsureConnected();
            if (Axis == TelescopeAxes.axisTertiary) throw new InvalidValueException("本驱动不支持第三轴。");
            int axis = Axis == TelescopeAxes.axisPrimary ? 1 : 2;
            _protocol.MoveAxis(axis, Rate);
            lock (_state)
            {
                if (axis == 1) _moveAxis1Active = Math.Abs(Rate) > 1e-9;
                else _moveAxis2Active = Math.Abs(Rate) > 1e-9;
            }
        }

        // ================= GOTO / 同步 =================

        public void SlewToCoordinates(double RightAscension, double Declination)
        {
            EnsureConnected();
            lock (_state)
            {
                _protocol.SetTargetRa(RightAscension);
                _protocol.SetTargetDec(Declination);
                _protocol.GotoEquatorial();
                _gotoActive = true;
            }
        }

        public void SlewToCoordinatesAsync(double RightAscension, double Declination)
        {
            double ra = RightAscension, de = Declination;
            lock (_state) { _gotoActive = true; }
            System.Threading.Tasks.Task.Run(() =>
            {
                try { SlewToCoordinates(ra, de); }
                catch { lock (_state) { _gotoActive = false; } }
            });
        }

        public void SlewToTarget()
        {
            EnsureConnected();
            lock (_state)
            {
                _protocol.GotoEquatorial();
                _gotoActive = true;
            }
        }

        public void SlewToTargetAsync()
        {
            lock (_state) { _gotoActive = true; }
            System.Threading.Tasks.Task.Run(() =>
            {
                try { SlewToTarget(); }
                catch { lock (_state) { _gotoActive = false; } }
            });
        }

        public void SlewToAltAz(double Azimuth, double Altitude)
        {
            EnsureConnected();
            lock (_state)
            {
                _protocol.SetTargetAltAz(Altitude, Azimuth);
                _protocol.GotoAltAz();
                _gotoActive = true;
            }
        }

        public void SlewToAltAzAsync(double Azimuth, double Altitude)
        {
            double az = Azimuth, al = Altitude;
            lock (_state) { _gotoActive = true; }
            System.Threading.Tasks.Task.Run(() =>
            {
                try { SlewToAltAz(az, al); }
                catch { lock (_state) { _gotoActive = false; } }
            });
        }

        public void AbortSlew()
        {
            EnsureConnected();
            _protocol.StopAll();
            lock (_state) { _gotoActive = _moveAxis1Active = _moveAxis2Active = false; }
        }

        public void AbortSlewAsync() { AbortSlew(); }

        public void SyncToCoordinates(double RightAscension, double Declination)
        {
            EnsureConnected();
            lock (_state)
            {
                _protocol.SetTargetRa(RightAscension);
                _protocol.SetTargetDec(Declination);
                _protocol.Sync();
            }
        }

        public void SyncToTarget()
        {
            EnsureConnected();
            lock (_state) { _protocol.Sync(); }
        }

        public void SyncToAltAz(double Azimuth, double Altitude)
        {
            EnsureConnected();
            lock (_state)
            {
                _protocol.SetTargetAltAz(Altitude, Azimuth);
                _protocol.Sync();
            }
        }

        // ================= 状态 =================

        public bool Slewing
        {
            get
            {
                EnsureConnected();
                lock (_state)
                {
                    if (_moveAxis1Active || _moveAxis2Active) return true;
                    if (!_gotoActive) return false;
                    return _protocol.IsBusy(ref _gotoActive);
                }
            }
        }

        public bool AtPark { get { EnsureConnected(); return _protocol.IsParked(); } }
        public bool AtHome { get { EnsureConnected(); return _protocol.IsAtHome(); } }

        public PierSide SideOfPier
        {
            get
            {
                EnsureConnected();
                int s = _protocol.GetPierSide();
                return s == 0 ? PierSide.pierEast : s == 1 ? PierSide.pierWest : PierSide.pierUnknown;
            }
            set { throw new PropertyNotImplementedException("本驱动不支持强制侧位翻转（CanSetPierSide=false）。"); }
        }

        // ================= Park / Home =================

        public void Park()
        {
            EnsureConnected();
            _protocol.Park();
            lock (_state) { _gotoActive = false; }
        }

        public void Unpark()
        {
            EnsureConnected();
            _protocol.Unpark();
        }

        public void SetPark()
        {
            EnsureConnected();
            _protocol.SetPark();
        }

        public void FindHome()
        {
            EnsureConnected();
            _protocol.FindHome();
            lock (_state) { _gotoActive = true; }
        }

        public short SlewSettleTime
        {
            get { EnsureConnected(); return (short)DriverSettings.GetInt("SlewSettleTime", 0); }
            set { EnsureConnected(); DriverSettings.SetInt("SlewSettleTime", value); }
        }

        // ================= 设置对话框 =================

        public void SetupDialog()
        {
            using (var dlg = new SetupDialog())
                dlg.ShowDialog();
        }

        // ================= ASCOM 注册回调（regasm 时自动调用） =================
        // 必须放在被注册的 COM 类上：regasm 只为被注册类调用 [ComRegisterFunction]，
        // 静态类不会被调用（首次版本因此漏写了 ASCOM Profile 键）。

        [ComRegisterFunction]
        public static void RegisterAscomDriver(Type t)
        {
            DriverSettings.WriteProfileKeys();
        }

        [ComUnregisterFunction]
        public static void UnregisterAscomDriver(Type t)
        {
            DriverSettings.DeleteProfileKeys();
        }
    }
}
