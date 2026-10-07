# OnStepAstro8 —— ASTRO8-OnstepX 望远镜 ASCOM 驱动（HID / WiFi 双连接）

在 `OnStepHID`（仅 HID）基础上扩展的新版驱动：**保留原工程 OnStepHID 待测试**，
本工程独立注册（ProgId 不同，可与旧版并存）。除原有 HID 连接外，新增
**WiFi（TCP）连接**（默认 `192.168.0.1:9998`，参考原 ASCOM 串口驱动样式），
并在驱动属性页（Setup）提供完整的手动设置项：**经度、纬度、UTC offset、
时间、日期、赤经/赤纬限制、地平/天顶/子午线限制、当前 slew 移动速度、跟踪速度**，
全部字段**默认回读硬件（OnStep）当前设置，允许手动修改**，连接时自动下发。

## 1. 与原版 OnStepHID 的区别

| 项 | OnStepHID（旧，保留测试） | OnStepAstro8（新） |
|---|---|---|
| ProgId | `ASCOM.OnStepHID.Telescope` | `ASCOM.OnStepAstro8.Telescope` |
| 连接方式 | 仅 HID | **HID + WiFi(TCP)** |
| 属性页 | 仅 VID/PID/序列号 | VID/PID/序列号 + 地址/端口 + **站点/时间/限制/速度 全字段** |
| 回读硬件设置 | 无 | **"回读硬件设置"按钮**（一次性连接回读全部可读项） |
| GOTO 限制 | 无 | 驱动侧 RA/Dec 软限制（超限拒绝 GOTO） |
| 注册 | 已注册 | 已注册（两者可共存，Chooser 中显示带 " (HID/WiFi)" 后缀） |

## 2. 连接方式

### HID（USB 转串口免驱芯片 / Rp2040HID 固件）

- VID `0x1A86`、PID `0x55D4`、序列号 `A8-0001`（与 Rp2040HID 固件一致）；
- Setup 中 VID/PID/序列号**精确匹配**才出现在下拉列表，否则提示"未找到 ASTRO8-OnstepX"并刷新。

### WiFi（TCP，默认 192.168.0.1:9998）

- OnStep 的 WiFi 模块（板载 ESP32/ESP8266 或串口转 WiFi）把 TCP 端口数据原样转发到望远镜串口；
- 地址 + 端口可改（参考 ASCOM 串口驱动样式）；
- 连接失败（5 秒超时）会给出地址/局域网/端口检查提示。

## 3. 属性页字段（SetupDialog）

打开方式：ASCOM Chooser → 选 **ASTRO8-OnstepX (HID/WiFi)** → **Setup**。

### 站点与时间（默认回读硬件，可手动修改）

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

### 限制

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

### 速度

| 字段 | 回读 | 设置 | 说明 |
|---|---|---|---|
| 当前 GOTO 速度 | `:GX97#` (deg/s) | - | 只读参考 |
| 移动速率档 | -（无查询命令） | `:RG#`(1x) `:RC#`(8x) `:RM#`(20x) `:RF#`(48x) `:RS#`(半速) `:R0#~:R9#` | 影响后续 :Me#/:Mw#/:Mn#/:Ms# 连续移动 |
| 跟踪速率 | `:GU#` 状态解析 | `:TQ#`(恒星) `:TL#`(月球) `:TS#`(太阳) `:TK#`(King) | 连接时自动下发 |

## 4. 构建与注册

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

## 5. 联调（tester，无需 ASCOM）

```bat
:: 工程目录：tester\bin\Release\net48\
OnStepAstro8Tester list                          :: 列出 HID 设备
OnStepAstro8Tester test 1A86 55D4 A8-0001       :: HID 打开 + 握手 :GVP#
OnStepAstro8Tester cmd  1A86 55D4 :GX97#        :: HID 任意命令（当前 GOTO 速度）
OnStepAstro8Tester wtest 192.168.0.1 9998       :: WiFi 连接 + 握手
OnStepAstro8Tester wcmd  192.168.0.1 9998 :GVP# :: WiFi 任意命令
```

## 6. 工程结构

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

## 7. 说明与限制

- 本工程未做真机联调（无 OnStep WiFi 与硬件现场）；HID 链路与 Rp2040HID 固件
  的兼容性已由原 OnStepHID 验证，WiFi 链路请用 `wtest` 先确认模块是原始 TCP 转发
  （部分模块默认 HTTP，需要切换模式或改端口）。
- 驱动侧 RA/Dec 软限制默认 0-24h / -90..+90°（不拦截）；在属性页改窄后，
  GOTO（SlewToCoordinates/SlewToTarget）超出范围会拒绝并给出提示。
- OnStep 的 Axis1(RA)/Axis2(Dec) 轴限位仅在固件编译时配置，运行时可读不可写；
  因此"赤经限制"用可写的东/西子午线限制表达，"赤纬限制"为驱动侧软限制 + Axis2 限位参考。
