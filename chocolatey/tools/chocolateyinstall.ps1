$ErrorActionPreference = 'Stop'

# DiskGeek ships an Inno Setup installer. The package downloads it from the GitHub release for the
# matching tag and verifies it against a SHA-256 checksum rather than embedding the binary. Because
# nothing is embedded, this package must NOT contain a tools\VERIFICATION.txt - that file is only
# for packages that ship a binary inside the nupkg, and including one is what the USP 8.0.0
# submission was rejected for.
$packageArgs = @{
  packageName    = 'diskgeek'
  fileType       = 'exe'
  url            = 'https://github.com/techygeekshome/DiskGeek/releases/download/v1.1.0/DiskGeekSetup.exe'
  checksum       = 'ef605542940caa201cfb0f0c52c1cdc206d379fdbbe933f2dac2f82edca859ea'
  checksumType   = 'sha256'
  silentArgs     = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-'
  validExitCodes = @(0, 3010, 1641)
}

Install-ChocolateyPackage @packageArgs
