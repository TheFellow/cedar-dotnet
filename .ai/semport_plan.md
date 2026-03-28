PORT

## Commit Summary
**SHA:** c3c8479  
**Message:** Add DOT graph export for EntityMap  
**Author:** Pierre-Henri Symoneaux  
**Date:** 2025-11-05T16:27:54+01:00

## Semantic Analysis

This commit adds a new experimental package `x/exp/dot` in cedar-go that exports an `EntityMap` (a collection of `Entity` values) as a DOT (Graphviz) digraph. The key semantics are:

1. **Grouping by entity type** — entities are clustered into DOT subgraphs keyed by their `EntityType`.
2. **Nodes** — each entity becomes a node, labeled by its `EntityUID.ID` (the local ID, not the full type::id string), identified by its full `EntityUID.String()`.
3. **Edges** — parent relationships (`entity.Parents`) become directed edges `entity -> parent`.
4. **Output format** — `strict digraph { ordering="out" node[shape=box] ... }` with subgraph clusters per type.
5. **ID quoting** — all DOT identifiers are double-quoted (using `strconv.Quote` equivalent).

Our `Cedar.Experimental` project already has DOT export per CLAUDE.md. This port should add or extend `EntityMap`-level DOT export with the same subgraph-clustering semantics.

## Port Tasks

### 1. Locate existing DOT export code in Cedar.Experimental
- Target project: `src/Cedar.Experimental/`
- Look for any existing `Dot` or `DotExport` class/file (likely `DotExporter.cs` or similar).
- If it exists, extend it; if not, create `src/Cedar.Experimental/Dot/EntityMapDotExporter.cs`.

### 2. Implement `EntityMapDotExporter` (or extend existing)
Create a static method `WriteDot(IEnumerable<Entity> entities, TextWriter writer)` that:
- Writes the DOT prelude: `strict digraph {\n\tordering="out"\n\tnode[shape=box]`
- Groups entities by `EntityType` (from `entity.Uid.Type`)
- For each type group, writes a subgraph cluster:
  ```
  \tsubgraph "cluster_<type>" {
  \t\tlabel=<quoted_type>
  \t\t<quoted_uid> [label=<quoted_id>]
  \t}
  ```
- Writes edges for all parent relationships:
  ```
  \t<quoted_uid> -> <quoted_parent_uid>
  ```
- Writes closing `}`
- ID quoting: use `JsonEncodedText` or simply `"\"" + value.Replace("\\","\\\\").Replace("\"","\\\"") + "\""` — consistent with `strconv.Quote`.

C# target file: `src/Cedar.Experimental/Dot/EntityMapDotExporter.cs` (create if missing)  
Go source reference: `inspiration/cedar-go/x/exp/dot/dot.go` lines 1–76

### 3. Add xUnit tests
Create `test/Cedar.Experimental.Tests/Dot/EntityMapDotExporterTests.cs` with cases:
- `WritesNodesAndEdges` — mirrors Go test: Group::admins, User::alice (parent=Group::admins), User::bob (no parents). Verifies subgraph clusters, node lines, and edge line.
- `NoEdgesWhenNoParents` — two entities of different types, no parents, verifies nodes present and no `->` in output.
- `EmptyEntities` — empty collection produces valid `strict digraph { ... }` with just prelude and closing brace.

Go test reference: `inspiration/cedar-go/x/exp/dot/dot_test.go` lines 1–170

### 4. Wire up public API
Ensure the method is accessible from the public surface of `Cedar.Experimental`. Check the project's existing exports and add a using/re-export if needed.

### 5. Build and test
- `dotnet build cedar-dotnet.sln`
- `dotnet test test/Cedar.Experimental.Tests/`
