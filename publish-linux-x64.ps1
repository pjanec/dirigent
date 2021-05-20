Param( $buildconf="Release" )

& "$PSScriptRoot\build-linux.ps1" $buildconf

dotnet publish src\Dirigent.Agent\Dirigent.Agent.csproj -f net5.0 -r linux-x64 -o src\Dirigent.Agent\publish\linux-x64\$buildconf

New-Item -ItemType Directory -Force -Path release\linux-x64\$buildconf

robocopy src\Dirigent.Agent\publish\linux-x64\$buildconf\ release\linux-x64\$buildconf\ /MIR


