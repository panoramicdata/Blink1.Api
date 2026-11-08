# Implementing

The contract for changes to Blink1.Api.

## Layout

- `Blink1Client.cs`: opening, enumeration, the exchange pipeline (read-only guard, generation guard, retries, the
  per-client lock) and argument encoding.
- `Blink1Client.Colors.cs`, `Blink1Client.Patterns.cs`, `Blink1Client.Device.cs`: one public method per command use.
- `Protocol/Blink1Command.cs`: every firmware command, its report, whether it changes the device, and the generations
  that accept it.
- `Transport/`: the HID boundary. `HidSharpBackend` is the only code that touches HidSharp.

## Adding or changing a command

1. Add or change its `Blink1Command`, with the generations from ThingM's docs or, better, a device.
2. Add the client method: async, `CancellationToken` last, no optional parameters (use overloads), marked
   `[Blink1Command('x')]` for every command it sends.
3. Add a row (or the method) to `docs/endpoints/commands.md`, naming the unit test.
4. Unit-test the exact bytes sent and the parsing of the answer, then keep coverage at 100% line and branch.
5. Add a live test if the attached device supports the command and it is reversible.

## Device behaviour the code relies on

These were measured on a blink(1) mk2 running firmware 204; each has a live test.

- `n` with an LED number sets the wrong LED and starts playback, so single-LED "set now" is sent as `c` with no fade.
  That lands on the device's next update, a few milliseconds later; `n` for every LED is readable at once.
- `p` takes an inclusive loop end and reads back end + 1; `D` takes an exclusive end and reads back as sent.
- `W` always fails its USB transfer, and the device then answers slowly (100 to 500 ms) for about ten seconds, with
  occasional failed transfers. Every command is idempotent, so failed transfers are retried (`MaxRetries`); `W` itself
  is never repeated.
- The serial number of a mk2 can be `20002`, which ThingM's serial-range rule reads as mk1, so the generation comes
  from the product string first.

## Code quality

Zero warnings (`TreatWarningsAsErrors`), XML docs on every public member, tabs, file-scoped namespaces. Run
`dotnet format whitespace` before building.
