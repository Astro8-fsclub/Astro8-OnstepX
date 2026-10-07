#ifndef _TUSB_CONFIG_H_
#define _TUSB_CONFIG_H_

/* Rp2040HID 的 TinyUSB 配置：只启用 HID 设备类 */
/* 注：Pico SDK 的 tinyusb 集成会在命令行预定义 CFG_TUSB_MCU/OS 等，
 * 这里用 #ifndef 保护避免重复定义告警 */

#ifndef CFG_TUSB_MCU
#define CFG_TUSB_MCU OPT_MCU_RP2040
#endif

#ifndef CFG_TUSB_OS
#define CFG_TUSB_OS OPT_OS_NONE
#endif

#ifndef CFG_TUSB_MEM_SECTION
#define CFG_TUSB_MEM_SECTION
#endif

#ifndef CFG_TUSB_MEM_ALIGN
#define CFG_TUSB_MEM_ALIGN __attribute__((aligned(4)))
#endif

#ifndef CFG_TUSB_DEBUG
#define CFG_TUSB_DEBUG 0
#endif

/* RP2040 单 USB 口，全速设备 */
#define CFG_TUSB_RHPORT0_MODE (OPT_MODE_DEVICE | OPT_MODE_FULL_SPEED)

#define CFG_TUD_ENDPOINT0_SIZE 64

/* 类驱动开关 */
#define CFG_TUD_CDC 0
#define CFG_TUD_MSC 0
#define CFG_TUD_HID 1
#define CFG_TUD_MIDI 0
#define CFG_TUD_VENDOR 0
#define CFG_TUD_DFU 0
#define CFG_TUD_DFU_RUNTIME 0
#define CFG_TUD_BTH 0

/* HID：端点缓冲 64 字节（报表为 32 字节，留足余量） */
#define CFG_TUD_HID_EP_BUFSIZE 64

#endif
