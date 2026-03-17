namespace MaggyHelper.HotReload;

/// <summary>
/// Example class demonstrating how to use Hot Code Reloading.
/// 
/// This class shows various patterns for making code hot-reloadable.
/// Try modifying the methods below while the game is running and
/// enable Hot Reload in the mod settings to see instant changes!
/// </summary>
[HotReloadable]
public class HotReloadExample : Entity
{
    #region Fields
    
    /// <summary>
    /// This field will be preserved during hot reload.
    /// Its value won't be lost when the code is recompiled.
    /// </summary>
    [PreserveOnReload]
    private int _preservedCounter = 0;
    
    /// <summary>
    /// This field will be reset to default on reload.
    /// </summary>
    private float _timer = 0f;
    
    private string _debugMessage = "Initial state";
    
    #endregion
    
    #region Constructor
    
    public HotReloadExample(Vector2 position) : base(position)
    {
        // NOTE: Constructors cannot be hot-reloaded!
        // Keep constructor logic minimal and put initialization
        // in Added() or use [OnHotReloaded] methods instead.
    }
    
    #endregion
    
    #region Hot-Reloadable Methods
    
    /// <summary>
    /// EXPERIMENT: Try changing this method while the game runs!
    /// 
    /// Examples to try:
    /// 1. Change the speed multiplier (0.5f, 2.0f, etc.)
    /// 2. Change the behavior (make it oscillate, pulse, etc.)
    /// 3. Add new visual effects
    /// </summary>
    public override void Update()
    {
        base.Update();
        
        // TRY CHANGING THIS VALUE!
        float speed = 1.0f;
        
        _timer += Engine.DeltaTime * speed;
        _preservedCounter++;
        
        // TRY CHANGING THIS CONDITION!
        if (_timer > 2.0f)
        {
            _timer = 0f;
            _debugMessage = $"Counter: {_preservedCounter}";
            
            // TRY UNCOMMENTING THIS LINE:
            // Logger.Log(LogLevel.Info, "HotReload", _debugMessage);
        }
    }
    
    /// <summary>
    /// EXPERIMENT: Modify how the entity is rendered!
    /// 
    /// Examples to try:
    /// 1. Change the color (Color.Blue, Color.Green, etc.)
    /// 2. Change the size
    /// 3. Add pulsing effects using _timer
    /// </summary>
    public override void Render()
    {
        base.Render();
        
        // TRY CHANGING THE COLOR!
        Color color = Color.Red;
        
        // TRY CHANGING THE SIZE!
        float size = 8f;
        
        // TRY MAKING IT PULSE!
        // float pulse = (float)Math.Sin(_timer * 3) * 0.5f + 0.5f;
        // size *= pulse;
        
        Draw.Rect(Position.X - size/2, Position.Y - size/2, size, size, color);
        
        // TRY ADDING MORE SHAPES!
        // Draw.Circle(Position, 12f, Color.Yellow, 16);
    }
    
    /// <summary>
    /// Custom method that can also be hot-reloaded.
    /// </summary>
    [HotReloadable]
    public string GetStatusMessage()
    {
        // TRY CHANGING THIS MESSAGE!
        return $"Hot Reload Example - Timer: {_timer:F2}, Counter: {_preservedCounter}";
    }
    
    #endregion
    
    #region Reload Callbacks
    
    /// <summary>
    /// This method is called automatically after hot reload.
    /// Use it to reinitialize anything that was lost.
    /// </summary>
    [OnHotReloaded]
    private void OnReloaded()
    {
        Logger.Log(LogLevel.Info, "HotReload", "HotReloadExample was reloaded!");
        _debugMessage = "Just reloaded!";
    }
    
    #endregion
}

/// <summary>
/// Example of a class that opts out of hot reload.
/// Use this for critical code that shouldn't be modified at runtime.
/// </summary>
[NoHotReload(Reason = "Critical initialization code")]
public class CriticalSystem
{
    public void Initialize()
    {
        // This code cannot be hot-reloaded
    }
}

/// <summary>
/// Example of implementing the IHotReloadCallback interface
/// for more control over the reload process.
/// </summary>
[HotReloadable(NotifyOnReload = true)]
public class AdvancedHotReloadExample : Entity, IHotReloadCallback
{
    private Dictionary<string, object> _savedState;
    
    public AdvancedHotReloadExample(Vector2 position) : base(position)
    {
    }
    
    /// <summary>
    /// Called BEFORE hot reload starts.
    /// Save any important state here.
    /// </summary>
    public void OnBeforeHotReload()
    {
        Logger.Log(LogLevel.Debug, "HotReload", "Saving state before reload...");
        
        _savedState = new Dictionary<string, object>
        {
            ["position"] = Position,
            ["visible"] = Visible,
            ["active"] = Active
        };
    }
    
    /// <summary>
    /// Called AFTER hot reload completes.
    /// Restore state and reinitialize here.
    /// </summary>
    public void OnHotReloaded()
    {
        Logger.Log(LogLevel.Debug, "HotReload", "Restoring state after reload...");
        
        if (_savedState != null)
        {
            Position = (Vector2)_savedState["position"];
            Visible = (bool)_savedState["visible"];
            Active = (bool)_savedState["active"];
        }
        
        // Reinitialize anything else needed
    }
    
    public override void Update()
    {
        base.Update();
        // Your update logic here
    }
}
