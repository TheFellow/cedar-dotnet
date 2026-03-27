# Semport: Earliest New Commit

**Short SHA:** 47584d0
**Timestamp:** 2026-03-18T15:41:46-06:00

## Commit Message

Fix Cedar marshal operator parenthesization for associativity

Binary operators were not parenthesizing children correctly based on
associativity:

- Non-associative relation ops (==, !=, <, <=, >, >=, in) and keyword
  ops (has, like, is, is-in) need to parenthesize both operands at the
  same precedence, since (a == b) == c is valid but a == b == c is not.

- Left-associative ops (+, -, *, &&, ||) need to parenthesize their
  right operand at the same precedence, since (a - b) - (c - d) must
  not be flattened to a - b - c - d which changes semantics.

Split marshalInfixBinaryOp to take separate left/right precedence
levels. Left-associative ops pass (p, p+1) and non-associative ops
pass (p+1, p+1).

Signed-off-by: Phil Hassey <phil@strongdm.com>

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
