5a500db 2026-03-18T15:41:46-06:00

Fix Pattern MarshalCedar to use Cedar-compatible escaping

Pattern.MarshalCedar() was using strconv.Quote which produces
Go-style escapes (\x00, \v) that are not valid in Cedar. Switch
to rust.EscapeString for Cedar-compatible escapes (\0, \u{b}).

Signed-off-by: Phil Hassey <phil@strongdm.com>

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
