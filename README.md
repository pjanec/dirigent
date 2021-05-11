## Dirigent Overview

Dirigent is an application life cycle management and diagnostic tool. It allows launching a given set of applications in given order on given computers according to a predefined launch plan. 

It runs on Windows platform with .net 5.0

![dirigent-agent](dirigent-agent.png)

### Launch plans

The plan specifies what applications to launch, on what computers, in what order and what another apps (dependencies) need to be running and initialized prior starting a given application.

The dependencies are checked among both local and remote applications. 

The plans in Dirigent can be used in several different ways

 1. App-keeping
    - To keep the contained apps up and running
    - All the apps contained in the plan are supposed to stay running for the whole life time of the plan (not Volatile)
    - Plan is successful if all apps are up and running.
    - Plan is failing if some of the apps failed to run, crashed, was killed using KillApp etc.

 2. Utility plan
    - To run some one-shot utility commands
    - The plan just run the apps (commands) and terminates. Often it sends a few commands to dirigent without caring whether the comamnds executed succesfully or not.
    - All the apps needs to be marked Volatile
    - The plan Status does not indicate anything useful (no relation to the consequences of running those commands)

 3. A combination of the two options above
    - Non-volatile apps are kept running
    - Volatile apps are started and then forgotten
    - Plan status is valid for the non-volatile apps
    - Failed volatile app (if returning an error as its exit status - depends on InitConditons) may cause the plan to fail

### Plan Status

The plan status indicates whether everything went successfully or if there was a failure.

| Status      | meaning                                                      |
| ----------- | ------------------------------------------------------------ |
| None        | Plan not running, i.e. not taking care about contained applications. |
| In Progress | Plan is running in launch mode. Applications get started sequentially in the order as defined by their interdependencies. Apps are optionally kept alive (restarted) if they terminate unexpectedly. Dirigent tries to guess whether apps have already finished their initialization |
| Success     | All apps have been started and initialized and all are running. The status may change to Failure when some non-volatile app stops running! |
| Failure     | Some apps have failed to start or to initialize within given time limit. The status will switch from Failure to Success as soon as the app causing the failure finally starts and initializes. |
| Killing     | Plan is in killing mode where all apps are being closed. As soon as the apps terminate the status goes back to None. |



### Individual applications control

Applications can be launched, terminated or restarted, either individually or all-at-once.

An application that is supposed to run continuously can be automatically restarted after unexpected termination or crash.

### Application status sharing

The applications are continuously monitored whether they are already initialized and still running. Their status is distributed to all agents on all machines.

Status is encoded in several flags

| Status Flag  | Meaning                                                      |
| ------------ | ------------------------------------------------------------ |
| Started      | The process launch attempt has been made.                    |
| Start Failed | The process launch has failed.                               |
| Running      | The process is currently running.<br />Warning: Process might be already dying! |
| Dying        | The process termination attempt has been made, but the process has not exited yet. |
| Killed       | The process termination attempt has been made via a direct KillApp request (i.e. not as a consequence of a KillPlan request) |
| Initialized  | Dirigent has guessed the app has initialized already. The guessing is based on the configured initialization detector mechanism. |
| Plan Applied | The application has already been processed as part of StartPlan operation. The plan will not attempt to launch the application again. This happens for example if the application was killed by a direct KillApp request. |
| Restarting   | Dirigent is going to restart the app. This happens after crash (if the app is configured to be automatically restarted) or after manual RestartApp request. |



### Launching apps at startup

A launch plan can be executed automatically on computer startup.

To speedup the boot process of a system comprising multiple interdependent computers, certain applications (independent on those on other computers) can be launched even before the connection among computers is established.

### Ways of control

All operations can be controlled

* from any computer via a control GUI

* from a command line tool or

* programmatically via a .net library.

#### Local and networked mode

Dirigent can be configured to to run either in single-machine or networked mode, with embedded control GUI or as GUI-less background process (daemon), or as a command line control application.

#### Architecture

Each computer is running an agent process. One of the computers runs a master server process. Agents connect to a single master. The master's role is to broadcast messages from agents to all other agents and share the launch plans (stored in shared config).

Agent manages the processes running locally on the same machine where the agent is running. Agent takes care of local application launching, killing, restarting and status monitoring. 

![dirigent-internals](doc/DirigentInternalStructure.png)

An agent listens to and executes application management commands from master.

Agents publish the status of local applications to master which in turn spreads it to all other agents. The status include whether the app is running, whether it is already initialized etc.

All agents share the same configuration of launch plans - each one knows what applications the others are supposed to run.

The master inform agents about the current launch plan automatically when the agent connects to the master.

The shared configuration file can be present either just on master or an identical copy of it must be present on every agent.

## Usage

### Basic steps

#### Configure launch plans

Define launch plans, i.e. what apps to start on what computer in what order. Store it into a `SharedConfig.xml` config file next to `Dirigent.Master.exe`.

For example the following plan opens a notepad app first on machine `m1` with file `c:\aaa.txt`. Then, in 2 seconds from launching the notepad on machine m1, it opens another notepad on machine `m2` with file `c:\bbb.txt`. Because of the dependency between those apps, the second notepad won't be launched if the first notepad doesn't start (from any reason).

    <?xml version="1.0" encoding="UTF-8"?>
    <Shared>
        <Plan Name="plan1" StartTimeout="10">
            <App
                AppIdTuple = "m1.a"
                Template = "apps.notepad"
                StartupDir = "c:\"
                CmdLineArgs = "aaa.txt"
                >
                <Env>
                  <Set Variable="TEMP" Value="C:\TEMP" />
                  <Path Prepend="C:\MYPATH;../sub1" /> 
                </Env>
                <WindowPos TitleRegExp="\s-\sNotepad" Rect="10,50,300,200" Screen="1" Keep="0" />
            </App>
    
            <App
                AppIdTuple = "m2.b"
                Template = "apps.notepad"
                StartupDir = "c:\"
                CmdLineArgs = "bbb.txt"
                Dependencies = "m1.a"
            />
        </Plan>
    
        <AppTemplate Name="apps.notepad"
                Template = ""
                ExeFullPath = "c:\windows\notepad.exe"
                StartupDir = "c:\"
                CmdLineArgs = ""
                StartupOrder = "0"
                RestartOnCrash = "1"
                SeparationInterval = "0.5"
            <InitDetectors>
              <WindowPoppedUp TitleRegExp="\s-\sNotepad"/>
              <TimeOut>5.0</TimeOut>
            </InitDetectors>
        />
    
    </Shared>

Deploy this config file to all agents if you want agents to start their plans without first waiting for master. All agents should use identical shared configuration file.

#### Setup a master

Start a master process on one of the machines. Master is not necessary in single-machine mode of operation.

On master machine:

    Dirigent.Master.exe --masterPort 5045 --startupPlan plan1

Alternatively, you can configure one of the dirigent agents to run the master process automatically. See the --IsMaster command line argument of Diregent.Agent process.

#### Deploy agents

On each machine install an agent application.

Assign a unique machineId to each agent so it could identify its application in the launch plan.

You can specify the IP address and port of the master and machineId of in the local configuration of each respective agent.

On first machine:

    Dirigent.Agent.exe --machineId m1 --mode trayGui --startHidden 1 --masterIp 10.1.1.2 --masterPort 5045

On second machine:

    Dirigent.Agent.exe --machineId m2 --mode trayGui --startHidden 1 --masterIp 10.1.1.2 --masterPort 5045

#### Load and start a launch plan

Select a launch plan to start, issue a Select Plan command followed by a Start Plan command.

For example using a command ling control app:

    Dirigent.CLI.Telnet --masterIp 10.1.1.2 --masterPort 5045 StartPlan plan1

Multiple commands can be executed at once if separated by a semicolon. For example  

    Dirigent.CLI.Telnet --masterIp 10.1.1.2 --masterPort 5045 Start plan1; StartPlan plan2

### Available Actions

The Dirigent can work either with whole launch plan or with an individual application that is part of the currently selected launch plan.

#### Launch Plan Actions

- **Select Plan.** The given plan becomes the current plan. New apps defined by this plan are added to the list of operated ones. This affects only the local agent where the command is issued.

- **Start Plan.** Apps from the current plan start to be launched according to the plan.

- **Stop Plan.** Stop launching of apps from the current plan. No apps are killed.

- **Kill Plan.** All apps that are part of the current launch plan are killed.

- **Restart Plan.** All apps from the current plan are first killed and then the plan starts.

#### Individual Apps Actions

- **Kill App.** The app is killed immediately if already running. The auto-restart (if configured) is disabled so that the app stays killed and is not started again automatically.

- **Launch App.** The app is launched if not already running, ignoring any dependency checks.

- **Restart App.** The app is first killed and then launched again.

#### Management Actions

- **Set Vars.** Sets an environment variable(s) for the Dirigent process. They can be used for expansion in the applications' exe paths, command lines and other places.
- **Kill All.** Kills all running apps on all computers and stops all running plans. Kind of a 'central stop' feature.
- **Terminate.** Terminates Dirigent agents on all computers (optionally on selected one).
- **Reboot.** Reboots all computers.
- **Shutdown.** shuts down all computers.
- **Reload Shared Config.** Tries to reload the shared config containing the plan and app definitions.

### Agent configuration options

`Diregent.Agent.exe` is a Windows Forms application capable of running either as a background process with no user interface (just the log file) or as a GUI application that can be minimalized into a system tray.

The options can be specified either on the command line (prefixed with double dash `--`, for example `--optionName`) or in the `agent.config` file located next to the agent executable.

#### Specifying the machine name

The applications are supposed to run on a specific computer. More precisely, to be launched by an agent configured to the same machine id as the application.

 `--machineId m1` ...id of the computer where the agent is running on

#### Operation mode selection

The following options changes the mode of operation:

 `--mode deamon|trayGui|remoteControlGui` .... select mode of operation

- `deamon` ... no user interface at all, just a log file

- `trayGui` ... an icon in tray with GUI control app accessible from the context menu; the default

- `remoteControlGui` ... not agent as such (not directly managing any local apps), just a remote control GUI that monitors the apps and remotely send commands to the agents

`--startHidden 0|1` .... start minimized (only works with `--trayGui` and `--remoteControlGui`)

#### Another options

 `--masterPort 5042` ... mater's port number.  Passed to the master process when `--IsMaster 1` is used.

 `--masterIp 1.2.3.4` ... mater's IP address

 `--mcastIp 239.121.121.121` ... multicast IP address used for application state sharing among agents.  Passed to the master process when `--IsMaster 1` is used.

 `--localIp 10.0.0.17` ... local network interface address used for multicasting (default 0.0.0.0 = auto select)  Passed to the master process when `--IsMaster 1` is used.

 `--mcastAppStates 1` ... use multicast for sharing application states instead of sending via master; multicast improves network performance for many agents.  Passed to the master process when `--IsMaster 1` is used.

 `--logFile xyz.log` ... what log file to use

 `--startupPlan <plan_name>` ... immediately loads an initial plan and makes it the current one (local agent) before the connection to the master is established

 `--sharedConfigFile mySharedConfig.xml` ... what shared config file to use

 `--localConfigFile myLocalConfig.xml` ... what local configuration file to use

 `--isMaster 0|1` .... start master process automatically (no need to run it separately then)

 `--CLIPort 5050` ... Command Line Interface port number. Passed to the master process when `--IsMaster 1` is used.

 `--tickPeriod 500` ... Period in milliseconds of commands/plan processing & GUI refresh. Passed to the master process when `--IsMaster 1` is used.

### Master configuration options

`Dirigent.Master.exe` is a console application designed to run in background on one of the computers.

 `--masterPort 5042` ... what TPC port to run on

 `--mcastIp 239.121.121.121` ... multicast IP address used for application state sharing among agents

 `--localIp 10.0.0.17` ... local network interface address used for multicasting (default 0.0.0.0 = auto select)

 `--mcastAppStates 1` ... use multicast for sharing application states instead of sending via master; multicast improves network performance for many agents

 `--logFile xyz.log` ... what log file to use

 `--sharedConfigFile mySharedConfig.xml` ... what shared config file to use

 `--startupPlan <plan_name>` ... what plan to be forced (make selected) on agents when they connect to master

 `--CLIPort 5050` ... what TPC port to run the Command Line Interface server on

 `--tickPeriod 500` ... Period in milliseconds of the main loop of incoming command broadcasting to clients, plan processing etc.

 `--CLITickPeriod 50` ... Period in milliseconds of processing the CLI server requests. Should be a fraction of the `tickPeriod`. If larger than `tickPeriod`, one CLI server tick per main loop will be executed.

### Agent Command Line Interface over TCP line-based connection

Master is running a TCP server providing for controlling agents' operations.

To send a command you can user a generic TCP socket from your app. Or use `Dirigent.CLI.Telnet.exe` app for testing:

    Dirigent.CLI.Telnet.exe --masterIp 10.1.1.2 --masterCLIPort 5050 Start plan1; StartPlan plan2


TCP server allows multiple simultaneous clients. Server accepts single text line based requests from clients. Line separation character is `\n`. For each request the server sends back one or more status reply lines depending on the command type. Each request can be optionally marked with request id which is then used to mark appropriate response lines. Requests are buffered and processed sequentially, response may come later. Clients do not need to wait for a response before sending another request.

##### Request line format:

  `[optional-req-id] request command text till the end of line\n`

##### Response line format:

  `[optional-req-id] response text till the end of line\n`

##### Request commands

  `StartPlan <planName>` .... starts given plan, i.e. start launching apps
  
  `StopPlan <planName>` ..... stops starting next applications from the plan
  
  `KillPlan <planName>` ..... kills given plans (kills all its apps)
  
  `RestartPlan <planName>` .. stops all apps and starts the plan again
  

Remarks:

 - Killing a plan usualy makes sense just for the app-keeping plans. It makes sure that all the apps get killed. This is indicated by the status switching from Success (all apps running) to Killing (apps getting killed) to None (all apps killed).

 - Started plan shall be killed first in order to be started again.

 - Apps can be started/killed inidividually, without using a plan.

 - Plan-driven app control approach can be combined with individual app control approach.





  `LaunchApp <appId>` ....... starts given app
  
  `KillApp <appId>` ......... kills given app
  
  `RestartApp <appId>` ...... restarts given app
 
 
 
  

  `GetPlanState <planName>`  returns the status of given plan
  
  `GetAppState <appName>`   returns the status of given app


  `GetAllPlansState` ..... returns one line per plan; last line will be "END\n"
  
  `GetAllAppsState` ...... returns one line per application; last line will be "END\n"

  `SetVars VAR=VALUE::VAR=VALUE` ...... sets environment variable(s) to be inherited by the processes launched afterwards (note that you can set multiple variables at once, separated by '::')

  `KillAll` ...... kills all running apps on all computers, stops all plans

  `Terminate [killApps=0] [machineId=<machineId>]` ...... terminates the Dirigent on all stations, optionally leaving the already started apps running

  `Reinstall` ...... terminates the Dirigent on all station and invoke the re-installer app allowing the Dirigent to be relaunched once the Dirigent files have been replaced with a newer version. 

  `Reboot` ...... reboots all computers where the Dirigent is running

  `Shutdown` ...... shuts down all computers where the Dirigent is running

  `ReloadSharedConfig [killApps=1]` ...... tries to reload the shared config, optionally killing all apps before the reload



##### 

##### Response text for GetPlanState

  `PLAN:<planName>:None`
  
  `PLAN:<planName>:InProgress`
  
  `PLAN:<planName>:Failure`
  
  `PLAN:<planName>:Success`
  
  `PLAN:<planName>:Killing`
  

##### Response text for GetAppState

  `APP:<AppName>:<Flags>:<ExitCode>:<StatusAge>:<%CPU>:<%GPU>:<MemoryMB>:<PlanName>`


###### Flags

   Each letter represents one status flag. If letter is missing, flag is cleared.
   
  `S` = started
  
  `F` = start failed
  
  `R` = running
  
  `K` = killed
  
  `D` = dying
  
  `I` = initialized
  
  `P` = plan applied
  
  `X` = restarting
  

###### ExitCode

  Integer number    if exit code (valid only if app has exited, i.e. Started but not Running)

###### StatusAge

  Number of seconds since last update of the app state

###### CPU

[NOT IMPLEMENTED]  Integer percentage of CPU usage

###### GPU

[NOT IMPLEMENTED]    Integer percentage of GPU usage

###### MemoryMB

[NOT IMPLEMENTED]    Integer number of MBytes used


###### PlanName

  The name of plan in whose context the app was most recently launched.


##### Response text for other commands

  `ACK\n` ... command reception was acknowledged, command was issued
  
  `ERROR: error text here\n`
  
  `END\n` ..... ends the list in case the command is expected to produce multiple line response


###### Using request id

  Request:   `[001] StartPlan plan1`
  
  Response:     `[001] ACK`


###### Leaving out the request id

  Request:   `KillPlan plan2`
  
  Response:     `ACK`


###### Wrong identifier

  Request:   `KillPlan invalidPlan1`
  
  Response:     `ERROR: Plan 'invalidPlan1' does not exist`


###### Starting an application

  Request:   `[002] StartApp m1.a`
  
  Response:     `[002] ACK`


###### Getting plan status

  Request:   `[003] GetPlanStatus plan1`
  
  Response:     `[003] PLAN:plan1:InProgress`


###### Getting app status

  Request:   `GetAppStatus m1.a1`
  
  Response:     `APP:m1.a:SIP:255:10:34:0:7623`

###### Setting environment variable

  Request:   `[002] SetVars VAR1=VALUE1::VAR2=VALUE2`
  
  Response:     `[002] ACK`


###### Killing all apps

  Request:   `KillApps`
  
  Response:     `ACK`


###### Terminating Dirigent

  Request:   `Terminate`
  
  Response:     `ACK`


###### Reinstalling Dirigent

  Request:   `Reinstall`
  
  Response:     `ACK`


###### Rebooting all computers

  Request:   `Reboot`
  
  Response:     `ACK`


###### Shutting down all computers

  Request:   `Shutdown`
  
  Response:     `ACK`


###### Reloading SharedConfig

  Request:   `ReloadSharedConfig killApps=1`
  
  Response:     `ACK`



### Agent Console Command Line Utility

There is a small executable specialized for sending commands to agents. It connects to the master and send a command specified on the command line.

 `Dirigent.CLI.Telnet <command> <arg1> <arg2>; <command> <arg1>...`

Zero exit code is returned on success, positive error code on failure.

The commands just simply follow the available agent actions, please see chapter *Available Actions* for more details.

    StartPlan <planId>
    
    StopPlan <planId>
    
    KillPlan <planId>
    
    RestartPlan <planId>
    
    
    LaunchApp <appId>
    
    KillApp <appId>
    
    RestartApp <appId>
    
    
    
    SetVars VAR1=VALUE1::VAR2=VALUE2::VAR3=VALUE3
    
    
    KillApps
    
    Terminate
    
    Reinstall
    
    Reboot
    
    Shutdown
    
    ReloadSharedConfig killApps=1
    

Multiple commands on a single line can be separated by semicolon
    `Dirigent.CLI.Telnet LaunchApp m1.a;StartPlan plan1`

## Configuration

Dirigent configuration comprises of two parts - a shared configuration  and a local configuration.

Shared configuration is shared among all agents. It specifies the launch plans but can be used also for another information like the names of all the machines involved etc.

Local configuration defines the network settings, operation mode details of a single agent or master application.

Local configuration is assembled from different pieces
 - the command line arguments
 - application executable config file
 - local configuration file


### Shared config file

Shared configuration contains the settings that needs to be same for all agents, for example the start plan definitions.

Shared configuration is stored in the `SharedConfig.xlm` file. The location of the file can be set through application option `sharedConfigFile`.

Shared config file is mandatory. Dirigent won't start without it.

### Agent's local config file

Agent's local config file contains configuration that is specific for an agent (for example folder watching settings). Each agent can use its own local configuration.

Local configuration is stored in the `LocalConfig.xlm` file. The location of the file can be set through application option `localConfigFile`.

Local configuration file is optional.


#### Launch plan

Launch plan comprises just a list of apps to be launched in given order. Multiple parallel plans can be active at a time.

##### `<plan/>` element

* `StartTimeout` - time in seconds before an unsuccessfully running plan is reported as *Failed*. Unsuccessful means that
  * Non-volatile apps (that should be running all the time) is not running or has not initialized yet.
  * Volatile apps have not yet been started, initialized or finished.

##### `<app/>` element

Each app in the launch plan has the following attributes:

- `AppIdTuple` - unique text id of the application instance; comes together with the machine id; format "machineId.appId"

- `ExeFullPath` - application binary file full path; can be relative to the Dirigent's shared config file location (or CWD if none defined). Environment variables in form of %VARNAME% are expanded using Agent's current environment.

  The following reserved values are handled in a specific way:

    - `[cmd]` - Similar to `cmd.exe <CmdLineArgs>`. Launches cmd.exe executable. Command line arguments stay untouched, passed to cmd.exe as they are specific in the `CmdLineArgs` attribute.
      Example: `ExeFullPath = "[cmd]" CmdLineArgs = "/c dir"`
  - `[cmd.file]` - similar to `cmd.exe /c <CmdLineArgs>`. Example: `ExeFullPath = "[cmd.file]" CmdLineArgs = "dir"`
  - `[cmd.command]` - same as `[cmd.file]`
  - `[powershell]` - Similar to `powershell.exe <CmdLineArgs>`. Launches powershell executable. Command line arguments stay untouched, passed to powershell.exe as they are specific in the `CmdLineArgs` attribute.
    Example: `ExeFullPath = "[powershell]" CmdLineArgs = "--file test1.ps1"`
  - `[powershell.command]` - launches `powershell.exe -command <CmdLineArgs>`.
    Example: `ExeFullPath = "[powershell.command]" CmdLineArgs = "ls"`
  - `[powershell.file]` - launches `powershell.exe -file <CmdLineArgs>`.
    Example: `ExeFullPath = "[powershell.file]" CmdLineArgs = "test1.ps1"`
  - `[dirigent.command]` - executes a dirigent command stored in `CmdLineArgs` attribute is if passed to Dirigent.CLI.Telnet command line (but parsed and executed internally by the dirigent agent). Multiple commands can be entered, separated by a semicolon.  The commands are sent immediately over the network, Dirigent does not wait for their completion so this 'app' never enter the `Running` state and immediately goes to 'Terminated'. Please always mark this app record as Volatile so the plan does not expect the app to stay running .
    Example: `ExeFullPath = "[dirigent.command]" CmdLineArgs = "LaunchApp m1.a; KillPlan plan2" Volatile="1"`

- `StartupDir` - startup directory; can be relative to the Dirigent's shared config file location (or CWD if none defined). Environment variables in form of %VARNAME% are expanded using Agent's current environment.

- `CmdLineArgs` - command line arguments

- `PriorityClass` - one of `Idle`, `BelowNormal`,  `Normal`, `AboveNormal` , `High`, `RealTime`. If missing or empty, default priority class as set by the OS is used.

- `StartupOrder` - the launch order in case of same priority of multiple apps

- `Volatile 0|1` - whether the application is expected to terminate automatically and not stay forever until killed; Such apps are not part of plan start success condition, meaning the plan reports 'success' even if this app already terminates.

- `Disabled 0|1` - whether the application is initially excluded from plan operation.

- `RestartOnCrash 0|1` - whether to automatically restart the app after crash. See the *Restarter* subsection for more details

- `AdoptIfAlreadyRunning 0|1` - whether not to start a new instance of a process if the process with same executable image name is already running. The adoption attempt is made only when the app is about to be started or killed. Dirigent does not scan all running processes periodically so it does not show the not-yet-adopted app as running until the app is launched via Dirigent. *WARNING: Should not be used for apps that may run in multiple instances on the same computer! Just first instance would be adopted!* 

- `Dependencies` - what apps is this one dependent on, i.e. what apps have to be launched and fully initialized before this one can be started; semicolon separated AppIdTuples.

- `InitCondition` - a mechanism to detect that the app is fully initialized (by time, by exit code etc.) See chapter *Selecting a boot up completion detector*. **DEPRECATED**, use the `InitDetectors` section instead.

- `WindowStyle` - "normal" (default), "minimized", "maximized", "hidden"

- `Template` - where to load default settings from; the name of a AppTemplate section in the same XML file

- `KillTree 0|1` - whether to kill not just the single process but also all its child processes. Child processes are killed only in case of a hard kill if previous "softer" attempts (if any, see KillSeq) fail.

- `KillSoftly 0|1` - whether to send the close command (as if user pressed the close button) instead of a forceful kill. Note this is implemented using the *SoftKill* mechanism described below with timeout=10secs. **DEPRECATED**, use the `SoftKill` section instead

- `SeparationInterval <numseconds>` - how much time to wait before starting the next application

App sub-sections:

- `SoftKill`
  
        <SoftKill>
           <Keys Timeout="1.5" Keys="^(c)"/>
           <Close Timeout="0.7"/>
        </SoftKill>
  
  Defines a a sequence of "soft" attempts to terminate a process. Dirigent try to terminate the process using the actions from the sequence, starting with the first one defined.
  
  If the action fail (the process is not terminated within defined timeout), next action (presumably more severe) is tried.
  
  Only if all actions fail, the process is killed in the hard way.
  
  If an extra Kill command is issued while the process is being attempted to be terminated in the soft way, the process is killed immediately in the hard way (impatient kill.)

  Note: The *KillTree* option in NOT applied if Dirigent succeeded to terminate the process in one of the soft ways.

  Sub-sections:
  
  - `Keys` - send one or more keys to the main window. See Window.Forms.SendKeys manual for the key name format. 

  - `Close` - emulates the close command sent to the main window.


- `WindowPos`
  
      <WindowPos TitleRegExp="\s-\sNotepad" Rect="10,50,300,200" Screen="1" Keep="0" /> 
  
  Finds a window belonging to the application by its title using regular expression search. Affects window settings (position, z-order etc.)
  
  The window must belong to the started process or to its first-level child processes. This allows for launching a batch file and starting the target process from there.
  
  There can be multiple WindowPos sections defined for one application.
  
  Attributes:
  
  - `TitleRegExp` - regular expression to search in the window title. This is the only mandatory attribute, the rest of attributes are optional.
  
  - `Rect` - desired screen coordinates [left,top,width,height] of the window relative to the given screen. All zeros means 'not set' and behaves as if not specified at all. 
  
  - `Screen` - screen number to place the window at; 0=main screen (default)
  
  - `Keep` - 0/1 whether to keep applying the coordinates in short regular intervals, i.e. to force the window to stay at given coordinates. If not set, the first successful search for
  
  - `SendToBack` - 0/1 whether to put window below all other windows, i.e. to avoid popping up
  
  - `BringToFront` - 0/1 whether to put window to the foreground and activate it; usefel in combination with Keep="1" to keep the window visible and focused
  
  - `TopMost` - 0/1 whether to make the window 'Always on top'
  
  - `WindowStyle` - "normal" | "minimized" | "maximized" | "hidden"
  
  If used in a template, the WindowPos definition is added to all application using this template.
  
  - `InitDetectors`
    
    <InitDetectors>
        <WindowPoppedUp TitleRegExp="\s-\sNotepad"/>
        <TimeOut>5.0</TimeOut>
      </InitDetectors>
  
  Defines a mechanism to detect that the app is fully initialized (by time, by exit code etc.) See chapter *Selecting a boot up completion detector*  
  
  If multiple detectors are defined, the first one whose condition is satisfied marks the app as initialized.

- `Env`
  
      <Env>
        <Set Variable="TMP" Value="C:\TEMP" />
        <Set Variable="TEMP" Value="C:\TEMP" />
        <Path Prepend="C:\MYPATH1" Append="C:\MYPATH2;..\sub1"/> 
        <Local Variable="P1" Value="myLocalParam1" />
      </Env>
  
  Modifies the environment variables for the started process, taking the Dirigent Agent's startup environment as a basis.
  
  Existing environment variables can be set to a new value. Non-existing will be created, existing will be overwritten.
  
  Specific support for PATH variable allows prepending or appending given string to PATH.
  
  Attributes:
  
  - `Set` - set given variable to a new value. Both attributes `Variable` and `Name` are mandatory. Environment variables in form of %VARNAME% contained in the Value are expanded using Agent's current environment.
  - `Path` - if attribute `Prepend` is present, prepends its value at the beginning of the PATH variable. if attribute `Append` is present, appends its value at the end of the PATH variable. Environment variables contained in the `Prepend` or `Append` attribute values in form of %VARNAME% are expanded using Agent's current environment. Relative paths are considered relative to the location of the shared config file and are converted to absolute paths.
  - `Local` - set Dirigent's internal variable to given value. The variable can be used for expansion inside process exe path and command line similarly as the env vars but is not propagated to the process environment.
  
- `Restarter`
  
      <Restarter maxTries="2" delay="5"/>
  
  This settings specifies the parameters of the restart service which is activated upon application crash (when the app terminates without being killed via Dirigent). Such service is enabled only if  `RestartOnCrash='1'`.
  
  Dirigent will try to restart a crashed app for given number of times before it gives up. Upon crash, Dirigent waits for specified time before a restart attempt is made.
  
  Attributes:

  - `maxTries` - how many restart attempts are made before the Dirigent gives up restarting. -1 means 'try forever'. Default is -1.
  - `delay` - how long time in seconds to wait before the Dirigent attempts to restart a crashed app. Default is 1 sec.

  Upon an `StartPlan` or `LaunchApp` request the number of remaining restart attempts is reset to the `maxTries` value.
  
  `KillApp` or `KillPlan` requests deactivate any pending restart operation.

#### Templated launch plan definition

Plan definition in an XML file uses a template sections allowing the inheritance of attributes.

Every record in the plan can reference a template record.

All the attributes are loaded first from the template and only then they can get overwritten by equally named attributes from the referencing entry. 

A template record itself can reference another more generic template record.

#### Identifying an application

The application instance is uniquely addressed by the name of the computer it is running on and by the name chosen for particular instance of an application. These two are separated by a dot, having format `machineId.applicationInstanceId`.

The `machineId` is unique globally. 

The `applicationInstanceId` is unique within the launch plan where it is used.

#### Launch plan example

The following plan example specify two instances of a notepad editor, named `a` and `b`. Both are based on the same template `apps.notepad`, just with specific command line argument (different files to edit).

The apps will be run on a computer where the agent is configured to  machineId `m1`.

```n
    <Plan Name="plan1">
        <App
            AppIdTuple = "m1.a"
            Template = "apps.notepad"
            StartupDir = "c:\"
            CmdLineArgs = "aaa.txt"
            >
            <WindowPos titleregexp="\s-\sNotepad" rect="10,50,300,200" screen="1" keep="0" />
        </App>

        <App
            AppIdTuple = "m1.b"
            Template = "apps.notepad"
            StartupDir = "c:\"
            CmdLineArgs = "bbb.txt"
        />
    </Plan>

    <AppTemplate Name="apps.notepad"
            Template = ""
            ExeFullPath = "c:\windows\notepad.exe"
            StartupDir = "c:\"
            CmdLineArgs = ""
            StartupOrder = "0"
            RestartOnCrash = "1"
            InitCondition = "timeout 2.0"
            SeparationInterval = "0.5"
    />
```

#### Selecting a boot up completion detector

Some apps take a long time to boot up and initialize. Dirigent should not start a dependent app until its dependencies are satisfied. By 'satisfied' it is meant that the all the dependencies are already running and that they have completed their initialization phase.

Dirigent supports multiple methods of detection whether an application is already up and running. The method together with its parameters can be specified for each application in the launch plan.

If not boot up completion detectors are defined, the app is considered initialized from the time it has been started. 

Following methods are available

- `<timeout>seconds</timeout>` - After specified amount of seconds after launching the app

- `<exitcode> <number></exitcode>` - After the app have terminated and its exit code matches the number specified. This can be combined with an auto-restart option of the application, resulting in a repetitive launches until given exitcode is returned.

### Starting with local copy of Shared config

Before the agent connects to master, it is using its local copy of SharedConfig. This is useful if agent needs to start applications event before the connection to master is established.

### Adopting master's plan upon connection

As soon as an agent connects to master, it receives and adopts the master's copy of the shared config. The local copy should be of course identical to the master's copy. If it is not, the currently running agent's plan is stopped, i.e. the all the apps launched by the agent so far are killed nad the new master's plan takes place.


### Utility Plans vs. standard plans

Usual plan "wants" to start all apps and watches if they are started. Such plan never ends automatically on its own even if all apps crash. If an app is set to be restarted automatically, the plan will do so until the plan gets stopped or killed.

Such the plan also can not be started again before it is manually stopped or killed.

An utility-plan it the one containing just volatile apps (having Volatile="1") is handled in a special way. Is is stopped automatically as soon as all the apps have been processed (an attempt to start them was performed) and all started apps have terminated (none left running).

Such volatile-only plan allows for being started again without prior stop or kill command.


### Autodetection of the machine id

Computer's NetBIOS name is used as a default machineId if the machine id is not specified on the command line.

### Logging

Both agent and master support logging of errors, warnings etc. into a log file through a Log4net library. The log file name as well as other options for logging (verbosity etc.) can be specified as a part of app.config file. 


### Folder Watching
Dirigent agent can be configured to watch a folder for file changes and trigger actions upon such a change.

The configuration is stored in agent's local configuration file (see --localConfigFile command line option).

Example of setting in agent's local config file:

	  <FolderWatcher
		  Path = "..\..\Tests"
		  IncludeSubdirs="0"
		  Conditions="NewFile"
		  Filter = "*.txt"
		  >
		  <!-- what to do if change detected -->
		  <Action Type="StartPlan" PlanName="CollectLogs">      
		  <Action Type="LaunchApp" AppIdTuple="PC1.WarningApp"/>
	  </FolderWatcher>

The Path, if relative, is resolved relative to the location of the SharedConfig.xml file. Environment variables in form of %VARNAME% are expanded using Agen't current environment.
	  
Conditions supported:

 * `NewFile` ... file gets created

Action types supported
 * `StartPlan` ... starts predefined plan (does nothing if already running and not finished yet)
 * `LauchApp` .... starts predefined application (does nothing if already running)

Errors related to FolderWatcher (path not valid etc.) are logged only info agent's log file. Error results in FolderWather not being installed.

### Environment Variable for processes started by Dirigent Agent

As any other process in Windows OS, the processes started by Dirigent inherit the environment variables of their parent process, i.e. the Dirigent agent itself.

The variables in the Dirigent agent's environment can be manipulated at runtime via the `SetVars` command.

Dirigent agent defines the following special variables for an app started from the launch plan:

 * `DIRIGENT_MACHINEID` = the machine id the agent was configured to (the first part of the AppIdTuple).
 * `DIRIGENT_APPID` = the application id (the second part of the AppIdTuple).
 * `DIRIGENT_PLAN` = the plan in whose context the app was started. Current plan name for apps launched without a plan via `LauchApp` command. Empty if no current plan.
 * `DIRIGENT_SHAREDCONFDIR` = full directory path to the SharedConfig.xml file
 * `DIRIGENT_MASTER_IP` = IP address of a Dirigent Master as defined in agent's configuration.


This provides a way to tell the processes started by the dirigent agent what station/machine (in terms of the dirigent machine naming) they are running at. This might come in handy if same process is started on many machines, it needs to know where it was started but you can not rely on the computer name.

Being environment variables, they can be used in command line parameters for the started process in the plan config file.

        <Plan Name="plan1">
            <App
                AppIdTuple = "m1.a"
                Template = "apps.notepad"
                StartupDir = "%DIRIGENT_SHAREDCONFDIR%\..\Documents"
                CmdLineArgs = "%DIRIGENT_MACHINEID%_%DIRIGENT_APPID%.txt"
                >
            </App>
        </Plan>

## Further Details

#### Multiple coexisting plans

Dirigent works with the union of all the applications found in the plans from the plan repository. Any of the plans can be selected and manipulated at any time.

All application that are still running and coming from some previously started plan are adopted by the new plan if their name matches one of the new plan's apps. 

Plans shall be designed and manipulated (started/killed etc.) in a non-conflicting way. Easiest way is to avoid using same application Id in multiple plans.  

### Execution of a launch plan

The application from the plan are initially assigned the state 'not launched'.

The launch order of all apps form the plan is determined. The result is a sequence of so called launch waves. A wave contains applications whose dependencies have been satisfied by the previous launch wave. The first wave comprises apps that do not depend on anything else. In the next wave there are apps dependent on the apps from the previous wave.

The waves are launched sequentially one after another until all apps from all waves have been launched. 

If some application fails to start, dirigent can be configured to retry the launch attempt multiple times.

If all attempts fail, the launch plan is stopped and an error is returned.

### Shortcuts

If a Dirigent's tray GUI is running on a machine, currently selected plan can be started/killed/restarted via a shortcut.

Also the current plan can be selected based on its order in the SharedConfig.xml.

Shortcut can be redefined in the Dirigent.Agent.exe.config. By default

    Start current plan ..... Control + Shift + Alt + S
    Kill current plan ...... Control + Shift + Alt + K
    Restart current plan ... Control + Shift + Alt + R
    Select 1st plan ........ Control + Shift + Alt + 1
    Select 2nd plan ........ Control + Shift + Alt + 2
    Select 3rd plan ........ Control + Shift + Alt + 3
    ...
    Select 9th plan ........ Control + Shift + Alt + 9
