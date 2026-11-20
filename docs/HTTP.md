# Web API

*This page is served by every master at `GET /api/help`.*

The master runs a small web server that makes Dirigent's state and its file features available over
HTTP. It serves three things:

* the **REST API** under `/api` - this document;
* a **WebSocket** endpoint at `/websock`, and
* a **static web page** at `/` (a small web GUI, see [Static files](#Static-files)).

The port is **8877** by default and is set with the master's `--httpPort`. Two things about that option are
worth knowing:

* **`--httpPort 0` does not switch the web server off.** Zero means "not specified", so the
  default 8877 applies. Pass **`-1`** to run without a web server - which also matters when two
  Dirigent installations share a machine, as they would otherwise fight over the port.
* No URL reservation or elevation is needed. The server listens on its own socket rather than
  through the Windows HTTP stack.

> **There is no authentication and no encryption.** Anyone who can reach the port can read everything below
> and, through [`POST /api/cli`](#POST-apicli), control Dirigent: start and kill applications, run scripts,
> shut machines down. Run it on a trusted LAN only, never expose it to an untrusted network, and put it behind
> a firewall rule or switch it off (`--httpPort -1`) where the API is not needed.

## Conventions

**Format.** Requests and answers are JSON (`application/json`, UTF-8 without a byte order mark), field names `lowerCamel`. The only exception is
the archive download, which is `application/zip`, and the static page. Strings are double-quoted JSON; the
examples below are real answers, shortened where they are long.

**Ids.**

| Id | Form | Example |
| --- | --- | --- |
| Application | `machineId.appId`; `machineId.appId@plan` addresses the application as defined in that plan | `m1.camera`, `m1.camera@plan1` - see [Identifying an application](Apps.md#identifying-an-application) |
| Plan | the plan name | `plan1` |
| Script | a guid | `2d5b3159-83c6-48d4-9c52-0ce1af92cbb2` |
| Client (machine) | the client id, usually the machine id | `m1` |
| File definition | the `Id` of a top-level `<File>`, `<Folder>`, `<VFolder>`, `<FilePackage>` or `<FileRef>` of the shared config; filters `machine` and `app` narrow it. The id and both filters take wildcards: `*` and `?`, empty means anything - see [Files](Files.md#Association-app-machine-or-global) | `logs.all`, `log?machine=m1&app=camera` |
| Download job | a guid, handed out by `POST /api/downloads` | |

**Errors.** Every failed `/api` call answers with a JSON body and the matching HTTP status:

```json
{"error":"No download 7c0d6d0c-0b4a-4a6e-8d6e-0d3a2b3c4d5e.","code":404}
```

| Status | Means |
| --- | --- |
| 400 | The request is malformed: an id that cannot be parsed (`m1.`, a script or download id that is no guid), a filter value that does not exist (`?type=Nonsense`), an unreadable body, a file that is not a shared config. |
| 403 | Serving archives is switched off (`--serveArchives 0`), or the caller's address is banned for 10 minutes for sending too many requests (see [Limits](#Limits)). |
| 404 | The application, plan, script, client, machine, file definition or download does not exist - or, for a file definition, nothing matches the filters. The `error` names what is missing. |
| 405 | The route exists, but not for this method (a `GET` on a `POST` route). |
| 409 | The master declined: the operation is impossible as things stand - a download job still running, a machine that is not connected, an application that failed to start. |
| 500 | An unexpected failure; the message is in `error`. |

Everything under `/api` answers errors in this shape - including the ban and a path no route takes.

**How long a call takes.** Three kinds:

* **State and definitions** (`appdefs`, `appstates`, `plandefs`, `planstates`, `scriptdefs`, `scriptstates`,
  `clients`, `filedefs`, `filedefs/{id}`, `downloads` listing) are answered at once from what the master already
  knows. Nothing is looked up on any machine.
* **File previews** (`…/files`) resolve the file definitions on the agents that own the files, which means a
  look at the file systems of those machines. That takes anything from milliseconds to many seconds, and a machine
  that is off or slow only affects its own entries - see [Files, “When something is not there”](Files.md#When-something-is-not-there).
* **Download jobs** (`POST /api/downloads`) start a collection that runs for as long as it takes. The call returns
  at once with a job id; you poll the job, optionally with `?wait=N` (seconds to hold the answer, at most 120).

**Mutating calls.** The calls that change anything are `POST /api/cli` (anything the [CLI](CLI.md) can do),
`POST /api/downloads`, `DELETE /api/downloads/{id}`, the marking calls (`POST /api/marks` and the `.../mark|clear|unmark` routes), and the [control calls](#Controlling-the-system) (plans, applications, machines' power, Kill All, the shared config, variables, scripts).
`POST /api/files/query` is a `POST` only because it takes a body: it is a **read-only** preview and changes nothing. Everything else is `GET`.

## Describing the site

For a tool - or an AI agent - that has to find out what the site is before it does anything, this is the order of questions. Each answer says what to ask next.
Knowing only the address is enough: `GET /` leads to [`GET /api`](#GET-api), which says all of this in JSON.

| Ask | What it tells | Then |
| --- | --- | --- |
| [`GET /api`](#GET-api) | what answers here: version, master, shared config, where this manual is, the order of questions below, the limits, and **every route with a link to its section** | |
| [`GET /api/status`](#GET-apistatus) | which Dirigent this is, which shared config it runs, how many machines, applications, plans, scripts and file definitions it defines, who is connected, whether a Kill All is going | |
| [`GET /api/machinedefs`](#GET-apimachinedefs) | every machine: its address, whether it is online, its shares, its MAC addresses, which applications and file definitions belong to it. **The machines are the key to everything else** - applications, files and power all hang on a machine id | [`/api/machinedefs/{id}`](#GET-apimachinedefsid) for one |
| [`GET /api/appdefs`](#GET-apiappdefs), [`/api/applocations`](#GET-apiapplocations) | the applications, and where their binaries and working folders are | [`/api/appstates`](#GET-apiappstates) for what they are doing now |
| [`GET /api/plandefs`](#GET-apiplandefs) | the launch plans and the applications in them | [`/api/planstates`](#GET-apiplanstates) |
| [`GET /api/filedefs`](#GET-apifiledefs) | what files there is to look at, collect or mark; `?type=FilePackage` for what to offer - a package's `component` says which component it stands for | [`POST /api/files/query`](#POST-apifilesquery) to see the files |
| [`GET /api/scriptdefs`](#GET-apiscriptdefs) | the scripts the configuration defines | [`POST /api/scriptdefs/{id}/start`](#POST-apiscriptdefsidstart) to run one |

Then act. The control calls are in [Controlling the system](#Controlling-the-system); how plans, applications and scripts behave is in [Plans](Plans.md), [Apps](Apps.md) and [Scripts](Scripts.md),
and power and Wake-on-LAN in [Management](Mgmt.md).

### GET /api

The front door - what a tool or an agent that knows only the address asks first (`GET /` redirects here). `/api/` answers the same.

```json
{ "name": "Dirigent", "version": "Dirigent.Agent 3.1.19.24", "masterMachineId": "master1", "sharedConfig": "C:\\Dirigent\\SharedConfig.xml",
  "help": "/api/help/HTTP.md",
  "docs": [ "/api/help/CLI.md", "/api/help/Files.md", "/api/help/HTTP.md", "..." ],
  "start": [ "GET /api/status - which Dirigent this is, what it runs, who is connected", "..." ],
  "limits": "More than 50 requests in a second ban the address for 10 minutes - ...",
  "routes": [ { "method": "GET", "path": "/api/status", "help": "/api/help/HTTP.md#GET-apistatus" }, ... ] }
```

`routes` is read from the master's own code, so it is exactly what this version answers; each `help` links to the route's section of
this manual (the manual of the same version - [`/api/help`](#GET-apihelp)). `start` is the order of questions of
[Describing the site](#Describing-the-site).

### GET /api/status

What this master is and what it runs:

```json
{ "version": "Dirigent.Agent 3.1.19.18", "siteName": "Lab 2", "masterMachineId": "master1", "masterIp": "10.0.0.5", "httpPort": 8877,
  "startedAt": "2026-11-19T08:00:00Z",
  "sharedConfig": { "path": "C:\\Dirigent\\SharedConfig.xml", "loadedAt": "2026-11-19T08:00:01Z", "minDirigentVersion": "3.1.19",
                    "machines": 12, "apps": 40, "plans": 6, "scripts": 3, "fileDefs": 85 },
  "localConfigPath": null, "archiveFolder": "C:\\ProgramData\\Dirigent\\Archives", "killAllInProgress": false,
  "connectedClients": [ "master1", "m1", "m2" ] }
```

`version` is what `--version` prints. `siteName` is the shared config's [`<Shared Name="...">`](SharedConfig.md#Name), null when it gives none. `minDirigentVersion` and `localConfigPath` are `null` when there is none. `connectedClients` are the agents and GUIs connected now.

### GET /api/machinedefs

The machines of the shared config, each as described below.

### GET /api/machinedefs/{id}

```json
{ "id": "m1", "ip": "10.0.0.11", "connected": true, "actualIp": "10.0.0.11", "lastChange": "2026-11-19T08:00:05Z",
  "mac": null, "macLearned": [ "AA-BB-CC-DD-EE-FF" ],
  "shares": [ { "name": "C", "path": "C:\\" } ],
  "fileDefs": [ "dirigent.log" ], "apps": [ "m1.camera", "m1.tracker" ] }
```

`ip` and `mac` are what SharedConfig gives (`null` when it gives none); `actualIp` is the address the agent connected from (`null` if it never has) and `macLearned` the adapters it
reported - what a wake-up uses unless `mac` is given. `connected` and `lastChange` say whether the agent is online now and since when. `shares` are the machine's `<Share>`s, which
make the `uncPath`s of the file previews possible. `fileDefs` are the ids of the file definitions that belong to the machine itself (a machine log), `apps` the applications defined for it.
Unknown machine: 404.

### POST /api/machinedefs/{id}/shutdown

Tells the machine's agent to power the computer off. `202 {"message"}`. Unknown machine: 404; a machine that is not connected: 409 (there is nobody to tell). (CLI: `Shutdown machineId=m1`.)

### POST /api/machinedefs/{id}/reboot

As above, restarting the computer. (CLI: `Shutdown machineId=m1 mode=Reboot`.)

### POST /api/machinedefs/{id}/wakeup

Wakes a switched-off machine with Wake-on-LAN, sent from the master. `202`: the packets were sent - the machine takes a while to come up, watch `connected`. A machine that is already connected: 409 `already online`;
no MAC known (it never connected and SharedConfig gives none): 409 with the reason. (CLI: [`WakeUp`](CLI.md#WakeUp); see [Management](Mgmt.md).)

## Overview

| Method | Route | Purpose | CLI twin |
| --- | --- | --- | --- |
| GET | [`/api`](#GET-api) | What answers here: version, the manual, where to begin, every route | |
| GET | [`/api/status`](#GET-apistatus) | What this master is and runs | |
| GET | [`/api/machinedefs`](#GET-apimachinedefs), [`/api/machinedefs/{id}`](#GET-apimachinedefsid) | The machines: address, online, shares, MACs, their applications and file definitions | [`GetAllClientsState`](CLI.md#GetAllClientsState) |
| POST | [`/api/machinedefs/{id}/shutdown`](#POST-apimachinedefsidshutdown), [`/reboot`](#POST-apimachinedefsidreboot), [`/wakeup`](#POST-apimachinedefsidwakeup) | Power a machine off, restart it, or wake it | [`Shutdown`](CLI.md#Shutdown), [`WakeUp`](CLI.md#WakeUp) |
| POST | [`/api/plandefs/{id}/start`](#POST-apiplandefsidstart), [`/stop`](#POST-apiplandefsidstop), [`/kill`](#POST-apiplandefsidkill), [`/restart`](#POST-apiplandefsidrestart) | Control a plan (`?waitSeconds=`) | [`StartPlan`](CLI.md#StartPlan) ... |
| POST | [`/api/appdefs/{id}/start`](#POST-apiappdefsidstart), [`/kill`](#POST-apiappdefsidkill), [`/restart`](#POST-apiappdefsidrestart) | Control an application (`?waitSeconds=`) | [`StartApp`](CLI.md#StartApp) ... |
| POST | [`/api/killall`](#POST-apikillall) | Kill everything, stop all plans | [`KillAll`](CLI.md#KillAll) |
| POST | [`/api/sharedconfig/reload`](#POST-apisharedconfigreload) | Read the shared config again | [`ReloadSharedConfig`](CLI.md#ReloadSharedConfig) |
| POST | [`/api/vars`](#POST-apivars) | Set variables on the agents | [`SetVars`](CLI.md#SetVars) |
| POST | [`/api/scriptdefs/{id}/start`](#POST-apiscriptdefsidstart) (or [`/api/scripts/{id}/start`](#POST-apiscriptsidstart)), [`/api/scripts`](#POST-apiscripts), [`/api/scripts/{id}/kill`](#POST-apiscriptsidkill) | Start a defined script, start any script by name, stop a script | [`StartScript`](CLI.md#StartScript), [`KillScript`](CLI.md#KillScript) |
| GET | [`/api/appdefs`](#GET-apiappdefs) | All application definitions | |
| GET | [`/api/appdefs/{id}`](#GET-apiappdefsid) | One application definition | |
| GET | [`/api/appdefs/{id}/location`](#GET-apiappdefsidlocation) | Where the binary and working folder of an application are, as its agent resolves them | [`GetAppLocation`](CLI.md#GetAppLocation) |
| GET | [`/api/applocations`](#GET-apiapplocations) | The same for all applications (`?machine=`) | [`GetAppLocations`](CLI.md#GetAppLocations) |
| GET | [`/api/appversions`](#GET-apiappversions) | The versions of the applications, each listed once (`?machine=`) | [`GetAppVersions`](CLI.md#GetAppVersions) |
| GET | [`/api/help`](#GET-apihelp), [`/api/help/{doc}`](#GET-apihelpdoc) | This documentation, from the running master | |
| GET | [`/api/appdefs/{id}/files`](#GET-apiappdefsidfiles) | The actual files of an application instance | [`GetAppFiles`](CLI.md#GetAppFiles) |
| GET | [`/api/appstates`](#GET-apiappstates) | State of all applications | [`GetAllAppsState`](CLI.md#GetAllAppsState) |
| GET | [`/api/appstates/{id}`](#GET-apiappstatesid) | State of one application (also `/api/appstate/{id}`) | [`GetAppState`](CLI.md#GetAppState) |
| GET | [`/api/plandefs`](#GET-apiplandefs) | All plan definitions | |
| GET | [`/api/plandefs/{id}`](#GET-apiplandefsid) | One plan definition | |
| GET | [`/api/planstates`](#GET-apiplanstates) | State of all plans | [`GetAllPlansState`](CLI.md#GetAllPlansState) |
| GET | [`/api/planstates/{id}`](#GET-apiplanstatesid) | State of one plan (also `/api/planstate/{id}`) | [`GetPlanState`](CLI.md#GetPlanState) |
| GET | [`/api/scriptdefs`](#GET-apiscriptdefs) | All script definitions | |
| GET | [`/api/scriptdefs/{id}`](#GET-apiscriptdefsid) | One script definition | |
| GET | [`/api/scriptstates`](#GET-apiscriptstates) | State of all known scripts | |
| GET | [`/api/scriptstates/{id}`](#GET-apiscriptstatesid) | State of one script (also `/api/scriptstate/{id}`) | [`GetScriptState`](CLI.md#GetScriptState) |
| GET | [`/api/clients`](#GET-apiclients) | All clients (machines) | [`GetAllClientsState`](CLI.md#GetAllClientsState) |
| GET | [`/api/clients/{id}`](#GET-apiclientsid) | One client | [`GetClientState`](CLI.md#GetClientState) |
| GET | [`/api/filedefs`](#GET-apifiledefs) | List the file definitions (`?type=&group=&id=&machine=&app=`) | [`GetFileDefs`](CLI.md#GetFileDefs) |
| POST | [`/api/files/query`](#POST-apifilesquery) | Preview the files of a [selection](#The-selection) - read-only | |
| POST | [`/api/marks`](#POST-apimarks) | Mark, clear or unmark a [selection](#The-selection) in one operation | [`MarkFiles`](CLI.md#MarkFiles) a b c ... |
| GET | [`/api/filedefs/{id}`](#GET-apifiledefsid) | The file definitions with an id | |
| GET | [`/api/filedefs/{id}/files`](#GET-apifiledefsidfilesmachineapp) | The actual files of file definitions | [`GetFiles`](CLI.md#GetFiles) |
| POST | [`/api/filedefs/{id}/mark`](#POST-apifiledefsidmark), [`/clear`](#POST-apifiledefsidclear), [`/unmark`](#POST-apifiledefsidunmark) | Draw a line under the clearable files of file definitions, empty them, or take the line away | [`MarkFiles`](CLI.md#MarkFiles), [`ClearFiles`](CLI.md#ClearFiles), [`UnmarkFiles`](CLI.md#UnmarkFiles) |
| POST | [`/api/appdefs/{id}/mark`](#POST-apiappdefsidmark), [`/clear`](#POST-apiappdefsidclear), [`/unmark`](#POST-apiappdefsidunmark) | The same for the files of one application | [`MarkAppFiles`](CLI.md#MarkAppFiles), [`ClearAppFiles`](CLI.md#ClearAppFiles), [`UnmarkAppFiles`](CLI.md#UnmarkAppFiles) |
| POST | [`/api/downloads`](#POST-apidownloads) | Start a collection into a zip | [`DownloadPackage`](CLI.md#DownloadPackage), [`DownloadApp`](CLI.md#DownloadApp) |
| GET | [`/api/downloads`](#GET-apidownloads) | List the download jobs | |
| GET | [`/api/downloads/{id}`](#GET-apidownloadsidwaitn) | State of a job (`?wait=N`) | [`GetDownload`](CLI.md#GetDownload) |
| GET | [`/api/downloads/{id}/archives/{name}`](#GET-apidownloadsidarchivesname) | Stream a finished archive | |
| DELETE | [`/api/downloads/{id}`](#DELETE-apidownloadsid) | Stop and remove a job | [`DeleteDownload`](CLI.md#DeleteDownload) |
| POST | [`/api/cli`](#POST-apicli) | Run any CLI command | all of [CLI](CLI.md) |
| WS | [`/websock`](#WebSocket) | WebSocket endpoint | |
| GET | [`/`](#Static-files) | Static web page | |

## Applications

An application has a **definition** (what it is, from the shared config) and a **state** (what it is doing).
Definitions are read from the shared config; states are reported by the agents. A state can outlive its
definition - see [An application Dirigent knows about but the config does not define](Apps.md#An-application-Dirigent-knows-about-but-the-config-does-not-define).

### GET /api/appdefs

All application definitions: the standalone applications and the ones in the default definition, not the plans' copies.

```json
[{"id": "m1.camera","groups": "","component": "Camera","machine": "m1","exeFullPath": "D:/apps/camera/camera.exe","startupDir": "D:/apps/camera","cmdLineArgs": "--id %APP_ID%"},
 {"id": "m2.recorder","groups": "","component": null,"machine": "m2","exeFullPath": "[cmd.command]","startupDir": "","cmdLineArgs": "echo hi"}]
```

`groups` is the `Groups` attribute of the application, `;`-separated - grouping for people. `component` is its [`Component`](SharedConfig.md#App-element), the software component it belongs to; null if it names none. `exeFullPath`, `startupDir` and `cmdLineArgs` are
**exactly as the config declares them**: variables like `%APP_ID%` are not expanded and a relative path stays relative. What they
become on the machine, and whether the file is there, is answered by [`/api/appdefs/{id}/location`](#GET-apiappdefsidlocation).
The meaning of `ExeFullPath`, `StartupDir` and the reserved names like `[cmd]` is in [the `<App/>` element](SharedConfig.md#App-element).

### GET /api/appdefs/{id}

One definition, as above: `{"id": "m1.camera","groups": "","component": "Camera","machine": "m1","exeFullPath": "D:/apps/camera/camera.exe","startupDir": "D:/apps/camera","cmdLineArgs": "--id %APP_ID%"}`.
A `@plan` suffix is accepted and ignored here. Unknown application: 404.

### GET /api/appdefs/{id}/location

Where the application's binary and working folder are **on its machine, as the agent of that machine resolves them** - the answer to
"where is this program, really". `{id}` is `m1.a` or `m1.a@plan1`; the definition is picked as for the
[file previews](#GET-apiappdefsidfiles): the requested plan's, else the plan the application last ran in, else the default one
(`planSource` says which).

The master asks the agent that owns the application - one call per machine - and the agent answers using **the very code the launcher
starts the process from**: the same variables (environment, the app's own variables, `%APP_ID%`, `%MACHINE_ID%`, `%APP_BINDIR%`,
`%APP_STARTUPDIR%`, the global variables), the same treatment of reserved names and the same rooting of relative paths at the folder of
the shared config. A relative path is therefore reported as the launcher would make it absolute.

```json
{"app": "m1.camera","plan": null,"planSource": "none","machine": "m1","groups": "Camera","component": "Camera","kind": "process",
 "exe": {"declared": "bin/%TOOLS%/camera.exe","localPath": "D:/root/bin/v1/camera.exe",
         "uncPath": "//10.0.0.5/D/root/bin/v1/camera.exe","uncError": null,
         "exists": true,"size": 1048576,"modified": "2026-11-19T09:58:03Z","fileVersion": "2.4.1.0","versionSource": "resource"},
 "startupDir": {"declared": "work/%APP_ID%","localPath": "D:/root/work/camera","uncPath": "//10.0.0.5/D/root/work/camera",
                "uncError": null,"exists": true},
 "cmdLineArgs": {"declared": "--id %APP_ID%","expanded": "--id camera"},
 "error": null}
```

(Paths are shown with `/` here; the real answer carries Windows paths with escaped backslashes, as the other examples do.)

* `kind` is `process` for a plain program, the reserved name without brackets for `[cmd]`, `[cmd.file]`, `[cmd.command]`, `[powershell]`,
  `[powershell.file]`, `[powershell.command]` (their `exe.localPath` is the resolved shell), and `dirigentCommand` for `[dirigent.command]`,
  which runs no program: it has no `exe` or `startupDir` paths, only the expanded `cmdLineArgs`.
* `cmdLineArgs.expanded` is the command line **exactly as the process would get it**, including the prefixes added for the reserved names
  (`/c ` for `[cmd.command]`).
* `exists`, `size`, `modified` (UTC) and `fileVersion` are read on the machine. `fileVersion` is the version resource of the file
  (`versionSource`: `"resource"`). Many executables have none - native C++ builds often do not - and are delivered with a text file instead:
  then the first of `version.txt`, `versioninfo.txt`, `VersionStamp.txt`, any other `VersionStamp*.txt` (by name) found **next to the executable** (case does not matter) is read, and its whole text,
  trimmed, is `fileVersion` - possibly several lines, joined with `
` (escaped in the JSON, so a [CLI](CLI.md#GetAppLocation) answer stays
  on one line); `versionSource` is then the full path of that file. Neither found: both `null`. A program that does not exist has neither.
  `uncPath` is the path through the machine's `<Share>` and `null` with the reason in `uncError` when no share covers it
  ([UNC paths and file shares](Files.md#UNC-paths-and-file-shares)).
* A program named without any folder (`cmd.exe`) is looked for where the launcher looks - under the folder of the shared config - and, only if it
  is not there, in the agent's `PATH` (with the `PATHEXT` extensions). If it is found there `exe.foundOnPath` is `true`: it exists, but the
  launcher itself would not find it that way. If it is found nowhere `exe.localPath` is the name as written and `exists` is `false`.
* A machine that is not connected, or does not answer, gives `error` on its applications only; the `declared` values are still there.

**Limits.** The variables are those the agent knows in advance. Variables given at `StartApp` time (`StartApp m1.a VAR=value`) are not known until
the application is started, so a path that uses one is reported without it. The result is a statement about the files **now**: the file may be replaced
a second later.

Unknown application or plan: 404; a malformed id: 400. This is a call that asks the agent, so it takes a moment; see
[Conventions](#Conventions). (CLI: [`GetAppLocation`](CLI.md#GetAppLocation).)

### GET /api/applocations

The same for every application, or those of one machine with `?machine=m1`: an array of the entries above, ordered by machine and application.
A machine that is neither declared nor named by any application: 404.
Each application is described from the definition [the same way](#GET-apiappdefsidlocation): the plan it last ran in, else the default.
All the machines are asked at once. Each entry carries the application's `groups` and `component`, so that versions can be shown per component.
(CLI: [`GetAppLocations`](CLI.md#GetAppLocations).)

### GET /api/appversions

**The versions on the site, each listed once**: the [locations](#GET-apiapplocations) of all the applications (or of one machine's,
`?machine=m1`), grouped by `component`, version `source` and version text. Many applications share one version - the programs of a
product next to one stamp file, the same build on several machines - so a site of 93 applications has about twenty entries, and a
machine that was not updated shows up as a second entry of the same component.

```json
[ { "component": "Viewer", "source": "D:\\Apps\\Viewer\\VersionStamp.txt",
    "version": [ "Viewer 2.4.0.337", "Built 2026-11-11 11:50", "Commit 4f1c2d9" ],
    "missing": null, "machines": [ "m1", "m2", "m3" ], "apps": [ "m1.viewer", "m2.viewer", "m3.render" ] },
  { "component": "Viewer", "source": null, "version": null, "missing": "the program is not there",
    "machines": [ "m3" ], "apps": [ "m3.tools" ] } ]
```

* `component` is the applications' [`Component`](SharedConfig.md#App-element); applications naming none come last, with `null`.
* `source` and `version` are what `fileVersion` and `versionSource` of the locations say: `"resource"` and the version resource, or the path
  of the version file next to the programs and its text. **The text is not interpreted** - it is only split into lines; two entries differ
  exactly when what is on the disk differs. Comparing and judging versions is the tool's business.
* An application without a version is not left out: it is in an entry with `version: null` and the reason in `missing` - the program is
  not there, it carries no version and has no version file, its machine did not answer, or it is a Dirigent command.

Unknown machine: 404. It takes as long as the locations (about a second on a site of 90 applications). (CLI: [`GetAppVersions`](CLI.md#GetAppVersions).)

### GET /api/appstates

The state of every application that has both a state and a definition, in the order the master keeps them - **all of
them in one call**: a tool showing the applications asks this, not `/api/appstates/{id}` for each row (see [Limits](#Limits)).
(CLI: [`GetAllAppsState`](CLI.md#GetAllAppsState).)

```json
[{"id": "m1.camera@plan1","component": "Camera","state": {"code": "Running","flags": "SRI","plan": "plan1","pid": 7480,
                                    "startedAt": "2026-11-20T13:20:07Z","endedAt": null,"exitCode": null,"restartsRemaining": null,
                                    "cpu": 1.5,"gpu": 0,"memoryMB": 212.4,"reportAgeSeconds": 0.4,"offline": false}},
 {"id": "m1.tracker","state": {"code": "Terminated (3)","flags": "S","plan": null,"pid": null,"exitCode": 3, ...}}]
```

`component` is the application's `Component` (null for none). Besides the text `code` and the `flags`: `plan` the application was last started in (null for none); `pid` while it runs;
`startedAt` - when its current or last process was started - and `endedAt` - when that process ended, null while it runs (both UTC, by the clock of its agent; null if it never ran); `exitCode`
once it was started and has ended on its own (null while it runs, after a kill, or before any start); `restartsRemaining` before its agent
gives up restarting it (-1 = without limit, null = not counted yet); `cpu` and `gpu` in percent and `memoryMB` of the process as its agent
last measured; `reportAgeSeconds` since the agent last reported (null if it never has), and `offline` once that is over 3 s.

**The `id` names the plan the state comes from.** An application can be defined differently in different plans, but an
instance never runs twice at once, so it has one state - the state of its most recently used definition. The `id` is that
definition: `m1.camera@plan1` when the application last ran in (or was last applied from) `plan1`, plain `m1.camera` when it
has only its default definition. The id of `/api/appdefs` has no `@plan`; to put definitions and states side by side, compare
the part before the `@`. A plain `m1.camera` in a request means the same: the application as last started in this run of the master.

`code` is the text the GUI shows - `Not running`, `Planned`, `Initializing`, `Running`, `Dying`,
`Restarting (2 remaining)`, `Killed`, `Terminated (<exit code>)`, `Failed to start` - and carries a suffix like
` (Offline for 12 sec)` when the machine has not reported lately. `flags` is a string of single letters, in this order:
`S` started, `F` start failed, `R` running, `K` killed, `I` initialized, `P` plan applied, `D` dying,
`X` restarting. What each means is in [Application status](Apps.md#Application-status).

### GET /api/appstates/{id}

One state, as above: `{"id": "m1.camera","state": {"code": "Running","flags": "SRI"}}`. (CLI: [`GetAppState`](CLI.md#GetAppState).)
With `m1.camera@plan1` the state is judged against that plan, which is what turns `Not running` into `Planned` while
the plan is still starting its applications - see [Plan status](Plans.md#Plan-status). It is still the one state of the
application, so the answer's `id` names the plan it last ran in, which need not be `plan1`.

The older name [`/api/appstate/{id}`](#GET-apiappstateid) is also accepted. Unknown application or plan, or an application that has no state or no definition: 404;
a malformed id (`m1.`): 400.

`GET /api/appdefs/{id}/files` previews the actual files of one application instance; it belongs with the file calls and is
described under [Files and downloads](#GET-apiappdefsidfiles).

#### GET /api/appstate/{id}

The older name of [`/api/appstates/{id}`](#GET-apiappstatesid); the same answer, still accepted.

## Plans

A plan launches a set of applications in dependency order; see [Plans](Plans.md) and
[Launch plan](SharedConfig.md#Launch-plan) in the shared config.

### GET /api/plandefs

```json
[{"id": "plan1","appDefs": [{"id": "m1.a","groups": ""},{"id": "m2.b","groups": ""}],"groups": "Test1",
  "markFiles": ["pkg.logs"],"clearFiles": [],"markTimeout": 60,"markInterval": 600}]
```

`appDefs` are the plan's own copies of its applications; `groups` is the plan's `Groups` attribute. `markFiles`, `clearFiles` and
`markTimeout` are its `MarkFiles`, `ClearFiles` and `MarkTimeout` - which file definitions it [marks or clears when it starts](Plans.md#marking-or-clearing-files-when-a-plan-starts), and
how many seconds that may take before the applications are started anyway; empty lists for a plan that marks nothing. `markInterval`
is its `MarkInterval`: seconds between the [checkpoints](Plans.md#checkpoints-collecting-since-a-time) it takes of its `markFiles` while it runs, 0 for none.

### GET /api/plandefs/{id}

One plan definition, as above. Unknown plan: 404.

### GET /api/planstates

```json
[{"id": "plan1","state": {"code": "InProgress","flags": "R"}}]
```

`code` is one of `None`, `InProgress`, `Success`, `Failure`, `Killing`; `flags` has `R` while the plan is running and `K`
while it is killing - all explained in [Plan status](Plans.md#Plan-status). (CLI: [`GetAllPlansState`](CLI.md#GetAllPlansState).)

A plan that [marks files when it starts](Plans.md#marking-or-clearing-files-when-a-plan-starts) adds, inside `state`, only while it does or once it has:
`phase` (e.g. `"Marking files (pkg.logs)"`, while the marking runs) and `markResult`
(`{"marked":2,"cleared":0,"skipped":0,"failed":0,"timedOut":false,"cancelled":false,"errors":[]}`); a plan with a `MarkInterval` also
`lastCheckpoint` (UTC), when its last [checkpoint](Plans.md#checkpoints-collecting-since-a-time) finished. Other plans have exactly the fields above.

### GET /api/planstates/{id}

One plan state, as above. (CLI: [`GetPlanState`](CLI.md#GetPlanState).) The older name [`/api/planstate/{id}`](#GET-apiplanstateid) is also accepted.
Unknown plan: 404.

#### GET /api/planstate/{id}

The older name of [`/api/planstates/{id}`](#GET-apiplanstatesid); the same answer, still accepted.

## Scripts

See [Scripts](Scripts.md). Script ids are guids.

### GET /api/scriptdefs

The scripts declared in the shared config (`<Script>`) and the ones added at run time.

```json
[{"id": "2d5b3159-83c6-48d4-9c52-0ce1af92cbb2","groups": "Maintenance"}]
```

### GET /api/scriptdefs/{id}

One script definition, as above. An id that is not a guid: 400. Unknown script: 404.

### GET /api/scriptstates

The scripts the master knows the state of.

```json
[{"id": "2d5b3159-83c6-48d4-9c52-0ce1af92cbb2","state": {"code": "Collecting from 2 machine(s)..."}}]
```

Note that `code` is the script's **status text** - whatever the script says it is doing - not its status. The status
(`Running`, `Finished`, `Failed`, ...) and the progress are described in [Script status](Scripts.md#Script-status); use the CLI's
[`GetScriptState`](CLI.md#GetScriptState) for the full state including data and progress.

### GET /api/scriptstates/{id}

One script state, as above. The older name [`/api/scriptstate/{id}`](#GET-apiscriptstateid) is also accepted. An id that is not a guid: 400;
unknown script: 404. See also [Singleton scripts](Scripts.md#Singleton-scripts).

#### GET /api/scriptstate/{id}

The older name of [`/api/scriptstates/{id}`](#GET-apiscriptstatesid); the same answer, still accepted.

## Clients

A client is anything connected to the master: an agent on a machine or a GUI.

### GET /api/clients

```json
[{"id": "m1","state": {"connected": true,"ip": "10.0.0.5","lastChange": "2026-11-19T12:04:27.1829062Z"}},
 {"id": "m2","state": {"connected": false,"ip": "10.0.0.6","lastChange": "2026-11-19T11:58:02.5010000Z"}}]
```

`lastChange` (UTC, ISO 8601) is when `connected` last changed. `ip` is the address the connection came from, last known.
(CLI: [`GetAllClientsState`](CLI.md#GetAllClientsState).)

### GET /api/clients/{id}

One client, as above. (CLI: [`GetClientState`](CLI.md#GetClientState).) Unknown client: 404.

## Files and downloads

How files, folders and packages are declared, resolved and limited is explained in [Files](Files.md): the
[node types](Files.md#File-definition-types), [how they belong to an app, machine or nothing](Files.md#Association-app-machine-or-global),
[how they are resolved](Files.md#Resolution), [`TailBytes`](Files.md#Files-too-big-to-collect-whole) for very big logs, and
[UNC paths and file shares](Files.md#UNC-paths-and-file-shares) - the `uncPath` in the answers below exists only where the
machine's `<Share>` covers the file ([machine definitions](SharedConfig.md#Machine-definitions)). Collecting logs from several
machines is walked through in [Log file collection](LogFileCollection.md). A file the operator has marked for a test run is collected only from the mark on - see [Marking and clearing](MarkAndClear.md).

The flow of a diagnostic tool: **list** the file definitions (`GET /api/filedefs`), **preview** what the user ticked
(`POST /api/files/query`), **collect** it (`POST /api/downloads`), **poll** the job, **fetch** the archive - or read the files in place
through their UNC paths - and **mark** the same selection before the next run (`POST /api/marks`). All three operations take the same
[selection](#The-selection). The single-item routes (`/api/filedefs/{id}/files`, `/api/appdefs/{id}/files`, the single `fileDef` / `app` forms of
`POST /api/downloads`, `/api/filedefs/{id}/mark`) stay as shortcuts for a selection of one.

### The selection

One object says what a tool's checkboxes stand for, and the same object goes to the preview, the download and the marking, so that what is previewed is what is
collected is what is marked:

```json
{ "fileDefs": [ { "id": "pkg.comp.viewer" }, { "id": "log.*", "machine": "m1", "app": "*" } ],
  "apps":     [ "m1.a", "m1.a@plan1" ] }
```

* `fileDefs` - file definitions or packages by `id`; `machine` and `app` are optional per item, and `id`, `machine` and `app` take the wildcards of a
  [`<FileRef>`](Files.md#fileref). A wildcard item may match several definitions. In a JSON body the wildcards need no escaping, unlike in a URL path.
* `apps` - applications, `m1.a` or `m1.a@plan1`, the definition picked as for [`/api/appdefs/{id}/files`](#GET-apiappdefsidfiles).
* **All the items are looked up before anything runs.** An item that matches nothing is a `404` whose message names it; an empty selection - no items, or an item
  without an `id` - is a `400`.
* There is **no deduplication across items**. A file that two items reach is listed under both, counted in both and, in a download, in the folder of each, each copy
  following its own definition (`TailBytes`, marks). A marking, which is per file, simply marks it once.

#### A tool with a checkbox per component

```
GET  /api/filedefs?type=FilePackage                      -> one checkbox per package naming a component: id, title, description, component
POST /api/files/query     {"fileDefs":[{"id":"pkg.comp.viewer"},{"id":"pkg.comp.broker"}]}   -> the files and the sizes, to show
POST /api/downloads       {"fileDefs":[{"id":"pkg.comp.viewer"},{"id":"pkg.comp.broker"}],"comment":"why"}   -> 202 {"id": "..."}
GET  /api/downloads/{id}?wait=60                         -> poll; then GET the archive's url
POST /api/marks           {"operation":"mark","fileDefs":[{"id":"pkg.comp.viewer"},{"id":"pkg.comp.broker"}]}  -> before the next run
```

The `group` is one entry of the `Groups` of the file definition - grouping for people ("Main components"); which component a package stands for is its `component` - see [Components and groups for tools](Files.md#components-and-groups-for-tools).

### POST /api/files/query

Previews the files of a [selection](#The-selection). **Read-only**: nothing is marked, collected or changed. Body: the selection. `?content=false` resolves the top-level definitions without scanning their folders ([what that means for a package](#GET-apiappdefsidfiles)).

```json
{ "items": [
    { "id": "pkg.comp.viewer", "machine": null, "app": null, "fileDefs": [ { "id": "pkg.comp.viewer", "type": "FilePackage", "files": [ ... ], "totals": { "files": 4, "bytes": 90112, "bytesToCollect": 45056 } } ],
      "totals": { "files": 4, "bytes": 90112, "bytesToCollect": 45056 } },
    { "app": "m1.a", "plan": null, "planSource": "none", "fileDefs": [ ... ], "totals": { "files": 2, "bytes": 2048, "bytesToCollect": 2048 } } ],
  "totals": { "files": 6, "bytes": 92160, "bytesToCollect": 47104 } }
```

`items` has one entry per selected item, in the order of the selection, in the shape of the single route's answer: that of
[`/api/filedefs/{id}/files`](#GET-apifiledefsidfilesmachineapp) for a file definition item, that of [`/api/appdefs/{id}/files`](#GET-apiappdefsidfiles) for an application.
Every file carries what the single routes give, and `app` and `fileDefId` (see below). The `totals` of the answer are the sum of the totals of the items.
Unknown item: 404; empty selection: 400.

#### Totals

Every preview answer - the single routes and the query - has `totals` at the top (and on each file definition entry):

| field | meaning |
| --- | --- |
| `files` | the number of files |
| `bytes` | the sum of their `size` (a file of unknown size counts as 0) |
| `bytesToCollect` | what a download would hold: the sum, over the files, of `min( size - (markedAtBytes or 0), truncatedTo or size )`, never below 0 |

The totals of a query are the sums of its items'; a file in two items is in the total twice, which is what the zip holds.

#### Where a file came from

Each previewed file has three more fields: `app`, the application it belongs to (null for a machine-level or global file); `fileDefId`, the `id` of the file
definition it came from - the one a package or an application referred to, carried down through folder scans and package members; and `component` - its application's
[`Component`](SharedConfig.md#App-element), else that of the nearest package or file definition around it that names one, null if none does. A tool can show
*component -> app -> files* and filter by kind (`log.*`, `cfg.*`) without reading the paths.

### GET /api/appdefs/{id}/files

returns the actual files of one application instance, each with the path on its own machine
(`localPath`) and, where a file share covers it, a UNC path (`uncPath`) a diagnostic tool can read.
`m1.a@plan1` addresses the application as defined in that plan.

Which definition answers (the one whose file definitions - `<File>`, `<Folder>`, ... - are listed):

* `m1.a@plan1` - the definition in `plan1` (`planSource` is `requested`). Unknown plan, or an app not in it: 404.
* `m1.a` - the plan the app last ran in, if it has one (`lastRun`); otherwise the default definition (`none`). Unknown app: 404.

`?content=false` asks *where* rather than *what*: each top-level file definition is resolved as itself - its path on its machine,
the UNC path, `error` when the folder is not there or no share covers it - but a `<Folder>` is not scanned, so its `files` stay empty.
That is cheap however big the folder, and is what a tool needs to open a log folder in a file manager, or to check that a definition
resolves before offering it. It does not reach inside a container: a package or a virtual folder is the list of its members, so its
members are always resolved with their files - `content=false` on a package lists the same files as without it.

```json
{ "app": "m1.a", "plan": "plan1", "planSource": "requested",
  "fileDefs": [ { "id": "logs", "title": "Logs", "type": "Folder", "description": null, "machine": "m1",
               "localPath": "D:\\Logs", "uncPath": "\\\\10.0.0.5\\D\\Logs", "uncError": null,
               "error": null, "notes": [],
               "files": [ { "path": "Logs/sub/a.log", "machine": "m1",
                            "localPath": "D:\\Logs\\sub\\a.log", "uncPath": "\\\\10.0.0.5\\D\\Logs\\sub\\a.log",
                            "size": 1048576, "modified": "2026-11-19T09:58:03Z", "truncatedTo": null } ] } ] }
```

Each top-level file definition of the app is resolved on its own, so one that fails (its machine is not
connected, the folder is missing) has `error` set and the others are still answered. If no
share of the machine covers a path, `uncPath` is `null` and `uncError` says why (once per file definition);
`localPath` is still there. Each file also says its `size` in bytes and `modified` (UTC) as found when it was looked up, and
`truncatedTo` when the file definition has `TailBytes` smaller than the file: a download will hold only the last
part, at most that many bytes (otherwise `null`). `notes` carries what the resolution had to leave out (size limits,
members of a package that could not be looked up). A file definition with no machine (global) has only `uncPath`.

The files are listed, not served: the tool reads them through the UNC path. The same answer is given by
`POST /api/cli` with `GetAppFiles m1.a@plan1` (see [CLI](CLI.md#GetAppFiles)).

### GET /api/filedefs/{id}/files?machine=&app=

the same for the top-level file definitions with this id - a file package, usually - addressed the way scripts address
them: `pkg` is the file definition id (wildcards allowed), `machine` and `app` optionally narrow it, `content`
is as above. Each matching definition is resolved as itself and answers with one entry of its own, typed and
titled as declared (`FilePackage`, `Folder`, ...), with its `description`; a wildcard matching two definitions gives two
entries, and nothing matching is a 404. A package spanning many machines costs one round trip per machine, and a member
that cannot be reached becomes a `note` while the other members' files are listed.

```json
{ "id": "pkg", "machine": null, "app": null,
  "fileDefs": [ { "id": "pkg", "title": "Pkg", "type": "FilePackage", "description": "...", "files": [ ... ] } ] }
```

Each file carries its own `machine`. Also available as `POST /api/cli` with `GetFiles pkg machine=m1`.

### GET /api/filedefs

lists the top-level file definitions (`<File>`, `<Folder>`, `<VFolder>`, `<FilePackage>`, `<FileRef>`) as declared in the shared config - what there is to collect. Nothing is
looked up in any file system. Optional filters: `type` (`File`, `Folder`, `VFolder`, `FilePackage`,
`FileRef`; case-insensitive; anything else is a 400 naming these), `id`, `machine`, `app` (the wildcards of the `ListVfsNodes` script: `*`
and `?`, empty means anything) and `group` - one entry of the `;`-separated `Groups` of the definition, compared case-insensitively, wildcards allowed
(`group=main*` finds `Groups="Main components;Back end"`; see [Components and groups for tools](Files.md#components-and-groups-for-tools)). Example: `GET /api/filedefs?type=FilePackage&id=logs.*`
or `GET /api/filedefs?type=FilePackage&group=Main*`. Each definition also says the `component` it stands for (null if none).

```json
[ { "id": "logs.all", "type": "FilePackage", "title": "Logs/All apps", "description": "...",
    "groups": "", "component": null, "machine": null, "app": null } ]
```

### GET /api/filedefs/{id}

the definitions with this id (wildcards allowed), narrowed by `?machine=` and `?app=`, in the shape of the
list above - the same as the list with an `id` filter, but a 404 when nothing matches. Example: `GET /api/filedefs/logs.all`

### Download jobs

A diagnostic tool collects files in five steps: **list** the file definitions (`/api/filedefs`), **preview** what the user ticked
(`POST /api/files/query`, or for one item `/api/filedefs/{id}/files`, `/api/appdefs/{id}/files`), **collect** (`POST /api/downloads`),
**poll** the job, **fetch** the archive. The archives are built on the master's machine, in a folder
of Dirigent's own (`--archiveFolder`, see [Local Config](LocalConfig.md#Download-archives)), one
subfolder per job, and kept for `--archiveRetentionHours` (24 by default).

#### POST /api/downloads

starts a collection. Body: `{"fileDef":{"id":"logs.all","machine":"m1","app":"a"}}` (a file definition or package, addressed
like `/api/filedefs/{id}/files`; machine and app optional) or `{"app":"m1.a@plan1"}` (all the file definitions of the
application, from the definition picked as for `/api/appdefs/{id}/files`), or a [selection](#The-selection) of several - `{"fileDefs":[...],"apps":[...]}` -
an optional `"comment"` that goes into the archive's cover note, `"since": "2026-11-20T12:00:00Z"` to collect what was written since that time
instead of since the marks - each clearable file from the latest record of its length at or before it (see
[Collecting since a time](MarkAndClear.md#collecting-since-a-time); without an offset the time is UTC; the previews do not apply it), `"siteState": false` to leave out the archive's
`_sitestate.json` (the versions, machines, plans and applications at the time of the collection - in by default; see
[the archive](Files.md#builtinsdownloadzippedcs---download-as-zip)), and an optional `"name"` for the archive - used as given (`"Bug 4711"` gives `Bug 4711.zip`;
`.zip` may be included; characters a file name cannot hold become `_`; a name already taken gets a number). Without it the name is the title and the time. Answers `202 {"id":"<job guid>"}`; unknown file definition, app or plan
(the message names the item): 404; nothing selected: 400. All the items are looked up before the job starts. Old jobs are cleaned up first.

**One zip, and its layout.** A job makes one archive however many items there are.

* **One item** (a single `fileDef` or `app`, or a selection with exactly one item): the layout is what it has always been - no extra folder.
* **More than one item**: one top folder per item, titled as the item is declared - the `Title` of the package or file definition, the app id for an application -
  made fit to be a folder name; the same title twice becomes `Title` and `Title (2)`. (When the files come from several machines the machine folders come first, as ever.)
* **No deduplication.** A file reached through two items is in both folders, each copy following its own definition (`TailBytes`, marks). The archive's
  `_comment.txt` lists the selection; `job.json` and the job's `request` echo it; what could not be collected is reported under the name of the item.

```json
{"fileDefs":[{"id":"pkg.comp.viewer"},{"id":"pkg.comp.broker"}],"apps":["m1.a"],"comment":"after the crash"}
```

#### GET /api/downloads

lists the jobs on disk, newest first, each as below.

#### GET /api/downloads/{id}?wait=N

```json
{ "id": "...", "status": "Finished", "progress": 1.0, "text": null,
  "created": "2026-11-19T10:00:00Z", "finished": "2026-11-19T10:00:20Z", "machine": "master1",
  "request": { "fileDef": { "id": "logs.all" } },
  "archives": [ { "name": "All apps_261119_1000.zip", "size": 1234567,
               "url": "/api/downloads/<id>/archives/All%20apps_261119_1000.zip",
               "localPath": "C:\\ProgramData\\Dirigent\\Archives\\<id>\\All apps_261119_1000.zip",
               "uncPath": "\\\\10.0.0.5\\C\\ProgramData\\Dirigent\\Archives\\<id>\\All apps_261119_1000.zip",
               "uncError": null } ],
  "errors": [], "notCollected": [] }
```

**The size of the archive is known before it is downloaded**: `archives[].size` (bytes) is there as soon as the job is `Finished` - check it
against an upload limit before fetching anything.

`status` is `Running`, `Finished`, `Failed`, `Cancelled`, or `Orphaned` (the job was running when the master
went away; nothing is left to finish it). While running, `progress` (0..1, or `null` when unknown) and `text`
come from the live script. `?wait=N` holds the answer until the job has ended, at most N seconds
(max 120). After a master restart the job is still answered from its `job.json`. `uncPath` is `null`
with the reason in `uncError` when no file share covers the archive folder. Only the `url` is percent-encoded; `name`,
`localPath` and `uncPath` are plain paths and may contain spaces.

#### GET /api/downloads/{id}/archives/{name}

streams the archive (`application/zip`, `Content-Disposition: attachment` with an ASCII `filename` and the exact `filename*=UTF-8''...`) without loading it into
memory. `name` must be one of the job's `archives`, otherwise 404; a job that is still running: 409;
`--serveArchives 0`: 403 (listing, status and `uncPath` still work, so a tool can read the archive through the share instead).

#### DELETE /api/downloads/{id}

stops the job if it is running and removes its folder (204); unknown job: 404. A running job's folder
goes as soon as its script has stopped.

The same through `POST /api/cli`: `GetFileDefs`, `GetFiles`, `DownloadPackage` (several ids: `DownloadPackage a b c`), `DownloadApp` (`DownloadApp x y`),
`GetDownload`, `DeleteDownload` (see [CLI](CLI.md#DownloadPackage)).

## Marking the files of a test run

Draw a line under the log files before a test run (**mark**), or empty them (**clear**), and take the line away (**unmark**), so that the next download holds the
run and not the whole afternoon - the mechanism, and why a mark rather than a delete, is in [Marking and clearing](MarkAndClear.md). Only definitions that say
`Clearable="1"` are ever touched; the rest are reported as skipped. Each call runs the same scripts the GUI's menu does (`BuiltIns/MarkFiles.cs`,
`ClearFiles.cs`, `UnmarkFiles.cs`), waits for the machines, and answers with what happened to which file:

```json
{ "operation": "mark", "selection": "run.pkg",
  "machines": [ { "machine": "m1",
                  "marked": [ "C:\\apps\\camera\\logs\\app.log" ], "cleared": [], "unmarked": [],
                  "skippedNotClearable": [ "C:\\apps\\camera\\app.cfg" ], "missing": [], "failed": [] } ],
  "errors": [], "message": null }
```

`cleared` are the files emptied, `marked` those a line was drawn under - which is also what a **clear** does to a file somebody holds open - and `failed` are
`{"path","error"}` pairs. `message` explains the one case worth explaining: nothing in the selection is `Clearable`. A selection that matches nothing is a 404.
`?waitSeconds=N` bounds the wait (60 by default, 300 at most); a script that is still running then is `504` with a JSON body naming its script id, which goes on
and can be followed at [`/api/scriptstates/{id}`](#GET-apiscriptstatesid). A script that fails - say, no machine holding the files is online - is `502`
with the reason. A file definition that has a mark is shown in the [previews](#GET-apiappdefsidfiles) as `markedAt` (UTC) and `markedAtBytes`.

### POST /api/marks

One operation over a whole [selection](#The-selection): `{"operation":"mark","fileDefs":[{"id":"pkg.comp.viewer"},{"id":"pkg.comp.broker"}],"apps":["m1.a"]}`.
`operation` is `mark`, `clear` or `unmark`. The operation runs over the **union** of the selected files - a file reached through two items is marked once, because marks
are per file - and answers in the shape above, with `selection` echoing the items. Marking works as ever: only `Clearable="1"` files, one helper per machine. Unknown
item: 404 naming it; empty selection or an unknown `operation`: 400. `?waitSeconds=N` as above. (CLI: `MarkFiles a b c`, `ClearFiles a b`, `UnmarkFiles a b`, `MarkAppFiles x y` - several ids.)

### POST /api/filedefs/{id}/mark

Draws a line under the clearable files of the file definitions matching `{id}` (wildcards allowed: `*` is everything clearable) - destroying nothing, which is
the one for a production site. `?machine=` and `?app=` narrow the selection as for [`/api/filedefs/{id}/files`](#GET-apifiledefsidfilesmachineapp).
(CLI: [`MarkFiles`](CLI.md#MarkFiles).)

### POST /api/filedefs/{id}/clear

Empties what can be emptied and marks the rest - a log somebody is writing to cannot be emptied, so the line is drawn under it, which keeps the promise that
the next download holds only what comes after. (CLI: [`ClearFiles`](CLI.md#ClearFiles).)

### POST /api/filedefs/{id}/unmark

Takes the lines away, so the next download holds the whole files again. Works on files that have since stopped being `Clearable`. (CLI: [`UnmarkFiles`](CLI.md#UnmarkFiles).)

### POST /api/appdefs/{id}/mark

The same for all the file definitions of one application, `m1.a` or `m1.a@plan1` (the definition picked as for the [previews](#GET-apiappdefsidfiles)),
as one selection - the way [`DownloadApp`](CLI.md#DownloadApp) takes them. (CLI: [`MarkAppFiles`](CLI.md#MarkAppFiles).)

### POST /api/appdefs/{id}/clear

As above, clearing. (CLI: [`ClearAppFiles`](CLI.md#ClearAppFiles).)

### POST /api/appdefs/{id}/unmark

As above, unmarking. (CLI: [`UnmarkAppFiles`](CLI.md#UnmarkAppFiles).)

## Controlling the system

JSON in, JSON out, and a status code that says what happened. Each call is a thin wrapper over what the matching [CLI](CLI.md) command calls, so what the master refuses it refuses here, with its own words:

* **404** - the plan, application, machine, script or file definition is not known;
* **400** - the body is not valid JSON, a variable name is not usable, a number of seconds is not a number;
* **409** - the master declined: the operation is impossible in the state things are in (a machine that is not connected, an instance already running, ...). The message is the master's;
* **504** - a `?waitSeconds=` ran out before the state was reached; whatever was started is still going on;
* otherwise the error is the JSON `{"error": "...", "code": 500}` of every `/api` error.

Nothing answers 200 for something that did not happen. Calls that only *ask* the system to do something - the work happens later, on the machines - answer `202`; calls given `?waitSeconds=N` (at most 300) wait for the
state and answer `200` with it, or `504`. Variables travel as `{"vars":{"NAME":"value"}}`, the same as the optional second argument of `StartPlan` and `StartApp` (names without `=` or `::`).

```
POST /api/plandefs/system/start?waitSeconds=120    {"vars":{"SITE":"lab2"}}   -> 200 {"id":"system","state":{"code":"Success","flags":"R"}}
POST /api/appdefs/m1.camera/kill?waitSeconds=30                              -> 200 {"id":"m1.camera","state":{"code":"NotRunning"...}}
POST /api/scripts?waitSeconds=30   {"name":"BuiltIns/ListVfsNodes.cs","args":{}} -> 200 {"id":"...","status":"Finished","data":{...}}
```

### POST /api/plandefs/{id}/start

Starts the plan - the [`StartPlan`](CLI.md#StartPlan) of the CLI. Optional body `{"vars":{...}}`. Answers the plan state ([`/api/planstates/{id}`](#GET-apiplanstatesid)): `202` at once; with `?waitSeconds=N`, `200` when the plan's
status is *Success* or *Failure* (read `state.code` - a *Failure* is the plan's own verdict, see [Plan status](Plans.md#Plan-status)), `504` if it is still *In Progress*. A plan that [marks files](Plans.md#marking-or-clearing-files-when-a-plan-starts) is *In Progress* meanwhile. Unknown plan: 404.

### POST /api/plandefs/{id}/stop

Stops the plan launching further applications; what already runs keeps running. `202` with the plan state.

### POST /api/plandefs/{id}/kill

Kills the plan's applications. `202` with the plan state; with `?waitSeconds=N`, `200` when the plan is no longer killing.

### POST /api/plandefs/{id}/restart

Kills the plan's applications and starts it again. Optional `{"vars":{...}}`; `?waitSeconds=N` waits for the new *Success* or *Failure* as for start.

### POST /api/appdefs/{id}/start

Starts the application, `m1.a` or `m1.a@plan1` (the definition of that plan, as the CLI's `StartApp`). Optional `{"vars":{...}}`. Answers the application state ([`/api/appstates/{id}`](#GET-apiappstatesid)): `202`, or with `?waitSeconds=N`
`200` once it is running and initialized, `504` if not. Unknown application or plan: 404. The machine of the application not connected: `409`
(there is nobody to tell - the same for kill and restart). A start that fails during the wait - the program is not there, say - is `409`
`failed to start` at once rather than a `504` at the end of the wait; an application that had already failed before the call is the exception, as its
old failure cannot be told from a new one, and is waited for as before.

### POST /api/appdefs/{id}/kill

Kills the application. `202`; with `?waitSeconds=N`, `200` once it is no longer running.

### POST /api/appdefs/{id}/restart

Restarts the application, optional `{"vars":{...}}`; `?waitSeconds=N` waits for the new instance to be running and initialized.

### POST /api/killall

Kills every application on every machine and stops every plan - [`KillAll`](CLI.md#KillAll) and its [extra actions](SharedConfig.md#KillAll-definitions). Optional body `{"machine":"m1"}` limits it to one machine. `202`. A plan that is
[marking files](Plans.md#marking-or-clearing-files-when-a-plan-starts) is stopped but its marking is left to finish; a second Kill All while the first is going cuts it. Unknown machine: 404.

### POST /api/sharedconfig/reload

Reads the shared config again - [`ReloadSharedConfig`](CLI.md#ReloadSharedConfig). **Like the CLI, it kills the applications first** - a reload that did not would leave processes started under the old definitions running that the new config no longer knows (orphaned processes nobody manages) - so pass `{"killApps":false}` only when you know the running applications are unaffected; `{"file":"..."}` names another file. The call
returns when the reload is done (a few seconds): `200 {"message","path","loadedAt"}`. A file that is not there or not valid XML: 400, and a config that is not valid (an id that matches nothing, a `MinDirigentVersion` newer than this master) is 400 with the reason; the running configuration is kept.

### POST /api/vars

Sets variables on the agents - [`SetVars`](CLI.md#SetVars). Body `{"vars":{"NAME":"value"}}`, which must not be empty. `202`.

### POST /api/scriptdefs/{id}/start

### POST /api/scripts/{id}/start

**One operation under two names**: `POST /api/scriptdefs/{id}/start` reads like the start of a plan or an application (`/api/plandefs/{id}/start`), `POST /api/scripts/{id}/start` pairs with `POST /api/scripts/{id}/kill`. Same handler, same body, `?waitSeconds=` and errors.


Starts a script the configuration defines - what the GUI does for a defined script. `{id}` is the guid of the `<Script>` definition, which is also the id the script runs under: **start and kill share the resource** (`POST /api/scripts/{id}/start`, `POST /api/scripts/{id}/kill`), and the state is at [`/api/scriptstates/{id}`](#GET-apiscriptstatesid). A script started by name with `POST /api/scripts` has the `id` that call answered with, and is stopped the same way. See [Scripts](Scripts.md). Optional body `{"args": ...}`: JSON, or a string, handed to the script as its arguments; without it the arguments of the definition are used. `202 {"id"}`;
with `?waitSeconds=N` the answer is the script's end state, `{"id","state":{"code"},"status":"Finished","text","progress","data"}`, where `data` is what the script returned (parsed when it is JSON) or, for a failed script, the error. An `{id}` that is no `<Script>` definition: 404 `no script definition {id}; start a script by name with POST /api/scripts`;
a script that is already running: 409.

### POST /api/scripts

Starts any script by name - [`StartScript`](CLI.md#StartScript) of the CLI. Body `{"name":"BuiltIns/ListVfsNodes.cs","args":{...},"machine":null,"id":"<guid>","title":"..."}`: `name` is needed; `machine` is `null` to run on the master or a
machine id to run on that machine's agent (not connected: 409). A `name` the master has no script for - not a built-in one and no file of that name next to the shared config - is a 404 when it is to run on the master; on an agent, whose script files the master cannot see, the script starts and fails with `No script '...'`; `id` is the instance id (a new one when not given). `202 {"id"}`, or with `?waitSeconds=N` the end state as above. The state of a script that is still running is at
[`/api/scriptstates/{id}`](#GET-apiscriptstatesid).

### POST /api/scripts/{id}/kill

Stops a running script - any instance, a config-defined script under its own guid or one started with `POST /api/scripts`. `202`. Unknown script: 404. (CLI: [`KillScript`](CLI.md#KillScript).)

## CLI

### POST /api/cli

Runs [CLI](CLI.md) commands. The body is **one line of plain text**: one command, or several separated by `;`, in the
syntax of the CLI - for example `StartApp m1.a` or `StartPlan plan1 ; WaitForScript 2d5b3159-83c6-48d4-9c52-0ce1af92cbb2`. Only the
first line of the body is read; an empty body is a 400.

The call returns when the command has finished - for the commands that wait (`WaitForScript`, `GetAppFiles`, `DownloadPackage wait=1`,
...) that may be a long time, so give the HTTP client a generous timeout. The answer is the response the CLI would print, as **one JSON
string**: the lines of the response joined with newlines.

```
POST /api/cli          body: GetAppState m1.camera
200                    "APP:m1.camera:SRI:0:0.56:0:0:0::75268\n"

POST /api/cli          body: StartApp m1.camera
200                    "ACK\n"

POST /api/cli          body: GetAllPlansState
200                    "PLAN:plan1:InProgress\nEND\n"
```

A command that fails - an unknown application included - answers `200` with a line starting `ERROR:` inside the string; see [CLI](CLI.md) for what every command answers
and how its answer ends. Whatever the CLI offers - starting and killing apps and plans, running scripts, `SetVars`, `Shutdown`,
`WakeUp`, `ReloadSharedConfig` - this endpoint offers too, which is why [the warning about access](#Web-API) matters.

## Help

### GET /api/help

The documentation of the master you are talking to: redirects (302) to [`/api/help/HTTP.md`](#GET-apihelpdoc), this page. Every master carries the pages
of its own version, so the answer always matches the program - there is nothing to download.

### GET /api/help/{doc}

One page of the documentation as Markdown (`text/markdown; charset=utf-8`): `HTTP.md`, `Files.md`, `CLI.md`, `SharedConfig.md`, `MarkAndClear.md`, ... The name is
the file name, case does not matter; anything else - an unknown page, a name with a path in it - is a 404 with the usual JSON body. The links between the pages
are relative (`Files.md#file-templates`), and since the pages are served side by side under `/api/help/`, they work in a browser or a Markdown viewer as they are.

## WebSocket

### WebSocket /websock

The server accepts WebSocket connections at `ws://<master>:8877/websock` (module `WebSocketDirigentModule`). It is meant
to push state notifications of apps and plans to a browser, with messages of the form
`{"type":"planState","data":{"id":"plan1","state":{"code":"InProgress","flags":"R"}}}`. **At present it pushes nothing**:
the connection is accepted and kept, messages sent to it are ignored, and the periodic push is not switched on. The page in
`wstest.html` of the static folder is a test client for it. Poll the REST calls above for state.

## Static files

### GET /

**Redirects (302) to [`GET /api`](#GET-api)**, the description of the API - so that the address alone leads a tool, an agent or a person
somewhere useful.

Any other path that is not under `/api` or `/websock` is served from an `html` folder next to the master's executable, if there is one;
releases ship none at present (the web page that used to be in the source tree spoke to routes that no longer exist). Files are cached
in memory. A path that does not exist is a 404 with EmbedIO's standard HTML page - the JSON error body is for `/api` only.

## Limits

* **Request rate.** An address that sends more than **50 requests within a second** is banned for **10 minutes**: every request
  from it is answered `403` with `{"error": "This address sent more than 50 requests in a second and is banned for 10 minutes. ..."}`.
  What gets a tool there is asking item by item - the state of each of 93 applications at once is 93 requests in a moment - so ask for
  the lists (`/api/appstates`, `/api/planstates`, `/api/clients`, `/api/applocations`, a selection to `/api/files/query`) and poll
  with a pause; `?wait=N` on a download job exists for that reason. Answers of `404` and other errors do **not** count towards a ban;
  only the rate does.
* **Concurrency.** State and definition calls are handled one after another on the master's own thread, in its tick (50 ms by
  default): each takes about 60 ms, so a client asking one after another gets about 16 answers a second. File previews and
  downloads run on other threads and do not block them.
* **Archives are streamed**, never held in memory, so their size is limited by the disk only, not by the server.
