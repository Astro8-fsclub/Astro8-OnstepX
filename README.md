# ASTRO8-OnstepX —— OnStep 赤道仪 USB/网络连接方案

一套让 **OnStep / OnStepX 赤道仪**通过 **USB(HID 免驱) 或 WiFi(TCP)** 直连电脑、
被 ASCOM 客户端（N.I.N.A. / Cartes du Ciel 等）识别的完整方案，完全替代传统串口线。

A complete solution that connects an **OnStep / OnStepX equatorial mount** to a PC
via **USB (driverless HID)** or **WiFi (TCP)**, recognized by ASCOM clients
(N.I.N.A. / Cartes du Ciel / etc.), fully replacing the traditional serial cable.

---

## 中文说明

### 三件套

| 目录 | 说明 |
|---|---|
| `OnStepHID/` | ASCOM 驱动（仅 HID 连接）—— 第一代，保留待测试 |
| `OnStepAstro8/` | **ASCOM 驱动（HID + WiFi 双连接）**，产品名 **ASTRO8-OnstepX**；属性页支持经度/纬度/UTC offset/时间日期/RA·Dec 限制/地平·天顶·子午线限制/移动速率/跟踪速率的回读与手动设置；含中英 README |
| `Rp2040HID/` | RP2040 (树莓派 Pico) 固件工程，用自定义 HID 设备替代 HID 转串口芯片；本机构建已验证，`Rp2040HID/Rp2040HID.uf2` 可直接烧录 |

### 快速开始

- 驱动编译注册：见 `OnStepAstro8/README.md`（`dotnet build` + `RegisterDriver.bat`）
- 固件构建烧录：见 `Rp2040HID/README.md`
- 硬件联调（无需 ASCOM）：`OnStepAstro8/tester/bin/Release/net48/OnStepAstro8Tester.exe`（list/test/cmd/wtest/wcmd）

### HID 契约

- VID `0x1A86` / PID `0x55D4` / 序列号 `A8-0001` / 产品名 `ASTRO8-OnstepX`
- 厂商自定义 HID（Usage Page 0xFF00，无 Report ID，IN/OUT 各 32 字节，首字节=有效长度）
- 串口侧 9600 8N1，2ms 空闲组帧

### 许可

个人天文项目，欢迎 fork 与改进。

---

## English

### The three parts

| Directory | Description |
|---|---|
| `OnStepHID/` | ASCOM driver (HID only) — first generation, kept for testing |
| `OnStepAstro8/` | **ASCOM driver (HID + WiFi dual connection)**, product name **ASTRO8-OnstepX**; the Setup page supports read-back and manual setting of longitude / latitude / UTC offset / time & date / RA·Dec limits / horizon·overhead·meridian limits / move rate / tracking rate; includes Chinese & English README |
| `Rp2040HID/` | RP2040 (Raspberry Pi Pico) firmware project that replaces the USB-serial bridge chip with a custom HID device; build-verified locally, `Rp2040HID/Rp2040HID.uf2` is ready to flash |

### Quick start

- Build & register the driver: see `OnStepAstro8/README.md` (`dotnet build` + `RegisterDriver.bat`)
- Build & flash the firmware: see `Rp2040HID/README.md`
- Hardware testing without ASCOM: `OnStepAstro8/tester/bin/Release/net48/OnStepAstro8Tester.exe` (list / test / cmd / wtest / wcmd)

### HID contract

- VID `0x1A86` / PID `0x55D4` / serial `A8-0001` / product name `ASTRO8-OnstepX`
- Vendor-defined HID (Usage Page 0xFF00, no Report ID, 32-byte IN/OUT reports, first byte = valid length)
- Serial side: 9600 8N1, 2 ms idle frame grouping

### License

A personal astronomy project — fork and improve freely.
