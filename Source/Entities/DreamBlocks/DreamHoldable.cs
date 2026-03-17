namespace Celeste.Mod.MaggyHelper.Entities;

/// <summary>
/// A <see cref="Holdable"/> that supports interaction with dream blocks.
/// This is a local stub replacing CommunalHelper's internal DreamHoldable
/// to allow compilation without requiring CommunalHelper internals access.
/// </summary>
public sealed class DreamHoldable : Holdable
{
    public bool AllowDreamDash { get; set; } = true;

    public DreamHoldable(Collider dreamCollider, float pickup, Action onActivate, Action onDeactivate)
        : base(pickup)
    {
    }
}
