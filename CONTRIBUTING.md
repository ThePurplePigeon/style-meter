# Contributing

Thanks for helping with Style Meter.

## Branches

- `master` should stay releasable.
- Use short-lived branches for changes:
  - `feat/<short-name>`
  - `fix/<short-name>`
  - `chore/<short-name>`
  - `docs/<short-name>`

## Validation

Run these before opening a pull request:

```powershell
dotnet format .\StyleMeter.sln --verify-no-changes --verbosity minimal
dotnet test .\StyleMeter.sln -v minimal
dotnet build .\StyleMeter.sln -c Release -v minimal
```

## Expectations

- Keep changes focused.
- Match the existing code style.
- Add or update tests when behavior changes.
- Update docs when commands, settings, metadata, or user-visible behavior changes.
- Test in game when changing tracking, hooks, config, or UI behavior.
