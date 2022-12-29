[BUG] Exception inside SyncOp is not shown, silently ignored.

[IDEA] Implement some tools as standard part of the Dirigent, always available. 

[IDEA] Add json viewer as one of internal tools? Or better use Notepad++?

[DONE] Script able to run a tool preconfigured in dirigent, passing parameters & values to the tool.

[DONE] Pass app's process PID to the tools started in the context of an app. As an internal variable "APP_PID" evaluatable on the command line.

[DONE] Let the script started in the context of an app (from app's context menu) know the PID of app's process. As "APP_PID" variable.

[BUG] FileDef menu containing actions overwrites existing menu folder with same name (Shared Config [FileDef] -> Actions  overwrites Shared Config -> Reload)

[BUG] FolderDef, when including content, fails with exception if one of the subfolders is inaccessible

[DONE] Let remote script itself evaluate %VAR% (on the hosting machine), do not evaluate it on machine where script call is made from.

[TODO] Use Glob nuget for filtering file names. Stop using extra "Mask" attribute.

[DONE] Monitor CPU memory network stats on each station, show in Machine tab. Agents to send MachineState to master periodically.

[DONE] Monitor memory, cpu, usage per app. Add columns to the app grid.

[TODO] Script libraries - initialized form built-ins as well as by scanning script files.

[BUG] icons show tooltip saying "Image"

[DONE] Add items to dirigent's tools menu via SharedConfigMenu

[IDEA] Add "Install OpenSSH server" to Tools menu, setting up the SSH server on each computer where Dirigent is running. Define a Script in shared config that runs the powershell to download necessary files, distribute to machines etc. Define similar script to enable powershell remoting on all the machines. 

[IDEA] Background scripts, defined per machine, automatically started when machine's agent gets SharedConfig (and killed when master sends Reset before sending new defs). Can periodically check for something to happen and emit notification baloon messages.

[IDEA] New dirigent's message for emitting a baloon notifications. What message to show, on what machine, script & args to fire if notification clicked. 

[IDEA] IconFile attribute for shared config records like File, Folder, Package etc.

[IDEA] VfsNodeDef.Title using "\\" characters to create menu tree. Helps sorting multiple items of similar type into groups shown as submenus. 

[IDEA] Show "service" icons in machines tab - take inspiration from Remoter. Services types configurable, services per machine defined in machineDef section of shared config; show icons for various service types. Available from any agent to any other (assuming the local network, no ssh)

[IDEA] Connecting with dirigent GUI client to a master using SSH port forwarding.

* Use port forwarding also for direct access to individual machine services. (as Remoter is doing).
* Check why Dirigent TCP comm fails to go through the SSH gateway.
* Try tunelling SMB file sharing over SSH - that would allow for remote file access to any computer
  * See https://sites.google.com/site/sbobovyc/home/windows-guides/tunnel-samba-over-ssh
  * For each computer behind a gateway we need specific local port to address port 139 on given computer
  * Requires installing Microsoft Loopback Adapter for each computer (to have one special IP per computer).
  * Or we could 
  * 

* https://github.com/variar/klogg/releases/download/v22.06/klogg-22.06.0.1289-Win-x64-Qt5-setup.exe
* 

[IDEA] Powershell scripts in addition to C# script.

* Inherit from Dirigent's Script class in same way as C# script do?
  * https://stackoverflow.com/questions/64485424/net-types-in-powershell-classes
  * https://stackoverflow.com/questions/65134626/inheritance-from-net-class-in-powershell
  * https://stackoverflow.com/questions/51218257/await-async-c-sharp-method-from-powershell
* Steps
  * Create runspace instance
  * Create powershell instance, link with runspace, feed with script creating the pwsh class with methods and storing the instance of it to a variable, Invoke()
  * Run async 2 independent methods:
     - Create powershell instance, link with runspace, feed with script calling the method on the class instance variable, BeginInvoke() to run asynchronously
     - Create another powershell instance, link to same runspace, run another script calling another mathod of that class instance, BeginInvoke() to run asynchronously

[TODO] In App Properties window show also the actual startup info used for starting the app last time. Add new message sent from client once when app is launched, broadcast to all, cache on each client.

[TODO] Remember grid column widths.

[IDEA] Single selected plan from all GUIs (optional). Dirigent could be configured to distribute the Selected Plan to all its GUIs, meaning all GUIS will share the same selected plan. This might be useful for example for development purposes with multiple computers.

[DONE] Sharing the ClientState with Master. Disabled for now as sending it caused StackOverflof on deserialization of proto message on master when using many clients and huge SharedConfig.xml. Not sure what was the cause. Might be some timing/initialization issue related to protobuf deserialization??

[IDEA] Tell the master about selecting a plan in the GUI using a new message PlanSelected. Master to update the app definitions to those from the plan (only if enabled, either by dirigent global setting, or individual plan setting...)

[BUG] "Collection modified" sometimes appear as notification baloon.

[TODO] HTTP port as a command line argument.
[TODO] bind web server to any interface (this is probably working already...)
[BUG] Publishing of ClientStateMessage by the agent each frame causes stack overflow on deserialization in master in case of SharedConfig.xml.HUGE. Without the agents publishing ClientStateMessage it works... It fails only for message sent from an agent running in a separate process. Same message from an agent embedded with the master in Dirigent.Agent.exe does not cause this problem. Fortunately sending the ClientStateMessage from the agent is not necessary for giving just Connected/Disconnected feedback so it was removed.

[TODO] Show RemoteOpErrors on ImGui always on top of the main app window, even if the content is scrolled down

[BUG] batch file app started within agent's console - shall be run in its own window!

[BUG] Agent is now using SendKeysWait (as SendKeys requires a msg pump). Will probably stuck on unresponsive app. Run it in a thread?

[TODO] FolderWatcher from trayapp to Agent

[BUG] ReloadSharedConfig does not change the appdef (changed cmdLineArgs, tested in linux version)

[TODO] Unselect app from plan also as CLI operation. Unselected app not affected by start/kill/restart plan. Starting unselected app uses it's default configuration.

[TODO] SetLocalAppsToMaxRestartTries( rti.Plan.getAppDefs() );

[TODO] Reinstall - is it wort the effort? Who needs it?

[BUG] When RemoteOperError Message box appears and gets closed, exception happens (iteration variable changed)

[IDEA] Send all info as full state/changes. Including AppState, PlanState. Reduces unnecessary traffic if no changes.
[IDEA] Assign each unique AppDefs a small unique integer number and use it for identifying the app def in network messages and possibly everywhere. Keep a global registry of AppDefs indexed by this number. Number assigned by master (simple counter).
[IDEA] Assign each plan a small unique integer number and use it for identifying the app def in network messages and possibly everywhere. Keep a global registry of AppDefs indexed by this number. Number assigned by master (simple counter).

[IDEA] Add debug mode (--debug) that disables catching exceptions, leaving them crash the app and allowing them to be caught by the debugger.

[IDEA] WebServer WebSocket API for periodical push notifications about app/plan/script/client status

    {type:'planState', id='plan1', state={'code':'InProgress'}}
    
    {type:'appState', id='m1.a', state={'code':'SR'}}

