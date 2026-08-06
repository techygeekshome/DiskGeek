$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName    = 'diskgeek'
  fileType       = 'exe'
  url            = 'https://techygeekshome.info/downloads/diskgeek/DiskGeekSetup.exe'
  softwareName   = 'DiskGeek*'
  checksum       = '1F29DF37544B9F5EA829EC87EE4FDC77B2209D3B82CF8E1A986021FC735F70B4'
  checksumType   = 'sha256'
  silentArgs     = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-'
  validExitCodes = @(0)
}

Install-ChocolateyPackage @packageArgs
