[BUG?] Adpoting apps by plans. A plan now does not restart an app if it is already running, started from another plan,
event if the settings are different. This creates inconsistency with the StartApp command that restarts the app
if the evn vars are different from the ones used what starting the app. Also it may lead to confusion because
the app is left running with different settings than defined in the plan...

[IDEA] Introduce the concept of a "service". Defined using <svc/> node. Behaves like an app (status can be initializing, running, dying, killed/nut running...) just the Launch/Kill operations are implemented differently. Both launch and kill operation execute specific tool process (specified using <Tool/> node in shared config) having different settings for Launch and different for Kill (can be the same process with different parameters passed as variables, or two completely different processes...) The process is expected to finish ASAP. Once the tool process finishes with no error (definable exit code), the service is considered to be in expected state (started if Start tool was run, killed if Stop tool was run). Start and stop tool are by default run for each Launch/Kill request no matter what the service status is. The status can not be determined on another way but running the start/stop tool.

[IDEA] Replace the exception message box with a simplified dialog showing just the main message. The type of exception and the call stack could be shown upon clicking the [Details] button.

[IDEA] When connecting to SSH gateway, just the Dirigent port needs to be forwarded initially. Once the list of all machines & their services is received, additional port forwarding can be established (leaving the one for the Dirigent intact). This would avoid reconnecting to SSH server and caching the machine info. GatewayConfig file would be just read-only.

[IDEA] Services defined for a machine not just for port forwarding, but also allows activating given service on the machine (for example starting a some windows service, or running a custom network service directly within the dirigent etc.)

[TODO] SSH-enabledpackage downloads. 

1. User action starts a GUI download script.
2. GUI script starts the master download script on master.
   1. Master picks local-network download location
      1. Best on the GUI machine if it is running on one of agent machines
      2. Otherwise the master's machine if master is running on a agent machine
      3. Otherwise any agent machine
   2. Then asks machines to zip their files and upload the zip to selected download location
   3. Then returns the download location to the GUI script.
3. GUI script downloads the package to GUI-local download location (if not already downloaded there). GUI script calls IDirigent.CopyFileAsync. It uses SSH download if it is a ssh path, otherwise UNC download.

[IDEA] Quick Launch tab, definable from SharedConfig, showing icons for most commonly used tasks (System Star, System Stop etc...) Icon click runs predefined action (command, tool, user script...) Each icon supporting Disabled state and tooltip (status text). Maybe an individual (optional) user script running on GUI that can modify the status, set disable state etc? With grouping, shown in "explorer like style" (tree view on left and list view on right?)



[BUG] After successful SSH connect we disconnect from current master and start connecting to a new one using the forwarded port. Connection succeeds, but machines and other data not received from the new master!

[TODO] Add File/Connect to master... dialog preconfigured for current master IP/port.



[BUG] Default menus for files, machines etc. should be moved from LocalConfig to to SharedConfig and should be sent to gui clients.

[BUG] When running in --mode gui, Exception: Could not find IP of machine . in UpdateMainMenu. Just basic menu shown, no configurable extensions.

[IDEA] Let the dirigent client connected via SSH gateway to a target system to open files behind the gateway via WinScp.

Convert the local UNC paths like \\192.168.0.110\folder\file.txt into ssh://gateway/C/IT/Links/110/folder/file.txt.

Add script creating on the gateway necessary symlinks to individual machines as defined in the SharedConfig.

[IDEA] Use SSH.NET for both port forwarding

[IDEA] Use SSH.NET for sftp file operations. It works well if the remote path contains a symlink.

[IDEA] Local edits for files behind SSH gateway. Download the file via SFPT to a local temp folder, launch selected tool, monitor the temp file for changes (as long as the tool is running), on local change copy the file back to the remote host via SFTP. Launched tools would need to be tracked (their launcher not forgotten) and associated with the temp files to be monitored. On tool exit delete the temp file.

Allows using standard tools working with local files. No need to select the tool based on whether the file is behind a SSH gateway.

[IDEA] Dirigent's local package browser. File packages produced by dirigent to be opened in Dirigent's internal browser. The browser supports local edits for SFPT files (see the idea above).



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
 * For a machine define what services are available on what ports (VNC, RDP, DIRIGENT etc.)
 * For each service on each machine estabilish a port tunelling via a unique local port.
 * For a tool specify what service it is using 
 * When the tool is invoked in the context of a machine, find the local port previously assigned to the service & machine number and pass it to the tool on cmd line

 * New menu item "Open SSH port tunnel". 
    * Lets the user to select a file where the following is defined
      * SSH server IP, name & password (the gateway computer accessible from outside)
      * Dirigent master IP and port
    * Opens port tunnel for dirigent master by starting plink
    * Gui connects to the master and receives the definitions of machines & services.
    * Gui disconnects from dirigent master, plink is closed.
    * List of port forwarding is updated to include the machines & services.
    * Port tunnel is opened again (plink started again)
    * Gui reconnects to the dirigent master
    * This ends the port tunnel setup sequence;
    * From now on, gui operates normally.
    * When a tool is started in relation to the machine, it receives the local port which is tunelled to tha remote machine
      * Machine specific tools like VNC
      * Access to file which is located on that machine

* Try tunelling SMB file sharing over SSH - that would allow for remote file access to any computer
  * See https://sites.google.com/site/sbobovyc/home/windows-guides/tunnel-samba-over-ssh
  * For each computer behind a gateway we need unique local IP to address port 139 on remote computer
  * Requires installing Microsoft Loopback Adapter for each computer (to have one special IP per computer).
    * For example 192.168.100.XXX with subnet mask 255.255.255.0 (where XXX is the last byte of the IP in remote LAN)
    * Could be done on demand from Dirigent UI, just for the duration of the connection to Dirigent Master.
      * power shell script with internet access can do it: 
        * https://gbe0.com/posts/windows/server-windows/create-loopback-interface-with-powershell/
  * Setup ssh port fwd
      LoopBackAdapterIP:139 => RemoteHostIP:139
      For example
      \\192.168.100.100\folder\file.txt  brings us to  \\192.168.0.100\folder\file.txt
      \\192.168.100.150\folder\file.txt  brings us to  \\192.168.0.150\folder\file.txt

  * Translate IP address in UNC paths - replace RemoteHostIP with corresponding LoopBackAdapterIP 


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

