/**
 * Rp2040HID —— RP2040 实现的 HID 转串口桥，等效替代 HID 转串口免驱芯片。
 *
 * 与 ASTRO8-OnstepX ASCOM 驱动完全兼容：
 *   - USB 侧：厂商自定义 HID，无 Report ID，Input/Output 均 32 字节报表；
 *   - 帧格式：首字节 = 本次有效串口数据长度（0..31），其后为数据；
 *   - 串口侧：UART0，默认 9600 8N1（OnStep 默认），可在下方修改。
 *
 * 数据流：
 *   Host(驱动) --HID OUT 32B--> RP2040 --UART TX--> OnStep
 *   OnStep    --UART RX--> RP2040 --HID IN 32B--> Host(驱动)
 *
 * UART RX 组包策略：攒满 31 字节立即发一帧；否则等 2ms 空闲判定后
 * 发送当前字节数（与 CH9326 的分帧行为一致，超长回复会自动分多帧）。
 */

#include <stdint.h>
#include <string.h>
#include "pico/stdlib.h"
#include "hardware/uart.h"
#include "hardware/irq.h"
#include "hardware/gpio.h"
#include "tusb.h"

/* ---------------- 可配置项 ---------------- */
#define UART_ID       uart0
#define UART_TX_PIN   0
#define UART_RX_PIN   1
#define UART_BAUD     9600

#define REPORT_LEN    32        /* HID 报表长度（固定 32，与驱动一致） */
#define MAX_PAYLOAD   (REPORT_LEN - 1)  /* 单报表最多携带的串口字节数 */

#define UART_IDLE_US  2000      /* UART 收包空闲判定：2ms 无新字节即发包 */
#define RING_SIZE     512       /* UART RX 环形缓冲 */

#define LED_PIN       PICO_DEFAULT_LED_PIN  /* 板载 LED（RP2040 = 25） */

/* UART0 对应的中断号；若改用 uart1，请同步改为 UART1_IRQ */
#define UART_IRQ      UART0_IRQ

/* ---------------- UART RX 环形缓冲（ISR 写，主循环读） ---------------- */
static uint8_t _ring[RING_SIZE];
static volatile uint16_t _head = 0, _tail = 0, _count = 0;
static volatile uint32_t _last_rx_us = 0;

static inline bool ring_push(uint8_t c) {
    if (_count >= RING_SIZE) return false;
    _ring[_head] = c;
    _head = (uint16_t)((_head + 1) % RING_SIZE);
    _count++;
    return true;
}

static inline uint16_t ring_avail(void) {
    return _count;
}

static void ring_peek(uint8_t* out, uint16_t n) {
    for (uint16_t i = 0; i < n; i++) {
        out[i] = _ring[(_tail + i) % RING_SIZE];
    }
}

static void ring_consume(uint16_t n) {
    _tail = (uint16_t)((_tail + n) % RING_SIZE);
    _count -= n;
}

/* ---------------- UART RX 中断 ---------------- */
static void on_uart_rx(void) {
    while (uart_is_readable(UART_ID)) {
        uint8_t c = uart_getc(UART_ID);
        ring_push(c);
        _last_rx_us = (uint32_t)time_us_32();
    }
}

/* ---------------- HID 处理 ---------------- */
static uint8_t _last_in[REPORT_LEN];  /* 最近一帧 IN 报表（供 GET_REPORT） */

/* 解析 HID OUT 报表并写入 UART：首字节 = 有效长度 */
static void handle_output(const uint8_t* buf, uint16_t len) {
    if (len < 1) return;
    uint16_t n = buf[0];
    if (n > MAX_PAYLOAD) n = MAX_PAYLOAD;
    if (n > len - 1) n = (uint16_t)(len - 1);
    if (n > 0) {
        uart_write_blocking(UART_ID, buf + 1, n);
    }
}

/* HID OUT 端点数据（Host→Device 的主路径） */
void tud_hid_report_cb(uint8_t itf, uint8_t report_id, hid_report_type_t report_type,
                       uint8_t const* buffer, uint16_t bufsize) {
    (void)itf; (void)report_id; (void)report_type;
    handle_output(buffer, bufsize);
}

/* SET_REPORT 控制传输（少数主机走此路径，处理同 OUT 端点） */
void tud_hid_set_report_cb(uint8_t itf, uint8_t report_id, hid_report_type_t report_type,
                           uint8_t const* buffer, uint16_t bufsize) {
    (void)itf; (void)report_id;
    if (report_type == HID_REPORT_TYPE_OUTPUT) {
        handle_output(buffer, bufsize);
    }
}

/* GET_REPORT：返回最近一帧 IN 报表（可选支持，避免主机空读） */
uint16_t tud_hid_get_report_cb(uint8_t itf, uint8_t report_id, hid_report_type_t report_type,
                               uint8_t* buffer, uint16_t reqlen) {
    (void)itf; (void)report_id;
    if (report_type == HID_REPORT_TYPE_INPUT && reqlen >= REPORT_LEN) {
        memcpy(buffer, _last_in, REPORT_LEN);
        return REPORT_LEN;
    }
    return 0;
}

/* ---------------- 主循环 ---------------- */
int main(void) {
    /* UART 初始化（9600 8N1，OnStep 默认） */
    uart_init(UART_ID, UART_BAUD);
    gpio_set_function(UART_TX_PIN, GPIO_FUNC_UART);
    gpio_set_function(UART_RX_PIN, GPIO_FUNC_UART);
    uart_set_fifo_enabled(UART_ID, true);

    /* UART RX 中断 → 环形缓冲 */
    irq_set_exclusive_handler(UART_IRQ, on_uart_rx);
    irq_set_enabled(UART_IRQ, true);
    uart_set_irq_enables(UART_ID, true, false);

    /* 板载 LED（心跳指示） */
    gpio_init(LED_PIN);
    gpio_set_dir(LED_PIN, GPIO_OUT);

    /* TinyUSB 初始化 */
    tusb_init();

    uint32_t led_tick = 0;
    bool led_on = false;

    for (;;) {
        tud_task();  /* 必须高频调用，处理 USB 事件 */

        /* UART RX → HID IN：攒满 31 字节立即发，否则等 2ms 空闲 */
        if (tud_hid_ready()) {
            uint16_t avail = ring_avail();
            if (avail > 0) {
                uint32_t now = (uint32_t)time_us_32();
                bool full = avail >= MAX_PAYLOAD;
                bool idle = (now - _last_rx_us) >= UART_IDLE_US;
                if (full || idle) {
                    uint16_t n = avail > MAX_PAYLOAD ? MAX_PAYLOAD : avail;
                    uint8_t buf[REPORT_LEN];
                    buf[0] = (uint8_t)n;
                    ring_peek(buf + 1, n);
                    if (tud_hid_report(0, buf, REPORT_LEN)) {
                        ring_consume(n);
                        memcpy(_last_in, buf, REPORT_LEN);
                    }
                    /* 发送失败（未 ready / 挂起）时数据保留，下轮重试 */
                }
            }
        }

        /* LED 心跳：500ms 翻转 */
        if ((uint32_t)time_us_32() - led_tick >= 500000u) {
            led_tick = (uint32_t)time_us_32();
            led_on = !led_on;
            gpio_put(LED_PIN, led_on);
        }
    }
}
