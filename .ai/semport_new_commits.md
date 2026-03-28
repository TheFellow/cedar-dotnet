# Semport: Next New Commit to Process

## Commit: 8a95a23
**Timestamp:** 2024-10-01T10:42:01-07:00

**Message:**
internal/eval: remove the inCache

We don't currently have a benchmark that's telling us that we need this cache. In fact, several of our benchmarks show that for large, shallow entity graphs, the inCache actually slows down authorizations and batch evaluations because of all the extra allocation that goes on to build the map.

For deep entity graphs, such a cache might be useful, but I think instead that we'll try to put such a cache in the entity graph itself by keeping track of the transitive closure of every entity's parents. That way, the cache will be effective across multiple authorizations.

Signed-off-by: Patrick Jakubowski <patrick.jakubowski@strongdm.com>
