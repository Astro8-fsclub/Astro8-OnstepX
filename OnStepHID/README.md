# ASTRO8-OnstepX —— HID 接口的 ASCOM 望远镜驱动 / HID ASCOM Telescope Driver

> 让 OnStep / OnStepX 赤道仪通过 **HID 转串口免驱芯片**直连电脑 USB，以 **HID 免驱**方式被 ASCOM 客户端（N.I.N.A. / Cartes du Ciel / ASCOM 望远镜 Simulator 等）识别和使用，完全替代原来的串口线。
>
> 驱动产品名称：**ASTRO8-OnstepX**（Chooser 中显示此名称）。
> 连接时校验芯片**序列号（默认 A8-0001）**，避免多台同 VID/PID 设备冲突。

> Connect an **OnStep / OnStepX equatorial mount** to the PC USB port through a
> **driverless HID-to-serial chip**; the mount is then recognized and used by
> ASCOM clients (N.I.N.A. / Cartes du Ciel / ASCOM Telescope Simulator, etc.)
> over **driverless HID**, fully replacing the serial cable.
>
> Driver product name: **ASTRO8-OnstepX** (shown in the Chooser).
> On connect the driver verifies the chip **serial number (default A8-0001)** to
> avoid conflicts among devices sharing the same VID/PID.

> ⚠️ **型号校正**：最初沟通时写的 “CH9236” 型号不存在，实际为 HID 转串口免驱芯片（内置晶振，USB 全速 12Mbps，默认 9600bps 串口，波特率与 VID/PID、字符串描述符均可配置）。本驱动按该系列芯片实现，兼容常见型号及其升级型号。
>
> ⚠️ **Model note**: the "CH9236" model mentioned early on does not exist; the
> actual chip is a driverless HID-to-serial bridge (built-in crystal, USB
> Full-Speed 12 Mbps, default 9600 bps serial; baud rate, VID/PID and string
> descriptors are all configurable). This driver targets that chip family and is
> compatible with common models and their upgrades.

---

## 中文说明

### 1. 系统框图

```
┌─────────────┐  ASCOM 客户端 (N.I.N.A. / CdC / 行星相机等)
│  Chooser    │── 选择 "ASTRO8-OnstepX" ──┐
└─────────────┘                           ▼
                                   ┌──────────────────────────────────────┐
                                   │        OnStepHID.dll（本驱动）        │
                                   │  ITelescopeV3 + ITelescopeV4 实现      │
                                   │  HidSharp → 枚举/读写 HID 报表         │
                                   └──────────────────────────────────────┘
                                                      │  USB HID 报表（32 字节/包，免驱动）
                                                      ▼
                                   ┌──────────────────────────────────────┐
                                   │  HID 转串口芯片（HID 转 UART）         │
                                   │  USB 侧：HID 免驱  |  UART 侧：TTL 3.3V │
                                   └──────────────────────────────────────┘
                                                      │  TTL 串口 (9600 8N1，OnStep 默认)
                                                      ▼
                                   ┌──────────────────────────────────────┐
                                   │  OnStep / OnStepX 控制板（LX200 协议）  │
                                   └──────────────────────────────────────┘
```

报文规则：**USB 报表固定 32 字节，首字节 = 本次有效串口数据长度，其后才是串口数据**。
例：写 31 字节数据 → 组包为 `0x1F, data[0..30]`；读方向同理，收到的报表首字节为有效长度。

### 2. 硬件接线

| 芯片引脚 | 方向 | 接 OnStep 控制板 |
|---|---|---|
| VCC（5V/3.3V） | - | 电源 3.3–5V（按板子逻辑电平） |
| GND | - | GND 共地（必须） |
| TXD（发送） | 芯片→OnStep | 接 OnStep 的 **RX**（主控串口接收） |
| RXD（接收） | 芯片←OnStep | 接 OnStep 的 **TX**（主控串口发送） |

- OnStep 默认串口参数 **9600 baud, 8, N, 1**；若你改过 `config.h`（`AXIS1_...` / `SERIAL_BAUD_DEFAULT`），需用芯片配置工具把波特率改为一致。
- 确保芯片串口电平与 OnStep 主控一致（多数板为 3.3V TTL；如遇电平不匹配需加电平转换）。
- USB 数据线请用带屏蔽的短线，供电不足时给芯片独立供电。

### 3. 芯片配置（可选，但建议）

芯片出厂通常可直接用；如 VID/PID 被改过，或波特率不匹配，用官方工具：

1. **HIDAssist**（调试软件）：查看设备、收发数据测试。
2. **配置工具**（SetCfg 系列）：修改 **VID / PID / 产品字符串（建议设为 ASTRO8-OnstepX）/ 序列号 / 波特率** 等。
3. 本驱动默认按 `VID=0x1A86  PID=0x55D4` 查找；如果你的芯片是其它 PID，**无需重新编译**，在驱动 Setup 对话框里输入实际 VID/PID 即可（或点“扫描设备”从列表选）。

> 提示：下拉列表**只显示与 VID/PID/序列号完全匹配的设备**；匹配不到会提示“未找到 ASTRO8-OnstepX”，点击“扫描设备”刷新即可。序列号用于区分同 VID/PID 的多台设备。

### 4. 编译

本机环境：Windows + .NET SDK（无 Visual Studio 也能编）。工程目标框架 **net48**。

```bat
cd OnStepHID
dotnet build -c Release
```

产物：`src\bin\Release\net48\OnStepHID.dll`（驱动）+ `tester\bin\Release\net48\OnStepHIDTester.exe`（联调工具）。

已内置 NuGet 依赖：ASCOM.DeviceInterfaces 7.1.2、ASCOM.Exception.Library 7.1.2、HidSharp 2.1.0、
Microsoft.NETFramework.ReferenceAssemblies（无 VS 命令行编译用）。

### 5. 安装 / 注册驱动（需管理员）

在工程目录以**管理员身份**运行：

```bat
RegisterDriver.bat
```

脚本做的事：
1. `RegAsm /codebase` 分别注册 64 位与 32 位 COM（ProgID：`ASCOM.OnStepHID.Telescope`）；
2. 通过 `[ComRegisterFunction]` 自动写入 ASCOM Profile 键：
   - `HKLM\SOFTWARE\WOW6432Node\ASCOM\Telescope Drivers\ASCOM.OnStepHID.Telescope`
   - `HKLM\SOFTWARE\ASCOM\Telescope Drivers\ASCOM.OnStepHID.Telescope`
   - 键默认值 = 驱动描述，Chooser 据此枚举。

卸载：

```bat
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" /unregister src\bin\Release\net48\OnStepHID.dll
"%WINDIR%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe"     /unregister src\bin\Release\net48\OnStepHID.dll
```

> RegAsm 对未强命名程序集使用 `/codebase` 时的 RA0000 警告属正常现象（ASCOM 驱动常见做法），不影响使用。

### 6. 在 ASCOM 客户端中使用

1. 插好 HID 转串口芯片（应看到设备管理器中新增一个 HID 设备，无驱动感叹号）；
2. 打开 N.I.N.A. / CdC 等的望远镜设置，在 **ASCOM Chooser** 中选择 **“ASTRO8-OnstepX”**；
3. 点 **Setup**：
   - 默认 VID=1A86 / PID=55D4；不对就手填后点“扫描设备”或“使用此 VID/PID”——下拉列表**只显示 VID、PID、序列号全部匹配的设备**，不匹配时提示“未找到 ASTRO8-OnstepX”，点“扫描设备”刷新；
   - **序列号验证**：默认 `A8-0001`，与设备实际序列号不一致时**连接会被拒绝**（提示“序列号验证失败”）；留空或填 `*` 表示跳过验证；
   - 点 **“测试连接（握手 :GVP#）”**：会依次校验 VID/PID、序列号、OnStep 握手，全部通过才显示“连接成功”；
   - 确定保存后，连接望远镜即可。
4. 首次连接时若提示校准，按 N.I.N.A. 向导选择“OnStep”协议风格即可（驱动内部已实现 LX200 兼容命令集）。

### 7. 无需 ASCOM 的硬件联调（推荐先做）

先不装 ASCOM，用自带工具验证“电脑 → 芯片 → OnStep”链路：

```bat
OnStepHIDTester list                    :: 列出系统 HID 设备（含 VID/PID/产品名/序列号）
OnStepHIDTester test 1A86 55D4 A8-0001  :: 打开设备，校验序列号并握手（:GVP#/:GVM#）
OnStepHIDTester test 1A86 55D4          :: 只握手，不校验序列号
OnStepHIDTester cmd 1A86 55D4 ":GR#"    :: 发任意命令看应答（如读 RA）
```

> 注意：`list` 会显示**系统全部 HID 设备**（含触摸屏等），未插入目标设备时也能看到其它设备，属正常现象。

### 8. 已实现的协议命令（LX200 / OnStep 子集）

依据 OnStepX 官方 `COMMAND_REFERENCE.md`（hjd1964/OnStepX）。

| 功能 | 命令 |
|---|---|
| 握手 / 固件 | `:GVP#` / `:GVM#` |
| 位置 | `:GR#`（RA 时）/ `:GD#`（Dec 度）/ `:GA#`（Alt）/ `:GZ#`（Az）/ `:GS#`（恒星时） |
| 目标 | `:Sr{hh:mm:ss}#` / `:Sd{sDD*MM:SS}#` / `:Sa#` / `:Sz#` / `:Gr#` / `:Gd#` |
| 指向 | `:MS#`（EQ GOTO）/ `:MA#`（AltAz GOTO）/ `:CS#`（同步） |
| 跟踪 | `:Te#`/`:Td#`/`:TQ#`（开/关/星表速率） / `:GT#`（查询）/ `:TL#`（设置速率） |
| 导星 | `:Mg{d}{n}#` 脉冲（n=毫秒）/ `:RA#`/`:RE#` 速率（0.5×）/ `:RR#`/`:RW#`/`:Rn#`/`:Rs#` 速率偏移 |
| 移动 | `:Mn#/:Ms#/:Me#/:Mw#` 开始，`:Qn#` 等停止，`:Q#` 全停 |
| 状态 | `:GU#` 状态串（GOTO/Park/Home/侧位/速率档/挂载类型）/ `:GXTR#` 速率偏移查询 |
| 附加 | `:hP#` Park / `:hR#` Unpark / `:hC#` 设 Park / `:hF#` 回零位 / `:hH#` Home / `:hZ#` 零位 |
| 时间 | `:SL{hh:mm:ss}#` / `:SC{mm/dd/yy}#` / `:GL#` / `:GC#` / `:GX80#`/`:GX81#`（UTC 相关） |
| 站点 | `:St{sDD*MM:SS}#`（纬度）/ `:Sg{...}#`（经度）/ `:Gt#` / `:Gg#` / `:GXEM#`（挂载类型） |

驱动层面同时实现了 **ITelescopeV3 + ITelescopeV4**（InterfaceVersion=3），覆盖：GOTO/同步/跟踪/导星/移动/
Park/Home/脉冲导星/速率偏移/站点与时间/`Action`（`OnStepCommand`、`GetFirmware`）/`CommandBlind|Bool|String` 透传。

### 9. 故障排查

| 现象 | 原因 / 处理 |
|---|---|
| Chooser 里没有本驱动 | 用管理员跑 `RegisterDriver.bat`；确认 `HKLM\SOFTWARE\WOW6432Node\ASCOM\Telescope Drivers\ASCOM.OnStepHID.Telescope` 存在 |
| 测试连接提示“未找到设备” | VID/PID 不对：用 `OnStepHIDTester list` 查实际值，在 Setup 里改；或设备被其它软件占用 |
| 测试连接提示“序列号验证失败” | 期望序列号与设备实际 S/N 不一致：用 `list` 看实际序列号，在 Setup 里改成一致；或留空跳过验证 |
| 能打开但握手无应答 | 波特率不匹配（OnStep 不是 9600）/ RX-TX 接反 / 未共地 / OnStep 未上电 |
| N.I.N.A. 报“Not Connected” | 先在 Setup 里通过“测试连接”，再用 tester `cmd` 验证 `:GR#` 有返回 |
| 中文乱码 | 无（协议纯 ASCII）；若 OnStep 固件输出过非 ASCII 字符会忽略 |

### 10. 工程结构

```
OnStepHID/
├── OnStepHID.sln
├── RegisterDriver.bat          # 管理员注册脚本（纯 ASCII，避免 cmd 编码问题）
├── src/
│   ├── OnStepHID.csproj        # net48 + ASCOM 7.1.2 + HidSharp 2.1.0
│   ├── HidTransport.cs         # HID 传输层（32 字节报文/首字节长度/序列号读取）
│   ├── OnStepProtocol.cs       # LX200/OnStep 协议编解码
│   ├── Telescope.cs            # 主驱动类（ITelescopeV3+V4，序列号验证 + ComRegister 回调）
│   ├── DriverSettings.cs       # ASCOM Profile 注册表 + VID/PID/序列号设置
│   ├── TrackingRates.cs        # 速率集合
│   ├── SetupDialog.cs          # 设置对话框（按 VID/PID/序列号过滤扫描/握手测试）
│   └── Properties\AssemblyInfo.cs
└── tester/
    ├── OnStepHIDTester.csproj
    └── Program.cs              # list / test [S/N] / cmd 联调工具
```

### 11. 验证记录（本机实测）

- `dotnet build -c Release`：**0 警告 0 错误**；
- `OnStepHIDTester list`：正常枚举系统 HID 设备（含 S/N）；
- 反射验证：`Telescope` 实现 `ITelescopeV3/ITelescopeV4/IDisposable`，`[ComRegisterFunction]`/`[ComUnregisterFunction]` 存在；
- `regasm` 实注册：64/32 位均 "Types registered successfully"，ASCOM Profile 键（32/64 视图）默认值 = **ASTRO8-OnstepX**，InterfaceVersion=3；
- COM 实例化：`Name = ASTRO8-OnstepX`，`Description = ASTRO8-OnstepX`，`Connected = False`；
- `SetupDialog` 实例化冒烟：正常创建，序列号输入框默认 `A8-0001`。

> 未做项：本机未插硬件真机，**序列号校验与真机收发联调需在硬件到手后**用 `OnStepHIDTester test 1A86 55D4 A8-0001` 完成。

---

## English

### 1. System diagram

```
┌─────────────┐  ASCOM client (N.I.N.A. / CdC / planet cameras, etc.)
│  Chooser    │── pick "ASTRO8-OnstepX" ──┐
└─────────────┘                           ▼
                                   ┌──────────────────────────────────────┐
                                   │      OnStepHID.dll (this driver)     │
                                   │  ITelescopeV3 + ITelescopeV4 impl     │
                                   │  HidSharp → enumerate/read/write HID │
                                   └──────────────────────────────────────┘
                                                      │  USB HID reports (32 B/pkt, driverless)
                                                      ▼
                                   ┌──────────────────────────────────────┐
                                   │  HID-to-serial chip (HID ↔ UART)     │
                                   │  USB side: HID, driverless | UART: TTL│
                                   └──────────────────────────────────────┘
                                                      │  TTL serial (9600 8N1, OnStep default)
                                                      ▼
                                   ┌──────────────────────────────────────┐
                                   │  OnStep / OnStepX board (LX200)      │
                                   └──────────────────────────────────────┘
```

Report rule: **USB reports are fixed 32 bytes; the first byte is the length of
the valid serial data that follows**. E.g. writing 31 bytes of data → packet
`0x1F, data[0..30]`; the read direction works the same — the first byte of a
received report is the valid length.

### 2. Wiring

| Chip pin | Direction | Connect to OnStep board |
|---|---|---|
| VCC (5V/3.3V) | - | power 3.3–5V (match board logic level) |
| GND | - | GND common ground (required) |
| TXD (transmit) | chip→OnStep | OnStep **RX** (main serial receive) |
| RXD (receive) | chip←OnStep | OnStep **TX** (main serial transmit) |

- OnStep default serial: **9600 baud, 8, N, 1**; if you changed `config.h`
  (`AXIS1_...` / `SERIAL_BAUD_DEFAULT`), reconfigure the chip baud to match.
- Keep the chip serial level consistent with the OnStep MCU (most boards are
  3.3V TTL; add a level shifter if they differ).
- Use a short shielded USB cable; give the chip independent power if needed.

### 3. Chip configuration (optional but recommended)

The chip usually works out of the box; if VID/PID were changed or the baud
mismatches, use the vendor tools:

1. **HIDAssist** (debug tool): inspect the device, send/receive test data.
2. **Configuration tool** (SetCfg family): change **VID / PID / product string
   (recommend ASTRO8-OnstepX) / serial number / baud rate** etc.
3. The driver looks for `VID=0x1A86  PID=0x55D4` by default; if your chip uses a
   different PID, **no recompile is needed** — enter the actual VID/PID in the
   driver Setup dialog (or click "Scan devices" to pick from the list).

> Note: the dropdown **only shows devices fully matching VID/PID/serial**; if
> nothing matches, "ASTRO8-OnstepX not found" is shown — click "Scan devices" to
> refresh. The serial number distinguishes multiple devices sharing VID/PID.

### 4. Build

Environment: Windows + .NET SDK (no Visual Studio required). Target framework **net48**.

```bat
cd OnStepHID
dotnet build -c Release
```

Output: `src\bin\Release\net48\OnStepHID.dll` (driver) + `tester\bin\Release\net48\OnStepHIDTester.exe` (test tool).

NuGet dependencies bundled: ASCOM.DeviceInterfaces 7.1.2, ASCOM.Exception.Library
7.1.2, HidSharp 2.1.0, Microsoft.NETFramework.ReferenceAssemblies (for CLI builds without VS).

### 5. Install / register the driver (administrator)

Run **as Administrator** in the project directory:

```bat
RegisterDriver.bat
```

What the script does:
1. `RegAsm /codebase` registers both 64-bit and 32-bit COM (ProgID: `ASCOM.OnStepHID.Telescope`);
2. `[ComRegisterFunction]` writes the ASCOM Profile keys automatically:
   - `HKLM\SOFTWARE\WOW6432Node\ASCOM\Telescope Drivers\ASCOM.OnStepHID.Telescope`
   - `HKLM\SOFTWARE\ASCOM\Telescope Drivers\ASCOM.OnStepHID.Telescope`
   - the default value = driver description, used by the Chooser enumeration.

Uninstall:

```bat
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" /unregister src\bin\Release\net48\OnStepHID.dll
"%WINDIR%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe"     /unregister src\bin\Release\net48\OnStepHID.dll
```

> The RA0000 warning from RegAsm `/codebase` on a non-strong-named assembly is
> normal (common for ASCOM drivers) and does not affect usage.

### 6. Use in an ASCOM client

1. Plug in the HID-to-serial chip (a new driverless HID device appears in Device Manager);
2. In N.I.N.A. / CdC etc. open telescope settings and pick **"ASTRO8-OnstepX"** in the **ASCOM Chooser**;
3. Click **Setup**:
   - Default VID=1A86 / PID=55D4; if wrong, type them and click "Scan devices"
     or "Use this VID/PID" — the dropdown **only shows devices matching VID,
     PID and serial exactly**; on no match it says "ASTRO8-OnstepX not found",
     click "Scan devices" to refresh;
   - **Serial verification**: default `A8-0001`; if it differs from the device
     serial, **connection is refused** ("Serial verification failed"); leave
     blank or `*` to skip;
   - Click **"Test connection (handshake :GVP#)"**: it checks VID/PID, serial and
     the OnStep handshake in order; only then "Connected" is shown;
   - OK to save, then connect the telescope.
4. If the first connection asks for calibration, follow the N.I.N.A. wizard and
   pick the "OnStep" protocol style (the driver already implements the LX200
   compatible command set).

### 7. Hardware testing without ASCOM (recommended first)

Verify the "PC → chip → OnStep" chain before installing ASCOM:

```bat
OnStepHIDTester list                    :: list system HID devices (VID/PID/name/serial)
OnStepHIDTester test 1A86 55D4 A8-0001  :: open device, verify serial, handshake (:GVP#/:GVM#)
OnStepHIDTester test 1A86 55D4          :: handshake only, no serial check
OnStepHIDTester cmd 1A86 55D4 ":GR#"    :: send any command and see the reply (e.g. read RA)
```

> Note: `list` shows **all system HID devices** (including touchscreens); seeing
> other devices when the target is not plugged in is normal.

### 8. Implemented protocol commands (LX200 / OnStep subset)

Per the official OnStepX `COMMAND_REFERENCE.md` (hjd1964/OnStepX).

| Function | Commands |
|---|---|
| Handshake / firmware | `:GVP#` / `:GVM#` |
| Position | `:GR#` (RA h) / `:GD#` (Dec °) / `:GA#` (Alt) / `:GZ#` (Az) / `:GS#` (sidereal time) |
| Target | `:Sr{hh:mm:ss}#` / `:Sd{sDD*MM:SS}#` / `:Sa#` / `:Sz#` / `:Gr#` / `:Gd#` |
| Pointing | `:MS#` (EQ GOTO) / `:MA#` (AltAz GOTO) / `:CS#` (sync) |
| Tracking | `:Te#`/`:Td#`/`:TQ#` (on/off/sidereal) / `:GT#` (query) / `:TL#` (set rate) |
| Guiding | `:Mg{d}{n}#` pulse (n=ms) / `:RA#`/`:RE#` rate (0.5×) / `:RR#`/`:RW#`/`:Rn#`/`:Rs#` rate offset |
| Move | `:Mn#/:Ms#/:Me#/:Mw#` start, `:Qn#` etc. stop, `:Q#` stop all |
| Status | `:GU#` status string (GOTO/Park/Home/side/rate/mount type) / `:GXTR#` rate offset query |
| Extra | `:hP#` Park / `:hR#` Unpark / `:hC#` set Park / `:hF#` go to zero / `:hH#` Home / `:hZ#` zero |
| Time | `:SL{hh:mm:ss}#` / `:SC{mm/dd/yy}#` / `:GL#` / `:GC#` / `:GX80#`/`:GX81#` (UTC related) |
| Site | `:St{sDD*MM:SS}#` (lat) / `:Sg{...}#` (lon) / `:Gt#` / `:Gg#` / `:GXEM#` (mount type) |

The driver implements **ITelescopeV3 + ITelescopeV4** (InterfaceVersion=3),
covering: GOTO/sync/tracking/guiding/move/Park/Home/pulse-guide/rate offset/
site & time/`Action` (`OnStepCommand`, `GetFirmware`)/`CommandBlind|Bool|String` passthrough.

### 9. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Driver missing in Chooser | Run `RegisterDriver.bat` as admin; check `HKLM\SOFTWARE\WOW6432Node\ASCOM\Telescope Drivers\ASCOM.OnStepHID.Telescope` exists |
| "Device not found" on test connect | Wrong VID/PID: get real values with `OnStepHIDTester list`, change in Setup; or the device is busy |
| "Serial verification failed" | Expected serial differs from the actual S/N: read it with `list`, match it in Setup, or leave blank to skip |
| Opens but no handshake reply | Baud mismatch (OnStep not 9600) / RX-TX swapped / no common ground / OnStep unpowered |
| N.I.N.A. reports "Not Connected" | First pass "Test connection" in Setup, then verify `:GR#` replies with tester `cmd` |
| Chinese mojibake | N/A (pure ASCII protocol); non-ASCII output from the firmware is ignored |

### 10. Project layout

```
OnStepHID/
├── OnStepHID.sln
├── RegisterDriver.bat          # admin registration script (pure ASCII)
├── src/
│   ├── OnStepHID.csproj        # net48 + ASCOM 7.1.2 + HidSharp 2.1.0
│   ├── HidTransport.cs         # HID transport (32 B report / first-byte length / serial read)
│   ├── OnStepProtocol.cs       # LX200/OnStep protocol codec
│   ├── Telescope.cs            # main driver class (V3+V4, serial check + ComRegister callback)
│   ├── DriverSettings.cs       # ASCOM Profile registry + VID/PID/serial settings
│   ├── TrackingRates.cs        # tracking rate set
│   ├── SetupDialog.cs          # setup dialog (scan filtered by VID/PID/serial, handshake test)
│   └── Properties\AssemblyInfo.cs
└── tester/
    ├── OnStepHIDTester.csproj
    └── Program.cs              # list / test [S/N] / cmd test tool
```

### 11. Verification record (local, real)

- `dotnet build -c Release`: **0 warnings 0 errors**;
- `OnStepHIDTester list`: enumerates system HID devices correctly (with S/N);
- Reflection check: `Telescope` implements `ITelescopeV3/ITelescopeV4/IDisposable`,
  `[ComRegisterFunction]`/`[ComUnregisterFunction]` present;
- `regasm` real registration: 64/32-bit both "Types registered successfully",
  ASCOM Profile keys (32/64 views) default = **ASTRO8-OnstepX**, InterfaceVersion=3;
- COM instantiation: `Name = ASTRO8-OnstepX`, `Description = ASTRO8-OnstepX`, `Connected = False`;
- `SetupDialog` smoke instantiation: OK; serial box defaults to `A8-0001`.

> Not done: no real hardware on this machine — **serial verification and
> end-to-end traffic testing must be done once hardware arrives** using
> `OnStepHIDTester test 1A86 55D4 A8-0001`.
