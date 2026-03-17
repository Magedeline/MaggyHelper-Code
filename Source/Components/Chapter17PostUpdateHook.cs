namespace MaggyHelper.Components;

/// <summary>
/// A PostUpdateHook that only fires when the current level is Chapter 17 (Epilogue).
/// </summary>
[Tracked(false)]
public class Chapter17PostUpdateHook : Component
{
    private static readonly string Chapter17SID = AreaModeExtender.BuildASideSID("17_Epilogue");

    public Action OnPostUpdate;

    public Chapter17PostUpdateHook(Action onPostUpdate)
        : base(active: false, visible: false)
    {
        OnPostUpdate = onPostUpdate;
    }

    /// <summary>
    /// Returns true if the current scene is a Level in Chapter 17.
    /// </summary>
    private bool IsChapter17()
    {
        return Scene is Level level
            && level.Session.Area.GetSID() == Chapter17SID;
    }

    /// <summary>
    /// Called by the engine after Update. Only invokes the callback in Chapter 17.
    /// </summary>
    public void PostUpdate()
    {
        if (IsChapter17())
        {
            OnPostUpdate?.Invoke();
        }
    }
}
