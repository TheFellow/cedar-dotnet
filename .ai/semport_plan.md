PORT

## Commit
`c1a7b0a` — 2024-08-23T13:41:24-06:00
`cedar-go/internal/json: implement checking for invalid extension functions and methods in the JSON parser`

## Semantic Analysis

### What changed in Go
In `internal/json/json_unmarshal.go`, the `extensionJSON.ToNode()` method gained a lookup against `extensions.ExtMap` after extracting the single key `k` from the JSON object. If `k` is not a known extension function/method name, it returns:

```go
fmt.Errorf("`%v` is not a known extension function or method", k)
```

Previously, an unknown extension name was silently ignored (or fell through to other error paths). Now it is an explicit, early error.

The test change is mechanical: all existing cases were always expected to error, so the per-case `errFunc` field was collapsed to a shared `testutil.Error` call, and a new case `"unknown-extension-function"` was added to exercise the new guard.

### Known extension names in cedar-go
`extensions.ExtMap` maps `types.String` → extension descriptor. In cedar-go the recognised names are the built-in extension functions and methods: `ip`, `decimal`, and their methods (`isIpv4`, `isIpv6`, `isLoopback`, `isMulticast`, `isInRange`, `lessThan`, `lessThanOrEqual`, `greaterThan`, `greaterThanOrEqual`).

### What to port to C#
The C# JSON policy parser (Cedar.Ast) deserializes Cedar policy JSON. When it encounters an object key in the expression position that looks like an extension call, it must now reject unknown names with a descriptive error rather than silently passing through or producing a misleading error later.

---

## Concrete Port Tasks

### 1. Identify the C# extension call deserialization site
**Go source:** `inspiration/cedar-go/internal/json/json_unmarshal.go` — `extensionJSON.ToNode()` (~line 137-150)
**C# target:** Locate the equivalent of `extensionJSON` deserialization in the Cedar policy JSON parser.
- Look in `src/Cedar.Ast/` for the file(s) that parse Cedar JSON policy format (likely something like `PolicyJsonConverter.cs`, `JsonUnmarshal.cs`, or similar).
- Search for where extension-function call nodes are constructed from a JSON object key (the pattern where a single-key JSON object maps to a function call, e.g. `{"ip": [...]}` → `ExtensionCall("ip", ...)`).

### 2. Build the set of known extension names
**Go source:** `inspiration/cedar-go/internal/extensions/extensions.go` — `ExtMap`
**C# target:** The C# codebase likely already has a registry or enum of known extension functions (e.g. `ip`, `decimal` constructors and their methods). Identify that registry (search `src/Cedar.Ast/` and `src/Cedar.Types/` for extension name constants or a lookup table).
- If a static `HashSet<string>` or similar does not exist, create one containing all known extension function and method names.

### 3. Add the validation guard
At the point where the extension function name `k` is extracted from the JSON object key, before building argument nodes, add:

```csharp
if (!KnownExtensionNames.Contains(k))
    throw new JsonException($"`{k}` is not a known extension function or method");
```

(Use `JsonException` or whatever exception type the existing parser uses for parse errors.)

### 4. Add a test case
**Go source:** `internal/json/json_test.go` — `"unknown-extension-function"` case with input:
```json
{"effect":"permit","principal":{"op":"All"},"action":{"op":"All"},"resource":{"op":"All"},
"conditions":[{"kind":"when","body":{"not_an_extension_function":[]}}]}
```
**C# target:** `test/Cedar.Tests/` (or whichever test project covers JSON policy parsing).
- Add an xUnit `[Fact]` or `[Theory]` that parses the above JSON and asserts an exception is thrown.
- Mirror the existing pattern in the relevant test file for JSON unmarshal error cases.
