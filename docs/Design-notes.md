# Dirigent.NetCore

[TOC]

# Building


# Components



## Agent
 * Running as a background service on each managed computers
 * Manages local applications
   * Launch, Kill, Restart
   * Initialization checking (timeout, window popped up, exit code...)
   * App window state control (position, style, visibility...) - MS Windows only
 * Monitors local app state and sends it to master
 * Executes management commands sent by master
 * Connects to master (IP address & port)
 * Receives relevant part of shared config (upon startup, when changed on master)


## Master
 * Manages the launch plans, executes launch logic
 * Instructs the agents what app to launch/kill/restart
 * Receives app states from agents
 * Receives control commans from GUI, from CLI...
 * Sends apps & plans state to UI
 * Loads shared config, distributes relevant parts it individual agents
 * Incorporated in an agent's executable, instantiated in one of agent processes

## Client
 * Communication endpoint able to send/receive network messages
 * Connects to master (IP address & port)
 * Used by Agent and Gui
 * Sends ClientInfo to master containing
   * What messages the client wants to receive from the master
     * AppStates
	 * etc.
	 
	 
	 
 
## RemoteAppRepo 
 * Intercepts mesages and gathers the state of applications
 


## GUI
 * Shows the state of controlled apps on managed machines (where an agent is running)
 * Allows individual app control (Launch, Kill, Restart)
 * Shows the list of plans
 * Allows individual plan control (Start, Stop, Kill)
 * Connects to Master, gets all info from it, sends all command to it 

## Command line interface client
 * Allows sending commands to Master in a text line format
 * Can be run in interactive mode
 * Incorporated in Dirigent.Agent executable


## Executables
 * Dirigent.Agent
   * 
 * Dirigent.GUI 
   * Windows only




# Network messages

## AppDef

Master -> Agent

Master updates one concrete app def.

Incremental (add/update, no delete).

Happens when app def is updated due to running an app from different plan with different settings etc.



## AppDefs

Master -> Agent

Master is sending the definitions of apps to be managed by the agent.

Contains a full set of individual App Defs

Agent merges the new list with its current own list of apps.

 * Kills apps that are no longer in the list
 * Updates app def for existing apps (to be applied for next start of the app). 

## AppStates
Agent -> Master
Master -> UI
Agent is periodically updating the master with current state of all the applications managed by the agent.
Master sends the state of all apps periodically when GUI is connected.

## AgentState
Agent -> Master
Agent/UI must send this as the first message after the connection is estabilished.
The status of the agent
 - name (agent's machine name or GUID for UI)
 - type (Agent/UI)
 - maybe later also, perf stats like CPU usage, Memory usage, net usage...


## LauchApp, KillApp, RestartApp, SetVars, KillAllApps, Terminate, Shutdown, Reinstall
Master -> Agent
UI -> Master
Master is instructing the agent to perform the operation 
Agent performs the action.
Action result is communicated back to master in the form ao perodical update of app state.

# StartPlan, StopPlan, RestartPlan, KillPlan, SetAppEnabled
UI -> Master
Master -> Agents
Instruction to master to operate plan

# PlanState
Master -> UI
Master is informing the UI about the status of all plans.


## RemoteOperationError
Agent -> Master
Master -> UI
Agent informs Master about some trouble/exception. Master is forwarding the exception to UI.

## ReloadSharedConfig
UI -> Master
Master reloads the plan definitions and sends updated AppDefs to agents.



# Agent's modules


## Agent's Context (actually the Agent class instance)
 * Links to all other modules
 * Initializes modules in correct order
 * Ticks the modules

## Comm
 * Connects to masters, keeps the connection
 * Incoming message queue (filled assynchronously from master, polled from outside)
 * Outgoing message queue (sent asynchronously to master)
 * Allows send message to master

## Message Processor
 * 

## Local Apps Repository
 * Holds the definiton and the current state of the applications belonging to that agent
 * Keeps various app-instance related processors
    * launcher
	* watchers
	* restarters
 * Initialized from master's AppDefs message
 * Updated based on incoming app statuses from agents.
 * Provides enumeration of all the apps and querying their status.
 * Provides Lauch/Kill/Restart operations

## Local Process List [probably not necessary as AdoptIfAlready running is mostly devel feature, few proceeses, no high perf req)
 * Holds info about all running processes
 * Updated infrequently
 * Searched for process names that should be adopted
 * Updated also on request (if not yet this tick) before any StartApp/KillApp that use AdoptIfAllreadyRunning (maybe not necessary, we can rely on infrequent update, the info will be delayed but the use is for processes that are running for a while already




# Master's modules


## Master's Context (actually the Master class instance)
 * Links to all other modules
 * Initializes modules in correct order
 * Ticks the modules

## Comm
 * Listens for client connections
 * Incoming message queue (including client info), filled from network asynchronously, polled from outside (from main thread)
 * Outgoing message queue (including client info), sent to network asynchronously
 * 

## Message Processor
 * 

## All Apps Repository
 * Holds the state of all applications
 * Initialized from shared config? Maybe
 * Allows updating based on incoming app statuses from agents.
 * Provides enumeration of all the apps and querying their status.

## All Plans Repository
 * Holds the definition of all plans; for each
    * AppDef for each apps
    * Current running state (Inactive, Launching, Killing)
	* Operation status (None, Launching, Killing, Success, Failure...)
 * Provides enumeration of the plans
 * Provides StartPlan, KillPlan, RestartPlan operations
 * Performs plan logic execution (launch wave planning/execution etc.)

## Agent's Repository
 * Holds info about known agents
 * Built based on agent connection events; never cleaned??

## UI Repository
 * Holds info about connected UI apps (disconnect = forget)
 * On connection/on change sends plan definitions
 * Periodically sends app states and plan states

## CLI server
 * Listens on CLI port
 * Responds to CLI requests
 * Request processor
 * 


# Configuration
The app configuration is loaded from a XML files

## Master's SharedConfig

 * List of Plan Defs, each

    * List of apps definition
   * Plan Script def
* List of Free App Defs (not belonging to any plan)
 * Each app definition
   * Process launch parameters (path to exe, cmdline arguments, startup folder, window state..)
   * Inter-application relations (dependency on another apps, time separation from the group of applications started before)
   * Process kill parameters (soft kill type etc.)
   * Process restart parameters (whether to restart, how many retries before stopping trying...)
   * Various App Watchers (init detectors, window position and style setters...)
   * App Script

 * List of application templates
   * Same content as an app record
   * May be referenced by the app as the base settings loaded before being replaced/updated with app-specific settings
	

During the load the application templates referenced by applications get resolved, incorporated into the final application definition. They are not kept in the memory.


## Agent's LocalConfig

 * Folder watcher

# Scripting

## Application script
 * Runs locally on agent where the app lives.
 * Has access to the AppDef, AppState, Launcher.
 * Can modify the app state, start/kill the app via Launcher's methods...
 * Depending on its type can be instantiated/ticked each agent frame independently on app state or just when the app should be running (like one of Watchers)
 * Gets notified about app control events Launch, Kill.

## Plan Script
 * Runs on master
 * Has access to AppDefs specified in the plan, to plan status, to built-in default plan logic.
 * Replaces the plan logic. Gets Start, Stop, Kill commands and executes them in some way. Updates the plan status.
 * Can utilize the existing default plan logic and pass the control commands to it; so it can replace just the plan status or custom status calculation if that is enough.
 * Instantiated always, ticked each master tick (but does not need to do anything...)
 * Can modify the list of AppDefs in the plan, can build the plan from scratch if needed.


# MISC

## AppDefs outside of plan (in a special internal Default plan??)
Applications can be defined outside of any plan as well as part of the plan.
When defined outside of the plan, this definition is loaded first as the base, before updating with definitions from the plan.

## Same app in different plans with different settings
AppDef is an optional part of LaunchApp. Used when app is launched with different settings than before (perhaps from another plan..)

## App adoption
Should happen periodically, not just when an app is being started or killed. No need for fast rate though.

## Shared process list snapshot
Various agent components should share process snapshot taken periodically by the agent.
Caching the process status.
WARNING: taking a snapshot of all processes takes a lot of time! Probably not worth it. Agents use Process.HasExited method which does not seem to be too perfromance demanding.
GetProcessById is what tkes so much time.

## App's IsSelected should be owned by master
Now part of AppState. Should be maintained by master, never updated from agent (not part of agent-determined state).
Actually agents do not care, it is used just for executing the plan (which happens on the master).

## Most functionality in a single executable Dirigent.Agent
 - Master started with --isMaster 1
   - master's log merged into agent's log (just a single log file)
 - Agent daemon started with --mode daemon
 - Interactive CLI sender started with --mode telnet
 - Single fire CLI sender started by putting non-option arguments on the command line
 - 
## Dirigent UI as a standalone executable Dirigent.GUI

## Plan's and App's CustomState
A JSON string that can read/written by a script. Shown by GUI, published via CLI. Not used by any dirigent components. Can be used for whatever.

## Populating Master's memory structures

* On Load when loading shared config
  * Free Apps => Remote App Registry (triggers sending an App Def update to agent)
  * Plan Defs => Plan Registry
* On App Launch
  * App Def => Remote App Registry (triggers sending an App Def update to agent)
* On manual App Launch from GUI
  * If the app is not part of the currently selected plan, first reset the App Def to the default (if defined)
  * Launch App command flags "Reset To Defaults"
* On Plan Start
  * No App Def updating, it happens just when app is being launched

## Switching plans with similar apps

Situation:

* New plan does not contain the app from a previous plan.
* The apps from the previous plans are still running.
* User switches to the new plan and runs it.

This is quite specific case when there is

* Max one plan at a time is expected to run
* Just the apps from that plan are required to run, while the other apps are not. The ones not in the new selected plan might still be started manually, with their default settings (not affected by the last plan applied).



User might expect that

* Just the apps from the new plan will be running. I.e. basically killing the apps that were part of the previously active plan and are not part of the new plan.
  * This is just local GUI related stuff, related to switching  to a new "selected" plan.
  * The apps not included in the new selected plan receive default settings (if started manually individually), instead of the app def settings left by the last applied plan.

These expectations above can be made a properties of the plan

If manually starting an app that is not part of the plan selected on the GUI, we should apply the default settings (if defined)



## Selected plan
This concept makes sense for individual GUIs (each of GUIs connected to same amster can select its own plan arbitrarily and independendtly on others).
It should not be central and dirigent master should not know about it.


## Application check box
Defines whther the app shall be started as part of the plan.
Now global (part of app state) but might make more sense if defined per plan, i.e. as part of the AppDef (where it is defined in config anyway).
When changed from the GUI, the AppDef gets changed (which is not a bad thing) and communicated to master who publishes it to all appropriate places.

## Running agent with a tray icon
The Dirigent.Agent.exe  is a multiplatform console application and as such can't have any tray icon (as the GUI is not multiplatform).

The Dirigent.Gui.exe is a Windows only app accepting same arguments as the agent.

Each can automatically start the other one on the same computer. The mode parameters tells what 

Dirigent.Gui provides the following features:
 * Tray icon
 * Control UI
 * Starting Dirigent.Agent as a subprocess (and keeping it running, i.e. start if killed/crashed)

The following modes are supported (via the --mode parameter):
 * "Gui" or default = Tray Icon + App/Plan control GUI
 * "TrayAgent" = Tray icon + Agent (GUI window can be open initially depending on --startHidden argument)
 * "TrayAgentGui" = Tray Icon + Agent + App/Plan control GUI

 Gui = just gui but no agent. If run from agent executable, just launches the gui and terminates
 TrayGui = agent + traygui (for compatibility with Dirigent 1.x)
 Agent = just agent, no gui (if used with GUI executable, the GUI will run the agent)


 
## Start apps after agent's late join
If plan is running and agent starts late or disconnects for a while and misses some LaunchApp commands.. Could master detect that and resend the LaunchApp commands? Master might check if agent is connected before sending commands to specific agent. And warn if it isn't. And remember it should send those later when agent is connected..


## Scripts on master
Scripts on master, running indepedently on apps and plans.
Scripts are shows in separate tab on the GUI.
A script can be started/killed/restarted via a separate set of dirigent commands.
Script provides text string status.
Script is ticked once each master's tick.
Script has access to control interfaces, being able to run apps, plans...
Script can watch for some specific constellation of apps states and trigger some actions...
Script can take text string messages??? Not right now, maybe later, if found useful..
