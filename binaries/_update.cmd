SET CFG=%1
SET ARC=%2
IF "%1"=="" SET CFG=Debug
REM IF "%2"=="" SET ARC=x64\

copy ..\src\packages\CommandLineParser.1.9.71\lib\net35\CommandLine.dll  .\
copy ..\src\packages\log4net.2.0.3\lib\net35-full\log4net.dll  .\

copy ..\src\Dirigent.Agent.Core\bin\%ARC%%CFG%\Dirigent.Agent.Core.* .\

copy ..\src\Dirigent.Agent.Gui\bin\%ARC%%CFG%\Dirigent.Agent.Gui.* .\

copy ..\src\Dirigent.Common\bin\%ARC%%CFG%\Dirigent.Common.*  .\

copy ..\src\Dirigent.Master\bin\%ARC%%CFG%\Dirigent.Master.*  .\

copy ..\src\Dirigent.Agent.TrayApp\bin\%ARC%%CFG%\Dirigent.Agent.* .\

copy ..\src\Dirigent.Agent.CmdLineCtrl\bin\%ARC%%CFG%\Dirigent.AgentCmd.* .\

copy ..\src\Dirigent.CLI\bin\%ARC%%CFG%\Dirigent.CLI.* .\

copy ..\src\Dirigent.CLI.Telnet\bin\%ARC%%CFG%\Dirigent.CLI.Telnet.* .\

copy ..\README.md .\

@REM determine dirigent's binaries version number and put it to the version stamp file
@SET PSCMD=powershell -ExecutionPolicy Bypass -NoLogo $ver=[System.Reflection.Assembly]::LoadFrom('Dirigent.Common.dll').GetName().Version; Write-Host $ver
@SET BIN_VER=
@for /F %%i in ('"%PSCMD%"') do @SET BIN_VER=%%i

..\src\GenVerStamp.bat %~dp0VersionStamp.txt %BIN_VER%
