Param( $buildconf="Release" )

& "$PSScriptRoot\build-linux.ps1" $buildconf

dotnet publish src\Dirigent.Agent.Console\Dirigent.Agent.Console.csproj -f net5.0 -r linux-x64 -o src\Dirigent.Agent.Console\publish\linux-x64\$buildconf

New-Item -ItemType Directory -Force -Path release\linux-x64\$buildconf

robocopy src\Dirigent.Agent.Console\publish\linux-x64\$buildconf\ release\linux-x64\$buildconf\ /MIR

Copy-Item "$PSScriptRoot\VersionStamp.txt" -Destination release\linux-x64\$buildconf -Force
