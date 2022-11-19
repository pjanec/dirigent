[IDEA] Add "Install OpenSSH server" to Tools menu, setting up the SSH server on each computer where Dirigent is running.

[IDEA] Allow for remote file access across dirigent-equipped stations. Get the machine IP address from client's connection.  Allow to define file share name per machine (use "C", "D" etc. as defaults). Add "Folders" to context menu in client tab, listing all predefined folders on the machines, the C root always.

[IDEA] Enable quick access to app files and folders. In the AppDef define where the app file(s) for each app are located - multiple per app; also app-specific folders. Add "Show files" command to app's context menu, listing the defined files, opening them via a network share using default associated app.

[IDEA] In sharedConfig Define file packages, allow to download them easily. Files get zipped on their local machines to a temp folder, UNC paths to them are offered. Or they are downloaded to the Downloads folder (one per machine), repackaged to one single archive and the folder is opened in file explorer.

[IDEA] Files tab showing all the files defined. Allows viewing given file by opening the viewer - independent app accessing the file via its UNC path. Allows downloading the file (zipped).

[IDEA] File Packages tab showing all the file packages defined. Allows downloading the packages. Grid is foldable [+], showing individual files within the package.

[TODO] Monitor CPU GPU memory network stats on each station, show in Client tab.

[TODO] Monitor memory, cpu, gpu usage per app. Add columns to the app grid.

[TODO] In App Properties window show also the actual startup info used for starting the app last time. Add new message sent from client once when app is launched, broadcast to all, cache on each client.

[TODO] Remember grid column widths.

[DONE] Mitigate the false process exited report (reported too early when the process is still running). Introduce a new process setting MinKillingTime="1.5" to postpone reporting of the "killed" status after the kill request. Applies only if the OS reports the death of the process within this given time since the kill request.

[IDEA] Single selected plan from all GUIs (optional). Dirigent could be configured to distribute the Selected Plan to all its GUIs, meaning all GUIS will share the same selected plan. This might be useful for example for development purposes with multiple computers.

[IDEA] Sharing the ClientState with Master. Disabled for now as sending it caused StackOverflof on deserialization of proto message on master when using many clients and huge SharedConfig.xml. Not sure what was the cause. Might be some timing/initialization issue related to protobuf deserialization??

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
