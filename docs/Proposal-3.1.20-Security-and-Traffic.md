# Dirigent 3.1.20 - security and network traffic (design)

*Status: design for review, branch `3.1.20`. Nothing here is built yet.*

3.1.20 changes the network protocol between the master and its clients (agents, GUIs). A site always runs one
version, so the change is made in one step, without a compatibility layer. Configuration files stay as they are:
with security off (the default) a 3.1.20 site runs from the same SharedConfig, LocalConfig and settings as 3.1.19.

Two goals:

1. **Traffic.** Idle traffic grows with the square of the number of machines; an 80-machine site used about 20 % of a
   1 Gbit network doing nothing.
2. **Security.** Whoever reaches the master's ports can start and kill programs, reboot machines and run any script
   (scripts may carry their C# source) on every machine. Close all three doors: the master port, the REST API and
   the telnet CLI.

## 1. What was measured

Counters per message type (3.1.19.29, `GET /api/status` -> `traffic`), on a 7-machine test site (14 clients: an agent
and a GUI per machine, 93 apps, GUIs minimised), idle, 300 s:

| sent by the master | B/s | per message | why |
|---|---|---|---|
| AppsState | 125 963 | 18 244 B | every app of the site, every second, to every GUI |
| ClientState | 55 912 | 289 B | every client's state, every second, to every client (agents too) |
| MachineState | 21 186 | 111 B | each agent's CPU/RAM, twice a second, forwarded to every client |
| PlansState | 10 709 | 1 551 B | every plan, every second, to every GUI |
| **total** | **213 770** | | |

All four grow with N^2 (N GUIs x N machines' content, or 2N clients x N reports). The simulation
(`TrafficMeasurementTests`) gives ~19 MB/s at 80 machines - the observed ~20 % of 1 Gbit. Encoding: MessagePack
*contractless*, which writes every field name as text in every object (~192 B per AppState); LZ4 compresses the
large messages ~5x but does nothing for the small ones (ClientState, MachineState), which are ~45 % of the total.

Fixed already on 3.1.19 (not part of this design): a message for one client went to all (3.1.19.31), each machine's
marking/download script got the whole site's file tree (3.1.19.32).

## 2. Traffic

### 2.1 Send what changed, to whom it concerns

| stream | today | 3.1.20 |
|---|---|---|
| AppsState master -> GUI | all apps, every 1 s, every GUI | **changes only**, every 1 s, to GUIs that **want the feed** (2.2); a full snapshot when a GUI starts wanting it, and every 30 s as a safety net |
| PlansState master -> GUI | all plans, every 1 s | the same: changes, snapshot on demand / every 30 s |
| ClientState master -> all | every client's state, every 1 s, to agents and GUIs | **on change** (connect, disconnect, IP, identity) to GUIs that want the feed; agents get a snapshot every 30 s (scripts on agents read client states) |
| MachineState agent -> master -> all | twice a second, forwarded to everyone | agent -> master every 2 s; master -> **GUIs that want the feed** only, every 2 s, changes only |
| AppsState agent -> master | all its apps every 500 ms | changes every 500 ms, all every 10 s |
| ScriptState master -> all | every update to everybody, with the result data | status to GUIs that want the feed; the **result data only to the requestor** of the script |

"Changed" for an AppState: any status flag, exit code, PID, plan, restarts - immediately; the performance values
(CPU, GPU, memory) only when they moved by more than a threshold (1 % CPU, 10 MB memory) or with the snapshot.

**Freshness without the flood.** Today `AppState.IsOffline` is "no update of this app for 3 s", which only works
because every app is sent all the time. In 3.1.20 freshness comes from the agent: the master refreshes the
`LastChange` of all apps of an agent whenever that agent's heartbeat (any message, at least the 2 s MachineState)
arrives, and marks them offline when the agent disconnects or stays silent for 3 s - sent as a change. A GUI no
longer derives "offline" from silence.

### 2.2 Minimised GUIs get nothing

Every machine runs a GUI, mostly minimised in the tray. A GUI tells the master whether it wants the state feed:
new message `FeedRequestMessage { bool Wanted }`, sent when its main window is shown or hidden (minimise to tray,
restore, start in tray). Default at connect: wanted (a GUI that says nothing behaves as today). When the feed is
wanted again, the master sends the full snapshots first. While a GUI does not want the feed it still gets what is
addressed to it (notifications, script results it asked for, definitions after a config reload).

### 2.3 Compact encoding

* Every class on the wire gets `[MessagePackObject]` and an integer `[Key(n)]` per member; computed properties
  (`AppState.Running`, `Dying`, ...) get `[IgnoreMember]`. Most classes already carry the keys - commented out
  (~230 members in 19 files); they are re-enabled and completed.
* The resolver becomes `StandardResolver` with `MessagePackCompression.Lz4BlockArray` for messages above a few
  hundred bytes.
* Two tests guard it: every `Union` type round-trips; every public field/property of a wire class has a `Key` or
  `IgnoreMember` (reflection), so a member added later without a key fails the build's tests instead of vanishing.

### 2.4 Expected result

For the 7-machine site, idle: AppsState goes from 126 KB/s to almost nothing while GUIs are minimised (a few hundred
B/s per open GUI otherwise); ClientState and MachineState from 77 KB/s to well under 5 KB/s. At 80 machines the
growth is linear in the number of *open* GUIs instead of N^2. `TrafficMeasurementTests` is rerun before and after
and the numbers go into the release notes; the counters stay in `/api/status` so a site can check.

## 3. Security

### 3.1 One switch, local only

* `Security` = `off` | `audit` | `on`, in the exe's `.config` next to `HttpPort`, or `--security` on the command
  line. **Never in the SharedConfig.** Default `off`: everything as in 3.1.19.
* Secrets live in files of their own, never in `LocalConfig.xml`:
  * `site.secret` - the shared site secret for the master port (any length >= 32 bytes, e.g. 64 random hex chars);
  * `remoting-public.pem` - the public key that checks REST tokens.
* Search order for each: a path given by `.config` / command line (`--siteSecretFile`, `--tokenPublicKeyFile`);
  the folder of `LocalConfig.xml`; the exe's folder.
* `on` or `audit` and a file missing or unreadable: the program does not start, with an exception naming the file and
  every place it looked. Never a silent fall-back to open.
* `audit` applies to REST only: every call is checked and the outcome logged, nothing refused - for a rollout. The
  master port is `on` or `off` (a TLS and a plain connection cannot share a port without guessing).

### 3.2 The master port: TLS plus the site secret

1. **TLS.** `Server`/`MessageClient` move to NetCoreServer's `SslServer`/`SslSession`/`SslClient` (same package,
   5.0.15). The master needs a certificate; it creates a self-signed one on first start
   (`%ProgramData%\Dirigent\master.pfx`, ECDSA P-256, 10 years) and keeps it. Agents and GUIs do not need to trust it
   in advance - step 2 binds it.
2. **Proof of the secret, both ways, bound to the TLS session.** Right after the TLS handshake, before `ClientIdent`
   is accepted:
   * client -> master: `Hello { protocolVersion, clientNonce }`
   * master -> client: `Challenge { masterNonce, masterProof = HMAC-SHA256(secret, "master" | clientNonce | masterNonce | certHash) }`
   * client checks `masterProof` with the certificate hash *it* saw, then -> master:
     `Proof { clientProof = HMAC-SHA256(secret, "client" | clientNonce | masterNonce | certHash) }`
   * master checks it; only then `ClientIdent` and everything else.

   A man in the middle presents its own certificate, so the client computes over a different `certHash` and neither
   proof matches; without the secret it cannot produce them. Nothing has to be pinned or distributed except the
   secret.
3. A client that fails is disconnected with a log line on both sides (`"<ip>: wrong site secret"`); repeated failures
   from one address are rate-limited (back-off) so the secret cannot be guessed at speed.
4. **Protocol version.** `Hello.protocolVersion` (and `ClientIdent` with security off) carries the protocol version.
   A mismatch is refused with a clear message on both sides: "machine m3 runs protocol 3.1.19, this master 3.1.20 -
   update it". Today a mismatch shows as undecodable messages.

Cost: TLS with AES-NI runs at GB/s; after 2.x the traffic is small. The GUI is an agent too, so every machine that
runs a GUI holds the secret - whoever runs the GUI is trusted with the site (as today, minus strangers on the network).

### 3.3 The REST API: tokens

* `Authorization: Bearer <JWT>`, ES256, checked against `remoting-public.pem`: signature, `exp` (one minute clock
  tolerance), scope. Dirigent issues nothing and stores nothing. `System.Security.Cryptography.ECDsa` - no package.
* **Open without a token:** `GET /api` (it says `"authentication": "on"` and lists the scopes) and `GET /api/help`,
  `/api/help/{doc}`. Everything else needs a token.
* **Scopes:** `read` (every GET, `POST /api/files/query`), `control` (everything that acts: plans, apps, scripts,
  Kill All, reload, vars, machine power, marks, downloads and their deletion), `sim` (`clear`). `/api/cli` needs
  `control`, all or nothing - no per-command scopes. The scope is an attribute on each controller method; a test fails
  for a route without one (next to `HttpDocDriftTests`).
* Refusals in the existing JSON error format: 401 (no token, bad signature, expired, "no public key configured"),
  403 naming the missing scope. The `sub` claim goes into the log line of every `control` call.
* `/websock` requires a `read` token when security is on.
* HTTPS stays a later, separate option.

### 3.4 The telnet CLI

`--cliPort off` (or `CliPort=off` in `.config`) does not start the telnet CLI at all. With security `on` it is off
unless `.config` names a port explicitly - it runs any command unchecked on all interfaces.

### 3.5 What stays open

* Anyone holding `site.secret` is fully trusted (agents and GUIs alike). Separate secrets for GUIs and agents would
  allow revoking operator machines; not in 3.1.20.
* Plain HTTP: a token can be read off the wire and reused until it expires (short lifetimes limit it).

## 4. Build order and tests

Each step is a commit on `3.1.20` with its tests (red-proved), the broad suites green:

1. **Compact encoding** (2.3) + the protocol version in `ClientIdent`. Tests: round-trip of every union type, the key
   coverage test, a version-mismatch refusal (TestBed).
2. **Feed policy** (2.1, 2.2): deltas, snapshots, `FeedRequestMessage`, freshness from the heartbeat. Tests: a GUI
   that does not want the feed receives no state; one that wants it again gets the full picture first; an app of a
   disconnected agent turns offline; deltas reconstruct the same state as full snapshots (randomised sequences);
   `TrafficMeasurementTests` before/after.
3. **Settings and secret files** (3.1). Tests: search order, the start-up exception texts.
4. **TLS + handshake** (3.2). Tests: right secret connects; wrong secret refused both ways; a relaying MITM with
   another certificate is detected (TestBed with a proxy); version mismatch; security off unchanged.
5. **REST tokens** (3.3). Tests: open routes, 401/403 cases, the scope-attribute coverage test, audit mode logs and
   lets through.
6. **CLI off** (3.4).
7. Docs: `Security.md` (new), `HTTP.md`, `LocalConfig.md`, `CLI.md`, release notes with the traffic numbers.

Then a release on the 3.1.20 line and the test site: traffic before/after with the counters, security `on` with a
secret on all 7 machines.

## 5. Open points

1. Token audience: tokens signed with the site key work on every application of the site. If a token for one stand or
   application must not drive Dirigent, Dirigent should check an `aud` claim - to agree with whoever issues tokens.
2. Snapshot period (30 s proposed) and the performance thresholds (1 % CPU, 10 MB) - adjustable after measuring.
