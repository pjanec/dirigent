Param( $buildconf="Release" )

& "$PSScriptRoot\build-linux.ps1" $buildconf

dotnet publish src\Dirigent.Agent\Dirigent.Agent.csproj -f net5.0 -r linux-arm64 --self-contained true -o src\Dirigent.Agent\publish\linux-arm64\$buildconf

New-Item -ItemType Directory -Force -Path release\linux-arm64\$buildconf

robocopy src\Dirigent.Agent\publish\linux-arm64\$buildconf\ release\linux-arm64\$buildconf\ /MIR


