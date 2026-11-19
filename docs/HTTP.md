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

### GET /api/appfiles/m1.a

returns the actual files of one application instance, each with the path on its own machine
(`localPath`) and, where a file share covers it, a UNC path (`uncPath`) a diagnostic tool can read.
`m1.a@plan1` addresses the application as defined in that plan.

Which definition answers (the one whose `<File>`/`<Folder>` nodes are listed):

* `m1.a@plan1` - the definition in `plan1` (`planSource` is `requested`). Unknown plan, or an app not in it: 404.
* `m1.a` - the plan the app last ran in, if it has one (`lastRun`); otherwise the default definition (`none`). Unknown app: 404.

`?content=false` skips scanning the folders: only the nodes themselves are resolved and `files` stays empty.

```json
{ "app": "m1.a", "plan": "plan1", "planSource": "requested",
  "nodes": [ { "id": "logs", "title": "Logs", "type": "Folder", "machine": "m1",
               "localPath": "D:\\Logs", "uncPath": "\\\\10.0.0.5\\D\\Logs", "uncError": null,
               "error": null, "notes": [],
               "files": [ { "path": "Logs/sub/a.log", "machine": "m1",
                            "localPath": "D:\\Logs\\sub\\a.log", "uncPath": "\\\\10.0.0.5\\D\\Logs\\sub\\a.log" } ] } ] }
```

Each top-level node of the app is resolved on its own, so a node that fails (its machine is not
connected, the folder is missing) has `error` set and the other nodes are still answered. If no
share of the machine covers a path, `uncPath` is `null` and `uncError` says why (once per node);
`localPath` is still there. `notes` carries what the resolution had to leave out (size limits,
members of a package that could not be looked up). A node with no machine (global) has only `uncPath`.

The files are listed, not served: the tool reads them through the UNC path. The same answer is given by
`POST /api/cli` with `GetAppFiles m1.a@plan1` (see [CLI](CLI.md#GetAppFiles)).

### GET /api/nodefiles/pkg?machine=m1&app=a

the same for any top-level VFS node - a file package, usually - addressed the way scripts address
nodes: `pkg` is the node id (wildcards allowed), `machine` and `app` optionally narrow it, `content`
is as above. A selector matching several nodes answers as one container of all of them. Nothing
matching: 404. The whole selection is resolved in one go, so a package spanning many machines costs one
round trip per machine, and a member that cannot be reached becomes a `note` while the other members'
files are listed.

```json
{ "id": "pkg", "machine": null, "app": null, "nodes": [ { "id": "pkg", "title": "Pkg", "type": "FileRef", "files": [ ... ] } ] }
```

Each file carries its own `machine`. Also available as `POST /api/cli` with `GetNodeFiles pkg machine=m1`.

### POST /api/cli

Executes a cli command sent as POST data; for example `StartApp m1.a`
Returns same response as from the CLI command (see CLI command reference), for example `ACK`
