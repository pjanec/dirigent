## Dirigent Overview
Dirigent is an application life cycle management tool controlling a set of applications running on one or multiple networked computers. It runs on .net and Mono platforms, supporting both Windows and Linux operating systems.

#### Launch plans
Dirigent allows launching a given set of applications in given order according to predefined launch plan.

The plan specifies what applications to launch, on what computers, in what order and what another apps (dependencies) need to be running and initialized prior starting a given application.

The dependencies are checked among both local and remote applications. 

#### Individual applications control
Applications can also be terminated or restarted, either individually or all-at-once.

An application that is supposed to run continuously can be automatically restarted after unexpected termination or crash.

#### Application status sharing
The applications are continuously monitored whether they are already initialized and still running. Their status is distributed to all agents on all machines.

#### Launching apps at startup
A launch plan can be executed automatically on computer startup.

To speedup the startup process of a system comprising multiple interdependent computers, certain applications (not dependent on those on other computers) can be launched even before the connection among computers is estabilished.

#### Ways of control
All operations can be controlled from any computer via a control GUI, from a separate remote control tool or programatically via a small .net library.

#### Local and networked mode
Dirigent can be configured to to run either in single-machine or networked mode, with embedded control GUI or as GUIless background process (daemon), or as a command line control application.

#### Architecture

Each computer is running an agent process. One of the computers runs a master server process. Agents connect to a single master. The master's role is to broadcast messages from agents to all other agents and share the launch plans.

Agent manages the processes running locally on the same machine where the agent is running. Agent takes care of local application launching, killing, restarting and status monitoring. 

Agents listens to and executes application management commands from master.

Agents publish the status of local applications to master which in turn spreads it to all other agents. The status include whether the app is running, whether it is already initialized etc.

All agents share the same configuration of launch plans - each one knows what applications the others are supposed to run.


## Usage

### Basic steps
#### 1. Setup a master
Start a master process on one of the machines. Master is not necessary in single-machine mode of operation.

#### Deploy agents
On each machine install an agent application.

Assign a unique machineId to each agent so it could identify its application in the launch plan.

Specify the IP address and port of the master and machineId of in the local configuration of each respective agent.


#### 2. Configure launch plans
Define launch plans (what apps to start on what computer in what order) into a SharedConfig.xml config file. Deploy this config file to all agents. All agents need to use identical shared configuration file.

#### 3. Load and start a launch plan
Select a launch plan to start, issue a Load Plan command followed by a Start Plan command.

### Available Actions
The Dirigent can perform actions related either to a set of applications grouped into a launch plan or to individual applications that are part of the currently loaded launch plan.

#### Launch Plan Actions

 - **Load Plan.** The given plan becomes the current plan. Any previous plan is stopped, i.e. all its app are killed.

 - **Start Plan.** The current plan starts to get executed. The launch order is determined and the applications launch process begins.

 - **Stop Plan.** All apps that are part of the current lauch plan are killed.

 - **Restart Plan.** The current plan is stopped and started again. All apps from the plan are first killed and then thei launch process begins.

#### Individual Apps Actions

 - **Stop App.** The app is killed immediately if already running. The auto-restart (if configured) is disabled so that the app stays killed and is not started again automatically.

 - **Start App.** The app is launched if not already running, ignoring any dependency checks.

 - **Restart App.** The app is first killed and then launched again.

### Agent command line arguments
`agent.exe` is a Windows Forms application capable of running either as a background process with no user interface (just the log file) or as a GUI application that can be minimalized into a system tray.

#### Operation mode selection

The following options changes the mode of operation:

 `--mode deamon|trayGui|remoteControlGui` .... select mode of operation
 
 - **deamon** ... no user inteface at all, just a log file
     
 - **trayGui** ... an icon in tray with gui control app accessible from the context menu; the default
     
 - **remoteControlGui** ... not agent as such (not directly managing any local apps), just a remote control GUI that monitors the apps and remotely send commands to the agents
 
#### Another options

 `--singleMachine` .... no network, just single-machine operation (no master needed); forces --traygui automatically.
 
 `--startHidden 0|1` .... start minimized (only with --traygui and --remotecontrolgui)

 `--logFile xyz.log` ... what log file to use

 `--startupPlan <plan_name>` ... immediately loads and starts executing an initial plan before the connection to the master is estabilished
 
### Agent Console Command Line Utility

There is a small executable specialized for sending commands to agents. It connects to the master and send a command specified on the command line.
 
 `agentcmd.exe <command> <arg1> <arg2> ...`
 
Zero exit code is returned on success, positive error code on failure.

The commands just simply follow the available agent actions, please see chapter *Available Actions* for more details.

    LoadPlan <planId>
    StartPlan <planId>
    StopPlan <planId>
    RestartPlan <planId>
    
    StartApp <appId>
    StopApp <appId>
    RestarApp <appId>
    

 
## Configuration

Dirigent configuration comprises of two parts - a shared configuration  and a local configuration.

Shared configuration is shared among all agents. It deals mainly with the launch plans but can be used also for common network settings like master's IP and port.

Local configuration defines the network settings and operation mode details of a single agent or master application.

### Shared config
Shared configuration is stored in the SharedConfig.xlm file. 

#### Launch plan

Launch plan comprises just a list of apps to be launched in given order. At most one plan at a time can be active.

Each app in the launch plan has the following attributes:

 - unique text id of the application instance; comes together with the machine id
 - application binary file full path
 - startup directory
 - command line arguments
 - the launch order in case of same priority of multiple apps
 - whether to automatically restart the app after crash
 - what computer to launch the application on (unique machine id as text string)
 - what apps is this one dependent on, ie. what apps have to be launched and fully initalized before this one can be started
 - a mechanism to detect that the app is fully initialized (by time, by a global mutex, by exit code etc.)
 

#### Templated launch plan definition

Plan definition in an XML file uses a template sections allowing the inheritance of attributes.

Every record in the plan can reference a template record.

All the attributes are loaded first from the template and only then they can ge overwritten by equally named attributes from the referencing entry. 

A template record itself can reference another more generic template record.

#### Identifying an application

The application instance is uniquely addressed by the name of the computer it is running on and by the name chosen for particular instance of an application. These two are separated by a dot, having format `machineId.applicationInstanceId`.

The `machineId` is unique globally. 

The `applicationInstanceId` is unique within the launch plan where it is used.

#### Selecting a boot up completion detector

Some apps take a long time to boot up and initialize. Dirigent should not start a dependent app until its dependecies are satisfied. By 'satisfied' it is meant that the all the dependencies are already running and that they have completed their initialization phase.

Dirigent supports multiple methods of detection whether an application is already up and running. The method together with its parameters can be specified for each application in the launch plan.

Following methods are available

 - `immediate` - An app is considered initialized immediately after the launching of an application. This is the default.

 - `timeout <seconds>` - After specified amount of seconds after launching the app

 - `exitcode <number>` - After the app have terminated and its exit code matches the number specified. This can be combined with an auto-restart option of the application, resulting in a repetitive launches until given exitcode is returned.
 

### Local config
Local configuration is put together from multiple sources. The are listed in the descending order of priority:

 - Command line arguments
 - App.config file
 - Shared config file (can be used for network setting like master IP and port)
 - Built-in defaults


#### Autodetection of the machine id
Computer's NetBIOS name is used as a default machineId if not specified otherwise.

## Further Details

### Execution of a launch plan

A new launch plan automatically cancels any previous plan, i.e. all apps from the previous plan are killed. The application from the new plan are initially assigned the state 'not launched'.

The launch order of all apps form the plan is determined. The result is a sequence of so called launch waves. A wave contains applications whose depedencied have been satisfied by the previous launch wave. The first wave comprises apps that do not depend on enything else. In the next wawe there are apps dependent on the apps from the previous wave.

The waves are launched sequentially one after another until all apps from all waves have been launched. 

If some application fails to start, dirigent can be configured to retry the launch attempt multiple times.

If all attempts fail, the plaunch plan is stopped and an error is returned.




