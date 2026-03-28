using Cedar.Types;

namespace Cedar.Core;

public sealed record Request(EntityUid Principal, EntityUid Action, EntityUid Resource, CedarRecord? Context);
