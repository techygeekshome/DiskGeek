<div align="center">

<img src="https://raw.githubusercontent.com/techygeekshome/DiskGeek/main/icons/diskgeek.png" alt="DiskGeek logo" width="96" height="96">

# DiskGeek

**A free, self-contained disk space analyser for Windows — find what's eating your storage, and clean it up.**

[![Version](https://img.shields.io/github/v/release/techygeekshome/DiskGeek?label=version&color=4c9bff)](https://github.com/techygeekshome/DiskGeek/releases)
[![Platform](https://img.shields.io/badge/platform-Windows-0078d4)](#-download--run)
[![License](https://img.shields.io/badge/license-proprietary%20freeware-b7791f)](LICENSE)
[![Made by TechyGeeksHome](https://img.shields.io/badge/made%20by-TechyGeeksHome-b191f2)](https://techygeekshome.info)
[![Support on Ko-fi](https://img.shields.io/badge/support-Ko--fi-ff5e5b)](https://ko-fi.com/techygeekshome)

[Download](#-download--run) · [Features](#-what-it-does) · [Screenshots](#-screenshots) · [Build from source](#-build-from-source) · [License](#-license)

</div>

---

DiskGeek scans your drives and shows exactly what's eating your space, with a list view and a treemap view of disk usage. It finds both exact duplicates and near-duplicate files using perceptual hashing — including similar-image detection — and lets you search full-text across scan results. Batch rename and snapshot comparison (see what changed between scans over time) round it out, plus scheduled scans for ongoing monitoring.

No installer bloat, no bundled offers, no telemetry. 100% free, no Pro tier, no upsells.

## 📸 Screenshots

<p float="left">
  <img src="https://raw.githubusercontent.com/techygeekshome/DiskGeek/main/screenshots/screenshot-list-view.png" width="49%" />
  <img src="https://raw.githubusercontent.com/techygeekshome/DiskGeek/main/screenshots/screenshot-treemap-view.png" width="49%" />
</p>
<p float="left">
  <img src="https://raw.githubusercontent.com/techygeekshome/DiskGeek/main/screenshots/screenshot-duplicates.png" width="49%" />
  <img src="https://raw.githubusercontent.com/techygeekshome/DiskGeek/main/screenshots/screenshot-similar-images.png" width="49%" />
</p>
<p float="left">
  <img src="https://raw.githubusercontent.com/techygeekshome/DiskGeek/main/screenshots/screenshot-search.png" width="49%" />
  <img src="https://raw.githubusercontent.com/techygeekshome/DiskGeek/main/screenshots/screenshot-batch-rename.png" width="49%" />
</p>
<p float="left">
  <img src="https://raw.githubusercontent.com/techygeekshome/DiskGeek/main/screenshots/screenshot-snapshots.png" width="49%" />
  <img src="https://raw.githubusercontent.com/techygeekshome/DiskGeek/main/screenshots/screenshot-scanning-banner.png" width="49%" />
</p>

## ⬇️ Download & run

| What it is | Get it |
| --- | --- |
| Windows installer *(self-contained, .NET 8 / Avalonia UI)* | [**Download DiskGeek**](https://techygeekshome.info/diskgeek/) — free |

Also available on [MajorGeeks](https://www.majorgeeks.com/files/details/diskgeek.html) and [Product Hunt](https://www.producthunt.com/products/diskgeek).

## ✨ What it does

- 📊 **List view and treemap view** of disk usage, so you can see what's actually taking up space.
- 🔁 **Exact and near-duplicate detection** — perceptual hashing catches near-identical files, not just byte-for-byte matches.
- 🖼️ **Similar-image finding**, built on the same perceptual-hashing engine.
- 🔎 **Full-text search** across scan results.
- ✏️ **Batch rename** for cleaning up messy file sets in bulk.
- 📸 **Snapshot comparison** — see what changed between scans over time.
- ⏰ **Scheduled scans** for ongoing monitoring.
- 🔒 **Private** — no telemetry, no bundled offers, no installer bloat.

### On the one network call

DiskGeek makes exactly one network request, and only when you click **Check for Updates…**.
It fetches a small XML file listing the latest published version, and sends nothing with the
request beyond what any HTTP client sends — no identifier, no scan data, no usage figures.
If a newer version exists you get a banner with a link; DiskGeek never downloads or installs
anything itself. **Nothing is requested at startup**, so opening the app makes no network
call at all.

## 🔧 Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet build DiskGeek.sln -c Release
```

To produce a self-contained win-x64 publish folder:

```powershell
dotnet publish src/DiskGeek.App/DiskGeek.App.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64
```

### Project layout

| Path | What's there |
| --- | --- |
| `src/DiskGeek.Core` | Scanning, duplicate detection, snapshots, search, batch rename, and export logic (no UI dependencies) |
| `src/DiskGeek.App` | The Avalonia desktop UI |
| `installer/DiskGeekSetup.iss` | Inno Setup script used to build the Windows installer |
| `icons/` | App icon assets |

## 🐛 Support & contributing

Found a bug or have a request? [Open an issue](https://github.com/techygeekshome/DiskGeek/issues) or [get in touch](https://techygeekshome.info/contact/).

## 📄 License

DiskGeek is free to download and use. This is proprietary freeware, not open source — see [LICENSE](LICENSE) for the full terms.

© 2026 TechyGeeksHome | Andrew Armstrong.

---

<div align="center">

Made with ❤️ by [**TechyGeeksHome**](https://techygeekshome.info)

[Website](https://techygeekshome.info) · [YouTube](https://www.youtube.com/channel/UCtEuFj1SMLiuRoucD1hv8dA) · [X](https://x.com/TechyGeeks1) · [Facebook](https://www.facebook.com/techygeeks.home) · [Instagram](https://www.instagram.com/andrewarmstrongtgh/)

</div>
