# File Definitions: Files, Folders and Packages

Dirigent lets you declare the interesting files of your system - application logs, config
files, crash dumps, data folders - in `SharedConfig.xml`, and then offers them in its UI for
viewing, browsing and downloading, no matter which machine they physically reside on.

Together these declarations form a **virtual file system** (VFS): a tree of file definitions
(*VfsNodes* in the code and in the names of the scripts) mixing physical files, physical folders and purely virtual grouping folders,
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

* [The elements at a glance](#the-elements-at-a-glance)
* [Setting up file collection, step by step](#setting-up-file-collection-step-by-step)
  * [1 - a machine-wide log](#step-1---a-machine-wide-log-a-folder-on-a-machine)
  * [2 - the logs of applications](#step-2---the-logs-of-applications-a-filetemplate-and-an-apptemplate)
  * [3 - packages: collect a kind with a wildcard](#step-3---packages-collect-a-kind-with-a-wildcard)
  * [4 - a per-machine package](#step-4---a-per-machine-package-from-a-machinetemplate)
  * [5 - override by Id](#step-5---one-app-or-one-machine-that-differs-override-by-id)
  * [6 - the name of the working folder](#step-6---the-name-of-the-working-folder-in-path-and-title-app_startupdir_name-and-local-variables)
* [Concepts](#concepts)
  * [The three stages](#the-three-stages)
  * [File definition types](#file-definition-types)
  * [Association: app, machine or global](#association-app-machine-or-global)
  * [Where file definitions can be declared](#where-file-definitions-can-be-declared)
  * [Resolution](#resolution)
  * [When something is not there](#when-something-is-not-there)
  * [UNC paths and file shares](#unc-paths-and-file-shares)
* [XML reference](#xml-reference)
  * [Attributes common to all file definitions](#attributes-common-to-all-file-definitions)
  * [`<File>`](#file)
  * [`<File Filter="Newest">`](#file-filternewest)
  * [`<Folder>`](#folder)
  * [Files too big to collect whole](#files-too-big-to-collect-whole)
  * [Collecting one test run: Clear, Mark and Unmark](#collecting-one-test-run-clear-mark-and-unmark)
  * [`<VFolder>`](#vfolder)
  * [`<FilePackage>`](#filepackage)
  * [`<FileRef>`](#fileref)
  * [File masks](#file-masks)
  * [Variables in `Path`, `Mask` and `Title`](#variables-in-path-mask-and-title)
  * [File templates](#file-templates)
  * [Actions on file definitions](#actions-on-file-definitions)
* [Looking the files up](#looking-the-files-up)
* [Built-in actions](#built-in-actions)
  * [What the operator sees while it runs](#what-the-operator-sees-while-it-runs)
* [Where files appear in the UI](#where-files-appear-in-the-ui)
* [Files without a GUI](#files-without-a-gui)
* [Examples](#examples)
* [Limitations and known issues](#limitations-and-known-issues)
* [See also](#see-also)

## The elements at a glance

| Element | What it is | Declared in |
| --- | --- | --- |
| `<File>` | one file, or - with `Filter="Newest"` - the newest few files of a folder | `<App>`, `<AppTemplate>`, `<Machine>`, `<MachineTemplate>`, top level |
| `<Folder>` | a folder, scanned recursively, filtered by `Mask` and limited by age, count, size | the same |
| `<VFolder>` | a named group of other definitions, shaping an archive | inside a package or another group |
| `<FilePackage>` | a named bundle to download or browse as a whole | the same as a file, usually the top level |
| `<FileRef>` | "all the definitions whose `Id`, machine and app match" - how a package collects without repeating | inside a package or group |
| `<FileTemplate>`, `<AppTemplate>`, `<MachineTemplate>` | defaults to write once: attributes of a definition, the definitions of an app, the shares and definitions of a machine | top level |

Each of them is explained in the [reference](#xml-reference); the walkthrough below uses them in the order a site is usually set up.

## Setting up file collection, step by step

The example is a small site: machines `m1` and `m2`, each running a Dirigent agent, and the applications `viewer` and `mt`.
Every step is a complete piece of configuration that loads; later steps build on earlier ones and repeat what they need. The *Result* paragraphs say what the
[REST API](HTTP.md#GET-apifiledefsidfilesmachineapp) (`GET /api/filedefs/{id}/files`) - or, in the GUI, the Files tab and the menu of the machine or app -
would show.

### Step 1 - a machine-wide log: a `<Folder>` on a `<Machine>`

A log that belongs to the machine and not to one application goes into the `<Machine>` element. A `<Share>` lets the other machines reach the files (see
[UNC paths and file shares](#unc-paths-and-file-shares)); a `<Folder>` collects a log folder, bounded so that it cannot grow into an unbounded download.

<!-- example: loads -->
```xml
<Machine Name="m1" IP="192.168.0.11">
    <Share Name="C" Path="C:\"/>

    <Folder Id="dirigent.log" Title="Logs/Dirigent" Path="%ProgramData%\Dirigent\logs" Mask="*.log"
            MaxFiles="20" MaxSeconds="604800" TailBytes="10485760" Clearable="1"/>
</Machine>
```

*Result.* The *Logs/Dirigent* entry is in the context menu of machine `m1`. `GET /api/filedefs/dirigent.log/files` answers one entry of type `Folder` on `m1`
with a file per `.log` found - each with its path on `m1`, a UNC path such as `\\192.168.0.11\C\ProgramData\Dirigent\logs\agent.log`, its size and modification
time. A file bigger than 10 MB says `truncatedTo: 10485760`: a download holds only its last 10 MB.

The `Id` is the definition's name and has a **kind prefix**: `dirigent.*`. Keep that in mind for step 3.

### Step 2 - the logs of applications: a `<FileTemplate>` and an `<AppTemplate>`

Many applications keep a log folder of the same shape. The attributes that repeat go into a `<FileTemplate>`; what every app of a kind has goes into an
`<AppTemplate>`, which is read once per app that uses it - so one declaration yields one definition per app, bound to that app's machine and
`%APP_STARTUPDIR%`:

<!-- example: loads -->
```xml
<FileTemplate Name="log.recent" Filter="Newest" MaxFiles="5" MaxSeconds="172800"
              TailBytes="10485760" Clearable="1"/>

<AppTemplate Name="apps.viewer" ExeFullPath="C:\Viewer\viewer.exe" StartupDir="C:\Viewer">
    <File Template="log.recent" Id="log.viewer" Title="Log/Viewer" Path="%APP_STARTUPDIR%\logs" Mask="viewer*.log"/>
    <File Id="cfg.app.viewer" Title="Config/Viewer" Path="%APP_STARTUPDIR%\viewer.xml"/>
</AppTemplate>

<AppTemplate Name="apps.mt" ExeFullPath="C:\MT\mt.exe" StartupDir="C:\MT">
    <File Template="log.recent" Id="log.mt" Title="Log/MT" Path="%APP_STARTUPDIR%\logs" Mask="mt*.log"/>
    <File Id="cfg.app.mt" Title="Config/MT" Path="%APP_STARTUPDIR%\mt.xml"/>
</AppTemplate>

<App AppIdTuple="m1.viewer" Template="apps.viewer"/>
<App AppIdTuple="m1.mt"   Template="apps.mt"/>
<App AppIdTuple="m2.viewer" Template="apps.viewer"/>
```

Every `Id` is **unique within its app** and starts with the **kind** it belongs to: `log.*` for logs, `cfg.app.*` for application configuration. The same `Id` on
different apps is fine - `m1.viewer` and `m2.viewer` both have `log.viewer` - and is what lets one reference collect them all. (A [`<FileTemplate>`](#file-templates)
carries only attributes; the `Id`, the `Title` and the `Path` stay on the definition. A `Clearable="1"` from the template is the permission to empty or mark the
log - see [Collecting one test run](#collecting-one-test-run-clear-mark-and-unmark); the config file below it is not clearable.)

*Result.* The context menu of `m1.viewer` offers *Log/Viewer* and *Config/Viewer*; `GET /api/appdefs/m1.viewer/files` lists the newest five `viewer*.log` files of `C:\Viewer\logs`
on `m1`, newest first, and `viewer.xml`. `GET /api/appdefs/m2.viewer/files` does the same on `m2`.

### Step 3 - packages: collect a kind with a wildcard

A `<FilePackage>` is a bundle; the `<FileRef>`s inside select definitions by `Id`, `MachineId` and `AppId`, wildcards allowed (`*` any run of characters, `?` one;
letter case does not matter). Because the `Id`s are prefixed by kind, one reference takes a whole kind:

<!-- example: loads -->
```xml
<Machine Name="m1" IP="192.168.0.11">
    <Share Name="C" Path="C:\"/>
    <Folder Id="dirigent.log" Title="Logs/Dirigent" Path="%ProgramData%\Dirigent\logs" Mask="*.log"/>
</Machine>

<AppTemplate Name="apps.viewer" ExeFullPath="C:\Viewer\viewer.exe" StartupDir="C:\Viewer">
    <File Id="log.viewer" Title="Log/Viewer" Path="%APP_STARTUPDIR%\logs" Mask="viewer*.log" Filter="Newest" MaxFiles="5"/>
    <File Id="cfg.app.viewer" Title="Config/Viewer" Path="%APP_STARTUPDIR%\viewer.xml"/>
</AppTemplate>
<App AppIdTuple="m1.viewer" Template="apps.viewer"/>
<App AppIdTuple="m2.viewer" Template="apps.viewer"/>

<!-- every application log, on every machine -->
<FilePackage Id="pkg.logs" Title="Logs/All application logs">
    <FileRef Id="log.*" MachineId="*" AppId="*"/>
</FilePackage>

<!-- logs and configuration of one machine's applications -->
<FilePackage Id="pkg.m1" Title="Logs/Machine m1">
    <FileRef Id="log.*"     MachineId="m1" AppId="*"/>
    <FileRef Id="cfg.app.*" MachineId="m1" AppId="*"/>
</FilePackage>
```

`log.*` matches `log.viewer` and `log.mt`, not `logs.x` and not a plain `log` (there is no dot); see [Ids: one per definition, kinds by
prefix](#ids-one-per-definition-kinds-by-prefix). Packages declared at the top level appear in no menu by themselves - put a `<FileRef>` to them in
`<MainMenu>` to get a menu entry (step 4 of the [cookbook](LogFileCollection.md#step-4-make-the-package-reachable)).

**The trap: `AppId="*"` matches the machine-level definitions too.** A machine-level definition has *no* app, and `*` means "any, including none". So `pkg.logs`
above would also collect a machine log named `log.something`. That is why the machine's own log in step 1 is `dirigent.log` and not `log.dirigent`: it belongs to a
different kind and a package that wants it says so (`<FileRef Id="dirigent.*" .../>`). To take only definitions that *do* have an app, name the app with a
pattern that needs one, for example `AppId="?*"`.

*Result.* `GET /api/filedefs/pkg.logs/files` answers one entry of type `FilePackage` titled *Logs/All application logs*, whose files are those of `log.viewer` on `m1`
and on `m2` - each file with its own `machine`. `GET /api/filedefs/pkg.m1/files` has the `m1` logs and the `m1` configuration file. A machine that is offline
gives a note on the entry and the files of the others.

### Step 4 - a per-machine package from a `<MachineTemplate>`

Most machines of a site have the same share, the same machine-wide log and the same package of "everything of this machine". That is written once in a
`<MachineTemplate>` and used with `Template=` on each `<Machine>`:

<!-- example: loads -->
```xml
<MachineTemplate Name="m.base">
    <Share Name="C" Path="C:\"/>
    <Folder Id="dirigent.log" Title="Logs/Dirigent" Path="%ProgramData%\Dirigent\logs" Mask="*.log"/>

    <!-- no MachineId in the references: they are those of the machine the template is used on -->
    <FilePackage Id="pkg.machine" Title="Logs/This machine">
        <FileRef Id="log.*"/>
        <FileRef Id="cfg.app.*"/>
        <FileRef Id="dirigent.*"/>
    </FilePackage>
</MachineTemplate>

<Machine Name="m1" IP="192.168.0.11" Template="m.base"/>
<Machine Name="m2" IP="192.168.0.12" Template="m.base"/>

<AppTemplate Name="apps.viewer" ExeFullPath="C:\Viewer\viewer.exe" StartupDir="C:\Viewer">
    <File Id="log.viewer" Title="Log/Viewer" Path="%APP_STARTUPDIR%\logs" Mask="viewer*.log"/>
    <File Id="cfg.app.viewer" Title="Config/Viewer" Path="%APP_STARTUPDIR%\viewer.xml"/>
</AppTemplate>
<App AppIdTuple="m1.viewer" Template="apps.viewer"/>
<App AppIdTuple="m2.viewer" Template="apps.viewer"/>
```

A `<FileRef>` written inside a `<Machine>` without `MachineId` takes that machine; without `AppId` it matches the machine's applications *and* the machine itself
(see [`<FileRef>`](#fileref)). So `pkg.machine` of `m1` collects exactly `m1`'s `log.viewer`, its `cfg.app.viewer` and its `dirigent.log` - and that of `m2` the same
of `m2`. The machines are listed by the template once; a machine with something extra says it in its own element (step 5).

*Result.* `GET /api/filedefs/pkg.machine/files?machine=m1` answers the package of `m1`; without `?machine=` it answers both, one entry each. The context menu
of `m1` offers *Logs/This machine*. See [MachineTemplate](SharedConfig.md#MachineTemplate-element) for how templates and mixins merge.

### Step 5 - one app, or one machine, that differs: override by `Id`

A definition in a derived template, in the `<App>` itself or in the `<Machine>` with the **same `Id`** as an inherited one *replaces* it, in its place; any attribute
may differ - the title included. A new `Id` is added:

<!-- example: loads -->
```xml
<FileTemplate Name="log.recent" Filter="Newest" MaxFiles="5" TailBytes="10485760" Clearable="1"/>

<AppTemplate Name="apps.viewer" ExeFullPath="C:\Viewer\viewer.exe" StartupDir="C:\Viewer">
    <File Template="log.recent" Id="log.viewer" Title="Log/Viewer" Path="%APP_STARTUPDIR%\logs" Mask="viewer*.log"/>
</AppTemplate>

<!-- m1 is a normal Viewer; m2 keeps more files and says so in the title -->
<App AppIdTuple="m1.viewer" Template="apps.viewer"/>
<App AppIdTuple="m2.viewer" Template="apps.viewer">
    <File Template="log.recent" Id="log.viewer" Title="Log/Viewer (last 50)" Path="%APP_STARTUPDIR%\logs" Mask="viewer*.log" MaxFiles="50"/>
    <File Id="log.viewer.crash" Title="Log/Viewer crashes" Path="%APP_STARTUPDIR%\crash" Mask="*.log" Filter="Newest"/>
</App>
```

`m2.viewer` ends with exactly two definitions: `log.viewer` *replaced* (fifty files, another title) and `log.viewer.crash` *added*. The inherited `log.viewer` is gone from `m2.viewer`;
`m1.viewer` is untouched. The same rule works for a `<Machine>` over its `<MachineTemplate>`.

Two definitions with the **same `Id` written on one level** - in one `<App>`, one `<AppTemplate>`, one `<Machine>` - are a mistake, and the config is refused:

```xml
<App AppIdTuple="m1.viewer" ExeFullPath="C:\Viewer\viewer.exe">
    <File Id="log.viewer" Path="C:\Viewer\logs\a.log"/>
    <File Id="log.viewer" Path="C:\Viewer\logs\b.log"/>   <!-- Duplicate file definition Id 'log.viewer' in App 'm1.viewer' -->
</App>
```

Give the second one its own `Id` (`log.viewer.b`) - and the wildcard `log.viewer.*` or `log.*` still finds both.

*Result.* `GET /api/appdefs/m2.viewer/files` lists up to fifty logs under *Log/Viewer (last 50)* and the crash logs; `GET /api/appdefs/m1.viewer/files` still lists five.

### Step 6 - the name of the working folder in `Path` and `Title`: `%APP_STARTUPDIR_NAME%` and `<Local>` variables

When applications of one kind run from a folder of their own - `..\RenderHost-3801`, `..\RenderHost-3802` - one definition in the template serves them all, taking the
folder's name in both the path and the title. An app's own variables (`<Local>`) are available in the same places:

<!-- example: loads -->
```xml
<AppTemplate Name="apps.ig" ExeFullPath="C:\IG\ig.exe">
    <File Id="log.ig" Title="Log/%APP_STARTUPDIR_NAME%" Path="C:\IG\Logs\%APP_STARTUPDIR_NAME%" Mask="*.log"/>
    <File Id="cfg.app.ig" Title="Config/%INSTANCE% (%APP_ID%)" Path="%APP_STARTUPDIR%\ig.xml"/>
</AppTemplate>

<App AppIdTuple="m1.ig1" Template="apps.ig" StartupDir="..\RenderHost-3801">
    <Env><Local Variable="INSTANCE" Value="primary"/></Env>
</App>
<App AppIdTuple="m1.ig2" Template="apps.ig" StartupDir="..\RenderHost-3802">
    <Env><Local Variable="INSTANCE" Value="backup"/></Env>
</App>
```

*Result.* The titles are worked out when the config is loaded: `m1.ig1` has *Log/RenderHost-3801* and *Config/primary (ig1)*; `m1.ig2` has *Log/RenderHost-3802* and
*Config/backup (ig2)*. The `Path` is expanded on the agent when the files are looked up, so `GET /api/appdefs/m1.ig1/files` lists `C:\IG\Logs\RenderHost-3801\*.log`.
Which variables are available where - launch, `Path`/`Mask`, `Title` - is the table in [Variables in `Path`, `Mask` and `Title`](#variables-in-path-mask-and-title).

## Concepts

### The three stages

Understanding the VFS is mostly understanding that a file definition and an actual file are
two different things, separated in time and place.

| Stage           | What happens                                                                                                                                                       | Where / when                                                                                     |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------ |
| **Declaration** | The XML elements are parsed into *file definitions*. Nothing is looked up in any file system; paths may still contain variables, masks and filters.                 | On the master, when `SharedConfig.xml` is loaded. Definitions are then sent to all GUIs.          |
| **Resolution**  | The definition tree is turned into a tree of *concrete* files: variables expanded, folders scanned, masks and filters applied, references followed, paths made UNC.  | On demand - when the user clicks a menu item. Each file definition is resolved on the machine that owns it.  |
| **Action**      | A tool or a script is started and receives the resolved tree (and/or the resolved paths as variables).                                                              | On the machine of the user who clicked, unless the action says otherwise.                         |

The consequence worth remembering: **a file definition is a recipe, not a file list**. A
`<Folder>` or a `Filter="Newest"` definition yields whatever exists at the moment of the click.

### File definition types

| Element         | Container? | Description                                                                                                                                                          |
| --------------- | ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `<File>`        | no         | A single physical file. With `Filter="Newest"` it becomes a *recipe* picking recent files out of a folder.                                                            |
| `<Folder>`      | yes        | A physical folder. On resolution its content is scanned recursively and turned into a subtree of files and subfolders, filtered by `Mask`.                             |
| `<VFolder>`     | yes        | Virtual folder. Has no counterpart in any real file system; it only groups other file definitions and gives that group a name. Used to shape the structure of a bundle.          |
| `<FilePackage>` | yes        | A named bundle of file definitions, meant to be downloaded or browsed as a whole. Structurally identical to `<VFolder>`; it differs only in getting the *package* default actions and in being intended as a top-level entry point. |
| `<FileRef>`     | -          | A reference to other file definition(s) declared elsewhere in the config, matched by `Id` / `MachineId` / `AppId`, wildcards allowed. This is the mechanism for collecting many apps' files into one package without repeating their definitions. |

Containers may contain any of the above, nested arbitrarily deep.

### Association: app, machine or global

Every file definition is associated with an **app**, with a **machine**, or with **nothing** (a *global*
file definition). The association is normally implied by *where in the XML the file definition is declared*, and can
be overridden with the `MachineId` / `AppId` attributes.

The association decides two things:

1. **Which context menu the file definition appears in** - that app's menu, that machine's menu, or, for
   global file definitions, nowhere by itself (global file definitions are reachable only through a `<FileRef>` or
   through `<MainMenu>`).
2. **In whose context the path is resolved** - which machine's file system is scanned, whose
   environment variables are expanded, and which app's `%APP_STARTUPDIR%` etc. are available.

So a file definition declared under an app running on machine `m1` has its `%USERPROFILE%` expanded using
the environment of the Dirigent agent on `m1`, regardless of which machine the user clicked on.

The machine-bound file definitions of a site rarely differ from machine to machine: write them once in a `<MachineTemplate>` and use it
with `Template=` on the `<Machine>`s - see [Machine templates](SharedConfig.md#MachineTemplate-element).

### Where file definitions can be declared

| Declaration site         | Resulting association                             | Appears in                                                |
| ------------------------ | ------------------------------------------------- | --------------------------------------------------------- |
| inside `<App>`           | app + machine (from `AppIdTuple`)                 | that app's context menu                                   |
| inside `<AppTemplate>`   | app + machine of **each app using the template**  | the context menu of every app using the template          |
| inside `<Machine>`       | machine (app = none)                              | that machine's context menu                               |
| top level of `<Shared>`  | global                                            | nowhere directly - only via `<FileRef>` or `<MainMenu>`    |
| inside `<MainMenu>`      | global                                            | the GUI's main menu bar                                   |
| inside a container  | inherited from the container, unless overridden   | wherever the container appears                            |

Declaring file definitions in an `<AppTemplate>` is the main tool for keeping the config small: the
template is parsed once per app that uses it, with that app's ids, so one declaration yields
one correctly bound file definition per app. See the [examples](#examples).

Independently of the menus, the file definitions declared **directly** under `<Shared>`, `<Machine>`,
`<App>` or `<AppTemplate>` are put into a flat registry. That registry is what `<FileRef>`
searches, so a globally declared package is perfectly usable even though it appears in no menu
on its own.

File definitions **nested inside a container** are part of that container's content but are *not* in the
registry, so they cannot be targeted by a `<FileRef>`. Declare a file definition at one of the levels
above if you want to reference it from several packages.

### Resolution

Resolution is what turns definitions into files. It is triggered by clicking a menu item and
proceeds recursively:

* If the file definition belongs to another machine, resolution of that file definition is **delegated to that
  machine** - Dirigent runs the built-in script `BuiltIns/ResolveVfsPath.cs` there and takes
  the result. If that machine is not connected, resolution fails with
  `Machine <id> not connected.`
* Global file definitions (no machine) are resolved locally; their paths must therefore already be UNC.
* `<File>`: variables in `Path` are expanded; a relative path is made absolute against the
  *root for relative paths* (by default the folder containing `SharedConfig.xml`, see the
  `rootForRelativePaths` option). With `Filter="Newest"` the folder is scanned and the
  matching files selected.
* `<Folder>`: the folder is scanned - subfolders recursively, files filtered by `Mask` - and
  becomes a subtree of virtual folders and files.
* `<VFolder>` / `<FilePackage>`: children are resolved one by one.
* `<FileRef>`: the registry is searched; see [`<FileRef>`](#fileref) for the outcome.
* Cycles are detected: a file definition already visited on the current path resolves to nothing.

The result is a tree containing only virtual folders and concrete file paths. Paths are
returned from the perspective of the *requesting* machine: files on other machines, and all
global files, come back as UNC paths; files on the requestor's own machine come back as plain
local paths.

### When something is not there

A package names things that need not all exist: a crash-dump folder on a machine that has never
crashed, a log an application has not written yet, a machine that is offline. **One member that
cannot be delivered never costs you the rest.**

> **This changed in 3.1.18.** Before that, a `<Folder>` whose folder did not exist on one machine
> **aborted the resolution of the whole package**, so a system-wide collection could be lost to a
> folder that had never existed anywhere - at its most likely just after an incident, when the
> collection matters most. If you are on an older build, or working from a runbook written against
> one, that is the behaviour you remember, and declaring no file definition for a folder that might be absent
> was the right way to work around it. It is no longer necessary.

| what is missing | what happens |
| --- | --- |
| the folder of a `<Folder>` or a `<File Filter="Newest">` | the file definition is left out of the resolved tree |
| the file of a plain `<File>` | the file definition resolves, and the collection finds nothing to add |
| a whole machine (offline) | reported per machine, the others still collect |

In every case the collection carries on, and what was named but not delivered is recorded in the
archive - in **`_incomplete.txt`**, naming the file definition, the machine and the reason, so that an archive
which lacks something says so, months later, to somebody who has only the zip.

The **closing message** gives the operator standing there the count and points at that file. It
deliberately does not list them: an incident package over a large system names hundreds of things
that are not there - most machines have not crashed - and a dialog of hundreds of sentences is not
read at all, which also buries the one or two lines in it that were real problems.

None of this is reported as an error. A dump folder that does not exist is the ordinary state of a
machine that has not crashed, and reporting it as a failure teaches the operator to ignore failures.

**The one absence that is stated outright is a machine that produced no archive at all.** Every
other machine leaves its `_incomplete.txt` behind, so even a machine that collected nothing accounts
for itself; a machine whose part never arrived accounts for nothing, and neither its files nor any
record of what it held exists. Those machines are named in the closing message and marked in
`_comment.txt`. It does not fail the download - the other machines' work still arrives, which before
was not so: one machine unable to write its archive used to end the whole collection and discard
everything already gathered.

Asked for **on its own**, a `<Folder>` or `<File Filter="Newest">` whose folder is missing fails
instead - there the caller wanted that one thing and there is nothing else to hand back.

### UNC paths and file shares

Turning a remote local path such as `D:\Logs\app.log` on `m1` into something the requestor can
open requires a matching file share declared for that machine:

<!-- example: loads -->
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

### Reading the files from a diagnostic tool

Two REST endpoints (and the matching CLI commands `GetAppFiles` and `GetFiles`) list the actual
files of an application or of a package, each with its machine-local path and, where a share covers it, a
UNC path: `GET /api/appdefs/m1.a[@plan]/files` and `GET /api/filedefs/<id>/files?machine=&app=`. The files are
listed, not served - the tool opens them through the UNC paths. See the [Web API reference](HTTP.md), in particular [the file previews](HTTP.md#GET-apiappdefsidfiles)
and [CLI](CLI.md#GetAppFiles).

To collect rather than just look, a tool starts a download job (`POST /api/downloads`, or `DownloadPackage` /
`DownloadApp`), polls it and fetches the archive; see [Download jobs](HTTP.md#Download-jobs). The archives are built on the
master's machine in `--archiveFolder`, which should be under a file share - see
[Local Config](LocalConfig.md#Download-archives).

## XML reference

### Attributes common to all file definitions

| Attribute   | Description                                                                                                                                                                                                              |
| ----------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Id`        | Human readable id, used by `<FileRef>` to find the definition. Unique within its app or machine - see [Ids: one per definition, kinds by prefix](#ids-one-per-definition-kinds-by-prefix); the same `Id` on different apps or machines is how one reference collects them all. Defaults to the definition's `Guid`. |
| `Title`     | Text shown in the menu. Backslash- or slash-separated segments create submenus (`"Logs/Recent"`). Also used as the **folder name inside a downloaded archive**. Falls back to `Id` when not given. Takes `%MACHINE_ID%`, `%APP_ID%` and a few more when the config is loaded - see [Variables](#variables-in-path-mask-and-title).  |
| `Guid`      | Explicit GUID identity of the file definition. Generated automatically when omitted. Useful only if you want a stable identity across config reloads.                                                                                |
| `MachineId` | Overrides the machine association inherited from the declaration site. Empty string means *no machine* and, in `<FileRef>`, *any machine*.                                                                                |
| `AppId`     | Overrides the app association inherited from the declaration site. Empty string means *no app* and, in `<FileRef>`, *any app*.                                                                                            |
| `AppIdTuple` | Shorthand setting both of the above at once, in the `"machineId.appId"` format. Without a dot it sets the app only, leaving the machine empty. `MachineId` / `AppId` given alongside it still win.                        |
| `Icon`      | Icon image shown next to the menu item.                                                                                                                                                                                  |
| `Groups`    | Semicolon-separated group paths, as elsewhere in the config.                                                                                                                                                              |
| `Clearable` | Whether **Clear** and **Mark** may touch the files this file definition yields. `0` (the default) means they never do, whatever any action or argument says - see [Collecting one test run](#collecting-one-test-run-clear-mark-and-unmark). |
| `Description` | What this file definition is, in prose. Shown where the user has to decide something about it - see [Asking for a comment](#asking-for-a-comment). Being an attribute, a line break in it is written `&#10;`.                       |

Any file definition may contain `<Tool>` and `<Script>` child elements - see
[Actions on file definitions](#actions-on-file-definitions).

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
| `Path`    | **Mandatory.** Full path to the file, or - with `Filter="Newest"` - to the folder to search. May contain environment variables and [path variables](#variables-in-path-mask-and-title). A relative path is taken against the root for relative paths.               |
| `Filter`  | Optional resolution filter. Only `Newest` is implemented, see below.                                                                                                                                                                             |
| `TailBytes` | Collect only the last this many bytes of the file, if it is bigger than that. `0` (the default) collects whole files. See [Files too big to collect whole](#files-too-big-to-collect-whole).                                                     |

A `<File>` with **no machine association** - declared at the top level of `<Shared>`, in
`<MainMenu>`, or with `MachineId=""` - must be given a UNC path. The config reader rejects
anything else, since there would be no machine on which to resolve it.

### `<File Filter="Newest">`

With `Filter="Newest"`, `Path` denotes a **folder** and the file definition resolves to the most recent
file(s) in it. This is the recipe to use for log files whose names contain a timestamp, and the
only file definition type that can restrict results by age.

```xml
<!-- the single newest *.log file -->
<File Id="log" Title="Newest log" Path="%APP_STARTUPDIR%\logs" Mask="*.log" Filter="Newest"/>

<!-- up to 10 log files, none older than 2 days (2*24*3600 = 172800 s);
     another definition, so another Id - one app does not declare the same Id twice -->
<File Id="log.recent" Title="Recent logs" Path="%APP_STARTUPDIR%\logs"
      Mask="*.log" Filter="Newest" MaxFiles="10" MaxSeconds="172800"/>
```

| Attribute    | Default  | Description                                                                                                        |
| ------------ | -------- | ------------------------------------------------------------------------------------------------------------------ |
| `Mask`       | all files | File mask, see [File masks](#file-masks). Applied to the file names in the folder itself; never recursive.         |
| `MaxFiles`   | `1`      | Maximum number of files to return. Values below 1 are treated as 1.                                                |
| `MaxSeconds` | no limit | Maximum age in seconds, measured from the file's last-write time. `0` means *no limit*.                             |
| `TailBytes`  | 0 = whole files | Collect only the last this many bytes of a file bigger than that. See [Files too big to collect whole](#files-too-big-to-collect-whole). |

`MaxTotalBytes` is **not** implemented here - it applies to `<Folder>` only, and is ignored
without a word if written on a `Newest` definition. Bound the result with `MaxFiles` instead.

The files are taken **newest first**, so the age, count and size limits always keep the most
recent files.

The shape of the result depends on `MaxFiles`:

* `MaxFiles="1"` (the default) - the file definition resolves to a **single file** - the newest matching one -
  or to *nothing* if the folder holds no matching file.
* `MaxFiles` greater than 1 - the file definition resolves to a **virtual folder** named after the file definition's
  `Title`, holding up to that many files, newest first.

### `<Folder>`

A physical folder, resolved into a subtree of its content.

<!-- example: loads -->
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

A file **below** the limit is collected whole, under its own name. `TailBytes` applies to the file definition
that declares it - a `<File>`, or every file a `<Folder>` yields - and is deliberately **not**
inherited by the children of a `<VFolder>` or `<FilePackage>`, which are file definitions in their own right
and may well live on another machine.

The tail is a property of collecting into an archive. Opening or browsing the file definition still points at
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
permission lives on the file definition, and it is **off by default**:

```xml
<File Id="log" Title="Log/RenderHost" Path="D:\Logs\RenderHost"
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
* The open **fails** - the file is in use, read-only, or refused for any other reason - so it is
  marked instead, and the report says which file and why. A file that cannot be emptied is not a
  failed operation: the line under it delivers the same result to the next collection.

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

All three work on a whole package, on a single file definition, and on one row of the Files tab - wherever the
action is declared.

#### What one operation does after another

Written out because it is what everybody asks. `A` and `B` stand for lines written to a log, and the
last column is what the archive holds:

| what was done | collected | |
| --- | --- | --- |
| `A B`, download | `A B` | with no line drawn, everything |
| `A` **Mark** `B`, download | `B` | the run, and only the run |
| `A` **Clear** `B`, download | `B` | the same, whether the file was emptied or only marked |
| `A` **Mark** `B` **Unmark** `C`, download | `A B C` | Unmark lifts the line; the history is back |
| `A` **Mark** `B` **Mark** `C`, download | `C` | the later line wins |
| `A` **Mark** `B` **Clear** `C`, download | `C` | so does a Clear after a Mark |
| `A` **Clear** `B` **Mark** `C`, download | `C` | and a Mark after a Clear |
| `A` **Clear** `B` **Unmark** `C`, download | `B C` | all of the file the Clear left behind |
| `A` **Unmark** `B`, download | `A B` | an Unmark with no line to lift changes nothing |
| `A` **Mark** `B`, download, download | `B` twice | a download reads the line, it does not consume it |
| `A` **Mark** `B`, *rotation*, `C`, download | `C` | the new file arrives whole - see below |
| any of the above on a file that is not `Clearable` | everything | no line is ever drawn under it |

Every row of that table is a test - `MarkClearSequenceTests` in the integration tests - so it says
what Dirigent does rather than what it was meant to do.

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

A file with **nothing** after the line is left out of the archive altogether, and `_incomplete.txt`
says so: an empty entry would read as a log that is empty, rather than as one whose contents all
predate the run.

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

The three scripts read their `Args` as a semicolon-separated list of file definition id patterns - the same
wildcards `<FileRef>` uses - and act only on the matching entries of the package:

<!-- example: loads -->
```xml
<Script Title="Clear" Name="BuiltIns/ClearFiles.cs" Args="log*"/>
```

Empty `Args` means every clearable file definition in scope, which is the usual case. The patterns are matched
against each entry's `Id` **or** `Title`, because a `<FileRef>` matching several file definitions resolves to a
folder carrying the reference's id as its title and no id of its own.

`Args` rather than a new attribute, because a list of file definition ids is the script's own argument.
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

A reference to file definition(s) declared elsewhere. `Path` is not used; the file definition is found by matching
`Id`, `MachineId` and `AppId` against the registry of all file definitions in the config.

```xml
<!-- every definition with Id="log", on any machine, in any app or none -->
<FileRef Id="log" MachineId="*" AppId="*"/>

<!-- only machine m1's copies -->
<FileRef Id="log" MachineId="m1" AppId="*"/>

<!-- all vision-subsystem logs, by Id naming convention -->
<FileRef Id="log.vision.*" MachineId="*" AppId="*"/>
```

Matching rules, applied per field:

| Pattern                 | Matches                                                                    |
| ----------------------- | -------------------------------------------------------------------------- |
| omitted or `""` (empty) | anything, including file definitions where the field is unset                         |
| `*`                     | anything, including file definitions where the field is unset                         |
| `abc`, `abc*`, `a?c`    | Win32-style name match; does **not** match file definitions where the field is unset   |

Remember that an omitted `MachineId` / `AppId` on a `<FileRef>` means *inherited from the
declaration site*, not *empty* - a `<FileRef Id="log"/>` written inside an `<App>` element
inherits that app's ids and therefore matches only that app's file definition. To broaden the search from
inside an app or machine context, state `MachineId="*" AppId="*"` (or `""`) explicitly.

Results:

* no match - resolves to nothing, silently;
* one match - resolves to that file definition, as if it had been written in place;
* several matches - resolves to a virtual folder named after the reference's `Title` (or `Id`),
  containing all the matches.

### Ids: one per definition, kinds by prefix

An `Id` names one file definition **within its app or machine**. The same `Id` on different apps or machines is the normal case (`log` on
every app, `applog` on every machine), but two definitions with one `Id` in the same app or machine - on one level of its templates, or
in the same record - are a load error, and a definition in a derived template, in the `<App>` or in the `<Machine>` with an inherited
`Id` *replaces* the inherited one (see [AppTemplate](SharedConfig.md#AppTemplate-replacing-inherited-nodes) and
[MachineTemplate](SharedConfig.md#MachineTemplate-element)).

What an app has more than one of, it names with **a kind prefix** - `log.viewer`, `log.mt.ios`, `cfg.main` - and a package selects the kind with
a wildcard in a `<FileRef>`:

```xml
<FilePackage Id="logs.all" Title="Logs/All">
    <FileRef Id="log.*" MachineId="*" AppId="*"/>     <!-- log.viewer, log.mt.ios, ... -->
</FilePackage>
```

`*` matches any run of characters and `?` any one, without regard to letter case (a Win32 name match). `log.*` therefore matches
`log.viewer` and `log.mt.ios` but **not** `logs.x` and not `log` itself (there is no dot after it); to take both write two references, or give the
single one the id `log.main`. See [`<FileRef>`](#fileref) for the rest of the matching rules.

Keep the kinds apart, because a wildcard takes everything of its kind: `AppId="*"` matches machine-level definitions too (they have no
app), so `<FileRef Id="log.*" MachineId="m1" AppId="*"/>` also collects a machine log named `log.dirigent`. Give files of another nature
their own first segment - `dirigent.log`, `dds.log` - and a package that wants them names that kind as well.

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

### Variables in `Path`, `Mask` and `Title`

One list of variables serves the launch of an app, the `Path` and `Mask` of its files and, in part, the `Title`.
The file paths start from **the very set the launcher expands `ExeFullPath`, `StartupDir` and `CmdLineArgs` with**
(computed by the same code, `AppLaunchSpec`) and add what only a file has.

| Variable | Value | Launch | `Path` / `Mask` | `Title` (at load) |
| --- | --- | :-: | :-: | :-: |
| environment variables (`%TEMP%`, `%ProgramData%`, ...) | the environment of the agent on the owning machine | yes | yes | no |
| global variables (`SetVars` and the agent's own) | as set on that agent | yes | yes (also for a file definition of a machine, not of an app) | no |
| `MACHINE_ID` | machine of the app / of the file definition | yes | yes | yes |
| `DIRIGENT_MACHINE_ID` | machine of the file definition | no | yes | no |
| `MACHINE_IP`, `DIRIGENT_MACHINE_IP` | IP address of the machine, looked up only if used | no | yes | no |
| `DOWNLOADS` | download folder of the agent's user | no | yes | no |
| `APP_ID` | app name without the machine part | yes | app-bound file definitions | yes |
| `DIRIGENT_APPID`, `DIRIGENT_MACHINEID`, `DIRIGENT_PLAN`, `DIRIGENT_MASTER_IP` | app, machine, plan of the app, address of the master | yes | app-bound file definitions | no |
| `APP_BINDIR` | folder of the executable (from `ExeFullPath`) | yes | app-bound file definitions | no |
| `APP_STARTUPDIR` | the startup folder (from `StartupDir`) | yes | app-bound file definitions | no |
| `APP_STARTUPDIR_NAME` | the **last folder name** of `StartupDir`, trailing separators ignored: `..\RenderHost-3801` gives `RenderHost-3801` | yes | app-bound file definitions | yes, if `StartupDir` needs only the `<Local>` variables |
| the app's `<Local>` variables | the `<Env><Local>` elements of the app | yes | app-bound file definitions | yes |
| the app's `<Set>` variables | the `<Env><Set>` elements; also go to the environment of the process | yes | app-bound file definitions | no |

**Precedence.** For a file path a `<Set>` variable wins over a `<Local>` or other launcher variable of the same name (it
always did), except for the four that say which app this is - `APP_ID`, `DIRIGENT_APPID`, `APP_BINDIR`, `APP_STARTUPDIR` -
which a `<Set>` can not replace. Variables only given at `StartApp` time are not known to a file path. A variable that is
not defined expands to nothing, as before.

The same variables work in `Mask` - on `<Folder>` and on `<File Filter="Newest">` - under the same
conditions as in `Path`: the file definition must belong to a machine, and the expansion happens only on that
machine. The machine variables and the app's own are available only to a file definition whose machine is the
one doing the expansion (the agent of the machine owning the file definition); anywhere else the mask is left
as written.

`Title` is different: it is expanded **once, when the config is loaded**. It takes `%MACHINE_ID%` and `%APP_ID%`
(any letter case), with the ids the file definition has by then - which for a file definition in an `<AppTemplate>` are those of each app
using the template - and, for a file definition of an app, the app's `<Local>` variables and `%APP_STARTUPDIR_NAME%`. The last one
is worked out from the `StartupDir` **as written**, with the `<Local>` variables put in; if it still needs the
environment (`%SOMEWHERE%\...`) it is not guessed and the token stays literal. A token that is unknown, or whose id is not
known (a global file definition, or a `<FileRef>` with `MachineId="*"`), stays as written. The same applies to the `Title` of
`<Tool>` and `<Script>` actions and of menu items bound to a machine or an app (those take the two ids only).

This is what lets one template serve every app whose log file is named after the app:

<!-- example: loads -->
```xml
<AppTemplate Name="apps.viewer" ExeFullPath="C:\Apps\Viewer\Release\viewer.exe" StartupDir="C:\Apps\Viewer\Release\">
    <File Id="log.viewer" Title="Log/%APP_ID%" Path="C:\Apps\Viewer\logs" Mask="%APP_ID%*.log"
          Filter="Newest" MaxFiles="3"/>
</AppTemplate>
<AppTemplate Name="apps.Viewer_SE" Template="apps.viewer" ExeFullPath="C:\Apps\Viewer\Release\SE.exe"/>

<App AppIdTuple="m1.Viewer_SE" Template="apps.Viewer_SE"/>
```

The app `m1.Viewer_SE` gets a file definition titled `Log/Viewer_SE` collecting `Viewer_SE*.log` from the folder. An app
whose log is named differently overrides the mask on its own `<File>`.

Apps that share a template but run from folders of their own name can say so the same way: with
`StartupDir="..\RenderHost-3801"` and `<File Id="log" Title="Log/%APP_STARTUPDIR_NAME%" Path="C:\IG\Logs\%APP_STARTUPDIR_NAME%" Mask="*.log"/>`
in the template the app gets the title `Log/RenderHost-3801` and collects `C:\IG\Logs\RenderHost-3801`.

A definition of a derived template, or of the `<App>` itself, with the same `Id` as an inherited one
*replaces* it, whatever else differs - see [Templates](SharedConfig.md#AppTemplate-replacing-inherited-nodes).

### File templates

File definitions of one kind tend to repeat the same attributes - on a site that collects logs, `Filter`,
`MaxFiles`, `MaxSeconds`, `TailBytes` and `Clearable` on every log file definition. A top-level
`<FileTemplate>` carries them once.

<!-- example: loads -->
```xml
<Shared>
    <FileTemplate Name="log" Filter="Newest" MaxFiles="3" MaxSeconds="604800"
                  TailBytes="10485760" Clearable="1"/>

    <Machine Name="m1" IP="192.168.0.10">
        <Share Name="C" Path="C:\"/>
        <File Template="log" Id="log.se"  Title="Log/SE"  Path="C:\Apps\Viewer\logs" Mask="SE*.log"/>
        <File Template="log" Id="log.bdc" Title="Log/BDC" Path="C:\Apps\Viewer\logs" Mask="BDC*.log"
              MaxFiles="2"/>
    </Machine>
</Shared>
```

Both file definitions are *newest 3 files from the last week, tail 10 MB, clearable* - except that the second
keeps only 2 files, because a file definition's own attribute beats the template's.

* **Where it applies.** `Template="name"` may be written on `<File>`, `<Folder>`, `<VFolder>`,
  `<FilePackage>` and `<FileRef>`, wherever they are: at the top level, in a `<Machine>`, an `<App>`,
  an `<AppTemplate>`, inside other file definitions, in menus. In an `<AppTemplate>` the template's attributes
  are merged first, and the file definition is then read once per app as usual.
* **The merge.** The template's attributes are defaults; the file definition's own attributes win. The
  `Template` attribute itself disappears. Nothing about the file definition is guessed: what is not on the file definition
  or the template keeps its usual default.
* **Child elements.** `<Tool>` and `<Script>` actions (and nested file definitions) inside the template are
  copied into every file definition using it, **in front of** the file definition's own children. A file definition's own action
  with the same `Id` as one of the template's therefore replaces it; one with a new `Id` is added.
* **Chaining.** A `<FileTemplate>` may itself carry `Template="base"`. The derived template's
  attributes beat the base's, and the children are concatenated base first.
* **Not a file definition.** A `<FileTemplate>` is not a file definition and is never listed or referenced.
  Template names are their own namespace: they do not clash with `<AppTemplate>` names.

Mistakes are load errors (the master refuses the config, a reload keeps the previous one):

| Message says | Cause |
| --- | --- |
| `FileTemplate 'x' not found` | a file definition or template names a template that does not exist |
| `FileTemplate cycle: a -> b -> a` | templates that lead back to themselves, through `Template=` or through file definitions inside their children |
| `FileTemplate without a Name` | a `<FileTemplate>` has no `Name` |
| `Duplicate FileTemplate 'x'` | two templates with the same `Name` |
| `FileTemplate 'x' must not have a Guid` | every file definition made from the template would get the same `Guid` |

A config with no `<FileTemplate>` and no `Template=` on a file definition is read exactly as before.

### Actions on file definitions

What can be *done* with a file definition is expressed by `<Tool>` and `<Script>` child elements - see
[Actions](Actions.md) for the general mechanism and [Tools](Tools.md) / [Scripts](Scripts.md)
for the two kinds.

```xml
<File Id="log" Title="Log file" Path="%APP_STARTUPDIR%\logs\app.log">
    <Tool   Title="Open in Notepad++"    Name="Notepad++" Args="%FILE_PATH%"/>
    <Script Title="Download zipped file" Name="BuiltIns/DownloadZipped.cs" Icon="Icons/Zipped.png"/>
</File>
```

Actions are started **after** the file definition has been resolved, and receive the result:

| File definition resolves to                            | `%FILE_PATH%` contains                                        | Script's `VfsNode` argument |
| ------------------------------------------- | ------------------------------------------------------------- | --------------------------- |
| a single file                               | the resolved path (UNC if not on the requestor's machine)      | the resolved file definition      |
| a container with a real path (`<Folder>`)   | the resolved folder path                                      | the whole resolved tree     |
| a container with no real path (package, virtual folder) | all contained file paths, each double-quoted, space separated | the whole resolved tree |

Actions that should be offered on *every* file or *every* package need not be repeated: put them
in `LocalConfig.xml` under `<DefaultFileActions>` and `<DefaultFilePackageActions>`. Which of the
two applies is decided **after** resolution, from whether the file definition resolved to a container.

## Looking the files up

Every action below starts by **resolving** the file definition it was given, and resolution is where the time
of a collection on a large site goes.

The reason is that a declaration is not an answer. `<Folder Path="C:\Logs" Mask="*.log"/>` on
machine `m17` says nothing about which files are in that folder right now, and only `m17` can say -
so the machine holding a file definition is the machine that resolves it, over the network. A package of
thirty file definitions spread over forty machines is therefore not a local computation at all; it is a
conversation with the site.

What that conversation costs is the thing worth knowing:

- The tree is walked **here**, on the machine that was asked - a `<FileRef>` is looked up in the
  registry, a package expands into its members - and every file definition belonging to somebody else is set
  aside rather than resolved.
- When the walk is done, each machine is asked **once**, about **all** of its own file definitions, and all
  the machines are asked **at the same time**.

So the lookup costs **one round trip**, whatever the size of the package and however many machines
it spans. It used to cost one round trip per file definition, taken one after another, which is why a
system-wide collection used to spend longer looking for the files than fetching them.

Two consequences worth remembering:

**A file definition that cannot be resolved costs only itself.** File definitions travel together to save a round trip,
but they remain separate things the operator asked for. A folder that has never existed on one
machine, or a machine that does not answer at all, is left out of the tree and the reason is
recorded as a note on the container - which reaches the archive's `_incomplete.txt` and the closing
message. Only a file definition asked for *on its own* fails out loud, because there is then nothing else to
deliver. See [Silent misses](#limitations-and-known-issues) for the case where nothing is reported.

**Batching is not backward compatible, and does not need to be.** A machine running an older
Dirigent would not understand a request about several file definitions. A site is upgraded as a whole, so this
is not a case that arises; it is worth knowing before mixing versions deliberately.

Covered by `ResolutionBatchingTests` (tier 0 - the number of round trips, that they overlap, and
that a failure still costs only its own file definition) and `BatchedResolveTests` (tier 1 - a real agent
answering about many file definitions over a real connection). Tier 1 cannot show the cost: every machine of a
test bed answers on loopback, so the master treats them all as itself and no round trip is ever
made.

## Built-in actions

### `BuiltIns/DownloadZipped.cs` - download as ZIP

The workhorse for getting files off the system. Applicable to any file definition - a single file, a folder,
a package.

What it does:

1. Resolves the file definition and collects the set of machines owning files in it.
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

* Folders are named after the `Title` of the containing file definition. Files belonging to an app
  additionally go into a subfolder named after the app, so that the same-named log files of
  several apps do not clash. Remaining name clashes get a `_2`, `_3`, ... suffix.
* That app subfolder is left out when a folder of the same name is already on the path, which is
  the case wherever a container is named after the app itself - a file definition titled or id'd like the app,
  or an untitled `<Folder>` over the app's own directory. Without it such archives read
  `log/viewer/viewer/app.log`.
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
* Callers with no resolved file definition tree - the CLI, REST, another script - name the file definition with a
  `Node` selector in the arguments instead, and the script resolves it. See
  [Files without a GUI](#files-without-a-gui).
* `AskComment="1"` on the action asks the operator for a note before anything starts - see
  [Asking for a comment](#asking-for-a-comment).
* One archive is always produced. `Args="perMachine"`, which used to deliver one per machine and
  skip the merging, is **withdrawn**: analysis is normally cross-machine, so a single file is what
  the recipient wants, and one output shape keeps the rest of this page short. An action still
  carrying that word logs a warning and gets the single archive.

#### Asking for a comment

An action carrying `AskComment="1"` puts a dialog in front of the collection: the file definition's
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

Symphony froze during the 14:20 run, right after the Scene reload.
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

`AskComment` is not specific to downloads. Any `<Script>` action can carry it - on a file definition, an
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

#### What the operator sees while it runs

A download started from the GUI gets one indicator in the status bar, with a cancel button. What it
is told, in order (`DownloadProgressShapeTests` records it):

```
  Starting     -
  Running      -                                     <- the runner: it has begun
  Running      -    Looking up the files...          <- one call per machine, all at once
  Running      5%   Collecting from 2 machine(s)...  <- weighted by bytes
  Running     85%   Merging the collected files...
  Finished   100%
```

| Phase | Share of the bar | Where the number comes from |
| --- | --- | --- |
| looking the package up | none - a sweeping bar | nothing about it is measurable in advance |
| the machines collecting | 0.05 - 0.85 | each machine's own bytes done, weighted by the bytes it announced |
| merging the parts | 0.85 - 1.00 | entries copied of entries total |

A phase with no number sweeps rather than sitting at zero: a bar frozen at 0% is what a hang looks
like. A phase that cannot measure itself publishes its name instead, so the indicator always says
something. See [Publishing status and progress](Scripts.md#publishing-status-and-progress) for the
general rule, and [Cancellation](Scripts.md#cancellation) for what makes the cross honest.

**Cancelling** removes the archive, the `.part` file and the staging folder, and stops the machines
rather than letting them finish in the background. The closing message box is skipped - the
operator knows, they asked for it.

Only the GUI that started the download shows it. A download started from the CLI, the REST surface
or another script reports to whoever asked for it, through the script state.

### `BuiltIns/ClearFiles.cs`, `MarkFiles.cs`, `UnmarkFiles.cs` - delimit a test run

Applicable to any file definition, like the download. Each starts a slave (`BuiltIns/MarkFilesSlave.cs`) on
every machine holding files of the file definition, follows them in the status bar, and ends in a message box
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

The `cfg` definitions need no exclusion: they are not `Clearable`, so Clear and Mark pass over them and
the archive still gets them whole.

### `BuiltIns/BrowseInDblCmdVirtPanel.cs` - browse in Double Commander

Resolves the file definition, writes a Double Commander *virtual panel* list describing the resolved tree,
and opens Double Commander on it - letting the user walk a tree of files gathered from several
machines as if it were one folder.

Requires a `DoubleCommander` tool defined in `LocalConfig.xml`, pointing to a build that supports
the `--startupscript` option (see <https://github.com/pjanec/doublecmd>).

### `BuiltIns/ListVfsNodes.cs` - list what is declared

Not an action on a file definition but a query: it returns the declared file definitions, optionally filtered, which is
how a caller with no GUI finds out what there is to ask for. Declarations only - nothing is looked
up in any file system.

```json
{ "Filter": { "Id": "log", "MachineId": "m1", "AppId": "*" } }
```

Every part of the filter is optional; leaving out `Filter` lists everything. The result is one
record per file definition - `Id`, `Guid`, `Type`, `MachineId`, `AppId`, `Title` and the *declared* `Path`.

## Where files appear in the UI

Currently the VFS is exposed by the **WinForms GUI** only:

* **App context menu** - the file definitions declared for that app, including those inherited from its
  `<AppTemplate>`.
* **Machine context menu** - the file definitions declared for that machine.
* **Main menu** - the file definitions declared in `<MainMenu>`; the natural home for whole-system packages,
  since they belong to no single app or machine.
* **Files tab** - all the declared file definitions in one sortable, filterable grid: machine, app, id, type,
  path and status. The right-click menu offers the file definition's actions, the same as in the context
  menus above.

Each file definition contributes one menu item carrying its actions as a submenu. `Title` segments
(`"Logs/Recent"`) become submenu levels, so a set of packages can be organised into a menu tree.

The *Status* column of the Files tab shows `<machine> offline` for the file definitions whose machine is not
currently connected. Choosing **Resolve** from the right-click menu (or double-clicking the row)
resolves that one file definition and reports what it currently points to - `Found`, `Missing`, `Not found`,
the number of files of a container, or the error - and rewrites the *Path* cell with the resolved
path(s). Resolution touches the remote file systems, so it happens only when asked for, never on
the periodic refresh.

The ImGui GUI does not render file definitions. Everything else reaches files through the built-in
scripts - see [Files without a GUI](#files-without-a-gui).

## Files without a GUI

There is no `DownloadFile` command, and none is needed: the built-in scripts are the interface,
and `StartScript` plus `GetScriptState` already carry them. This works over the CLI, over
`POST /cli`, and from another script.

The one thing a script cannot be handed from outside is a **resolved** file definition tree - resolving is a
remote operation. So each VFS script accepts a *selector* instead, naming the file definition the way a
`<FileRef>` does, and resolves it itself:

```json
{ "Node": { "Id": "log", "MachineId": "m1", "AppId": "camera" } }
```

`MachineId` and `AppId` are filters and default to `*`, so `{"Node":{"Id":"logs.all"}}` means
"the file definition called `logs.all`, wherever it is". Only top-level file definitions can be named - see
[Where file definitions can be declared](#where-file-definitions-can-be-declared).

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

### One log file definition for every app, declared once

Because an `<AppTemplate>` is parsed once per app that uses it - with that app's machine and app
id - a single declaration in the template yields one properly bound file definition per app:

<!-- example: loads -->
```xml
<AppTemplate Name="apps.base" ExeFullPath="C:\apps\app.exe">
    <!-- up to 10 *.log files from the app's log folder, none older than 2 days -->
    <File Id="log" Title="Recent logs" Path="%APP_STARTUPDIR%\logs"
          Mask="*.log" Filter="Newest" MaxFiles="10" MaxSeconds="172800">
        <Tool   Title="Open newest in Notepad++" Name="Notepad++" Args="%FILE_PATH%"/>
        <Script Title="Download zipped"          Name="BuiltIns/DownloadZipped.cs"/>
    </File>
</AppTemplate>

<App AppIdTuple="m1.camera"   Template="apps.base" StartupDir="D:\apps\camera"/>
<App AppIdTuple="m1.tracker"  Template="apps.base" StartupDir="D:\apps\tracker"/>
<App AppIdTuple="m2.recorder" Template="apps.base" StartupDir="E:\apps\recorder"/>
```

Every one of those apps now has *Recent logs* in its context menu, resolved against its own
startup folder on its own machine.

When many file definitions repeat the same attributes, write them once in a
[`<FileTemplate>`](#file-templates); and `Title="Log/%APP_ID%"` / `Mask="%APP_ID%*.log"` give each
app its own entry from this one declaration (see [Variables](#variables-in-path-mask-and-title)).

### Bundles built from those file definitions

Nothing needs to be declared twice: `<FileRef>` collects the existing file definitions by id.

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

<!-- example: loads -->
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

<!-- example: loads -->
```xml
<App AppIdTuple="m1.camera" ExeFullPath="D:\apps\camera\camera.exe" StartupDir="D:\apps\camera">
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
verb, and the ImGui GUI does not render file definitions at all.

**File definitions nested in a container are not referenceable.** Only the file definitions declared directly under
`<Shared>`, `<Machine>`, `<App>` or `<AppTemplate>` can be found by a `<FileRef>`. See
[Where file definitions can be declared](#where-file-definitions-can-be-declared).

**Silent misses.** A file definition that resolves to nothing - an unmatched `<FileRef>`, a
`Filter="Newest"` folder with no matching file - makes its menu item do nothing at all when
clicked, without a message. The Files tab is the way to tell: its **Resolve** command reports
`Not found` for the same file definition. *Download zipped* is the exception: it resolves the file definition itself and
reports a file definition that yields nothing as a failed operation, visible in the status bar.

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

**`<Folder>` limits are per file definition, not per download.** A package pulling in several `<Folder>`
file definitions can still add up to a large archive, as `MaxTotalBytes` bounds each file definition separately.

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
* [Scripts](Scripts.md) - writing scripts, including ones that consume a resolved file definition tree
* [SharedConfig](SharedConfig.md) - the surrounding configuration file
* [LocalConfig](LocalConfig.md) - tool definitions and default actions
* [`config/SharedConfig.xml`](../config/SharedConfig.xml) - a working example of most of the above
