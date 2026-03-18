using MaggyHelper.Entities;

namespace MaggyHelper.Triggers
{
    /// <summary>
    /// Trigger to manage time freezing during the bridge collapse sequence.
    /// Place this as a large trigger box around the bridge to easily control when time freezes and unfreezes.
    /// Customize freezeStrength to adjust how slow time becomes (0.001 = nearly frozen, 1.0 = normal).
    /// </summary>
    [CustomEntity("MaggyHelper/BridgeFreezeTrigger")]
    [HotReloadable]
    public class BridgeFreezeTrigger : Trigger
    {
        private float freezeStrength;
        private Entities.Bridge bridge;

        public BridgeFreezeTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            // Freeze strength: 0.001 = almost frozen, 0.1 = slow motion, 1.0 = normal speed
            freezeStrength = data.Float(nameof(freezeStrength), 0.001f);
        }

        public override void OnEnter(global::Celeste.Player player)
        {
            base.OnEnter(player);
            
            var level = Scene as Level;
            if (level == null) return;

            // Find and freeze the bridge
            bridge = level.Tracker.GetEntity<Entities.Bridge>();
            if (bridge != null && bridge.TimeRateModifier != null)
            {
                bridge.TimeRateModifier.SetTimeRateMultiplier(freezeStrength);
            }
        }

        public override void OnLeave(global::Celeste.Player player)
        {
            base.OnLeave(player);
            
            // Unfreeze the bridge when leaving the trigger
            if (bridge != null && bridge.TimeRateModifier != null)
            {
                bridge.TimeRateModifier.ResetTimeRateMultiplier();
                bridge = null;
            }
        }

        /// <summary>
        /// Manually unfreeze time from external code
        /// </summary>
        public void UnfreezeNow()
        {
            if (bridge != null && bridge.TimeRateModifier != null)
            {
                bridge.TimeRateModifier.ResetTimeRateMultiplier();
            }
        }

        /// <summary>
        /// Manually freeze time from external code
        /// </summary>
        public void FreezeNow(float customStrength = -1f)
        {
            if (bridge != null && bridge.TimeRateModifier != null)
            {
                float strength = customStrength < 0f ? freezeStrength : customStrength;
                bridge.TimeRateModifier.SetTimeRateMultiplier(strength);
            }
        }
    }
}
