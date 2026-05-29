# d8aed25 — Reject IPv6 zone identifiers in Cedar IP parsing

- **Author:** Patrick Jakubowski (merge of Ryan Harris' commit 244a332)
- **Date:** 2026-05-29
- **Subject:** Merge pull request #156 — types: Reject IPv6 zone identifiers in ip parser

## Rationale

This Go-side fix adds an explicit zone-identifier rejection inside `ParseIPAddr`
after `netip.ParseAddr` succeeds, since Go's parser otherwise swallows `%zone`
suffixes (and even CIDR-looking suffixes interpreted as zones). The C# port
already rejects zone identifiers up front by checking for `%` in the address
text before calling `IPAddress.TryParse` — see
`src/Cedar.Types/CedarIpAddress.cs:51-54` (throws `FormatException("IPv6 zone
identifiers are not supported.")`).

The four new Go test cases (`fe80::1%eth0`, `fe80::1%1`, `fe80::1%eth0/64`,
`2001:db8::1%eth0`) are already present and asserted in
`test/Cedar.Tests/Types/CedarIpAddressTests.cs:79-98`. No port action required.

## cedar-go files touched

- `types/ipaddr.go`
- `types/ipaddr_test.go`
