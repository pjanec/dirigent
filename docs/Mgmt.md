# Management

## Management Operations

- **Set Vars.** Sets an environment variable(s) for the Dirigent process. They can be used for expansion in the applications' exe paths, command lines and other places.
- **Kill All.** Kills all running apps on all computers and stops all running plans. Kind of a 'central stop' feature.
- **Reload Shared Config.** Tries to reload the shared config containing the plan and app definitions.
- **Reboot All.** Reboots all computers where Dirigent agent is running.
- **Shutdown All.** Shuts down all computers where Dirigent agent is running.
- **Terminate Agents.** Kill the dirigent agents on all the computers. To be used before reinstalling the dirigent app.


### Logging

Both agent and master support logging of errors, warnings etc. into a log file through a Log4net library. The log file name as well as other options for logging (verbosity etc.) can be specified as a part of an app's .config file. 


### Folder Watching

Dirigent agent can be configured to watch a folder for file changes and trigger actions upon such a change.

The configuration is stored in agent's local configuration file (see --localConfigFile command line option).

Example of setting in agent's local config file:

	  <FolderWatcher
		  Path = "..\..\Tests"
		  IncludeSubdirs="0"
		  Conditions="NewFile"
		  Filter = "*.txt"
		  >
		  <!-- what to do if change detected -->
		  <Action Type="StartPlan" PlanName="CollectLogs">      
		  <Action Type="LaunchApp" AppIdTuple="PC1.WarningApp"/>
		  <Action Type="LaunchApp" AppIdTuple="PC2.AnotherApp@plan1"/>
	  </FolderWatcher>

The Path, if relative, is resolved relative to the location of the SharedConfig.xml file. Environment variables in form of %VARNAME% are expanded using Agen't current environment.
	  
Conditions supported:

 * `NewFile` ... file gets created

Action types supported

 * `StartPlan` ... starts predefined plan (does nothing if already running and not finished yet)
 * `LauchApp` .... starts predefined application (does nothing if already running)

Errors related to FolderWatcher (path not valid etc.) are logged only info agent's log file. Error results in FolderWather not being installed.

### 