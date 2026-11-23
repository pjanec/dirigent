## Network protocol

Agents and GUIs connect to the master on its TCP port (`--masterPort`, 5045 by default). They exchange
messages framed as a 4-byte length followed by a MessagePack body. Nothing else travels on that port.

### Encoding

Every message class and everything it contains is written with **integer keys** - an object is an array
whose positions are the `[Key(n)]` numbers of its members, not a map of field names - and a body above a few
dozen bytes is LZ4-compressed (`Lz4BlockArray`). A site's idle traffic is mostly many small, similar objects, and
this is about a fifth of what the field names used to cost.

A class that travels in a message must therefore carry `[MessagePackObject]` and a `[Key]` (or `[IgnoreMember]`)
on every public field and property. Tests (`WireEncodingTests`) walk everything reachable from the message list and
fail for a member without one, so that a field added later does not silently vanish from the wire.

### Protocol version

The encoding is not compatible between versions, so master and clients check that they speak the same one. Every
client says its protocol version (`Net.Protocol.Version`, 3120 for 3.1.20) in the first message it sends, the
`ClientIdent`. The version is raised whenever a change would make an older program misread a message.

A site runs one version of Dirigent, and what a mismatch looks like is:

* **A client that speaks another protocol version.** The master logs
  `m3 at 192.168.0.13 speaks protocol 3119, this master 3120 - update it to 3.1.20.0`, sends the client a
  `RejectedMessage` carrying that text and closes the session. The client logs
  `The master refused this client: ...` as an error and, if it reconnects by itself (agents and GUIs do), tries again
  at most every 30 seconds instead of every second, so that the master's log is not flooded.
* **A client of an older Dirigent (3.1.19 and earlier).** Its first message is not an array but a map of field names,
  which this master cannot read at all. The master logs
  `a client at 192.168.0.13 sent a message this master cannot read - it probably runs an older Dirigent than 3.1.20.0; update it`
  - once per address per 10 minutes - and closes the session. The older client sees only that the connection drops.

In both cases the fix is the same: update the machine to the version of the master.

### What is sent to whom, and when

The states of everything - apps, plans, clients, machines, scripts - are the *feed*. Idle traffic used to grow with the
square of the number of machines because every state was sent to every GUI all the time; now a state is sent when it
changes, and only to the GUIs that look at it.

| What | Agent to master | Master to GUIs | Master to agents |
|---|---|---|---|
| app states | the ones that changed, every agent tick (500 ms); all of them every 10 s | the ones that changed, once a second; all of them every 30 s | - |
| plan states | - | the ones that changed, once a second; all of them every 30 s | - |
| client states | every second (the heartbeat) | on change (connected or not, address, who it is) | on change, and all of them every 30 s |
| machine state (CPU, memory) | every 2 s | the agent's, as it arrives (every 2 s) | - |
| script states | as the script runs | without the result data | only to the agent that asked for the script |

"Changed" for an app is any status flag, exit code, PID, plan, restarts, start or end time - at once; its CPU, GPU and memory
only when they moved by 1 percentage point or 10 MB since they were last sent, or with the periodic full send. The time of
the last change is never a change. The comparison is `AppState.DiffersForFeed` (and the same for plans and clients).

**The feed request.** A GUI sends `FeedRequestMessage { Wanted }`: false when its window is hidden or minimised to the
tray, true when it is shown again, and - if its window is not shown - right after it connects. A GUI that says nothing wants
the feed. While a GUI does not want it, it receives no app, plan, client, machine or script states at all; what is addressed
to it still arrives: notifications, the results of scripts it started, and definitions (apps, plans, files, menus, scripts) after
a configuration reload. When it wants the feed again the master sends it everything - clients, plans, apps, scripts - before the
changes, because the changes it missed are gone. Hidden-to-tray GUIs are why a site's idle traffic is mostly a function of the
number of *open* GUIs, not of machines.

**Script results.** The state of a script carries its data - the progress or result, which can be large. The master sends the whole
state to whoever started the script and the state without data to the GUIs that look at the feed; a script a GUI did not start
shows its status and text there, not its result. Scripts of the master itself (started from REST, the CLI, a startup script or another script)
are read at the master with their data.

**Freshness.** An app's state is sent when it changes, so a GUI cannot tell from its age whether the agent is still there. The
master does: whatever an agent says - the client state every second at the least - keeps the states of its apps fresh, and when the agent
disconnects or is silent for 3 seconds the master marks its apps `Offline`, which is a change and goes to the GUIs like any other.
`AppState.IsOffline` is that flag (or: the app has never been reported); GUIs and the REST `offline` field use it, and the "offline for N sec"
of a GUI counts from the last news of the agent. A client state shows "offline for N sec" only once the client is not connected.

### The secured master port

With [security](Security.md) on or audit the master port is TLS, and a client is only let in after a handshake that proves that both ends
know the site secret. Between the TLS handshake and the `ClientIdent` three more messages are exchanged:

1. client to master: `Hello { ProtocolVersion, ClientNonce[32] }` - the protocol version is checked here, as it is for `ClientIdent`;
2. master to client: `Challenge { MasterNonce[32], MasterProof }`, with
   `MasterProof = HMAC-SHA256(secret, "dirigent-master" | ClientNonce | MasterNonce | certHash)`;
3. client to master: `Proof { ClientProof }`, with
   `ClientProof = HMAC-SHA256(secret, "dirigent-client" | ClientNonce | MasterNonce | certHash)`, sent only after the client has checked `MasterProof`.

`certHash` is the SHA-256 of the master's certificate (its DER). The certificate is self-signed (ECDSA P-256), made in memory at every start of
the master and never stored, so nothing can be pinned; the client accepts any certificate and hashes the one it was shown. Because the
proofs are bound to that hash, a program between the client and the master - which has to show a certificate of its own - cannot pass them on: the client
computes the check over *its* certificate, the master over its own, and they differ. The two labels differ, so a proof of the master is no use as one of
a client. The master compares with a constant-time comparison.

Anything but the expected message, a wrong proof, or a handshake not completed within 10 seconds closes the connection. More than five wrong proofs from one address
within a minute make the master close new connections from that address at once for a minute. A refused client asks again no faster than every 30 seconds.
