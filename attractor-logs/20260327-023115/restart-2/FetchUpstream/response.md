Done. The earliest 'new' commit has been written to `.ai/semport_new_commits.md`:

- **Short SHA:** `db1d330`
- **Date:** `2026-02-09T10:32:46-08:00`
- **Summary:** `schema: create a public schema parser and programmatic construction implementation`

This commit replaces the experimental schema package in cedar-go with a new public schema API supporting both human-readable (`.cedarschema`) and JSON formats, programmatic AST construction via `schema/ast`, and type resolution via `schema/resolved`. The key changes include a cleaner type hierarchy and new public methods (`UnmarshalCedar`, `MarshalCedar`, `UnmarshalJSON`, `MarshalJSON`, `Resolve`) on the top-level `schema.Schema` type.