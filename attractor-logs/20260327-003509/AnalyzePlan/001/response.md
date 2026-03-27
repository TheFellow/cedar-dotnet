`.ai/semport_plan.md` has been written with disposition **PORT**. Here's the decision summary:

---

**Decision: PORT**

The PR introduces `UnmarshalCedar` — a genuine semantic capability that parses Cedar text format (`Namespace::Type::"id"`) back into an `EntityUID`. While the Go `encoding.BinaryMarshaler`/`BinaryUnmarshaler` interface wrappers are Go-idiom-specific (no .NET equivalent), the underlying parse operation is a real round-trip gap in our C# implementation.

**What gets ported:**
- `EntityUid.TryParseCedar(string, out EntityUid)` — static parse from Cedar text format
- `EntityUid.ParseCedar(string)` — throwing convenience overload
- xUnit tests covering valid round-trips and 7 invalid-input cases from the Go test table

**What is skipped:** `MarshalBinary`/`UnmarshalBinary` — these satisfy Go-specific interfaces with no .NET equivalent.