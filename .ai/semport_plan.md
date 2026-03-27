PORT

## Commit Summary
`47584d0` — Fix Cedar marshal operator parenthesization for associativity

The upstream Go fix corrects how binary operators parenthesize their children when serializing a Cedar AST back to text. The bug caused incorrect (or missing) parentheses when operators of the same precedence were nested, producing semantically different or invalid Cedar policy text.

**Rule:**
- **Left-associative** ops (`+`, `-`, `*`, `&&`, `||`): left child at same precedence `p`, right child at `p+1`. This ensures `(a - b) - c` is not incorrectly round-tripped as `a - b - c - d`.
- **Non-associative** relation/keyword ops (`==`, `!=`, `<`, `<=`, `>`, `>=`, `in`, `has`, `like`, `is`, `is-in`): both children at `p+1`. This ensures `(a == b) == c` is preserved with parens rather than becoming the invalid `a == b == c`.

## Semantic Analysis of C# `CedarWriter.cs`

File: `src/Cedar.Core/Internal/Parser/CedarWriter.cs`

Comparing the current C# code against the upstream fix:

| Operator | Left prec | Right prec | Correct? |
|---|---|---|---|
| `<`, `<=`, `>`, `>=`, `==`, `!=`, `in` | `PrecRel+1` | `PrecRel+1` | ✅ Already correct |
| `is`, `is-in`, `has`, `like` | `PrecRel+1` | `PrecRel+1` | ✅ Already correct |
| `+` | `PrecAdd` | `PrecAdd` (unless right is NodeSub) | ⚠️ Partially correct — special-cased for NodeSub but not general |
| `-` | `PrecAdd` | `PrecAdd+1` | ✅ Already correct |
| `*` | `PrecMult` | `PrecMult` | ❌ BUG — right should be `PrecMult+1` |
| `&&` | `PrecAnd` | `PrecAnd` | ❌ BUG — right should be `PrecAnd+1` |
| `\|\|` | `PrecOr` | `PrecOr` | ❌ BUG — right should be `PrecOr+1` |

**Three bugs to fix, one simplification possible:**

### Bug 1 — `NodeMult` (line ~249): right child uses same precedence
```csharp
// BEFORE (buggy):
case NodeMult mult:
    WriteNode(builder, mult.Left, PrecMult);
    builder.Append(" * ");
    WriteNode(builder, mult.Right, PrecMult);   // BUG: should be PrecMult + 1
    break;
```

### Bug 2 — `NodeAnd` (line ~161): right child uses same precedence
```csharp
// BEFORE (buggy):
case NodeAnd and:
    WriteNode(builder, and.Left, PrecAnd);
    builder.Append(" && ");
    WriteNode(builder, and.Right, PrecAnd);    // BUG: should be PrecAnd + 1
    break;
```

### Bug 3 — `NodeOr` (line ~155): right child uses same precedence
```csharp
// BEFORE (buggy):
case NodeOr or:
    WriteNode(builder, or.Left, PrecOr);
    builder.Append(" || ");
    WriteNode(builder, or.Right, PrecOr);      // BUG: should be PrecOr + 1
    break;
```

### Simplification — `NodeAdd` (lines ~231-242): special NodeSub case not needed
The current code special-cases `NodeSub` as the right child of `NodeAdd` to bump its precedence. The upstream fix uniformly applies `p+1` to the right operand of all left-associative ops, which makes the special case unnecessary. Simplify to always pass `PrecAdd+1` for the right operand.

## Concrete Port Tasks

**File:** `src/Cedar.Core/Internal/Parser/CedarWriter.cs`

1. **Fix `NodeOr`** (~line 155–159): change `WriteNode(builder, or.Right, PrecOr)` → `WriteNode(builder, or.Right, PrecOr + 1)`

2. **Fix `NodeAnd`** (~line 160–164): change `WriteNode(builder, and.Right, PrecAnd)` → `WriteNode(builder, and.Right, PrecAnd + 1)`

3. **Fix `NodeMult`** (~line 248–252): change `WriteNode(builder, mult.Right, PrecMult)` → `WriteNode(builder, mult.Right, PrecMult + 1)`

4. **Simplify `NodeAdd`** (~line 230–242): remove the special-case `NodeSub` check; always pass `PrecAdd + 1` for the right operand.

5. **Add regression tests** in `test/Cedar.Tests/` covering:
   - `(a || b) || c` round-trips with correct parens (right-nesting forces parens)
   - `(a && b) && c` same
   - `(a * b) * c` same
   - `(a + b) + c` same
   - `(a == b) == c` both sides parenthesized
   - `(a - b) - c` left is unparenthesized, right is parenthesized
