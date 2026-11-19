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

**Format.** Requests and answers are JSON (`application/json`), field names `lowerCamel`. The only exception is
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
| 400 | The request is malformed: an id that cannot be parsed (`m1.`, a script id that is no guid), an unreadable body. |
| 403 | Serving archives is switched off (`--serveArchives 0`), or the caller's address has been temporarily banned for sending too many requests (see [Limits](#Limits)). |
| 404 | The application, plan, script, client, file definition or download does not exist - or, for a file definition, nothing matches the filters. |
| 409 | The download job is still running; its archive is not there yet. |
| 500 | An unexpected failure; the message is in `error`. |

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
`POST /api/downloads`, `DELETE /api/downloads/{id}`, and the marking calls (`POST /api/marks` and the `.../mark|clear|unmark` routes).
`POST /api/files/query` is a `POST` only because it takes a body: it is a **read-only** preview and changes nothing. Everything else is `GET`.

## Overview

| Method | Route | Purpose | CLI twin |
| --- | --- | --- | --- |
| GET | [`/api/appdefs`](#GET-apiappdefs) | All application definitions | |
| GET | [`/api/appdefs/{id}`](#GET-apiappdefsid) | One application definition | |
| GET | [`/api/appdefs/{id}/location`](#GET-apiappdefsidlocation) | Where the binary and working folder of an application are, as its agent resolves them | [`GetAppLocation`](CLI.md#GetAppLocation) |
| GET | [`/api/applocations`](#GET-apiapplocations) | The same for all applications (`?machine=`) | [`GetAppLocations`](CLI.md#GetAppLocations) |
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
[{"id": "m1.camera","groups": "","machine": "m1","exeFullPath": "D:/apps/camera/camera.exe","startupDir": "D:/apps/camera","cmdLineArgs": "--id %APP_ID%"},
 {"id": "m2.recorder","groups": "","machine": "m2","exeFullPath": "[cmd.command]","startupDir": "","cmdLineArgs": "echo hi"}]
```

`groups` is the `Groups` attribute of the application, `;`-separated. `exeFullPath`, `startupDir` and `cmdLineArgs` are
**exactly as the config declares them**: variables like `%APP_ID%` are not expanded and a relative path stays relative. What they
become on the machine, and whether the file is there, is answered by [`/api/appdefs/{id}/location`](#GET-apiappdefsidlocation).
The meaning of `ExeFullPath`, `StartupDir` and the reserved names like `[cmd]` is in [the `<App/>` element](SharedConfig.md#App-element).

### GET /api/appdefs/{id}

One definition, as above: `{"id": "m1.camera","groups": "","machine": "m1","exeFullPath": "D:/apps/camera/camera.exe","startupDir": "D:/apps/camera","cmdLineArgs": "--id %APP_ID%"}`.
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
{"app": "m1.camera","plan": null,"planSource": "none","machine": "m1","kind": "process",
 "exe": {"declared": "bin/%TOOLS%/camera.exe","localPath": "D:/root/bin/v1/camera.exe",
         "uncPath": "//10.0.0.5/D/root/bin/v1/camera.exe","uncError": null,
         "exists": true,"size": 1048576,"modified": "2026-11-19T09:58:03Z","fileVersion": "2.4.1.0"},
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
* `exists`, `size`, `modified` (UTC) and `fileVersion` (the file version resource; `null` if the file has none) are read on the machine.
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
Each application is described from the definition [the same way](#GET-apiappdefsidlocation): the plan it last ran in, else the default.
All the machines are asked at once. (CLI: [`GetAppLocations`](CLI.md#GetAppLocations).)

### GET /api/appstates

The state of every application that has both a state and a definition, in the order the master keeps them.
(CLI: [`GetAllAppsState`](CLI.md#GetAllAppsState).)

```json
[{"id": "m1.camera","state": {"code": "Running","flags": "SRI"}},
 {"id": "m1.tracker","state": {"code": "Not running","flags": ""}}]
```

`code` is the text the GUI shows - `Not running`, `Planned`, `Initializing`, `Running`, `Dying`,
`Restarting (2 remaining)`, `Killed`, `Terminated (<exit code>)`, `Failed to start` - and carries a suffix like
` (Offline for 12 sec)` when the machine has not reported lately. `flags` is a string of single letters, in this order:
`S` started, `F` start failed, `R` running, `K` killed, `I` initialized, `P` plan applied, `D` dying,
`X` restarting. What each means is in [Application status](Apps.md#Application-status).

### GET /api/appstates/{id}

One state, as above: `{"id": "m1.camera","state": {"code": "Running","flags": "SRI"}}`. (CLI: [`GetAppState`](CLI.md#GetAppState).)
With `m1.camera@plan1` the state is judged against that plan, which is what turns `Not running` into `Planned` while
the plan is still starting its applications - see [Plan status](Plans.md#Plan-status).

The older name [`/api/appstate/{id}`](#GET-apiappstateid) is also accepted. Unknown application, or one that has no state or no definition: 404.

`GET /api/appdefs/{id}/files` previews the actual files of one application instance; it belongs with the file calls and is
described under [Files and downloads](#GET-apiappdefsidfiles).

#### GET /api/appstate/{id}

The older name of [`/api/appstates/{id}`](#GET-apiappstatesid); the same answer, still accepted.

## Plans

A plan launches a set of applications in dependency order; see [Plans](Plans.md) and
[Launch plan](SharedConfig.md#Launch-plan) in the shared config.

### GET /api/plandefs

```json
[{"id": "plan1","appDefs": [{"id": "m1.a","groups": ""},{"id": "m2.b","groups": ""}],"groups": "Test1"}]
```

`appDefs` are the plan's own copies of its applications; `groups` is the plan's `Groups` attribute.

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
(`{"marked":2,"cleared":0,"skipped":0,"failed":0,"timedOut":false,"cancelled":false,"errors":[]}`). Other plans have exactly the fields above.

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
GET  /api/filedefs?type=FilePackage&group=component      -> one checkbox per package: id, title, description
POST /api/files/query     {"fileDefs":[{"id":"pkg.comp.viewer"},{"id":"pkg.comp.ig"}]}   -> the files and the sizes, to show
POST /api/downloads       {"fileDefs":[{"id":"pkg.comp.viewer"},{"id":"pkg.comp.ig"}],"comment":"why"}   -> 202 {"id": "..."}
GET  /api/downloads/{id}?wait=60                         -> poll; then GET the archive's url
POST /api/marks           {"operation":"mark","fileDefs":[{"id":"pkg.comp.viewer"},{"id":"pkg.comp.ig"}]}  -> before the next run
```

The `group` is one entry of the `Groups` of the file definition (`Groups="component;major"`) - see [Groups for tools](Files.md#groups-for-tools).

### POST /api/files/query

Previews the files of a [selection](#The-selection). **Read-only**: nothing is marked, collected or changed. Body: the selection. `?content=false` skips scanning the folders.

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

Each previewed file has two more fields: `app`, the application it belongs to (null for a machine-level or global file), and `fileDefId`, the `id` of the file
definition it came from - the one a package or an application referred to, carried down through folder scans and package members. A tool can show *component -> app ->
files* and filter by kind (`log.*`, `cfg.*`) without reading the paths.

### GET /api/appdefs/{id}/files

returns the actual files of one application instance, each with the path on its own machine
(`localPath`) and, where a file share covers it, a UNC path (`uncPath`) a diagnostic tool can read.
`m1.a@plan1` addresses the application as defined in that plan.

Which definition answers (the one whose file definitions - `<File>`, `<Folder>`, ... - are listed):

* `m1.a@plan1` - the definition in `plan1` (`planSource` is `requested`). Unknown plan, or an app not in it: 404.
* `m1.a` - the plan the app last ran in, if it has one (`lastRun`); otherwise the default definition (`none`). Unknown app: 404.

`?content=false` skips scanning the folders: only the file definitions themselves are resolved and `files` stays empty.

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
`FileRef`; case-insensitive), `id`, `machine`, `app` (the wildcards of the `ListVfsNodes` script: `*`
and `?`, empty means anything) and `group` - one entry of the `;`-separated `Groups` of the definition, compared case-insensitively, wildcards allowed
(`group=component` finds `Groups="Component;major"`; see [Groups for tools](Files.md#groups-for-tools)). Example: `GET /api/filedefs?type=FilePackage&id=logs.*`
or `GET /api/filedefs?type=FilePackage&group=component`.

```json
[ { "id": "logs.all", "type": "FilePackage", "title": "Logs/All apps", "description": "...",
    "groups": "", "machine": null, "app": null } ]
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
and an optional `"comment"` that goes into the archive's cover note. Answers `202 {"id":"<job guid>"}`; unknown file definition, app or plan
(the message names the item): 404; nothing selected: 400. All the items are looked up before the job starts. Old jobs are cleaned up first.

**One zip, and its layout.** A job makes one archive however many items there are.

* **One item** (a single `fileDef` or `app`, or a selection with exactly one item): the layout is what it has always been - no extra folder.
* **More than one item**: one top folder per item, titled as the item is declared - the `Title` of the package or file definition, the app id for an application -
  made fit to be a folder name; the same title twice becomes `Title` and `Title (2)`. (When the files come from several machines the machine folders come first, as ever.)
* **No deduplication.** A file reached through two items is in both folders, each copy following its own definition (`TailBytes`, marks). The archive's
  `_comment.txt` lists the selection; `job.json` and the job's `request` echo it; what could not be collected is reported under the name of the item.

```json
{"fileDefs":[{"id":"pkg.comp.viewer"},{"id":"pkg.comp.ig"}],"apps":["m1.a"],"comment":"after the crash"}
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

One operation over a whole [selection](#The-selection): `{"operation":"mark","fileDefs":[{"id":"pkg.comp.viewer"},{"id":"pkg.comp.ig"}],"apps":["m1.a"]}`.
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

## CLI

### POST /api/cli

Runs [CLI](CLI.md) commands. The body is **one line of plain text**: one command, or several separated by `;`, in the
syntax of the CLI - for example `StartApp m1.a` or `StartPlan plan1 ; WaitForScript 2d5b3159-83c6-48d4-9c52-0ce1af92cbb2`. Only the
first line of the body is read.

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

A command that fails answers `200` with a line starting `ERROR:` inside the string; see [CLI](CLI.md) for what every command answers
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

Everything that is not under `/api` or `/websock` is served from the `html` folder next to the master's executable:
`index.html`, `favicon.ico`, `manifest.json`, `robots.txt`, `wstest.html` and the `static` folder with the scripts and styles of
the web page (a small React application). Files are cached in memory. A path that does not exist is a 404 with
EmbedIO's standard HTML page - the JSON error body is for `/api` only.

## Limits

* **Request rate.** The web server bans an address for a while (answering `403`) when it sends more than about 50 requests
  per second. A tool that polls should wait between requests - `?wait=N` on a download job exists for that reason. Answers of
  `404` and other errors do **not** count towards a ban; only the rate does.
* **Concurrency.** State and definition calls are handled one after another on the master's own thread, so they are quick but
  not parallel; file previews and downloads run on other threads and do not block them.
* **Archives are streamed**, never held in memory, so their size is limited by the disk only, not by the server.
