# Shared Config

### Shared config file

Shared configuration contains the settings that are same  for all agents, for example the start plan definitions. It is required for master only.

Shared configuration is stored in the `SharedConfig.xlm` file. The location of the file can be set through application option `sharedConfigFile`.

Shared config file is mandatory. Dirigent agent executable in master mode won't start without it.

#### Basic structure

The file defines

* Standalone apps
* Plans and apps inside them
* Scripts

```
<Shared>
    <App ... />
    <App ... />

    <Script ... />
    <Script ... />    

    <Plan ... >
        <App ... />
        <App ... />
    <Plan/>  

    <Plan ... >
        <App ... />
        <App ... />
    <Plan/>
    <KillAll/>
</Shared>
```

#### App definitions

##### `<App/>` element

Example:

```xml
<App
     AppIdTuple = "m1.a"
     ExeFullPath = "c:\windows\notepad.exe"
     StartupDir = "c:\"
     CmdLineArgs = "C:\file1.txt"
/>
```



Attributes:

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
    Example: `ExeFullPath = "[dirigent.command]" CmdLineArgs = "StartApp m1.a; KillPlan plan2" Volatile="1"`

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

- `MinKillingTime <numseconds>` - minimal time since the kill operation to wait before reporting "killed". This avoids reporting the process death too early after the kill, when the process might be still running (mitigates the false/too early process death report provided by Process.hasExited).

App sub-sections:

- `SoftKill`

  ```xml
  <SoftKill>
     <Keys Timeout="1.5" Keys="^(c)"/>
     <Close Timeout="0.7"/>
  </SoftKill>
  ```

  Defines a a sequence of "soft" attempts to terminate a process. Dirigent try to terminate the process using the actions from the sequence, starting with the first one defined.

  If the action fail (the process is not terminated within defined timeout), next action (presumably more severe) is tried.

  Only if all actions fail, the process is killed in the hard way.

  If an extra Kill command is issued while the process is being attempted to be terminated in the soft way, the process is killed immediately in the hard way (impatient kill.)

  Note: The *KillTree* option in NOT applied if Dirigent succeeded to terminate the process in one of the soft ways.

  Sub-sections:

  - `Keys` - send one or more keys to the main window. See Window.Forms.SendKeys manual for the key name format. 
  
  - `Close` - emulates the close command sent to the main window.


- `ReusePrevVars 0|1` - controls whether to reuse cached environment variables used last time when launching the app again. 0 by default.

  * 1 = The cached env. variables  (used for previous launch of this app) are applied again if no vars are explicitly specified in the `StartApp` command.
  * 0 = The cached env. variables are unset before launching the app. The app will NOT inherit any variables from it's previous launches, will always start with clean environment.

- `LeaveRunningWithPrevVars 0|1` - controls how to handle situation when the app is already running but with different set of environment variables. 0 by default.

  - 0 =  `StartApp` command will **restart** the already running app if it was started with different set of env vars.

  - 1 =  `StartApp` command will keep the already running app intact (**no restart**) even if it was launched with different set of env vars. But Dirigent will remember the new variables and will use them the next time this apps is started.

    

- `WindowPos`

  ```xml
  <WindowPos TitleRegExp="\s-\sNotepad" Rect="10,50,300,200" Screen="1" Keep="0" /> 
  ```

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

  ```xml
  <Env>
    <Set Variable="TMP" Value="C:\TEMP" />
    <Set Variable="TEMP" Value="C:\TEMP" />
    <Path Prepend="C:\MYPATH1" Append="C:\MYPATH2;..\sub1"/> 
    <Local Variable="P1" Value="myLocalParam1" />
  </Env>
  ```

  Modifies the environment variables for the started process, taking the Dirigent Agent's startup environment as a basis.

  Existing environment variables can be set to a new value. Non-existing will be created, existing will be overwritten.

  Specific support for PATH variable allows prepending or appending given string to PATH.

  Attributes:

  - `Set` - set given variable to a new value. Both attributes `Variable` and `Name` are mandatory. Environment variables in form of %VARNAME% contained in the Value are expanded using Agent's current environment.
  - `Path` - if attribute `Prepend` is present, prepends its value at the beginning of the PATH variable. if attribute `Append` is present, appends its value at the end of the PATH variable. Environment variables contained in the `Prepend` or `Append` attribute values in form of %VARNAME% are expanded using Agent's current environment. Relative paths are considered relative to the location of the shared config file and are converted to absolute paths.
  - `Local` - set Dirigent's internal variable to given value. The variable can be used for expansion inside process exe path and command line similarly as the env vars but is not propagated to the process environment.

- `Restarter`

  ```xml
  <Restarter maxTries="2" delay="5"/>
  ```

  This settings specifies the parameters of the restart service which is activated upon application crash (when the app terminates without being killed via Dirigent). Such service is enabled only if  `RestartOnCrash='1'`.

  Dirigent will try to restart a crashed app for given number of times before it gives up. Upon crash, Dirigent waits for specified time before a restart attempt is made.

  Attributes:
  
  - `maxTries` - how many restart attempts are made before the Dirigent gives up restarting. -1 means 'try forever'. Default is -1.
  - `delay` - how long time in seconds to wait before the Dirigent attempts to restart a crashed app. Default is 1 sec.

  Upon an `StartPlan` or `StartApp` request the number of remaining restart attempts is reset to the `maxTries` value.
  
  `KillApp` or `KillPlan` requests deactivate any pending restart operation.

#### Standalone app definitions

Applications can be defined either as part of a plan (see below) or as a "standalone" `<App/>` elements outside of a plan.

The standalone ones are useful in cases like

1. The app does not belong to any plan
2. The app within the plan uses different settings than the standalone app

Standalone apps definition is used by the Dirigent if

* If the app is not part of any plan but is defined as standalone
* The app is started with explicitly specified empty plan (`StartApp m1.a@`)


#### Launch plan

Launch plan comprises just a list of apps to be launched in given order. Multiple parallel plans can be active at a time.

##### `<Plan/>` element

Example

```xml
<Plan Name="plan1" StartTimeout="10">
```

Attributes

* `Name` - unique id of the plan
* `StartTimeout` - time in seconds before an unsuccessfully running plan is reported as *Failed*. Unsuccessful means that
  * Non-volatile apps (that should be running all the time) is not running or has not initialized yet.
  * Volatile apps have not yet been started, initialized or finished.
* `Groups` - a semicolon separated list of group names the plan belongs to
  * Some GUIs support sorting the plans to the groups
* `ApplyOnStart` - the app definition for all of the apps from the plan are updated to the ones from this plan when the plan gets started/restarted.
* `ApplyOnSelect` - the app definition for all of the apps from the plan are updated to the ones from this plan as soon as the plan is selected on the GUI, even before it gets started. This makes sure that after selecting the plan in the GUI the apps will be started with plan's settings even when starting them individually and without explicitly specifying the plan.


##### <App/>

Define what apps belong to the plan. Located inside the plan definition like in the following example

App element have same format and meaning as the "standalone" `<App/>` elements defined outside of a plan.

#### Script definitions

##### `<Script/>` element

Example:

```xml
<Script Name="Demo1" File="Scripts/DemoScript1.cs" Args="" />
```

 Script element has the following attributes:

- `Name` - unique text id of the script instance;

- `File` - script's file path; can be relative to the Dirigent's shared config file location (or CWD if none defined). Environment variables in form of %VARNAME% are expanded using Agent's current environment.

- `Args` - command line arguments string passed to the script; available via the `Args` member variable of the script class.

#### KillAll definitions

This optional section lists the action taken when a [KillAll](CLI.md#KillAll) command is issued, in addition to killing all running apps and plans.

##### `<KillAll/>` element

Example:

```xml
<KillAll>
    <!-- HostId is mandatory. The tool needs to be defined in LocalConfig.xml on that host. -->
    <Tool HostId="m1" Name="BatchScript" Args="start /min kill-all-action-1.bat"/>
</KillAll>

```

Available sub-elements

* Tool (see [Tools](Tools.md))
* Script (see [Scripts](#Script-definitions))

Remarks:

The `HostId` attribute needs to specify the machine where to run the tool/script on.

In case of a `<Tool/>` the tool needs to be defined in LocalConfig.xml on that host.

