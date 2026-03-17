namespace MaggyHelper.Helpers;

public static class TimeRateModifierExtensions
{
    public static float CurrentTimeRate(this TimeRateModifier modifier)
    {
        return modifier.Enabled ? modifier.Multiplier : 1f;
    }

    public static void SetTimeRateMultiplier(this TimeRateModifier modifier, float multiplier)
    {
        modifier.Multiplier = multiplier;
        modifier.Enabled = Math.Abs(multiplier - 1f) > 0.0001f;
    }

    public static void ResetTimeRateMultiplier(this TimeRateModifier modifier)
    {
        modifier.Multiplier = 1f;
        modifier.Enabled = false;
    }
}