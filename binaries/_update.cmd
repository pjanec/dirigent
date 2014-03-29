SET CFG=%1
IF "%1"=="" SET CFG=Debug


copy ..\src\packages\CommandLineParser.1.9.71\lib\net35\CommandLine.dll  .\
copy ..\src\packages\log4net.2.0.3\lib\net35-full\log4net.dll  .\

copy ..\src\Dirigent.Agent.Core\bin\%CFG%\Dirigent.Agent.Core.dll .\

copy ..\src\Dirigent.Agent.Gui\bin\%CFG%\Dirigent.Agent.Gui.dll .\

copy ..\src\Dirigent.Common\bin\%CFG%\Dirigent.Common.dll  .\

copy ..\src\Dirigent.Master\bin\%CFG%\Master.exe  .\

copy ..\src\Dirigent.Agent.TrayApp\bin\%CFG%\agent.exe .\

copy ..\src\Dirigent.Agent.CmdLineCtrl\bin\%CFG%\agentcmd.exe .\


