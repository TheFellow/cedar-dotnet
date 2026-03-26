namespace Cedar.Types;

public interface IEntityGetter
{
    bool TryGet(EntityUid uid, out Entity entity);
}
