# OnStepAstro8 — ASTRO8-OnstepX Telescope ASCOM Driver (HID / WiFi dual connection)

An enhanced driver built on `OnStepHID` (HID only): **the original OnStepHID project
is kept untouched for testing**. This project registers independently (different
ProgId, so both can coexist). In addition to the original HID connection, a
**WiFi (TCP) connection** is added (default `192.168.0.1:9998`, styled after the
original ASCOM serial driver). The driver Setup dialog (properties page) provides
full manual settings: **longitude, latitude, UTC offset, time, date, RA/Dec
limits, horizon/overhead/meridian limits, current slew speed, tracking rate** —
all fields **default to reading back the hardware (OnStep) current settings and
can be edited manually**; they are applied automatically on connect.

## 1. Differences from the original OnStepHID

| Item | OnStepHID (old, kept for testing) | OnStepAstro8 (new) |
|---|---|---|
| ProgId | `ASCOM.OnStepHID.Telescope` | `ASCOM.OnStepAstro8.Telescope` |
| Connection | HID only | **HID + WiFi(TCP)** |
| Setup page | VID/PID/serial only | VID/PID/serial + host/port + **site/time/limits/speed fields** |
| Hardware read-back | none | **"Read hardware settings" button** (one-shot connect, reads all readable items) |
| GOTO limits | none | Driver-side RA/Dec soft limits (GOTO rejected outside range) |
| Registration | registered | registered (coexists with the old one; Chooser shows " (HID/WiFi)" suffix) |

## 2. Connection methods

### HID (driverless USB-serial chip / Rp2040HID firmware)

- VID `0x1A86`, PID `0x55D4`, serial `A8-0001` (identical to the Rp2040HID firmware);
- In Setup, only devices matching VID/PID/serial **exactly** appear in the
  dropdown; otherwise "ASTRO8-OnstepX not found" is shown and you are asked to refresh.

### WiFi (TCP, default 192.168.0.1:9998)

- The OnStep WiFi module (onboard ESP32/ESP8266 or a serial-to-WiFi bridge)
  transparently forwards TCP-port data to the mount serial port;
- Host and port are editable (styled after the ASCOM serial driver);
- A failed connect (5 s timeout) gives a message about address/LAN/port checks.

## 3. Setup page fields (SetupDialog)

Open: ASCOM Chooser → pick **ASTRO8-OnstepX (HID/WiFi)** → **Setup**.

### Site & time (defaults to hardware read-back, editable)

| Field | Read command | Write command | Notes |
|---|---|---|---|
| Longitude (°) | `:Gg#` | `:Sg(s)DDD*MM#` | negative for west |
| Latitude (°) | `:Gt#` | `:StsDD*MM#` | negative for south |
| UTC offset | `:GG#` | `:SGsHH:MM#` | added to local time to get UT1; use whole/half/three-quarter hours |
| Local time | `:GL#` (24 h) | `:SLHH:MM:SS#` | shown as UT1 + offset after read-back |
| Local date | `:GC#` | `:SCMM/DD/YY#` | same |

- **"Read hardware settings"** reads back in one go: longitude, latitude, UTC
  offset, time, date, horizon/overhead/east/west meridian limits, Axis2(Dec)
  limits, current GOTO speed, tracking rate.
- After editing time/date manually, clicking **OK** writes them to the hardware
  immediately; the remaining fields are applied automatically when the driver connects.

### Limits

| Group | Field | Default | Notes |
|---|---|---|---|
| Driver GOTO soft limits | RA min/max (h) | 0 / 24 | GOTO outside range is rejected (SlewTo throws) |
| Driver GOTO soft limits | Dec min/max (°) | Axis2 read-back / +90 | same |
| Hardware (writable) | Horizon (°) | read-back | `:ShsDD#` |
| Hardware (writable) | Overhead (°) | read-back | `:SoDD#` |
| Hardware (writable) | East meridian (min) | read-back | `:SXE9,n#`; on a GEM this is the RA-direction limit |
| Hardware (writable) | West meridian (min) | read-back | `:SXEA,n#` |

- Hardware limit read commands: `:Gh#` `:Go#` `:GXE9#` `:GXEA#`;
- Axis1(RA) axis limits (`:GXEe#` min° / `:GXEB#` max h) are shown as reference
  only after read-back (OnStep does not support runtime writes to axis limits).

### Speed

| Field | Read-back | Set | Notes |
|---|---|---|---|
| Current GOTO speed | `:GX97#` (deg/s) | - | read-only reference |
| Move rate preset | - (no query command) | `:RG#`(1x) `:RC#`(8x) `:RM#`(20x) `:RF#`(48x) `:RS#`(half) `:R0#~:R9#` | affects subsequent :Me#/:Mw#/:Mn#/:Ms# continuous moves |
| Tracking rate | `:GU#` status parse | `:TQ#`(sidereal) `:TL#`(lunar) `:TS#`(solar) `:TK#`(King) | applied on connect |

## 4. Build & register

```bat
:: 1) Build (Release, net48)
dotnet build OnStepAstro8.sln -c Release

:: 2) Register (run RegisterDriver.bat as Administrator, or manually)
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" /codebase "src\bin\Release\net48\OnStepAstro8.dll"
"%WINDIR%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe"     /codebase "src\bin\Release\net48\OnStepAstro8.dll"
```

- Output: `src\bin\Release\net48\OnStepAstro8.dll`
- Registry: `HKLM\SOFTWARE\ASCOM\Telescope Drivers\ASCOM.OnStepAstro8.Telescope` (plus the 32-bit view)
- Uninstall: RegAsm /unregister (both)
- Note: the RA0000 warning is expected (HidSharp is not strong-named, same as the old version)

## 5. Hardware testing (tester, no ASCOM needed)

```bat
:: tool dir: tester\bin\Release\net48\
OnStepAstro8Tester list                          :: list HID devices
OnStepAstro8Tester test 1A86 55D4 A8-0001       :: HID open + handshake :GVP#
OnStepAstro8Tester cmd  1A86 55D4 :GX97#        :: HID arbitrary command (current GOTO speed)
OnStepAstro8Tester wtest 192.168.0.1 9998       :: WiFi connect + handshake
OnStepAstro8Tester wcmd  192.168.0.1 9998 :GVP# :: WiFi arbitrary command
```

## 6. Project layout

```
OnStepAstro8/
├── OnStepAstro8.sln
├── RegisterDriver.bat
├── src/
│   ├── OnStepAstro8.csproj
│   ├── ITransport.cs        # transport abstraction (Write/ReadUntilHash/Flush)
│   ├── HidTransport.cs      # HID transport (32-byte report, first byte = length; serial check)
│   ├── WifiTransport.cs     # TCP transport (default 192.168.0.1:9998)
│   ├── OnStepProtocol.cs    # OnStep/LX200 command codec (incl. UTC offset/limits/rates)
│   ├── SetupDialog.cs       # properties page (connection + site/time/limits/speed + read-back)
│   ├── Telescope.cs         # ITelescopeV3/V4 implementation (dual connection + GOTO soft limits)
│   ├── DriverSettings.cs    # Profile storage + Chooser registration
│   └── TrackingRates.cs
└── tester/
    └── Program.cs           # list/test/cmd/wtest/wcmd
```

## 7. Notes & limitations

- This project has not been tested against real hardware (no OnStep WiFi / bench
  available here). The HID path is verified for compatibility with the Rp2040HID
  firmware by the original OnStepHID driver; for the WiFi path, first confirm
  with `wtest` that the module is raw TCP forwarding (some modules default to
  HTTP and need a mode switch or a different port).
- The driver-side RA/Dec soft limits default to 0-24 h / -90..+90° (no blocking);
  once narrowed in the properties page, GOTO (SlewToCoordinates/SlewToTarget)
  outside the range is rejected with a clear message.
- OnStep Axis1(RA)/Axis2(Dec) axis limits are compile-time configuration and are
  readable but not writable at runtime. Therefore "RA limit" is expressed by the
  writable east/west meridian limits, and "Dec limit" is the driver-side soft
  limit plus the Axis2 limit reference.
