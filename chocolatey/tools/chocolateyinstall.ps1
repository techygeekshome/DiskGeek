$ErrorActionPreference = 'Stop'

# DiskGeek ships an Inno Setup installer. The package downloads it from the GitHub release for
# the matching tag and verifies it against a SHA-256 checksum, rather than embedding the binary.
# Because nothing is embedded, this package must NOT contain a tools\VERIFICATION.txt - that file
# is only for packages that ship a binary inside the nupkg.
$packageArgs = @{
  packageName    = 'diskgeek'
  fileType       = 'exe'
  url            = 'https://github.com/techygeekshome/DiskGeek/releases/download/v1.0.0/DiskGeekSetup.exe'
  checksum       = '1f29df37544b9f5ea829ec87ee4fdc77b2209d3b82cf8e1a986021fc735f70b4'
  checksumType   = 'sha256'
  silentArgs     = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-'
  validExitCodes = @(0, 3010, 1641)
}

Install-ChocolateyPackage @packageArgs
