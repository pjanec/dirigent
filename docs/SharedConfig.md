# Shared Config

### Shared config file

Shared configuration contains the settings that are same  for all agents, for example the start plan definitions. It is required for master only.

Shared configuration is stored in the `SharedConfig.xlm` file. The location of the file can be set through application option `sharedConfigFile`.

Shared config file is mandatory. Dirigent agent executable in master mode won't start without it.

#### When the file is read

**Once, by the master, at startup - and then held in memory.** Editing the file changes nothing by
itself: until the master reads it again, a plan you have just added does not exist as far as Dirigent
is concerned, and `GetAllPlansState` will keep answering with the plans it started with. That is the
answer to "my edit had no effect" and to a plan count that disagrees with the file.

To make an edit take effect, either

* run [`ReloadSharedConfig`](CLI.md#ReloadSharedConfig) - from the CLI, the GUI or `POST /cli` - which
  re-reads the file and hands the new definitions to the agents, no restart anywhere; or
* restart the master.

A reload picks up everything this file defines: plans, application definitions, machines and their
shares, file and package nodes, scripts, menus. `killApps=0` leaves running applications alone, and a
changed application definition then applies the next time that application is started.

**It does not read `LocalConfig.xml`.** Despite the name covering "the config", each agent reads its
own local file once when *that agent* starts, so `<Tool>` definitions, `<FolderWatcher>` entries and
the `DefaultFileActions` / `DefaultFilePackageActions` blocks need the **agent** restarted on the
machine concerned - see [Local Config](LocalConfig.md#when-the-file-is-read). A new tool action not
appearing in a menu after a reload is this, not a mistake in the file.

#### A trap when commenting the file

**An XML comment may not contain `--`.** That is plain XML rather than anything of Dirigent's, but
this file invites it: a header comment explaining the command line options, or a note about
`--machineId`, makes the whole file unparseable, and the master then refuses to start over what looks
like a perfectly ordinary comment. Write the option without its dashes, or as `<Option>machineId`, or
put it in an attribute value where dashes are fine.

#### Basic structure

The file defines

* Standalone apps
* Plans and apps inside them
* Scripts
* `<AppTemplate>` elements - default settings for apps, named by the `Template` attribute of an app (or of another template)
* `<FileTemplate>` elements - default attributes for file nodes (`<File>`, `<Folder>`, `<VFolder>`, `<FilePackage>`, `<FileRef>`), named by their `Template` attribute; see [File templates](Files.md#file-templates)

```
<Shared>
    <App ... />
    <App ... />

    <Script ... />
    <Script ... />    

    <Plan ... >
        <App ... />
        <App ... />
    </Plan>

    <Plan ... >
        <App ... />
        <App ... />
    </Plan>
    <KillAll/>
</Shared>
```

##### `Name`

`<Shared Name="Lab 2">` names the site - optional, free text. It is what `GET /api/status` and `GET /api` answer as `siteName`,
and what the cover note (`_comment.txt`) of a collected archive starts with, so that an archive attached to a bug report says where it
comes from.

##### `MinDirigentVersion`

`<Shared MinDirigentVersion="3.1.19">` states the oldest Dirigent this file may be used with. A master
older than that refuses to load it, with `This SharedConfig needs Dirigent 3.1.19 or newer; this is 3.1.18.7.`
Versions are compared number by number (a missing number is 0); a value that is not dotted numbers is a load error
naming it. A build without a release version (a development build, which reports 1.0.0.0) is not checked.

Why: an older master does not fail on what it does not know. It would quietly ignore a `<FileTemplate>` and
collect whole folders where the config asked for the newest few files. Put the attribute in any file that
uses a feature newer than the oldest master on the site.

#### Machine definitions

##### `<Machine/>` element

Declaring machines is optional; an agent can connect without being declared. The element is the place for a machine's file shares and file nodes (see [Files](Files.md)) and for its addresses.

| Attribute | Meaning |
| --- | --- |
| `Name` | The machine id, the same as the agent's. |
| `IP` | Overrides the address the master determined from the agent's connection; leave empty normally. |
| `MAC` | MAC address used for Wake-on-LAN, e.g. `AA-BB-CC-DD-EE-FF` (separators `-`, `:` or `.`, or none). Optional. |

The `MAC` attribute is an **override**, not a requirement. Agents report their network adapters (MAC, IP address, subnet) to the master whenever they connect, and the master keeps them in `MachineMacs.json` in the agent status folder (`%LocalAppData%\Dirigent` unless `agentStatusFolder` says otherwise), so a machine that has connected to the master once can be woken up without any configuration. Set `MAC` only for a machine that has never connected, or that should be woken by another adapter than the one it reports. When it is set, it is the only MAC woken; the subnets the machine was last seen in are still addressed.

```
<Machine Name="m1" MAC="AA-BB-CC-DD-EE-FF" />
```

Waking is done from the GUI (Machines tab, right click, Power, Wake Up - offered for machines that are not connected) or with the [WakeUp](CLI.md#WakeUp) command. The master sends the packets, from every network interface it has.

##### `<MachineTemplate/>` element

On a large site most machines share the same `<Share>`s and the same machine-wide log and configuration nodes. They are written once
in a `<MachineTemplate>` and used with `Template=` on each `<Machine>`:

<!-- example: loads -->
```xml
<FileTemplate Name="log.dirigent" Id="log.dirigent" Title="Log/Dirigent agent" Path="C:\Dirigent\logs" Mask="*.log"/>

<MachineTemplate Name="m.base">
    <Share Name="C" Path="C:\"/>
    <Folder Template="log.dirigent"/>
    <File Id="cfg" Title="Config" Path="C:\site\cfg.xml"/>
</MachineTemplate>
<MachineTemplate Name="m.ios" Template="m.base">      <!-- a template can name a parent -->
    <File Id="ios" Title="IOS log" Path="C:\ios\ios.log"/>
</MachineTemplate>
<MachineTemplate Name="m.viewer">
    <File Id="viewer" Title="Viewer log" Path="C:\viewer\viewer.log"/>
</MachineTemplate>

<Machine Name="ios01" Template="m.ios"/>
<Machine Name="ios02" Template="m.ios;m.viewer"/>        <!-- several templates, applied left to right -->
<Machine Name="ios03" Template="m.ios">                <!-- and the machine's own, which win -->
    <Share Name="C" Path="D:\"/>
    <Folder Template="log.dirigent" Title="Log/Dirigent agent and MASTER"/>
    <File Id="own" Title="Own" Path="C:\own.txt"/>
</Machine>
```

* **Attributes.** The attributes of a template (except `Name` and `Template`) are defaults for the machine; the machine's own attributes
  win, as do the ones of a later template over an earlier. `IP` and `Guid` are not allowed on a template - every machine would share them.
* **Children.** Everything a `<Machine>` accepts - `<Share>`, file nodes, `<Tool>`, `<Script>` - is copied into the machine in front of its own
  children, in the order: the parent template, the template, the next template of the list, the machine itself.
* **Shares.** A `<Share>` with the same `Name` replaces the earlier one in its place (the machine's `C` above, not the template's).
* **Nodes.** The replace rule applies:

**The replace rule** (the same for apps and for machines): a file definition declared later - in a derived template, a later mixin, or in the
`<App>` / `<Machine>` itself - with the **same `Id`** as an inherited one **replaces** it, in the inherited definition's place; any attribute,
the `Title` included, may differ. A new `Id` is added. A definition without an `Id` is always added. Nothing else is compared.

Two file definitions with the same `Id` written directly on **one level** - in one `<AppTemplate>`, one `<App>`, one `<MachineTemplate>`, one
`<Machine>` - are a **load error**: `Duplicate file definition Id 'log' in AppTemplate 'apps.IOS'`. (The `Id`s are taken after the
`<FileTemplate>`s are expanded, so one that comes from a file template counts; the children of a `<FilePackage>` or `<VFolder>` are not
covered.) So after merging, an app or a machine has one definition per `Id`; see [Ids: one per definition](Files.md#Ids-one-per-definition-kinds-by-prefix).

  In the example `ios03` ends up with *one* `log.dirigent` node, titled `Log/Dirigent agent and MASTER`, in the template's place.
* **Registry.** Only the final list of a machine is ever registered, so a replaced node is not found by a `<FileRef>` or listed anywhere.
  The nodes of a template are ordinary machine nodes of each machine that uses it: `<FileRef Id="cfg" MachineId="ios01"/>` finds them.
* **Load errors,** each naming the template or machine: two definitions with one `Id` on one level (see the rule), unknown template (`MachineTemplate 'x' not found`), a cycle through `Template=` of a template or through the
  mixins (`MachineTemplate cycle: a -> b -> a`), a template without a `Name`, two with the same `Name`, an `IP` or a `Guid` on a template.
* A `<Machine>` without `Template=` is read exactly as before, and `<MachineTemplate>` elements are never machines themselves.

See also [File templates](Files.md#File-templates) for the attributes repeated on the nodes.

#### App definitions

##### `<App/>` element

Example:

<!-- example: loads -->
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

- `Component` - the software component the application belongs to (`Viewer`); optional, inherited from the template (the application's own wins). Tools group versions and collected files by it: the API answers it with the application, its location and each of its files - see [Components and groups for tools](Files.md#components-and-groups-for-tools). Not a UI grouping, and changing it restarts nothing.

- `Groups` - `;`-separated group paths, accumulated through the templates; grouping for people - the folders of the GUI's application tree.

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

    Such an app has no process, so it counts as terminated with exit code 0 the moment it is asked - which means `InitCondition="exitcode 0"` on it is satisfied at once, whatever became of the command. To make a plan *wait* for the command and act on how it went, use `InitCondition="cliresponse ok"` (or `any`); see [`cliresponse`](#cliresponse---waiting-for-a-dirigent-command) below and [Running a script as a step of a plan](ScriptsInPlans.md).

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

- `Template` - where to load default settings from; the name of a AppTemplate section in the same XML file. Templates may chain (an `<AppTemplate>` with its own `Template`); a template that leads back to itself (`a -> b -> a`) is a load error.

- `KillTree 0|1` - whether to kill not just the single process but also all its child processes. Child processes are killed only in case of a hard kill if previous "softer" attempts (if any, see KillSeq) fail.

- `KillSoftly 0|1` - whether to send the close command (as if user pressed the close button) instead of a forceful kill. Note this is implemented using the *SoftKill* mechanism described below with timeout=10secs. **DEPRECATED**, use the `SoftKill` section instead

- `SeparationInterval <numseconds>` - how much time to wait before starting the next application

- `MinKillingTime <numseconds>` - minimal time since the kill operation to wait before reporting "killed". This avoids reporting the process death too early after the kill, when the process might be still running (mitigates the false/too early process death report provided by Process.hasExited).

App sub-sections:

- `SoftKill`

<!-- example: loads -->
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

    
- `DeElevate 0|1` - controls whether the process will be run as non-elevated even if the Dirigent itself is elevated.
  - 0 = Run with same elevation as the dirigent. (default)
  - 1 = Run non-elevated. Only works if Dirigent itself runs with elevation. 
  * WARNING! Launching in non-elevated mode works like if the process exe was started from the desktop shell, i.e.:
    * Process is opened in normal window (ignoring window state settings like Minimized, Hidden...)
    * Process does not get any extra env vars provided by the dirigent (ignoring the ENV section from shared config, not setting DIRIGENT_xxx vars etc.)

- `WindowPos`

<!-- example: loads -->
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

  The detectors available are `TimeOut` (seconds), `ExitCode` (a list or ranges of codes, e.g. `0`, `0,2`, `0-7`), `WindowPoppedUp` (Windows only) and `cliresponse` - see below.

  Note on `ExitCode`: a range is expanded into one entry per value, so `0-255` is fine while something like `0-2147483647` would try to build a list of two billion. Name the codes you mean.

##### `cliresponse` - waiting for a dirigent command

For an app whose `ExeFullPath` is `[dirigent.command]`, this detector holds the app *not initialized*
until the master has answered the command line the app sent. Since a plan launches an app only once
everything it depends on is initialized, such a step makes the rest of the plan wait for the command.

The value is mandatory:

| | |
| --- | --- |
| `cliresponse ok` | initialized once the answer arrives and **every** command of the line succeeded. A failure leaves the step uninitialized, so its dependants never start and the plan reports a failure. |
| `cliresponse any` | initialized once the answer arrives, whatever it says. The command is waited for, a failure is logged, and the plan carries on. |

<!-- example: loads -->
```xml
<App AppIdTuple = "master.mark_logs"
     ExeFullPath = "[dirigent.command]"
     CmdLineArgs = "StartScript 7B3C1E90-1111-2222-3333-444455556666 BuiltIns/MarkFiles.cs '{Node:{Id:''pkg.run''}}' ; WaitForScript 7B3C1E90-1111-2222-3333-444455556666 timeout=300"
     Volatile = "1"
     InitCondition = "cliresponse any"
/>
```

Notes:

* it is refused at config load time on an app that is not `[dirigent.command]`, or without a value - such a condition could never be satisfied and would hold the plan for ever;
* a command that answers as soon as it is accepted (most of them) makes this an immediate initialization; the waiting comes from the command, e.g. [`WaitForScript`](CLI.md#waitforscript), which answers only when the script is over;
* nothing bounds the waiting by itself. Where a ceiling is wanted, combine the detectors - the first one satisfied wins:

<!-- example: loads -->
  ```xml
  <InitDetectors>
      <cliresponse>any</cliresponse>
      <TimeOut>60</TimeOut>
  </InitDetectors>
  ```

- `Env`

<!-- example: loads -->
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

<!-- example: loads -->
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

##### AppTemplate: replacing inherited nodes

An app, or a derived `<AppTemplate>`, inherits the `<File>`, `<Folder>`, ... nodes of its template. A node it declares replaces an
inherited one by the rule below, instead of being added as a second node. This lets a template say "a log node" once and one app
say "but for me it is 50 files".

**The replace rule** (the same for apps and for machines): a file definition declared later - in a derived template, a later mixin, or in the
`<App>` / `<Machine>` itself - with the **same `Id`** as an inherited one **replaces** it, in the inherited definition's place; any attribute,
the `Title` included, may differ. A new `Id` is added. A definition without an `Id` is always added. Nothing else is compared.

Two file definitions with the same `Id` written directly on **one level** - in one `<AppTemplate>`, one `<App>`, one `<MachineTemplate>`, one
`<Machine>` - are a **load error**: `Duplicate file definition Id 'log' in AppTemplate 'apps.IOS'`. (The `Id`s are taken after the
`<FileTemplate>`s are expanded, so one that comes from a file template counts; the children of a `<FilePackage>` or `<VFolder>` are not
covered.) So after merging, an app or a machine has one definition per `Id`; see [Ids: one per definition](Files.md#Ids-one-per-definition-kinds-by-prefix).

(Before 3.1.19 such a node was added as a second one, and the menu showed two.)

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
* `MarkFiles`, `ClearFiles` - semicolon separated lists of file definition ids (wildcards allowed) whose clearable files are marked, or cleared, when the plan starts, before the first app is launched. An id that matches no file definition is a load error. See [Marking or clearing files when a plan starts](Plans.md#marking-or-clearing-files-when-a-plan-starts).
* `MarkTimeout` - seconds the marking may take before it is stopped and the apps are launched anyway, 60 by default.
* `MarkInterval` - seconds between the checkpoints the plan takes of its `MarkFiles` while it runs (the file lengths recorded, the
  mark left alone), so that a collection "since a time" can start near that time; none by default, 10 at least, needs `MarkFiles`.
  See [Checkpoints](Plans.md#checkpoints-collecting-since-a-time).
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

<!-- example: loads -->
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

<!-- example: loads -->
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

