PORT

## Commit Summary
- **SHA**: a12ba1d
- **Message**: add feather: trailing commas
- **Author**: jaredzhou
- **Date**: 2025-11-23T15:50:11+08:00

## Semantic Analysis
This commit adds **trailing comma support** to the Cedar policy language parser in three list-like constructs:

1. **Entity lists** (e.g., `[User::"alice", User::"bob",]`) — parsed by `entlist()`
2. **Expression lists** (e.g., `[1, 2,]` or function args with trailing comma) — parsed by `expressions()`
3. **Record literals** (e.g., `{"key": 1,}`) — parsed by `record()`

The Go change refactors the comma-handling from a "require comma before each item after the first" pattern to a "consume item, then switch on what follows: comma → advance; end-marker → stop; else → error" pattern. This allows a trailing comma before the closing delimiter.

This is a **language-level semantic change** — Cedar policies with trailing commas in these positions should now be accepted as valid. It must be reflected in the C# parser to maintain conformance.

## Port Tasks

### 1. Locate the C# parser's list-parsing methods
Target project: `src/Cedar.Ast` (or `src/Cedar.Core` linked files)
Look for the equivalent of:
- `entlist()` — parses `[EntityUID, EntityUID, ...]`
- `expressions()` — parses comma-separated expression lists
- `record()` — parses `{ key: value, ... }` record literals

Likely files (search for these):
- `src/Cedar.Ast/Parser/` — look for a Cedar policy parser class
- Search for methods handling `[` / `]` delimited entity lists
- Search for `ParseRecord`, `ParseExpressionList`, `ParseEntityList` or similar

### 2. Update `entlist` equivalent
**Go before**: check `len(res) > 0` then require exact(",")
**Go after**: after appending each entity, switch on next token:
  - `,` → advance (consume comma, allow trailing)
  - `]` → break out of loop
  - else → error "got X want ,"

Port the same logic to C# for the entity list parser.

### 3. Update `expressions` equivalent
Same pattern as entlist but for general expression lists, using `endOfListMarker` as the closing token.
- `,` → advance
- endOfListMarker → break
- else → error

### 4. Update `record` equivalent
Same pattern for record literal parsing:
- After each key-value pair, switch on next token:
  - `,` → advance
  - `}` → break
  - else → error

### 5. Add tests
In `test/Cedar.Tests` (or wherever parser tests live), add xUnit test cases mirroring the Go test additions:
- `"expr list with trailing comma"`: `permit (principal, action, resource) when {[1,2,].isEmpty() };`
- `"record with trailing comma"`: `permit (principal, action, resource) when {{"key":1,} has key };`
- `"entity list with trailing comma"`: `permit (principal, action, resource) when {User::"alice" in [User::"bob",] };`

These should parse successfully and produce the same AST as without the trailing comma.
