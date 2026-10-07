# ASTRO8-OnstepX — USB / Network connectivity for OnStep equatorial mounts

A complete solution that connects an **OnStep / OnStepX equatorial mount** to a PC
via **USB (driverless HID)** or **WiFi (TCP)**, recognized by ASCOM clients
(N.I.N.A. / Cartes du Ciel / etc.), fully replacing the traditional serial cable.

## The three parts

| Directory | Description |
|---|---|
| `OnStepHID/` | ASCOM driver (HID only) — first generation, kept for testing |
| `OnStepAstro8/` | **ASCOM driver (HID + WiFi dual connection)**, product name **ASTRO8-OnstepX**; the Setup page supports read-back and manual setting of longitude / latitude / UTC offset / time & date / RA·Dec limits / horizon·overhead·meridian limits / move rate / tracking rate; includes Chinese & English README |
| `Rp2040HID/` | RP2040 (Raspberry Pi Pico) firmware project that replaces the USB-serial bridge chip with a custom HID device; build-verified locally, `Rp2040HID/Rp2040HID.uf2` is ready to flash |

## Quick start

- Build & register the driver: see `OnStepAstro8/README.md` (`dotnet build` + `RegisterDriver.bat`)
- Build & flash the firmware: see `Rp2040HID/README.md`
- Hardware testing without ASCOM: `OnStepAstro8/tester/bin/Release/net48/OnStepAstro8Tester.exe` (list / test / cmd / wtest / wcmd)

## HID contract

- VID `0x1A86` / PID `0x55D4` / serial `A8-0001` / product name `ASTRO8-OnstepX`
- Vendor-defined HID (Usage Page 0xFF00, no Report ID, 32-byte IN/OUT reports, first byte = valid length)
- Serial side: 9600 8N1, 2 ms idle frame grouping

## License

A personal astronomy project — fork and improve freely.
