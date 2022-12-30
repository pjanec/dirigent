# Apps

Application controlled by Dirigent is an executable located on a certain machine.

Applications can be launched, terminated or restarted, either individually or all-at-once.

An application that is supposed to run continuously can be automatically restarted after unexpected termination or crash.

Available apps are predefined in SharedConfig.xml.

## Individual Apps Actions

- **Kill App.** The app is killed immediately if already running. The auto-restart (if configured) is disabled so that the app stays killed and is not started again automatically.
- **Launch App.** The app is launched if not already running, ignoring any dependency checks.
- **Restart App.** The app is first killed and then launched again.
- **Get App State.** Returns the status of one concrete app.
- **Get All Apps State.** Returns the status of all apps known to Dirigent.



## Application status

The applications are continuously monitored whether they are already initialized and still running. Their status is distributed to all agents on all machines.

Status is encoded in several flags

| Status Flag  | Meaning                                                      |
| ------------ | ------------------------------------------------------------ |
| Started      | The process launch attempt has been made.                    |
| Start Failed | The process launch has failed.                               |
| Running      | The process is currently running.<br />Warning: Process might be already dying! |
| Dying        | The process termination attempt has been made, but the process has not exited yet. |
| Killed       | The process termination attempt has been made via a direct KillApp request (i.e. not as a consequence of a KillPlan request) |
| Initialized  | Dirigent has guessed the app has initialized already. The guessing is based on the configured initialization detector mechanism. |
| Plan Applied | The application has already been processed as part of StartPlan operation. The plan will not attempt to launch the application again. This happens for example if the application was killed by a direct KillApp request. |
| Restarting   | Dirigent is going to restart the app. This happens after crash (if the app is configured to be automatically restarted) or after manual RestartApp request. |

## Identifying an application

The application instance is uniquely addressed by the name of the computer it is running on and by the name chosen for particular instance of an application. These two are separated by a dot, having format `machineId.applicationInstanceId`.

The `machineId` is unique globally. 

The `applicationInstanceId` is unique within the launch plan where it is used.

## Detecting that an app has initialized

Some apps take a long time to boot up and initialize. 

Dirigent supports multiple methods of detection whether an application is already up and running. The method (called the initialization detector) can be specified for each application in the launch plan.

Following methods are available

- **Timeout** - the app is considered initialized if still running after specified amount of seconds after launching the app
- **ExitCode** - the app is considered initialized after it has terminated and its exit code matches the number specified. This can be combined with an auto-restart option of the application, resulting in a repetitive launches until given exit code is returned.

If no initialization detector is defined, the app is considered initialized from the time it has been started. 

## Environment Variable for apps started by Dirigent Agent

The processes started by Dirigent inherit the environment variables of their parent process, i.e. the Dirigent agent itself.

The variables in the Dirigent agent's environment can be manipulated at runtime via the `SetVars` command.

Dirigent agent defines the following special variables for an app started from the launch plan:

 * `DIRIGENT_MACHINEID` = the machine id the agent was configured to (the first part of the AppIdTuple).
 * `DIRIGENT_APPID` = the application id (the second part of the AppIdTuple).
 * `DIRIGENT_PLAN` = the plan in whose context the app was started. Current plan name for apps launched without a plan via `LauchApp` command. Empty if no current plan.
 * `DIRIGENT_SHAREDCONFDIR` = full directory path to the SharedConfig.xml file
 * `DIRIGENT_MASTER_IP` = IP address of a Dirigent Master as defined in agent's configuration.


This provides a way to tell the processes started by the dirigent agent what station/machine (in terms of the dirigent machine naming) they are running at. This might come in handy if same process is started on many machines, it needs to know where it was started but you can not rely on the computer name.

Being environment variables, they can be used in command line parameters for the started process in the plan config file.

        <Plan Name="plan1">
            <App
                AppIdTuple = "m1.a"
                Template = "apps.notepad"
                StartupDir = "%DIRIGENT_SHAREDCONFDIR%\..\Documents"
                CmdLineArgs = "%DIRIGENT_MACHINEID%_%DIRIGENT_APPID%.txt"
                >
            </App>
        </Plan>

