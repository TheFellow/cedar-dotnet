# Finalized Port Plan: 8a95a23 — Remove `InCache` from `EvalEnv`

## Summary
Remove the per-evaluation `InCache` dictionary from `EvalEnv` and collapse the now-trivial `EntityInOne` helper, exactly mirroring the upstream cedar-go commit 8a95a23.

---

## File 1: `src/Cedar.Core/Internal/Eval/EvalEnv.cs`

### Change: Remove `InCache` property and its `using`

**Current file (14 lines):**
```csharp
using System.Collections.Generic;   // line 1 — remove (only used for Dictionary<>)
using Cedar.Types;                  // line 2 — keep

namespace Cedar.Core.Internal.Eval;

internal sealed record EvalEnv(IEntityGetter Entities, ICedarData Principal, ICedarData Action, ICedarData Resource, ICedarData? Context)
{
    internal Dictionary<(EntityUid Lhs, EntityUid Rhs), bool> InCache { get; } = [];   // line 8 — remove entire line

    public static EvalEnv FromRequest(IEntityGetter entities, Request request)
    {
        return new EvalEnv(entities, request.Principal, request.Action, request.Resource, request.Context);
    }
}
```

**Target file after edit:**
```csharp
using Cedar.Types;

namespace Cedar.Core.Internal.Eval;

internal sealed record EvalEnv(IEntityGetter Entities, ICedarData Principal, ICedarData Action, ICedarData Resource, ICedarData? Context)
{
    public static EvalEnv FromRequest(IEntityGetter entities, Request request)
    {
        return new EvalEnv(entities, request.Principal, request.Action, request.Resource, request.Context);
    }
}
```

**edit_file calls needed:**
1. Replace `"using System.Collections.Generic;\nusing Cedar.Types;"` → `"using Cedar.Types;"`
2. Replace the `InCache` property line + blank line → empty (remove them)

---

## File 2: `src/Cedar.Core/Internal/Eval/Evaluators/MembershipEvaluators.cs`

### Change A: `InOperator.Contains` — update call sites to pass `env.Entities`

**Lines 42–50 (current):**
```csharp
public static bool Contains(EvalEnv env, EntityUid entity, ICedarData query)
{
    return query switch
    {
        EntityUid parent => EntityInOne(env, entity, parent),       // pass env.Entities instead
        CedarSet set => EntityInSet(env, entity, set),              // pass env.Entities instead
        _ => throw new EvalException($"expected set or entity, got {EvalErrors.TypeName(query)}")
    };
}
```

**Lines 42–50 (target):**
```csharp
public static bool Contains(EvalEnv env, EntityUid entity, ICedarData query)
{
    return query switch
    {
        EntityUid parent => EntityInOne(env.Entities, entity, parent),
        CedarSet set => EntityInSet(env.Entities, entity, set),
        _ => throw new EvalException($"expected set or entity, got {EvalErrors.TypeName(query)}")
    };
}
```

### Change B: Replace `EntityInOne` — drop cache, change signature to `IEntityGetter`

**Lines 52–63 (current):**
```csharp
private static bool EntityInOne(EvalEnv env, EntityUid entity, EntityUid parent)
{
    (EntityUid Lhs, EntityUid Rhs) key = (entity, parent);
    if (env.InCache.TryGetValue(key, out bool cached))
    {
        return cached;
    }

    bool result = EntityInEntity(env.Entities, entity, parent);
    env.InCache[key] = result;
    return result;
}
```

**Lines 52–63 (target):**
```csharp
private static bool EntityInOne(IEntityGetter entities, EntityUid entity, EntityUid parent)
{
    return EntityInEntity(entities, entity, parent);
}
```

### Change C: `EntityInSet` — update signature to `IEntityGetter`

**Lines 65–77 (current):**
```csharp
private static bool EntityInSet(EvalEnv env, EntityUid entity, CedarSet set)
{
    foreach (ICedarData candidate in set)
    {
        EntityUid parent = TypeConversion.ValueToEntity(candidate);
        if (EntityInOne(env, entity, parent))
        {
            return true;
        }
    }

    return false;
}
```

**Lines 65–77 (target):**
```csharp
private static bool EntityInSet(IEntityGetter entities, EntityUid entity, CedarSet set)
{
    foreach (ICedarData candidate in set)
    {
        EntityUid parent = TypeConversion.ValueToEntity(candidate);
        if (EntityInOne(entities, entity, parent))
        {
            return true;
        }
    }

    return false;
}
```

**Note:** `using System.Collections.Generic;` at line 1 of this file must **stay** — `HashSet<EntityUid>` and `Stack<EntityUid>` in `EntityInEntity` (lines 86–87) still require it.

---

## No Other Files Need Changes

- `InEvaluator.Eval` (line 12): calls `InOperator.Contains(env, ...)` — signature unchanged, no edit needed.
- `IsInEvaluator.Eval` (line 36): same — no edit needed.
- `PartialEvaluator.cs`: uses `EvalEnv` but never touches `InCache` directly — no edit needed.
- `ConstantFolder.cs`: constructs `EvalEnv` via positional constructor — no edit needed (property initializer was auto-init, no ctor arg).

---

## Acceptance Criteria

1. **`EvalEnv.cs`** has no `InCache` property and no `using System.Collections.Generic`.
2. **`MembershipEvaluators.cs`** `EntityInOne` and `EntityInSet` accept `IEntityGetter` instead of `EvalEnv`; no `InCache` references remain anywhere in `src/`.
3. `grep -r "InCache" src/` returns no results.
4. `dotnet build cedar-dotnet.sln` succeeds with zero errors/warnings.
5. `dotnet test cedar-dotnet.sln` passes — all `in` expression tests (conformance corpus + unit) remain green.
6. No new tests required: this is a pure performance/allocation removal with identical observable behavior.

---

## Go → C# Pattern Map (for reference)

| Go | C# |
|---|---|
| `map[(EntityUID, EntityUID)]bool` | `Dictionary<(EntityUid, EntityUid), bool>` |
| `Env` struct (value type) | `EvalEnv` sealed record (reference type, but immutable) |
| Remove field from struct → zero alloc | Remove auto-property init `= []` → no `Dictionary` heap alloc per `EvalEnv` |
| `entityInSet(env *Env, ...)` | `EntityInSet(EvalEnv env, ...)` → `EntityInSet(IEntityGetter entities, ...)` |
