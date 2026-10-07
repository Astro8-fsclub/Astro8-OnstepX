# Astro8-OnstepHIDascom

**ASTRO8-OnstepX ASCOM Telescope Driver (HID / Serial / WiFi)**

基于官方 OnStep ASCOM 驱动的完整逻辑（不改其背后实现），仅**新增 HID 连接方式**，用于通过 RP2040 HID 固件（Rp2040HID，VID `0x1A86` / PID `0x55D4` / SN `A8-0001`）替代 USB 串口连接 OnStep/OnStepX。

Based on the official OnStep ASCOM driver's complete logic (its internals are left untouched), this project **only adds an HID transport** so an RP2040 HID firmware (Rp2040HID, VID `0x1A86` / PID `0x55D4` / SN `A8-0001`) can replace the USB serial connection to OnStep/OnStepX.

---

## 特性 / Features

- **HID 连接 / HID transport**：枚举并连接 HID 设备，可手动指定 VID / PID / 序列号（SN 留空则跳过校验）；支持多个同 VID/PID 设备按 SN 精确匹配。
- **官方原版逻辑 / Original logic preserved**：Telescope 协议、命令集、站点/时间/限制/光学/反向间隙/最大速率等属性页与官方驱动一致（GPLv3，作者 Howard Dutton）。
- **串口 / WiFi 保留**：官方驱动原有的 Serial (COM) 与 TCP/IP 连接方式原样保留。
- **官方属性页 / Official Setup dialog**：端口选择下拉新增 `HID Device` 项，其余界面与官方 OnStep Setup 完全一致。

## HID 报告协议 / HID Report Protocol

与 `Rp2040HID` 固件一致（厂商自定义 HID，无 Report ID，IN/OUT 均 32 字节）：

- OUT 报表（Host→Device）：`byte[0] = payload length (0..31)`，`byte[1..] = serial data`
- IN 报表（Device→Host）：同上格式
- 串口侧：UART 9600 8N1（OnStep 默认），由固件完成 HID↔UART 桥接

Compatible with the `Rp2040HID` firmware (vendor-defined HID, no Report ID, 32-byte IN/OUT reports):
- OUT report: `byte[0] = payload length (0..31)`, `byte[1..] = serial data`
- IN report: same layout
- Serial side: UART 9600 8N1 (OnStep default), bridged by the firmware.

## 工程结构 / Layout

```
Astro8-OnstepHIDascom/
├── ASCOM.OnStep/                  # 驱动源码（官方逻辑 + HID 分支）
│   ├── Telescope.cs               # 官方望远镜驱动（未改连接逻辑）
│   ├── SetupDialogForm.cs         # 官方属性页 + HID 连接 UI
│   ├── SharedResources.cs         # 官方通讯层 + HID 分支入口
│   ├── HidTransport.cs            # 新增：HID 设备读写（P/Invoke）
│   └── ...                        # 官方其余文件（原样）
├── ASCOM.OnStep.Telescope.csproj  # 单工程构建（net48 / AnyCPU）
├── LICENSE                        # GPLv3（官方驱动沿用）
└── README.md
```

## 构建 / Build

需要 .NET SDK 8.0+（含 .NET Framework 4.8 引用包，NuGet 自动还原）：

```
dotnet build ASCOM.OnStep.Telescope.csproj -c Release
```

输出：`bin/Release/net48/Astro8-OnstepHIDascom.Telescope.dll`

## 注册 / Register (COM)

以管理员身份运行（x64 与 x86 各一次）：

```
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe Astro8-OnstepHIDascom.Telescope.dll /codebase
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe Astro8-OnstepHIDascom.Telescope.dll /codebase
```

注册后 ASCOM Chooser 中将出现 **ASTRO8-OnstepHIDascom (HID/WiFi)**（ProgId：`ASCOM.Astro8OnstepHIDascom.Telescope`）。

## 使用 / Usage

1. 将 `Rp2040HID.uf2` 刷入 RP2040（Pico），USB 连接电脑。
2. 在 N.I.N.A. / 任意 ASCOM 客户端中选择望远镜 **ASTRO8-OnstepHIDascom (HID/WiFi)**。
3. 属性页中连接方式选 **HID Device**，确认 VID `1A86`、PID `55D4`、SN `A8-0001`（可改），点击测试/连接。
4. 也可选 **IP Address** 走 WiFi TCP（默认 `192.168.0.1:9999`）或选择物理 **COM 口**。

## 协议 / License

GPLv3。官方 OnStep ASCOM 驱动版权归 Howard Dutton / The ASCOM Initiative 所有；本工程为 GPLv3 派生（新增 HID 传输层）。详见 `LICENSE`。

## 兼容性 / Compatibility

- OnStep / OnStepX 固件 3.16 及以上（握手要求与官方一致）
- Windows x64 / x86，ASCOM Platform 6.x/7.x
- Rp2040HID 固件：`https://github.com/Astro8-fsclub/Astro8-OnstepX`（子项目 `Rp2040HID`）
