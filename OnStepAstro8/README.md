# OnStepAstro8 —— ASTRO8-OnstepX 望远镜 ASCOM 驱动（HID / WiFi 双连接）

在 `OnStepHID`（仅 HID）基础上扩展的新版驱动：**保留原工程 OnStepHID 待测试**，
本工程独立注册（ProgId 不同，可与旧版并存）。除原有 HID 连接外，新增
**WiFi（TCP）连接**（默认 `192.168.0.1:9998`，参考原 ASCOM 串口驱动样式），
并在驱动属性页（Setup）提供完整的手动设置项：**经度、纬度、UTC offset、
时间、日期、赤经/赤纬限制、地平/天顶/子午线限制、当前 slew 移动速度、跟踪速度**，
全部字段**默认回读硬件（OnStep）当前设置，允许手动修改**，连接时自动下发。

An enhanced driver built on `OnStepHID` (HID only): **the original OnStepHID
project is kept untouched for testing**. This project registers independently
(different ProgId, so both can coexist). In addition to the original HID
connection, a **WiFi (TCP) connection** is added (default `192.168.0.1:9998`,
styled after the original ASCOM serial driver). The driver Setup dialog provides
full manual settings: **longitude, latitude, UTC offset, time, date, RA/Dec
limits, horizon/overhead/meridian limits, current slew speed, tracking rate** —
all fields **default to reading back the hardware (OnStep) current settings and
can be edited manually**; they are applied automatically on connect.

---

## 中文说明

### 1. 与原版 OnStepHID 的区别

| 项 | OnStepHID（旧，保留测试） | OnStepAstro8（新） |
|---|---|---|
| ProgId | `ASCOM.OnStepHID.Telescope` | `ASCOM.OnStepAstro8.Telescope` |
| 连接方式 | 仅 HID | **HID + WiFi(TCP)** |
| 属性页 | 仅 VID/PID/序列号 | VID/PID/序列号 + 地址/端口 + **站点/时间/限制/速度 全字段** |
| 回读硬件设置 | 无 | **"回读硬件设置"按钮**（一次性连接回读全部可读项） |
| GOTO 限制 | 无 | 驱动侧 RA/Dec 软限制（超限拒绝 GOTO） |
| 注册 | 已注册 | 已注册（两者可共存，Chooser 中显示带 " (HID/WiFi)" 后缀） |

### 2. 连接方式

#### HID（USB 转串口免驱芯片 / Rp2040HID 固件）

- VID `0x1A86`、PID `0x55D4`、序列号 `A8-0001`（与 Rp2040HID 固件一致）；
- Setup 中 VID/PID/序列号**精确匹配**才出现在下拉列表，否则提示"未找到 ASTRO8-OnstepX"并刷新。

#### WiFi（TCP，默认 192.168.0.1:9998）

- OnStep 的 WiFi 模块（板载 ESP32/ESP8266 或串口转 WiFi）把 TCP 端口数据原样转发到望远镜串口；
- 地址 + 端口可改（参考 ASCOM 串口驱动样式）；
- 连接失败（5 秒超时）会给出地址/局域网/端口检查提示。

### 3. 属性页字段（SetupDialog）

打开方式：ASCOM Chooser → 选 **ASTRO8-OnstepX (HID/WiFi)** → **Setup**。

#### 站点与时间（默认回读硬件，可手动修改）

| 字段 | 回读命令 | 写入命令 | 说明 |
|---|---|---|---|
| 经度 (°) | `:Gg#` | `:Sg(s)DDD*MM#` | 西经为负 |
| 纬度 (°) | `:Gt#` | `:StsDD*MM#` | 南纬为负 |
| UTC offset | `:GG#` | `:SGsHH:MM#` | 加此值得 UT1；建议整点/半点/三刻 |
| 本地时间 | `:GL#`（24h） | `:SLHH:MM:SS#` | 回读时按 UT1+offset 换算显示 |
| 本地日期 | `:GC#` | `:SCMM/DD/YY#` | 同左 |

- 点 **"回读硬件设置"** 一次性读回：经度、纬度、UTC offset、时间、日期、
  地平/天顶/东/西子午线限制、Axis2(Dec) 限位、当前 GOTO 速度、跟踪速率。
- 手动修改时间/日期后点"确定"会**立即写入硬件**；其余字段在**驱动连接时自动下发**。

#### 限制

| 分组 | 字段 | 默认 | 说明 |
|---|---|---|---|
| 驱动 GOTO 软限制 | RA 最小/最大 (h) | 0 / 24 | 超限拒绝 GOTO（SlewTo 抛异常） |
| 驱动 GOTO 软限制 | Dec 最小/最大 (°) | 回读 Axis2 限位 / +90 | 同上 |
| 硬件限制（可写） | 地平 (°) | 回读 | `:ShsDD#` |
| 硬件限制（可写） | 天顶 (°) | 回读 | `:SoDD#` |
| 硬件限制（可写） | 东子午线 (min) | 回读 | `:SXE9,n#`，GEM 上即 RA 方向限制 |
| 硬件限制（可写） | 西子午线 (min) | 回读 | `:SXEA,n#` |

- 硬件限制回读命令：`:Gh#` `:Go#` `:GXE9#` `:GXEA#`；
- Axis1(RA) 轴限位（`:GXEe#` min° / `:GXEB#` max h）回读后仅作参考展示（OnStep 不支持运行时写入轴限位）。

#### 速度

| 字段 | 回读 | 设置 | 说明 |
|---|---|---|---|
| 当前 GOTO 速度 | `:GX97#` (deg/s) | - | 只读参考 |
| 移动速率档 | -（无查询命令） | `:RG#`(1x) `:RC#`(8x) `:RM#`(20x) `:RF#`(48x) `:RS#`(半速) `:R0#~:R9#` | 影响后续 :Me#/:Mw#/:Mn#/:Ms# 连续移动 |
| 跟踪速率 | `:GU#` 状态解析 | `:TQ#`(恒星) `:TL#`(月球) `:TS#`(太阳) `:TK#`(King) | 连接时自动下发 |

### 4. 构建与注册

```bat
:: 1) 编译（Release，net48）
dotnet build OnStepAstro8.sln -c Release

:: 2) 注册（以管理员运行 RegisterDriver.bat，或手动）
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" /codebase "src\bin\Release\net48\OnStepAstro8.dll"
"%WINDIR%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe"     /codebase "src\bin\Release\net48\OnStepAstro8.dll"
```

- 产物：`src\bin\Release\net48\OnStepAstro8.dll`
- 注册表：`HKLM\SOFTWARE\ASCOM\Telescope Drivers\ASCOM.OnStepAstro8.Telescope`（含 32 位视图）
- 卸载：RegAsm /unregister（两条）
- 注：RA0000 警告为预期（HidSharp 无强命名，与旧版一致）

### 5. 联调（tester，无需 ASCOM）

```bat
:: 工程目录：tester\bin\Release\net48\
OnStepAstro8Tester list                          :: 列出 HID 设备
OnStepAstro8Tester test 1A86 55D4 A8-0001       :: HID 打开 + 握手 :GVP#
OnStepAstro8Tester cmd  1A86 55D4 :GX97#        :: HID 任意命令（当前 GOTO 速度）
OnStepAstro8Tester wtest 192.168.0.1 9998       :: WiFi 连接 + 握手
OnStepAstro8Tester wcmd  192.168.0.1 9998 :GVP# :: WiFi 任意命令
```

### 6. 工程结构

```
OnStepAstro8/
├── OnStepAstro8.sln
├── RegisterDriver.bat
├── src/
│   ├── OnStepAstro8.csproj
│   ├── ITransport.cs        # 传输抽象（Write/ReadUntilHash/Flush）
│   ├── HidTransport.cs      # HID 传输（32 字节报表、首字节=长度；序列号校验）
│   ├── WifiTransport.cs     # TCP 传输（默认 192.168.0.1:9998）
│   ├── OnStepProtocol.cs    # OnStep/LX200 命令编解码（含 UTC offset/限制/速率）
│   ├── SetupDialog.cs       # 属性页（连接方式 + 站点/时间/限制/速度 + 回读）
│   ├── Telescope.cs         # ITelescopeV3/V4 实现（双连接 + GOTO 软限制）
│   ├── DriverSettings.cs    # Profile 存储 + Chooser 注册
│   └── TrackingRates.cs
└── tester/
    └── Program.cs           # list/test/cmd/wtest/wcmd
```

### 7. 说明与限制

- 本工程未做真机联调（无 OnStep WiFi 与硬件现场）；HID 链路与 Rp2040HID 固件
  的兼容性已由原 OnStepHID 验证，WiFi 链路请用 `wtest` 先确认模块是原始 TCP 转发
  （部分模块默认 HTTP，需要切换模式或改端口）。
- 驱动侧 RA/Dec 软限制默认 0-24h / -90..+90°（不拦截）；在属性页改窄后，
  GOTO（SlewToCoordinates/SlewToTarget）超出范围会拒绝并给出提示。
- OnStep 的 Axis1(RA)/Axis2(Dec) 轴限位仅在固件编译时配置，运行时可读不可写；
  因此"赤经限制"用可写的东/西子午线限制表达，"赤纬限制"为驱动侧软限制 + Axis2 限位参考。

---

## English

### 1. Differences from the original OnStepHID

| Item | OnStepHID (old, kept for testing) | OnStepAstro8 (new) |
|---|---|---|
| ProgId | `ASCOM.OnStepHID.Telescope` | `ASCOM.OnStepAstro8.Telescope` |
| Connection | HID only | **HID + WiFi(TCP)** |
| Setup page | VID/PID/serial only | VID/PID/serial + host/port + **site/time/limits/speed fields** |
| Hardware read-back | none | **"Read hardware settings" button** (one-shot connect, reads all readable items) |
| GOTO limits | none | Driver-side RA/Dec soft limits (GOTO rejected outside range) |
| Registration | registered | registered (coexists with the old one; Chooser shows " (HID/WiFi)" suffix) |

### 2. Connection methods

#### HID (driverless USB-serial chip / Rp2040HID firmware)

- VID `0x1A86`, PID `0x55D4`, serial `A8-0001` (identical to the Rp2040HID firmware);
- In Setup, only devices matching VID/PID/serial **exactly** appear in the
  dropdown; otherwise "ASTRO8-OnstepX not found" is shown and you are asked to refresh.

#### WiFi (TCP, default 192.168.0.1:9998)

- The OnStep WiFi module (onboard ESP32/ESP8266 or a serial-to-WiFi bridge)
  transparently forwards TCP-port data to the mount serial port;
- Host and port are editable (styled after the ASCOM serial driver);
- A failed connect (5 s timeout) gives a message about address/LAN/port checks.

### 3. Setup page fields (SetupDialog)

Open: ASCOM Chooser → pick **ASTRO8-OnstepX (HID/WiFi)** → **Setup**.

#### Site & time (defaults to hardware read-back, editable)

| Field | Read command | Write command | Notes |
|---|---|---|---|
| Longitude (°) | `:Gg#` | `:Sg(s)DDD*MM#` | negative for west |
| Latitude (°) | `:Gt#` | `:StsDD*MM#` | negative for south |
| UTC offset | `:GG#` | `:SGsHH:MM#` | added to local time to get UT1; use whole/half/three-quarter hours |
| Local time | `:GL#` (24 h) | `:SLHH:MM:SS#` | shown as UT1 + offset after read-back |
| Local date | `:GC#` | `:SCMM/DD/YY#` | same |

- **"Read hardware settings"** reads back in one go: longitude, latitude, UTC
  offset, time, date, horizon/overhead/east/west meridian limits, Axis2(Dec)
  limits, current GOTO speed, tracking rate.
- After editing time/date manually, clicking **OK** writes them to the hardware
  immediately; the remaining fields are applied automatically when the driver connects.

#### Limits

| Group | Field | Default | Notes |
|---|---|---|---|
| Driver GOTO soft limits | RA min/max (h) | 0 / 24 | GOTO outside range is rejected (SlewTo throws) |
| Driver GOTO soft limits | Dec min/max (°) | Axis2 read-back / +90 | same |
| Hardware (writable) | Horizon (°) | read-back | `:ShsDD#` |
| Hardware (writable) | Overhead (°) | read-back | `:SoDD#` |
| Hardware (writable) | East meridian (min) | read-back | `:SXE9,n#`; on a GEM this is the RA-direction limit |
| Hardware (writable) | West meridian (min) | read-back | `:SXEA,n#` |

- Hardware limit read commands: `:Gh#` `:Go#` `:GXE9#` `:GXEA#`;
- Axis1(RA) axis limits (`:GXEe#` min° / `:GXEB#` max h) are shown as reference
  only after read-back (OnStep does not support runtime writes to axis limits).

#### Speed

| Field | Read-back | Set | Notes |
|---|---|---|---|
| Current GOTO speed | `:GX97#` (deg/s) | - | read-only reference |
| Move rate preset | - (no query command) | `:RG#`(1x) `:RC#`(8x) `:RM#`(20x) `:RF#`(48x) `:RS#`(half) `:R0#~:R9#` | affects subsequent :Me#/:Mw#/:Mn#/:Ms# continuous moves |
| Tracking rate | `:GU#` status parse | `:TQ#`(sidereal) `:TL#`(lunar) `:TS#`(solar) `:TK#`(King) | applied on connect |

### 4. Build & register

```bat
:: 1) Build (Release, net48)
dotnet build OnStepAstro8.sln -c Release

:: 2) Register (run RegisterDriver.bat as Administrator, or manually)
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" /codebase "src\bin\Release\net48\OnStepAstro8.dll"
"%WINDIR%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe"     /codebase "src\bin\Release\net48\OnStepAstro8.dll"
```

- Output: `src\bin\Release\net48\OnStepAstro8.dll`
- Registry: `HKLM\SOFTWARE\ASCOM\Telescope Drivers\ASCOM.OnStepAstro8.Telescope` (plus the 32-bit view)
- Uninstall: RegAsm /unregister (both)
- Note: the RA0000 warning is expected (HidSharp is not strong-named, same as the old version)

### 5. Hardware testing (tester, no ASCOM needed)

```bat
:: tool dir: tester\bin\Release\net48\
OnStepAstro8Tester list                          :: list HID devices
OnStepAstro8Tester test 1A86 55D4 A8-0001       :: HID open + handshake :GVP#
OnStepAstro8Tester cmd  1A86 55D4 :GX97#        :: HID arbitrary command (current GOTO speed)
OnStepAstro8Tester wtest 192.168.0.1 9998       :: WiFi connect + handshake
OnStepAstro8Tester wcmd  192.168.0.1 9998 :GVP# :: WiFi arbitrary command
```

### 6. Project layout

```
OnStepAstro8/
├── OnStepAstro8.sln
├── RegisterDriver.bat
├── src/
│   ├── OnStepAstro8.csproj
│   ├── ITransport.cs        # transport abstraction (Write/ReadUntilHash/Flush)
│   ├── HidTransport.cs      # HID transport (32-byte report, first byte = length; serial check)
│   ├── WifiTransport.cs     # TCP transport (default 192.168.0.1:9998)
│   ├── OnStepProtocol.cs    # OnStep/LX200 command codec (incl. UTC offset/limits/rates)
│   ├── SetupDialog.cs       # properties page (connection + site/time/limits/speed + read-back)
│   ├── Telescope.cs         # ITelescopeV3/V4 implementation (dual connection + GOTO soft limits)
│   ├── DriverSettings.cs    # Profile storage + Chooser registration
│   └── TrackingRates.cs
└── tester/
    └── Program.cs           # list/test/cmd/wtest/wcmd
```

### 7. Notes & limitations

- This project has not been tested against real hardware (no OnStep WiFi / bench
  available here). The HID path is verified for compatibility with the Rp2040HID
  firmware by the original OnStepHID driver; for the WiFi path, first confirm
  with `wtest` that the module is raw TCP forwarding (some modules default to
  HTTP and need a mode switch or a different port).
- The driver-side RA/Dec soft limits default to 0-24 h / -90..+90° (no blocking);
  once narrowed in the properties page, GOTO (SlewToCoordinates/SlewToTarget)
  outside the range is rejected with a clear message.
- OnStep Axis1(RA)/Axis2(Dec) axis limits are compile-time configuration and are
  readable but not writable at runtime. Therefore "RA limit" is expressed by the
  writable east/west meridian limits, and "Dec limit" is the driver-side soft
  limit plus the Axis2 limit reference.
