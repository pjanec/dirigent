# Dirigent Overview

Dirigent is a multi-platform application life cycle management and diagnostic tool. It allows

* Launching a given set of apps in given order on multiple different computers according to a predefined XML launch plans
* Monitoring if the apps are  running, optionally restarting them automatically.
* Remotely starting the app/plan, querying the app/plan status
* Automation of the above tasks via user scripts written in C#
* Controlling via a simple GUI apps running in a system tray.
* Remote control via a CLI utility or via a TCP connection

It depends on .net 5.0, running on Windows and Linux.

![dirigent-agent](dirigent-agent.png)

# Quick Start Guide

#### Configure launch plans

Define launch plans, i.e. what apps to start on what computer in what order. Store it into a `SharedConfig.xml` config file next to the `Dirigent.Agent.exe` on a computer chosen to be the Dirigent Master.

For example the following plan "plan1" contains two applications. One named "a" should be run  on machine 'm1', the other is named "b" and should be run on machine m2.

Both apps are based on the same template `apps.notepad` defining the basic attributes that are then overridden with settings from the <App /> element.

    <?xml version="1.0" encoding="UTF-8"?>
    <Shared>
        <Plan Name="plan1" StartTimeout="10">
            <App
                AppIdTuple = "m1.a"
                Template = "apps.notepad"
                StartupDir = "c:\"
                CmdLineArgs = "aaa.txt"
                >
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
                SeparationInterval = "3.5"
            <InitDetectors>
              <WindowPoppedUp TitleRegExp="\s-\sNotepad"/>
              <TimeOut>5.0</TimeOut>
            </InitDetectors>
        />
    
    </Shared>

What that plan will do if launched?

1. Opens a notepad app (called 'a') on machine `m1` with file `c:\aaa.txt`. 
2. Waits for the notepad to open. The window will be positioned to specified screen coordinates.
3. Waits 3.5 seconds
4. Opens another notepad (called 'b') on machine `m2` with file `c:\bbb.txt`.
5. If the 'a' notepad doesn't start in 5 seconds, the plan indicates a failure. Moreover, because 'b' is defined to be dependent on 'a', the 'b' notepad won't be launched.

#### Deploy agents

On each machine install the Dirigent binaries (Windows Desktop .net runtime required).

Assign a unique machineId to each agent so it could identify its application in the launch plan. The machineIds need to match those used in the SharedConfig.xml.

On first machine start the agent in master mode (there must be exactly one master):

    Dirigent.Agent.exe --machineId m1 --isMaster 1

On second machine start the agent in slave mode; specify the IP address of the master machine

    Dirigent.Agent.exe --machineId m2 --masterIp 10.1.1.2

#### Tell Dirigent what to do

Issue a Start Plan command, either from Dirigent's UI, or via a CLI.

For example using a command ling control app:

    Dirigent.CLI.exe --masterIp 10.1.1.2 StartPlan plan1

Multiple commands can be executed at once if separated by a semicolon. For example  

    Dirigent.CLI.exe --masterIp 10.1.1.2 StartApp m1.a; StartApp m2.b



## Individual applications control

Applications can be launched, terminated or restarted, either individually or all-at-once.

An application that is supposed to run continuously can be automatically restarted after unexpected termination or crash.

Available apps are predefined in SharedConfig.xml.


### Individual Apps Actions

- **Kill App.** The app is killed immediately if already running. The auto-restart (if configured) is disabled so that the app stays killed and is not started again automatically.
- **Launch App.** The app is launched if not already running, ignoring any dependency checks.
- **Restart App.** The app is first killed and then launched again.
- **Get App State.** Returns the status of one concrete app.
- **Get All Apps State.** Returns the status of all apps known to Dirigent.

### Application status

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

#### Identifying an application

The application instance is uniquely addressed by the name of the computer it is running on and by the name chosen for particular instance of an application. These two are separated by a dot, having format `machineId.applicationInstanceId`.

The `machineId` is unique globally. 

The `applicationInstanceId` is unique within the launch plan where it is used.

#### Detecting that an app has initialized

Some apps take a long time to boot up and initialize. 

Dirigent supports multiple methods of detection whether an application is already up and running. The method (called the initialization detector) can be specified for each application in the launch plan.

Following methods are available

- **Timeout** - the app is considered initialized if still running after specified amount of seconds after launching the app
- **ExitCode** - the app is considered initialized after it has terminated and its exit code matches the number specified. This can be combined with an auto-restart option of the application, resulting in a repetitive launches until given exit code is returned.

If no initialization detector is defined, the app is considered initialized from the time it has been started. 

## Launch plans

The plan specifies what applications to launch, on what computers, in what order and what another apps (dependencies) need to be running and initialized prior starting a given application.

The dependencies are checked among both local and remote applications. 

Available plans are predefined in SharedConfig.xml.

### Launch Plan Operations

- **Start Plan.** Apps from the current plan start to be launched according to the plan.
  - Note: Apps can be started/killed individually, without using a plan.
   - Note: Plan-driven app control approach can be combined with individual app control approach.
- **Stop Plan.** Stop launching of apps from the current plan. No apps are killed.
- **Kill Plan.** All apps that are part of the current launch plan are killed.
- Killing a plan usually makes sense just for the app-keeping plans. It makes sure that all the apps get killed. This is indicated by the status switching from Success (all apps running) to Killing (apps getting killed) to None (all apps killed).
   - Started plan shall be killed first in order to be started again.

- **Restart Plan.** All apps from the current plan are first killed and then the plan starts.

- **Get Plan State.** Returns the status of one concrete plan.

- **Get All Plans State.** Returns the status of all plans known to Dirigent.

### Plan types

The plans in Dirigent can be used in several different ways

 1. **App-keeping plan**
    - To keep the contained apps up and running
    - All the apps contained in the plan are supposed to stay running for the whole life time of the plan (not Volatile)
    - Plan is successful if all apps are up and running.
    - Plan is failing if some of the apps failed to run, crashed, was killed using `KillApp` etc.

 2. **Utility plan**
    - To run some one-shot utility commands
    - The plan just run the apps (commands) and terminates. Often it sends a few commands to dirigent without caring whether the commands executed successfully or not.
    - All the apps needs to be marked Volatile
    - The plan Status does not indicate anything useful (no relation to the consequences of running those commands)

 3. **A combination of the two options above**
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

### Execution of a launch plan

The application from the plan are initially assigned the state 'not launched'.

The launch order of all apps form the plan is determined. The result is a sequence of so called launch waves. A wave contains applications whose dependencies have been satisfied by the previous launch wave. The first wave comprises apps that do not depend on anything else. In the next wave there are apps dependent on the apps from the previous wave.

The waves are launched sequentially one after another until all apps from all waves have been launched. 

If some application fails to start, dirigent can be configured to retry the launch attempt multiple times.

If all attempts fail, the launch plan is stopped and an error is returned.

### App dependencies

If an app is started as part of a plan, Dirigent will not start the app until all its dependencies (meaning some other apps) are satisfied.

'Satisfied' means that the all those other apps listed in the `dependencies`  attribute dependencies are already running and initialized.

### Multiple coexisting plans

Any of the plans can be selected and manipulated independently at any time.

### Apps in plans

Dirigent can work individually with each of applications found in any the plans from the plan repository. 

An app with same name can appear in multiple plans. In each plan it can be defined with different parameters. The parameters get applied when the app is being launched as part of the plan.

### Adopting apps

If a plan references an app that is already running (possibly started as part of a different plan executed before), the new plan does not try to start the app again or restart it. The app is left running with its original parameters. The new plan acts as if that app have been started by the plan.

The app parameters as defined in the new plan are remembered and are applied as soon as the app happens to be started again.


## Scripts

Dirigent can run user-provided C# scripts having access to all the capabilities of the Dirigent. 

A script can access all features of the dirigent (starting apps, plans etc...)

Available scripts are predefined in SharedConfig.xml.

### Script Operations

- **Start Script.** Starts given script.
- **Kill Script.** Kills given script.
- **Get Script State.** Returns the status of one concrete script.
- **Get All Script State.** Returns the status of all scripts known to Dirigent.

### Script Status

Script is responsible for  updating its status. The status is an arbitrary text string. Its meaning is script-specific. App that queries the script needs to understand the meaning.

For a script that is not running, a special text string `None` is returned.

## Management Operations

- **Set Vars.** Sets an environment variable(s) for the Dirigent process. They can be used for expansion in the applications' exe paths, command lines and other places.
- **Kill All.** Kills all running apps on all computers and stops all running plans. Kind of a 'central stop' feature.
- **Reload Shared Config.** Tries to reload the shared config containing the plan and app definitions.


### Dirigent.Agent

`Diregent.Agent.exe` can be running either as a background process with no user interface (the default for Linux) or as a GUI Windows Forms GUI application that shows an icon in a system tray (default for Windows).

### Dirigent.CLI Console Command Line Utility

 `Dirigent.CLI.exe` is specialized for sending commands to agents. It connects to the master via the TCP CLI interface and sends a command specified either on the command line.

    Dirigent.CLI.exe <command> <arg1> <arg2>; <command> <arg1>...

Zero exit code is returned on success, positive error code on failure.

For example:

    Dirigent.CLI.exe --masterIp 10.1.1.2 Start plan1; StartPlan plan2


Command can be also entered interactively from the console:

    Dirigent.CLI.exe --masterIp 10.1.1.2 --mode telnet

The commands are same as for the CLI TCP connection, see some examples below:

    StartPlan plan1
    
    StopPlan plan1
    
    KillPlan plan1
    
    RestartPlan plan1
    
    LaunchApp m1.a
     
    LaunchApp m1.b@plan1
    
    KillApp m2.b
    
    RestartApp m1.a
    
    GetAppState m1.a
    
    GetAllPlansState
    
    SetVars VAR1=VALUE1::VAR2=VALUE2::VAR3=VALUE3
    
    KillApps
    
    ReloadSharedConfig

Multiple commands on a single line can be separated by semicolon

    Dirigent.CLI.exe LaunchApp m1.a;StartPlan plan1

## CLI control over TCP line-based connection

Master is running a TCP server providing for controlling agents' operations.

To send a command you can user a generic TCP socket from your app. Or use `Dirigent.CLI.exe` app for testing:


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

  `GetPlanState <planName>`  returns the status of given plan

  `GetAllPlansState` ..... returns one line per plan; last line will be "END\n"



  `LaunchApp <appId>` ....... starts given app

  `LaunchApp <appId>@<planid>` ....... starts given app with the parameters as defined in given plan

  `KillApp <appId>` ......... kills given app

  `RestartApp <appId>` ...... restarts given app 

  `GetAppState <appName>`   returns the status of given app

  `GetAllAppsState` ...... returns one line per application; last line will be "END\n"





  `SetVars VAR=VALUE::VAR=VALUE` ...... sets environment variable(s) to be inherited by the processes launched afterwards (note that you can set multiple variables at once, separated by '::')

  `KillAll` ...... kills all running apps on all computers, stops all plans

  `ReloadSharedConfig` ...... tries to reload the shared config, optionally killing all apps before the reload





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


###### Reloading SharedConfig

  Request:   `ReloadSharedConfig

  Response:     `ACK`



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

 `--isMaster 0|1` .... run master component `Diregent.Agent.exe` only `--CLIPort 5050` ... Command Line Interface port number. Passed to the master process when `--IsMaster 1` is used.

 `--tickPeriod 500` ... Period in milliseconds of commands/plan processing & GUI refresh. Passed to the master process when `--IsMaster 1` is used.

 `--rootForRelativePaths` ... in what folder to look for processes that are specified by relative path. If not specified, the SharedConfig.xml file location is used (if defined). If neither the SharedConfig file is defined, the app's current working directory is used.

 `--guiAppExe` ... what app executable to run when the tray icon is double clicked; if not defined, the default winForms gui embedded in the agent executable is shown.

### Agent's local config file

Agent's local config file contains configuration that is specific for an agent (for example folder watching settings). Each agent can use its own local configuration.

Local configuration is stored in the `LocalConfig.xlm` file. The location of the file can be set through application option `localConfigFile`.

Local configuration file is optional.


### Shared config file

Shared configuration contains the settings that are same  for all agents, for example the start plan definitions. It is required for master only.

Shared configuration is stored in the `SharedConfig.xlm` file. The location of the file can be set through application option `sharedConfigFile`.

Shared config file is mandatory. Dirigent agent executable in master mode won't start without it.


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
  - `[dirigent.command]` - executes a dirigent command stored in `CmdLineArgs` attribute is if passed to Dirigent.CLI command line (but parsed and executed internally by the dirigent agent). Multiple commands can be entered, separated by a semicolon.  The commands are sent immediately over the network, Dirigent does not wait for their completion so this 'app' never enter the `Running` state and immediately goes to 'Terminated'. Please always mark this app record as Volatile so the plan does not expect the app to stay running .
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


### Utility Plans vs. standard plans

Usual plan "wants" to start all apps and watches if they are started. Such plan never ends automatically on its own even if all apps crash. If an app is set to be restarted automatically, the plan will do so until the plan gets stopped or killed.

Such the plan also can not be started again before it is manually stopped or killed.

An utility-plan it the one containing just volatile apps (having Volatile="1") is handled in a special way. Is is stopped automatically as soon as all the apps have been processed (an attempt to start them was performed) and all started apps have terminated (none left running).

Such volatile-only plan allows for being started again without prior stop or kill command.


### Autodetection of the machine id

Computer's NetBIOS name is used as a default machineId if the machine id is not specified on the command line.

### Logging

Both agent and master support logging of errors, warnings etc. into a log file through a Log4net library. The log file name as well as other options for logging (verbosity etc.) can be specified as a part of an app's .config file. 


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

The processes started by Dirigent inherit the environment variables of their parent process, i.e. the Dirigent agent itself.

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

### Keyboard Global Shortcuts

If a Dirigent's tray GUI is running on a machine, currently selected plan can be started/killed/restarted via a shortcut.

Also the current plan can be selected based on its order in the `SharedConfig.xml`.

Shortcut can be redefined in the `Dirigent.Agent.dll.config`. By default

    Start current plan ..... Control + Shift + Alt + S
    Kill current plan ...... Control + Shift + Alt + K
    Restart current plan ... Control + Shift + Alt + R
    Select 1st plan ........ Control + Shift + Alt + 1
    Select 2nd plan ........ Control + Shift + Alt + 2
    Select 3rd plan ........ Control + Shift + Alt + 3
    ...
    Select 9th plan ........ Control + Shift + Alt + 9

## Architecture

Each computer is running an agent process. One of the agents runs a master server component. Agents connect to a single master.

The master's role is to execute plan/script logic and to tell agents what apps to start/kill.

Agent's role is to manage individual applications. Agent manages the processes running locally on the same machine where the agent is running. Agent takes care of local application launching, killing, restarting and status monitoring. 

Agents publish the status of local applications to master which in turn spreads it to all other agents. The status include whether the app is running, whether it is already initialized etc.


![dirigent-internals](doc/DirigentInternalStructure.png)

