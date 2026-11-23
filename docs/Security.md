## Security

Dirigent starts and kills programs, reboots machines and runs scripts on every machine of a site, for whoever can talk to it. By
default - `Security=off` - it does what it always did: anybody who reaches the master's ports can do all of that. Security closes the
master port; the other two doors, the web API and the telnet CLI, are described at the end.

### What is protected, and what is not

| | Off (default) | On or audit |
|---|---|---|
| **The master port** (agents and GUIs to the master, 5045) | plain TCP | TLS, and a handshake that proves the site secret both ways before anything else is accepted |
| **The telnet CLI** (5050) | runs | **off**, unless a port is named; if it is named it still asks nobody anything |
| **The web API** (8877) | open | a token on every call but the index and the manual; plain HTTP |

What the secret means: whoever holds `site.secret` is trusted with the whole site - agents and GUIs alike, as every machine that runs a
GUI holds it. There is one secret per site; separate ones for operators and agents are not planned for 3.1.20. The traffic on the master port is
encrypted, but the web API and the CLI port are plain: anything read off them can be reused (a token, until it expires).

`audit` differs from `on` only for the web API (checked and logged, nothing refused); for the master port, and for every
process of the site, it means the same as `on`. There is no half-secured mode of the master port: a TLS port cannot also speak plain TCP.

### Setting it up

All machines of a site run the same version and the same setting. Do it everywhere, then restart everything.

1. **Make a secret** - at least 32 characters, 64 random hex characters are suggested. On one machine, in PowerShell:

   ```powershell
   $b = New-Object byte[] 32; [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); -join ($b | ForEach-Object { $_.ToString('x2') })
   ```

   It uses the system's cryptographic random number generator and works in Windows PowerShell and PowerShell 7. Save it as `site.secret`, a text file
   that holds nothing but the secret. Leading and trailing white space is ignored.
2. **Put `site.secret` on every machine** - the master's, every agent's, every machine that runs a GUI - in the folder of the `LocalConfig.xml` that
   program uses, or next to the executable. Or name the file: `--siteSecretFile D:\keys\site.secret`, or `SiteSecretFile` in the `.config`. A file
   that is named is the only place looked at. Keep it readable only by the account Dirigent runs as, and never put it in the SharedConfig, in LocalConfig.xml
   or anywhere that is shared.
3. **Set `Security=on`** on every machine: `--security on` on the command line, or the `Security` setting in the exe's `.config`, next to `HttpPort`. Never in the
   SharedConfig: a program must not take its security from a file somebody else can change.
4. **Restart** the master first, then the agents and GUIs. A program that cannot find or read the secret does not start (below).

Where `site.secret` is searched for, in order: the file named by `--siteSecretFile`; the folder of the LocalConfig.xml in use; the folder of the executable.
The same order is used for `remoting-public.pem`, which the master of a site with a web API needs to check its tokens (`--tokenPublicKeyFile`; see below).

### If the secret is not there

With `on` or `audit` every process - master, agent, GUI - reads the secret when it starts. If the file is missing, cannot be read or holds fewer than 32 characters, the
process does not start and there is **no fall-back to open**. It writes the reason to its log and to standard error, and exits with code **5**. A missing file is
reported with the name of the file and every place that was looked in, for instance:

```
Security is on but the site secret ('site.secret') was not found. Looked in:
  D:\Dirigent\site.secret  - the folder of the LocalConfig.xml in use (D:\Dirigent\LocalConfig.xml)
  C:\Program Files\Dirigent\site.secret  - the folder of the executable
Put the file in one of those places, or name it with --siteSecretFile; or set Security=off. See docs/Security.md.
```

### What the logs say

| Log line | Where | Meaning |
|---|---|---|
| `The master port is secured (Security=on).` | master | at start; and `... is not secured (Security=off)` otherwise |
| `<ip>: wrong site secret (or not the same Dirigent)` | master | a client from that address failed the proof - it sent a wrong one, or it did not like the master's (its secret differs) and hung up. Check `site.secret` on that machine |
| `<ip>: more than 5 failed proofs within 60 s - connections from it are closed at once for 60 s` | master | the address is shut out for a minute; said once |
| `<ip>: TLS handshake failed - is security on and the same version on that machine?` | master | the client did not do TLS: a plain client (security off there), an old version, or something else. Said once per address per 10 minutes |
| `<ip>: the handshake was not completed within 10 s; closing` | master | a connection that did not get through TLS and the proof in time, or said nothing |
| `a client at <ip> speaks protocol <v>, this master <V> - update it to <version>` | master | the protocol version of its Hello differs - the program is of another version |
| `the master at <ip> did not prove the site secret - wrong secret, or someone in between` | client | the master's proof does not match: the secrets differ, **or a program between the two presents a certificate of its own** |
| `the TLS handshake with the master at <ip> failed - is security on there too, and the same version?` / `... did not finish the handshake within 10 s ...` | client | the master does not speak TLS (security is off there), or is not Dirigent |

A client that was refused asks again after 30 seconds instead of every second.

### How it works

See [Network protocol](Network.md): the master's certificate is made anew at each start and nothing pins it; the proof of the site secret, bound to that
certificate, is what tells a client it is talking to the master and the master that it is talking to one of its own.

### The telnet CLI

`--CLIPort off` (or `CLIPort=off` in the `.config`) does not start the telnet CLI at all. The CLI runs any command, unchecked, for anyone who can reach its port - so with
`--security on` or `audit` it is **off by default**, and runs only if a port is named (`--CLIPort 5050`, or a number in the `.config`), whereupon the master logs a
warning that it asks nobody anything. Scripts and tools that use the CLI have to be given a port then; the web API is the other way in. (A `.config` that was shipped with
`CLIPort` set to 5050 still has it on; empty that setting.) `--CLIPort 0` still means "the default" and does not name a port.

### The web API

With security on or audit the web API (and its web socket and static page) checks a token on every call but the index and the manual (`GET /api`, `/api/help`). The master needs
`remoting-public.pem`, the public half of the ES256 key pair whoever issues tokens holds: it is looked for like `site.secret` (`--tokenPublicKeyFile`, then the folder of the LocalConfig.xml, then the
folder of the executable), and a master with a web server (`--httpPort` above 0) that cannot find or read it **does not start** - exit code 5, with every place it looked. Agents and GUIs do not need it.

**How a tool gets in.** It sends `Authorization: Bearer <token>`, a JWT signed with ES256. Dirigent issues nothing; whoever holds the private key makes tokens, with a short life (`exp` is required, 60 seconds of clock
difference are tolerated) and the scopes the tool needs:

| scope | allows |
|---|---|
| `read` | every `GET`, `POST /api/files/query`, the web socket and the static page |
| `control` | everything that acts - plans, apps, scripts, kill all, reload, variables, machine power, marks, downloads, `POST /api/cli` |
| `sim` | clearing files (with `control` too, for `POST /api/marks`) |

Scopes add up, and `control` does not include `read`: a tool that looks and acts has `"scope": "read control"`. `GET /api` lists every route with its scope, so a tool or an agent sees what it needs. A route nobody described needs `control`.
See [Web API](HTTP.md#Authentication) for the refusals (401 and 403) and the claims.

**The audience.** Tokens signed with the site's key work on every application that trusts that key. To keep tokens made for another application away from Dirigent, start the master with `--tokenAudience dirigent-lab` (or `TokenAudience` in the `.config`): then a token
must carry `"aud": "dirigent-lab"` (or an array containing it). Unset, `aud` is not looked at.

**Rolling it out.** `Security=audit` secures the master port like `on` but only *checks* the web API: every call that would have been refused is logged
(`audit: 10.0.0.7 POST /api/plandefs/all/start would be refused (401): no token ...`) and let through. Run a while in audit, give every tool its token, then switch to `on`.

**What is logged.** Every call that changes something - everything but `GET` - is logged by the master with the token's `sub`: `ci-runner POST /api/plandefs/all/start from 10.0.0.7`. A refusal is logged with the address, the route and the reason.

**What is still open.** The web API is plain HTTP: a token can be read off the wire and reused until it expires, which is why tokens should be short-lived. HTTPS is a later, separate option.
