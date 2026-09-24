# Minimal current-cursor clicker

This is a small Windows Forms example of the repository's foreground click path. It reads the current system cursor position, sends a left-button down event through `SendInput`, holds it for 25 ms, and sends a left-button up event. It repeats on a background thread without activating or selecting a target window.

The main application already offers more controls. This example is intended as a compact, independently buildable version for comparing input behavior. It does not bypass Roblox input restrictions, and behavior in a particular experience must be tested locally.

## Build

Requires the .NET 8 SDK on Windows:

```powershell
dotnet build examples/RobloxCursorClicker/RobloxCursorClicker.csproj -c Release
```

Run the built `RobloxCursorClicker.exe` with the .NET 8 Desktop Runtime installed. Keep its `.dll`, `.deps.json`, and `.runtimeconfig.json` files beside it.

## Use

1. Set the click interval in seconds.
2. Focus the target application and place the cursor on the desired point.
3. Press **F6** to start, **F7** to stop, or **F8** to toggle.

The clicks go to whatever application is under the cursor. Leave the cursor over the intended target while the loop runs.
