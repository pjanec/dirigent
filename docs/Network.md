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
