# Sprint Plan Critique: Cedar-DotNet

This document provides a comprehensive critique of the Claude and Codex sprint plan drafts for the `cedar-dotnet` port, evaluated against the user's explicit preferences: .NET 9.0+, multi-project solution, .NET naming conventions (`EntityUid` not `EntityUID`), full feature parity (schema+batch), and a granularity of ~8-10 fine-grained sprints.

## 1. Claude Draft Assessment

**Sprint Granularity:** 10 Sprints. Perfectly hits the 8-10 sprint target with highly granular, focused iterations.
**Useful Deliverables:** Yes. Every sprint produces a testable, independently verifiable increment (e.g., primitives -> entities -> AST -> parsing -> JSON -> evaluation).

### Strengths
- **Extreme Detail:** Provides a rigorous, file-by-file breakdown mapping directly to the Go reference implementation, including LOC counts and exact Go package equivalents.
- **Architectural Clarity:** Clearly separates the internal hierarchical AST from the public fluent builder, matching the Go design well.
- **Phased Evaluation:** Breaks the evaluator into multiple manageable sprints (Core Eval in Sprint 6, Constant Folding in Sprint 7), reducing risk.

### Weaknesses
- **Missed Naming Convention Preference:** Explicitly violates the user preference by stating "Keep `EntityUID` (not `EntityUid`)". 
- **Overly Strict Base Type:** The decision to avoid value structs entirely in favor of sealed record classes for all `CedarValue` types simplifies pattern matching but guarantees heap allocations for every boolean and integer.

### Gaps in Risk Analysis
- **Concurrency / Thread Safety:** Fails to explicitly highlight the thread-safety requirements of the compiled `IEvaluator` tree. In a typical .NET web API, a single compiled `PolicySet` will be evaluated concurrently by many threads.
- **GC Pressure:** Does not analyze the performance impact of high allocation rates during deeply nested recursive descent parsing and evaluation.

### Missing Edge Cases
- **Stack Overflows:** Recursive descent parsing and recursive AST evaluation are vulnerable to `StackOverflowException` on maliciously deep policies. The draft mentions bounded parsing, but .NET lacks Go's goroutine stack expansion, making C# more susceptible to hard crashes here.
- **Decimal/IP String Formats:** Misses edge cases around canonicalizing IP addresses and ensuring `.NET` IP string formatting perfectly matches `netip.Addr` from Go.

### Definition of Done Completeness
- **Highly Complete:** Features exact test file targets, specific operator coverage checklists, and concrete requirements (e.g., short-circuit logic verification).

---

## 2. Codex Draft Assessment

**Sprint Granularity:** 8 Sprints. Fits the target range, but groups too much functionality into the middle sprints.
**Useful Deliverables:** Yes, but the pacing is uneven. Sprints 1-4 are foundational, but true end-to-end authorization isn't achieved until Sprint 5/6.

### Strengths
- **Caught User Preferences:** Correctly identifies and mandates idiomatic .NET casing (`EntityUid`, `SchemaDocument`), perfectly aligning with user instructions.
- **Pragmatic Architecture:** Acknowledges the performance tradeoff of the `CedarValue` sealed record hierarchy upfront and proposes optimizing it later if needed.
- **Clean Grouping:** Neatly groups the experimental features, batch processing, and DOT export into a cohesive sidecar sprint.

### Weaknesses
- **Overloaded Sprint 6:** Sprint 6 attempts to deliver the remainder of the core evaluators, extension functions, constant folding, partial evaluation, *and* the conformance corpus all at once. This is a massive bottleneck and a high-risk integration point.
- **Lack of Detail:** Much lighter on specific file breakdowns, phase planning, and exact Go package mappings compared to Claude.

### Gaps in Risk Analysis
- **Implicit vs. Explicit JSON:** Does not highlight the risk of parsing the Cedar JSON format, specifically the ambiguity between implicit `{"type", "id"}` and explicit `{"__entity": ...}` formats.
- **Decimal Representation:** Misses the critical detail that Cedar's decimal type must be implemented as a `long` multiplied by 10,000 to perfectly match the Go reference implementation's overflow and precision semantics.

### Missing Edge Cases
- **Rust String Unquoting:** Completely omits the need to port Go's internal Rust-style string unquoting logic, which is required for 100% corpus parity.
- **Fail-Safe Semantic Drift:** Does not explicitly call out the edge cases where an extension function type mismatch must yield an evaluation error rather than a hard crash.

### Definition of Done Completeness
- **Adequate but Vague:** Relies on vague statements like "README-style examples authorize correctly" rather than the strict, verifiable operator checklists provided by Claude.

---

## Conclusion & Recommendation

**Winner:** The **Claude Draft** provides a vastly superior technical roadmap due to its depth, exact Go-to-C# mappings, and superior pacing (splitting core evaluation and constant folding into separate sprints). 

**Corrective Action for the Final Plan:**
To create the perfect `SPRINT-PLAN-GEMINI-DRAFT.md`, we should:
1. Adopt Claude's 10-sprint structure, rigorous DoD, and file-level detail.
2. Override Claude's naming conventions with Codex's correct `.NET` idiomatic casing (e.g., `EntityUid`, `PolicySet`).
3. Incorporate Codex's upfront acknowledgement of the `CedarValue` allocation risks.
4. Add explicit risk mitigations for C# `StackOverflowException` (via depth limiters in the parser and evaluator) and concurrency guarantees for cached evaluators.