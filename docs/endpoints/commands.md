# blink(1) firmware commands

Sources: ThingM's [blink(1) HID commands](https://github.com/todbot/blink1/blob/main/docs/blink1-hid-commands.md) and the
reference implementation, [blink1-lib.c](https://github.com/todbot/blink1-tool/blob/main/blink1-lib.c), which the HID
commands page names as the preferred implementation and which fills the page's gaps (response layouts, the version
format, report 2 sizes).

Every command is a USB HID feature report. Report 1 is 8 bytes (`{1, code, arg0 .. arg5}`); report 2 (mk3 and later) is
60 bytes. One row per command code. `Client method` is `Blink1Client.Method`; `Test` is `TestClass.TestMethod` in
Blink1.Api.Test. Device enumeration (`Blink1Client.GetDevices`) is a USB query, not a firmware command, so it has no row.

| Code | Report | Generations | Command | Client method | Test |
|---|---|---|---|---|---|
| `c` | 1 | all | Fade to RGB colour | `Blink1Client.FadeToColorAsync`, `Blink1Client.SetColorAsync` | `ColorCommandTests.FadeToColorAsync_SendsCommandC`, `ColorCommandTests.SetColorAsync_SendsCommandN` |
| `n` | 1 | all | Set RGB colour now | `Blink1Client.SetColorAsync`, `Blink1Client.TurnOffAsync` | `ColorCommandTests.SetColorAsync_ForEveryLed_UsesCommandN`, `ColorCommandTests.TurnOffAsync_TurnsEveryLedOffAtOnce` |
| `r` | 1 | mk2+ | Read current RGB colour | `Blink1Client.ReadColorAsync` | `ColorCommandTests.ReadColorAsync_ReturnsTheColour` |
| `D` | 1 | all | Server tickle | `Blink1Client.EnableServerTickleAsync`, `Blink1Client.DisableServerTickleAsync` | `PatternCommandTests.EnableServerTickleAsync_SendsCommandD`, `PatternCommandTests.DisableServerTickleAsync_SendsCommandD` |
| `p` | 1 | mk2+ | Play loop | `Blink1Client.PlayAsync`, `Blink1Client.PlayLoopAsync`, `Blink1Client.StopAsync` | `PatternCommandTests.PlayAsync_PlaysToTheEndForEver`, `PatternCommandTests.PlayLoopAsync_SendsCommandP`, `PatternCommandTests.StopAsync_SendsCommandPWithPlayOff` |
| `S` | 1 | mk2+ | Read play state | `Blink1Client.ReadPlayStateAsync` | `PatternCommandTests.ReadPlayStateAsync_ReturnsTheState` |
| `P` | 1 | all | Set colour pattern line | `Blink1Client.WritePatternLineAsync` | `PatternCommandTests.WritePatternLineAsync_SetsTheLedThenWritesTheLine` |
| `W` | 1 | mk2+ | Save colour patterns | `Blink1Client.SavePatternsAsync` | `PatternCommandTests.SavePatternsAsync_SendsCommandWWithItsKey` |
| `R` | 1 | all | Read colour pattern line | `Blink1Client.ReadPatternLineAsync` | `PatternCommandTests.ReadPatternLineAsync_ReturnsTheLine` |
| `l` | 1 | mk2+ | Set LED for pattern writes | `Blink1Client.SetPatternLedAsync`, `Blink1Client.WritePatternLineAsync` | `PatternCommandTests.SetPatternLedAsync_SendsCommandL` |
| `e` | 1 | mk1 | Read EEPROM location | `Blink1Client.ReadEepromAsync` | `DeviceCommandTests.ReadEepromAsync_ReturnsTheByte` |
| `E` | 1 | mk1 | Write EEPROM location | `Blink1Client.WriteEepromAsync` | `DeviceCommandTests.WriteEepromAsync_SendsCommandE` |
| `v` | 1 | all | Get firmware version | `Blink1Client.GetFirmwareVersionAsync` | `DeviceCommandTests.GetFirmwareVersionAsync_ReturnsTheVersionNumber` |
| `!` | 1 | all | Test command | `Blink1Client.RunTestCommandAsync` | `DeviceCommandTests.RunTestCommandAsync_ReturnsTheRawAnswer` |
| `F` | 2 | mk3+ | Write note | `Blink1Client.WriteNoteAsync` | `DeviceCommandTests.WriteNoteAsync_SendsCommandFOnReportTwo` |
| `f` | 2 | mk3+ | Read note | `Blink1Client.ReadNoteAsync` | `DeviceCommandTests.ReadNoteAsync_ReturnsTheNote` |
| `G` | 2 | mk3+ | Go to bootloader | `Blink1Client.EnterBootloaderAsync` | `DeviceCommandTests.EnterBootloaderAsync_ReportsWhetherTheDeviceAgreed` |
| `L` | 2 | mk3+ | Lock bootloader (irreversible) | `Blink1Client.LockBootloaderAsync` | `DeviceCommandTests.LockBootloaderAsync_ReportsWhetherTheDeviceLocked` |
| `B` | 1 | mk3+ | Set startup parameters | `Blink1Client.SetStartupParametersAsync` | `DeviceCommandTests.SetStartupParametersAsync_SendsCommandB` |
| `b` | 1 | mk3+ | Get startup parameters | `Blink1Client.GetStartupParametersAsync` | `DeviceCommandTests.GetStartupParametersAsync_ReturnsTheParameters` |
| `U` | 2 | mk3+ | Get chip unique ID | `Blink1Client.GetChipIdAsync` | `DeviceCommandTests.GetChipIdAsync_ReturnsTheIdBlock` |
