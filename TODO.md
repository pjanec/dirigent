[IDEA] Add "Install OpenSSH server" to Tools menu, setting up the SSH server on each computer where Dirigent is running.

[IDEA] Allow for remote file access across dirigent-equipped stations. Get the machine IP address from client's connection.  Allow to define file share name per machine (use "C", "D" etc. as defaults). Add "Folders" to context menu in client tab, listing all predefined folders on the machines, the C root always.

[IDEA] Enable quick access to app files and folders. In the AppDef define where the app file(s) for each app are located - multiple per app; also app-specific folders. Add "Show files" command to app's context menu, listing the defined files, opening them via a network share using default associated app.

[IDEA] Distributed Tasks.

* A client issues a Task for multiple clients (either all or just listed).
* Task logic consists of controller part and worker part. Worker part is running on affected clients. Controller part is running on master.
* Controller and Worker parts are either built-in (hardcoded) or exist as a user script file residing on master.
* The controller instantiate workers, sends requests to workers, keeps track of the task progress on workers (by listening to their status updates via TaskResponse messages) and decides task completion.
* Each worker can provides task state update for its part of the job (In-Progress + estimated time, Success, Failure etc.)
* Controller aggregates worker states, determines whole task status, removes the task instance when done.
* Dirigent framework provides top level task management messages (RunTask, KillTask), whole task status update message (TaskStatus), low level messages for communication between controller and worker (TaskRequest, TaskResponse with string id, json payload), message forwarding through master. 
* Both Controller and Worker runs asynchronously, syncing with main dirigent thread when calling dirigent framework functions (sending requests, querying information etc.)

Maybe extending the existing Script implementation is the right way to go. Current Script can be implemented as just the Controller part of the task running on the master, no workers.

Controller part and Worker part both reside in one single class?? InitController, InitWorker, TickController, etc?? Better to run both as two independent scripts? That would avoid false assumptions that the Controller and worker can share some member variables (they can't as they run in different instance on different machines). We could also reuse the one single script running infrastructure for both!

Example of file download from one client to another:

* Built-in task start request is sent from the client where the file should be downloaded to (file recipient). Arguments = guid of the FileDef to download.
* Master instantiates the task on master and starts its Controller part.
* The Controller part starts the Worker part on the client where the file resides (file provider) as well as on the client where the file should be downloaded to (file recipient). It does so by calling RunWorkers with client list containing one single MachineId of the client.
* The Dirigent sends InstantiateWorker message to clients. The clients instantiate the Worker part of the task. The worker instances on each client are marked with the task instance guid so we can later (in the clean-up phase) kill the workers belonging to given task instance.
* Both the Controller part and the Worker parts are ticked
* The worker part can immediately start executing some logic (by starting a coroutine for example), using the arguments received from the InstantiateWorker message. is event driven, waiting for requests. When the request comes, the worker either responds immediately or starts a new coroutine handling the request.
* 
* 
*  This message is considered an initial request so it is assigned a new guid to allow for sending responses back to the controller. Worker parts get instantiated, finds the FileDef, resolves the path to the (local) file, zips the file to a temporary folder and sends response to the controller. The response data contains the UNC path back to the Controller.
* 
* The Controller sends to the file provider a new request "please zip  the file and give me its UNC path" carrying the FileDef's guid in its data.
* The Dirigent sends InstantiateTaskWorker message to the file provider client. This message is considered an initial request so it is assigned a new guid to allow for sending responses back to the controller. Worker parts get instantiated, finds the FileDef, resolves the path to the (local) file, zips the file to a temporary folder and sends response to the controller. The response data contains the UNC path back to the Controller.
* Controller call RunWorkers again, this time instantiating the worker 

- StartTaskMessage makes the master to instantiate the task controller
- The controller sends the InstantiateTaskMessage to clients.
- 
- 

Currently just the Script was cloned to DTask and task-management messages were defined (not sent nor handled). Further 

TODO:

- Define a base task that serves as a base for internal built-in tasks not based on user scripts as well as for the script based.
- Make sure the TaskRegistry is present on each client, not just on master. in response to InstantiateTaskMessage it starts executing the worker part when the task is instatiated on client (). When 
- Analyze what would it take to run task scripts asynchronously and isolated from dirigent's tick to avoid affecting the dirigent functions if the script malfunctions.
- Instantiate task on all clients by sending its script file over network and storing it to a temp folder on target machine, in client-specific folder.
- etc etc.



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
