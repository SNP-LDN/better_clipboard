# Localization smoke test

From the repository root, run:

```powershell
dotnet run --project tests/LocalizationSmoke/LocalizationSmoke.csproj -- .tools/localization-smoke
```

This Windows/WPF integration test creates isolated settings and clipboard data in the specified scratch folder. It verifies live language switching, persistence, reopening, unchanged pending settings and clipboard text, translated metadata, time groups, dialogs, and resetting defaults. It renders English and Chinese pages to PNGs in the scratch folder without opening windows or starting clipboard monitoring.
