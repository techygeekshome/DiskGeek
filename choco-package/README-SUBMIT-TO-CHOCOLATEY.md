# Submitting DiskGeek to Chocolatey — what's left for you

I've built the complete Chocolatey package for DiskGeek. Everything is done except the
one step that needs your own account credentials, which I don't handle for you
(Chocolatey has no web upload form any more — I checked, and confirmed on their own
`/packages/upload` page: "Directly uploading nupkg files through the Chocolatey
Community Repository Website is no longer supported. All packages should be uploaded
using the choco push command.").

## What's in this folder

- `diskgeek.nuspec` — the package manifest (name, version, description, links, tags)
- `tools\chocolateyInstall.ps1` — downloads the real DiskGeek installer from
  `https://techygeekshome.info/downloads/diskgeek/DiskGeekSetup.exe` (the same live
  download link already on your site) and runs it silently, with a SHA256 checksum
  check baked in so Chocolatey verifies the file wasn't tampered with in transit.
- `tools\chocolateyUninstall.ps1` — finds DiskGeek's own Windows uninstaller (Inno
  Setup registers one automatically) and runs it silently.
- `tools\VERIFICATION.txt` — required by Chocolatey's moderators; explains how anyone
  can independently confirm the checksum matches your real installer.

The package points at your existing live download URL rather than embedding the 32MB
installer inside the package itself — that's the standard, preferred pattern.

**One thing to flag before you submit:** DiskGeek's installer isn't signed with a paid
code-signing certificate yet. I've disclosed that plainly in the nuspec description and
in VERIFICATION.txt, since Chocolatey moderators run every package through automated
malware/AV scanning and an unsigned exe is more likely to need manual review or get
flagged. It'll very likely still pass, since it's a small, honest, transparent
description of a real installer — just don't be surprised if it sits "pending" for a
few days rather than going live instantly.

## What you need to do (3 steps, a couple of minutes)

Open PowerShell on your PC, `cd` into this folder, then:

**1. Build the package**
```powershell
choco pack
```
This reads `diskgeek.nuspec` and produces `diskgeek.1.0.0.0.nupkg` right here in the folder.

**2. Get your API key one time** (I did not — and can't — look at this myself)

Go to https://community.chocolatey.org/account/ApiKeys , click "Show API Key", copy it.

**3. Push it**
```powershell
choco apikey --api-key YOUR_KEY_HERE --source https://push.chocolatey.org/
choco push diskgeek.1.0.0.0.nupkg --source https://push.chocolatey.org/
```
The first command saves the key locally so you don't have to paste it every time; the
second actually submits the package. That's the "submit" click, effectively — there's
no web button, this push *is* the submission.

After that it goes into Chocolatey's moderation queue. You'll get an email when it's
approved (or if a moderator has questions).

## If `choco pack` complains about anything

Most likely culprit would be nuspec schema validation — if it throws an error, paste it
back to me and I'll fix the file. I validated the XML is well-formed, but I don't have
a Windows machine to run the actual `choco pack`/`choco push` commands from this
session, so this is the one part I couldn't test end-to-end myself.
