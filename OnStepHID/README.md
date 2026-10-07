# ASTRO8-OnstepX —— HID 接口的 ASCOM 望远镜驱动

> 让 OnStep / OnStepX 赤道仪通过 **HID 转串口免驱芯片**直连电脑 USB，以 **HID 免驱**方式被 ASCOM 客户端（N.I.N.A. / Cartes du Ciel / ASCOM 望远镜 Simulator 等）识别和使用，完全替代原来的串口线。
>
> 驱动产品名称：**ASTRO8-OnstepX**（Chooser 中显示此名称）。
> 连接时校验芯片**序列号（默认 A8-0001）**，避免多台同 VID/PID 设备冲突。

> ⚠️ **型号校正**：最初沟通时写的 “CH9236” 型号不存在，实际为 HID 转串口免驱芯片（内置晶振，USB 全速 12Mbps，默认 9600bps 串口，波特率与 VID/PID、字符串描述符均可配置）。本驱动按该系列芯片实现，兼容常见型号及其升级型号。

---

## 1. 系统框图

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

---

## 2. 硬件接线

| 芯片引脚 | 方向 | 接 OnStep 控制板 |
|---|---|---|
| VCC（5V/3.3V） | - | 电源 3.3–5V（按板子逻辑电平） |
| GND | - | GND 共地（必须） |
| TXD（发送） | 芯片→OnStep | 接 OnStep 的 **RX**（主控串口接收） |
| RXD（接收） | 芯片←OnStep | 接 OnStep 的 **TX**（主控串口发送） |

- OnStep 默认串口参数 **9600 baud, 8, N, 1**；若你改过 `config.h`（`AXIS1_...` / `SERIAL_BAUD_DEFAULT`），需用芯片配置工具把波特率改为一致。
- 确保芯片串口电平与 OnStep 主控一致（多数板为 3.3V TTL；如遇电平不匹配需加电平转换）。
- USB 数据线请用带屏蔽的短线，供电不足时给芯片独立供电。

---

## 3. 芯片配置（可选，但建议）

芯片出厂通常可直接用；如 VID/PID 被改过，或波特率不匹配，用官方工具：

1. **HIDAssist**（调试软件）：查看设备、收发数据测试。
2. **配置工具**（SetCfg 系列）：修改 **VID / PID / 产品字符串（建议设为 ASTRO8-OnstepX）/ 序列号 / 波特率** 等。
3. 本驱动默认按 `VID=0x1A86  PID=0x55D4` 查找；如果你的芯片是其它 PID，**无需重新编译**，在驱动 Setup 对话框里输入实际 VID/PID 即可（或点“扫描设备”从列表选）。

> 提示：下拉列表**只显示与 VID/PID/序列号完全匹配的设备**；匹配不到会提示“未找到 ASTRO8-OnstepX”，点击“扫描设备”刷新即可。序列号用于区分同 VID/PID 的多台设备。

---

## 4. 编译

本机环境：Windows + .NET SDK（无 Visual Studio 也能编）。工程目标框架 **net48**。

```bat
cd OnStepHID
dotnet build -c Release
```

产物：`src\bin\Release\net48\OnStepHID.dll`（驱动）+ `tester\bin\Release\net48\OnStepHIDTester.exe`（联调工具）。

已内置 NuGet 依赖：ASCOM.DeviceInterfaces 7.1.2、ASCOM.Exception.Library 7.1.2、HidSharp 2.1.0、
Microsoft.NETFramework.ReferenceAssemblies（无 VS 命令行编译用）。

---

## 5. 安装 / 注册驱动（需管理员）

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

---

## 6. 在 ASCOM 客户端中使用

1. 插好 HID 转串口芯片（应看到设备管理器中新增一个 HID 设备，无驱动感叹号）；
2. 打开 N.I.N.A. / CdC 等的望远镜设置，在 **ASCOM Chooser** 中选择 **“ASTRO8-OnstepX”**；
3. 点 **Setup**：
   - 默认 VID=1A86 / PID=55D4；不对就手填后点“扫描设备”或“使用此 VID/PID”——下拉列表**只显示 VID、PID、序列号全部匹配的设备**，不匹配时提示“未找到 ASTRO8-OnstepX”，点“扫描设备”刷新；
   - **序列号验证**：默认 `A8-0001`，与设备实际序列号不一致时**连接会被拒绝**（提示“序列号验证失败”）；留空或填 `*` 表示跳过验证；
   - 点 **“测试连接（握手 :GVP#）”**：会依次校验 VID/PID、序列号、OnStep 握手，全部通过才显示“连接成功”；
   - 确定保存后，连接望远镜即可。
4. 首次连接时若提示校准，按 N.I.N.A. 向导选择“OnStep”协议风格即可（驱动内部已实现 LX200 兼容命令集）。

---

## 7. 无需 ASCOM 的硬件联调（推荐先做）

先不装 ASCOM，用自带工具验证“电脑 → 芯片 → OnStep”链路：

```bat
OnStepHIDTester list                    :: 列出系统 HID 设备（含 VID/PID/产品名/序列号）
OnStepHIDTester test 1A86 55D4 A8-0001  :: 打开设备，校验序列号并握手（:GVP#/:GVM#）
OnStepHIDTester test 1A86 55D4          :: 只握手，不校验序列号
OnStepHIDTester cmd 1A86 55D4 ":GR#"    :: 发任意命令看应答（如读 RA）
```

> 注意：`list` 会显示**系统全部 HID 设备**（含触摸屏等），未插入目标设备时也能看到其它设备，属正常现象。

---

## 8. 已实现的协议命令（LX200 / OnStep 子集）

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

---

## 9. 故障排查

| 现象 | 原因 / 处理 |
|---|---|
| Chooser 里没有本驱动 | 用管理员跑 `RegisterDriver.bat`；确认 `HKLM\SOFTWARE\WOW6432Node\ASCOM\Telescope Drivers\ASCOM.OnStepHID.Telescope` 存在 |
| 测试连接提示“未找到设备” | VID/PID 不对：用 `OnStepHIDTester list` 查实际值，在 Setup 里改；或设备被其它软件占用 |
| 测试连接提示“序列号验证失败” | 期望序列号与设备实际 S/N 不一致：用 `list` 看实际序列号，在 Setup 里改成一致；或留空跳过验证 |
| 能打开但握手无应答 | 波特率不匹配（OnStep 不是 9600）/ RX-TX 接反 / 未共地 / OnStep 未上电 |
| N.I.N.A. 报“Not Connected” | 先在 Setup 里通过“测试连接”，再用 tester `cmd` 验证 `:GR#` 有返回 |
| 中文乱码 | 无（协议纯 ASCII）；若 OnStep 固件输出过非 ASCII 字符会忽略 |

---

## 10. 工程结构

```
ASTRO8-OnstepX/
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

## 11. 验证记录（本机实测）

- `dotnet build -c Release`：**0 警告 0 错误**；
- `OnStepHIDTester list`：正常枚举系统 HID 设备（含 S/N）；
- 反射验证：`Telescope` 实现 `ITelescopeV3/ITelescopeV4/IDisposable`，`[ComRegisterFunction]`/`[ComUnregisterFunction]` 存在；
- `regasm` 实注册：64/32 位均 "Types registered successfully"，ASCOM Profile 键（32/64 视图）默认值 = **ASTRO8-OnstepX**，InterfaceVersion=3；
- COM 实例化：`Name = ASTRO8-OnstepX`，`Description = ASTRO8-OnstepX`，`Connected = False`；
- `SetupDialog` 实例化冒烟：正常创建，序列号输入框默认 `A8-0001`。

> 未做项：本机未插硬件真机，**序列号校验与真机收发联调需在硬件到手后**用 `OnStepHIDTester test 1A86 55D4 A8-0001` 完成。
