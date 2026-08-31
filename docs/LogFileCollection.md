# Collecting Log Files from Multiple Machines

A cookbook: how to configure Dirigent so that the log files of applications spread over several
machines can be opened, browsed and downloaded as one bundle.

This page shows *recipes*. For the concepts behind them - what a VfsNode is, when a definition
turns into actual files, and the full attribute reference - see [Files.md](Files.md).

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Configuration Approaches](#configuration-approaches)
3. [Step-by-Step Examples](#step-by-step-examples)
4. [Advanced Patterns](#advanced-patterns)
5. [Complete Examples](#complete-examples)
6. [Tips and Best Practices](#tips-and-best-practices)
7. [Troubleshooting](#troubleshooting)

## Prerequisites

### 1. Configure Machine Shares

Before you can access files from remote machines, you must configure file shares for each
machine. Shares are what lets Dirigent turn a local path on another machine into a UNC path the
requestor can open.

**Example Machine Configuration:**

```xml
<Machine Name="station1" IP="192.168.1.100">
    <!-- File shares for UNC path conversion -->
    <!-- Name: Share name used in UNC paths (\\IP\Name\path) -->
    <!-- Path: Local path on the machine (must be absolute) -->
    <Share Name="C" Path="C:\"/>
    <Share Name="D" Path="D:\"/>
    <Share Name="Logs" Path="C:\Logs"/>  <!-- optional: a dedicated share for the logs -->
</Machine>

<Machine Name="station2" IP="192.168.1.101">
    <Share Name="C" Path="C:\"/>
    <Share Name="E" Path="E:\"/>
</Machine>
```

**Important Notes:**
- Share `Path` must be an absolute path
- Share `Name` is used in UNC paths: `\\{MachineIP}\{ShareName}\{relative_path}`
- Windows file shares must be accessible without additional credentials (or credentials must be cached)
- Where several shares cover the same file, the **most specific** one is used - with both `C:\` and
  `C:\Logs` declared as above, log files go through the `Logs` share, which is usually the one whose
  permissions were set up for them

### 2. Give the Downloading Machine a Share Too

*Download zipped* has every participating machine zip its own files and copy the archive to the
machine the download goes to. Those copies travel through a share of the **requestor's** machine,
so that machine needs a `<Share>` covering its download folder - typically the whole drive:

```xml
<!-- the machine the operator's GUI runs on -->
<Machine Name="operator1" IP="192.168.1.50">
    <Share Name="C" Path="C:\"/>
</Machine>
```

Without it, only the machines that share the requestor's disks can deliver their part; the rest
are reported as errors in the final message. The download folder itself defaults to the Downloads
folder of the user the agent runs as, and can be changed with the agent's `--downloadFolder`
option.

## Configuration Approaches

There are three main approaches to configure log file collection:

1. **App-Scoped**: Define log files within each `<App>` (or, better, once in an `<AppTemplate>`)
2. **Machine-Scoped**: Define log files within each `<Machine>` section
3. **Global-Scoped**: Define log files at the top level, then reference them

Each approach has its use cases:

- **App-Scoped**: Best when each application has its own log files
- **Machine-Scoped**: Best when collecting all logs from a machine regardless of app
- **Global-Scoped**: Best for centralized log file definitions

They mix freely - one package can reference nodes declared in all three places.

## Step-by-Step Examples

### Approach 1: App-Scoped Log Files

Define log files within each application, then collect them using a FilePackage.

#### Step 1: Define Log Files in Each App

```xml
<App AppIdTuple="station1.webapp" ExeFullPath="C:\Apps\webapp.exe">
    <File Id="webapp_log" Path="C:\Logs\webapp.log"/>
    <File Id="webapp_error_log" Path="C:\Logs\webapp_errors.log"/>
    <Folder Id="webapp_log_folder" Path="C:\Logs\webapp" Mask="*.log"/>
</App>

<App AppIdTuple="station1.database" ExeFullPath="C:\Apps\db.exe">
    <File Id="db_log" Path="C:\Logs\database.log"/>
    <Folder Id="db_log_folder" Path="C:\Logs\database" Mask="*.log"/>
</App>

<App AppIdTuple="station2.webapp" ExeFullPath="E:\Apps\webapp.exe">
    <File Id="webapp_log" Path="E:\Applications\Logs\webapp.log"/>
    <Folder Id="webapp_log_folder" Path="E:\Applications\Logs\webapp" Mask="*.log"/>
</App>
```

Nodes declared in an `<App>` inherit that app's machine and app id, so `webapp_log` above exists
twice - once bound to `station1.webapp`, once to `station2.webapp`. That is what makes collecting
both with a single reference possible.

The same works inside a `<Plan>`'s `<App>` elements, but then the declaration has to be repeated
in every plan the app appears in. Declare the files with the app itself, at the top level of
`<Shared>`, and every plan inherits them.

#### Step 2: Create a FilePackage to Collect All Logs

```xml
<!-- Global FilePackage to collect logs from all stations -->
<FilePackage Id="all_production_logs" Title="Logs/All production logs">
    <!-- Collect webapp logs from all machines -->
    <FileRef Id="webapp_log" MachineId="*" AppId="*"/>
    <FileRef Id="webapp_error_log" MachineId="*" AppId="*"/>

    <!-- Collect database logs from all machines -->
    <FileRef Id="db_log" MachineId="*" AppId="*"/>

    <!-- Collect log folders from all machines -->
    <FileRef Id="webapp_log_folder" MachineId="*" AppId="*"/>
    <FileRef Id="db_log_folder" MachineId="*" AppId="*"/>
</FilePackage>
```

`MachineId="*"` and `AppId="*"` mean *any*; an empty string (`""`) does the same. What you must
not do is leave the attributes out - an omitted `MachineId` / `AppId` means *inherited from the
declaration site*, which inside an `<App>` element narrows the reference down to that one app.

#### Step 3: Collect Logs from Specific Stations Only

```xml
<FilePackage Id="station1_logs_only" Title="Logs/Station 1">
    <!-- Only collect from station1 -->
    <FileRef Id="webapp_log" MachineId="station1" AppId="*"/>
    <FileRef Id="webapp_error_log" MachineId="station1" AppId="*"/>
    <FileRef Id="db_log" MachineId="station1" AppId="*"/>
</FilePackage>
```

#### Step 4: Make the Package Reachable

A `<FilePackage>` declared at the top level of `<Shared>` is in the registry but in **no menu**.
Put it in the GUI's main menu bar to be able to click it:

```xml
<MainMenu>
    <FileRef Title="File/Logs/All production logs" Id="all_production_logs"/>
    <FileRef Title="File/Logs/Station 1"           Id="station1_logs_only"/>
</MainMenu>
```

The package then offers the *package* default actions from `LocalConfig.xml` - *Download zipped
package* and *Browse* in the shipped example config. This step is easy to forget: a perfectly
correct package that appears nowhere is the most common "it does not work".

### Approach 2: Machine-Scoped Log Files

Define log files at the machine level, useful for collecting all logs from a machine.

#### Step 1: Define Log Files in Machine Sections

```xml
<Machine Name="station1" IP="192.168.1.100">
    <Share Name="C" Path="C:\"/>

    <!-- Collect newest log files (up to 10) -->
    <File Id="newest_logs" Path="C:\Logs" Mask="*.log" Filter="Newest" MaxFiles="10">
        <Script Title="Download zipped" Name="BuiltIns/DownloadZipped.cs"/>
    </File>

    <!-- Collect all logs from a folder tree -->
    <Folder Id="all_logs" Path="C:\Logs" Mask="*.log">
        <Tool Title="Open in Explorer" Name="WinExplorer"/>
    </Folder>

    <!-- Collect recent logs (max 5, max 1 hour old) -->
    <File Id="recent_logs" Path="C:\Logs" Mask="*.log" Filter="Newest" MaxFiles="5" MaxSeconds="3600">
        <Script Title="Download zipped" Name="BuiltIns/DownloadZipped.cs"/>
    </File>
</Machine>

<Machine Name="station2" IP="192.168.1.101">
    <Share Name="E" Path="E:\"/>

    <File Id="newest_logs" Path="E:\Applications\Logs" Mask="*.log" Filter="Newest" MaxFiles="10"/>
    <Folder Id="all_logs" Path="E:\Applications\Logs" Mask="*.log"/>
</Machine>
```

Two different mechanisms are at work here, and they are easy to mix up:

* `<File Filter="Newest">` treats `Path` as a **folder** and picks the newest files **directly in
  it** - its mask never descends into subfolders. Without `MaxFiles` it yields a single file.
* `<Folder>` scans the whole tree. A mask with no path separator - `*.log` - applies at **any
  depth**, so the longer `**/*.log` is only needed when you want to spell it out.

#### Step 2: Create a FilePackage to Collect from Multiple Machines

```xml
<!-- named machines -->
<FilePackage Id="all_station_logs" Title="Logs/Stations 1 and 2">
    <FileRef Id="newest_logs" MachineId="station1" AppId="*"/>
    <FileRef Id="newest_logs" MachineId="station2" AppId="*"/>
</FilePackage>

<!-- or every machine that declares such a node, current and future -->
<FilePackage Id="all_machine_logs" Title="Logs/All machines">
    <FileRef Id="newest_logs" MachineId="*" AppId="*"/>
</FilePackage>
```

Use one or the other - listing both a wildcard and the individual machines would collect the same
files twice.

### Approach 3: Global Log File Definitions

Define log files globally, then reference them selectively.

#### Step 1: Define Global Log Files

```xml
<!-- Global log file definitions -->
<File Id="webapp_log_station1" MachineId="station1" Path="C:\Logs\webapp.log"/>
<File Id="webapp_log_station2" MachineId="station2" Path="E:\Applications\Logs\webapp.log"/>
<File Id="db_log_station1" MachineId="station1" Path="C:\Logs\database.log"/>
<File Id="db_log_station2" MachineId="station2" Path="E:\Applications\Logs\database.log"/>
```

The `MachineId` matters: it says on whose file system the path is to be resolved. A top-level
`<File>` **without** it is a global node and must be given a UNC path - the config reader rejects
a plain local one.

#### Step 2: Create Selective FilePackages

```xml
<!-- Collect only webapp logs from all stations -->
<FilePackage Id="webapp_logs_all_stations" Title="Logs/Webapp">
    <FileRef Id="webapp_log_station1" MachineId="*" AppId="*"/>
    <FileRef Id="webapp_log_station2" MachineId="*" AppId="*"/>
</FilePackage>

<!-- ... or, with a naming convention, in one line -->
<FilePackage Id="webapp_logs_short" Title="Logs/Webapp">
    <FileRef Id="webapp_log_*" MachineId="*" AppId="*"/>
</FilePackage>
```

## Advanced Patterns

### Declaring the Files Once for Many Apps

Repeating the same `<File>` in every `<App>` is what `<AppTemplate>` exists to avoid. The
template is parsed once per app that uses it, with that app's ids, so one declaration yields one
correctly bound node per app:

```xml
<AppTemplate Name="apps.base">
    <File Id="log" Title="Recent logs" Path="%APP_STARTUPDIR%\logs"
          Mask="*.log" Filter="Newest" MaxFiles="10" MaxSeconds="172800"/>
</AppTemplate>

<App AppIdTuple="station1.webapp"   Template="apps.base" ExeFullPath="..." StartupDir="C:\apps\webapp"/>
<App AppIdTuple="station1.database" Template="apps.base" ExeFullPath="..." StartupDir="C:\apps\db"/>
<App AppIdTuple="station2.webapp"   Template="apps.base" ExeFullPath="..." StartupDir="E:\apps\webapp"/>

<FilePackage Id="logs.all" Title="Logs/All apps (2 days)">
    <FileRef Id="log" MachineId="*" AppId="*"/>
</FilePackage>
```

Each app's `%APP_STARTUPDIR%` is expanded on its own machine, so the one template covers the whole
system.

### Using Wildcards in FileRef

You can use wildcard patterns to match multiple nodes by ID:

```xml
<FilePackage Id="all_newest_logs" Title="Logs/Newest">
    <!-- Match all nodes with IDs starting with "newest" -->
    <FileRef Id="newest*" MachineId="*" AppId="*"/>

    <!-- Match all log nodes from station1 -->
    <FileRef Id="*_log" MachineId="station1" AppId="*"/>
</FilePackage>
```

### Using Empty Values for Matching

An empty `MachineId` or `AppId` in a `FileRef` matches any value, exactly like `*`:

```xml
<FilePackage Id="flexible_collection">
    <!-- Match "app_log" from any machine, any app -->
    <FileRef Id="app_log" MachineId="" AppId=""/>

    <!-- Match "app_log" from station1, any app -->
    <FileRef Id="app_log" MachineId="station1" AppId=""/>

    <!-- Match "app_log" from any machine, but only from the "webapp" app -->
    <FileRef Id="app_log" MachineId="" AppId="webapp"/>
</FilePackage>
```

Both `""` and `*` also match nodes whose field is unset - a machine-scoped node has no app id, and
is still found by `AppId=""` or `AppId="*"`. A concrete pattern such as `webapp` never matches an
unset field.

### Using Folders with Glob Patterns

Collect multiple files using folder definitions with glob-style masks:

```xml
<FilePackage Id="comprehensive_logs" Title="Logs/Everything on station1">
    <!-- All .log files in the whole tree (a mask without a separator applies at any depth) -->
    <Folder Id="station1_all_logs" MachineId="station1" Path="C:\Logs" Mask="*.log"/>

    <!-- Collect specific log types -->
    <Folder Id="error_logs" MachineId="station1" Path="C:\Logs" Mask="*error*.log"/>

    <!-- Only under the subfolders whose name starts with "app" -->
    <Folder Id="app_logs" MachineId="station1" Path="C:\Logs" Mask="app*/**/*.{log,txt}"/>

    <!-- Keep a growing tree bounded: newest 200 files, 2 days, 50 MB -->
    <Folder Id="bounded_logs" MachineId="station1" Path="C:\Logs" Mask="*.log"
            MaxFiles="200" MaxSeconds="172800" MaxTotalBytes="52428800"/>
</FilePackage>
```

These `<Folder>` nodes are written *inside* the package, which is fine as content but leaves them
unreferenceable - only nodes declared directly under `<Shared>`, `<Machine>`, `<App>` or
`<AppTemplate>` can be found by a `<FileRef>`.

### Asking the Operator Why

A bundle is usually collected because something went wrong, and the reason is worth keeping with
it. `AskComment="1"` on the download action shows the package's `Description` and takes a note
before the collection starts:

```xml
<FilePackage Id="all_production_logs" Title="Logs/All production logs"
             Description="Every application's recent log from both stations, plus the configs.">
    <FileRef Id="webapp_log" MachineId="*" AppId="*"/>
    <Script Title="Download zipped package" Name="BuiltIns/DownloadZipped.cs" AskComment="1"/>
</FilePackage>
```

The archive then holds `_comment.txt` at its root with the note, above a header naming the package,
the machines and their addresses, the time and the Dirigent version. Cancelling the dialog collects
nothing. See [Asking for a comment](Files.md#asking-for-a-comment).

### Logs Too Big to Download

A logger that never rotates grows one file to tens of gigabytes, which no download can carry.
`TailBytes` collects only the end of such a file - which is the part an investigation wants
anyway:

```xml
<Machine Name="station1" IP="192.168.1.100">
    <Share Name="C" Path="C:\"/>

    <!-- the last 50 MB of any log in the tree bigger than that; smaller ones come whole -->
    <Folder Id="all_logs" Path="C:\Logs" Mask="*.log" TailBytes="52428800"/>

    <!-- or one known-huge file -->
    <File Id="trace_log" Path="C:\Logs\trace.log" TailBytes="10485760"/>
</Machine>
```

Only those last bytes are read and compressed, so collecting the tail of a 60 GB file costs no
more than collecting a 50 MB one. The cut is moved to the next line break, the entry is named
`trace.last10MB.log` so the truncation shows in the archive listing, and its first line says which
file it came from and how big that file was. See
[Files too big to collect whole](Files.md#files-too-big-to-collect-whole) for the details.

Rotation itself is the producing application's job - Dirigent takes the files as they are.
`TailBytes` is what makes an unrotated one collectable at all.

### Using Filters for Recent Files

Use the `Filter="Newest"` attribute to collect only the most recent files of one folder:

```xml
<Machine Name="station1" IP="192.168.1.100">
    <Share Name="C" Path="C:\"/>

    <!-- Single newest log file (MaxFiles defaults to 1) -->
    <File Id="latest_log" Path="C:\Logs" Mask="*.log" Filter="Newest"/>

    <!-- Top 5 newest log files -->
    <File Id="top5_logs" Path="C:\Logs" Mask="*.log" Filter="Newest" MaxFiles="5"/>

    <!-- Newest logs from last hour (max 10 files) -->
    <File Id="recent_logs" Path="C:\Logs" Mask="*.log" Filter="Newest" MaxFiles="10" MaxSeconds="3600"/>
</Machine>
```

With `MaxFiles="1"` the node resolves to a single file, with more to a virtual folder named after
the node's `Title`.

### Organizing with Virtual Folders

Use `VFolder` to organize collected logs into a hierarchical structure. The `Title` of each
becomes a folder name inside the downloaded ZIP:

```xml
<FilePackage Id="organized_logs" Title="Logs/Incident report">
    <VFolder Title="station1">
        <FileRef Id="webapp_log" MachineId="station1" AppId="*"/>
        <FileRef Id="db_log" MachineId="station1" AppId="*"/>
    </VFolder>

    <VFolder Title="station2">
        <FileRef Id="webapp_log" MachineId="station2" AppId="*"/>
        <FileRef Id="db_log" MachineId="station2" AppId="*"/>
    </VFolder>

    <VFolder Title="errors">
        <FileRef Id="webapp_error_log" MachineId="*" AppId="*"/>
    </VFolder>
</FilePackage>
```

## Complete Examples

### Example 1: Collecting Logs from a Subset of Apps on Specific Stations

**Scenario**: Collect logs from the `webapp` and `database` apps running on `station1` and
`station2`, but not from other apps.

```xml
<Shared>
    <!-- Machine definitions with shares -->
    <Machine Name="station1" IP="192.168.1.100">
        <Share Name="C" Path="C:\"/>
    </Machine>

    <Machine Name="station2" IP="192.168.1.101">
        <Share Name="E" Path="E:\"/>
    </Machine>

    <!-- the operator's machine, so that the parts can be delivered to it -->
    <Machine Name="operator1" IP="192.168.1.50">
        <Share Name="C" Path="C:\"/>
    </Machine>

    <!-- App definitions with log files -->
    <App AppIdTuple="station1.webapp" ExeFullPath="C:\Apps\webapp.exe">
        <File Id="webapp_log" Path="C:\Logs\webapp.log"/>
        <File Id="webapp_error_log" Path="C:\Logs\webapp_errors.log"/>
    </App>

    <App AppIdTuple="station1.database" ExeFullPath="C:\Apps\db.exe">
        <File Id="db_log" Path="C:\Logs\database.log"/>
    </App>

    <App AppIdTuple="station1.other" ExeFullPath="C:\Apps\other.exe">
        <!-- This app's logs are NOT collected by the package below -->
        <File Id="other_log" Path="C:\Logs\other.log"/>
    </App>

    <App AppIdTuple="station2.webapp" ExeFullPath="E:\Apps\webapp.exe">
        <File Id="webapp_log" Path="E:\Applications\Logs\webapp.log"/>
        <File Id="webapp_error_log" Path="E:\Applications\Logs\webapp_errors.log"/>
    </App>

    <App AppIdTuple="station2.database" ExeFullPath="E:\Apps\db.exe">
        <File Id="db_log" Path="E:\Applications\Logs\database.log"/>
    </App>

    <!-- FilePackage collecting only the webapp and database logs -->
    <FilePackage Id="selected_app_logs" Title="Logs/Webapp + database">
        <FileRef Id="webapp_log"       MachineId="*" AppId="webapp"/>
        <FileRef Id="webapp_error_log" MachineId="*" AppId="webapp"/>
        <FileRef Id="db_log"           MachineId="*" AppId="database"/>
    </FilePackage>

    <MainMenu>
        <FileRef Title="File/Logs/Webapp + database" Id="selected_app_logs"/>
    </MainMenu>
</Shared>
```

`AppId="webapp"` is what excludes `station1.other` - matching on the app id is usually shorter
than listing the machines one by one.

### Example 2: Collecting All Logs from Specific Stations

**Scenario**: Collect all log files from `station1` and `station2`, regardless of which app
created them.

```xml
<Shared>
    <Machine Name="station1" IP="192.168.1.100">
        <Share Name="C" Path="C:\"/>

        <!-- Collect all logs from this machine -->
        <Folder Id="all_machine_logs" Path="C:\Logs" Mask="*.log">
            <Tool Title="Open in Explorer" Name="WinExplorer"/>
        </Folder>

        <!-- Or collect newest logs only -->
        <File Id="newest_logs" Path="C:\Logs" Mask="*.log" Filter="Newest" MaxFiles="20">
            <Script Title="Download zipped" Name="BuiltIns/DownloadZipped.cs"/>
        </File>
    </Machine>

    <Machine Name="station2" IP="192.168.1.101">
        <Share Name="E" Path="E:\"/>

        <Folder Id="all_machine_logs" Path="E:\Applications\Logs" Mask="*.log"/>
        <File Id="newest_logs" Path="E:\Applications\Logs" Mask="*.log" Filter="Newest" MaxFiles="20">
            <Script Title="Download zipped" Name="BuiltIns/DownloadZipped.cs"/>
        </File>
    </Machine>

    <!-- Everything -->
    <FilePackage Id="all_station_logs" Title="Logs/All stations (complete)">
        <FileRef Id="all_machine_logs" MachineId="*" AppId="*"/>
    </FilePackage>

    <!-- Or only the newest ones -->
    <FilePackage Id="newest_station_logs" Title="Logs/All stations (newest)">
        <FileRef Id="newest_logs" MachineId="*" AppId="*"/>
    </FilePackage>

    <MainMenu>
        <FileRef Title="File/Logs/All stations (complete)" Id="all_station_logs"/>
        <FileRef Title="File/Logs/All stations (newest)"   Id="newest_station_logs"/>
    </MainMenu>
</Shared>
```

A whole-tree `<Folder>` can grow without bound - add `MaxFiles`, `MaxSeconds` or `MaxTotalBytes`
if the folder is one that keeps filling up.

### Example 3: Collecting Logs with Time-Based Filtering

**Scenario**: Collect only recent log files (from the last hour) from specific apps on multiple
stations.

```xml
<Shared>
    <Machine Name="station1" IP="192.168.1.100">
        <Share Name="C" Path="C:\"/>

        <!-- Recent webapp logs (last hour, max 10 files) -->
        <File Id="recent_webapp_logs" Path="C:\Logs\webapp" Mask="*.log"
              Filter="Newest" MaxFiles="10" MaxSeconds="3600"/>

        <!-- Recent database logs -->
        <File Id="recent_db_logs" Path="C:\Logs\database" Mask="*.log"
              Filter="Newest" MaxFiles="10" MaxSeconds="3600"/>
    </Machine>

    <Machine Name="station2" IP="192.168.1.101">
        <Share Name="E" Path="E:\"/>

        <File Id="recent_webapp_logs" Path="E:\Applications\Logs\webapp" Mask="*.log"
              Filter="Newest" MaxFiles="10" MaxSeconds="3600"/>

        <File Id="recent_db_logs" Path="E:\Applications\Logs\database" Mask="*.log"
              Filter="Newest" MaxFiles="10" MaxSeconds="3600"/>
    </Machine>

    <!-- Collect recent logs from both stations, one folder per station in the ZIP -->
    <FilePackage Id="recent_logs_collection" Title="Logs/Last hour">
        <VFolder Title="station1">
            <FileRef Id="recent_webapp_logs" MachineId="station1" AppId="*"/>
            <FileRef Id="recent_db_logs" MachineId="station1" AppId="*"/>
        </VFolder>

        <VFolder Title="station2">
            <FileRef Id="recent_webapp_logs" MachineId="station2" AppId="*"/>
            <FileRef Id="recent_db_logs" MachineId="station2" AppId="*"/>
        </VFolder>
    </FilePackage>
</Shared>
```

### Example 4: Using Wildcards for Flexible Collection

**Scenario**: Collect all logs matching a naming pattern from multiple stations.

```xml
<Shared>
    <Machine Name="station1" IP="192.168.1.100">
        <Share Name="C" Path="C:\"/>

        <!-- Define logs with consistent naming pattern -->
        <File Id="app1_log" Path="C:\Logs\app1.log"/>
        <File Id="app2_log" Path="C:\Logs\app2.log"/>
        <File Id="app3_log" Path="C:\Logs\app3.log"/>
    </Machine>

    <Machine Name="station2" IP="192.168.1.101">
        <Share Name="E" Path="E:\"/>

        <File Id="app1_log" Path="E:\Applications\Logs\app1.log"/>
        <File Id="app2_log" Path="E:\Applications\Logs\app2.log"/>
    </Machine>

    <!-- Collect all app logs using a wildcard pattern -->
    <FilePackage Id="all_app_logs" Title="Logs/All apps">
        <FileRef Id="app*_log" MachineId="*" AppId="*"/>
    </FilePackage>

    <!-- Or collect from one station only -->
    <FilePackage Id="station1_app_logs" Title="Logs/Station 1 apps">
        <FileRef Id="app*_log" MachineId="station1" AppId="*"/>
    </FilePackage>
</Shared>
```

## Tips and Best Practices

1. **Consistent Naming**: Use consistent ID naming patterns (`{appname}_log`, or `log.vision.*`
   for a subsystem) so that a single wildcard `<FileRef>` collects the whole group.

2. **Declare Once**: Put the per-app log declaration into an `<AppTemplate>` rather than into each
   `<App>`, and into the app itself rather than into every `<Plan>` the app appears in.

3. **Bound the Result**: Use `Filter="Newest"` with `MaxFiles` / `MaxSeconds`, or a `<Folder>` with
   `MaxFiles` / `MaxSeconds` / `MaxTotalBytes`, so that a full log folder cannot turn into a
   gigabyte-sized download. Note `MaxTotalBytes` works on `<Folder>` only - on a `Filter="Newest"`
   node it is ignored without a word, so bound that one with `MaxFiles`.

4. **Expect the Unrotated Log**: One file that grew to gigabytes is the usual reason a collection
   becomes untransferable. `TailBytes` keeps it collectable; see
   [Logs Too Big to Download](#logs-too-big-to-download).

5. **Organize with VFolders**: Their titles become the folder names inside the ZIP, so shape the
   archive by station, app or log type while you are declaring it.

6. **Prefer Variables to Relative Paths**: `%APP_STARTUPDIR%`, `%APP_BINDIR%`, `%MACHINE_ID%`,
   `%DOWNLOADS%` and the app's own environment variables are all expanded on the machine owning
   the node. A *relative* path is **not** taken against the app's folder - it is resolved against
   the root for relative paths (by default the folder holding `SharedConfig.xml`).

7. **Test the Shares**: Before relying on a path, open `\\{IP}\{ShareName}\{path}` in Explorer from
   the operator's machine. Most collection failures are share failures.

8. **Combine Approaches**: App-scoped, machine-scoped and global definitions all land in one
   registry; a single package can reference all three.

## Troubleshooting

### The Package Is Nowhere in the GUI

- A `<FilePackage>` at the top level of `<Shared>` appears in no menu on its own - reference it
  from `<MainMenu>`, or declare it there directly
- Nodes declared under a `<Machine>` appear in that machine's context menu, nodes under an `<App>`
  in that app's

### Empty or Incomplete Package

- Check that the `FileRef` ids match the ids of actually declared nodes (wildcards allowed)
- Check `MachineId` / `AppId`: **omitting** them means *inherited from the declaration site*, not
  *any*. Write `MachineId="*" AppId="*"` to broaden the search
- Only nodes declared **directly** under `<Shared>`, `<Machine>`, `<App>` or `<AppTemplate>` can be
  found by a `<FileRef>`; a node nested inside another container cannot
- An unmatched reference, and a `Filter="Newest"` folder holding nothing, resolve to nothing
  silently - no error is reported
- The order of declarations does not matter; the whole config is read before anything is resolved

### The Archive Contains an `_incomplete.txt`

That file is the collection telling you what it could not include in full, and it names each case:

- a file over the `MaxTotalBytes` budget of its node - it was passed over, and the files behind it
  were still collected. Raise the budget, or set `TailBytes` so a big file costs only its tail
- a file truncated by `TailBytes` - expected; the entry named `*.last<size>*` holds its end

### Files Not Found

- Verify that the paths exist on the target machines, and that the variables expand to what you
  expect - they are expanded on the machine owning the node, using that machine's agent environment
- `Machine <id> not connected.` means the agent owning the node is down - Dirigent cannot resolve
  the node without it, and *Download zipped* skips such machines

### UNC Path Issues

- `Can't construct UNC path, No file share matching ...` means no `<Share>` of that machine covers
  the path - note the share must cover it up to a folder boundary, so `C:\Logs` does not cover
  `C:\LogsBackup`
- Verify the file shares are accessible without additional credentials
- Check the Windows file sharing permissions
- Ensure the share `Path` is absolute and is a prefix of your file paths

### Download Produces Errors per Machine

- The machine the download goes to needs a `<Share>` covering its download folder; without it, the
  other machines cannot deliver their part
- The download goes to the machine of the *requestor*; a GUI on a machine with no agent falls back
  to the master's machine
- Errors are collected per machine and shown at the end - a missing file, or even a whole machine
  failing, does not abort the rest of the download

## See Also

- [Files.md](Files.md) - concepts and full reference for files, folders and packages
- [Scripts.md](Scripts.md) - `DownloadZipped` and the other built-in scripts, including their use
  from the CLI and REST
- [Actions.md](Actions.md) - how `<Tool>` and `<Script>` menu items work
- [SharedConfig.md](SharedConfig.md) - shared configuration reference
- [Apps.md](Apps.md) - application configuration
