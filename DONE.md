[DONE] Default actions for File and FilePackage defined in LocalConfig DefaultFileActions and DefaultFilePackageActions sections. 

[DONE] New dirigent's message for emitting a baloon notifications. What message to show, on what machine, script & args to fire if notification clicked. 

[DONE] icons show tooltip saying "Image"

[DONE] Sharing the ClientState with Master. Disabled for now as sending it caused StackOverflof on deserialization of proto message on master when using many clients and huge SharedConfig.xml. Not sure what was the cause. Might be some timing/initialization issue related to protobuf deserialization??

[DONE] Add items to dirigent's tools menu via SharedConfigMenu

[DONE] Monitor CPU memory network stats on each station, show in Machine tab. Agents to send MachineState to master periodically.

[DONE] Monitor memory, cpu, usage per app. Add columns to the app grid.

[DONE] Let remote script itself evaluate %VAR% (on the hosting machine), do not evaluate it on machine where script call is made from.

[DONE] Exception inside SyncOp is not shown, silently ignored.

[DONE] Script able to run a tool preconfigured in dirigent, passing parameters & values to the tool.

[DONE] Pass app's process PID to the tools started in the context of an app. As an internal variable "APP_PID" evaluatable on the command line.

[DONE] Let the script started in the context of an app (from app's context menu) know the PID of app's process. As "APP_PID" variable.

[DONE] Extend possible impacts of the Select Plan operation in the GUI

 1. To choose what plan will be started when pressing the big green Play button. This affects jut the GUI when issuing the StartPlan command.
 2. To choose from what plan the the app definition will be taken if an individual app is started from the GUI. Here we need to remember the selected plan just inside the GUI when issuing the "LaunchApp xxx.yyy@plan" command when clicking on the "Start app" icon.
 3. To choose from what plan the the app definition will be taken when an external controller will start an individual app. Here we need to change the app definition in the Agent's memory so when a "LaunchApp xxx.yyy" command comes to the agent, it already remembers what app definition (from what plan) to use.

Options 1 and 2 are in place already. Option 3 is the question discussed below.

Selecting a plan from GUI could switch all apps from the plan into the plan's configuration, to be applied when the app is later started without specifiong the plan name (i.e. using the most recent plan applied to that app.) This is fine for development where there is only one Dirigent. But is it good for production?

In production there are many Dirigent GUIs on different computers, operated possibly by different users not knowing about each other. When they select & start a plan, the plan selection should not affect the system until the plan is actually started. So the users (or some Dirigent GUI startup script) can select the plan as needed for that particular user. But the selection should remain GUI-local until it is applied when actually starting a plan or a single app from the selected plan...

Selecting a plan on one GUI would switch the app's definition on agents to the ones from the selected plan for anyone who is going to start the app without specifying the plan (like via "LaunchApp m1.a", i.e. using the most recently applied plan). Do we want this?

Or should we keep the "Select Plan" operation strictly just a local state of that particular GUI, not affecting the agents (by applying the plan to the apps) until the plan is actually started?

Should we choose how the SelectPlan works on per-GUI basis (config option, 3-state checkbox etc?) Or should it be a plan's config new attribute? Probably all of these, with the 3-state checkbox having the highest priority (if not in the "default" state), then the plan's config attribute, then the agent-wide option...


Controlling the Dirigents from some background apps would work safely if it works either on Plan level (starting plans - this applies the app def from the plan when the app gets started by the plan) or using explicit plan names when working on the app level (lauching individual apps from a concrete plan). If it works on the app level without explicitly specifying a plan, it assumes the apps definitions have been somehow chosen before - usually coming from the default (standalone) app definitions in the SharedConfig.


[DONE] LaunchApp without specifying a plan should use app def from last used plan. If empty string ("LaunchApp m1.a@"), force the default appdef if defined.

[DONE] Terminate, Shutdown

[DONE] WebServer REST API on master for querying the defs/statuses, extended to allow firing commands

    GET /api/plandefs ... list of all plandefs [{'name':'plan1', 'appDefs':[...]}, {'name':'plan2', 'appDefs':[...]}]
    
    GET /api/plandefs/plan1 ... plandef of a single given plan {'name':'plan1', 'appDefs':[...]}
    
    GET /api/planstates ... list of the state of all plans [{'name':'plan1', 'status':{'code':'InProgress'}, {'name':'plan2', status={'code':'None'}}]
    
    GET /api/planstates/plan1 ... state of a single plan {'code':'InProgress'}
    
    POST /api/cli, data "StartApp m1.a" ... response on success: "ACK"; response on failure: "ERROR: xxxxxx"


[DONE] If an app is disabled, it will never be started so its appdef is never sent to an agent that will never change the Disabled flag in AppState.
We need to tell the agent about disabling the app for concrete plan.. Do we?? Why the agent should know? Agent does not need to know if app is disabled
because it affects just the master who process plans...


[DONE] PlanApplied neve comes from agent as it is set by master.
Who needs it? Just GUIs to indicate the "Planned" app status.
An app needs two parts of the state - one from agent, another one from master!
PlanApplied is master's flag. Migh not even be published! Who needs it?
AppState message should allow setting the flags partially. Both agent and master can change its part of the state. Master sends its part to guis (agent does not need to know master's flags)
Send "Plan applied" info from master as part of PlanState message? List all apps from the plan there at once? Better just incrementally, just those that changed...
App start attempted on agent as part of plan (plan name not empty) => Plan applied! Usually... Rather use explicit flag..
PlanApplied to AppDef in master's Plan.AppDefs? Ok if reset properly when starting the plan...
Why not take it ONLY from the AppState? PlanApplied = agent has attempted to start the app.
Add SetPlanApplied flag to StartApp command? Maybe it can be deduced from "AppDef.PlanName is not empty"?
What if an app gets started from two different plans? Each should have its own "PlanApplied flag... This migth be the best reason for splitting the flag to AppDef.

Shall we set "PlanApplied" if the app from the plan is started manually? Probably yes?

[DONE] ProcessPlanKilling: when the plan is finished killing (no app running anymore), we should reset app flags to stop indicating "killed" or "start failed".
We simply want neutral "not running" as at the very beginning where the app was never attempted to start.
Maybe add some flags to KillApp command indicating we want to reset the app status? Only the KillApp commands resulting from KillPlan...

[FIXED] we started plan2 with app m1.b (app went Planned), then we launched app m1.b from plan1 - agent still having appDef from plan2... Starting app from certain plan should use that plan name!
[FIXED] Should All-volatile plan report success if all volatile apps have been planApplied, started and initialized (does not need to run), none failed?
On the other hand, we want such an all-volatile plans to be startable again without killing it?
I.e. when StartPlan comes again when already reporting success, shall we start it again?
Was set to require explicit kill!

[FIXED] Messages from master to agent contain empty sender attribute (master's ident = empty string). For sending error messages back to the original requestor of the operation (to the GUI that invoked the operation), we need to pass the requestors id!

[DONE] ImGui: Little simple icons for ImGui (green triangle = play, red cross = kill)

[DONE] Dirigent.Gui project to produce Dirigent.Agent.exe with no console but with tray icon & gui & agent. Do not build Dirigent.Agent project for windows. Rename project "Gui.Winforms" to "Dirigent.Agent.Winforms"

[DONE] Dirigent.Gui to optionally integrate the Agent (no starting of agent executable). In such a case, GUI to show the agent's machine Id in its title. Default mode = trayApp, i.e. agent+tray icon. Provide message pump for SendKeys to work.

[DONE] ImGui: More space for app/plan name (move icons to 2/3 of width)

[DONE] Sending empty dictionary in a protobuf encoded message results in receiving null dictionary - check for null on receiving side! PlansState, plandefs, appdefs etc.

[FIXED] Exit in GUI does not kill the agent but crashes the Gui... If Gui started from agent, Exit kills the GUI but agent re-launches it.

[FIXED] StartupPlan not applied in Gui.WinForms, always starts without a plan selected

[BUG] Detect "offline" by time delat from last received update (don't rely on time sync across computers)

[BUG] Single instance checking not working, multiple instances can be opened

[BUG] Windows agent not showing machine name in tray icon until the GUI is first time opened


[BUG] CLI app not outputting anything to stdout

[TODO] CLI commands implementation: GetAllPlansState, GetAllAppsState etc...

[TODO] ImGui: Main menu - Reload SharedConfig, Kill All


[IDEA] Scripts on master, running indepedently on apps and plans. A script can be started/killed/restarted (separate set of dirigent commands). Script provides text string status.
SharedConfig
        <Script Name="Demo1" File="Scripts/DemoScript1.cs" Args="" />
CLI
        StartScript Demo1::argument string
        KillScript Demo1
        GetScriptState Demo1  ... returns string set by the script. Reserved values:
            "None" ... script is not running, not returning any value
Command line
        Run script at startup (can be used multiple times)
          cmd line arg --startupScript "Demo1::argument string"

[TODO] "Groups" attribute for scripts, apps, plans to allow presenting them in a tree view in GUIs. Ex.: Groups="Common/Demo;Examples".

[BUG] Apps keeps restarting after KillAll 

[IDEA] Run scripts asynchronously

* https://stackoverflow.com/questions/51218257/await-async-c-sharp-method-from-powershell
* What if we want to kill a long running operation?
  * Firstly we should prevent such operations from blocking out script. We need to be able to respond to a cancellation request. If long operation is needed, we wrap it inside a cancellable task that
    * starts the long task (in a thread)
    * periodically checks if the long task has finished
    * periodically checks  if cancelled; if so, it performs proper cleanup (kills the thread...)

[IDEA] App-bound tasks

For an AppDef there can be some Tasks  defined. The tasks show up in the app context menu. Such an app task is actually a scripts (built-in or user defined) getting the AppIdTuple as a parameter.

[IDEA] App-bound and machine-bound tools

[IDEA] Async script execution. Synchronize with Dirigent on calling its API

[DONE] Mitigate the false process exited report (reported too early when the process is still running). Introduce a new process setting MinKillingTime="1.5" to postpone reporting of the "killed" status after the kill request. Applies only if the OS reports the death of the process within this given time since the kill request.

 # Quick access to app files and folders.

* In the AppDef define where the app file(s) for each app are located - multiple per app; also app-specific folders. Add "Show files" command to app's context menu, listing the defined files, opening them via a network share using default associated app.
* [IDEA] Allow for remote file access across dirigent-equipped stations. Get the machine IP address from client's connection.  Allow to define file share name per machine (use "C", "D" etc. as defaults). Add "Folders" to context menu in client tab, listing all predefined folders on the machines, the C root always.
* [IDEA] In sharedConfig Define file packages, allow to download them easily. Files get zipped on their local machines to a temp folder, UNC paths to them are offered. Or they are downloaded to the Downloads folder (one per machine), repackaged to one single archive and the folder is opened in file explorer.
  * Package can contains individual files, folders with file mask (using the glob library https://github.com/kthompson/glob)

  * Package content can be defined by a script? Same for individual files, folders...


* [IDEA] Files tab showing all the files defined. Allows viewing given file by opening the viewer - independent app accessing the file via its UNC path. Allows downloading the file (zipped).

* [IDEA] File Packages tab showing all the file packages defined. Allows downloading the packages. Grid is foldable [+], showing individual files within the package.
* From the package a tree of concrete local/UNC paths and virtual folders is created. From this tree a context menu can be generated, or it cane be used to generate def file for VirtualFolders in a file manager.
* [IDEA] Bundle Dirigent with Double Commander. Call Double Commander from task scripts for file operations like viewing, editing, maybe also copying and packing.  Use VirtualFolders plugin for working with files inside Dirigent's file packages.

## File Packages

* A tree of files/folders available for some operation like downloading, browsing/viewing/editing etc.
* Represents a virtual file system (VFS) containing items in a tree structure similar to the real filesystem
  * References to physical files

  * Virtual folders (= nested file packages)

* The content can be generated by a script.
  * The script returns a tree of items.
  * Script can for example
    * Find the most recently created file in a folder
    * Filter the real files by specific criteria
* The package content needs to be refreshed (regenerated) before use to match the current status of the filesystem
  * Refresh is made right before using the package, for example just before running a local tool
  * The result is a tree of VFS items

### Running a tool from client

* This is an async job running on the client, performing the following operations
  * Refresh of the package
    * Can be done locally if the full package definition is available on the client. That means the file system operations with filesystem on remote machines are made via SMB file sharing.
    * Can be also performed using some kind of distributed tasks, locally on each machine where the files reside. Much more complicated, might be worth the effort only in cases where the SMB sharing would be too slow - for example thousands of files in a folder etc.
  * Dump the package VFS tree into a temporary file
  * Start a local tool, passing it the VFS dump file

## Distributing the File & Package info

* Full definitions of the files/folders and packages are loaded from SharedConfig by the master.
* They are extracted from all different places like Apps, Machines and put to a global list of individual files/folders and packages.
* The definitions are published to all clients, unresolved. Meaning that each clients knows all defs, but needs to resolve them before using.

# Async Scripts

Script is a class having a Run() method returning a result value. The script runs asynchronously. When the Run method ends, the script ends.

A script can call dirigent's API using await.

The script can be cancelled but not forcefully killed. Cancellation can happen within the call to dirigent's API. In other places the cancellation needs to be supported by actively checking the cancellation token.

### Tracking script status

Script can be in one of the following states Starting, Running, Finished, Failed, Cancelling, Cancelled.

Script status is described by a triplet 1. status code (see the states above), 2. status text 3. status user data (arbitrary serializable data struct).

The script result (the value returned from Run method) is automatically saved to the status user data once the script successfully finishes. If the script fails (throws an exception), the status text contains the reason code (for example "Exception") and the status user data contains exception details.

Script can update its status info (status text and status user data) at any time during its Running phase.

The status of the script is published to all other nodes whenever the status changes.

Each node keeps track of the status of all scripts running on any node. Once the script finishes, its final status is kept in memory for a while before removal to be available for whoever is polling it.

### Singleton scripts

Scripts that are permanently available to the user to run, presented in a menu. Such scripts are defined in shared config.

There can be up to one single instance of each of these scripts, always having same GUID as defined in the shared config.

The script can be run on any dirigent node named in the script definition (agent, master, GUI...)

### Further implementation details

Calls to dirigent API are dispatched to dirigent's main thread so the script  block until the API call gets executed in dirigent's main thread.

## Remote script calls

Scripts can be started on any node like client, agent or master.

Script start request carries the client id where to run the script, the GUID of the upcoming script instance, script name (optionally also script code) and arguments.

Script code (if not provided in the start request) is loaded from a script library. The library contains built-in scripts (hardcoded within dirigent) as well as the script files found in dirigent's script folder.

Dirigent's async API includes running a script on given node and waiting for it to finish.

`Task<TResult> result = await RunScriptAndWait<TArgs,TResult>( scriptName: "scripts/myscrip1", args: "myarg", timeout: 20);`

If script finishes successfully, its result is returned.

If the script executions fails, the exception that happened in the script will be re-thrown locally.

# GUI async actions

Some actions need to be performed on multiple machines different than the one from where the request comes from.

GUI action is an async method.

It can call async dirigent API, including starting scripts & waiting for their termination. GUI action can then easily using the result returned from the script for some local UI operation.

