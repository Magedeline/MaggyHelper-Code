using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using CelesteMountainCamera = global::Celeste.MountainCamera;
using CelesteMountainRenderer = global::Celeste.MountainRenderer;
using CelesteOverworld = global::Celeste.Overworld;
using CelesteSaveData = global::Celeste.SaveData;

namespace MaggyHelper;

/// <summary>
/// Overworld mountain/Oui hooks for the Desolo Zantas level set.
/// Adapted from the CrossoverCollab pattern and scoped to this mod.
/// </summary>
public static class DesoloZantasOuiHooks
{
    private static bool _hooked;
    private const float EaseDuration = 3f;
    private const float FocusThresholdZ = 6.5f;
    private const float FarThresholdMaxZ = 8f;
    private const float FarThresholdMinZ = -4f;

    private static readonly CelesteMountainCamera FocusCamera = new CelesteMountainCamera()
    {
        Position = new Vector3(0.9029947f, 1.2620742f, 6.399421f),
        Rotation = new Quaternion(0.057743218f, -0.2076822f, -0.01228193f, 0.9764133f)
    };

    private static readonly CelesteMountainCamera FarCamera = new CelesteMountainCamera()
    {
        Position = new Vector3(5.277999f, 2.36f, -3.9000018f),
        Rotation = new Quaternion(-0.03054283f, 0.9360384f, 0.08399759f, -0.34035814f)
    };

    public static void Load()
    {
        if (_hooked)
            return;

        _hooked = true;
        On.Celeste.MountainRenderer.Update += OnMountainRendererUpdate;
        IL.Celeste.Overworld.Update += ModOverworldUpdate;
    }

    public static void Unload()
    {
        if (!_hooked)
            return;

        _hooked = false;
        On.Celeste.MountainRenderer.Update -= OnMountainRendererUpdate;
        IL.Celeste.Overworld.Update -= ModOverworldUpdate;
    }

    private static void OnMountainRendererUpdate(
        global::On.Celeste.MountainRenderer.orig_Update orig,
        CelesteMountainRenderer self,
        Scene scene)
    {
        orig(self, scene);

        if (!IsDesoloLevelSet())
            return;

        if (self?.Model == null)
            return;

        CelesteMountainCamera targetCamera = ResolveTargetCamera(self);

        if (self.Camera.Position.Z > FarThresholdMaxZ || self.Camera.Position.Z < FarThresholdMinZ)
        {
            self.Model.Camera = FarCamera;
        }
        else if (self.Camera.Position.Z > FocusThresholdZ)
        {
            self.Model.Camera = targetCamera;
        }

        self.EaseCamera(0, targetCamera, EaseDuration, true);
    }

    private static CelesteMountainCamera ResolveTargetCamera(CelesteMountainRenderer renderer)
    {
        try
        {
            int areaId = renderer.Area;
            if (areaId >= 0 && areaId < AreaData.Areas.Count)
            {
                AreaData area = AreaData.Get(areaId);
                if (AreaModeExtender.IsOurMap(area) && area?.MountainSelect != null)
                {
                    return area.MountainSelect;
                }
            }
        }
        catch
        {
            // Fallback to the static camera when area data is unavailable.
        }

        return FocusCamera;
    }

    private static void ModOverworldUpdate(ILContext il)
    {
        ILCursor cursor = new(il);
        if (cursor.TryGotoNext(
            MoveType.After,
            instr => instr.MatchCallvirt<CelesteOverworld>("IsCurrent")))
        {
            cursor.EmitDelegate<Func<bool, bool>>(MusicCheck);
        }
    }

    private static bool MusicCheck(bool orig)
    {
        if (orig)
            return true;

        return IsDesoloLevelSet();
    }

    private static bool IsDesoloLevelSet()
    {
        string levelSet = CelesteSaveData.Instance?.LevelSet ?? string.Empty;
        return levelSet.StartsWith("DesoloZantas", StringComparison.OrdinalIgnoreCase)
            || levelSet.StartsWith("Maggy", StringComparison.OrdinalIgnoreCase)
            || levelSet.StartsWith("MaggyHelper", StringComparison.OrdinalIgnoreCase);
    }
}