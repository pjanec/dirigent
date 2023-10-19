# Agent

**Agent** is a process running on each machine to be controlled by the Dirigent. It executes commands received from Master or from Dirigent UI and publishes the status info about the machine and its apps.

**Master** is a centralized "brain" of the Dirigent. It runs as part of one of the agents. There can be just one master in the system. Master is a server all the agents connect to as clients.

**UI** is a GUI app that shows the current status and allows to control Dirigent's functions.

All of these components are compiled into a single `Dirigent.Agent.exe` executable.

### Dirigent.Agent

`Dirigent.Agent.exe` can be running either as a background process with no user interface (the default for Linux) or as a GUI Windows Forms GUI application that shows an icon in a system tray (default for Windows).


## Configuration

Configuration of the Dirigent processes is assembled from different sources, sorted by priority in descending order

 - The command line arguments
 - Application executable config file *.dll.config
 - Hardcoded application defaults

If the option is not specified on command line, it is searched in the executable .config file. If not present there or empty, the hardcoded default is used.

All dirigent apps accept the same set of command line arguments and config sections. If the arguments has no meaning for certain app, the argument is ignored.

### Configuration options

The options can be specified either on the command line (prefixed with double dash `--`, for example `--optionName`) or in the `Dirigent.Agent.dll.config` file located next to the agent executable.

 `--machineId m1` ...id of the agent instance (identifies the computer the agent is running on)

 `--mode deamon|agent|trayGui|gui|master` .... select mode of operation

- `agent` or `trayGui` ... an icon in tray with GUI control app accessible from the context menu; the default
- `deamon` ... agent with no user interface at all, just a log file
- `gui` ... not agent as such (not directly managing any local apps), just a remote control GUI that monitors the apps and remotely sends commands to the agents
- `master` ... no agent, no gui,  just the master part

`--startHidden 0|1` .... start minimized (only works with `--trayGui` and `--remoteControlGui`)

 `--masterPort 5042` ... mater's port number.  Passed to the master process when `--IsMaster 1` is used.

 `--masterIp 1.2.3.4` ... mater's IP address

 `--logFile xyz.log` ... what log file to use

 `--startupPlan <planId>` ... for GUI only; when GUI opens, this plan gets selected as the "current" one

 `--startupScript <scriptId>` ... for master only; starts given script on startup

 `--sharedConfigFile mySharedConfig.xml` ... what shared config file to use (master only)

 `--localConfigFile myLocalConfig.xml` ... what local configuration file to use

 `--isMaster 0|1` .... run master component `Diregent.Agent.exe` only
 
 `--CLIPort 5050` ... Command Line Interface port number. Passed to the master process when `--IsMaster 1` is used.

 `--httpPort 8877` ... Web API port number. -1 to disable the web api. Passed to the master process when `--IsMaster 1` is used.

 `--tickPeriod 500` ... Period in milliseconds of commands/plan processing & GUI refresh. Passed to the master process when `--IsMaster 1` is used.

 `--rootForRelativePaths` ... in what folder to look for processes that are specified by relative path. If not specified, the SharedConfig.xml file location is used (if defined). If neither the SharedConfig file is defined, the app's current working directory is used.

 `--guiAppExe` ... what app executable to run when the tray icon is double clicked; if not defined, the default winForms gui embedded in the agent executable is shown.

 `--attachdebugger` ... pauses the agent loading until Enter is pressed on the console. Allows attaching a debugger to a starting agent process. Requires the agent to be started form a console window!

### 


### Autodetection of the machine id

Computer's NetBIOS name is used as a default machineId if the machine id is not specified on the command line.

### 

## Keyboard Global Shortcuts

If a Dirigent's tray GUI is running on a machine, currently selected plan can be started/killed/restarted via a shortcut.

Also the current plan can be selected based on its order in the `SharedConfig.xml`.

Shortcut can be redefined in the `Dirigent.Agent.dll.config`. By default

    Start current plan ..... Control + Shift + Alt + S
    Kill current plan ...... Control + Shift + Alt + K
    Restart current plan ... Control + Shift + Alt + R
    
    Select no plan ......... Control + Shift + Alt + 0
    Select 1st plan ........ Control + Shift + Alt + 1
    Select 2nd plan ........ Control + Shift + Alt + 2
    Select 3rd plan ........ Control + Shift + Alt + 3
    ...
    Select 9th plan ........ Control + Shift + Alt + 9

