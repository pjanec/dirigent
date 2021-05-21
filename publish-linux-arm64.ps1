Param( $buildconf="Release" )

& "$PSScriptRoot\build-linux.ps1" $buildconf

dotnet publish src\Dirigent.Agent.Console\Dirigent.Agent.Console.csproj -f net5.0 -r linux-arm64 --self-contained true -o src\Dirigent.Agent.Console\publish\linux-arm64\$buildconf

New-Item -ItemType Directory -Force -Path release\linux-arm64\$buildconf

robocopy src\Dirigent.Agent.Console\publish\linux-arm64\$buildconf\ release\linux-arm64\$buildconf\ /MIR

Copy-Item "$PSScriptRoot\VersionStamp.txt" -Destination release\linux-arm64\$buildconf -Force
