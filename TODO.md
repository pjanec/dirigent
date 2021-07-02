[TODO] HTTP port as a command line argument.
[TODO] bind web server to any interface.
[BUG] Publishing of ClientStateMessage by the agent each frame causes stack overflow on deserialization in master in case of SharedConfig.xml.HUGE. Without the agents publishing ClientStateMessage it works... It fails only for message sent from an agent running in a separate process. Same message from an agent embedded with the master in Dirigent.Agent.exe does not cause this problem. Fortunately sending the ClientStateMessage from the agent is not necessary for giving just Connected/Disconnected feedback so it was removed.

[TODO] Show RemoteOpErrors on ImGui always on top of the main app window, even if the content is scrolled down

[BUG] batch file app started within agent's console - shall be run in its own window!

[BUG] Agent is now using SendKeysWait (as SendKeys requires a msg pump). Will probably stuck on unresponsive app. Run it in a thread?

[TODO] FolderWatcher from trayapp to Agent

[BUG] ReloadSharedConfig does not change the appdef (changed cmdLineArgs, tested in linux version)

[TODO] Unselect app from plan also as CLI operation. Unselected app not affected by start/kill/restart plan. Starting unselected app uses it's default configuration.

[TODO] SetLocalAppsToMaxRestartTries( rti.Plan.getAppDefs() );

[TODO] Terminate, Shutdown, Reinstall - are they worth the effort? Who needs them?

[BUG] When RemoteOperError Message box appears and gets closed, exception happens (iteration variable changed)

[IDEA] Send all info as full state/changes. Including AppState, PlanState. Reduces unnecessary traffic if no changes.
[IDEA] Assign each unique AppDefs a small unique integer number and use it for identifying the app def in network messages and possibly everywhere. Keep a global registry of AppDefs indexed by this number. Number assigned by master (simple counter).
[IDEA] Assign each plan a small unique integer number and use it for identifying the app def in network messages and possibly everywhere. Keep a global registry of AppDefs indexed by this number. Number assigned by master (simple counter).


[IDEA] Add debug mode (--debug) that disables catching exceptions, leaving them crash the app and allowing them to be caught by the debugger.

[DONE] WebServer REST API on master for querying the defs/statuses, extended to allow firing commands

    GET /api/plan/def ... list of all plandefs [{'name':'plan1', 'appDefs':[...]}, {'name':'plan2', 'appDefs':[...]}]

    GET /api/plan/def/plan1 ... plandef of a single given plan {'name':'plan1', 'appDefs':[...]}
    GET /api/plan/def?name="plan1"

    GET /api/plan/state ... list of the state of all plans [{'name':'plan1', 'status':{'code':'InProgress'}, {'name':'plan2', status={'code':'None'}}]
    
    GET /api/plan/state/plan1 ... state of a single plan {'code':'InProgress'}
    GET /api/plan/state?name="plan1"

    POST /api/cmd/StartApp/m1.a ... response on success: {}; response on failure: {error:{text:'error text'}}
    POST /api/cmd/StartApp?id=m1.a&plan=plan1
  

[IDEA] WebServer WebSocket API for periodical push notifications about app/plan/script/client status

    {type:'planState', id='plan1', state={'code':'InProgress'}}

    {type:'appState', id='m1.a', state={'code':'SR'}}
