using Microsoft.Xna.Framework;

namespace MaggyHelper.Extensions.Kirby;

/// <summary>
/// Lonn-facing alias entity that routes to KirbyPlayerExtension runtime behavior.
/// This keeps map content aligned with the KirbyPlayerCore architecture naming.
/// </summary>
[CustomEntity("MaggyHelper/KirbyPlayerCore")]
[Tracked]
public sealed class KirbyPlayerCoreEntity : KirbyPlayerExtension
{
    public KirbyPlayerCoreEntity(EntityData data, Vector2 offset)
        : base(data, offset)
    {
    }
}
