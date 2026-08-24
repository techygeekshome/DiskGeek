# Chocolatey package — diskgeek

Source for the [community.chocolatey.org](https://community.chocolatey.org/packages/diskgeek)
package. The `packageSourceUrl` in the nuspec points at this repository, so this is where the
package source belongs. Before this folder existed the nuspec lived only on a local machine and
`packageSourceUrl` pointed at the product page, which is not a package source.

## What it does

The package does **not** embed the application. `chocolateyinstall.ps1` downloads
`DiskGeekSetup.exe` from the GitHub release for the matching tag, verifies it against a SHA-256
checksum, and runs it silently. `chocolateyuninstall.ps1` finds Inno Setup's own uninstaller
through the uninstall registry key rather than guessing a path.

Because the installer is downloaded rather than embedded, this package must **not** contain a
`tools\VERIFICATION.txt`. That file is only for packages that ship a binary inside the nupkg.

## Why this folder exists

The published 1.0.0 package carries a Chocolatey validation note: *"The licenseUrl should be
added if there is one."* It is set here, pointing at the repository's own LICENSE file, so the
next version bump clears the guideline.

## Building and pushing a new version

```powershell
# from this folder
choco pack
choco push diskgeek.<version>.nupkg --source https://push.chocolatey.org/
```

`choco push` needs the API key from your community.chocolatey.org account
(`choco apikey --key <key> --source https://push.chocolatey.org/`, once per machine).

## Checklist for a new release

1. Cut the GitHub release and note the `DiskGeekSetup.exe` asset URL.
2. `Get-FileHash DiskGeekSetup.exe -Algorithm SHA256` and put the hash and the new URL in
   `tools/chocolateyinstall.ps1`.
3. Bump `<version>` and `<releaseNotes>` in the nuspec.
4. `choco pack`, then install locally from the nupkg and check the uninstall path works too.
5. `choco push`.

The 1.0.0 values currently in place:

| | |
|---|---|
| Asset | `DiskGeekSetup.exe`, 33,581,203 bytes |
| SHA-256 | `1f29df37544b9f5ea829ec87ee4fdc77b2209d3b82cf8e1a986021fc735f70b4` |
| Installer | Inno Setup 6.5 — silent args `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-` |
