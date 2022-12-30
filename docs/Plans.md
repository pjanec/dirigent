# Plans

The launch plan specifies a group of apps to launch and keep running, on what computers, in what order and what another apps (dependencies) need to be running and initialized prior starting a given application.

The dependencies are checked among both local and remote applications. 

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

### Execution of a launch plan

The application from the plan are initially assigned the state 'not launched'.

The launch order of all apps form the plan is determined. The result is a sequence of so called launch waves. A wave contains applications whose dependencies have been satisfied by the previous launch wave. The first wave comprises apps that do not depend on anything else. In the next wave there are apps dependent on the apps from the previous wave.

The waves are launched sequentially one after another until all apps from all waves have been launched. 

If some application fails to start, dirigent can be configured to retry the launch attempt multiple times.

If all attempts fail, the launch plan is stopped and an error is returned.

### App dependencies

If an app is started as part of a plan, Dirigent will not start the app until all its dependencies (meaning some other apps) are satisfied.

'Satisfied' means that the all those other apps listed in the `dependencies`  attribute dependencies are already running and initialized.

### Multiple coexisting plans

Any of the plans can be selected and manipulated independently at any time.

### Apps in plans

Dirigent can work individually with each of applications found in any the plans from the plan repository. 

An app with same name can appear in multiple plans. In each plan it can be defined with different parameters. The parameters get applied when the app is being launched as part of the plan.

When starting an app, Dirigent sets the DIRIGENT_PLAN environment variable to the plan name the app belongs to.

### Adopting apps

If a plan references an app that is already running (possibly started as part of a different plan executed before), the new plan does not try to start the app again or restart it. The app is left running with its original parameters. The new plan acts as if that app have been started by the plan.

The app parameters as defined in the new plan are remembered and are applied as soon as the app happens to be started again.

### Utility Plans vs. standard plans

Usual plan "wants" to start all apps and watches if they are started. Such plan never ends automatically on its own even if all apps crash. If an app is set to be restarted automatically, the plan will do so until the plan gets stopped or killed.

Such the plan also can not be started again before it is manually stopped or killed.

An utility-plan it the one containing just volatile apps (having Volatile="1") is handled in a special way. Is is stopped automatically as soon as all the apps have been processed (an attempt to start them was performed) and all started apps have terminated (none left running).

Such volatile-only plan allows for being started again without prior stop or kill command.


