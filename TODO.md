[TODO] FolderWatcher from trayapp to Agent

[TODO] ReloadSharedConfig

[TODO] Unselect app from plan also as CLI operation. Unselected app not affected by start/kill/restart plan. Starting unselected app uses it's default configuration.

[TODO] SetLocalAppsToMaxRestartTries( rti.Plan.getAppDefs() );

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

[IDEA] Send all info as full state/changes. Including AppState, PlanState. Reduces unnecessary traffic if no changes.
[IDEA] Assign each unique AppDefs a small unique integer number and use it for identifying the app def in network messages and possibly everywhere. Keep a global registry of AppDefs indexed by this number. Number assigned by master (simple counter).
[IDEA] Assign each plan a small unique integer number and use it for identifying the app def in network messages and possibly everywhere. Keep a global registry of AppDefs indexed by this number. Number assigned by master (simple counter).

[BUG] batch file app started within agent's console - shall be run in its own window!
[BUG] we started plan2 with app m1.b (app went Planned), then we launched app m1.b from plan1 - agent still having appDef from plan2... Starting app from certain plan should use that plan name!
