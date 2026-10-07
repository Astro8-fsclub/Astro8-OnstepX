/**
 * USB 描述符：设备 / 配置 / 字符串 / HID 报表。
 * 兼容对象：ASTRO8-OnstepX ASCOM 驱动（HidSharp 按 VID/PID 打开，
 * 读写 32 字节、无 Report ID 的 HID 报表，首字节 = 有效串口数据长度）。
 */

#include "tusb.h"
#include <string.h>

/* ---------------- 可配置项（默认与驱动完全一致，插上即用） ----------------
 * 注意：0x1A86 是厂商 ID，自用/打样没问题；
 * 产品化发布建议改用自己申请的 USB-IF VID（或树莓派基金会 VID 0x2E8A），
 * 然后在驱动 Setup 对话框里同步 VID/PID/序列号即可，无需改驱动代码。
 */
#define USBD_VID          0x1A86
#define USBD_PID          0x55D4
#define USBD_BCD_DEVICE   0x0100
#define USBD_MANUFACTURER "ASTRO8"
#define USBD_PRODUCT      "ASTRO8-OnstepX"
#define USBD_SERIAL       "A8-0001"

/* ---------------- HID 报表描述符 ----------------
 * 厂商自定义 HID（Usage Page 0xFF00），无 Report ID；
 * Output 32 字节（Host→Device），Input 32 字节（Device→Host）；
 * 两方向首字节均为有效串口数据长度（0..31）。 */
uint8_t const desc_hid_report[] = {
    0x06, 0x00, 0xFF,       /* Usage Page (Vendor Defined 0xFF00) */
    0x09, 0x01,             /* Usage (Vendor Usage 0x01) */
    0xA1, 0x01,             /* Collection (Application) */
    /* Output report: 32 bytes */
    0x09, 0x02,             /*   Usage (Vendor Usage 0x02) */
    0x15, 0x00,             /*   Logical Minimum (0) */
    0x26, 0xFF, 0x00,       /*   Logical Maximum (255) */
    0x75, 0x08,             /*   Report Size (8) */
    0x95, 0x20,             /*   Report Count (32) */
    0x91, 0x02,             /*   Output (Data, Var, Abs) */
    /* Input report: 32 bytes */
    0x09, 0x03,             /*   Usage (Vendor Usage 0x03) */
    0x15, 0x00,             /*   Logical Minimum (0) */
    0x26, 0xFF, 0x00,       /*   Logical Maximum (255) */
    0x75, 0x08,             /*   Report Size (8) */
    0x95, 0x20,             /*   Report Count (32) */
    0x81, 0x02,             /*   Input (Data, Var, Abs) */
    0xC0                    /* End Collection */
};

/* ---------------- 设备描述符 ---------------- */
tusb_desc_device_t const desc_device = {
    .bLength            = sizeof(tusb_desc_device_t),
    .bDescriptorType    = TUSB_DESC_DEVICE,
    .bcdUSB             = 0x0200,
    .bDeviceClass       = 0x00,
    .bDeviceSubClass    = 0x00,
    .bDeviceProtocol    = 0x00,
    .bMaxPacketSize0    = CFG_TUD_ENDPOINT0_SIZE,
    .idVendor           = USBD_VID,
    .idProduct          = USBD_PID,
    .bcdDevice          = USBD_BCD_DEVICE,
    .iManufacturer      = 0x01,
    .iProduct           = 0x02,
    .iSerialNumber      = 0x03,
    .bNumConfigurations = 0x01,
};

/* ---------------- 配置描述符（手写字节，避免 TinyUSB 宏版本差异） ----------------
 * 结构：配置(9) + 接口(9) + HID(9) + EP IN(7) + EP OUT(7) = 41 字节 */
#define CONFIG_TOTAL_LEN     41
#define HID_REPORT_DESC_LEN  ((uint8_t)sizeof(desc_hid_report))  /* 34 */

uint8_t const desc_configuration[] = {
    /* 配置描述符 */
    0x09, 0x02,
    (uint8_t)(CONFIG_TOTAL_LEN & 0xFF), (uint8_t)((CONFIG_TOTAL_LEN >> 8) & 0xFF),
    0x01,               /* bNumInterfaces */
    0x01,               /* bConfigurationValue */
    0x00,               /* iConfiguration */
    0x80,               /* bmAttributes: 总线供电 */
    0x32,               /* bMaxPower: 100 mA */
    /* 接口描述符 (HID) */
    0x09, 0x04,
    0x00,               /* bInterfaceNumber */
    0x00,               /* bAlternateSetting */
    0x02,               /* bNumEndpoints */
    0x03,               /* bInterfaceClass: HID */
    0x00,               /* bInterfaceSubClass: 无 */
    0x00,               /* bInterfaceProtocol: 无 */
    0x00,               /* iInterface */
    /* HID 描述符 */
    0x09, 0x21,
    0x11, 0x01,         /* bcdHID 1.11 */
    0x00,               /* bCountryCode */
    0x01,               /* bNumDescriptors */
    0x22,               /* bDescriptorType: Report */
    (uint8_t)(HID_REPORT_DESC_LEN & 0xFF), (uint8_t)((HID_REPORT_DESC_LEN >> 8) & 0xFF),
    /* 端点描述符 IN (0x81) */
    0x07, 0x05, 0x81, 0x03, 0x40, 0x00, 0x0A,
    /* 端点描述符 OUT (0x01) */
    0x07, 0x05, 0x01, 0x03, 0x40, 0x00, 0x0A,
};

/* ---------------- 字符串描述符 ---------------- */
static uint16_t _desc_str[33];

char const* _string_desc_arr[] = {
    (char const[]){0x09, 0x04},  /* 0: 语言 ID 0x0409 (English US) */
    USBD_MANUFACTURER,           /* 1 */
    USBD_PRODUCT,                /* 2 */
    USBD_SERIAL,                 /* 3 */
};

uint16_t const* tud_descriptor_string_cb(uint8_t index, uint16_t langid) {
    (void)langid;
    uint8_t chr_count;

    if (index == 0) {
        memcpy(&_desc_str[1], _string_desc_arr[0], 2);
        chr_count = 1;
    } else {
        if (index >= sizeof(_string_desc_arr) / sizeof(_string_desc_arr[0])) return NULL;
        const char* str = _string_desc_arr[index];
        chr_count = (uint8_t)strlen(str);
        if (chr_count > 32) chr_count = 32;
        for (uint8_t i = 0; i < chr_count; i++) {
            _desc_str[1 + i] = (uint16_t)str[i];
        }
    }

    _desc_str[0] = (uint16_t)((TUSB_DESC_STRING << 8) | (2 * chr_count + 2));
    return _desc_str;
}

/* ---------------- 描述符回调 ---------------- */
uint8_t const* tud_descriptor_device_cb(void) {
    return (uint8_t const*)&desc_device;
}

uint8_t const* tud_descriptor_configuration_cb(uint8_t index) {
    (void)index;
    return desc_configuration;
}

uint8_t const* tud_hid_descriptor_report_cb(uint8_t itf) {
    (void)itf;
    return desc_hid_report;
}
