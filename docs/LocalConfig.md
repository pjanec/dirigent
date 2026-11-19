# Local Config

Agent's local config file contains configuration that is specific for an agent (for example folder watching settings). Each agent can use its own local configuration.

Local configuration is stored in the `LocalConfig.xlm` file. The location of the file can be set through application option `localConfigFile`.

Local configuration file is optional.

### When the file is read

**Once, by the agent, when that agent starts.** There is no command that re-reads it:
[`ReloadSharedConfig`](CLI.md#ReloadSharedConfig) re-reads `SharedConfig.xml` only, whatever its name
suggests. So a change here - a new `<Tool>`, a `<FolderWatcher>`, an entry in
`DefaultFileActions` or `DefaultFilePackageActions` - takes effect only after **the agent on that
machine** is restarted, and only on that machine.

Worth planning for on a site of any size: the file is per machine, so a tool added to every machine
means restarting every agent. A menu action that stubbornly fails to appear after a config reload is
almost always this.

### Basic structure

The file defines

* Actions to take if a folder content changes (file added etc.)
* Tool apps available on a local machine
* Default menu actions added to various context menus

```xml
<Local>
    <FolderWatcher ... />

    <Tool ... />
    <Tool ... />    

    <DefaultMachineActions ... />
    <DefaultAppActions ... />
    <DefaultFileActions ... />
    <DefaultFilePackageActions ... />
    
</Local>
```

### Folder Watching

Allows to trigger action(s) when the folder content changes

##### `<FolderWatcher/>` element

Example:

```xml
  <!-- watching for new crash dump !-->
  <FolderWatcher
      Path = "CrashDumps"
      IncludeSubdirs="false"
      Conditions="NewFile"
      Filter = "*.dmp"
      >
      <!-- what to do if change detected -->
      <Action Type="StartPlan" PlanName="CollectLogs"/>
      <!--Action Type="LaunchApp" AppIdTuple="DevelStation.LogCollector"/-->
  </FolderWatcher>
```



Attributes:

- `Path` - path to the folder being watched. If not absolute, then relative to the location of SharedConfig.xml.
- `IncludeSubdirs` - true if files in subfolders shall be watched, false if just filed in the `Path`.
- `Conditions` - when to invoke the actions
  - `NewFile` - when a new file gets created in the folder
- `Filter` - file mask to trigger the actions; if empty, any file name works

##### `<FolderWatcher> <Action\>` sub-element

Defines what to do if the trigger condition happens

Attributes

* `Type`
  * `StartPlan` - starts a plan from SharedConfig.xml
  * `LaunchApp` - starts an app from SharedConfig.xml
* `PlanName` - what plan to start; used with Type=StartPlan
* `AppIdTuple` - what app to start; used with Type=LaunchApp

### Tools

Each `<Tool/>` element defines an application that can be started on the local machine as a response to various events like

* Main menu item click
* Context menu item click on items of machines, apps, plans tabs
* [KillAll](CLI.md#KillAll) command issued

Example:

```xml
  <Tool
    AppIdTuple = "BatchScript"
    Icon = "Icons\BatchScript.png"
    ExeFullPath = "[cmd.file]"
    CmdLineArgs = "%FILE_PATH%"
    StartupDir = ""
  />
```

The definition of the tool follows the same format as the [App definitions](SharedConfig.md#App-definitions) in SharedConfig.

`AppIdTuple` is a text name used to reference this tool from SharedConfig.xml. Unlike the apps in SharedConfig, no machine name is used here as all the tools are meant for the local machine only.

`CmdLineArgs` can contain various variable macros, see [Action variables](Actions.md#Action-variables). The variables available depend on the context the tool is started from.

### Default menu actions

Various context menus can be filled with a default items.

These items are added after more item-specific one that can be defined in SharedConfig.xml.

Example

```xml
  <!-- These are added to each machine's context menu -->
  <DefaultMachineActions>
    <Tool Title="Do something with the machine" Name="Notepad++" Args="%MACHINE_ID% %MACHINE_IP%"/>
    <Tool Title="Remote Desktop" Name="RemoteDesktop" Args="/v:%MACHINE_IP%"/>
  </DefaultMachineActions>

  <!-- These are added to each app's context menu -->
  <DefaultAppActions>
    <Tool Title="Do something with the app++" Name="Notepad++" Args="%APP_ID% %APP_PID%"/>
  </DefaultAppActions>


  <!-- These are added to each file's context menu -->
  <DefaultFileActions>
    <Tool Title="Edit in Notepad++" Name="Notepad++" Args="%FILE_PATH%"/>
    <Script Title="Download zipped file" Name="BuiltIns/DownloadZipped.cs" Icon="Icons/Zipped.png" />
  </DefaultFileActions>

  <!-- These are added to each file pakage's context menu -->
  <DefaultFilePackageActions>
    <Script Title="Download zipped package" Name="BuiltIns/DownloadZipped.cs" Icon="Icons/Zipped.png" />
  </DefaultFilePackageActions>
```


### Download archives

The master keeps the collections requested through the [download jobs](HTTP.md#Download-jobs) of the web API
and the CLI in a folder of its own. These are **application options** of the master (command line), like
`--agentStatusFolder`, not elements of this file:

| Option | Default | |
| --- | --- | --- |
| `--archiveFolder` | `<agentStatusFolder>\Archives` (`%LocalAppData%\Dirigent\Archives`) | One subfolder per job, named by the job's guid, holding `job.json` and the archive. |
| `--archiveRetentionHours` | 24 | Finished, failed, cancelled and orphaned jobs older than this are deleted. |
| `--archiveMaxBytes` | 0 (no cap) | When the jobs together are larger, the oldest finished ones are deleted until they fit. |
| `--serveArchives` | 1 | `0`: the web server answers 403 for the archive files; listing, status and the UNC path still work. |

**Put `ArchiveFolder` under a file share** (declared for the master's machine in SharedConfig) when a
package spans several machines: the other machines upload their parts through the UNC path, and a
diagnostic tool reads the finished archive through it (`uncPath` in the job's answer). Without a share
the machines other than the master's cannot deliver and the job's `errors` say so.

Cleanup runs when the master starts, before each new job and hourly. **It touches only what is
Dirigent's own:** subfolders of `ArchiveFolder` whose name is a guid *and* that contain a `job.json`.
Anything else in the folder is left alone, and so is a job that is running now. A folder that
cannot be deleted (a file in use) is skipped and tried again on the next pass.
