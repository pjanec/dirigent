# Files, Folders and Packages (VfsNodes)

Dirigent lets you declare the interesting files of your system - application logs, config
files, crash dumps, data folders - in `SharedConfig.xml`, and then offers them in its UI for
viewing, browsing and downloading, no matter which machine they physically reside on.

Together these declarations form a **virtual file system** (VFS): a tree of nodes
(*VfsNodes*) mixing physical files, physical folders and purely virtual grouping folders,
possibly drawn from many machines at once.

Typical uses:

* Open the log file of one particular application from a single context menu click, without
  knowing which machine it runs on or where its log folder is.
* Download a zipped bundle of *"the recent logs of all applications"* from the whole system
  in one go.
* Browse a set of files scattered over several machines as if it were one folder tree.

Contents:

* [Concepts](#concepts)
  * [The three stages](#the-three-stages)
  * [Node types](#node-types)
  * [Association: app, machine or global](#association-app-machine-or-global)
  * [Where nodes can be declared](#where-nodes-can-be-declared)
  * [Resolution](#resolution)
  * [UNC paths and file shares](#unc-paths-and-file-shares)
* [XML reference](#xml-reference)
  * [Attributes common to all node types](#attributes-common-to-all-node-types)
  * [`<File>`](#file)
  * [`<File Filter="Newest">`](#file-filternewest)
  * [`<Folder>`](#folder)
  * [`<VFolder>`](#vfolder)
  * [`<FilePackage>`](#filepackage)
  * [`<FileRef>`](#fileref)
  * [File masks](#file-masks)
  * [Path variables](#path-variables)
  * [Actions on nodes](#actions-on-nodes)
* [Built-in actions](#built-in-actions)
* [Where files appear in the UI](#where-files-appear-in-the-ui)
* [Examples](#examples)
* [Limitations and known issues](#limitations-and-known-issues)
* [See also](#see-also)

## Concepts

### The three stages

Understanding the VFS is mostly understanding that a node definition and an actual file are
two different things, separated in time and place.

| Stage           | What happens                                                                                                                                                       | Where / when                                                                                     |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------ |
| **Declaration** | The XML elements are parsed into node *definitions*. Nothing is looked up in any file system; paths may still contain variables, masks and filters.                 | On the master, when `SharedConfig.xml` is loaded. Definitions are then sent to all GUIs.          |
| **Resolution**  | The definition tree is turned into a tree of *concrete* files: variables expanded, folders scanned, masks and filters applied, references followed, paths made UNC.  | On demand - when the user clicks a menu item. Each node is resolved on the machine that owns it.  |
| **Action**      | A tool or a script is started and receives the resolved tree (and/or the resolved paths as variables).                                                              | On the machine of the user who clicked, unless the action says otherwise.                         |

The consequence worth remembering: **a node definition is a recipe, not a file list**. A
`<Folder>` or a `Filter="Newest"` node yields whatever exists at the moment of the click.

### Node types

| Element         | Container? | Description                                                                                                                                                          |
| --------------- | ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `<File>`        | no         | A single physical file. With `Filter="Newest"` it becomes a *recipe* picking recent files out of a folder.                                                            |
| `<Folder>`      | yes        | A physical folder. On resolution its content is scanned recursively and turned into a subtree of files and subfolders, filtered by `Mask`.                             |
| `<VFolder>`     | yes        | Virtual folder. Has no counterpart in any real file system; it only groups other nodes and gives that group a name. Used to shape the structure of a bundle.          |
| `<FilePackage>` | yes        | A named bundle of nodes, meant to be downloaded or browsed as a whole. Structurally identical to `<VFolder>`; it differs only in getting the *package* default actions and in being intended as a top-level entry point. |
| `<FileRef>`     | -          | A reference to other node(s) declared elsewhere in the config, matched by `Id` / `MachineId` / `AppId`, wildcards allowed. This is the mechanism for collecting many apps' files into one package without repeating their definitions. |

Containers may contain any of the above, nested arbitrarily deep.

### Association: app, machine or global

Every node is associated with an **app**, with a **machine**, or with **nothing** (a *global*
node). The association is normally implied by *where in the XML the node is declared*, and can
be overridden with the `MachineId` / `AppId` attributes.

The association decides two things:

1. **Which context menu the node appears in** - that app's menu, that machine's menu, or, for
   global nodes, nowhere by itself (global nodes are reachable only through a `<FileRef>` or
   through `<MainMenu>`).
2. **In whose context the path is resolved** - which machine's file system is scanned, whose
   environment variables are expanded, and which app's `%APP_STARTUPDIR%` etc. are available.

So a node declared under an app running on machine `m1` has its `%USERPROFILE%` expanded using
the environment of the Dirigent agent on `m1`, regardless of which machine the user clicked on.

### Where nodes can be declared

| Declaration site         | Resulting association                             | Appears in                                                |
| ------------------------ | ------------------------------------------------- | --------------------------------------------------------- |
| inside `<App>`           | app + machine (from `AppIdTuple`)                 | that app's context menu                                   |
| inside `<AppTemplate>`   | app + machine of **each app using the template**  | the context menu of every app using the template          |
| inside `<Machine>`       | machine (app = none)                              | that machine's context menu                               |
| top level of `<Shared>`  | global                                            | nowhere directly - only via `<FileRef>` or `<MainMenu>`    |
| inside `<MainMenu>`      | global                                            | the GUI's main menu bar                                   |
| inside a container node  | inherited from the container, unless overridden   | wherever the container appears                            |

Declaring nodes in an `<AppTemplate>` is the main tool for keeping the config small: the
template is parsed once per app that uses it, with that app's ids, so one declaration yields
one correctly bound node per app. See the [examples](#examples).

Independently of the menus, the nodes declared **directly** under `<Shared>`, `<Machine>`,
`<App>` or `<AppTemplate>` are put into a flat registry. That registry is what `<FileRef>`
searches, so a globally declared package is perfectly usable even though it appears in no menu
on its own.

Nodes **nested inside a container** are part of that container's content but are *not* in the
registry, so they cannot be targeted by a `<FileRef>`. Declare a node at one of the levels
above if you want to reference it from several packages.

### Resolution

Resolution is what turns definitions into files. It is triggered by clicking a menu item and
proceeds recursively:

* If the node belongs to another machine, resolution of that node is **delegated to that
  machine** - Dirigent runs the built-in script `BuiltIns/ResolveVfsPath.cs` there and takes
  the result. If that machine is not connected, resolution fails with
  `Machine <id> not connected.`
* Global nodes (no machine) are resolved locally; their paths must therefore already be UNC.
* `<File>`: variables in `Path` are expanded; a relative path is made absolute against the
  *root for relative paths* (by default the folder containing `SharedConfig.xml`, see the
  `rootForRelativePaths` option). With `Filter="Newest"` the folder is scanned and the
  matching files selected.
* `<Folder>`: the folder is scanned - subfolders recursively, files filtered by `Mask` - and
  becomes a subtree of virtual folders and files.
* `<VFolder>` / `<FilePackage>`: children are resolved one by one.
* `<FileRef>`: the registry is searched; see [`<FileRef>`](#fileref) for the outcome.
* Cycles are detected: a node already visited on the current path resolves to nothing.

The result is a tree containing only virtual folders and concrete file paths. Paths are
returned from the perspective of the *requesting* machine: files on other machines, and all
global files, come back as UNC paths; files on the requestor's own machine come back as plain
local paths.

### UNC paths and file shares

Turning a remote local path such as `D:\Logs\app.log` on `m1` into something the requestor can
open requires a matching file share declared for that machine:

```xml
<Machine Name="m1" IP="192.168.0.11">
    <Share Name="D$" Path="D:\"/>
</Machine>
```

The shares of the machine are searched for the first one whose `Path` is a prefix of the file
path (case insensitive); that prefix is then replaced by `\\<machine IP>\<share name>\`, giving
`\\192.168.0.11\D$\Logs\app.log`. Note it is the first match, not the longest one, so avoid
declaring overlapping shares. Share paths must be absolute. If no share covers the path,
resolution fails with `Can't construct UNC path, No file share matching ...`.

Dirigent expects these shares to need **no extra credentials**. If credentials are required,
the user must have entered them beforehand so that Windows can reuse the cached ones.

## XML reference

### Attributes common to all node types

| Attribute   | Description                                                                                                                                                                                                              |
| ----------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Id`        | Human readable id, used by `<FileRef>` to find the node. Need not be unique - matching several nodes with one reference is a feature. Defaults to the node's `Guid`.                                                       |
| `Title`     | Text shown in the menu. Backslash- or slash-separated segments create submenus (`"Logs/Recent"`). Also used as the **folder name inside a downloaded archive**. Falls back to `Id` when not given. Not variable-expanded.  |
| `Guid`      | Explicit GUID identity of the node. Generated automatically when omitted. Useful only if you want a stable identity across config reloads.                                                                                |
| `MachineId` | Overrides the machine association inherited from the declaration site. Empty string means *no machine* and, in `<FileRef>`, *any machine*.                                                                                |
| `AppId`     | Overrides the app association inherited from the declaration site. Empty string means *no app* and, in `<FileRef>`, *any app*.                                                                                            |
| `AppIdTuple` | Shorthand setting both of the above at once, in the `"machineId.appId"` format. Without a dot it sets the app only, leaving the machine empty. `MachineId` / `AppId` given alongside it still win.                        |
| `Icon`      | Icon image shown next to the menu item.                                                                                                                                                                                  |
| `Groups`    | Semicolon-separated group paths, as elsewhere in the config.                                                                                                                                                              |

Any node may contain `<Tool>` and `<Script>` child elements - see
[Actions on nodes](#actions-on-nodes).

### `<File>`

A single physical file.

```xml
<!-- app-bound: path resolved on the machine where the app runs -->
<File Id="log" Title="Log file" Path="%APP_STARTUPDIR%\logs\app.log">
    <Tool Title="Open in Notepad++" Name="Notepad++" Args="%FILE_PATH%"/>
</File>

<!-- global: must be a UNC path -->
<File Id="masterCfg" Path="\\server\share\SharedConfig.xml"/>
```

| Attribute | Description                                                                                                                                                                                                                                     |
| --------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Path`    | **Mandatory.** Full path to the file, or - with `Filter="Newest"` - to the folder to search. May contain environment variables and [path variables](#path-variables). A relative path is taken against the root for relative paths.               |
| `Filter`  | Optional resolution filter. Only `Newest` is implemented, see below.                                                                                                                                                                             |

A `<File>` with **no machine association** - declared at the top level of `<Shared>`, in
`<MainMenu>`, or with `MachineId=""` - must be given a UNC path. The config reader rejects
anything else, since there would be no machine on which to resolve it.

### `<File Filter="Newest">`

With `Filter="Newest"`, `Path` denotes a **folder** and the node resolves to the most recent
file(s) in it. This is the recipe to use for log files whose names contain a timestamp, and the
only node type that can restrict results by age.

```xml
<!-- the single newest *.log file -->
<File Id="log" Title="Newest log" Path="%APP_STARTUPDIR%\logs" Mask="*.log" Filter="Newest"/>

<!-- up to 10 log files, none older than 2 days (2*24*3600 = 172800 s) -->
<File Id="log" Title="Recent logs" Path="%APP_STARTUPDIR%\logs"
      Mask="*.log" Filter="Newest" MaxFiles="10" MaxSeconds="172800"/>
```

| Attribute    | Default  | Description                                                                                                        |
| ------------ | -------- | ------------------------------------------------------------------------------------------------------------------ |
| `Mask`       | all files | File mask, see [File masks](#file-masks). Applied to the file names in the folder itself; never recursive.         |
| `MaxFiles`   | `1`      | Maximum number of files to return. Values below 1 are treated as 1.                                                |
| `MaxSeconds` | no limit | Maximum age in seconds, measured from the file's last-write time. `0` means *no limit*.                             |

The files are taken **newest first**, so the age, count and size limits always keep the most
recent files.

The shape of the result depends on `MaxFiles`:

* `MaxFiles="1"` (the default) - the node resolves to a **single file** - the newest matching one -
  or to *nothing* if the folder holds no matching file.
* `MaxFiles` greater than 1 - the node resolves to a **virtual folder** named after the node's
  `Title`, holding up to that many files, newest first.

### `<Folder>`

A physical folder, resolved into a subtree of its content.

```xml
<Folder Id="logDir" Title="Log folder" Path="D:\Logs" Mask="*.log">
    <Tool Title="Open in Explorer" Name="WinExplorer" Args="%FILE_PATH%"/>
</Folder>

<!-- the recent part of a log tree that keeps growing: at most 200 files,
     none older than 2 days, at most 50 MB altogether -->
<Folder Id="logTree" Title="Recent logs" Path="D:\Logs" Mask="**/*.{log,txt}"
        MaxSeconds="172800" MaxFiles="200" MaxTotalBytes="52428800"/>
```

| Attribute       | Default   | Description                                                                                                         |
| --------------- | --------- | ------------------------------------------------------------------------------------------------------------------- |
| `Path`          | -         | **Mandatory.** Folder path; same expansion rules as `<File>`.                                                        |
| `Mask`          | all files | File mask, see [File masks](#file-masks). Matched against the paths relative to this folder.                          |
| `MaxSeconds`    | no limit  | Maximum age in seconds, measured from the file's last-write time. `0` = whatever age.                                 |
| `MaxFiles`      | no limit  | Maximum number of files to include. `0` = unlimited.                                                                 |
| `MaxTotalBytes` | no limit  | Maximum total size of the included files, in bytes. `0` = unlimited. One file is always included, however big.        |

Subfolders are descended into without a depth limit. The resulting tree mirrors the location of
the matching files, and contains no folders that ended up empty.

When a count or size limit applies, the **newest** files are kept - the limits are there to keep
a growing log folder from producing an unbounded download. Note that `MaxSeconds` on a `<Folder>`
filters the files it contributes; to pick just the few newest files of one folder, use
[`<File Filter="Newest">`](#file-filternewest) instead.

### `<VFolder>`

A virtual folder: a name and a list of children, with no counterpart in any file system. `Path`
is ignored. Its `Title` becomes the folder name inside a downloaded archive, which makes
`<VFolder>` the tool for shaping a bundle's structure.

```xml
<VFolder Title="Vision">
    <FileRef Id="log" MachineId="*" AppId="camera*"/>
    <File Id="visionCfg" MachineId="m1" Path="D:\cfg\vision.xml"/>
</VFolder>
```

### `<FilePackage>`

A named bundle, intended as a top-level entry point for downloading or browsing. It accepts the
same children as `<VFolder>`.

```xml
<FilePackage Id="logs.all" Title="Logs/All apps (recent)">
    <FileRef Id="log" MachineId="*" AppId="*"/>
</FilePackage>
```

Packages receive the *package* default actions from `LocalConfig.xml`
(`<DefaultFilePackageActions>`), which is where *Download zipped package* and *Browse* come from
in the shipped example config.

### `<FileRef>`

A reference to node(s) declared elsewhere. `Path` is not used; the node is found by matching
`Id`, `MachineId` and `AppId` against the registry of all nodes in the config.

```xml
<!-- every node with Id="log", on any machine, in any app or none -->
<FileRef Id="log" MachineId="*" AppId="*"/>

<!-- only machine m1's copies -->
<FileRef Id="log" MachineId="m1" AppId="*"/>

<!-- all vision-subsystem logs, by Id naming convention -->
<FileRef Id="log.vision.*" MachineId="*" AppId="*"/>
```

Matching rules, applied per field:

| Pattern                 | Matches                                                                    |
| ----------------------- | -------------------------------------------------------------------------- |
| omitted or `""` (empty) | anything, including nodes where the field is unset                         |
| `*`                     | anything, including nodes where the field is unset                         |
| `abc`, `abc*`, `a?c`    | Win32-style name match; does **not** match nodes where the field is unset   |

Remember that an omitted `MachineId` / `AppId` on a `<FileRef>` means *inherited from the
declaration site*, not *empty* - a `<FileRef Id="log"/>` written inside an `<App>` element
inherits that app's ids and therefore matches only that app's node. To broaden the search from
inside an app or machine context, state `MachineId="*" AppId="*"` (or `""`) explicitly.

Results:

* no match - resolves to nothing, silently;
* one match - resolves to that node, as if it had been written in place;
* several matches - resolves to a virtual folder named after the reference's `Title` (or `Id`),
  containing all the matches.

### File masks

The `Mask` attribute of `<Folder>` and of `<File Filter="Newest">` is a glob-style pattern:

| Syntax  | Meaning                                                                                  |
| ------- | ---------------------------------------------------------------------------------------- |
| `*`     | any number of characters within a single path segment                                     |
| `?`     | a single character                                                                        |
| `**`    | any number of path segments; must form a whole segment of the pattern                     |
| `{a,b}` | alternatives, expanded into separate patterns before matching; may be nested              |

Matching is case insensitive. Both `/` and `\` work as separators. Two conventions make the
common cases short:

* A mask containing **no separator** is matched against the file name at **any depth**, so
  `Mask="*.log"` on a `<Folder>` finds the log files in the whole tree.
* An **empty** mask, and the Win32-style `*.*`, both mean *every file* - including the files
  with no extension.

Examples:

| Mask                      | Matches                                                              |
| ------------------------- | -------------------------------------------------------------------- |
| `*.log`                   | any `.log` file at any depth                                          |
| `*.{log,txt}`             | any `.log` or `.txt` file at any depth                                |
| `logs/*.log`              | `.log` files directly in the `logs` subfolder                         |
| `logs/**/*.log`           | `.log` files anywhere under the `logs` subfolder                      |
| `**/crash-??.dmp`         | e.g. `a/b/crash-01.dmp`                                               |
| `logs/**`                 | everything under `logs`                                               |

### Path variables

`Path` is expanded on the machine that owns the node, using that machine's agent environment, so
ordinary environment variables (`%TEMP%`, `%USERPROFILE%`, `%ProgramData%`, ...) work as
expected. In addition:

| Association | Variable                              | Value                                                            |
| ----------- | ------------------------------------- | ---------------------------------------------------------------- |
| any         | `MACHINE_ID`, `DIRIGENT_MACHINE_ID`   | Name of the machine owning the node.                             |
|             | `MACHINE_IP`, `DIRIGENT_MACHINE_IP`   | IP address of that machine.                                      |
|             | `DOWNLOADS`                           | The download folder of the user the agent runs as, on that machine. |
| app-bound   | `APP_ID`, `DIRIGENT_APPID`            | App name without the machine part.                               |
|             | `APP_BINDIR`                          | Folder holding the app's executable (from `ExeFullPath`).        |
|             | `APP_STARTUPDIR`                      | The app's startup folder (from `StartupDir`).                    |
|             | *(the app's own `<Env>` variables)*   | Everything the app would get in its environment when launched.   |

### Actions on nodes

What can be *done* with a node is expressed by `<Tool>` and `<Script>` child elements - see
[Actions](Actions.md) for the general mechanism and [Tools](Tools.md) / [Scripts](Scripts.md)
for the two kinds.

```xml
<File Id="log" Title="Log file" Path="%APP_STARTUPDIR%\logs\app.log">
    <Tool   Title="Open in Notepad++"    Name="Notepad++" Args="%FILE_PATH%"/>
    <Script Title="Download zipped file" Name="BuiltIns/DownloadZipped.cs" Icon="Icons/Zipped.png"/>
</File>
```

Actions are started **after** the node has been resolved, and receive the result:

| Node resolves to                            | `%FILE_PATH%` contains                                        | Script's `VfsNode` argument |
| ------------------------------------------- | ------------------------------------------------------------- | --------------------------- |
| a single file                               | the resolved path (UNC if not on the requestor's machine)      | the resolved file node      |
| a container with a real path (`<Folder>`)   | the resolved folder path                                      | the whole resolved tree     |
| a container with no real path (package, virtual folder) | all contained file paths, each double-quoted, space separated | the whole resolved tree |

Actions that should be offered on *every* file or *every* package need not be repeated: put them
in `LocalConfig.xml` under `<DefaultFileActions>` and `<DefaultFilePackageActions>`. Which of the
two applies is decided **after** resolution, from whether the node resolved to a container.

## Built-in actions

### `BuiltIns/DownloadZipped.cs` - download as ZIP

The workhorse for getting files off the system. Applicable to any node - a single file, a folder,
a package.

What it does:

1. Resolves the node and collects the set of machines owning files in it.
2. Skips the machines that are not currently connected. If none is left, it says so and stops
   instead of producing an empty archive.
3. Starts a slave script (`BuiltIns/DownloadZippedSlave.cs`) on each of the remaining machines.
   Each slave copies **its own local files** into a temp tree mirroring the virtual folder
   structure, zips it, and uploads the archive over UNC to a staging folder next to the
   requestor's download folder. Files belonging to no machine (the global ones) are handled by
   the first machine in the list.
4. Runs `BuiltIns/MergeZipped.cs` on the requestor's machine, which joins the uploaded archives
   into the final one and removes the staging folder.
5. Shows a message box naming the archive, plus any errors collected along the way - a missing
   file, or even a whole machine failing, does not abort the download. Confirming the dialog
   opens the containing folder in Explorer.

The resulting archive is named `<Title>_<yyMMdd_HHmm>.zip` and is laid out like this:

```
Incident report_260827_1432.zip
   m1/                            <- machine, omitted when only one takes part
      AppLogs/                    <- Title of the VFolder
         camera/                  <- app the files belong to
            app.log
         tracker/
            app.log
   m2/
      AppLogs/
         recorder/
            app.log
```

Notable properties:

* Folders are named after the `Title` of the containing node. Files belonging to an app
  additionally go into a subfolder named after the app, so that the same-named log files of
  several apps do not clash. Remaining name clashes get a `_2`, `_3`, ... suffix.
* The files land in the download folder of the **machine the requestor runs on**, determined from
  the agent connected from the same address as the requesting GUI. That folder must be reachable
  by UNC from every participating machine, so the requestor's machine also needs a `<Share>`
  covering it.
* Each machine compresses its own part, so what travels over the network is already compressed.
  The merging step then repacks the parts on the requestor's machine, which costs some CPU there
  but keeps the transfer small.
* `Args="perMachine"` on the action gives one archive per machine instead - named
  `<Title>_<yyMMdd_HHmm>_<machine>.zip`, delivered without any merging step:

  ```xml
  <Script Title="Download zipped (per machine)" Name="BuiltIns/DownloadZipped.cs" Args="perMachine"/>
  ```

### `BuiltIns/BrowseInDblCmdVirtPanel.cs` - browse in Double Commander

Resolves the node, writes a Double Commander *virtual panel* list describing the resolved tree,
and opens Double Commander on it - letting the user walk a tree of files gathered from several
machines as if it were one folder.

Requires a `DoubleCommander` tool defined in `LocalConfig.xml`, pointing to a build that supports
the `--startupscript` option (see <https://github.com/pjanec/doublecmd>).

## Where files appear in the UI

Currently the VFS is exposed by the **WinForms GUI** only:

* **App context menu** - the nodes declared for that app, including those inherited from its
  `<AppTemplate>`.
* **Machine context menu** - the nodes declared for that machine.
* **Main menu** - the nodes declared in `<MainMenu>`; the natural home for whole-system packages,
  since they belong to no single app or machine.
* **Files tab** - all the declared nodes in one sortable, filterable grid: machine, app, id, type,
  path and status. The right-click menu offers the node's actions, the same as in the context
  menus above.

Each node contributes one menu item carrying its actions as a submenu. `Title` segments
(`"Logs/Recent"`) become submenu levels, so a set of packages can be organised into a menu tree.

The *Status* column of the Files tab shows `<machine> offline` for the nodes whose machine is not
currently connected. Choosing **Resolve** from the right-click menu (or double-clicking the row)
resolves that one node and reports what it currently points to - `Found`, `Missing`, `Not found`,
the number of files of a container, or the error - and rewrites the *Path* cell with the resolved
path(s). Resolution touches the remote file systems, so it happens only when asked for, never on
the periodic refresh.

The ImGui GUI does not render VFS nodes, and there is no CLI or HTTP verb for files; a
`StartScript` invocation of a script that builds its own node set is the only non-GUI route.

## Examples

### One log node for every app, declared once

Because an `<AppTemplate>` is parsed once per app that uses it - with that app's machine and app
id - a single declaration in the template yields one properly bound node per app:

```xml
<AppTemplate Name="apps.base" ... >
    <!-- up to 10 *.log files from the app's log folder, none older than 2 days -->
    <File Id="log" Title="Recent logs" Path="%APP_STARTUPDIR%\logs"
          Mask="*.log" Filter="Newest" MaxFiles="10" MaxSeconds="172800">
        <Tool   Title="Open newest in Notepad++" Name="Notepad++" Args="%FILE_PATH%"/>
        <Script Title="Download zipped"          Name="BuiltIns/DownloadZipped.cs"/>
    </File>
</AppTemplate>

<App AppIdTuple="m1.camera"   Template="apps.base" ExeFullPath="..." StartupDir="D:\apps\camera"/>
<App AppIdTuple="m1.tracker"  Template="apps.base" ExeFullPath="..." StartupDir="D:\apps\tracker"/>
<App AppIdTuple="m2.recorder" Template="apps.base" ExeFullPath="..." StartupDir="E:\apps\recorder"/>
```

Every one of those apps now has *Recent logs* in its context menu, resolved against its own
startup folder on its own machine.

### Bundles built from those nodes

Nothing needs to be declared twice: `<FileRef>` collects the existing nodes by id.

```xml
<!-- everything, from every machine -->
<FilePackage Id="logs.all" Title="Logs/All apps (2 days)">
    <FileRef Id="log" MachineId="*" AppId="*"/>
</FilePackage>

<!-- one machine only -->
<FilePackage Id="logs.m1" Title="Logs/Machine m1">
    <FileRef Id="log" MachineId="m1" AppId="*"/>
</FilePackage>

<!-- per subsystem, using an Id naming convention (log.vision.camera, log.vision.tracker, ...) -->
<FilePackage Id="logs.vision" Title="Logs/Vision subsystem">
    <FileRef Id="log.vision.*" MachineId="*" AppId="*"/>
</FilePackage>
```

### Making the bundles reachable

Packages declared at the top level of `<Shared>` appear in no menu. Reference them from
`<MainMenu>` so they sit in the GUI's menu bar:

```xml
<MainMenu>
    <FileRef Title="File/Logs/All apps (2 days)" Id="logs.all"/>
    <FileRef Title="File/Logs/Machine m1"        Id="logs.m1"/>
</MainMenu>
```

Alternatively declare the `<FilePackage>` directly inside `<MainMenu>`.

### Shaping the archive

A bundle mixing several kinds of material reads better with explicit `<VFolder>` levels; their
titles become folder names inside the ZIP:

```xml
<FilePackage Id="incident" Title="Logs/Incident report">
    <VFolder Title="AppLogs">
        <FileRef Id="log" MachineId="*" AppId="*"/>
    </VFolder>
    <VFolder Title="Config">
        <FileRef Id="cfg" MachineId="*" AppId="*"/>
    </VFolder>
    <VFolder Title="SystemLogs">
        <File Id="dirigentLog" MachineId="m1" Path="%ProgramData%\Dirigent\logs"
              Filter="Newest" Mask="*.log" MaxFiles="3"/>
    </VFolder>
</FilePackage>
```

Clicking *Download zipped package* then yields one archive `Incident report_260827_1432.zip`
holding a folder per participating machine, each containing `AppLogs\`, `Config\` and
`SystemLogs\`.

### One file of one app, opened directly

The everyday case - no packages involved:

```xml
<App AppIdTuple="m1.camera" ... >
    <File Id="cfg" Title="Config file" Path="%APP_STARTUPDIR%\camera.xml">
        <Tool Title="Edit in Notepad++" Name="Notepad++" Args="%FILE_PATH%"/>
    </File>
</App>
```

## Limitations and known issues

Behaviour of the current implementation that the rest of this document would not lead you to
expect.

**Merging repacks the archives.** `System.IO.Compression` has no raw entry-copy API, so joining
the per-machine parts decompresses and recompresses them on the requestor's machine. The
transfer over the network stays compressed, but a very large download costs some CPU and disk
there. `Args="perMachine"` skips the merging altogether.

**The merging machine must run an agent.** The merge happens on the machine the download goes
to. If the requesting GUI runs on a machine with no agent, the download - and the merging -
falls back to the machine running the master.

**No non-GUI access.** Files are reachable from the WinForms GUI only. The ImGui GUI does not
render VFS nodes, and there is no CLI or HTTP verb for files; a `StartScript` invocation of a
script that builds its own node set is the only non-GUI route.

**Nodes nested in a container are not referenceable.** Only the nodes declared directly under
`<Shared>`, `<Machine>`, `<App>` or `<AppTemplate>` can be found by a `<FileRef>`. See
[Where nodes can be declared](#where-nodes-can-be-declared).

**Silent misses.** A node that resolves to nothing - an unmatched `<FileRef>`, a
`Filter="Newest"` folder with no matching file - makes its menu item do nothing at all when
clicked, without a message. The Files tab is the way to tell: its **Resolve** command reports
`Not found` for the same node.

**Unknown config content is accepted silently.** Unrecognised elements and attributes are
skipped without any warning, deliberately, so that a config written for a newer Dirigent still
loads. The flip side is that a typo in an element or attribute name is not reported either - it
just has no effect. Note that `<ScriptedContent>`, which appears in the example config, falls
into this category: it is not implemented.

**`<Folder>` limits are per node, not per download.** A package pulling in several `<Folder>`
nodes can still add up to a large archive, as `MaxTotalBytes` bounds each node separately.

**There is no "list file" variable.** `%FILEPACKAGE_LIST_FILE%` / `%PACKAGE_LIST_FILE%`, used by
some tool examples in the config, are not provided and expand to nothing. Container actions get
`%FILE_PATH%` with the quoted list of paths instead.

**The download folder is derived from the requestor's address.** The machine to download to is
found by looking for the agent connected from the same address as the requesting GUI. If the GUI
runs on a machine with no agent, the files land in the download folder of the machine running
the master, and a warning is logged.

**`%DOWNLOADS%` follows the agent's user.** It is read from the registry of the user the agent
process runs as, which is not the interactive user if the agent runs as a service under a
different account.

**Browsing needs a patched Double Commander.** See
[the browse action](#builtinsbrowseindblcmdvirtpanelcs---browse-in-double-commander).

**A "Collection modified" balloon may appear after a download** (`TODO.md`).

## See also

* [Actions](Actions.md) - the general action mechanism and the full list of action variables
* [Tools](Tools.md) - defining the tool applications that actions start
* [Scripts](Scripts.md) - writing scripts, including ones that consume a resolved node tree
* [SharedConfig](SharedConfig.md) - the surrounding configuration file
* [LocalConfig](LocalConfig.md) - tool definitions and default actions
* [`config/SharedConfig.xml`](../config/SharedConfig.xml) - a working example of most of the above
