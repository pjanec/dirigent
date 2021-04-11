SET CFG=%1
SET ARC=%2
IF "%1"=="" SET CFG=Debug
IF "%2"=="" SET ARC=net5.0-windows
SET OPTIONS=/e /xf SharedConfig.xml /xf *.config

robocopy ..\src\Dirigent.Agent.TrayApp\bin\%CFG%\%ARC%\ . %OPTIONS%
robocopy ..\src\Dirigent.Master\bin\%CFG%\%ARC%\ . %OPTIONS%
robocopy ..\src\Dirigent.CLI.Telnet\bin\%CFG%\%ARC%\ . %OPTIONS%
robocopy ..\src\Dirigent.Reinstaller\bin\%CFG%\%ARC%\ . %OPTIONS%

copy ..\README.md .\

..\src\GenVerStamp.bat %~dp0VersionStamp.txt
