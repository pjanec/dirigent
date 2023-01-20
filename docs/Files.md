# Files and packages (VfsNodes)

Files and packages are user definable files, folders or packages of these two (virtual folders). They are referred to as Vfs Nodes (Virtual File System nodes) in the text below.

VfsNodes are shown in different context menus around the Dirigent's UI. They allow for quick and easy manipulation like for example:

* Examining individual apps/machine files.
  * For example a log file.

* Automatic download of a a set of files from multiple different machines at once.
  * For example a selection of interesting files from the whole system.


#### VfsNode types

| VfsNode        | Description                                                  |
| -------------- | ------------------------------------------------------------ |
| File           | Physical file.                                               |
| Folder         | Physical folder containing physical files and subfolders; by default all included recursively. |
| FilePackage    | A set of files, folders, references to files etc. May include virtual subfolders. The main purpose of the FilePackage is to bundle files from possibly multiple different machines into  one package for easy download/browsing. |
| VFolder        | Virtual folder. Used for organizing the content of a package into subfolders. Can contain same stuff as the package. |
| FileRef        | Reference to a file/folder/package defined on another place in SharedConfig. |

See examples in [SharedConfig.xml](../config/SharedConfig.xml).




TODO: XML attrib specs

### 

#### Association with apps and machines

VfsNodes are defined in `SharedConfig.xml` at various places - under `App` section, under `Machine` section, or in the top level `Shared` section.

This makes each VfsNode associated with an app, with a machine, or with neither or these, being a global file/package.

This association determines

* In what context menu the VfsNode shows up
* In what context the file path of the VfsNode will be resolved.

#### Path resolution

For example if there is an environment variable used in the path (like for example `"%USERPROFILE%\Documents\Greetings.txt"`), it will be resolved remotely on the machine where the file is local.

So if the file was defined for an application running on machine "m1", the path will be resolved from the perspective of "m1" machine, no matter on what machine the user made the request.

The following special variables are defined for expansion in the `Path` attribute:

| Associate | Variable       |                                                              |
| --------- | -------------- | ------------------------------------------------------------ |
| App       | MACHINE_ID     | Name of the machine where the app is located.                |
|           | MACHINE_IP     | IP address of the machine where the app is located.          |
|           | APP_BINDIR     | Folder path where the app's exe file resides (`ExeFullPath` in `App` section in SharedConfig) |
|           | APP_STARTUPDIR | Folder path where the app is started in (`StartupDir` in `App` section in SharedConfig) |
|           |                |                                                              |
| Machine   | MACHINE_ID     | Name of the machine.                                         |
|           | MACHINE_IP     | IP address of the machine.                                   |

#### UNC Paths and File Shares

The resulting path in the target machine's local file system is converted to an UNC path to be directly accessible from the requestor's machine - like for example for opening the file for viewing.

Note: The UNC conversion requires the File Shares to be defined in the SharedConfig.xml for each machine.

Warning: Dirigent expect these file shares not requiring any extra credentials. If credential are needed, the user needs to enter them beforehand and let the Windows cache/reuse them.
