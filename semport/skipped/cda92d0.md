# cda92d0 — Merge pull request #157 (reject IPv4-mapped IPv6 in IsLoopback/IsMulticast)

- **Author:** Kevin Jamieson <115490966+kjamieson-sdm@users.noreply.github.com>
- **Date:** 2026-06-01T14:01:07-07:00
- **Subject:** Merge pull request #157 from strongdm/kjamieson/is-loopback-exclude-4-in-6

## Rationale

This is the GitHub merge commit for PR #157 (parents `d8aed25` and `9005ab6`).
The only semantic change on the branch — `9005ab6` — was already ported in our
prior commit `2112714 semport: implement 9005ab6 - reject IPv4-mapped IPv6 in
IPAddr loopback/multicast` (see `src/Cedar.Types/CedarIpAddress.cs` `IsLoopback`
and `IsMulticast`, both of which return `false` when `Address.IsIPv4MappedToIPv6`).
The merge itself adds no additional diff over its second parent, so there is
nothing left to port.

## Files touched

- (merge commit only; underlying changes were in `types/ipaddr.go` and
  `types/ipaddr_test.go` via `9005ab6`)
