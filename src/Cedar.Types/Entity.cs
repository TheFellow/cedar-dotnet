namespace Cedar.Types;

public sealed record Entity(EntityUid Uid, EntityUidSet Parents, CedarRecord Attributes, CedarRecord Tags);
