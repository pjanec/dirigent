# Command Line Interface
Dirigent can be controlled remotely using commands.

The commands can come from different sources:
  1. Dirigent UI
  2. Command line client app called [Dirigent.CLI](#Dirigent.CLI-Console-Command-Line-Utility)
  3. Over [TCP connection](#CLI-control-over-TCP-line-based connection) from a remote control app.

Commands are received by Dirigent master agent. The master then asks dirigent agent app to perform necessary operations on the machines affected by the command.

| Command                              | Description                                                  |
| ------------------------------------ | ------------------------------------------------------------ |
| [ApplyPlan](#ApplyPlan)              | Changes the app definitions to the one from given plan.      |
| [GetAllAppsState](#GetAllAppsState)  | returns status of all apps known to dirigent.                |
| [GetAllClientsState](#GetAllClientsState) | Gathers the status of all clients/machines known to the Dirigent. |
| [GetAllPlansState](#GetAllPlansState)| Gathers the status of all plans known to the Dirigent.       |
| [GetAppState](#GetAppState)          | returns the status of an app. |
| [GetClientState](#GetClientState)    | Returns the status of a client/machine. |
| [GetPlanState](#GetPlanState)        | Returns the status of a plan. |
| [GetScriptState](#GetScriptState)    | Returns the status code, status text and associated data of a script, in json single line format. |
| [KillAll](#KillAll)                  | Kills all running apps on all computers, stops all plans. |
| [KillApp](#KillApp)                  | Kills single app. |
| [KillPlan](#KillPlan)                | Kills all apps in the plan. |
| [KillScript](#KillScript)            | Kills a running script. |
| [ReloadSharedConfig](#ReloadSharedConfig)| Reloads the shared config file. |
| [RestartApp](#RestartApp)            | Restarts an app. |
| [RestartPlan](#RestartPlan)          | Kills all apps from the plan and starts the plan again. |
| [SelectPlan](#SelectPlan)            | Informs the Dirigent about the plan selection in a GUI. |
| [SetVars](#SetVars)                  | Sets environment variable(s) to be inherited by the apps launched afterwards. |
| [Shutdown](#Shutdown)                | Reboots or shuts down computer (or all computers) where dirigent agent is running. |
| [StartApp](#StartApp)                | Starts given single application if not already running. Might restart it if running with different environment than the desired one. |
| [StartPlan](#StartPlan)              | Starts launching apps from a plan according to the plan rules. |
| [StartScript](#StartScript)          | Starts a script on master. |
| [StopPlan](#StopPlan)                | Stops starting next applications from the plan. Does not kill any app! |
| [WaitForScript](#WaitForScript)      | Waits for a running script to finish and reports how it went. |
| [Terminate](#Terminate)              | Terminates Dirigent agents on computers, optionally killing all the apps managed by the Dirigent. |


## Command Line Interface

CLI uses a text formatted line based commands.

There can be one or more (semicolon separated) commands per line but a single command cannot span multiple lines.


## Correctly Quoting Arguments for Dirigent.CLI.exe in Batch Files

When using Dirigent.CLI.exe from a Windows batch file (.bat), command-line arguments are processed by two different parsers in sequence. This can lead to quotes and special characters being stripped or misinterpreted. This guide explains the reliable method for quoting arguments to ensure they arrive at your script handler intact.

### The Core Problem: Two-Step Parsing

1. **cmd.exe Parser**: The Windows Command Prompt first parses your entire command line. It uses double quotes (") to group arguments containing spaces or other special characters. It then passes the resulting arguments to the executable.  
2. **Dirigent's internal Parser**: After your application starts, it receives the arguments from cmd.exe and uses its own custom parser to split the command line again. This parser uses both single (') and double (") quotes as delimiters to group tokens.

The key to success is to craft an argument that survives both of these parsing stages correctly.

### The Golden Rule

The most reliable method is to **wrap your entire argument in double quotes for cmd.exe, and then wrap the content in single quotes for Dirigent's parser**. Any literal single quotes needed within the content must be escaped by doubling them ('').

```
" ' content ' "  
  ^-----------^---- For cmd.exe  
    ^-------^------ For Dirigent's parser
```

### Examples

Here are practical examples for common use cases.

#### 1. Argument with Spaces

* **Goal**: Pass the argument an argument with spaces.  
* **Command**:  
  Dirigent.CLI.exe YourCommand "'an argument with spaces'"

* **How it Works**:  
  1. **cmd.exe**: Sees "..." and passes 'an argument with spaces' to Dirigent.  
  2. **Dirigent Parser**: Sees '...' and returns the content an argument with spaces as a single token.

#### 2. "Relaxed" JSON with Single Quotes

This is the scenario that was confirmed to work for you.

* **Goal**: Pass the literal string {MachineName:'m1'}.
* **Command**:
  Dirigent.CLI.exe StartScript ... "'{MachineName:''m1''}'"

* **How it Works**:
  1. **cmd.exe**: Sees the outer "..." and passes the content '{MachineName:''m1''}' to Dirigent.
  2. **Dirigent Parser**:
     * Sees the outer '...' as delimiters.
     * Reads the content inside.
     * Encounters '' and correctly interprets it as a literal single quote (').
     * Returns the final token: {MachineName:'m1'}.

#### 3\. Standard, Valid JSON with Double Quotes

This is the recommended approach for passing structured data.

* **Goal**: Pass the valid JSON string {"MachineName":"m1"}.
* **Command**:
  Dirigent.CLI.exe StartScript ... "'{""MachineName"":""m1""}'"

* **How it Works**: 
  1. **cmd.exe**: Sees the outer "..." and passes the content '{""MachineName"":""m1""}' to Dirigent.
  2. **Dirigent Parser**: 
     * Sees the outer '...' as delimiters for a single token. 
     * Because it is inside a single-quoted string, it treats the double quotes (") and the doubled double quotes ("") as literal characters, not as delimiters or escape sequences for its own logic.
     * It returns the token: {"MachineName":"m1"}.
  3. **JSON Deserializer**: Receives a valid JSON string.

This method is robust and should be your go-to for passing JSON data. It avoids the complexities and ambiguities of trying to escape characters for cmd.exe and relies on the clear rules of the Dirigent parser.

## Commands

### StartApp

Starts given app if not already running. The general syntax is as follows:

	StartApp <appId>[@<planid>] [<varlist>]

Note: the app might get restarted if already running with different set of explicit env vars - see `LeaveRunningWithPrevVars` option.

Note: "LaunchApp" is an alias for StartApp.

#### App definitions from different plans

If just the app name is specified, the app is launched with settings defined by the recent plan the app was started from.

	StartApp m1.a

If the plan name is specified after the ampersand character, Dirigent starts given app with the parameters as defined in given plan (and not those used for the most recent launch)

	StartApp m1.a@plan1

If an empty plan is explicitly specified (the ampersand character present but no plan name follows), Dirigent uses the standalone app definition if available (see `Standalone apps` description - they are the `<app>` elements defined outside of any plan in the SharedConfig.xml) 

	StartApp m1.a@

#### Explicit Environment Variables

If the list of variables is present, those variables are passed as environment variable to the process started. They can also be used for expansion of the app's `CmdLineArgs` and `ExeFullPath` attributes in the SharedConfig.

	StartApp m1.a VAR1=VALUE1
	StartApp m1.a VAR1=VALUE1::VAR2=VALUE2

If the list of values is missing, the app can be optionally started with the most recently used explicit env. variables  - if the `ReusePrevVars=1` option is specified in the app definition. Without the option the app will be started without any explicit env var (unless they are specified on the command line).

If you want to explicitly avoid using the variable specified before, pass `::` as the variable list. Or you specify the variable value with empty value:

	StartApp m1.a ::
	StartApp m1.a VAR1=

Variable value strings containing spaces need to be enclosed in double-quotes. The outer double-quotes are removed. To add a double-quote character in the variable body, use two successive double-quotes.

	StartApp m1.a@plan1 VAR1="VALUE ""1"""::VAR2="VALUE 2"

The result will be like:

    VAR1=VALUE "1"
    VAR2=VALUE 2


### KillApp

Kills given app

  `KillApp <appId>`

##### Example

    KillApp m1.a

### RestartApp

Restarts given app. Optionally passing a new list of values.

  `RestartApp <appId> [varlist]`

If the list of values is missing, the app can be optionally started with the most recent explicit environment variables used. See the `StartApp` description for more details on explicit environment variables handling.

##### Example

    RestartApp m1.a
    RestartApp m1.a VAR1=VALUE1
    RestartApp m1.a VAR1=VALUE1::VAR2=VALUE2
    RestartApp m1.a VAR1=
    RestartApp m1.a ::

### StartPlan

Starts given plan, i.e. start launching apps according the plan rules

  `StartPlan <planName> [<varlist>]` 

If the varlist is specified, the variables are set to each app started from this plan.

See the `StartApp` chapter for more details on how the explicit environment variables are handled.

If you want to clear the variable specified before, you pass '::' as a varlist.

##### Example

    StartPlan plan1
    StartPlan plan1 VAR1=VALUE1
    StartPlan plan1 VAR1="VALUE ""1"""::VAR2="VALUE 2"
    StartPlan plan1 ::

### StopPlan

Stops starting next applications from the plan and evaluating the plan status.

This DOES NOT kill any app! 

  `StopPlan <planName>`

##### Example

    StopPlan plan1

### KillPlan

Kills given plans (kills all its apps)

  `KillPlan <planName>` 

##### Example

    KillPlan plan1

### RestartPlan

Kills all apps from the plan and starts the plan again.

  `RestartPlan <planName> [<varlist>]` 

If the varlist is specified, the variables are set to each app started from this plan.

If you want to clear the variable specified before, you pass '::' as a varlist.

##### Example

    RestartPlan m1.a
    RestartPlan m1.a VAR1=VALUE1
    RestartPlan m1.a VAR1="VALUE ""1"""::VAR2="VALUE 2"
    RestartPlan m1.a 

### GetAppState

  `GetAppState <appName>`   returns the status of given app

##### Response text

  `APP:<AppName>:<Flags>:<ExitCode>:<StatusAge>:<%CPU>:<%GPU>:<MemoryMB>:<PlanName>`


###### Flags

   Each letter represents one status flag. If letter is missing, flag is cleared.

  `S` = started

  `F` = start failed

  `R` = running

  `K` = killed

  `D` = dying

  `I` = initialized

  `P` = plan applied

  `X` = restarting


###### ExitCode

Integer number of the last exit code. Valid only if app has exited, i.e. Started but not Running, zero otherwise.

###### StatusAge

Number of seconds since last update of the app state

###### CPU

Float percentage of CPU usage

###### GPU

Float percentage of GPU usage [NOT IMPLEMENTED, zero]

###### MemoryMB

Float number of MBytes od system RAM used


###### PlanName

  The name of plan in whose context the app was most recently launched.

##### Example

  Request:   `GetAppState m1.a1`

  Response:     `APP:m1.a:SIP:255:10.1:34.7:82.5:7623.7:plan1`




### GetAllAppsState

  `GetAllAppsState` ...... returns status of all apps known to dirigent 

**Response text**

One line per application (see GetAppState command). The last line is "END\n"

    APP:m1.a:SIP:1:0.45:20.1:0:7623.2:plan1
    APP:m1.b::0:0:0:65.2:
    END

### GetPlanState

Returns the status of given plan

  `GetPlanState <planName>`  

##### Response text

  `PLAN:<planName>:<status>`

##### Example

  Request:   `GetPlanState plan1`

  Response:     `PLAN:plan1:Success`

### GetAllPlansState

Gathers the status of all plans known to the Dirigent

  `GetAllPlansState` 

##### Response text

Returns one line per plan; last line is "END\n"

	PLAN:plan1:None
	PLAN:plan2:Success
	PLAN:plan3:Killing
	END

### GetClientState

Returns the status of given client/machine

  `GetClientState <machineId>`  

##### Response text

  `CLIENT:<ClientName>:<IsConnected>:<ReportedSecondsAgo>:<IP address>`

##### Example

  Request:   `GetClientState m1`

  Response:     `CLIENT:m1:1:0.5:127.0.0.1`

### GetAllClientsState

Gathers the status of all clients/machines known to the Dirigent.

  `GetAllClientsState` 

##### Response text

Returns one line per client/machine; last line is "END\n"

	CLIENT:m1:1:0.3:192.168.1.1
	CLIENT:m2:0:2.7:192.168.1.2
	END


### StartScript

Starts a script on master. 

  `StartScript <guid> <path> [<args>]` 

The script instance is identified by a guid.

The script path and arguments can be predefined in shared config - in such case the `path` and `args` are taken from there if omitted in this command.

If the instance with given GUID is already running, the StartCommand is ignored until the existing instance terminates.

If a script with such guid was already defined before (in SharedConfig or by a `StartScript` command with specified `path` argument), the old one is forgotten and the new definition is used.

The 'args' argument is passed as a string to the script code; the script can interpret it as a json or similar (Newtonsoft Json can be used).

See [Singleton Scripts](Scripts.md#Singleton-scripts) for more details.

##### Example

If the script is predefined in shared config or was already started, the `path` can be omitted:

    StartScript "2d5b3159-83c6-48d4-9c52-0ce1af92cbb2"

Running a custom script:

    StartScript "2d5b3159-83c6-48d4-9c52-0ce1af92cbb2" "Script/DemoScript1.cs"

Running a custom script with custom arguments. Notice the relaxed syntax for the JSON allowed by Newtonsoft Json:

    StartScript "2d5b3159-83c6-48d4-9c52-0ce1af92cbb2" "Script/DemoScript1.cs" "'{""SomeString"":""Hi there!""}'"

Running a shared-config defined script with custom arguments - notice the empty 'path':

    StartScript "2d5b3159-83c6-48d4-9c52-0ce1af92cbb2" "" "'{""SomeString"":""Hi there!""}'"

Running a custom script specified by full script file path (on master computer file system):

    StartScript "F85D98C0-E6B1-435B-A56A-192D4CFAC9D0" "C:\DirigentScripts\GetConnectedMachines.cs"

### WaitForScript

Waits until a script finishes, and says how it went.

  `WaitForScript <guid> [timeout=<seconds>]`

Where `StartScript` answers as soon as the script has been *started*, this one answers when it is
over - which is what a caller that must not carry on yet needs, a plan step marking the log files
before the applications start writing to them being the case it was written for.

The answer comes in two parts:

  `ACK` immediately, meaning the instance exists and the wait has begun (the usual meaning of ACK:
  delivered and processed, not finished);

  `END` when the script has finished, or `ERROR: <reason>` if it failed, was cancelled, the timeout
  expired, or there is no such script instance. When there is nothing to wait for, the `ERROR` is the
  only line - the ACK is not sent.

`timeout` is optional; without it the wait lasts as long as the script does. When it expires the
script is **killed**, on the grounds that whoever asked is about to carry on regardless, and work that
lands afterwards is worse than work that never ran.

Waiting costs the master nothing: the command is ticked between other requests, which are served
normally throughout.

##### Examples

Start a script and wait for it, in one line:

    StartScript 2d5b3159-83c6-48d4-9c52-0ce1af92cbb2 "BuiltIns/MarkFiles.cs" "'{Node:{Id:''pkg.run''}}'" ; WaitForScript 2d5b3159-83c6-48d4-9c52-0ce1af92cbb2 timeout=300

Wait for a script somebody else started:

    WaitForScript 2d5b3159-83c6-48d4-9c52-0ce1af92cbb2

##### From a plan

An `<App ExeFullPath="[dirigent.command]" InitCondition="cliresponse ok">` step whose command line
ends in a `WaitForScript` holds the plan until the script is done - see
[Running a script as a step of a plan](ScriptsInPlans.md).

### KillScript

Kills given script if running.

Ignored if a script instance with such guid is not defined or is not running.

  `KillScript <guid>` 

##### Example

    KillScript 2d5b3159-83c6-48d4-9c52-0ce1af92cbb2



### GetScriptState

Returns the status of given script.

  `GetScriptState <guid>`

See [Scripts](Scripts.md) for more details about available script states.

##### Response text

  `SCRIPT:<guid>:<statusAsJson>`

##### Example

  Request:   `GetScriptState 2d5b3159-83c6-48d4-9c52-0ce1af92cbb2`

  Responses (various options presented below, always just one is returned):

     `SCRIPT:2d5b3159-83c6-48d4-9c52-0ce1af92cbb2:{"Status":"Running", "Text":"Some status text" }`
    
     `SCRIPT:2d5b3159-83c6-48d4-9c52-0ce1af92cbb2:{"Status":"Finished", "Data":<json formatted result data>}`
    
     `SCRIPT:2d5b3159-83c6-48d4-9c52-0ce1af92cbb2:{"Status":"Failed", "Data":<json formatted exception data>}`

### SetVars

Sets environment variable(s) to be inherited by the processes launched afterwards. Changes Dirigent's environment so it is applied to all process started later.

Note: This is a different set of variables than the explicit variables that can be set as part of `StartApp` or `RestartApp` commands.

Multiple variables can be set at once , separated by '::'.

String containing spaces need to be enclosed in double-quotes. The outer double-qotes are removed. To add a double-quote character in the variable body, use two successive double-quotes.

Once set, the environment variables stays set until they are changed. Each process started by Dirigent will inherit all those variables.

To unset a variable, you need to set it to an empty value (VAR1=)

  `SetVars <varlist>`

##### Examples

	SetVars VAR1=VALUE1
	SetVars VAR1="VALUE ""1"""::VAR2="VALUE 2"
	SetVars VAR1=
	SetVars VAR1=::VAR2=


### KillAll

Kills all running apps on all computers, stops all plans

  `KillAll` 

If defined in the [KillAll section](SharedConfig.md#KillAll-definitions) of the SharedConfig.xml, it also takes a set of extra actions.

### ReloadSharedConfig

Reloads the shared config.

If *file* option is used, dirigent will load this file (otherwise the currently used one gets reloaded).

If *killApps* option is "1", Dirigent tries to kill all apps before reloading the shared config. Default = "1".

If *killApps* is not specified or 0, the reload does not affect the apps that are already running. Should the app definition change, it is applied the next time the app is started/restarted.

  `ReloadSharedConfig  [file=c:\Path\To\SharedConfig.xml] [killApps=0|1]`

##### Examples

	ReloadSharedConfig
	ReloadSharedConfig killApps=0
	ReloadSharedConfig file=C:\dirigent\sharedConfig.xml killApps=0

### Shutdown

Reboots or shuts down all computers where the dirigent agent is running or just one specified machine.

 `Shutdown mode=PowerOff|Reboot` [machineId=<machineId>]`

##### Examples

	Shutdown mode=PowerOff
	Shutdown mode=Reboot machineId=m1

### Terminate

Terminates Dirigent agents on all the computers (or just on specified machine), optionally killing all the apps managed by the Dirigent. To be used before reinstalling the dirigent app.

 `Terminate [killApps=0|1] [machineId=<machineId>]`

If *killApps* option is "1", Dirigent tries to kill all apps before terminating itself. Default = "0".

Warning: Dirigent does not make sure the apps really get killed. It terminates itself right after issuing the kill command to the operating system for each of the running app managed by the Dirigent.

If *machineId* options is given, terminates the agent just on that machine. Of empty or missing, the Dirigent terminates it agents and GUIs on all machines. Default: empty.

##### Examples

	Terminate
	Terminate killApps=1
	Terminate machineId=PC-1
	Terminate machineId=PC-1 killApps=1


### ApplyPlan

Changes the app definitions to the one from given plan. Applies the plan either to all apps from the plan or just one single app from the plan (if appIdTuple is present).

 `ApplyPlan <planName> [<appIdTuple>]`

If *appIdTuple* is missing, the app definition will be updated for all the apps in the plan. If present, just that app will be updated.

##### Remarks

The app definition from the plan will be used on the next start of the app. Until then the previously set app definitions will be used, potentially coming from different plan or from the standalone app definitions.

Affects also the DIRIGENT_PLAN environment variable which is set when the app is started.

##### Examples

	ApplyPlan plan1
	ApplyPlan plan1 m1.a

### SelectPlan

Informs the Dirigent about the plan selection in a GUI.

 `SelectPlan <planName>`

##### Remarks

Dirigent performs actions related to a plan selection. For example it might execute the `ApplyPlan` if this option is enabled.

##### Examples

	SelectPlan plan1


## Dirigent.CLI Console Command Line Utility

 `Dirigent.CLI.exe` is specialized for sending commands to agents. It connects to the master via the TCP CLI interface and sends a command specified either on the command line.

    Dirigent.CLI.exe <command> <arg1> <arg2>; <command> <arg1>...

Zero exit code is returned on success, positive error code on failure.

For example:

    Dirigent.CLI.exe --masterIp 10.1.1.2 Start plan1; StartPlan plan2


Command can be also entered interactively from the console:

    Dirigent.CLI.exe --masterIp 10.1.1.2 --mode telnet

The commands are same as for the CLI TCP connection, see the command reference for examples.

Multiple commands on a single line can be separated by semicolon

    Dirigent.CLI.exe StartApp m1.a;StartPlan plan1



## CLI control over TCP line-based connection

Master is running a TCP server providing for controlling agents' operations.

To send a command open a TCP client connection to the master (server) and send the lines of text commands. Or use `Dirigent.CLI.exe` app sending the content of the command line to the master.

TCP server supports multiple simultaneous clients.

Server accepts single text line based requests from each client. Line separation character is `\n`.

Requests from different clients are buffered on the server in a single queue and processed sequentially in reception order. Response for a command may come later, after another commands have already been sent.

Each request can be optionally marked with request id. The request id is used to mark corresponding response lines.

For each request the server sends back one or more reply lines depending on the command type.

There is always a response to each command.  The client knows that the command was processed when a response with matching request id have arrived.

Clients can but do not need to wait for a response before sending another request. If the client marks each command with a unique request id, it can pair incoming response with corresponding request using provided request id.

##### Request line format:

  `[optional-req-id] request command text till the end of line\n`

##### Response line format:

  `[optional-req-id] response text till the end of line\n`


##### Response texts

  `ACK\n` ... Command reception was acknowledged, command was issued.

  `ERROR: error text here\n`

  `END\n` ..... Ends the list in case the command is expected to produce multiple line response

ACK does not mean that the command finished successfully! Only that it was delivered and processed.

Some commands do not return ERROR even if the command fails (but ACK is returned).

There are therefore three shapes of answer, and a client has to know which to expect **before** it
sends:

| shape | commands | the line that ends it |
| --- | --- | --- |
| acknowledged, and done | most of them - `StartPlan`, `StartApp`, `StartScript`, ... | `ACK` |
| a list | `GetAllAppsState`, `GetAllPlansState`, `GetAllClientsState` - one line per item, and no ACK at all | `END` |
| acknowledged, then finished later | [`WaitForScript`](#WaitForScript) | `ACK`, then `END` |

`ERROR` ends any of them. Each command declares its own terminator in the code, so `Dirigent.CLI.exe`
knows to read past the `ACK` of a waiting command instead of reporting success at it - and waits
without a deadline while it does, since the wait is as long as the work. A telnet client sees the same
lines and can apply the same rule.

###### Using request id

  Request:   `[001] StartPlan plan1`

  Response:     `[001] ACK`


###### Leaving out the request id

  Request:   `KillPlan plan2`

  Response:     `ACK`


###### Wrong identifier

  Request:   `KillPlan invalidPlan1`

  Response:     `ERROR: Plan 'invalidPlan1' does not exist`


###### Starting an application

  Request:   `[002] StartApp m1.a`

  Response:     `[002] ACK`


###### Getting plan status

  Request:   `[003] GetPlanStatus plan1`

  Response:     `[003] PLAN:plan1:InProgress`


###### Getting app status

  Request:   `GetAppStatus m1.a1`

  Response:     `APP:m1.a:SIP:255:10.9:34.1:0.6:7623`

###### Setting environment variable

  Request:   `[002] SetVars VAR1=VALUE1::VAR2=VALUE2`

  Response:     `[002] ACK`


###### Killing all apps

  Request:   `KillApps`

  Response:     `ACK`


###### Reloading SharedConfig

  Request:   `ReloadSharedConfig`

  Response:     `ACK`




## **Real-Time Event Subscriptions**

To avoid repeatedly polling for the status of applications, plans, and scripts, the Dirigent Command Line Interface (CLI) provides a real-time event subscription mechanism. By issuing the SendEvents command, a client can subscribe its current connection to receive automatic status updates whenever a meaningful change occurs.

### **The SendEvents Command**

This command manages the event subscription for the specific TCP connection that sends it.

**Syntax:**

SendEvents \<0|1\> [<update interval in seconds>]

* **SendEvents 1**: Subscribes the current connection to receive automatic status updates.  
  * The command should be prefixed with a unique **request ID**. This ID will be used to tag all subsequent automatic status messages sent to this client.

  * Upon successful subscription, the server immediately performs an  
    **initial status dump**, sending the current state of all known applications, plans, and scripts to the client.

  * After the initial dump, the server will only send updates for individual items whose status has meaningfully changed.

* **SendEvents 0**: Unsubscribes the current connection from receiving further updates. The client's status cache on the server is cleared to free up resources4.

---

### **Example Workflow**

Here is a typical sequence of commands and responses when using the event subscription feature.

**1\. Client Subscribes to Events**

The client connects and sends the SendEvents command with a request ID, in this case, \[event01\].

*Client Request:*

\[event01\] SendEvents 1

**2\. Acknowledgment and Initial Status Dump**

The server immediately acknowledges the command using a ACK and sends the current state of all relevant items, each line prefixed with the client's chosen request ID \[event01\].

*Server Response:*

\[event01\] ACK  
\[event01\] APP:m1.app1:R...  
\[event01\] APP:m2.app2:...  
\[event01\] PLAN:plan1:Success...  
\[event01\] PLAN:plan2:None...  
\[event01\] SCRIPT:2d5b3159-83c6-48d4-9c52-0ce1af92cbb2:{"Status":"Finished", ...}

The client can check for the ACK and if not received until a threshold (see master tick period), the client should return to the older polling concept (like GetAppStatus) to support older versions of the Dirigent.

**3\. Automatic Status Update**

Later, if m1.app1 is killed, its status changes. The server detects this change during its next polling cycle and automatically sends an update to the subscribed client. Note that only the changed item is sent.

*Server Sends Spontaneously:*

\[event01\] APP:m1.app1:SKI...

**4\. Client Unsubscribes**

When the client no longer needs real-time updates, it sends the SendEvents 0 command.

*Client Request:*

\[anyReqId\] SendEvents 0

*Server Response:*

\[anyReqId\] ACK

After this, the server will stop sending automatic updates to this client.

---

### **Remarks**

* **Connection Specific**: The subscription is tied to the individual TCP connection. If the client disconnects, the subscription is automatically terminated.  
* **Meaningful Changes Only**: Updates are triggered by changes in the operational state (e.g., Running to Killed), not by constantly fluctuating values like CPU usage or status age. This ensures that you only receive notifications for significant events.  
* **Polling Interval**: The server checks for changes at a regular interval (typically once per second). Updates are therefore near-real-time but not instantaneous.

* **Request ID is Key**: The request ID provided with the SendEvents 1 command is essential for identifying and parsing the automatic updates your application receives.  