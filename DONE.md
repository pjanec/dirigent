

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

