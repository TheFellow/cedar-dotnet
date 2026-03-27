d267346
2026-03-18T15:41:46-06:00
Fix canMarshalAsIdent for empty strings and reserved keywords

canMarshalAsIdent("") returned true because the loop over an empty
string iterates zero times. It also accepted reserved keywords like
"true", "false", "if", "in", etc., producing invalid Cedar like
`context.true` instead of `context["true"]`.

Add an early return for empty strings and reserved keywords, using
the existing IsReservedKeyword helper.

Signed-off-by: Phil Hassey <phil@strongdm.com>

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
