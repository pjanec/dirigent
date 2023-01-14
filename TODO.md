[DONE] Add "Power > Reboot" to machine context menu.

[DONE] Add "Power > Wake On Lan" to machine context menu. Add MAC="2C:D8:5D:CE:F0:B8" attribute to `<Machine/>` section. Show WakeOnLAN menu only if MAC is defined tor the machine. https://benniroth.com/blog/2021-6-21-csharp-wake-over-lan/

[DONE] Tool action specified in the ToolsMenu section of the SharedConfig to allow running on different machine than the default local one. Via HostId attrib.

[DONE] Tool actions can override the StartupDir used for starting the tool. Via StartipDir attribute.

[IDEA] When Show Window is clicked, show a dialog that runs script on the remote machine grabbing all windows of selected process. Dialog shows the window titles and user can select what window to show/hide. Maybe put then to app's Properties tab.

[IDEA] Add "Show/Hide Desktop" to Machines context menu. 

[IDEA] Allow referencing app's startup folder from the File or Package definitions. Some special variable like `%APP_STARTUPDIR%` or something? Evaluated during the path resolution.

[BUG] "Collection modified" sometimes appear as notification baloon. Happens always after a file download.

[TODO] Built-in default tools like a simple window showing JSON, HTML viewer (using default web browser), Windows Explorer etc. These does not require special record in LocalConfig's Tools section, but can be overwritten by one if present there.

[IDEA] Implement some tools as standard part of the Dirigent, always available. 

[TODO] In App Properties window show multiple tabs. One of them is the actual startup info used for starting the app last time. Read that via remote script, use WMI etc.; return AppDef, command line string, environment of the process...

[IDEA] Add json viewer as one of internal tools? Or better use Notepad++?

[BUG] FolderDef, when including content, fails with exception if one of the subfolders is inaccessible

[IDEA] Use Glob style masks for filtering file names. 

[TODO] Script libraries - initialized from built-ins as well as by scanning script files. Applies to relative script paths only. At the moment relative paths are resolved to one single physical root folder, by default the location of the shared config file.

[IDEA] Add "Install OpenSSH server" to Tools menu, setting up the SSH server on each computer where Dirigent is running. Define a Script in shared config that runs the powershell to download necessary files, distribute to machines etc. Define similar script to enable powershell remoting on all the machines. 

[IDEA] Background scripts, defined per machine, automatically started when machine's agent gets SharedConfig (and killed when master sends Reset before sending new defs). Can periodically check for something to happen and emit notification baloon messages.

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

[TODO] Remember grid column widths.

[IDEA] Single selected plan from all GUIs (optional). Dirigent could be configured to distribute the Selected Plan to all its GUIs, meaning all GUIS will share the same selected plan. This might be useful for example for development purposes with multiple computers.

[IDEA] Tell the master about selecting a plan in the GUI using a new message PlanSelected. Master to update the app definitions to those from the plan (only if enabled, either by dirigent global setting, or individual plan setting...)

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

[IDEA] WebServer WebSocket API for periodical push notifications about app/plan/script/client status

    {type:'planState', id='plan1', state={'code':'InProgress'}}
    
    {type:'appState', id='m1.a', state={'code':'SR'}}

