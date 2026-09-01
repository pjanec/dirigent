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

This page is the conceptual and reference documentation. For a task-oriented walk-through of the
most common use - gathering the logs of many applications from many machines into one downloadable
bundle - start with [Collecting Log Files from Multiple Machines](LogFileCollection.md).

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
  * [Files too big to collect whole](#files-too-big-to-collect-whole)
  * [Collecting one test run: Clear, Mark and Unmark](#collecting-one-test-run-clear-mark-and-unmark)
  * [`<VFolder>`](#vfolder)
  * [`<FilePackage>`](#filepackage)
  * [`<FileRef>`](#fileref)
  * [File masks](#file-masks)
  * [Path variables](#path-variables)
  * [Actions on nodes](#actions-on-nodes)
* [Built-in actions](#built-in-actions)
* [Where files appear in the UI](#where-files-appear-in-the-ui)
* [Files without a GUI](#files-without-a-gui)
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

The shares of the machine are searched for the one whose `Path` covers the file path (case
insensitive); that prefix is then replaced by `\\<machine IP>\<share name>\`, giving
`\\192.168.0.11\D$\Logs\app.log`. Where several shares cover it, the **most specific** one wins,
the way a mount table works: with both `D:\` and `D:\Logs` declared, a file under `D:\Logs` goes
through the `D:\Logs` share. The match ends at a folder boundary, so a share at `D:\Logs` does not
cover `D:\LogsBackup`. Share paths must be absolute. If no share covers the path, resolution fails
with `Can't construct UNC path, No file share matching ...`.

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
| `Clearable` | Whether **Clear** and **Mark** may touch the files this node yields. `0` (the default) means they never do, whatever any action or argument says - see [Collecting one test run](#collecting-one-test-run-clear-mark-and-unmark). |
| `Description` | What this node is, in prose. Shown where the user has to decide something about it - see [Asking for a comment](#asking-for-a-comment). Being an attribute, a line break in it is written `&#10;`.                       |

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
| `TailBytes` | Collect only the last this many bytes of the file, if it is bigger than that. `0` (the default) collects whole files. See [Files too big to collect whole](#files-too-big-to-collect-whole).                                                     |

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
| `TailBytes`  | 0 = whole files | Collect only the last this many bytes of a file bigger than that. See [Files too big to collect whole](#files-too-big-to-collect-whole). |

`MaxTotalBytes` is **not** implemented here - it applies to `<Folder>` only, and is ignored
without a word if written on a `Newest` node. Bound the result with `MaxFiles` instead.

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
| `MaxTotalBytes` | no limit  | Maximum total size of the included files, in bytes. `0` = unlimited. One file is always included, however big. A file that does not fit is passed over - the walk continues, so the smaller files behind it are still collected - and the archive gets an `_incomplete.txt` naming what was left out. With `TailBytes` set, what counts against this budget is the size of the tail, not of the file. |
| `TailBytes`     | 0 = whole files | Collect only the last this many bytes of a file bigger than that, for every file this folder yields. See [Files too big to collect whole](#files-too-big-to-collect-whole). |

Subfolders are descended into without a depth limit. The resulting tree mirrors the location of
the matching files, and contains no folders that ended up empty.

When a count or size limit applies, the **newest** files are kept - the limits are there to keep
a growing log folder from producing an unbounded download. Note that `MaxSeconds` on a `<Folder>`
filters the files it contributes; to pick just the few newest files of one folder, use
[`<File Filter="Newest">`](#file-filternewest) instead.

### Files too big to collect whole

A logger that never rotates grows a single file to tens of gigabytes. Such a file cannot be
downloaded at all - and what an investigation wants is almost always its end. `TailBytes` says
to collect only that:

```xml
<!-- the last 50 MB of any file in the tree that is bigger than that -->
<Folder Id="logs" Title="Logs" Path="D:\Logs" Mask="*.log" TailBytes="52428800"/>

<!-- one known-huge file -->
<File Id="trace" Title="Trace" Path="D:\Logs\trace.log" TailBytes="10485760"/>
```

What happens to a file over the limit:

* Only its last `TailBytes` bytes are read and compressed - the rest is never touched, so
  collecting the tail of a 60 GB file costs the same as collecting a 50 MB one.
* The cut is moved forward to the **next line break**, so the first line is a whole line rather
  than a fragment. A file with no line break near the cut - a binary one - is cut at the exact
  offset instead.
* The entry is named `<name>.last<size><ext>` - `app.last50MB.log` - so the archive listing alone
  shows which files are partial.
* Its **first line** states which file it came from, how big that file was, and when the tail was
  taken. An archive read months later has nothing else to go by.
* The archive also gets an `_incomplete.txt` at its root, listing every file that was truncated
  or left out entirely.
* A `MaxTotalBytes` budget counts the size of the tail, not of the file, so a folder full of huge
  tailed logs is affordable rather than looking impossible.

A file **below** the limit is collected whole, under its own name. `TailBytes` applies to the node
that declares it - a `<File>`, or every file a `<Folder>` yields - and is deliberately **not**
inherited by the children of a `<VFolder>` or `<FilePackage>`, which are nodes in their own right
and may well live on another machine.

The tail is a property of collecting into an archive. Opening or browsing the node still points at
the real, whole file; `%FILE_PATH%` is unaffected. And since a live log keeps growing, the tail is
a snapshot: the bytes taken are the last ones as of the moment of collection.

### Collecting one test run: Clear, Mark and Unmark

Somebody who can reproduce a problem wants two clicks around the run: one before it, one after,
and an archive holding only what that run produced. Without the first click the collection takes
whatever is in the log files, which on a long-running system is mostly somebody else's afternoon.

The mechanism is a **high-water mark**: the first click records how long each file is, and the
download takes only what came after. It works on a file a logger is holding open, corrupts
nothing, and destroys no history.

#### Why a mark rather than emptying the file

Measured against a logger that holds its file open - the normal state of a running system:

| the logger permits | delete | truncate to zero | read the length |
| --- | --- | --- | --- |
| `Read` (the usual case) | **fails** | **fails** | works |
| `ReadWrite` | **fails** | succeeds, but see below | works |
| `ReadWrite \| Delete` | succeeds, but see below | succeeds, but see below | works |

Where truncation succeeds the logger keeps writing at its old offset, so the file comes back as a
run of NUL bytes followed by the new line. Where deletion succeeds the file is unlinked while the
logger writes into it, and everything logged afterwards goes somewhere nobody can find. Reading
the length always works and changes nothing.

#### `Clearable`: nothing is touched unless it says so

A package worth collecting is rarely only logs - the interesting ones hold the applications'
logs *and* their configuration files, and one archive containing both is the point. So the
permission lives on the node, and it is **off by default**:

```xml
<File Id="log" Title="Log/IgManager" Path="D:\Logs\IgManager"
      Filter="Newest" MaxFiles="10" Clearable="1"/>

<File Id="cfg.dds" Title="Config/cyclonedds.xml" Path="%APP_STARTUPDIR%\cyclonedds.xml"/>
```

`cfg.dds` above cannot be emptied by any click, action or argument.

The flag gates **marking as well as clearing**, deliberately. Marking looks harmless, but marking
a configuration file would mean the next collection takes only the bytes appended since - usually
none - so the file would silently arrive empty. A flag that allowed marking but not clearing would
trade a loud failure for a quiet one. Read `Clearable` as the whole permission, of which marking is
the gentler half.

| kind of file | `Clearable` | why |
| --- | --- | --- |
| application log, append-only | `1` | the run boundary is the whole point |
| crash dump folder | `1` | old dumps muddy a run, and nothing holds a dump open |
| configuration file | `0` (default) | it is the run's input, not its output |
| anything somebody may need whole | `0` | the default protects it |

Set on a `<Folder>`, it applies to every file that folder yields. Like `TailBytes`, it is **not**
inherited by the children of a `<VFolder>` or `<FilePackage>`.

#### The three operations

| action | menu | what it does to each **clearable** file in scope |
| --- | --- | --- |
| `BuiltIns/ClearFiles.cs` | **Clear** | empties it if that is safe, marks it otherwise |
| `BuiltIns/MarkFiles.cs` | **Mark** | records the mark only, touches no file |
| `BuiltIns/UnmarkFiles.cs` | **Unmark** | drops the mark, so the next download takes everything again |

**Clear** decides per file, and the decision is a measurement rather than a guess about names or
folders: it opens the file **exclusively**, which succeeds only when no other process has it open.

* The open **succeeds** - so the file is truncated inside that window and then deleted, and any
  mark on it is dropped. Truncate first, because once the exclusive handle is held the truncation
  cannot fail while the deletion still can - a read-only attribute, a folder's permissions - and an
  emptied file is cleared either way. The application recreates the file on its next write; a new
  file has no mark, so the download takes it whole, which is exactly "since the clear".
* The open **fails** - something is writing - so the file is marked instead, and the report says so.

**A Clear on a running system therefore leaves marks, and that is how it keeps its promise.** A log
being written to cannot be emptied, so the line under it is what makes the next download hold only
what came after the Clear. The count of marked files in the closing message says how many; the
archive names the operation - "written after the Clear of 15:32:10" - rather than talking about a
"mark" nobody asked for.

The only marks a Clear leaves behind are its own: it drops the mark of every file it empties, and the
marks of files that have vanished altogether are forgotten when the marks are next written. So after
a Clear, no mark can refer to a file that is not there.

**Mark** is the same operation with the destructive half removed, which is what a production site
wants: the run is delimited and the history survives. **Unmark** ignores `Clearable`, being the one
operation that can only ever make a later collection *more* complete.

Each operation reports per machine what happened - cleared, marked, skipped as not clearable, not
there, failed - in the status bar while it runs and in a message box when it ends. The *skipped*
count is what makes a forgotten `Clearable="1"` discoverable; without it a log would quietly keep
its old contents and the archive would look wrong for no visible reason.

All three work on a whole package, on a single node, and on one row of the Files tab - wherever the
action is declared.

#### What the download does with a mark

| what it finds | what it collects |
| --- | --- |
| no mark | the whole file - all of it is new |
| a mark, and the file is still the one that was marked | from the mark, cut at the next line break |
| a mark, but the file was replaced | the whole file, noting that it was replaced |
| a mark, but the file is shorter than the mark | the whole file, noting that it was truncated or rotated |

Rotation therefore yields slightly **more** than the window, never less: the fresh `app.log` is a
new file, so it arrives whole, and `app.log.1` was never marked, so it arrives whole too. Failing
towards too much is the right direction, and `_incomplete.txt` says why.

A partial entry is named `<name>.since-mark<ext>` - `app.since-mark.log` - and its first line
states the offset and the time and operation the line was drawn by, exactly as a `TailBytes` entry
states its own reason. `_comment.txt` gains a line naming the beginning of the window:

```
Since     : 2026-08-31 15:02:11 - 12 file(s) hold only what was written after the Clear of that time; the rest are complete.
```

It says `Clear` or `Mark`, whichever was run - or "Clear or Mark" where the files disagree, which
happens when part of the system was marked and part of it cleared.

Marks and `TailBytes` compose: a file starts at whichever cut is later, and the entry is named and
headed after whichever of the two was binding. Downloading does **not** consume the mark - two
downloads after one mark give the same window, which is what somebody re-downloading after a failed
transfer expects.

#### Where the marks live, and how a file is recognised

On each machine, in a small JSON file beside the agent status file (`--agentStatusFolder`), keyed by
**path** - so that "mark, then collect" holds however the collection happens to be assembled. Two
people marking overlapping packages means the later mark wins; harmless, and every partial entry
states the time of the mark it was cut at.

A mark also keeps the **last 32 bytes before the offset**, and a download compares them before
trusting the offset. That is not belt and braces: on Windows the obvious check does not hold. NTFS
*tunneling* restores the original creation time on a file deleted and recreated under the same name
within about fifteen seconds - which is exactly what a rotating logger does - so a rotated file
arrives wearing the marked file's creation time. Comparing the bytes checks the one thing that has
to be true for an offset to mean anything: that the boundary is still where it was put.

#### Narrowing what is acted on

The three scripts read their `Args` as a semicolon-separated list of node id patterns - the same
wildcards `<FileRef>` uses - and act only on the matching entries of the package:

```xml
<Script Title="Clear" Name="BuiltIns/ClearFiles.cs" Args="log*"/>
```

Empty `Args` means every clearable node in scope, which is the usual case. The patterns are matched
against each entry's `Id` **or** `Title`, because a `<FileRef>` matching several nodes resolves to a
folder carrying the reference's id as its title and no id of its own.

`Args` rather than a new attribute, because a list of node ids is the script's own argument.
(`AskComment` is the other way round - a directive the GUI reads before the script exists - which
is why that one is an attribute. The distinction is who consumes the value.)

#### What this deliberately will not do

* **Clear a file that is not `Clearable`.** No action, argument or package can override it.
* **Stop an application to free its log.** Dirigent could; a menu item that stops the system under
  test is not something anybody wants. A held file is marked instead.
* **Guess.** Nothing decides by file name, extension or folder whether emptying is safe.

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
   Each slave streams **its own local files** straight into a zip archive - the virtual folder
   structure becoming the entry names, nothing copied anywhere first - and it writes that archive
   directly into a staging folder next to the requestor's download folder, under a `.part` name
   renamed into place once complete. A slave running on the machine that owns that folder writes to it as a plain
   local path; the others go through the UNC path (see
   [the destination folder](#the-destination-folder) below). Files belonging to no machine (the
   global ones) are handled by the first machine that gets a slave started.
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
   _comment.txt                   <- what was collected, from where, why, and since when
   _incomplete.txt                <- only when something was truncated or left out
```

Notable properties:

* Folders are named after the `Title` of the containing node. Files belonging to an app
  additionally go into a subfolder named after the app, so that the same-named log files of
  several apps do not clash. Remaining name clashes get a `_2`, `_3`, ... suffix.
* That app subfolder is left out when a folder of the same name is already on the path, which is
  the case wherever a container is named after the app itself - a node titled or id'd like the app,
  or an untitled `<Folder>` over the app's own directory. Without it such archives read
  `log/cgfx/cgfx/app.log`.
* Anything the collection could not include in full - a file over a `MaxTotalBytes` budget, a file
  truncated by `TailBytes` - is listed in `_incomplete.txt` at the root of the archive, so that an
  incomplete collection can be told from a complete one long after the fact.
* Each collected file keeps its own modification time, which the zip format stores to a resolution
  of two seconds.
* The files land in the download folder of the **machine the requestor runs on**. A GUI carries
  that machine in its client name (`<machineId>_gui_<guid>`); for a client named some other way,
  the agent connected from the same address is used instead.
* Each machine compresses its own part, so what travels over the network is already compressed.
  The merging step then repacks the parts on the requestor's machine, which costs some CPU there
  but keeps the transfer small.
* Callers with no resolved node tree - the CLI, REST, another script - name the node with a
  `Node` selector in the arguments instead, and the script resolves it. See
  [Files without a GUI](#files-without-a-gui).
* `AskComment="1"` on the action asks the operator for a note before anything starts - see
  [Asking for a comment](#asking-for-a-comment).
* One archive is always produced. `Args="perMachine"`, which used to deliver one per machine and
  skip the merging, is **withdrawn**: analysis is normally cross-machine, so a single file is what
  the recipient wants, and one output shape keeps the rest of this page short. An action still
  carrying that word logs a warning and gets the single archive.

#### Asking for a comment

An action carrying `AskComment="1"` puts a dialog in front of the collection: the node's
`Description` above a box for the operator to say why they are collecting. *Collect* starts the
download; *Cancel* starts nothing at all.

```xml
<FilePackage Id="pkg.logs" Title="Logs/All app logs"
             Description="Every application's recent log from both machines, plus Dirigent's own logs and config. Usually 5-10 MB.">
    <FileRef Id="log" MachineId="*" AppId="*"/>
    <Script Title="Download zipped package" Name="BuiltIns/DownloadZipped.cs" AskComment="1"/>
</FilePackage>
```

`AskComment` is an attribute of the action rather than a word inside its `Args`, because `Args` is
the script's own argument string: a directive hidden in there would have to be found by a substring
search, which any other argument could trip.

The archive then carries `_comment.txt` at its root, holding what the operator wrote under a header
that answers the questions an archive raises on its own:

```
Collected : 2026-08-31 14:32:10
Package   : Logs/All app logs   [pkg.logs]
Machines  : BackEnd [192.168.0.120, 172.21.112.1]
            FrontEnd [192.168.0.150, 192.168.1.39]
Downloaded to: FrontEnd [192.168.0.150, 192.168.1.39]
Dirigent  : 3.1.18.12

Symphony froze during the 14:20 run, right after the BScene reload.
```

One machine per line, in name order, each with the addresses **it reports about itself** - every
routable IPv4 address of every interface that is up. That is the only reliable source: `<Machine
IP="...">` is usually not set at all, and the address the master observes is where the connection
came from, which is loopback for anything running beside it. Nothing is chosen among several
addresses, because which one matters depends on the question.

Where the config does declare an address it comes first in the brackets, and where it declares one
the machine does not have, the note says so - a misconfiguration that otherwise surfaces much later
as a file share that cannot be reached. A declared `127.0.0.1` is not treated as a disagreement:
every machine has loopback, and a single-machine system is normally written that way.

If a machine was online but could not deliver its files - typically because no file share of the
requestor's machine covers its download folder - the note says so, loudly, above the comment:

```
*** NOT COLLECTED - these machines were online but could not deliver their files, so nothing of
theirs is in this archive: ***
    BackEnd [192.168.0.120]
        No file share of FrontEnd covers its download folder, so BackEnd has no way of uploading the files there.
```

That is the only trace such a machine leaves: it writes no folder and no `_incomplete.txt`, so
without this line an archive missing a whole machine reads exactly like a complete one - and the
dialog that reported it is long gone by the time anybody opens the zip. Files that individual
machines failed to read are listed under it in the same spirit.

The header is written even when the operator says nothing, since it is useful on its own; the
comment line then reads `(no comment)`.

Asking happens **before** the collection: it can run for minutes, so a dialog afterwards would be
one nobody is waiting at, and the words are needed by the collection itself. Only the WinForms GUI
asks - a script on the master cannot show a dialog, and a CLI or REST caller has nobody to ask, so
those pass `Comment` in the arguments if they want one.

`AskComment` is not specific to downloads. Any `<Script>` action can carry it - on a file node, an
app, a machine, or in the main menu - and the answer reaches the script as
[`ScriptActionArgs.Comment`](Scripts.md), including when the action is hosted by another client.
Whether anything comes of it is up to the script: `DownloadZipped` writes it into the archive, and a
script that never reads `Comment` simply ignores it. Cancelling the dialog always means the action
does not run.

#### The destination folder

Every participating machine has to be able to write into the requestor's download folder. Each
slave is handed that folder twice - as a local path and as a UNC path - and uses the local one
when it is running on the machine that owns the folder. That is the common case of a GUI and an
agent on the same box: the archive is copied straight to disk instead of through a share.

For the *other* machines the UNC path is the only way in, so the requestor's machine needs a
`<Share>` covering its download folder (see
[UNC paths and file shares](#unc-paths-and-file-shares)). Without one:

* machines that own the folder still deliver their files;
* every other machine is reported in the final message as unable to upload, and the download
  completes with what could be collected rather than failing outright.

A machine answering at the same address as the requestor's machine shares its disks with it, so
where no share is defined at all, such a machine is allowed to use the local path too.

### `BuiltIns/ClearFiles.cs`, `MarkFiles.cs`, `UnmarkFiles.cs` - delimit a test run

Applicable to any node, like the download. Each starts a slave (`BuiltIns/MarkFilesSlave.cs`) on
every machine holding files of the node, follows them in the status bar, and ends in a message box
naming what happened per machine. See
[Collecting one test run](#collecting-one-test-run-clear-mark-and-unmark) for what they do to a
file and why, and [Marking and clearing](MarkAndClear.md) for the reasoning behind it.

```xml
<FilePackage Id="pkg.run" Title="Logs/Test run"
             Description="One test run: clear, run the case, download.">
    <FileRef Id="log" MachineId="*" AppId="*"/>
    <FileRef Id="cfg" MachineId="*" AppId="*"/>

    <Script Title="Clear"    Name="BuiltIns/ClearFiles.cs"/>
    <Script Title="Mark"     Name="BuiltIns/MarkFiles.cs"/>
    <Script Title="Unmark"   Name="BuiltIns/UnmarkFiles.cs"/>
    <Script Title="Download" Name="BuiltIns/DownloadZipped.cs" AskComment="1"/>
</FilePackage>
```

The `cfg` nodes need no exclusion: they are not `Clearable`, so Clear and Mark pass over them and
the archive still gets them whole.

### `BuiltIns/BrowseInDblCmdVirtPanel.cs` - browse in Double Commander

Resolves the node, writes a Double Commander *virtual panel* list describing the resolved tree,
and opens Double Commander on it - letting the user walk a tree of files gathered from several
machines as if it were one folder.

Requires a `DoubleCommander` tool defined in `LocalConfig.xml`, pointing to a build that supports
the `--startupscript` option (see <https://github.com/pjanec/doublecmd>).

### `BuiltIns/ListVfsNodes.cs` - list what is declared

Not an action on a node but a query: it returns the declared nodes, optionally filtered, which is
how a caller with no GUI finds out what there is to ask for. Declarations only - nothing is looked
up in any file system.

```json
{ "Filter": { "Id": "log", "MachineId": "m1", "AppId": "*" } }
```

Every part of the filter is optional; leaving out `Filter` lists everything. The result is one
record per node - `Id`, `Guid`, `Type`, `MachineId`, `AppId`, `Title` and the *declared* `Path`.

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

The ImGui GUI does not render VFS nodes. Everything else reaches files through the built-in
scripts - see [Files without a GUI](#files-without-a-gui).

## Files without a GUI

There is no `DownloadFile` command, and none is needed: the built-in scripts are the interface,
and `StartScript` plus `GetScriptState` already carry them. This works over the CLI, over
`POST /cli`, and from another script.

The one thing a script cannot be handed from outside is a **resolved** node tree - resolving is a
remote operation. So each VFS script accepts a *selector* instead, naming the node the way a
`<FileRef>` does, and resolves it itself:

```json
{ "Node": { "Id": "log", "MachineId": "m1", "AppId": "camera" } }
```

`MachineId` and `AppId` are filters and default to `*`, so `{"Node":{"Id":"logs.all"}}` means
"the node called `logs.all`, wherever it is". Only top-level nodes can be named - see
[Where nodes can be declared](#where-nodes-can-be-declared).

A whole log collection from the command line, then:

```
# what is there to take?
StartScript 11111111-1111-1111-1111-111111111111 BuiltIns/ListVfsNodes.cs
GetScriptState 11111111-1111-1111-1111-111111111111

# is the file really there, right now?
StartScript 22222222-2222-2222-2222-222222222222 BuiltIns/ResolveVfsPath.cs '{"Node":{"Id":"log","MachineId":"m1","AppId":"camera"},"IncludeContent":true}'
GetScriptState 22222222-2222-2222-2222-222222222222

# collect the lot
StartScript 33333333-3333-3333-3333-333333333333 BuiltIns/DownloadZipped.cs '{"Node":{"Id":"logs.all"}}'
GetScriptState 33333333-3333-3333-3333-333333333333
```

Points worth knowing:

* **Arguments are always JSON** deserialisable into the script's argument type - never a bare
  string to be parsed. Newtonsoft's relaxed syntax is accepted, so `{Node:{Id:'logs.all'}}` works
  as well. Arguments that are not valid JSON for that type fail the script, which
  `GetScriptState` then reports as `Failed` - they are never quietly treated as defaults.
* **Wrap the JSON in single quotes** on a command line, so its double quotes survive the command
  tokenizer.
* **The result comes back in `ScriptState.Data`**, as the JSON of the script's result type.
  `DownloadZipped` returns the full path of each archive produced, the machine it was downloaded
  to, the machines that took part, and one error entry per machine that had trouble. A download
  that partly failed still finishes - the errors are in the result, not in the script's status.
* **The guid is yours to invent**, and it names the script instance for `GetScriptState` and
  `KillScript` afterwards. It shares the namespace with the `<Script>` definitions in the shared
  config, so do not reuse one of those unless you mean to replace it.

* **Where the files land.** A GUI's download goes to the GUI's own machine. A CLI or REST caller is
  on no machine that Dirigent knows, so the files go to the machine running the master; name
  `ToMachine` in the arguments to send them somewhere else. The machine has to have a connected
  agent, and the result's `DownloadMachine` says which one was used.

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
the per-machine parts decompresses and recompresses them on the requestor's machine. The transfer
over the network stays compressed, but a very large download costs some CPU and disk there - about
what the compressed size costs, so tens of seconds for a multi-gigabyte collection rather than
minutes, and the progress bar follows it. A collection from a single machine is not repacked at
all: one part with no machine folder wanted is moved into place.

**The merging machine must run an agent.** The merge happens on the machine the download goes
to. If the requesting GUI runs on a machine with no agent, the download - and the merging -
falls back to the machine running the master.

**Files have no commands of their own.** Outside the WinForms GUI, files are reached by running
the built-in scripts through `StartScript` and reading `GetScriptState` - see
[Files without a GUI](#files-without-a-gui). There is no `GetVfsNodes` or `DownloadFile`
verb, and the ImGui GUI does not render VFS nodes at all.

**Nodes nested in a container are not referenceable.** Only the nodes declared directly under
`<Shared>`, `<Machine>`, `<App>` or `<AppTemplate>` can be found by a `<FileRef>`. See
[Where nodes can be declared](#where-nodes-can-be-declared).

**Silent misses.** A node that resolves to nothing - an unmatched `<FileRef>`, a
`Filter="Newest"` folder with no matching file - makes its menu item do nothing at all when
clicked, without a message. The Files tab is the way to tell: its **Resolve** command reports
`Not found` for the same node. *Download zipped* is the exception: it resolves the node itself and
reports a node that yields nothing as a failed operation, visible in the status bar.

**A download that produces nothing fails.** One machine or one unreadable file does not fail a
download - what the others delivered is still collected, and the errors are listed in the closing
message and in the result. But a download that produces no archive at all ends as a *failed*
script, so a progress indicator shows red rather than success, and a CLI or REST caller reading
the script status sees the failure instead of a `Finished` with the reason buried in the result.

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

**The download folder follows the requestor's client name.** The machine to download to is taken
from the `<machineId>_gui_<guid>` name a GUI gives itself; only if that machine has no agent
connected is the agent at the same address looked for. A requestor that is neither - a CLI or
REST client - is on no machine at all, so the files land on the machine running the master.
Pass `ToMachine` in the script arguments to choose.

**`%DOWNLOADS%` follows the agent's user.** It is read from the registry of the user the agent
process runs as, which is not the interactive user if the agent runs as a service under a
different account.

**Browsing needs a patched Double Commander.** See
[the browse action](#builtinsbrowseindblcmdvirtpanelcs---browse-in-double-commander).

**A "Collection modified" balloon may appear after a download** (`TODO.md`).

## See also

* [Collecting log files](LogFileCollection.md) - the same material as a cookbook of worked examples
* [Actions](Actions.md) - the general action mechanism and the full list of action variables
* [Tools](Tools.md) - defining the tool applications that actions start
* [Scripts](Scripts.md) - writing scripts, including ones that consume a resolved node tree
* [SharedConfig](SharedConfig.md) - the surrounding configuration file
* [LocalConfig](LocalConfig.md) - tool definitions and default actions
* [`config/SharedConfig.xml`](../config/SharedConfig.xml) - a working example of most of the above
