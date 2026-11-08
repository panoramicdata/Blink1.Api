# Testing

## Unit tests (`Blink1.Api.Test`)

Need no device. The client talks to an internal `IBlink1HidBackend`/`IBlink1Transport` pair; the tests substitute
`FakeBackend`/`FakeTransport`, which record every feature report sent and answer reads from a queue, so every command is
checked byte for byte. CI runs these, and they hold the library at 100% line and branch coverage.

```powershell
dotnet test --project Blink1.Api.Test/Blink1.Api.Test.csproj
```

`InventoryTests` keeps `docs/endpoints/commands.md` in step with the client: each row's methods must exist and be marked
with that command, and each named test must exist.

The HidSharp adapter (`Transport/HidSharpBackend.cs`) is excluded from coverage: it holds no logic of its own and only a
real device can exercise it, which the integration tests do.

## Live tests (`Blink1.Api.IntegrationTest`)

Need a blink(1) attached by USB. They open the first one found, or the one named by the `Blink1:SerialNumber` setting:

```powershell
dotnet user-secrets set Blink1:SerialNumber <serial> --project Blink1.Api.IntegrationTest
```

With no blink(1) attached they fail with a message saying so; nothing is skipped. CI does not run them.

```powershell
dotnet test --project Blink1.Api.IntegrationTest/Blink1.Api.IntegrationTest.csproj
```

They change the LEDs, play patterns and arm server tickle, then put things back: the fixture reads each LED's colour and
every pattern line first and restores them at the end. One test saves the (unchanged) pattern to flash, after which the
device answers slowly for about ten seconds. They never send the bootloader commands (`G`, `L`), startup parameters
(`B`) or EEPROM writes (`E`); on a device that lacks a command, they check it is refused before anything is sent.

Verified so far on a blink(1) mk2, firmware 204. mk1, mk3 and mk4 commands are unit-verified only.

## Linux

Grant access to the device with a udev rule, then replug it:

```text
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="27b8", ATTRS{idProduct}=="01ed", TAG+="uaccess"
```
