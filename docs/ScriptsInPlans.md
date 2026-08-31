# Running a script as a step of a plan

A design, for review. Nothing of this is implemented.

Third revision, after review. The first had the CLI client do the waiting; the second moved it to the
master. This one settles the response protocol: a waiting command answers `ACK` when it accepts the
work and `END` when it is done, and the sender knows which commands do that from an **attribute on
the command class**. No protocol extension, no marker words, no new machinery.

## The need

A `System Start` plan should draw a line under the log files before it starts anything, so that
whatever is collected afterwards belongs to this run:

* run `BuiltIns/MarkFiles.cs` (or `ClearFiles.cs`) on a package, and
* **hold the plan** until that has finished - the applications must not start logging before the line
  is drawn.

Generally: a plan step that issues a Dirigent command and does not count as done until that command
really is.

Contents:

* [What already exists](#what-already-exists)
* [The four holes](#the-four-holes)
* [The design](#the-design)
* [What it looks like in the config](#what-it-looks-like-in-the-config)
* [Failure: what fails the plan, and what must not](#failure-what-fails-the-plan-and-what-must-not)
* [Non-interactive, by requirement](#non-interactive-by-requirement)
* [Telling "unknown" from "finished"](#telling-unknown-from-finished)
* [Compatibility](#compatibility)
* [What would be tested](#what-would-be-tested)
* [Rejected alternatives](#rejected-alternatives)
* [Settled in review](#settled-in-review)

## What already exists

**The plan gate is complete.** An app of a plan is launched only once every app named in its
`Dependencies` is `Initialized` (`AppLaunchPlanner.AreAllDepsSatisfied`, `AppLaunchPlanner.cs:133`) -
the only gate there is. `Initialized` is set **true at launch** (`LocalApp.AfterLaunch:201`) and only
an init detector pulls it back to false; the three detectors that exist are `exitcode`, `timeout` and
`windowpoppedup`. So a step worth waiting for is one whose `InitCondition` is not satisfied until the
work is done.

**A plan can already issue a Dirigent command with no external process.** `ExeFullPath` has reserved
names (`Launcher.ParseExe`, `Launcher.cs:271`): `[cmd]`, `[cmd.file]`, `[powershell]`,
`[powershell.file]`, `[powershell.command]`, and **`[dirigent.command]`**, which sends the app's
`CmdLineArgs` to the master as a CLI command (`LaunchDirigentCmd`, `Launcher.cs:500`).

**The answer to that command already comes back.** The master answers a `CLIRequestMessage` with a
`CLIResponseMessage` addressed to the requesting client (`Master.CLIClient.WriteResponse`,
`Master.cs:608`), and the CLI protocol carries an optional `[reqId]` which the master echoes in every
answer line (`CLIRequest.WriteResponseLine`).

**The master does not have to block to wait.** `CLIProcessor.Tick` keeps `pendingRequests` across
ticks and removes a request only when it reports `Finished`, spending at most
`MaxProcessingTimePerTickMs` (20 ms) per tick (`CLIProcessor.cs:146`). A request that takes minutes
costs nothing but a place in that list.

**Scripts already report enough to wait on.** `GetScriptState` gives `Status`, `Text`, `Progress` and
`Data`; a singleton script's record is kept for as long as the master lives
(`SingletonScriptRegistry.cs:46`), so its final state is readable after it ends.

**The response protocol already has three shapes**, and `ACK` does not mean what one might assume:

| shape | example | terminal line |
| --- | --- | --- |
| simple command | `StartPlan`, `StartScript`, `KillScript` | `ACK` |
| listing | `GetAllAppsState`, `GetAllPlansState`, `GetAllClientsState` - lines, then `END`; **no ACK at all** | `END` |
| accepted, then more later | `SendEvents` - *"sends an immediate ACK to confirm support before performing longer operations"* (`TelnetServer.cs:248`), then the status dump and spontaneous updates | none; the subscription just runs |

`CLI.md:641` is explicit: *"ACK does not mean that the command finished successfully! Only that it was
delivered and processed."* So a command that answers `ACK` on acceptance and `END` on completion
invents nothing - it combines the second and third shapes that are already there.

## The four holes

**1. A `[dirigent.command]` app's exit code is meaningless.** It has no process, so
`Launcher.checkExited()` reports "exited" the moment it is asked (`_proc == null`, `Launcher.cs:723`)
and `ExitCode` stays at the 0 it was set to at launch (`Launcher.cs:340`). So
`InitCondition="exitcode 0"` on such an app **initializes immediately**, whatever became of the
command - even if the master never received it. The response that would say otherwise is sent, but
**no client handles `CLIResponseMessage`**: nothing in the codebase reads it.

**2. No command answers "the script has finished".** `StartScript` answers `ACK` as soon as the script
has been *started* (`DirigentControlCommands.cs:467`); `GetScriptState` answers the state once.
Nothing waits.

**3. A CLI request cannot span ticks.** The infrastructure allows it, but `CLIRequest.Tick` runs every
command in one pass and then sets `Finished = true` (`CLIRequest.cs:103`), and `ICommand` has no way
to say "not yet".

**4. Starting a script whose instance id already exists cancels every other script on the master.**
Verified by reading the chain:

* the CLI `StartScript` always takes the path-carrying overload (`DirigentControlCommands.cs:497`)
  → `SingletonScriptRegistry.StartScript` (`:132`), which calls `entry.Dispose()` when the id is
  already known;
* `Entry.Dispose()` → `_localScriptRegistry.Dispose()` (`SingletonScriptRegistry.cs:36`) - and that
  registry is the master's own, shared by every entry (`Master.cs:179`);
* `LocalScriptRegistry.Dispose` disposes every `LocalScript` → `ScriptRunner.Dispose` → `Stop()`,
  whose own comment says it: *"this initiates the cancellation if the script is running"*
  (`ScriptRunner.cs:66`).

**Why that line exists.** It is a refactoring slip, and the history says so. Before `47a11a5`
("Scripts refactored. Tasks discontinued in favour of running plain remote scripts.", Dec 2022) the
predecessor of `Entry` was `ScriptEntry`, which **owned its script instance** - `_script`, `_runTask`,
`_runCTS` were its own fields, and its `Dispose()` called `Remove()`, which disposed *that one*
script. The intent was right. That refactor moved the instances into a shared `LocalScriptRegistry`
and translated `_script.Dispose()` as `_localScriptRegistry.Dispose()`, silently widening "end my
script" into "end every script". So the line is not to be deleted but **scoped back**.

**A second trap on the same path.** `LocalScriptRegistry.Start` declines a start while that instance
is alive (`LocalScriptRegistry.cs:94`), and `Stop` only *initiates* cancellation - the status passes
through `Cancelling` before `Cancelled`. So replacing a still-running instance stops the old script
and then silently declines to start the new one, today as much as after the fix.

## The design

Five pieces, each small.

### 1. A CLI command may take more than one tick

`ICommand` gains a way to say it is not finished - a property defaulting to `true`, so every existing
command is unaffected - and `CLIRequest.Tick` leaves an unfinished command at the head of its queue
instead of dequeuing it, keeping `Finished` false. `CLIProcessor` already does the rest.

This is the only change to the command infrastructure, and it is what lets a command wait without the
master blocking on anything.

### 2. `WaitForScript` - a master-side command that finishes when the script does

```
WaitForScript <guid> [timeout=<seconds>]
```

* `ACK` at once - the instance exists and the wait has begun (the documented meaning of ACK:
  delivered and processed).
* then nothing until it is over. No progress lines: whoever wants progress asks `GetScriptState`.
* `END` when the script ends up `Finished`.
* `ERROR: <message>` when it `Failed`, was `Cancelled`, the timeout expired, or the instance is
  unknown. On a timeout it also kills the script, because a mark that lands after the applications
  have started cuts the beginning off the run.

`StartScript` stays exactly as it is, and the two compose in one command line:

```
StartScript <guid> BuiltIns/MarkFiles.cs "<args>" ; WaitForScript <guid> timeout=300
```

The wait is also useful on its own - waiting for a script somebody else started, a GUI download
included.

### 3. The terminator is a property of the command class

A command declares how its response ends, so that every sender knows what to wait for without parsing
anything or guessing:

```csharp
[CliResponse( Terminator = ETerminator.End )]
public class WaitForScript : DirigentControlCommand { ... }
```

`ETerminator.Ack` is the default, so nothing has to be written on the twenty commands that answer
`ACK`. The three listings get `End` - which is what they already do; today a client waits for their
`END` only because no `ACK` is ever sent, and this makes it true by design.

`CommandRepository.Register` takes the command type, so it can read the attribute and answer
`TerminatorOf( commandName )` without instantiating anything. One source of truth, in the class that
implements the behaviour, which cannot drift from it.

### 4. `[dirigent.command]` learns the outcome of its command

* The launcher tags the request with a fresh id - `[<reqid>] <command>` - which the master already
  echoes back.
* The agent handles `CLIResponseMessage`, keeps the lines whose id matches, and expects **one
  terminal line per command it sent, in order**: `ACK`/`ERROR` for an `Ack`-terminated command,
  `END`/`ERROR` for an `End`-terminated one. The launcher only looks at the first token of each
  `;`-separated part - a name lookup, not command parsing.
* When the last one has arrived, it hands the outcome to the app: **exit code 0** if no `ERROR` line
  appeared, **1** if one did, and the app is marked as exited.
* Nothing new is needed for the gate: the existing `exitcode` init detector then means what it says.

No timeout on the answer: messages are not lost, and a master that never answers is a dead master,
which is the end of the world for the whole plan anyway. The `timeout=` of `WaitForScript` covers the
case that is real - a script that hangs.

Two exit codes and no more: "the command did what it said" or "it did not". `WaitForScript` puts the
reason in its `ERROR` text and in the log, and an unreachable master is indistinguishable from a dead
one, so there is nothing for a finer vocabulary to express.

### 5. `Entry.Dispose()` scoped back to its own script

* `Entry.Dispose()` → `_localScriptRegistry.Stop( Def.Guid )`: end *this* entry's script, which is
  what it meant before the 2022 refactor - not every script on the master.
* and the replace path force-removes that one instance before starting the new one, so that
  restarting a running singleton script actually starts it instead of being declined while the old
  one is still cancelling. That needs a scoped `Remove( Guid )` on `LocalScriptRegistry` - the
  private one it already uses for housekeeping, made available to its owner.

### 6. The one-shot CLI honours the same attribute

`Dirigent.CLI.exe "<command>"` stops at the first line starting with `ACK`, `END` or `ERROR` and gives
up after 5 s of silence (`CliApp.NonInteractiveSubCmd`). With the attribute it knows better: for an
`End`-terminated command it reads past the `ACK` and waits without that limit. So a batch file can
wait for a script too, and the client stops conflating "acknowledged" with "finished" - which the
docs say are different things.

Small and optional: the plan route does not go through this client at all, and a telnet session
already waits as long as it likes (`respReadingThreadFunc` loops on `ReadResp(10)` ignoring the
silence).

## What it looks like in the config

```xml
<Plan Name="System Start">

    <!-- draw the line under the logs before anything starts writing; no process involved -->
    <App AppIdTuple = "master.mark_logs"
         ExeFullPath = "[dirigent.command]"
         CmdLineArgs = "StartScript 7B3C1E90-1111-2222-3333-444455556666 BuiltIns/MarkFiles.cs ""'{Node:{Id:''pkg.run''}}'"" ; WaitForScript 7B3C1E90-1111-2222-3333-444455556666 timeout=300"
         Volatile = "1"
         InitCondition = "exitcode 0,1"
    />

    <App AppIdTuple = "m1.camera"   Dependencies = "master.mark_logs" ... />
    <App AppIdTuple = "m2.recorder" Dependencies = "master.mark_logs" ... />

</Plan>
```

* A fixed guid in the config, which is how singleton scripts are already identified there.
* One step covers the whole system: `MarkFiles` marks every machine holding files of the package, in
  parallel, by itself.
* `Volatile="1"`, because the step is meant to end - see
  [Utility plans](Plans.md#utility-plans-vs-standard-plans).
* `exitcode 0,1` rather than `exitcode 0`: see below.
* The same shape gives a plan `ClearFiles`, `UnmarkFiles`, or any other Dirigent command.

## Failure: what fails the plan, and what must not

The rule stands as it is (`Plan.CalculatePlanStatus`, `Plan.cs:310`): a plan is a *Success* when every
app has initialized and every non-volatile one is still running, so **one step that fails to
initialize fails the plan**. That is what a step should do when it matters.

Marking the logs does not matter that much: a missing mark degrades a later collection - it will hold
more than one run - but it must not stop the system from starting. So the step counts as initialized
whatever the script did, which is a matter of what its `InitCondition` accepts:

| `InitCondition` on the step | effect |
| --- | --- |
| `exitcode 0` | only a clean run initializes the step; a failure holds the dependent apps and fails the plan. For a step that must not be skipped. |
| `exitcode 0,1` | either answer initializes the step - the mark is attempted, a failure shows in the log and in the answer, and the plan carries on. **This is the one for the mark.** |

No code either way: the choice is in the config, which is where "is this step critical" belongs.

**A caveat to document:** `ExitCodeInitDetector` expands a range into one entry per value
(`ExitCodeInitDetector.cs:46`), so `0-255` is 256 entries and fine, while `0-2147483647` would try to
build two billion. Worth a note there; teaching it to keep ranges is a small fix outside this design.

## Non-interactive, by requirement

A script started from a plan or the CLI must touch no user interface: nobody is at the machine, and a
modal dialog on the master would sit there until somebody found it.

This holds structurally, and needs no code:

* a script reports through a `UserNotificationMessage` addressed to its **requestor**;
* the only thing in the system that renders one is the WinForms GUI (`Forms/Main.cs:151`), and it
  ignores any that is not addressed to itself (`m.HostClientId != _core.Client.Ident.Name`);
* the requestor of a `[dirigent.command]` step is the **agent** that issued it, and an agent has no
  handler for that message at all - nor has the console agent, nor the ImGui build.

So a plan-started script's message box has nobody to open it. No check inside the script, no `Quiet`
flag in the arguments: the invariant is that only a GUI renders notifications and only for itself.
A tier-1 test asserts it rather than leaving it to reading - the bed's operator already collects the
notifications it receives.

Two things need no change: the status bar with the progress and the cancel button belongs to the GUI
that *started* an operation, so a plan-started script shows nothing there; and the Scripts tab keeps
showing the script's state, which is passive and useful. The `AskComment="1"` dialog is a GUI-side
thing that never reaches a script started any other way - a plan step passes a `Comment` in its
arguments if it wants one in the archive.

## Telling "unknown" from "finished"

`WaitForScript` has to answer at once when there is nothing to wait for, so it must tell a finished
script from an id nobody knows. It can:

| what the master finds | `GetScriptState` gives | what it means |
| --- | --- | --- |
| a singleton entry whose script has run | a state with `Status = Finished` (or `Failed`, `Cancelled`) and its `Data` | it is over - answer now |
| a singleton entry whose script is running | `Running` / `Starting` / `Cancelling` | keep waiting |
| no entry at all, or one declared in the config but never started | **null** - an empty response line | nothing to wait for - `ERROR` |

The retention is what makes the first row possible: a singleton script's record is kept for as long as
the master lives (`forgetTime = -1`, `SingletonScriptRegistry.cs:46`). Two things do **not** survive,
both outside this design: a *generic* script started through `StartScriptMessage` is forgotten ten
seconds after it dies, and nothing survives a master restart.

"Unknown id" and "declared but never started" cannot be told apart - both are a null state - and for a
wait they are the same case.

## Compatibility

* **No protocol extension.** `ACK`, `END` and `ERROR` keep their documented meanings; no new response
  word is introduced. `CLIRequestMessage` and `CLIResponseMessage` are used as they are, and the
  request id is an optional part of the CLI line the master already supports.
* **`ICommand` gains a property with a default**, and `CommandRepository.Register` gains the command
  type. Every existing command compiles and behaves unchanged.
* **`WaitForScript` is a new command**, so a client using it needs a master that has it - the usual
  rule. Nothing existing changes meaning.
* **`[dirigent.command]` apps change behaviour**: their exit code stops being a constant 0 and starts
  meaning something. A config relying on `exitcode 0` initializing such a step regardless would begin
  to depend on the command actually succeeding. That is the point of the change, and the one thing
  here worth a line in the release notes.
* **No config schema change.** `InitCondition`, `Dependencies` and `Volatile` are used as they are.
* The one-shot `Dirigent.CLI.exe` changes only in that it waits for the terminator its command
  declares; for every command that exists today the terminator is the one it already waits for.

## What would be tested

* **Tier 0**: the tickable-command mechanics - a command reporting unfinished stays in its request and
  the request is not dropped; the terminator attribute answering for every registered command;
  `WaitForScript` mapping Finished / Failed / Cancelled / timeout / unknown onto `END` and `ERROR`.
* **Tier 1**: `Entry.Dispose` - two scripts running on the master, restart one by id, assert the other
  survives (the regression test for hole 4), and that the restarted one really restarts; the
  non-interactive rule - a script whose requestor is an agent sends no user notification, one whose
  requestor is a GUI does.
* **Tier 1 or 2**: a plan whose first step is a `[dirigent.command]` mark and whose second app depends
  on it - the second app does not start until the script has finished; with `exitcode 0` a failed
  script holds the plan; with `exitcode 0,1` it does not.
* Tier 1 cannot cover the `Dirigent.CLI.exe` route (no CLI process in an in-process bed); tier 2 can.

## Rejected alternatives

* **The CLI client does the waiting** (revision 1): a client-side `RunScript` polling
  `GetScriptState`. Dropped - it puts the waiting in one client only, invents its own exit-code
  vocabulary, and rests on the false premise that a master-side wait must block the tick. It would
  also not have helped the plan route, which uses no CLI exe.
* **A master-side command that blocks its tick**: never - it would stop apps, plans and everything
  else for the duration.
* **A marked acceptance line** (`ACK WAIT`) so a reader could tell a provisional ACK from a final one:
  rejected - it extends the protocol. The command class knows its own terminator; the sender can ask.
* **A per-request completion marker** emitted when a request finishes: same objection, and more
  machinery.
* **A GUI check inside the script** (`GetClientState( Requestor )?.Ident?.IsGui`) to keep a
  plan-started script quiet: unnecessary - nothing renders a notification for a non-GUI requestor, so
  the check would guard against nothing. Were it ever wanted explicitly, the single place for it is
  the master's forwarding, not every script.
* **A timeout on the plan step's answer**: not worth it - messages are not lost, and a master that
  never answers is dead, which ends the plan anyway.
* **A richer set of synthesized exit codes**: an unreachable master is a dead master, so there is
  nothing to distinguish.
* **A new init detector watching a script instance** (`InitCondition="scriptfinished <guid>"`):
  possible, but it puts knowledge of scripts into an app watcher on every agent and solves only the
  script case. Piece 4 solves every command.
* **A script as a first-class plan step** - reviving `PlanScriptDef` (declared in
  `Common/PlanScriptDef.cs`, never used; `AppScript.cs` is an empty TODO of the same idea): the clean
  long-term shape, and much bigger - plan sequencing, a state for a step that is not a process,
  killing, adoption, plan status, the GUI. This design does not stand in its way.
* **The master's `--startupScript`**: fires when the master starts, not when a plan is started.

## Settled in review

* The plan gate needs nothing: `Dependencies` + `InitCondition="exitcode …"` is the whole mechanism.
* A failed step fails the plan; the mark step is made non-critical through its `InitCondition`, not
  through a switch on the command.
* A script started from a plan or the CLI shows nothing on any UI - structurally, with no check
  anywhere: only the WinForms GUI renders notifications, and only those addressed to itself.
* The waiting belongs on the master, not in a client.
* `WaitForScript` alone; no `RunScript` shorthand. `StartScript` stays as it is and the two compose.
* One answer at the end, no streamed progress: `ACK` on acceptance, `END` on completion.
* Which terminator to expect is an **attribute on the command class**, not a table kept elsewhere and
  not a marker in the response.
* No timeout on the plan step's answer, and two exit codes only: a master that does not answer is
  dead.
* `Entry.Dispose` is scoped back to the entry's own script rather than deleted - which is what it
  meant before the 2022 refactor widened it.
* The `ExitCodeInitDetector` range expansion gets a documented warning, not a change.
