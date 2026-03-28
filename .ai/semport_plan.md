PORT

## Commit: 7ca921b
**"feat: add decoder to read policies from an io.Reader"**
Date: 2025-09-26T13:21:38+02:00

---

## Semantic Analysis

The Go commit adds a symmetric `Decoder` counterpart to the existing `Encoder` in `stream.go`. The `Decoder` wraps an `io.Reader` and supports sequential single-policy reads via `Decode(*Policy)`, returning `io.EOF` when the stream is exhausted. This is a genuine API surface addition — stateful streaming parse of Cedar policy text.

Key semantics:
- `NewDecoder(r io.Reader) *Decoder` — factory wrapping a parser-level decoder
- `Decoder.Decode(p *Policy) error` — reads exactly one policy statement from the stream; returns `io.EOF` at end
- Internally delegates to `parser.Decoder` (the low-level tokenizing decoder)
- The resulting `Policy` is constructed via `NewPolicyFromAST`

.NET idiom mapping:
- `io.Reader` → `System.IO.TextReader` (or `Stream` with a `StreamReader` wrapper)
- Stateful `Decoder` struct → `sealed class PolicyDecoder` (not a record — it holds mutable reader state)
- `io.EOF` → return `null` or `bool` from `TryDecode`, OR throw `EndOfStreamException` — prefer a `bool TryDecode(out Policy? policy)` pattern which is idiomatic C#
- `Encode` already exists — match its home location for `Decode`

---

## Port Tasks

### 1. Locate the existing Encoder in C# to establish placement
- Find where `PolicyEncoder` or equivalent lives (likely `src/Cedar.Ast` or `src/Cedar.Core`)
- Search for `Encode` or `Encoder` in the solution to find the existing streaming write API

### 2. Create `PolicyDecoder` class alongside the existing encoder
- Target file: same namespace/project as the existing encoder (expected: `src/Cedar.Ast/PolicyDecoder.cs`)
- Implement:
  ```csharp
  public sealed class PolicyDecoder
  {
      private readonly TextReader _reader;
      public PolicyDecoder(TextReader reader) { _reader = reader; }
      // Returns true and sets policy if a policy was read; false at end-of-stream
      public bool TryDecode(out Policy? policy);
  }
  ```
- Internally reuse whatever `PolicyParser.Parse(string)` or equivalent method already exists, feeding it one policy-worth of text at a time from the reader
- Handle EOF by returning `false` / `null`

### 3. Add `TextReader`/`Stream` convenience factory (optional overload)
- `public static PolicyDecoder FromStream(Stream stream, Encoding? encoding = null)`
  wrapping `new StreamReader(stream, encoding ?? Encoding.UTF8)`

### 4. Add xUnit tests in `test/Cedar.Tests` (or `test/Cedar.Ast.Tests` if it exists)
- Mirror the Go test: create a `TextReader` over a multi-policy string
- Decode policy 0 → assert it is `permit(principal, action, resource);`
- Decode policy 1 → assert it is `forbid(principal, action, resource);`  
- Decode again → assert `TryDecode` returns `false` (EOF)

### Go source references
- `inspiration/cedar-go/stream.go` lines 27–53 — `Decoder` struct, `NewDecoder`, `Decode`
- `inspiration/cedar-go/stream_test.go` lines 72–113 — `TestDecoder`

### C# target references (to locate before editing)
- Grep for `Encoder` or `Encode` in `src/` to find the existing encoder file
- Grep for `PolicyParser` or `ParsePolicy` to find the parser entry point used internally
