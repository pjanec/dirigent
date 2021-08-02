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

