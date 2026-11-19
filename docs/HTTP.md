# Web API

Dirigent is running a simple web server on default port **8877**, making Dirigent's features
available also via the HTTP protocol. The port is set with `--httpPort`, master only.

Two things about that option are worth knowing:

* **`--httpPort 0` does not switch the web server off.** Zero means "not specified", so the
  default 8877 applies. Pass **`-1`** to run without a web server - which also matters when two
  Dirigent installations share a machine, as they would otherwise fight over the port.
* No URL reservation or elevation is needed. The server listens on its own socket rather than
  through the Windows HTTP stack.

### GET /api/appdefs

returns a list of all appdefs as JSON `[{'id':'m1.a'}, ...]`

### GET /api/appdefs/m1.a

returns a list of a specific appdefs as JSON `{'id':'m1.a'}`

### GET /api/appstates

returns a list of the state of all apps as JSON `[{'id':'m1.a', 'status':{'code':'Running', 'flags':'SIP'}}, ...]`

### GET /api/appstates/m1.a

returns a list of the state of given app as JSON `{'id':'m1.a', 'status':{'code':'Running', 'flags':'SIP'}}`

### GET /api/clients

returns a list of all machines  `[{"id": "m1","state": {"connected": true,"ip": "127.0.0.1","lastChange": "2026-05-06T13:30:15.5558945Z"}}, ...]`

### GET /api/plandefs

returns a list of all plandefs as JSON `[{'name':'plan1', 'appDefs':[...]}, {'name':'plan2', 'appDefs':[...]}]`

### GET /api/plandefs/plan1

returns plandef of a single given plan as JSON `{'name':'plan1', 'appDefs':[...]}`

### GET /api/planstates

returns a list of the state of all plans as JSON `[{'name':'plan1', 'status':{'code':'InProgress'}}, {'name':'plan2', status={'code':'None'}}]`

### GET /api/planstates/plan1

returns a state of a single plan as JSON `{'name':'plan1', 'status':{'code':'InProgress'}}`

### GET /api/appdefs/m1.a/files

returns the actual files of one application instance, each with the path on its own machine
(`localPath`) and, where a file share covers it, a UNC path (`uncPath`) a diagnostic tool can read.
`m1.a@plan1` addresses the application as defined in that plan.

Which definition answers (the one whose file definitions - `<File>`, `<Folder>`, ... - are listed):

* `m1.a@plan1` - the definition in `plan1` (`planSource` is `requested`). Unknown plan, or an app not in it: 404.
* `m1.a` - the plan the app last ran in, if it has one (`lastRun`); otherwise the default definition (`none`). Unknown app: 404.

`?content=false` skips scanning the folders: only the file definitions themselves are resolved and `files` stays empty.

```json
{ "app": "m1.a", "plan": "plan1", "planSource": "requested",
  "fileDefs": [ { "id": "logs", "title": "Logs", "type": "Folder", "machine": "m1",
               "localPath": "D:\\Logs", "uncPath": "\\\\10.0.0.5\\D\\Logs", "uncError": null,
               "error": null, "notes": [],
               "files": [ { "path": "Logs/sub/a.log", "machine": "m1",
                            "localPath": "D:\\Logs\\sub\\a.log", "uncPath": "\\\\10.0.0.5\\D\\Logs\\sub\\a.log" } ] } ] }
```

Each top-level file definition of the app is resolved on its own, so one that fails (its machine is not
connected, the folder is missing) has `error` set and the others are still answered. If no
share of the machine covers a path, `uncPath` is `null` and `uncError` says why (once per file definition);
`localPath` is still there. `notes` carries what the resolution had to leave out (size limits,
members of a package that could not be looked up). A file definition with no machine (global) has only `uncPath`.

The files are listed, not served: the tool reads them through the UNC path. The same answer is given by
`POST /api/cli` with `GetAppFiles m1.a@plan1` (see [CLI](CLI.md#GetAppFiles)).

### GET /api/filedefs/pkg/files?machine=m1&app=a

the same for any top-level file definition - a file package, usually - addressed the way scripts address
them: `pkg` is the file definition id (wildcards allowed), `machine` and `app` optionally narrow it, `content`
is as above. A selector matching several file definitions answers as one container of all of them. Nothing
matching: 404. The whole selection is resolved in one go, so a package spanning many machines costs one
round trip per machine, and a member that cannot be reached becomes a `note` while the other members'
files are listed.

```json
{ "id": "pkg", "machine": null, "app": null, "fileDefs": [ { "id": "pkg", "title": "Pkg", "type": "FileRef", "files": [ ... ] } ] }
```

Each file carries its own `machine`. Also available as `POST /api/cli` with `GetFiles pkg machine=m1`.

Errors of every `/api` call are JSON, `{"error":"<message>","code":404}`, with the status code in the response too.

### GET /api/filedefs

lists the top-level file definitions (`<File>`, `<Folder>`, `<VFolder>`, `<FilePackage>`, `<FileRef>`) as declared in the shared config - what there is to collect. Nothing is
looked up in any file system. Optional filters: `type` (`File`, `Folder`, `VFolder`, `FilePackage`,
`FileRef`; case-insensitive), `id`, `machine`, `app` (the wildcards of the `ListVfsNodes` script: `*`
and `?`, empty means anything). Example: `GET /api/filedefs?type=FilePackage&id=logs.*`

```json
[ { "id": "logs.all", "type": "FilePackage", "title": "Logs/All apps", "description": "...",
    "groups": "", "machine": null, "app": null } ]
```

### Download jobs

A diagnostic tool collects files in five steps: **list** the file definitions (`/api/filedefs`), **preview** what a
file definition or app stands for (`/api/filedefs/{id}/files`, `/api/appdefs/{id}/files`), **collect** (`POST /api/downloads`),
**poll** the job, **fetch** the archive. The archives are built on the master's machine, in a folder
of Dirigent's own (`--archiveFolder`, see [Local Config](LocalConfig.md#Download-archives)), one
subfolder per job, and kept for `--archiveRetentionHours` (24 by default).

#### POST /api/downloads

starts a collection. Body: `{"fileDef":{"id":"logs.all","machine":"m1","app":"a"}}` (a file definition or package, addressed
like `/api/filedefs/{id}/files`; machine and app optional) or `{"app":"m1.a@plan1"}` (all the file definitions of the
application, from the definition picked as for `/api/appdefs/{id}/files`), and an optional `"comment"` that goes
into the archive's cover note. Answers `202 {"id":"<job guid>"}`; unknown file definition, app or plan: 404.
Old jobs are cleaned up first.

#### GET /api/downloads

lists the jobs on disk, newest first, each as below.

#### GET /api/downloads/{id}?wait=N

```json
{ "id": "...", "status": "Finished", "progress": 1.0, "text": null,
  "created": "2026-11-19T10:00:00Z", "finished": "2026-11-19T10:00:20Z", "machine": "master1",
  "request": { "fileDef": { "id": "logs.all" } },
  "files": [ { "name": "All apps_261119_1000.zip", "size": 1234567,
               "url": "/api/downloads/<id>/files/All%20apps_261119_1000.zip",
               "localPath": "C:\\ProgramData\\Dirigent\\Archives\\<id>\\All%20apps_261119_1000.zip",
               "uncPath": "\\\\10.0.0.5\\C\\ProgramData\\Dirigent\\Archives\\<id>\\All%20apps_261119_1000.zip",
               "uncError": null } ],
  "errors": [], "notCollected": [] }
```

`status` is `Running`, `Finished`, `Failed`, `Cancelled`, or `Orphaned` (the job was running when the master
went away; nothing is left to finish it). While running, `progress` (0..1, or `null` when unknown) and `text`
come from the live script. `?wait=N` holds the answer until the job has ended, at most N seconds
(max 120). After a master restart the job is still answered from its `job.json`. `uncPath` is `null`
with the reason in `uncError` when no file share covers the archive folder.

#### GET /api/downloads/{id}/files/{name}

streams the archive (`application/zip`, `Content-Disposition: attachment` with an ASCII `filename` and the exact `filename*=UTF-8''...`) without loading it into
memory. `name` must be one of the job's `files`, otherwise 404; a job that is still running: 409;
`--serveArchives 0`: 403 (listing, status and `uncPath` still work, so a tool can read the archive through the share instead).

#### DELETE /api/downloads/{id}

stops the job if it is running and removes its folder (204); unknown job: 404. A running job's folder
goes as soon as its script has stopped.

The same through `POST /api/cli`: `GetFileDefs`, `GetFiles`, `DownloadPackage`, `DownloadApp`, `GetDownload`,
`DeleteDownload` (see [CLI](CLI.md#DownloadPackage)).

### POST /api/cli

Executes a cli command sent as POST data; for example `StartApp m1.a`
Returns same response as from the CLI command (see CLI command reference), for example `ACK`
