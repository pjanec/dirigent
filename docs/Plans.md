# Plans

The launch plan specifies a group of apps to launch and keep running, on what computers, in what order and what another apps (dependencies) need to be running and initialized prior starting a given application.

The dependencies are checked among both local and remote applications. 

Apps planned for a remote machine are started as soon as the machine gets on-line (if the plan has not been stopped before).

Available plans are predefined in SharedConfig.xml.

## Launch Plan Operations

- **Start Plan.** Apps from the current plan start to be launched according to the plan.
  - Note: Apps can be started/killed individually, without using a plan.
   - Note: Plan-driven app control approach can be combined with individual app control approach.
- **Stop Plan.** Stop launching of apps from the current plan. No apps are killed.
- **Kill Plan.** All apps that are part of the current launch plan are killed.
- Killing a plan usually makes sense just for the app-keeping plans. It makes sure that all the apps get killed. This is indicated by the status switching from Success (all apps running) to Killing (apps getting killed) to None (all apps killed).
  - Started plan shall be killed first in order to be started again.

- **Restart Plan.** All apps from the current plan are first killed and then the plan starts.

- **Get Plan State.** Returns the status of one concrete plan.

- **Get All Plans State.** Returns the status of all plans known to Dirigent.

## Plan types

The plans in Dirigent can be used in several different ways

  1. **App-keeping plan**
     - To keep the contained apps up and running
     - All the apps contained in the plan are supposed to stay running for the whole life time of the plan (not Volatile)
     - Plan is successful if all apps are up and running.
     - Plan is failing if some of the apps failed to run, crashed, was killed using `KillApp` etc.

  2. **Utility plan**
     - To run some one-shot utility commands
     - The plan just run the apps (commands) and terminates. Often it sends a few commands to dirigent without caring whether the commands executed successfully or not.
     - All the apps needs to be marked Volatile
     - The plan Status does not indicate anything useful (no relation to the consequences of running those commands)

  3. **A combination of the two options above**
     - Non-volatile apps are kept running
     - Volatile apps are started and then forgotten
     - Plan status is valid for the non-volatile apps
     - Failed volatile app (if returning an error as its exit status - depends on InitConditons) may cause the plan to fail

### Plan Status

The plan status indicates whether everything went successfully or if there was a failure.


| Status      | meaning                                                      |
| ----------- | ------------------------------------------------------------ |
| None        | Plan not running, i.e. not taking care about contained applications. |
| In Progress | Plan is running in launch mode. Applications get started sequentially in the order as defined by their interdependencies. Apps are optionally kept alive (restarted) if they terminate unexpectedly. Dirigent tries to guess whether apps have already finished their initialization |
| Success     | All apps have been started and initialized and all are running. The status may change to Failure when some non-volatile app stops running! |
| Failure     | Some apps have failed to start or to initialize within given time limit. The status will switch from Failure to Success as soon as the app causing the failure finally starts and initializes. |
| Killing     | Plan is in killing mode where all apps are being closed. As soon as the apps terminate the status goes back to None. |

### Marking or clearing files when a plan starts

A plan can draw the line under the logs of a run itself, so that every start is a run boundary and nobody has to remember:

<!-- example: loads -->
```xml
<Shared>
  <File Id="pkg.logs" Title="Logs" MachineId="m1" Path="C:\logs\run.log" Clearable="1"/>
  <Plan Name="system" MarkFiles="pkg.logs" MarkTimeout="45">
    <App AppIdTuple="m1.camera" ExeFullPath="C:\camera.exe"/>
  </Plan>
</Shared>
```

* `MarkFiles` and `ClearFiles` - semicolon separated lists of file definition `Id`s (wildcards allowed, blanks and empty items ignored). A name that matches no file definition is a load error. Only files whose definition says `Clearable="1"` are touched, as with [Mark and Clear](MarkAndClear.md).
* `MarkTimeout` - seconds the marking may take, 60 by default. Both lists may be given; the clear is run first.
* `MarkInterval` - seconds between the **checkpoints** the plan takes of its `MarkFiles` while it runs; none by default. See
  [Checkpoints](#checkpoints-collecting-since-a-time) below.

What happens:

* The marking runs when the plan actually starts (not when it is already running), **before the first app is launched**. While it runs the plan is `Running`, its status stays *In Progress*, and its state carries `phase` ("Marking files (pkg.logs)").
* The plan's `StartTimeout` counts from the moment the apps are launched, not from the start of the marking.
* When the marking ends the plan state carries `markResult`: how many files were marked, cleared, skipped (not clearable) and failed, the errors, and `timedOut`/`cancelled`. See [`planstates`](HTTP.md#GET-apiplanstates) and [`GetPlanState`](CLI.md#GetPlanState).
* A marking that fails or does not finish in `MarkTimeout` **never fails the plan**: the operation is stopped, a warning is logged, the result is recorded and the apps are launched. Machines that are not connected are not waited for; the result says so.
* **Stop** or **Kill** of the plan during the marking launches nothing; the marking is not cut short, it ends by itself (bounded by `MarkTimeout`) and its result is recorded.
* A **Start** while the marking of the previous start is still going starts no second one; the apps are launched when it ends.
* **Restart** kills the apps, then marks again as part of its start.
* The same holds however the plan is started: GUI, CLI, REST or a step of another plan.
* **Kill All** stops the plan but does **not** cut the marking: it finishes, bounded by `MarkTimeout`, and no app is launched. A **second Kill All** while the first is still going (the emergency stop) does cut it: `markResult.cancelled` is set, the phase is cleared and the plan is not left stuck. A marking whose script is killed any other way (`KillScript`) is recorded as cancelled too. A plan without these attributes is not affected in any way.

A plan without `MarkFiles`/`ClearFiles` behaves exactly as before. For doing it by hand with a plan step, see [Marking from a plan](MarkAndClear.md#marking-from-a-plan).

### Checkpoints: collecting "since a time"

A mark divides a log into before and after one moment - the start of the plan. To collect *the last two hours* of a system
that has been running for days, Dirigent has to know how long each log was two hours ago, and it learns that without
reading a single line of any log: a plan with `MarkInterval` records it as it goes.

<!-- example: loads -->
```xml
<Shared>
  <File Id="pkg.logs" Title="Logs" MachineId="m1" Path="C:\logs\run.log" Clearable="1"/>
  <Plan Name="system" MarkFiles="pkg.logs" MarkInterval="600">
    <App AppIdTuple="m1.camera" ExeFullPath="C:\camera.exe"/>
  </Plan>
</Shared>
```

* Every `MarkInterval` seconds **while the plan runs** - from its start until it is stopped or killed - the plan takes a
  checkpoint of its `MarkFiles`: on each machine, the length of every clearable file is recorded in the file's history.
  Nothing is read from inside the files; the files themselves are not touched.
* **A checkpoint never moves the mark.** The mark - the boundary an ordinary collection uses - stays where the start of the
  plan, or an explicit `MarkFiles`/`POST /api/marks`, put it. Checkpoints only add records to the history, which only a
  collection "since a time" reads. So they cannot get in the way of a mark, a clear or an unmark, whenever those come.
* Never two at a time: one is not started while the previous one is still running, nor while the plan's own marking runs.
* The first interval counts from the start of the plan (whose marking is itself the first record).
* `MarkInterval` needs `MarkFiles` (the checkpoints are taken of those) and is at least 10 seconds; otherwise the config does
  not load. The plan state's `lastCheckpoint` says when the last one finished ([`planstates`](HTTP.md#GET-apiplanstates)).

A collection asked for **since a time** (`"since"` of [`POST /api/downloads`](HTTP.md#POST-apidownloads)) then takes each
clearable file from the latest record at or before that time - see
[Collecting since a time](MarkAndClear.md#collecting-since-a-time). The finer the interval, the closer the collection starts to the
time asked for; a record is one length per file, so even 10 minutes over a week is a small file per machine.

### Execution of a launch plan

The application from the plan are initially assigned the state 'not launched'.

The launch order of all apps form the plan is determined. The result is a sequence of so called launch waves. A wave contains applications whose dependencies have been satisfied by the previous launch wave. The first wave comprises apps that do not depend on anything else. In the next wave there are apps dependent on the apps from the previous wave.

The waves are launched sequentially one after another until all apps from all waves have been launched. 

If some application fails to start, dirigent can be configured to retry the launch attempt multiple times.

If all attempts fail, the launch plan is stopped and an error is returned.

### App dependencies

If an app is started as part of a plan, Dirigent will not start the app until all its dependencies (meaning some other apps) are satisfied.

'Satisfied' means that the all those other apps listed in the `dependencies`  attribute dependencies are already running and initialized.

### A step that waits for a Dirigent command

An application of a plan can be a Dirigent command rather than a program -
`ExeFullPath="[dirigent.command]"` - and, with `InitCondition="cliresponse ok|any"`, the plan waits
for that command before launching whatever depends on it. The case it exists for is drawing a line
under the log files before the applications start writing to them:

<!-- example: loads -->
```xml
<Plan Name="System Start">

    <App AppIdTuple = "master.mark_logs"
         ExeFullPath = "[dirigent.command]"
         CmdLineArgs = "StartScript 7B3C1E90-1111-2222-3333-444455556666 BuiltIns/MarkFiles.cs '{Node:{Id:''pkg.run''}}' ; WaitForScript 7B3C1E90-1111-2222-3333-444455556666 timeout=300"
         Volatile = "1"
         InitCondition = "cliresponse any"
    />

    <App AppIdTuple="m1.camera"   ExeFullPath="C:\apps\camera.exe"   Dependencies="master.mark_logs"/>
    <App AppIdTuple="m2.recorder" ExeFullPath="C:\apps\recorder.exe" Dependencies="master.mark_logs"/>

</Plan>
```

* `cliresponse any` waits for the command and carries on whatever it answered - for a step that must
  not hold up a system start. `cliresponse ok` makes a failure of the command a failure of the step,
  and therefore of the plan.
* The waiting itself comes from the command: [`WaitForScript`](CLI.md#waitforscript) answers only when
  the script is over, while most commands answer as soon as they are accepted.
* `Volatile="1"`, because the step is meant to end - see
  [Utility plans](#utility-plans-vs-standard-plans).

The whole story, including what happens when the command fails and why it is not an exit code, is in
[Running a script as a step of a plan](ScriptsInPlans.md).

### Multiple coexisting plans

Any of the plans can be selected and manipulated independently at any time.

### Apps in plans

Dirigent can work individually with each of applications found in any the plans from the plan repository. 

An app with same name can appear in multiple plans. In each plan it can be defined with different parameters. The parameters get applied when the app is being launched as part of the plan.

When starting an app, Dirigent sets the DIRIGENT_PLAN environment variable to the plan name the app belongs to.

### Adopting apps

If a plan references an app that is already running (possibly started as part of a different plan executed before), the new plan does not try to start the app again or restart it.

The app is left running with its original parameters. The new plan acts as if that app have been started by the plan.

The app parameters as defined in the new plan are remembered and applied as soon as the app happens to be started again.

### Utility Plans vs. standard plans

Usual plan "wants" to start all apps and watches if they are started. Such plan never ends automatically on its own even if all apps crash. If an app is set to be restarted automatically, the plan will do so until the plan gets stopped or killed.

Such the plan also can not be started again before it is manually stopped or killed.

An utility-plan it the one containing just volatile apps (having Volatile="1") is handled in a special way. Is is stopped automatically as soon as all the apps have been processed (an attempt to start them was performed) and all started apps have terminated (none left running).

Such volatile-only plan allows for being started again without prior stop or kill command.

### Starting apps as part of a plan

An app is started from a plan if the is started and contains an app record for this app. Or even later, when such an app was previously run as part of the plan, then terminated and then started again using the StartApp command.

App started from a plan uses the settings from the app record defined inside the plan in the SharecConfig. I.e. not any of the the app records defined outside of the plan.

### Plan-contained apps vs. free apps

The same app can be started with different settings from different plans - each plan defines its own settings for an app.

<!-- example: loads -->
```xml
<Shared>
  <Plan Name="plan1">
    <App AppIdTuple="Machine1.App1" ExeFullPath="C:\apps\app1.exe"/>
  </Plan>
</Shared>
```

A free app is an app whose settings are defined outside of any plan.  
<!-- example: loads -->
```xml
<Shared>
  <App AppIdTuple="Machine1.App1" ExeFullPath="C:\apps\app1.exe"/>
</Shared>
```

An app can be defined in multiple plans or also as a free app.

When starting an app, it can be specified from what plan the settings shall be taken or if they should be taken from the free app (See the StartApp command)
