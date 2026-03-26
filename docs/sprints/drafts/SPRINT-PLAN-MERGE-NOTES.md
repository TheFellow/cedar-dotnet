# Cedar-DotNet Sprint Plan Merge Notes

## Critique Consensus

All three critiques converge on the same core findings:

### Claude Draft Strengths Adopted
- Strongest scope coverage — only draft covering schema, batch, experimental, benchmarks, packaging
- File-level implementation detail makes sprints directly executable
- Cumulative test count tracking across sprints (687+ target)
- Two-level AST design (internal nodes + public fluent builder)
- Dedicated constant folding + conformance corpus sprint
- MapSet port for hash-based set operations
- Rust string unquoting compatibility layer

### Codex Draft Strengths Adopted
- Correct .NET naming conventions (`EntityUid`, `PolicySet`, `CedarValue`)
- `Directory.Build.props` + `Directory.Packages.props` scaffolding
- Separate evaluator files by concern (Boolean, Comparison, Arithmetic, Collection, etc.)
- `CedarValue` allocation risk acknowledged upfront
- Security section in every sprint
- Thoughtful open questions with downstream implications

### Gemini Draft Strengths Adopted
- `FrozenSet<T>` recommendation for immutable sets
- FsCheck for property-based parser round-trip testing
- Set/Record equality risk callout
- Dedicated parser sprint (not bundled with AST)

## Valid Critiques Accepted

| Critique | Source | Action |
|----------|--------|--------|
| Wrong naming (EntityUID) | Codex, Gemini critiques of Claude | Use .NET conventions throughout |
| Single assembly conflicts with user preference | Codex critique of Claude | Multi-project solution per user selection |
| Sprint 6 too large (eval + extensions + auth + corpus) | All three critiques | Split into eval+auth (Sprint 6) and corpus+folding (Sprint 7) |
| Sprint 9 overloaded (batch + experimental + DOT) | Codex critique | Batch gets own sprint; experimental+benchmarks separate |
| Missing CancellationToken strategy | Claude critique of Codex | Address in Sprint 1 design decisions |
| Missing isEmpty(), extended has, tag operators | Claude critique of Codex | Added to operator checklist |
| Partial evaluation scope unclear | Codex critique of Claude | Include — Go ships it in x/exp/batch |
| STJ polymorphic serialization risk | Claude critique of Codex | Named as explicit risk in Sprint 5 |
| Rust string unquoting omitted | Gemini critique of Codex | Added to parser sprint |
| Stack overflow depth limiting | Gemini critique of both | Added to parser and evaluator security |
| Thread safety discussion missing | Claude critique of Codex | Added to Sprint 1 design decisions |
| Corpus pass rate not specified | Claude critique of Codex | 100% corpus pass rate in DoD |
| Test count targets too low (327) | Claude critique of Codex | Target 500+ unit tests + full corpus |

## Critiques Rejected

| Critique | Source | Reason |
|----------|--------|--------|
| FluentAssertions is unnecessary | Claude critique of Gemini | Agree — xUnit built-in assertions + custom helpers suffice |
| Result<T> is non-standard .NET | Claude critique of Gemini | Agree — use exceptions at parse boundary, diagnostic collection at eval |
| Over-proliferated assemblies (5+) | Codex critique of Gemini | User explicitly chose multi-project; we'll make it work |

## Interview Refinements Applied

1. **Multi-project solution** — Cedar.Types, Cedar.Ast, Cedar.Core (public), Cedar.Parser (internal), Cedar.Eval (internal)
2. **Full feature parity** — schema, batch, experimental all in scope, split into logical sprints
3. **.NET naming conventions** — `EntityUid`, `PolicyId`, `CedarIpAddress`, `PolicySet`
4. **~8-10 fine sprints** — Final plan: 9 sprints

## Final Sprint Sequence (9 Sprints)

| Sprint | Focus | Key Deliverable |
|--------|-------|-----------------|
| 001 | Bootstrap + primitive values | Solution builds, CedarBool/Long/String |
| 002 | Extended types + entities + serialization | Full Cedar type system |
| 003 | AST nodes + fluent builder | Programmatic policy construction |
| 004 | Tokenizer + parser + Cedar text | Parse arbitrary Cedar policies |
| 005 | Cedar JSON + Policy containers | Cedar JSON round-trips, PolicySet API |
| 006 | Evaluator + extensions + Authorize | Core authorization works |
| 007 | Constant folding + conformance corpus | Full core parity validated |
| 008 | Schema package | Cedar schema parsing (human + JSON) |
| 009 | Batch + experimental + benchmarks + packaging | Complete feature parity, release-ready |
