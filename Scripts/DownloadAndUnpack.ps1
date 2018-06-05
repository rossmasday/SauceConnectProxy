#$uri = Invoke-RestMethod -uri https://saucelabs.com/versions.json | select -ExpandProperty 'Sauce Connect' |  select -ExpandProperty 'win32' |  select -expand download_url
#Invoke-WebRequest $uri -OutFile "sc.zip"
#Expand-Archive .\sc.zip -DestinationPath $PSScriptRoot -Force
#$sqlPackagePath = Get-ChildItem -Path "C:\Program Files (x86)\Microsoft SQL Server" -Filter "SqlPackage.exe" -recurse | % { $_.FullName }
Write-Output $sqlPackagePath
Get-ChildItem -Path "$PSScriptRoot" -Filter "sc.exe" -recurse | Copy-Item -Destination "$PSScriptRoot/../src/SauceConnectProxy/proxy/sc.exe" -Force