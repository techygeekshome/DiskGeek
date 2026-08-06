# DiskGeek

A free, self-contained disk space analyser for Windows, built with Avalonia UI on .NET 8.

- List view and treemap view of disk usage
- Exact and perceptual-hash (near-duplicate) file detection
- Similar-image finding
- Full-text search across scan results
- Batch rename
- Snapshot comparison, to see what changed over time
- Scheduled scans

No installer bloat, no bundled offers, no telemetry. 100% free, no Pro tier.

Homepage: https://techygeekshome.info/diskgeek/
Download: https://techygeekshome.info/downloads/diskgeek/DiskGeekSetup.exe

## Project layout

- `src/DiskGeek.Core` — scanning, duplicate detection, snapshots, search, batch rename, and export logic (no UI dependencies)
- `src/DiskGeek.App` — the Avalonia desktop UI
- `installer/DiskGeekSetup.iss` — Inno Setup script used to build the Windows installer
- `icons/` — app icon assets

## Building

Requires the .NET 8 SDK.

```powershell
dotnet build DiskGeek.sln -c Release
```

To produce a self-contained win-x64 publish folder:

```powershell
dotnet publish src/DiskGeek.App/DiskGeek.App.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64
```

## License

Copyright (c) TechyGeeksHome. All rights reserved. This repository is private and not currently licensed for external use or redistribution.
