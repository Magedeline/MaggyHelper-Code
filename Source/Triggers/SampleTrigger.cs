namespace MaggyHelper.Triggers
{
    [CustomEntity(IngesteConstants.EntityNames.SAMPLE_TRIGGER)]
    [HotReloadable]
    public class SampleTrigger : Trigger
    {
        public SampleTrigger(EntityData data, Vector2 offset) : base(data, offset)
        {
            // TODO: read properties from data
        }
    }
}



