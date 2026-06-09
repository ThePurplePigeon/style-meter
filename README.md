# Style Meter

Style Meter is a Dalamud API 15 plugin that shows a small HUD for your PvE GCD streak.

It reads action effects and recast state instead of keypresses. Button spam, invalid presses, and oGCD weaving do not increase the GCD combo or rank.

## Scope

- Tracks the local player only.
- Tracks PvE GCD actions, including replacement, generated, casted, and dance actions.
- Tracks oGCD abilities as a separate count. They do not advance rank or extend the combo.
- Ignores items, PvP actions, invalid actions, and non-player actions.
- Shows combo count, chain count, best combo, rank, and remaining time.
- Does not automate actions, parse damage, choose actions, or use network calls.

## How It Works

1. Hooks `ActionEffectHandler.Receive` to observe action effects.
2. Calls the original game function first.
3. Filters events to local-player PvE actions.
4. Classifies GCD actions using cooldown group `58` and action-category fallback.
5. Reads the active action or shared-GCD recast, with adjusted/Lumina fallbacks.
6. Holds the timer while a tracked hardcast is in progress.
7. Continues the combo if the next GCD lands before `lastGcdTime + recast + grace`.
8. Ends and briefly fades the combo once the timer expires.

The code is split into small layers: `Interop/` observes game state, `Actions/` resolves actions, `Tracking/` owns combo state, and `Windows/` draws the UI.

## Ranks

| Rank | Combo Count | Base-GCD Landmark |
| ---- | ----------- | ----------------- |
| D    | 1           | Below `20s` |
| C    | 8           | `20s` |
| B    | 16          | `40s` |
| A    | 25          | About `1m` |
| S    | 50          | About `2m` |
| SS   | 100         | About `4m` |
| SSS  | 152         | `6m20s` burst/pot window |

## Commands

- `/stylemeter` toggles the overlay.
- `/stylemeter config` opens plugin settings.
- `/stylemeter debug` toggles detailed debug logging.
- `/stylemeter diag` writes current tracking state to the Dalamud log.

## Settings

| Setting | Default | Notes |
| ------- | ------- | ----- |
| Show overlay | On | Controls whether the meter is drawn. |
| Lock overlay position | Off | Prevents accidental dragging. |
| Overlay scale | `1.40` | Clamped from `0.75` to `2.50`. |
| Grace threshold | `0.50s` | Extra time after adjusted recast before the combo ends. |
| Debug logging | Off | Emits observed GCD details to the Dalamud log. |

## Behavior

Style Meter is local and display-only. It does not execute actions, automate gameplay, read party or alliance combat output, upload telemetry, call external services, or save combat history.

## Building

Prerequisites:

- XIVLauncher, Final Fantasy XIV, and Dalamud installed and launched at least once.
- Dalamud dev files available at the default XIVLauncher path, or through `DALAMUD_HOME`.
- .NET 10 SDK.

Build:

```powershell
dotnet build .\StyleMeter.sln -v minimal
```

Run tests:

```powershell
dotnet test .\StyleMeter.sln -v minimal
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

Debug builds generate the dev manifest before compiling the DLL, which reduces automatic-reload races while Dalamud is watching the output folder. If a dev reload still fails with an error about `StyleMeter.json` being used by another process, wait for the build to finish and reload the plugin manually; disabling automatic reloading for the dev plugin avoids that race during rapid iteration.

## In-Game Verification

Recommended manual checks:

- Use a target dummy and confirm consecutive GCDs increment the combo.
- Weave oGCDs and confirm they update the CHAIN/weave display without incrementing the GCD combo or rank.
- Break a combo during combat and confirm BEST keeps the highest GCD combo reached.
- Leave combat and confirm BEST resets to zero/idle.
- Spam a GCD button and confirm duplicate cooldown events do not inflate the combo.
- Run `/stylemeter diag` and confirm the dev log reports player state and hook diagnostics.
- Wait past adjusted recast plus grace and confirm the combo ends.
- Change jobs, zones, log out, die, or enter PvP and confirm state clears safely.
- Reload/unload the plugin and confirm there are no lingering callbacks or crashes.

## Publishing

Before submitting to the official Dalamud plugin repository, review the current Dalamud submission process, plugin restrictions, and metadata guidance.

## Contributing

Use `CONTRIBUTING.md` for branch, validation, and review expectations. CI runs format, build, and test on pushes and pull requests.

## Project Layout

```text
StyleMeter/
  Plugin.cs                 Dalamud plugin entrypoint and command wiring.
  Configuration.cs          Persisted plugin settings.
  Actions/                  Action lookup, classification, and recast fallback.
  Interop/                  Dalamud hook, framework tick, player state, and cast-state adapters.
  Tracking/                 Combo state, snapshots, and diagnostics.
  Windows/                  ImGui overlay and configuration windows.

StyleMeter.Tests/
  *Tests.cs                 Unit tests for combo behavior, action resolution, recast fallback, UI math, and crash prevention.
```

## References

- [Dalamud API 15](https://dalamud.dev/versions/v15/)
- [Dalamud plugin metadata](https://dalamud.dev/plugin-development/plugin-metadata/)
- [Dalamud technical considerations](https://dalamud.dev/plugin-development/technical-considerations/)
- [Dalamud plugin restrictions](https://dalamud.dev/plugin-publishing/restrictions/)
- [Dalamud submission process](https://dalamud.dev/plugin-publishing/submission/)
- [FFXIVClientStructs ActionManager](https://ffxiv.wildwolf.dev/api/FFXIVClientStructs.FFXIV.Client.Game.ActionManager.html)
