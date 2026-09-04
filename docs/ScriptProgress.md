# Progress and cancellation for long operations

**Implemented.** This is the record of what was built and why, kept because most of it is reasoning
that the reference documentation has no room for. For using it, see
[Publishing status and progress](Scripts.md#publishing-status-and-progress) and
[Cancellation](Scripts.md#cancellation) for what a script publishes and what it must do to be
interruptible, and [what the operator sees](Files.md#what-the-operator-sees-while-it-runs) for the
phases of the one operation this was built for.

What differed from the design as written is under
[What the building changed](#what-the-building-changed).

## The problem it was built for

Collecting logs from several machines takes from seconds to many minutes, and while it ran Dirigent
showed nothing. `BuiltIns/DownloadZipped.cs` sent a one-second balloon at the start (*"Downloading
from 3 machine(s)..."*) and a message box at the end. In between there was no indication that
anything was happening, no idea how far along it was, and no way to stop it.

That mattered most exactly where it hurts: a folder holding an unrotated multi-gigabyte log takes
minutes to compress, which is indistinguishable from a hang.

## Scope

Agreed before building, and still the boundary:

* Only operations **started from the GUI** show progress.
* Progress appears **only in the GUI that started it**, not in others.
* No tray-icon tooltip.
* Cancelling **removes the partial archive**.

Everything else - scripts started from the CLI, the REST surface, or by another script - keeps
behaving as it does today. They are not silent, they simply report to whoever asked for them.

## What already existed

| Piece | Where | Note |
| --- | --- | --- |
| `SetStatus( text, data )` | `Script` | any script can publish a status at any time |
| `ScriptState { Status, Text, Data }` | `ScriptState.cs` | no number in it, `Data` is script-specific JSON |
| `ScriptStateMessage` | `Messages.cs` | **broadcast to every subscribed client**, keyed by instance guid |
| cached states per client | `ReflectedScriptRegistry` | `GetScriptState(guid)` on the GUI side |
| the instance guid, at start | `RunScriptNoWait` returns it | the starter knows what it started |
| `GetScriptStateAsync(guid)` | `IDirigAsync` | a script can poll another script |
| `KillScriptMessage` | master -> agent -> `ScriptRunner` | cancels the runner's `CancellationTokenSource` |
| `EScriptStatus.Cancelling` | `ScriptState.cs` | a state to display between the click and the stop |

So the transport, the caching and the kill path were all in place already. What was missing was a
number, an aggregation rule, three cancellation checks, and the widget - which is why this was a
cheap feature rather than a protocol change.

## Which script the GUI shows

**The GUI shows the scripts it started itself.** No new flag.

`RunScriptNoWait` already returns the instance guid; the GUI keeps those guids in a set and drops
each one when its state stops being alive. The nesting question answers itself: the download's
slave scripts are started by `DownloadZipped`, not by the GUI, so a GUI never has their guids and
never shows them.

A *"top level"* flag on the script would say *this is worth watching* but not *by whom*, so with
two GUIs open both would show a bar for a download only one of them asked for. If tracking ever
needs to survive a GUI restart, the fix is not a flag either: it is to carry `Requestor` (which
`StartScriptMessage` already has) into `ScriptState`, and let each client show the alive scripts
whose requestor is itself. Not needed for the agreed scope.

One small change is required: `ToolsRegistry.StartScript` currently discards the guid that
`RunScriptNoWait` returns. `StartAction` / `StartScript` / `StartFileBoundAction` /
`StartFilePackageBoundAction` need to return it so `MenuBuilder` can hand it to the status bar.

```mermaid
flowchart LR
    subgraph GUI["GUI (the one that clicked)"]
        M[menu item] -->|StartScript| TR[ToolsRegistry]
        TR -->|returns guid| SB[status bar slot]
        RSR[(cached script states)] -->|every tick| SB
    end
    subgraph Master
        MA[master]
    end
    subgraph Agents
        P["DownloadZipped<br/>(parent)"]
        S1["slave on m1"]
        S2["slave on m2"]
    end
    TR -->|StartScriptMessage| MA --> P
    P -->|RunScriptAsync| S1
    P -->|RunScriptAsync| S2
    S1 -->|"SetStatus(progress)"| MA
    S2 -->|"SetStatus(progress)"| MA
    P -->|"SetStatus(aggregated)"| MA
    MA -->|ScriptStateMessage broadcast| RSR
```

## The progress number

A nullable `double` on `ScriptState`, and an optional third parameter on `SetStatus` - the API is in
[Publishing status and progress](Scripts.md#publishing-status-and-progress). Two choices in it are
worth the words:

**Nullable rather than 0.** "Running, no idea how far" is a real state and a common one, and a
number is a claim. Given a null the GUI sweeps instead of drawing a bar, which is the difference
between an operation that looks busy and one that looks hung - see
[What the building changed](#what-the-building-changed), where publishing `0.0` for an unmeasurable
phase turned out to be exactly the bug it was meant to avoid.

**On `ScriptState`, not inside `Data`.** `Data` is script-specific JSON. Putting the number there
would mean the status bar had to understand a particular script to draw a bar for it.

Wire-safe in both directions: MessagePack is contractless here, so members travel by name and a
peer that does not know the field ignores it.

## Aggregation

In the script, not in the framework. The script is the only thing that knows what "half done" means
for its own work, and everything needed was already there: `DownloadZipped` holds every slave's guid
in `SlaveTask.scriptId`, and `GetScriptStateAsync(guid)` lets it ask. So no parent/child links were
added to the protocol and the GUI walks no trees.

Within the collecting phase the parent weighs each machine by the bytes that machine announced - one
holding 60 GB must not count the same as one holding 2 MB - and a machine that has not reported yet
counts as zero. A slave reports every ~4 MB rather than per file, because a single 60 GB file must
not sit at one number for minutes.

The phases and their shares are in
[what the operator sees](Files.md#what-the-operator-sees-while-it-runs).

## Cancellation

The kill path existed end to end before this, but **none of the three download scripts observed its
`CancellationToken`** - and `ScriptRunner` explicitly lets a script that ignores the token run on. A
cancel button would therefore have removed the bar while the machines carried on working, and a zip
would have appeared minutes later. What an interruptible script has to do is in
[Cancellation](Scripts.md#cancellation); the three that had to change:

1. **`DownloadZippedSlave`** - a chunked copy that checks the token, so a cancel takes effect inside
   a huge file rather than after it. Its `.part` file is removed by the existing failure path, which
   is also what satisfies *partial gone*: the archive under construction never had its final name.
2. **`DownloadZipped`** - kill every slave and the merge, then remove the staging folder. Killing
   only the parent leaves the slaves compressing.
3. **`MergeZipped`** - the same check in its entry loop, and the half-written destination deleted.
   It is the one that writes under the final name.

A cancelled download leaves no archive, no `.part` and no staging folder.

## The user interface

### Where it goes

`Main.Designer.cs` had a `statusStrip` holding a single `toolStripStatusLabel1`, which
`refreshStatusBar()` sets to `Connected.` / `Disconnected.` on every tick. The operation slots share
that strip.

A `StatusStrip` is a single row, so several operations cannot be stacked without a second strip.
They go **side by side**, each as a slot of three items:

```
+--------------------------------------------------------------------------------+
| Connected.   Collecting logs  [######----]  X   Incident report [##--------] X  |
+--------------------------------------------------------------------------------+
```

| Item | Type | Size |
| --- | --- | --- |
| title | `ToolStripStatusLabel` | auto, `AutoToolTip` carrying the full status text |
| bar | `ToolStripProgressBar` | 100 px, `Continuous`, or `Marquee` while `Progress` is null |
| cancel | `ToolStripButton` | 20 px, `DisplayStyle = Image`, a red cross, `ToolTipText = "Cancel"` |

At roughly 250 px per slot, two fit next to the connection label on the default 889 px window.
**Two slots are shown**; beyond that a single `+N more` label appears, its tooltip listing the
titles. Concurrency above two is rare enough not to design further around.

### Lifecycle

The items are created and removed at run time, not in the designer, since the count varies. A
small private class holds them together:

```csharp
class OperationSlot
{
    public Guid Instance;
    public ToolStripStatusLabel Title;
    public ToolStripProgressBar Bar;
    public ToolStripButton Cancel;
    public bool Cancelling;
}
```

`Main` keeps `Dictionary<Guid, OperationSlot> _operations`, filled when a menu action returns a
guid. On every tick, inside the existing `refreshStatusBar()`:

* read `_core.ReflStates.GetScriptState( guid )` for each tracked guid;
* `Status == Running` / `Cancelling` -> update the title (`Title` + `Text`) and the bar
  (`Progress * 100`, or marquee if null);
* `Finished` or `Cancelled`, or a state that has disappeared -> remove the slot;
* `Failed` -> keep the slot, colour it red, show the failure text, and turn its cross into a
  dismiss button. It stays until clicked away, so an operation cannot fail unnoticed while the user
  is looking elsewhere;
* create the items for a newly tracked guid, dispose them on removal.

The tick is the GUI's existing timer (`TickPeriod`, 500 ms by default), which also pumps the
client - so `ScriptStateMessage` arrives on the UI thread and no marshalling is needed anywhere.
500 ms is coarse for animation and entirely adequate for a bar that moves over minutes.

### The cancel button

On click: send `KillScriptMessage( Ctrl.Name, instance )`, set the slot to `Cancelling...`, put the
bar into marquee, and disable the button so it cannot be pressed twice. The slot disappears when
the state stops being alive. No confirmation dialog - the operation is interruptible by design and
leaves nothing behind.

### What stays

The start balloon goes (the bar says the same thing, better) and the closing message box stays,
including its list of errors.

## Testing

Tier 1 covers everything except the widget:

* progress rises and reaches 1.0 for a download; the operator already records script states.
* a slave reports a total and partial progress for a large file.
* cancelling mid-download: the script ends `Cancelled`, no archive, no `.part`, no staging folder
  is left, and the collecting processes stop rather than finishing in the background.
* the existing download tests must stay green - progress reporting must not change what lands.

The status bar itself is manual: run `Invoke-DirigentTests.ps1 -KeepAlive -WithGui`, download the
seeded logging world, watch the bar, press the cross.

## Decided during review

1. **Polling stays at the existing 500 ms tick**, for the parent asking its slaves as well as for
   the GUI refreshing the bar. Revisit only if a system with dozens of machines feels it.
2. **A failed operation stays in the bar until it is clicked away.** The slot turns red, keeps the
   failure text, and its cross becomes a dismiss button rather than a cancel. `Finished` and
   `Cancelled` slots disappear on their own - only a failure demands acknowledgement.
3. **Scripts only.** `ReloadSharedConfig`, plan starts and `KillAll` keep their current silence;
   the mechanism is generic enough to cover them later, with no more than their own `SetStatus`
   calls.

## What the building changed

Three things the design did not foresee, each of which had made the feature silently useless:

* **`ReflectedScriptRegistry` copies the script state field by field.** A new field on `ScriptState`
  travels over the wire but is dropped where every client caches it, so nothing ever saw a
  `Progress`. Anything added to `ScriptState` has to be added there too.
* **`await Task.WhenAny(...)` hands back the cancelled task rather than throwing.** The parent's
  wait therefore ignored its own cancellation and carried on to the end. The token has to be looked
  at explicitly after the wait.
* **Cleaning up while the slaves are still dying does not work.** Deleting the staging folder under
  a slave that is still writing fails, and the slave then recreates it for its own cleanup - leaving
  an empty folder behind. The parent now waits for the slaves to stop (bounded, 10 s) before taking
  the folder away.

Two smaller decisions:

* `ScriptRunner` sets `Progress = 1.0` on a finished script, whatever it last reported, so an
  indicator ends full rather than frozen wherever it got to.
* The harness seeder can write **incompressible** files. Its usual filler compresses at gigabytes
  per second, so a world built from it finishes before there is anything to watch or interrupt.

## One indicator that always says something

Reported from the field once this was running: *"a status bar for a short time going from 0 to 100,
then another status bar a long time staying at zero and then quickly running to 100"*. Both were the
**same** operation - the GUI only ever creates one indicator per click - shown in two different ways.

The cause was the unmeasurable phase publishing `0.0` instead of null. A bar frozen at zero is
indistinguishable from a hang, and it looked like a *second* indicator because the change from a
sweeping bar to a filled one changes the widget's appearance. The nullable progress had been
designed for exactly this case and the script simply had not used it.

So the rule the phases now follow: a phase that can measure itself publishes a number, a phase that
cannot publishes its name, and the label carries the phase whenever there is no number. The states
are listed in [what the operator sees](Files.md#what-the-operator-sees-while-it-runs).

**Visible is not the same as quick.** The lookup was honest about being unmeasurable but was also
genuinely slow - one remote round trip per node, in sequence, which on a site of forty machines took
longer than the download it preceded. That was a separate fix: see *Looking the files up* in
[Files.md](Files.md#looking-the-files-up). It is now one round trip per machine, all machines at
once, and the phase is short enough that a sweep is all it needs.

## What is covered

`ScriptProgressTests`, tier 1: progress rises without going backwards and ends at 1.0; each machine
announces a total it never exceeds; a cancelled download ends as `Cancelled` and leaves no archive,
no `.part` and no staging folder - checked over an observation window, since a machine that ignored
the cancel would deliver its archive a moment later.

The status bar itself has no automated cover: tier 1 has no GUI. Run
`Invoke-DirigentTests.ps1 -KeepAlive -WithGui`, download the seeded logging world from the menu,
and watch the bar - that is the one part that needs eyes.
