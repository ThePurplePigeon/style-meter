# Style Meter

Style Meter is a Dalamud API 15 plugin that shows a small HUD for your PvE GCD streak.

It reads action effects and recast state instead of keypresses. Button spam, invalid presses, and oGCD weaving do not increase the GCD combo or rank.

## Ranks

| Rank | Combo Count |
| ---- | ----------- |
| D    | 1           |
| C    | 8           |
| B    | 16          |
| A    | 25          |
| S    | 50          |
| SS   | 100         |
| SSS  | 152         |

## Commands

- `/stylemeter` toggles the overlay.
- `/stylemeter config` opens plugin settings.
- `/stylemeter debug` toggles detailed debug logging.
- `/stylemeter diag` writes current tracking state to the Dalamud log.

## Building

Prerequisites:

- XIVLauncher, Final Fantasy XIV, and Dalamud installed and launched at least once.
- Dalamud dev files available at the default XIVLauncher path, or through `DALAMUD_HOME`.
- .NET 10 SDK.

Build:

```powershell
dotnet build .\StyleMeter.sln
```

Run tests:

```powershell
dotnet test .\StyleMeter.sln
```

The debug plugin DLL is written to:

```text
.\StyleMeter\bin\x64\Debug\StyleMeter.dll
```

## Loading as a Dev Plugin

1. Launch the game with Dalamud enabled.
2. Open `/xlsettings`.
3. Go to `Experimental`.
4. Add the full path to `StyleMeter.dll` under Dev Plugin Locations.
5. Open `/xlplugins`.
6. Enable `Style Meter` from `Dev Tools > Installed Dev Plugins`.

## Contributing

Use `CONTRIBUTING.md` for branch, validation, and review expectations. CI runs format, build, and test on pushes and pull requests.

Style Meter is local and display-only. It does not execute actions, automate gameplay, read party or alliance combat output, upload telemetry, call external services, or save combat history.

## References

- [Dalamud API 15](https://dalamud.dev/versions/v15/)
- [Dalamud plugin metadata](https://dalamud.dev/plugin-development/plugin-metadata/)
- [Dalamud technical considerations](https://dalamud.dev/plugin-development/technical-considerations/)
- [Dalamud plugin restrictions](https://dalamud.dev/plugin-publishing/restrictions/)
- [Dalamud submission process](https://dalamud.dev/plugin-publishing/submission/)
- [FFXIVClientStructs ActionManager](https://ffxiv.wildwolf.dev/api/FFXIVClientStructs.FFXIV.Client.Game.ActionManager.html)
