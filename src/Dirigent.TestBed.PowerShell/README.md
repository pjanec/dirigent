# Tier 2: real processes, driven from PowerShell

Tier 1 (`src/Dirigent.TestBed`) runs a master, agents and an operator inside one process — fast, and
where most tests belong. Tier 2 starts the **shipped executables** on this machine and talks to them
over the command-line interface, which is the only way to see the things tier 1 cannot: that the
hosting model starts up at all, that an agent finds its configuration, that a killed agent adopts
its applications when it comes back, and that the remote-control surfaces answer.

Windows PowerShell 5.1. Nothing to install.

## Running

```powershell
# everything
.\Invoke-DirigentTests.ps1

# one thing, by name
.\Invoke-DirigentTests.ps1 -Filter download

# build first
.\Invoke-DirigentTests.ps1 -Build
```

About 30 seconds for the eight tests, each in its own world on its own ports.

## A world to poke at by hand

This replaces the `run_m1_gui_master.bat` / `run_m2_con.bat` pair, and gives you a *curated* world
rather than whatever is in `config\SharedConfig.xml`:

```powershell
.\Invoke-DirigentTests.ps1 -KeepAlive -WithGui -WithHttp
```

It generates the world, starts the master as a tray GUI plus an agent per further machine, prints
what it made, and binds it to `$w`:

```
Dirigent tier-2 world 'LoggingWorld'
  folder        C:\...\Temp\DirigentTier2\2e01d6
  master port   52579
  command port  52580
  web server    http://127.0.0.1:52581/
  machines      m1, m2
  applications  m1.camera, m1.tracker, m2.recorder
  file nodes    log, logs.all
```

Then drive it:

```powershell
Invoke-DirigentCli -World $w -Command 'GetAllAppsState' -List
Invoke-DirigentCli -World $w -Command 'StartApp m1.camera'
Wait-DirigentAppState -World $w -App 'm1.camera' -Flags 'R'

Invoke-DirigentScript -World $w -Script 'BuiltIns/DownloadZipped.cs' `
    -Arguments '{"Node":{"Id":"logs.all"}}'

Stop-DirigentWorld -World $w      # kills everything and removes the folder
```

`Stop-DirigentWorld` is not optional housekeeping: Dirigent deliberately leaves managed applications
running when an agent goes away, so without it the applications stay.

## Where the worlds come from

Nowhere in PowerShell. The worlds are the same `Scenario` presets tier 1 uses, in C#;
`Dirigent.TestBed.Gen` renders one to a folder:

```
Dirigent.TestBed.Gen.exe --list
Dirigent.TestBed.Gen.exe --scenario LoggingWorld --out C:\temp\myworld
```

It writes `SharedConfig.xml`, a `LocalConfig.xml`, the application folders with their back-dated
seed files, and a `world.json` manifest naming the machines, the applications, their folders and the
file-node ids. Everything here works from that manifest, so a change to a scenario reaches tier 1,
tier 2 and (later) tier 3 at once.

## The verbs

| Verb | What it does |
| --- | --- |
| `Start-DirigentWorld` | generates a world, allocates free ports, starts the processes, returns when every agent is connected |
| `Stop-DirigentWorld` | `KillAll`, then the agents, then anything they left running, then the folder |
| `Invoke-DirigentCli` | one text command; `-List` for the ones that answer with a list ending in `END` |
| `Invoke-DirigentScript` | `StartScript` with JSON arguments, poll `GetScriptState`, return the parsed result |
| `Wait-DirigentCondition` | poll a condition until a deadline, and say what it was waiting for when it never came |
| `Wait-DirigentAppState` | the common case of the above: wait for an application's state flags |
| `Get-DirigentWorldSummary` | what to print for a human |
| `Get-DirigentWorldLog` | the tail of every agent log, for a failure that needs explaining |

`Test-Case`, `Expect-True`, `Expect-Equal` and `Expect-Match` are a runner small enough to need no
module installed. If Pester is ever wanted, the specs translate directly.

## Rules

**Nothing waits for a fixed time.** There is no virtual time in Dirigent, so a sleep is a guess that
fails on a loaded machine. `Wait-DirigentCondition` polls a condition and reports what it wanted.

**Every world is isolated.** Free ports per world, its own `SharedConfig.xml` and `LocalConfig.xml`,
its own agent-status and download folders. Two runs, or a run and the real Dirigent on this machine,
cannot interfere.

**Applications start minimized**, as everywhere in the harness, so a run does not interrupt whoever
is at the keyboard. `-Visible` shows the agent consoles when you are debugging.

**Keep tier 2 small.** Anything that can be shown in-process belongs at tier 1, where it costs a
second instead of four. The eight tests here are deliberately about hosting, recovery, the two
remote-control surfaces, and one end-to-end download.

## Things tier 2 found that tier 1 could not

- **An agent dies if `LocalConfig.xml` is missing.** The setting defaults to `LocalConfig.xml`
  resolved against the working directory, and the file being absent takes the process down at
  startup. A real deployment always has one, so the generator writes one.
- **`--httpPort 0` does not disable the web server** — it falls back to 8877, which every world (and
  a real installation) would then fight over. `-1` is the switch that turns it off.
- **A download requested by anything that is not an agent or a GUI** used to resolve its destination
  to the literal `%DOWNLOADS%`: the requestor lookup fell back to the client name, which is empty on
  the master, and an empty machine id means "global" to the resolver, which returns the path
  unexpanded. Now the master falls back to its own machine, and `ToMachine` in the arguments lets a
  caller name the machine outright.
