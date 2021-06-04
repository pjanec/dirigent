Param( $buildconf="Release", $clean=1 )

& "$PSScriptRoot\build-win.ps1" $buildconf $clean

dotnet publish src\Dirigent.CLI\Dirigent.CLI.csproj -f net5.0-windows -r win-x64 --self-contained false -o publish\Dirigent.CLI\win-x64\$buildconf
dotnet publish src\Dirigent.Agent.WinForms\Dirigent.Agent.WinForms.csproj -f net5.0-windows -r win-x64 --self-contained false -o publish\Dirigent.Agent.WinForms\win-x64\$buildconf
dotnet publish src\Dirigent.Gui.ImGui\Dirigent.Gui.ImGui.csproj -f net5.0-windows -r win-x64 --self-contained false -o publish\Dirigent.Gui.ImGui\win-x64\$buildconf

New-Item -ItemType Directory -Force -Path release\win-x64\$buildconf

#robocopy src\Dirigent.Agent\publish\win-x64\$buildconf\ release\win-x64\$buildconf\ /MIR
robocopy publish\Dirigent.Gui.ImGui\win-x64\$buildconf release\win-x64\$buildconf\ /E
robocopy publish\Dirigent.CLI\win-x64\$buildconf release\win-x64\$buildconf\ /E
robocopy publish\Dirigent.Agent.WinForms\win-x64\$buildconf release\win-x64\$buildconf\ /E

Copy-Item "$PSScriptRoot\VersionStamp.txt" -Destination release\win-x64\$buildconf -Force


