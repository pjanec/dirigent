# Writing a diagnostic tool against Dirigent

*Served by every master at `GET /api/help/DiagnosticTools.md`.*

How a tool - a desktop UI, a script or an AI agent - uses the [web API](HTTP.md) of a Dirigent master to find out
what a site is and which versions of the applications it runs, and to collect logs and configuration into one zip -
for example to attach it to a bug report. This page says **what to call and in which order**; every route is
described in [HTTP.md](HTTP.md).

What Dirigent does **not** do for a tool: it does not know what an application's version *means* (it reports what is on
the disk, as text), it does not judge the state of the system (it reports facts), it does not authenticate anybody, and
it does not talk to a bug tracker or anything else outside the site.

## 1. Reaching the master

* The API is on the **master** machine, port **8877** (`--httpPort`). It and every path it hands out use the **site's
  own addresses**, so the tool runs on a machine of the site network (or through a tunnel into it).
* **There is no authentication**: whoever reaches the port can start and kill applications. Keep the tool and the port
  inside the site.
* Knowing only the address is enough: **`GET /`** redirects to **`GET /api`**, which answers what this is (version, site
  name, master, shared config), where the manual is, the order of first questions, the limits, and every route with a
  link to its section of the manual.
* **Ask for lists, not item by item.** More than 50 requests in one second ban the tool's address for **10 minutes**
  (`403`, JSON message). Everything a UI shows has a list call: `/api/appstates`, `/api/planstates`, `/api/clients`,
  `/api/applocations`, `/api/appversions`, one selection for `/api/files/query`. Poll with a pause; download jobs take
  `?wait=N` so that polling is one request per N seconds.
* Answers are JSON, UTF-8 without a byte order mark. Every error under `/api` is `{"error": "...", "code": N}`: 400 the
  request is wrong, 404 something named does not exist (the message names it), 409 the master declined (a machine not
  connected, an application that failed to start), 504 a `?waitSeconds=` ran out.

## 2. What the site is

| Ask | For |
| --- | --- |
| `GET /api/status` | `siteName` (`<Shared Name="...">`), Dirigent version, master, shared config path and load time, counts, connected clients |
| `GET /api/machinedefs` | every machine: id, configured and actual IP, `connected` and since when, its `shares`, its applications and machine-level file definitions |
| `GET /api/appdefs` | every application: id (`machine.app`), `component`, `groups`, program, working folder and arguments **as declared** |
| `GET /api/appstates` | what each application does: `code` (`Running`, `Terminated (3)`, `Failed to start`, ...), `flags`, `plan`, `pid`, `startedAt`, `endedAt`, `exitCode`, `restartsRemaining`, `cpu`, `memoryMB`, `offline` |
| `GET /api/plandefs`, `/api/planstates` | the launch plans, their applications, which file definitions a plan marks when it starts (`markFiles`) and how often it takes checkpoints (`markInterval`) |

**Components.** An application, and a package of files, may say which software component it belongs to: `component`
(the config's `Component` attribute - `Viewer`, `Broker`, ...). That is the key a tool groups by - versions, applications and
collected files. `groups` is something else: grouping for people (GUI folders, a tool's sections), with no meaning
attached.

**Ids.** A state's `id` carries the plan its application last ran in (`m1.camera@plan1`) - an application has one
instance and so one state, that of its most recently used definition - while definitions have none; compare the part
before `@`. Machine ids are case-sensitive.

## 3. Versions of the applications

**`GET /api/appversions`** lists the versions on the site, **each once**, with the machines and applications that have it:

```json
{ "component": "Viewer", "source": "D:\\Apps\\Viewer\\VersionStamp.txt",
  "version": [ "Viewer 2.4.0.337", "Built 2026-11-11 11:50", "Commit 4f1c2d9" ],
  "machines": [ "m1", "m2", "m3" ], "apps": [ "m1.viewer", "m2.viewer", "m3.render" ] }
```

* `version` is what is on the disk, **split into lines and nothing else**: the version resource of the program
  (`source: "resource"`), or the text of the first of `version.txt`, `versioninfo.txt`, `VersionStamp.txt`,
  `VersionStamp*.txt` next to the program (`source` is its path).
* Two entries differ exactly when what is on the disk differs - so a component listed twice is a component with two
  versions on the site (a machine that was not updated).
* An application without a version is listed too, `version: null`, with the reason in `missing` (the program is not
  there, it has no version, its machine did not answer).
* **Judging compatibility is the tool's job.** Dirigent does not interpret version texts; their formats belong to the
  products.

Where each program and working folder is - `localPath`, `uncPath`, `exists`, `size`, `modified`, the version - per
application: `GET /api/applocations` (`?machine=` to narrow it; `/api/appdefs/{id}@plan/location` for one as a plan
defines it).

## 4. Offering components to collect

The files a site can collect are declared in the shared config as **file definitions** and grouped into **packages**.
`GET /api/filedefs?type=FilePackage` lists them with `id`, `title`, `description`, `component` and `groups`.

* A package with a `component` stands for that component: one checkbox, labelled with the component (or the last
  segment of the `title`), the `description` as its tooltip.
* `groups` can order the checkboxes into sections ("Main components", "Other components") - whatever the site's
  configuration says; Dirigent attaches no meaning to it.
* A package with a `machine` belongs to one machine (the same package declared for every machine by a machine
  template - "everything of this machine"): address it with its `machine`.
* The others are ready-made sets across components (all logs, the incident set, ...).

## 5. Preview, collect, fetch

All three take **one selection** - the same object, so that what is previewed is what is collected:

```json
{ "fileDefs": [ { "id": "pkg.comp.viewer" }, { "id": "pkg.comp.broker" }, { "id": "pkg.machine", "machine": "m1" } ],
  "apps": [ "m2.broker" ] }
```

1. **Preview** - `POST /api/files/query` with the selection: every file with `machine`, `localPath`, `uncPath`, `size`,
   `modified`, its `app`, `component` and the file definition it came from, and totals (`files`, `bytes`,
   `bytesToCollect` - what a collection from the marks would take, after `TailBytes`). Read-only; a second or so for a
   whole site.
2. **Collect** - `POST /api/downloads` with the selection, and optionally:
   * `"comment"` - goes into the archive's cover note and site state; put the bug id there;
   * `"name"` - the archive's file name, e.g. `"Bug 4711 - Lab 2"`;
   * `"since"` - collect what was written since a time instead of since the marks (see 7);
   * `"siteState": false` - leave out `_sitestate.json` (it is in by default).

   Answers `202 {"id"}`.
3. **Wait** - `GET /api/downloads/{id}?wait=60` until `status` is `Finished` (or `Failed`); `progress` (0..1) and
   `text` meanwhile. **The archive's `size` is in the answer before anything is downloaded** - check it against the
   upload limit of the bug tracker there.
4. **Fetch** - `GET` the archive's `url` (a stream, `application/zip`), or read it through its `uncPath`.
5. **Clean up** - `DELETE /api/downloads/{id}` once uploaded. Archives are otherwise kept 24 hours on the master.

The zip:

* `_comment.txt` - site, time, selection, machines, Dirigent version, the comment, the time window of the logs;
* **`_sitestate.json`** - the site at that moment, for whoever reads the bug: the versions (each once, as lines), the
  machines, the plans, and every application's state, start and end time and exit code - indented for a person, facts
  and no diagnosis;
* one folder per machine and in it one per selected item; a file reached through two selected items is in both;
* `_incomplete.txt` per machine - what could not be collected in full, and why.

## 6. Reading files in place

Every previewed file has `uncPath` - `\\<machine IP>\<share>\...` built from the machine's `<Share>` in the shared
config - so the tool can open or copy a single file without collecting anything. Dirigent hands out the path; the
**access is Windows'**: the tool's session needs rights on that share. A session that itself came in over the network
(a remote PowerShell, a scheduled task) has no credentials to pass on and must map the share explicitly
(`net use \\10.0.0.5\C /user:...`). A file whose machine declares no share has `uncPath: null` and the reason in
`uncError`.

## 7. Which part of the logs

Logs grow across runs, so a collection takes **only what was written since a line**:

* **Since the mark** (the default). A mark records how long each clearable log is at a moment; a plan that starts the
  system usually marks its logs first (`markFiles` in `/api/plandefs`; `markResult` in `/api/planstates`). A tool can
  mark by hand before reproducing a problem: `POST /api/marks` `{"operation":"mark", ...selection}`; `"unmark"` takes
  the line away. Marks destroy nothing; `clear` empties files and is not for a production site.
* **Since a time** - `"since": "2026-11-20T12:00:00Z"` on the download: each log from the latest record of its length
  at or before that time. Records are made by every mark and - every `markInterval` seconds while it runs - by a plan
  that has one (checkpoints; `lastCheckpoint` in its state). Without such a plan, "since" can only start at the last mark
  before the time, or take the whole file. Wrong only towards too much; every cut entry says where it starts and why.
  The previews do not apply `since`; the archive's size is known before it is downloaded.

On a real site a mark made the difference between 224 MB and 4 MB for the logs of one run.

## 8. A tool's session, end to end

```
GET  /api                                         what is this, where is the manual
GET  /api/status                                  site name, Dirigent version, who is connected
GET  /api/machinedefs                             machines: online? shares?
GET  /api/appversions                             the versions on the site, each once -> compatibility check
GET  /api/appstates                               what runs, what ended and how (startedAt, endedAt, exitCode)
GET  /api/filedefs?type=FilePackage               the checkboxes: packages with a component
POST /api/files/query        {selection}          sizes for the ticked boxes
POST /api/downloads          {selection, "comment":"Bug 4711", "name":"Bug 4711 - Lab 2"}
GET  /api/downloads/{id}?wait=60                  until Finished; archives[0].size before fetching
GET  /api/downloads/{id}/archives/{name}          the zip (with _sitestate.json) -> attach to the bug
DELETE /api/downloads/{id}
```
