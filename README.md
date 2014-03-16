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

#### Arhitecture

Each computer is running an agent process. One of the computers runs a master process. Agents connect to a single master. The master's role is to broadcast messages from agents to all other agents.

Agent manages the processes running locally on the same machine where the agent is running. Agent takes care of local application launching, killing, restarting and status monitoring. 

Agents listens to and executes application management commands from master.

Agents publish the status of local applications to master which in turn spreads it to all other agents. The status include whether the app is running, whether it is already initialized etc.

All agents share the same configuration of launch plans - each one knows what applications the others are supposed to run.



## Usage

### Basic steps
#### 1. Deploy master and agents
Start a master process on one of the computers. Master is not necessary in single-machine mode of operation.

On each computer install an agent application.  Either as a service or as a system tray GUI application.

Specify the IP address and port of the master and machineId of in the local configuration of each respective agent.


#### 2. Configure launch plans
Define launch plans (what apps to start on what computer in what order) into a SharedConfig.xml config file. Deploy this config file to all agents. All agents need to use identical shared configuration file.

#### 3. Load and start a launch plan
Select a launch plan to start, issue a Load Plan command followed by a Start Plan command.

### Agent command line arguments

#### Command line control
By default the agent executeble works as a command line tool to send commands to agents
 
 `agent.exe <command> <arg1> <arg2> ...`
 
Zero exit code is returned on success, positive error code on failure.
 
#### Operation mode selection
The following options changes the mode of operation:

 `--daemon` .... no UI at all, just a log file
 
 `--traygui` ... an icon in tray with gui control app accessible from the context menu
 
 `--remotectrlgui` ... not agent as such (not directly managing any local apps), just a remote control GUI that monitors the apps and remotely send commands to the agents
 
#### Another options

 `--singlemachine` .... no network, just single-machine operation (no master needed); forces --traygui automatically.
 
 `--minimized` .... start minimized (only with --traygui and --remotecontrolgui)

 `--logfile xyz.log` ... what log file to use

 `--autostartplan <plan_name>` ... immediately loads and starts executing an initial plan before the connection to the master is estabilished
 
 
### Launch Plan Operations

 - **Load Plan.** The given plan becomes the current plan. Any previous plan is stopped, i.e. all its app are killed.

 - **Start Plan.** The current plan starts to get executed. The launch order is determined and the applications launch process begins.

 - **Stop Plan.** All apps that are part of the current lauch plan are killed.

 - **Restart Plan.** The current plan is stopped and started again. All apps from the plan are first killed and then thei launch process begins.

### Individual Apps Operations

 - **Kill App.** The app is killed immediately if already running. The auto-restart (if configured) is disabled so that the app stays killed and is not started again automatically.

 - **Run App.** The app is launched if not already running, ignoring any dependency checks.

 - **Restart App.** The app is first killed and then launched again.

## Configuration

Dirient configuration comprises of two parts - a shared configuration  and a local configuration.

Shared configuration is shared among all agents. It deals mainly with the launch plans.

Local configuration defines the network connection information and operation mode details of a single agent or master application.

### Shared config
Shared configuration is stored in the SharedConfig.xlm file. 

#### Launch plan

Launch plan comprises just a list of apps to be launched in given order. At most one plan at a time can be active.

Each app in the launch plan has the following attributes:

 - unique text id of the application; togeher with the machine id it makes a unique id
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

### Local config
Local configuration is put together from multiple sources. The are listed in the descending order of priority:

 - Command line arguments
 - App.config file
 - Shared config file
 - Built-in defaults


#### Autodetection of the machine id

[UPDATE] Not used.
By comapring the computer's IP address with those available in the computer list the dirigent processes automaticaly determine on what machine they are running. There is no need to tell them what machine id they are going to use.


### Application boot up completion detection

Some apps take a long time to boot up and initialize. Dirigent should not start a dependent app until its dependecies are satisfied. By 'satisfied' it is meant that the all the dependencies are already running and that they have completed their initialization phase.

Dirigent supports multiple methods of detection whther an application is already up and running. The method can can be specified for each application in the launch plan.

The simplest methods do not require any involvement of the application - for example the time measured from app launch. Such method are usually suboptimal - they usually need to wait longer than abosolutely necessary to safely avoid premature completion.

Better methods rely on some observable results of application execution - like showing a window, creating a file, creating a global mutext etc. For optimal results the application may be required to implement a direct support for such a detection.
 

#### Dirigent control

Dirigent can be controlled in multiple ways, each fitted for different use case. Everything can be controlled manually from the control GUI. Control commands can be sent to dirigent by executing its command line remote control application. Also a .net remote control library is available for embedding into user applications.

#### Computer list

[UPDATE] Not used.
For each computer there is a textual machine id and the IP address defined. One of the machines is marked as master. Such computer will run not just agent process but also the master process. UPDATE: computer list not used, the configuration of each agent is local in local app config files.

 

#### Execution of launch plan

A new launch plan automatically cancels any previous plan, i.e. all apps from the previous plan are killed. The application from the new plan are initially assigned the state 'not launched'.

The launch order of all apps form the plan is determined. The result is a sequence of so called launch waves. A wave contains applications whose depedencied have been satisfied by the previous launch wave. The first wave comprises apps that do not depend on enything else. In the next wawe there are apps dependent on the apps from the previous wave.

The waves are launched sequentially one after another until all apps from all waves have been launched. 

If some application fails to start, dirigent can be configured to retry the launch attempt multiple times.

If all attempts fail, the plaunch plan is stopped and an error is returned.

Pokud se nÏkter· z aplikacÌ nepoda¯Ì spustit, dirigent (v z·vislosti na nastavenÌ tÈ kterÈ aplikace) m˘ûe pokus o spuötÏnÌ i nÏkolik·t opakovat.

SkonËÌ-li vöechny pokusy o spuötÏnÌ nezdarem, prov·dÏnÌ spouötÏcÌho pl·nu se zastavÌ a nahl·sÌ se chyba. Chyba se hl·sÌ vöem ˙ËastnÌk˘m. Zobrazit uûivateli by ji mÏl ale pouze zadavatel povelu pro spouötÏnÌ pl·nu. Pokud byl pl·n spuötÏn z dirigentova GUI, objevÌ se chyba v tomto GUI. Pokud o spuötÏnÌ pl·nu poû·dala jin· aplikace (nap¯. p¯es sÌù), dostane chybovou zpr·vu zpÏt po stejnÈm kan·lu.



