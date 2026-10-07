# Rp2040HID —— RP2040 实现的 HID 转串口桥 / RP2040 HID-to-Serial Bridge

用 **树莓派 Pico（RP2040）** 跑 TinyUSB 固件，模拟原 HID 转串口免驱芯片的全部行为：
USB 侧呈现为**厂商自定义 HID 设备**（32 字节报表、无 Report ID、首字节=有效串口数据长度），
串口侧是 UART 直连 OnStep。**ASTRO8-OnstepX 驱动零改动即可使用**——同一套
VID/PID/序列号/帧格式，插上 Pico 就等同插了一块"可编程的 HID 转串口芯片"。

A TinyUSB firmware for **Raspberry Pi Pico (RP2040)** that emulates the full
behavior of the original driverless HID-to-serial chip: the USB side presents a
**vendor-defined HID device** (32-byte reports, no Report ID, first byte = valid
serial data length), while the serial side is UART straight to OnStep. The
**ASTRO8-OnstepX driver works with it with zero changes** — same VID/PID/serial/
frame format; plugging in the Pico is like plugging in a "programmable
HID-to-serial chip".

> 与配套的 ASCOM 驱动工程 `OnStepHID`（Windows 侧）配合使用；本工程不依赖它，可独立构建烧录。
>
> Works with the companion ASCOM driver project `OnStepHID` (Windows side); this
> project does not depend on it and can be built and flashed independently.

---

## 中文说明

### 1. 与驱动兼容性对照

| 项 | ASTRO8-OnstepX 驱动默认值 | 本固件默认值 | 说明 |
|---|---|---|---|
| VID | `0x1A86` | `0x1A86` | 见 §5 更换建议 |
| PID | `0x55D4` | `0x55D4` | 同上 |
| 序列号 | `A8-0001` | `A8-0001` | 驱动连接时校验 |
| 产品名 | 显示 iProduct | `ASTRO8-OnstepX` | 设备管理器/驱动下拉显示 |
| HID 报表 | 32 字节，无 Report ID | 32 字节，无 Report ID | 帧格式一致 |
| 帧格式 | 首字节=长度(0..31)+数据 | 同左 | 超长回复自动分多帧 |

### 2. 硬件接线

| RP2040 (Pico) | 方向 | 接 OnStep 控制板 |
|---|---|---|
| GP0 (UART0 TX) | Pico→OnStep | 接 OnStep **RX**（主控串口接收） |
| GP1 (UART0 RX) | Pico←OnStep | 接 OnStep **TX**（主控串口发送） |
| GND | - | GND 共地（必须） |
| 3V3 (OUT) | - | 可给 OnStep 逻辑供电（按板子要求，勿超载） |
| GP25（板载 LED） | - | 心跳指示（500ms 翻转） |

- 串口默认 **9600 8N1**（OnStep 默认）；若 OnStep 改过波特率，改 `main.c` 顶部 `UART_BAUD` 重新编译。
- RP2040 是 3.3V TTL，与多数 OnStep 板电平一致；若主控是 5V 需加电平转换。
- **自测技巧**：不接 OnStep 时把 GP0 与 GP1 短接，`OnStepHIDTester test 1A86 55D4 A8-0001` 发 `:GVP#` 会收到自己回显 → 说明 USB+UART 链路都通。

### 3. 构建（已在本机验证通过）

**本机已验证**（2026-10-07）：使用 Arduino rp2040 板卡包 6.0.0 内嵌的
pico-sdk 2.x + ARM GCC 16.1.0 + 预编译 picotool，配合 CMake 3.30.5 + Ninja 1.12.1
构建成功，0 警告 0 错误；产物 `build/Rp2040HID.uf2` 已生成并校验（内含
VID/PID/序列号/产品名字符串）。

**本机重建命令**（PowerShell，一次跑通）：

```powershell
$sdk  = "$env:LOCALAPPDATA\Arduino15\packages\rp2040\hardware\rp2040\6.0.0\pico-sdk"
$gcc  = "$env:LOCALAPPDATA\Arduino15\packages\rp2040\tools\pqt-gcc\5.0.0-9576866\bin"
$cm   = "$env:LOCALAPPDATA\pico-build-tools\cmake-3.30.5-windows-x86_64\bin\cmake.exe"
$nj   = "$env:LOCALAPPDATA\pico-build-tools\ninja\ninja.exe"
$src  = "C:\Users\DA ZHOU\Doubao\chats\2026-10-06\new-chat\Rp2040HID"
$env:PICO_SDK_PATH = $sdk; $env:PATH = "$gcc;$env:PATH"
& $cm -G Ninja -S $src -B "$src\build" -DCMAKE_BUILD_TYPE=Release `
     "-DCMAKE_MAKE_PROGRAM=$nj" `
     "-DCMAKE_TOOLCHAIN_FILE=$sdk/cmake/preload/toolchains/pico_arm_cortex_m0plus_gcc.cmake"
& $cm --build "$src\build"
```

> 说明：工程 CMakeLists 已把 picotool 注入为 Arduino 预编译版（避免 SDK 从
> git 拉取构建），并自定义 POST_BUILD 生成 `.uf2`（Arduino 版 picotool 要求
> `.elf` 扩展名，脚本已自动处理）。换机器用标准环境时，`cmake -B build &&
> cmake --build build` 亦可（标准 SDK 自带 elf2uf2/picotool 流程）。

产物：

```
build/Rp2040HID.uf2    # 烧录用（本机已生成，35 KB）
build/Rp2040HID.elf    # 调试用
build/Rp2040HID.bin    # 裸二进制
build/Rp2040HID.hex    # Intel HEX
```

### 4. 烧录

1. 按住 Pico 板上 **BOOTSEL** 键，插入 USB，松开 → 出现 `RPI-RP2` 磁盘；
2. 把 `Rp2040HID.uf2` 拖进去，自动烧录并重启；
3. 设备管理器应出现 **`ASTRO8-OnstepX`**（HID 设备，无驱动感叹号）；
4. 拔插一次 USB 让主机重新枚举（或直接在驱动 Setup 点"扫描设备"）。

### 5. 更换 VID/PID/序列号/产品名

改 `usb_descriptors.c` 顶部 6 个宏，重新编译烧录：

```c
#define USBD_VID          0x1A86   // 厂商 ID
#define USBD_PID          0x55D4   // 产品 ID
#define USBD_MANUFACTURER "ASTRO8"
#define USBD_PRODUCT      "ASTRO8-OnstepX"
#define USBD_SERIAL       "A8-0001"
```

- 驱动侧**无需改代码**：改完固件后，在 ASTRO8-OnstepX 驱动的 Setup 对话框里
  把 VID/PID/序列号填成一致即可（或序列号留空跳过校验）。
- ⚠️ **关于 VID**：`0x1A86` 是厂商 ID。自用/打样没问题；**产品化发布请改用
  自己申请的 USB-IF VID**（或树莓派基金会 VID `0x2E8A` + 自选 PID），
  避免与芯片厂商产品撞标识。

### 6. 与驱动联调（Windows 侧）

```bat
:: 工程目录：..\OnStepHID\tester\bin\Release\net48\
OnStepHIDTester list                    :: 应看到 1A86:55D4  ASTRO8-OnstepX  [S/N: A8-0001]
OnStepHIDTester test 1A86 55D4 A8-0001  :: 打开设备 + 校验序列号 + 握手 :GVP#
OnStepHIDTester cmd 1A86 55D4 ":GR#"    :: 读 RA 应返回坐标
```

通过后再到 N.I.N.A. / CdC 的 ASCOM Chooser 选 **ASTRO8-OnstepX** → Setup →
测试连接 → 确定，即可正常 GOTO/跟踪/导星。

### 7. 报文协议（与芯片行为一致）

```
Host → 设备 (HID OUT, 32B):  [len][data 0..len-1]   len∈[0,31]，驱动据此发到 UART
设备 → Host (HID IN,  32B):  [len][data 0..len-1]   串口收到的数据按此打包上报
```

- 串口收包：攒满 31 字节立即发一帧；否则等待 2ms 无新字节后把当前积累的字节发出去
  （超长回复会拆成多帧，驱动协议层自动重组）。
- 发送失败（USB 未就绪/挂起）时数据保留在环形缓冲，就绪后自动重发，不丢字节。

### 8. 工程结构

```
Rp2040HID/
├── CMakeLists.txt          # Pico SDK 构建脚本
├── pico_sdk_import.cmake   # SDK 定位脚本（标准版）
├── tusb_config.h           # TinyUSB 配置（只启用 HID）
├── usb_descriptors.c       # 设备/配置/字符串/HID 报表描述符 + VID/PID/SN 配置
├── main.c                  # 主程序：UART 中断 + 环形缓冲 + HID 收发组帧 + LED 心跳
└── README.md
```

### 9. 已知限制与说明

- ✅ 固件已在本机编译验证（2026-10-07），0 警告 0 错误，`Rp2040HID.uf2` 已生成。
  未做真机烧录联调（无 Pico 硬件），烧录后请按 §4 检查设备管理器枚举，
  再按 §6 用 `OnStepHIDTester list` 确认 `1A86:55D4  ASTRO8-OnstepX  [S/N: A8-0001]`。
- 无 debug printf（UART0 全留给 OnStep）；调试观察用板载 LED 心跳即可。
- 环缓冲 512 字节，足以容纳 OnStep 最长的回复（如 `:GVP#`/`GX80#` 均 <100 字节）。

---

## English

### 1. Compatibility with the driver

| Item | ASTRO8-OnstepX driver default | This firmware default | Notes |
|---|---|---|---|
| VID | `0x1A86` | `0x1A86` | see §5 for changing it |
| PID | `0x55D4` | `0x55D4` | same |
| Serial | `A8-0001` | `A8-0001` | verified by the driver on connect |
| Product name | shows iProduct | `ASTRO8-OnstepX` | Device Manager / driver dropdown |
| HID report | 32 bytes, no Report ID | 32 bytes, no Report ID | identical framing |
| Frame format | first byte = length (0..31) + data | same | long replies split into multiple frames |

### 2. Wiring

| RP2040 (Pico) | Direction | Connect to OnStep board |
|---|---|---|
| GP0 (UART0 TX) | Pico→OnStep | OnStep **RX** (main serial receive) |
| GP1 (UART0 RX) | Pico←OnStep | OnStep **TX** (main serial transmit) |
| GND | - | common ground (required) |
| 3V3 (OUT) | - | may power OnStep logic (per board spec, don't overload) |
| GP25 (onboard LED) | - | heartbeat (500 ms toggle) |

- Serial default **9600 8N1** (OnStep default); if OnStep baud was changed, edit
  `UART_BAUD` at the top of `main.c` and rebuild.
- RP2040 is 3.3V TTL, matching most OnStep boards; add a level shifter if the
  MCU is 5V.
- **Self-test trick**: with no OnStep connected, short GP0 to GP1; then
  `OnStepHIDTester test 1A86 55D4 A8-0001` sending `:GVP#` will receive its own
  echo → proves both the USB and UART paths work.

### 3. Build (verified on this machine)

**Verified locally** (2026-10-07): built with pico-sdk 2.x + ARM GCC 16.1.0 +
prebuilt picotool embedded in the Arduino rp2040 board package 6.0.0, with CMake
3.30.5 + Ninja 1.12.1 — 0 warnings 0 errors; `build/Rp2040HID.uf2` generated and
validated (VID/PID/serial/product strings embedded).

**Rebuild command for this machine** (PowerShell, one-shot):

```powershell
$sdk  = "$env:LOCALAPPDATA\Arduino15\packages\rp2040\hardware\rp2040\6.0.0\pico-sdk"
$gcc  = "$env:LOCALAPPDATA\Arduino15\packages\rp2040\tools\pqt-gcc\5.0.0-9576866\bin"
$cm   = "$env:LOCALAPPDATA\pico-build-tools\cmake-3.30.5-windows-x86_64\bin\cmake.exe"
$nj   = "$env:LOCALAPPDATA\pico-build-tools\ninja\ninja.exe"
$src  = "C:\Users\DA ZHOU\Doubao\chats\2026-10-06\new-chat\Rp2040HID"
$env:PICO_SDK_PATH = $sdk; $env:PATH = "$gcc;$env:PATH"
& $cm -G Ninja -S $src -B "$src\build" -DCMAKE_BUILD_TYPE=Release `
     "-DCMAKE_MAKE_PROGRAM=$nj" `
     "-DCMAKE_TOOLCHAIN_FILE=$sdk/cmake/preload/toolchains/pico_arm_cortex_m0plus_gcc.cmake"
& $cm --build "$src\build"
```

> Note: the CMakeLists injects the Arduino prebuilt picotool (avoiding an SDK
> git fetch/build) and adds a custom POST_BUILD that produces `.uf2` (the
> Arduino picotool requires a `.elf` extension; the script handles this). On a
> standard machine, `cmake -B build && cmake --build build` also works (the
> stock SDK ships its own elf2uf2/picotool flow).

Outputs:

```
build/Rp2040HID.uf2    # for flashing (generated here, 35 KB)
build/Rp2040HID.elf    # for debugging
build/Rp2040HID.bin    # raw binary
build/Rp2040HID.hex    # Intel HEX
```

### 4. Flashing

1. Hold the **BOOTSEL** button on the Pico, plug in USB, release → an `RPI-RP2` drive appears;
2. Drag `Rp2040HID.uf2` onto it; it flashes and reboots automatically;
3. Device Manager should show **`ASTRO8-OnstepX`** (HID device, no driver warning);
4. Re-plug USB to let the host re-enumerate (or click "Scan devices" in the driver Setup).

### 5. Changing VID/PID/serial/product name

Edit the 6 macros at the top of `usb_descriptors.c`, rebuild and flash:

```c
#define USBD_VID          0x1A86   // vendor ID
#define USBD_PID          0x55D4   // product ID
#define USBD_MANUFACTURER "ASTRO8"
#define USBD_PRODUCT      "ASTRO8-OnstepX"
#define USBD_SERIAL       "A8-0001"
```

- **No driver code change needed**: after reflashing, enter matching VID/PID/serial
  in the ASTRO8-OnstepX driver Setup dialog (or leave serial blank to skip).
- ⚠️ **About VID**: `0x1A86` is a vendor ID. Fine for personal use/prototyping;
  for **commercial release, apply for your own USB-IF VID** (or use the
  Raspberry Pi Foundation VID `0x2E8A` + your own PID) to avoid collisions.

### 6. Testing with the driver (Windows side)

```bat
:: tool dir: ..\OnStepHID\tester\bin\Release\net48\
OnStepHIDTester list                    :: should show 1A86:55D4  ASTRO8-OnstepX  [S/N: A8-0001]
OnStepHIDTester test 1A86 55D4 A8-0001  :: open device + verify serial + handshake :GVP#
OnStepHIDTester cmd 1A86 55D4 ":GR#"    :: read RA; should return coordinates
```

Then in N.I.N.A. / CdC, pick **ASTRO8-OnstepX** in the ASCOM Chooser → Setup →
Test connection → OK, and GOTO/tracking/guiding work normally.

### 7. Report protocol (identical to the chip)

```
Host → Device (HID OUT, 32B):  [len][data 0..len-1]   len∈[0,31]; driver sends it to UART
Device → Host (HID IN,  32B):  [len][data 0..len-1]   serial data packed into reports
```

- Serial receive: a full 31 bytes is sent immediately; otherwise the accumulated
  bytes go out 2 ms after the last byte (long replies split into frames; the
  driver reassembles them).
- On send failure (USB not ready/suspended), data stays in the ring buffer and
  is retransmitted when ready — no bytes lost.

### 8. Project layout

```
Rp2040HID/
├── CMakeLists.txt          # Pico SDK build script
├── pico_sdk_import.cmake   # SDK locate script (standard)
├── tusb_config.h           # TinyUSB config (HID only)
├── usb_descriptors.c       # device/config/string/HID report descriptors + VID/PID/SN config
├── main.c                  # main: UART IRQ + ring buffer + HID framing + LED heartbeat
└── README.md
```

### 9. Known limitations & notes

- ✅ Firmware build-verified locally (2026-10-07), 0 warnings 0 errors,
  `Rp2040HID.uf2` generated. No real flashing on hardware (no Pico available
  here); after flashing, check Device Manager per §4, then confirm
  `1A86:55D4  ASTRO8-OnstepX  [S/N: A8-0001]` with `OnStepHIDTester list` per §6.
- No debug printf (UART0 is fully reserved for OnStep); use the onboard LED
  heartbeat for observation.
- The 512-byte ring buffer fits OnStep's longest replies (`:GVP#`/`GX80#` are
  both < 100 bytes).
