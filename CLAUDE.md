# CLAUDE.md — Dirigent.NetCore

## Code search: use the knowledge graph *and* grep

Two tools, always together. The graph answers *where things are* and *how they relate*; grep
and reading establish *what the code actually says*. Neither is sufficient alone — the graph
surfaces components keyword search misses (it found a headless master+agent host in
`Dirigent.Agent.Console/AgentMasterApp.cs` that grep had not), and grep catches the graph's
wrong edges.

### Getting at it

**Prefer the MCP server** when it is connected: tools named `mcp__codebase-memory-mcp__*`.
They are deferred, so load the schemas before calling:

```
ToolSearch("select:mcp__codebase-memory-mcp__search_graph,mcp__codebase-memory-mcp__query_graph")
```

**Fall back to the CLI** when no MCP server is available — the same bundled exe serves every
tool without a server:

```
codebase-memory-mcp.exe cli <tool> --flag value
echo '<json-args>' | codebase-memory-mcp.exe cli <tool>
codebase-memory-mcp.exe cli <tool> --help          # per-tool flags
```

On this machine the exe is `%USERPROFILE%\.local\bin\codebase-memory-mcp.exe`
(v0.9.0); elsewhere find it via `where codebase-memory-mcp` or the `mcpServers` entry in
`~/.claude.json`. Passing raw JSON as a positional argument still works but is deprecated —
use flags or piped stdin.

Project name for this repo: **`D-Work-Dirigent.NetCore`**. Verify with `list_projects`; if it
is absent, run `index_repository` with `repo_path` and `mode=moderate` — moderate or full is
required for `semantic_query`, and `bin`/`obj`/`docs`/`publish` are excluded automatically.
After large changes, `index_status` and `detect_changes` show staleness; re-index rather than
trusting a stale graph.

### Which tool for which question

| Question | Use |
| --- | --- |
| "where is the code that does X", name unknown | `search_graph` with `query` — BM25, splits camelCase, boosts Functions/Methods/Classes |
| exact symbol name or regex | `search_graph` with `name_pattern`, or grep |
| "who calls this", "what reaches this type", multi-hop | `query_graph` with Cypher — grep cannot answer these |
| vocabulary mismatch, e.g. "send" vs "publish" | `search_graph` with `semantic_query` — an **array** of keywords, not a string |
| a literal string, an XML/config value, a comment | grep — the graph indexes code symbols only |
| anything under `bin`, `obj`, `docs`, `publish` | grep — excluded from the index |
| confirming the exact lines before editing | Read or grep, every time |

Also available: `trace_path` (paths between two nodes), `get_code_snippet`,
`get_architecture`, `search_code`, `manage_adr`.

### Rules learned the hard way

- **Confirm before acting.** `CALLS` edges resolve same-named methods loosely across types.
  In this repo the graph reported `AppWatcherCollection.Tick → AgentWindow.Tick`, which is
  false. Every graph hit is a lead: verify with grep or by reading the file before changing
  code, and never state a fact on a graph edge alone.
- **Narrow, then paginate.** Results are capped at `limit` (default 200) and truncated
  silently. Check `total` and `has_more`; cut the set down with `label`, `file_pattern` or
  `min_degree` before paging with `offset`.
- **Guard the output size.** Responses are single enormous JSON lines — `list_projects` alone
  is roughly 10k tokens. In CLI mode pipe through a formatter that prints only
  label / qualified name / file:line. In MCP mode keep `limit` small and always set `label`
  or `file_pattern`.
- **Map, then territory.** Use the graph to decide where to look and to see relationships;
  use grep and Read to establish facts. A conclusion drawn from the graph alone is not
  evidence.

## Sonnet delegation — one warm worker

*Imported 2026-11-04 from another repository's `CLAUDE.md` (user rules of 2026-10-26, revised
2026-10-29 and 2026-10-30).* Measured there: a fresh agent spent ~100k+ tokens re-reading before any
work and 8–42 min per task — mostly cold builds and broad test runs, not code. Three parallel agents
hit the 5-hour rate limit twice.

### Who does what

| | |
|---|---|
| **Sonnet: every well-specified build task, small ones too** — the brief can name the files, the seam to reuse and success conditions a test decides | mirroring a pattern, wiring, tests, measurements, a scoped fix |
| **Opus: what needs judgment** — design and trade-offs, a defect whose cause is unknown, subtle semantics (wire, timing, reconnection), ambiguous work, reviews, merges | the test: if the brief would have to say *"figure out why"* or *"decide which"*, it is Opus's |
| **No warm worker and a one-file fix?** Do it directly | a cold start costs more than the fix |

### How the worker runs

| | |
|---|---|
| **One standing worker at a time, kept warm and reused** — the next task goes to it with `SendMessage`, naming only the delta | a fresh agent only after a restart or when its context is too full |
| **In the main checkout, no worktree** (warm `bin/obj`) | both sides commit only their own files, by path — never `git add -A` |
| **One build at a time on the machine** — the worker builds test projects one after another; Opus does not start a build while the worker is building | parallel builds of the same projects fight over one compiler |
| **After a killed worker: `git diff` its WIP for leftover mutations before anything else** | |

### The brief and the review

| | |
|---|---|
| **The brief hands over what Opus measured** — file:line, the seam, the test class to extend — and **checkable success conditions**; Opus writes any design first | never "make it work", never ask it to re-discover |
| **Gates in the brief: the touched test classes + every class that exercises the changed seam (integration included)**, each new test red-proved by a compiling mutation; exact build/test commands | broad suites once, after the merge, by Opus in the background |
| **Proof is proportionate to risk** — a cosmetic change red-proves one key test and gates only the touched test classes; the full mutation set and broad gates are for wire, timing and cross-machine changes | |
| **Sonnet writes its own tests and works until they pass; Opus does not re-run them** — the review is: do the tests assert what matters, and is the code right (read the diff) | a test that misses what matters goes back to the worker with the missing case named |
| **Verify every "cannot be done / does not exist / not reachable" claim before accepting it** — one grep for a precedent | |
