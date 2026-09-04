# Running a script as a step of a plan

**Implemented.** This is the record of what was built and why, kept because most of it is reasoning
that the reference documentation has no room for. For using it, see
[`cliresponse`](SharedConfig.md#cliresponse---waiting-for-a-dirigent-command),
[`WaitForScript`](CLI.md#waitforscript) and
[A step that waits for a Dirigent command](Plans.md#a-step-that-waits-for-a-dirigent-command).

What was built, in the order the commits went in:

1. a characterisation suite pinning what the master answers to every text command, written first, so
   that the rest could not change it unnoticed;
2. `ICommand.Finished`, so a command can outlive one master tick without blocking anything;
3. `WaitForScript <guid> [timeout=]`, and `[CliResponse(Terminator=...)]` declaring how each command's
   answer ends;
4. `Entry.Dispose` scoped back to its own script, which fixed a bug that let one restarted script
   cancel every other script on the master;
5. the `[dirigent.command]` response tracker and the `cliresponse ok|any` init condition;
6. the one-shot `Dirigent.CLI.exe` reading to the terminator its command declares.

The design went through four revisions in review, and the ones that were turned down are as much a
part of the record as the one that was built - see [Rejected alternatives](#rejected-alternatives).
The fourth revision is what follows: every piece is additive, and a step opts in through its
`InitCondition`, so nothing existing changes at all.

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
* [Compatibility, and how it is guaranteed](#compatibility-and-how-it-is-guaranteed)
* [What would be tested](#what-would-be-tested)
* [Rejected alternatives](#rejected-alternatives)
* [Settled in review](#settled-in-review)

## What already exists

**The plan gate is complete, and it reads only `Initialized`.** An app of a plan is launched once
every app named in its `Dependencies` is `Initialized` (`AppLaunchPlanner.AreAllDepsSatisfied`,
`AppLaunchPlanner.cs:133`) - the only gate there is. `Initialized` is set **true at launch**
(`LocalApp.AfterLaunch:201`) and only an init detector pulls it back to false. `CalculatePlanStatus`
(`Plan.cs:310`) does not require a *volatile* app to be still running. So a step can exit immediately
and still hold the plan, as long as something keeps its `Initialized` false.

**Init detectors combine as OR.** Each one sets `Initialized = true` on its own when satisfied
(`ExitCodeInitDetector.cs:120`, `TimeOutInitDetector.cs:77`, `WindowPoppedUpInitDetector.cs:138`), so
the first to fire wins. The three that exist are `exitcode`, `timeout` and `windowpoppedup`.

**A plan can already issue a Dirigent command with no external process.** `ExeFullPath` has reserved
names (`Launcher.ParseExe`, `Launcher.cs:271`): `[cmd]`, `[cmd.file]`, `[powershell]`,
`[powershell.file]`, `[powershell.command]`, and **`[dirigent.command]`**, which sends the app's
`CmdLineArgs` to the master as a CLI command (`LaunchDirigentCmd`, `Launcher.cs:500`).

**The answer to that command already comes back.** The master answers a `CLIRequestMessage` with a
`CLIResponseMessage` addressed to the requesting client (`Master.CLIClient.WriteResponse`,
`Master.cs:608`), and the CLI protocol carries an optional `[reqId]` which the master echoes in every
answer line (`CLIRequest.WriteResponseLine`).

**The master does not have to block to wait.** `CLIProcessor.Tick` keeps `pendingRequests` across
ticks and removes a request only when it reports `Finished`, spending at most 20 ms per tick
(`CLIProcessor.cs:146`). A request that reports `!Finished` stays and is ticked again. This machinery
is original - the first version of `CLIProcessor.Tick` is already "tick all pending requests / remove
finished requests" - and `CLIRequest.Tick` is `virtual`.

**Scripts already report enough to wait on.** `GetScriptState` gives `Status`, `Text`, `Progress` and
`Data`; a singleton script's record is kept for as long as the master lives
(`SingletonScriptRegistry.cs:46`), so its final state is readable after it ends.

**The response protocol already has three shapes**, and `ACK` does not mean what one might assume:

| shape | example | terminal line |
| --- | --- | --- |
| simple command | `StartPlan`, `StartScript`, `KillScript` | `ACK` |
| listing | `GetAllAppsState`, `GetAllPlansState`, `GetAllClientsState` - lines, then `END`; **no ACK at all** | `END` |
| accepted, then more later | `SendEvents` - *"sends an immediate ACK to confirm support before performing longer operations"* (`TelnetServer.cs:248`) | none; the subscription just runs |

`CLI.md:641` is explicit: *"ACK does not mean that the command finished successfully! Only that it was
delivered and processed."* So a command answering `ACK` on acceptance and `END` on completion invents
nothing - it combines the second and third shapes that are already there.

## The four holes

**1. Nothing tells a plan step how its command went.** The response is sent, but **no client handles
`CLIResponseMessage`** - nothing in the codebase reads it. And a `[dirigent.command]` app has no
process, so `Launcher.checkExited()` reports "exited" the moment it is asked (`_proc == null`,
`Launcher.cs:723`) with `ExitCode` still at the 0 it was set to at launch (`Launcher.cs:340`) - so
`InitCondition="exitcode 0"` on such a step initializes immediately, whatever became of the command.

**2. No command answers "the script has finished".** `StartScript` answers `ACK` as soon as the script
has been *started* (`DirigentControlCommands.cs:467`); `GetScriptState` answers the state once.
Nothing waits.

**3. A command cannot say "not yet".** The request-level machinery is there and original, but
`ICommand.Execute()` returns `void` and `CLIRequest.Tick` runs every command in one pass and then sets
`Finished = true` (`CLIRequest.cs:105-120`). A waiting command would have to block inside `Execute()`,
which would stop the master's tick - every request, app and plan with it.

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

**Why that line exists.** A refactoring slip, and the history says so. Before `47a11a5` ("Scripts
refactored. Tasks discontinued in favour of running plain remote scripts.", Dec 2022) the predecessor
of `Entry` was `ScriptEntry`, which **owned its script instance** - `_script`, `_runTask`, `_runCTS`
were its own fields and its `Dispose()` disposed *that one* script. The refactor moved instances into
a shared `LocalScriptRegistry` and translated `_script.Dispose()` as `_localScriptRegistry.Dispose()`,
silently widening "end my script" into "end every script". So the line is not to be deleted but
**scoped back**.

**A second trap on the same path.** `LocalScriptRegistry.Start` declines a start while that instance
is alive (`LocalScriptRegistry.cs:94`), and `Stop` only *initiates* cancellation - the status passes
through `Cancelling` before `Cancelled`. So replacing a still-running instance stops the old script
and then silently declines to start the new one, today as much as after the fix.

## The design

Six pieces, each small, none of them changing anything that exists today.

### 1. A CLI command may take more than one tick

`ICommand` gains `bool Finished { get; }`, defaulted to `true` in `DirigentControlCommand` so every
existing command is unaffected, and `CLIRequest.Tick` leaves an unfinished command at the head of its
queue instead of dequeuing it, keeping the request's own `Finished` false. `CLIProcessor` already does
the rest. It is the command-level counterpart of the flag the request has had from the beginning.

### 2. `WaitForScript` - a master-side command that finishes when the script does

```
WaitForScript <guid> [timeout=<seconds>]
```

* `ACK` at once - the instance exists and the wait has begun (the documented meaning of ACK:
  delivered and processed);
* then nothing until it is over - no progress lines; whoever wants progress asks `GetScriptState`;
* `END` when the script ends up `Finished`;
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

A command declares how its response ends, so that a sender knows what to wait for without parsing
anything:

```csharp
[CliResponse( Terminator = ETerminator.End )]
public class WaitForScript : DirigentControlCommand { ... }
```

`ETerminator.Ack` is the default, so nothing has to be written on the twenty commands that answer
`ACK`. The three listings get `End` - which is what they already do; today a client waits for their
`END` only because no `ACK` is ever sent, and this makes it true by design.
`CommandRepository.Register` takes the command type so it can read the attribute and answer
`TerminatorOf( commandName )` without instantiating anything: one source of truth, in the class that
implements the behaviour.

### 4. The outcome reaches the plan through a new init condition

The exit code of a `[dirigent.command]` app is left alone - it defines none, and inventing one would
change the meaning of every step that exists today. Instead the outcome is what a **new init
detector** waits for.

**Collecting the outcome** (agent side):

* the launcher tags the request with a fresh id - `[<reqid>] <command>` - which the master already
  echoes back; this is the only change to that path and it is invisible, since nobody reads those
  lines today;
* the agent handles `CLIResponseMessage` and hands the lines whose id matches to the launcher that
  sent them;
* the launcher settles on `Pending → Ok | Error`:
  * an `ERROR` line settles it as **Error** at once - which also covers the case of a request that
    failed to parse, where the master answers one `ERROR` for the whole line rather than one terminal
    line per command;
  * otherwise it waits for **one terminal line per command it sent**, in order, each being the
    terminator that command's class declares, and settles as **Ok**;
* the outcome lives on the launcher rather than on the detector, so an answer that arrives before the
  detector's first tick is not missed.

**`InitCondition="cliresponse <ok|any>"`** - the value is mandatory, there is no default:

| form | initialized when |
| --- | --- |
| `cliresponse ok` | the answer arrived and **every** command in the line succeeded. A failure leaves the step uninitialized, so its dependents wait and the plan reports Failure - the "this step must not be skipped" case. |
| `cliresponse any` | the answer arrived, whatever it said. The command is waited for, its failure is logged, and the plan carries on - **this is the one for the mark step**. |

*All* must succeed for `ok`, because a line is one step: a step that half worked has not done what it
was put in the plan to do. Note that a failing command does not stop the ones after it - `CLIRequest`
catches per command and carries on - so the later terminal lines still arrive, and the outcome is
`Error` either way.

The detector carries `Flags = ClearOnLaunch`, like the other two, so a re-run waits for a fresh
answer.

**Used on an app that is not `[dirigent.command]`**, `cliresponse` can never be satisfied, so it is
treated as a configuration error rather than a hang: the **shared config fails to load**, naming the
app. The same for a missing or unknown value. That is safe to do loudly - on startup a bad config
already aborts, and on `ReloadSharedConfig` the throw becomes an `ERROR` reply to the requestor while
the previous config stays in effect - and it cannot break any existing config, because nothing can be
using a condition that does not exist yet.

**If the answer never comes** - a dead master, which ends the plan anyway - the OR semantics of the
detectors give a ceiling for anyone who wants one:

```xml
<InitDetectors>
    <cliresponse>any</cliresponse>
    <timeout>60</timeout>
</InitDetectors>
```

Initialized as soon as the answer arrives, or after 60 s at the latest. The element form is needed
because `InitCondition` carries only one detector.

### 5. `Entry.Dispose()` scoped back to its own script

* `Entry.Dispose()` → `_localScriptRegistry.Stop( Def.Guid )`: end *this* entry's script, which is
  what it meant before the 2022 refactor - not every script on the master;
* and the replace path force-removes that one instance before starting the new one, so that restarting
  a running singleton script actually starts it instead of being declined while the old one is still
  cancelling. That needs a scoped `Remove( Guid )` on `LocalScriptRegistry` - the private one it
  already uses for housekeeping, made available to its owner.

### 6. The one-shot CLI honours the same attribute

`Dirigent.CLI.exe "<command>"` stops at the first line starting with `ACK`, `END` or `ERROR` and gives
up after 5 s of silence (`CliApp.NonInteractiveSubCmd`). With the attribute it knows better: for an
`End`-terminated command it reads past the `ACK` and waits without that limit. So a batch file can
wait for a script too, and the client stops conflating "acknowledged" with "finished" - which the docs
say are different things. For every command that exists today the terminator is the one the client
already waits for.

## What it looks like in the config

```xml
<Plan Name="System Start">

    <!-- draw the line under the logs before anything starts writing; no process involved -->
    <App AppIdTuple = "master.mark_logs"
         ExeFullPath = "[dirigent.command]"
         CmdLineArgs = "StartScript 7B3C1E90-1111-2222-3333-444455556666 BuiltIns/MarkFiles.cs '{Node:{Id:''pkg.run''}}' ; WaitForScript 7B3C1E90-1111-2222-3333-444455556666 timeout=300"
         Volatile = "1"
         InitCondition = "cliresponse any"
    />

    <App AppIdTuple = "m1.camera"   Dependencies = "master.mark_logs" ... />
    <App AppIdTuple = "m2.recorder" Dependencies = "master.mark_logs" ... />

</Plan>
```

* A fixed guid in the config, which is how singleton scripts are already identified there.
* **The quoting is load-bearing and passes two levels.** The XML attribute is delimited by double
  quotes, so the argument must not contain any; the CLI word tokenizer then eats a single quote it
  reads as a delimiter, so `'{Id:'x'}'` would reach the script as `{Id:x}` and fail to deserialize.
  Doubling the inner ones - `'{Node:{Id:''pkg.run''}}'` - is what delivers the JSON intact and needs
  no XML entity. A `;` inside an argument is not possible at all: both sides split the line on it.
  `ConfigExampleTests` pins this against the working copy in `config/SharedConfig.xml`.
* One step covers the whole system: `MarkFiles` marks every machine holding files of the package, in
  parallel, by itself.
* `Volatile="1"`, because the step is meant to end - see
  [Utility plans](Plans.md#utility-plans-vs-standard-plans).
* `cliresponse any` because a mark that could not be taken must not stop the system from starting; a
  step that must succeed uses `cliresponse ok`.
* The same shape gives a plan `ClearFiles`, `UnmarkFiles`, or any other Dirigent command.

## Failure: what fails the plan, and what must not

The rule stands as it is (`Plan.CalculatePlanStatus`, `Plan.cs:310`): a plan is a *Success* when every
app has initialized and every non-volatile one is still running, so **one step that fails to
initialize fails the plan**. That is what a step should do when it matters.

Marking the logs does not matter that much: a missing mark degrades a later collection - it will hold
more than one run - but it must not stop the system from starting. Hence the two forms of the
condition, `cliresponse ok` and `cliresponse any`: which one a step carries is a decision about the
site, and it belongs in the config rather than in the command.

Unrelated but worth documenting where `InitCondition` is described: `ExitCodeInitDetector` expands a
range into one entry per value (`ExitCodeInitDetector.cs:46`), so `exitcode 0-255` is 256 entries and
fine, while `exitcode 0-2147483647` would try to build two billion.

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
flag in the arguments. A tier-1 test asserts it rather than leaving it to reading - the bed's operator
already collects the notifications it receives.

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

## Compatibility, and how it is guaranteed

The CLI, the plans and `[dirigent.command]` are in production and must not change behaviour. After
this revision, **nothing existing does**: every piece is additive and a step opts in through its
`InitCondition`.

* `[dirigent.command]` keeps its behaviour bit for bit - it still exits at once with code 0, and the
  exit code is not redefined. The only difference is that the request now carries a request id, which
  changes nothing observable: the master already echoes ids, and nothing reads those lines today.
* `cliresponse` is a **new** condition, so no existing config can use it - which is also why failing
  the config load on a misuse of it is safe.
* Config attributes are read selectively and unknown ones ignored (`SharedConfigReader`), and there is
  no XSD anywhere, so a config carrying `cliresponse` still loads on an older Dirigent - it simply
  does not wait. The config can be rolled out before the binaries.
* `ICommand.Finished` is defaulted, `CommandRepository.Register` gains the command type, and no
  existing command declares a terminator other than the one it already writes.
* `WaitForScript` against an **older master** degrades gracefully: the parse throws
  `UnknownCommandException`, the request catches it and answers `ERROR: Unknown command …`
  (`CLIRequest.cs:60`), so a `cliresponse any` step carries on and a `cliresponse ok` step fails
  loudly. Deploy the master first.

**The guarantee is a characterisation suite written before any of this** - and it is cheap, because
the request pipeline can be driven in-process the way the REST controller drives it:
`Master.AddCliRequest( captureClient, "<command line>" )` with an `ICLIClient` that collects the
lines. Committed first, it pins today's behaviour; only then does anything change.

| change | what could break | what pins it |
| --- | --- | --- |
| `ICommand.Finished` | nothing - no existing command overrides it | a test asserting every registered command reports finished after one `Execute` |
| `CLIRequest.Tick` peeks instead of dequeuing | order of commands, `ERROR`-and-continue on an exception, disposal, one-tick completion | the characterisation suite, plus an explicit test that an exception in the middle command still writes `ERROR` and the remaining commands still run |
| `[CliResponse]` + `Register<T>` | a mistyped registration | a golden list of *command name → declared terminator*, so any drift needs a deliberate edit |
| `WaitForScript` (new) | a new client against an old master | a test sending an unknown command, asserting the `ERROR` line and a finished request |
| request id on `[dirigent.command]` | a step that used to work | a test asserting such a step with no `cliresponse` still initializes immediately and exits 0 |
| `cliresponse` (new) | nothing existing; a misconfiguration | tests for `ok` / `any` / a failing command / a multi-command line where one fails / the config-load refusal on a non-`[dirigent.command]` app and on a missing value |
| `Entry.Dispose` scoped | something relying on a config reload killing scripts | tier-1: two scripts on the master, restart one by id, the other survives *and* the restarted one really restarts. Release-note line. |
| one-shot CLI reads to the declared terminator | existing batch files | tier-2: the real exe for `StartApp`, `GetAllAppsState` and an unknown command, asserting unchanged output and exit codes |

One harness addition is needed for several of these: the bed's `Master` is private
(`TestBed.cs:78`), so the `Operator` gains `SendCliCommandAsync( line )` returning the collected
response lines - useful well beyond this feature.

## What would be tested

Beyond the compatibility table above:

* **Tier 0**: `WaitForScript` mapping Finished / Failed / Cancelled / timeout / unknown onto `END` and
  `ERROR`; the launcher's outcome logic - one terminal line per command, first `ERROR` settling it,
  a parse failure answering once for the whole line.
* **Tier 1**: a plan whose first step is a `[dirigent.command]` mark with `cliresponse any` and whose
  second app depends on it - the second app does not start until the script has finished; the same
  with `cliresponse ok` and a failing script holds the plan; the non-interactive rule - a
  plan-started script produces no notification for any GUI.
* **Tier 2**: the one-shot CLI cases above, and a real `Dirigent.CLI.exe "StartScript … ;
  WaitForScript …"` returning only when the script has ended.
* Tier 1 cannot cover the `Dirigent.CLI.exe` route (no CLI process in an in-process bed); tier 2 can.

## Rejected alternatives

* **The CLI client does the waiting** (revision 1): a client-side `RunScript` polling
  `GetScriptState`. It puts the waiting in one client only, invents its own exit-code vocabulary, and
  rests on the false premise that a master-side wait must block the tick. It would also not have
  helped the plan route, which uses no CLI exe.
* **A master-side command that blocks its tick**: never - it would stop apps, plans and everything
  else for the duration.
* **Redefining a `[dirigent.command]` app's exit code** (revision 3): the one breaking change in the
  design, and unnecessary - the plan gate reads `Initialized`, so an init condition can do the same
  job additively.
* **A second reserved name** (`[dirigent.command.wait]`) or **a `CmdResult` attribute** on the app to
  ask for the waiting behaviour: both add a concept where `InitCondition` already is one, and the
  init condition says what it means at the place a reader is already looking.
* **A marked acceptance line** (`ACK WAIT`) so a reader could tell a provisional ACK from a final one:
  it extends the protocol. The command class knows its own terminator; the sender can ask.
* **A per-request completion marker**: same objection, and more machinery.
* **A deferred-waiter list on the master**, leaving `ICommand` untouched: the REST route
  (`CmdApiController.PostCliCmd`) awaits request completion to build its response, so the request must
  stay open until the wait ends - and the trick would also write responses through a disposed request.
* **A timeout on the plan step's answer**: messages are not lost, and a master that never answers is
  dead, which ends the plan anyway. Anyone wanting a ceiling combines `cliresponse` with `timeout`.
* **A GUI check inside the script** to keep a plan-started script quiet: nothing renders a
  notification for a non-GUI requestor, so the check would guard against nothing.
* **A new init detector watching a script instance** (`scriptfinished <guid>`): it would solve only
  the script case, while `cliresponse` covers every command a plan can send.
* **A script as a first-class plan step** - reviving `PlanScriptDef` (declared in
  `Common/PlanScriptDef.cs`, never used; `AppScript.cs` is an empty TODO of the same idea): the clean
  long-term shape, and much bigger - plan sequencing, a state for a step that is not a process,
  killing, adoption, plan status, the GUI. This design does not stand in its way.
* **The master's `--startupScript`**: fires when the master starts, not when a plan is started.

## Settled in review

* The plan gate needs nothing: `Dependencies` + an init condition is the whole mechanism.
* A failed step fails the plan; a step that must not block the plan says so in its init condition.
* The waiting belongs on the master, not in a client.
* `WaitForScript` alone; no `RunScript` shorthand. `StartScript` stays as it is and the two compose.
* One answer at the end, no streamed progress: `ACK` on acceptance, `END` on completion.
* Which terminator to expect is an **attribute on the command class**, not a table kept elsewhere and
  not a marker in the response.
* `ICommand` gains the `Finished` flag - the command-level counterpart of the request's own.
* The outcome of a `[dirigent.command]` step is gated by **`InitCondition="cliresponse ok|any"`**, the
  value mandatory, and no exit code is invented. All commands of a line must succeed for `ok`.
* `cliresponse` on an app that is not `[dirigent.command]`, or without a value, **fails the shared
  config load** - loud, and impossible for an existing config to trigger.
* No timeout on the answer; combine with `timeout` for a ceiling.
* A script started from a plan or the CLI shows nothing on any UI - structurally, with no check
  anywhere.
* `Entry.Dispose` is scoped back to the entry's own script rather than deleted.
* A characterisation suite of today's CLI behaviour is committed **before** any change.
