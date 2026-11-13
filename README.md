# Blink1.Api

[![NuGet](https://img.shields.io/nuget/v/Blink1.Api.svg)](https://www.nuget.org/packages/Blink1.Api)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 11.0](https://img.shields.io/badge/.NET-11.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![CI](https://github.com/panoramicdata/Blink1.Api/actions/workflows/ci.yml/badge.svg)](https://github.com/panoramicdata/Blink1.Api/actions/workflows/ci.yml)
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/9bcdb0171da54fe8b11857b9cfb8f404)](https://app.codacy.com/gh/panoramicdata/Blink1.Api/dashboard)
[![Codacy Coverage](https://app.codacy.com/project/badge/Coverage/9bcdb0171da54fe8b11857b9cfb8f404)](https://app.codacy.com/gh/panoramicdata/Blink1.Api/dashboard)

A typed, modern .NET client for the [ThingM blink(1)](https://blink1.thingm.com/) USB RGB LED: find the devices
attached, and send every firmware command (colours and fades, single LEDs, stored patterns and playback, server tickle,
notes, startup parameters and more) on Windows, Linux and macOS.

Supports .NET 11.0.

Blink1.Api is an independent open-source project by Panoramic Data Limited. It is not made, endorsed or supported by
ThingM. blink(1) is a trademark of ThingM Corporation.

## Installation

```shell
dotnet add package Blink1.Api
```

Blink1.Api targets .NET 11.0 and uses [HidSharp](https://www.nuget.org/packages/HidSharp) for USB HID access. On Linux,
give your user access to the device with a udev rule, then replug it:

```text
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="27b8", ATTRS{idProduct}=="01ed", TAG+="uaccess"
```

## Quick start

```csharp
using Blink1.Api;

using var blink1 = Blink1Client.Open(new Blink1ClientOptions());

await blink1.FadeToColorAsync(Blink1Colors.Green, TimeSpan.FromMilliseconds(500), CancellationToken.None);
```

## Finding devices

```csharp
foreach (var device in Blink1Client.GetDevices())
{
	Console.WriteLine($"{device.SerialNumber} {device.ProductName} ({device.Generation})");
}

using var second = Blink1Client.Open(new Blink1ClientOptions { SerialNumber = "20002" });
```

With no serial number or device path in the options, the first device (by serial number) is opened. `Blink1Exception`
is thrown when none matches.

## Colours

```csharp
var ct = CancellationToken.None;

await blink1.SetColorAsync(Blink1Color.Parse("#FF8000"), ct);                              // every LED, at once
await blink1.FadeToColorAsync(Blink1Colors.Blue, TimeSpan.FromSeconds(2), Blink1Led.Second, ct);
await blink1.SetColorAsync(Blink1Colors.Red, Blink1Led.First, ct);                          // mk2 and later
var top = await blink1.ReadColorAsync(Blink1Led.First, ct);                                 // mk2 and later
await blink1.TurnOffAsync(ct);
```

Colours are gamma-corrected as ThingM's own tools do, so they look the same. Set `ApplyGammaCorrection = false` to send
raw values.

## Patterns and playback

The device stores a pattern of colour lines (12 on mk1, 32 on later generations) and can play it by itself.

```csharp
await blink1.WritePatternLineAsync(0, new Blink1PatternLine(Blink1Colors.Red, TimeSpan.FromMilliseconds(300), Blink1Led.All), ct);
await blink1.WritePatternLineAsync(1, new Blink1PatternLine(Blink1Colors.Black, TimeSpan.FromMilliseconds(300), Blink1Led.All), ct);
await blink1.PlayLoopAsync(0, 1, 5, ct);      // lines 0 to 1 inclusive, five times
var state = await blink1.ReadPlayStateAsync(ct);
await blink1.StopAsync(ct);
await blink1.SavePatternsAsync(ct);           // keep the pattern over power-down
```

After `SavePatternsAsync` the device answers slowly for about ten seconds while it writes flash; failed USB transfers in
that time are retried automatically (`MaxRetries`, default 3).

## Server tickle

A watchdog: unless your program calls again within the timeout, the device starts playing its pattern by itself, so a
crashed or hung host is visible.

```csharp
await blink1.EnableServerTickleAsync(TimeSpan.FromSeconds(30), ct);   // call again before 30 s pass
await blink1.DisableServerTickleAsync(ct);
```

## Device information and mk3 features

```csharp
var version = await blink1.GetFirmwareVersionAsync(ct);   // for example 204
```

mk3 and later also have notes (`WriteNoteAsync`, `ReadNoteAsync`), startup parameters (`SetStartupParametersAsync`,
`GetStartupParametersAsync`), the chip ID (`GetChipIdAsync`) and bootloader commands (`EnterBootloaderAsync`, and the
irreversible `LockBootloaderAsync`). mk1 has EEPROM access (`ReadEepromAsync`, `WriteEepromAsync`).

A command the device's generation lacks throws `NotSupportedException` before anything is sent. When the generation
cannot be determined, every command is sent.

## Read-only mode

```csharp
using var observer = Blink1Client.Open(new Blink1ClientOptions { IsReadOnly = true });
```

Every command that changes the device then throws `InvalidOperationException` before it is sent; reads still work.

## Errors

- `Blink1Exception`: no matching device, a USB transfer that still failed after retries, or an unexpected answer.
- `NotSupportedException`: the device's generation lacks the command.
- `InvalidOperationException`: a write on a read-only client.
- `ArgumentOutOfRangeException`: a pattern position past the device's pattern, or a time outside 0 to 655.35 s.

`Blink1Client` is safe to share between threads; commands to one device are sent one at a time.

## Coverage

All 21 firmware commands are implemented; [docs/endpoints/commands.md](docs/endpoints/commands.md) lists each with its
client methods and tests. They are verified live on a blink(1) mk2 (firmware 204); mk1, mk3 and mk4 commands are
verified by unit tests against the documented protocol. See [docs/TESTING.md](docs/TESTING.md).

## Quality

Unit tests cover 100% of lines and branches, and an inventory test keeps the command list and the client in step. The
build treats warnings as errors.

## Licence

MIT. See [LICENSE](LICENSE).
