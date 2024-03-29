Param( $buildconf="Release", $clean=1 )

& "$PSScriptRoot\build-linux.ps1" $buildconf $clean

dotnet publish src\Dirigent.Agent.Console\Dirigent.Agent.Console.csproj -f net6.0 -r linux-arm64 --self-contained true -o publish\Dirigent.Agent.Console\linux-arm64\$buildconf

New-Item -ItemType Directory -Force -Path release\linux-arm64\$buildconf

robocopy publish\Dirigent.Agent.Console\linux-arm64\$buildconf\ release\linux-arm64\$buildconf\ /MIR

Copy-Item "$PSScriptRoot\VersionStamp.txt" -Destination release\linux-arm64\$buildconf -Force
