# Semport Plan Finalized: c3c8479 — Add DOT graph export for EntityMap

## STATUS: ALREADY IMPLEMENTED — ACKNOWLEDGE

The upstream feature is **fully present** in the C# codebase. No code changes are needed.

---

## Evidence

### Go source (upstream, c3c8479)
`inspiration/cedar-go/x/exp/dot/dot.go` — new file, 76 lines  
Introduces `Write(w io.Writer, entities iter.Seq[types.Entity]) error` that:
1. Writes `strict digraph { ordering="out" node[shape=box] }`
2. Groups entities into subgraph clusters by `EntityType`
3. Renders each entity as a node with `label=<quoted ID>`
4. Renders parent edges as `quoted_uid -> quoted_parent_uid`
5. Quotes all DOT identifiers using `strconv.Quote` (double-quote wrapping with backslash escapes)

### C# implementation (existing)
`src/Cedar.Experimental/EntityGraphDotWriter.cs` — **already exists**, full implementation:
- `public static string ToDot(EntityMap entities)` → line 12
- `public static void Write(TextWriter writer, EntityMap entities)` → line 22
- Identical semantics: `strict digraph {` prelude (line 30), `SortedDictionary<string, List<Entity>>` grouping by type (line 34), subgraph clusters with `cluster_<type>` label (line 48–53), parent edges ordered by `ToString()` (line 57–61), `Quote()` helper with same backslash/double-quote escaping (line 64–79)
- Uses `Cedar.Types.EntityMap`, `Cedar.Types.Entity`, `Cedar.Types.EntityUid`

`test/Cedar.Experimental.Tests/DotWriterTests.cs` — **already exists**, full test coverage:
- `EmptyGraph_WritesPrelude` (line 12) ✓
- `WritesNodesAndEdges` (line 21) ✓  — mirrors Go `WritesNodesAndEdges`
- `QuotesIdentifiersAndLabels` (line 41) ✓
- `OrdersClustersByType` (line 51) ✓
- `NoEdgesWhenNoParents` (line 64) ✓  — mirrors Go `NoEdgesWhenNoParents`

---

## Go → C# Mapping (for reference)

| Go | C# |
|---|---|
| `iter.Seq[types.Entity]` | `IEnumerable<Entity>` (via `EntityMap : IReadOnlyCollection<Entity>`) |
| `types.EntityMap` | `Cedar.Types.EntityMap` (`src/Cedar.Types/EntityMap.cs:8`) |
| `entity.UID.Type` (`EntityType`) | `entity.Uid.Type` (`EntityType`, `src/Cedar.Types/EntityUid.cs:15`) |
| `entity.UID.ID.String()` | `entity.Uid.Id.Value` (`CedarString`, `src/Cedar.Types/EntityUid.cs:17`) |
| `entity.Parents.All()` | `entity.Parents` (iterable `EntityUidSet`, `src/Cedar.Types/EntityUidSet.cs:8`) |
| `strconv.Quote(v)` | `Quote(v)` private helper — double-quote wrap + `\\` and `\"` escaping |
| `io.Writer` error return | `TextWriter` (throws on failure; C# uses exceptions not error returns) |
| `map[EntityType][]Entity` | `SortedDictionary<string, List<Entity>>` (adds deterministic ordering) |

---

## Action Required

Run:
```
python3 semport/ledger.py update c3c8479 acknowledged && python3 semport/ledger.py sort
git add semport/ledger.tsv && git commit -m "semport: acknowledge c3c8479 - DOT graph export already implemented in EntityGraphDotWriter"
rm -f .ai/semport_new_commits.md
```
