# Actions
Action is an user definable menu item associated with an app, machine or a file/package.
Actions are available from the context menus in Dirigent's UI.

| Action type    | Description                                 |
| -------------- | ------------------------------------------- |
| `ToolAction`   | Starts a tool, see [Tools](Tools.md).       |
| `ScriptAction` | Starts a script, see [Scripts](Scripts.md). |

See examples in [SharedConfig.xml](../config/SharedConfig.xml).



TODO: XML attrib specs



### Association with VfsNode, app  and machine contexts

The place where the action is specified in the `SharedConfig.xml` defines the context in which the action is started.

This context determines

* What variables will be passed to the action.
* In what context menu the action will be displayed.

### Action variables

An action can receive some details about the item in whose context it is run. The details are provided as variables that can be used.

In case of a tool action these  variables can used as part of the command line arguments of the tool.

In case of a script these variables are passed to the script as script arguments.

| Item type   | Variable    |                                                              |
| ----------- | ----------- | ------------------------------------------------------------ |
| File        | FILE_PATH   | Resolved full path to the file. UNC path if the file is located on different machine or if it is a 'global' file. |
| Folder      | FILE_PATH   | Resolved full path of the folder. UNC path if the file is located on different machine or if it is a 'global' folder. |
| FilePackage | N/A         | FilePackages not supported by ToolAction. But they are by the ScriptAction! |
|             |             |                                                              |
| App         | MACHINE_ID  | Name of the machine where the app is located.                |
|             | MACHINE_IP  | IP address of the machine where the app is located.          |
|             | APP_IDTUPLE | Full app name in format "MachineName.AppName".               |
|             | APP_ID      | Name of the app excluding the machine name.                  |
|             | APP_PID     | Process id of the app. Available only if the app is running. Usable only on the same machine where the app is running. |
|             |             |                                                              |
| Machine     | MACHINE_ID  | Name of the machine.                                         |
|             | MACHINE_IP  | IP address of the machine.                                   |

### ToolAction

Tools started as the result of a `ToolAction` receive the command line and the list o variables.

If the `Args` attribute of the `ToolAction` is specified, it **overwrites** the default command line arguments specified in the tool definition in `LocalConfig.xml`.

### ScriptAction

Scripts started as the result of a `ScriptAction` receive a `ScriptActionArgs`class as an argument.

| ScriptActionArgs members          |                                                              |
| --------------------------------- | ------------------------------------------------------------ |
| Args [string]                     | Argument string specified in the ScriptAction definition entry in SharedConfig. |
| VfsNode [VfsNodeDef]              | The full definition of the already fully resolved VfsNode, containing only physical files and virtual folders. Null if the action is not associated with a VfsNode. |
| Vars [Dictionary<string, string>] | Action variables described above.                            |

### Default Actions

Default actions defined in the LocalConfig.xml are added to the context menu of each respective item:

* File
* FilePackage
* App
* Machine
