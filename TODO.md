[IDEA] Scripts on master, running indepedently on apps and plans. A script can be started/killed/restarted (separate set of dirigent commands). Script provides text string status.

[BUG] batch file app started within agent's console - shall be run in its own window!

[BUG] Agent is now using SendKeysWait, run it in a thread to avoid endless waiting for app that is stuck!

[TODO] FolderWatcher from trayapp to Agent

[BUG] ReloadSharedConfig does not change the appdef (changed cmdLineArgs, tested in linux version)

[TODO] Unselect app from plan also as CLI operation. Unselected app not affected by start/kill/restart plan. Starting unselected app uses it's default configuration.

[TODO] SetLocalAppsToMaxRestartTries( rti.Plan.getAppDefs() );

[TODO] Terminate, Shutdown, Reinstall

[BUG] When RemoteOperError Message box appears and gets closed, exception hapens (iteration variable changed)

[IDEA] Send all info as full state/changes. Including AppState, PlanState. Reduces unnecessary traffic if no changes.
[IDEA] Assign each unique AppDefs a small unique integer number and use it for identifying the app def in network messages and possibly everywhere. Keep a global registry of AppDefs indexed by this number. Number assigned by master (simple counter).
[IDEA] Assign each plan a small unique integer number and use it for identifying the app def in network messages and possibly everywhere. Keep a global registry of AppDefs indexed by this number. Number assigned by master (simple counter).


[IDEA] Add debug mode (--debug) that disables catching exceptions, leaving them crash the app and allowing them to be caught by the debugger.
