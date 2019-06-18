SET CFG=%1
IF "%1"=="" SET CFG=Debug


copy ..\src\packages\CommandLineParser.1.9.71\lib\net35\CommandLine.dll  .\
copy ..\src\packages\log4net.2.0.3\lib\net35-full\log4net.dll  .\

copy ..\src\Dirigent.Agent.Core\bin\%CFG%\Dirigent.Agent.Core.* .\

copy ..\src\Dirigent.Agent.Gui\bin\%CFG%\Dirigent.Agent.Gui.* .\

copy ..\src\Dirigent.Common\bin\%CFG%\Dirigent.Common.*  .\

copy ..\src\Dirigent.Master\bin\%CFG%\Dirigent.Master.*  .\

copy ..\src\Dirigent.Agent.TrayApp\bin\%CFG%\Dirigent.agent.* .\

copy ..\src\Dirigent.Agent.CmdLineCtrl\bin\%CFG%\Dirigent.agentcmd.* .\

copy ..\README.md .\

