# Finalized Port Plan: 7ca921b
**"feat: add decoder to read policies from an io.Reader"**

---

## Context & Current State

The C# codebase already has:

- **`src/Cedar.Core/Encoder.cs`** — `static Encoder` (Stream overloads) + `sealed PolicyEncoder` (stateful `TextWriter` wrapper, one-policy-at-a-time `Encode(Policy)`)
- **`src/Cedar.Core/Decoder.cs`** — `static Decoder.Decode(Stream)` which reads the *entire* stream at once and returns all policies as `Policy[]`
- **`test/Cedar.Tests/Policy/StreamTests.cs`** — existing encoder tests + `Decoder_Decode_ReadsPoliciesFromStream` + round-trip test

The Go commit adds a **stateful, incremental** `Decoder` (one policy per `Decode()` call, EOF signaled). The C# `static Decoder` is a bulk "read all" API — it does **not** have a stateful, one-at-a-time counterpart. The `PolicyEncoder` class is exactly that stateful counterpart for encoding.

**The gap:** There is no `PolicyDecoder` class in C#. We need to add one.

---

## Go → C# Idiom Mapping

| Go | C# |
|---|---|
| `type Decoder struct { dec *parser.Decoder }` | `public sealed class PolicyDecoder` (class, not record — holds mutable reader state) |
| `NewDecoder(r io.Reader) *Decoder` | `public PolicyDecoder(TextReader reader)` constructor |
| `Decode(p *Policy) error` returning `io.EOF` at end | `public bool TryDecode(out Policy? policy)` — returns `false` at EOF, idiomatic C# try-pattern |
| `io.Reader` | `System.IO.TextReader` (matches `PolicyEncoder`'s use of `TextWriter`) |
| `parser.Decoder` (internal tokenizing decoder) | `CedarParser.ParsePolicies(string)` — parse accumulated text; OR read one policy boundary at a time |
| `NewPolicyFromAST(...)` | `new Policy(ast)` via existing internal constructor path |

---

## Implementation Plan

### File 1: `src/Cedar.Core/Decoder.cs` (MODIFY — append `PolicyDecoder` class)

**Current content ends at line 17.** Append a new `PolicyDecoder` sealed class to the same file (matching the `Encoder.cs` pattern where both `static Encoder` and `sealed PolicyEncoder` coexist).

**Key design decision on incremental parsing:**
`CedarParser.ParsePolicies` tokenizes the whole input at once. To support incremental reads, `PolicyDecoder` must buffer text from the `TextReader` and detect policy boundaries. A Cedar policy ends with `;` at the top level (after the closing `)`). The simplest correct approach:

- Use `Policy.UnmarshalCedarList(buffered)` to parse all remaining text each time, but that defeats streaming.
- **Preferred:** Read the full remaining text once (lazy, on first call), split into individual policy strings via `CedarParser.ParsePolicies`, cache the array, and walk an index. This matches the actual parser architecture without reimplementing tokenization.

```csharp
public sealed class PolicyDecoder
{
    private readonly TextReader _reader;
    private Policy[]? _policies;
    private int _index;

    public PolicyDecoder(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    public bool TryDecode(out Policy? policy)
    {
        if (_policies is null)
        {
            string text = _reader.ReadToEnd();
            _policies = string.IsNullOrWhiteSpace(text)
                ? []
                : Policy.UnmarshalCedarList(text);
        }

        if (_index < _policies.Length)
        {
            policy = _policies[_index++];
            return true;
        }

        policy = null;
        return false;
    }
}
```

**Usings needed:** `System` (already present), `System.IO` (already present) — no new usings required.

**Target location:** Append after the closing `}` of `static class Decoder` in `src/Cedar.Core/Decoder.cs` (currently line 17).

---

### File 2: `test/Cedar.Tests/Policy/StreamTests.cs` (MODIFY — append test methods)

**Current file has tests ending around line 130.** Add new `[Fact]` methods to the existing `StreamTests` class.

**Tests to add:**

```csharp
[Fact]
public void PolicyDecoder_TryDecode_ReadsPoliciesSequentially()
{
    const string input = "permit(principal, action, resource);\nforbid(principal, action, resource);\n";
    using StringReader reader = new(input);
    PolicyDecoder decoder = new(reader);

    bool got0 = decoder.TryDecode(out Policy? policy0);
    bool got1 = decoder.TryDecode(out Policy? policy1);
    bool got2 = decoder.TryDecode(out Policy? policy2);

    Assert.True(got0);
    Assert.NotNull(policy0);
    Assert.Equal("permit(principal, action, resource);", policy0.MarshalCedar());

    Assert.True(got1);
    Assert.NotNull(policy1);
    Assert.Equal("forbid(principal, action, resource);", policy1.MarshalCedar());

    Assert.False(got2);
    Assert.Null(policy2);
}

[Fact]
public void PolicyDecoder_TryDecode_EmptyReader_ReturnsFalse()
{
    using StringReader reader = new(string.Empty);
    PolicyDecoder decoder = new(reader);

    bool got = decoder.TryDecode(out Policy? policy);

    Assert.False(got);
    Assert.Null(policy);
}

[Fact]
public void PolicyDecoder_Constructor_NullReader_Throws()
{
    Assert.Throws<ArgumentNullException>(() => new PolicyDecoder(null!));
}
```

**Usings already in file:** `System`, `System.IO`, `System.Text`, `Cedar.Core`, `Xunit` — no additions needed.

---

## Acceptance Criteria

1. `PolicyDecoder` class exists in `Cedar.Core` namespace in `src/Cedar.Core/Decoder.cs`
2. Constructor accepts `TextReader`, throws `ArgumentNullException` on null
3. `TryDecode(out Policy? policy)` returns `true` + populated policy while policies remain; returns `false` + null at end
4. Sequential calls walk policies in source order
5. Three new `[Fact]` tests in `StreamTests` all pass
6. `dotnet build cedar-dotnet.sln` succeeds with zero warnings
7. `dotnet test cedar-dotnet.sln` passes

---

## Files to Touch (exhaustive list)

| File | Action |
|---|---|
| `src/Cedar.Core/Decoder.cs` | Append `PolicyDecoder` sealed class after existing `static Decoder` class |
| `test/Cedar.Tests/Policy/StreamTests.cs` | Append 3 `[Fact]` methods inside existing `StreamTests` class |

**No new files needed. No project file changes needed.**
