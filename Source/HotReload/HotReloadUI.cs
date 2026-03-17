using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace MaggyHelper.HotReload;

/// <summary>
/// In-game UI overlay for the Hot Code Reloading system.
/// Shows status, recent reloads, and compilation errors.
/// </summary>
public class HotReloadUI : IDisposable
{
    #region Constants
    
    private const float PANEL_WIDTH = 400f;
    private const float PANEL_PADDING = 10f;
    private const float LINE_HEIGHT = 20f;
    private const float HEADER_HEIGHT = 30f;
    private const float TOAST_DURATION = 3f;
    private const float TOAST_FADE_TIME = 0.5f;
    
    #endregion
    
    #region Fields
    
    private bool _visible = true;
    private bool _expanded = false;
    private float _toastTimer = 0f;
    private string _toastMessage = "";
    private bool _toastIsError = false;
    private float _pulseTimer = 0f;
    private bool _disposed = false;
    
    // Colors
    private static readonly Color BgColor = new Color(0, 0, 0, 180);
    private static readonly Color HeaderColor = new Color(40, 40, 60, 220);
    private static readonly Color SuccessColor = new Color(100, 200, 100);
    private static readonly Color ErrorColor = new Color(200, 100, 100);
    private static readonly Color WarningColor = new Color(200, 180, 100);
    private static readonly Color InfoColor = new Color(150, 150, 200);
    private static readonly Color CompilingColor = new Color(100, 180, 255);
    
    #endregion
    
    #region Properties
    
    /// <summary>
    /// Whether the UI is visible.
    /// </summary>
    public bool Visible
    {
        get => _visible && (MaggyHelperModule.Settings?.HotReloadShowUI ?? true);
        set => _visible = value;
    }
    
    /// <summary>
    /// Whether the UI is expanded to show history.
    /// </summary>
    public bool Expanded
    {
        get => _expanded;
        set => _expanded = value;
    }
    
    #endregion
    
    #region Constructor
    
    public HotReloadUI()
    {
        // Subscribe to hot reload events
        HotReloadManager.OnStatusChanged += OnStatusChanged;
        HotReloadManager.OnAfterReload += OnAfterReload;
        HotReloadManager.OnReloadFailed += OnReloadFailed;
        
        // Hook into game rendering
        On.Celeste.Level.Render += Level_Render;
        
        Logger.Log(LogLevel.Debug, "HotReload", "HotReloadUI initialized");
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnStatusChanged(string status)
    {
        // Could trigger visual feedback here
    }
    
    private void OnAfterReload(string file, System.Reflection.Assembly assembly)
    {
        ShowToast($"✓ Reloaded: {Path.GetFileName(file)}", false);
        
        // Play success sound if enabled
        if (MaggyHelperModule.Settings?.HotReloadPlaySound == true)
        {
            try
            {
                Audio.Play("event:/ui/main/button_select");
            }
            catch { }
        }
    }
    
    private void OnReloadFailed(string file, Exception error)
    {
        ShowToast($"✗ Failed: {Path.GetFileName(file)}", true);
        
        // Play error sound if enabled
        if (MaggyHelperModule.Settings?.HotReloadPlaySound == true)
        {
            try
            {
                Audio.Play("event:/ui/main/button_invalid");
            }
            catch { }
        }
    }
    
    private void Level_Render(On.Celeste.Level.orig_Render orig, Level self)
    {
        orig(self);
        
        if (!_disposed && Visible)
        {
            Render();
        }
    }
    
    #endregion
    
    #region Rendering
    
    /// <summary>
    /// Render the hot reload UI overlay.
    /// </summary>
    public void Render()
    {
        if (!HotReloadManager.IsInitialized && _toastTimer <= 0)
            return;
        
        // Update animations
        _pulseTimer += Engine.DeltaTime * 2f;
        if (_toastTimer > 0)
            _toastTimer -= Engine.DeltaTime;
        
        Draw.SpriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            Engine.ScreenMatrix
        );
        
        try
        {
            Vector2 pos = new Vector2(10, 10);
            
            // Render compact status bar
            RenderStatusBar(ref pos);
            
            // Render toast notification
            if (_toastTimer > 0)
            {
                RenderToast();
            }
            
            // Render expanded panel if enabled
            if (_expanded)
            {
                RenderExpandedPanel(ref pos);
            }
        }
        finally
        {
            Draw.SpriteBatch.End();
        }
    }
    
    /// <summary>
    /// Render the compact status bar.
    /// </summary>
    private void RenderStatusBar(ref Vector2 pos)
    {
        float barHeight = 24f;
        float barWidth = 280f;
        
        // Background
        Draw.Rect(pos.X, pos.Y, barWidth, barHeight, BgColor);
        
        // Status indicator
        Color statusColor = GetStatusColor();
        float indicatorSize = 10f;
        float indicatorX = pos.X + 8f;
        float indicatorY = pos.Y + (barHeight - indicatorSize) / 2f;
        
        // Pulsing indicator when compiling
        if (HotReloadManager.IsCompiling)
        {
            float pulse = (float)(Math.Sin(_pulseTimer) * 0.3 + 0.7);
            statusColor *= pulse;
        }
        
        Draw.Rect(indicatorX, indicatorY, indicatorSize, indicatorSize, statusColor);
        
        // Status text
        string statusText = GetStatusText();
        Vector2 textPos = new Vector2(indicatorX + indicatorSize + 8f, pos.Y + 4f);
        ActiveFont.Draw(statusText, textPos, Vector2.Zero, Vector2.One * 0.4f, Color.White);
        
        // Reload count
        if (HotReloadManager.ReloadCount > 0)
        {
            string countText = $"[{HotReloadManager.ReloadCount}]";
            Vector2 countPos = new Vector2(pos.X + barWidth - 50f, pos.Y + 4f);
            ActiveFont.Draw(countText, countPos, Vector2.Zero, Vector2.One * 0.4f, InfoColor);
        }
        
        // Toggle expand hint
        string hint = _expanded ? "▼" : "►";
        Vector2 hintPos = new Vector2(pos.X + barWidth - 20f, pos.Y + 4f);
        ActiveFont.Draw(hint, hintPos, Vector2.Zero, Vector2.One * 0.4f, Color.Gray);
        
        pos.Y += barHeight + 4f;
    }
    
    /// <summary>
    /// Render the toast notification.
    /// </summary>
    private void RenderToast()
    {
        if (string.IsNullOrEmpty(_toastMessage))
            return;
        
        float alpha = Math.Min(_toastTimer / TOAST_FADE_TIME, 1f);
        
        Vector2 toastPos = new Vector2(Engine.Width / 2f, 100f);
        Vector2 textSize = ActiveFont.Measure(_toastMessage) * 0.5f;
        
        float padding = 15f;
        Color bgColor = (_toastIsError ? ErrorColor : SuccessColor) * 0.8f * alpha;
        
        Draw.Rect(
            toastPos.X - textSize.X / 2f - padding,
            toastPos.Y - textSize.Y / 2f - padding / 2f,
            textSize.X + padding * 2f,
            textSize.Y + padding,
            new Color(0, 0, 0, (int)(200 * alpha))
        );
        
        Draw.Rect(
            toastPos.X - textSize.X / 2f - padding,
            toastPos.Y - textSize.Y / 2f - padding / 2f,
            4f,
            textSize.Y + padding,
            bgColor
        );
        
        ActiveFont.Draw(
            _toastMessage,
            toastPos,
            new Vector2(0.5f, 0.5f),
            Vector2.One * 0.5f,
            Color.White * alpha
        );
    }
    
    /// <summary>
    /// Render the expanded panel with history.
    /// </summary>
    private void RenderExpandedPanel(ref Vector2 pos)
    {
        float panelHeight = 200f;
        
        // Background
        Draw.Rect(pos.X, pos.Y, PANEL_WIDTH, panelHeight, BgColor);
        
        // Header
        Draw.Rect(pos.X, pos.Y, PANEL_WIDTH, HEADER_HEIGHT, HeaderColor);
        ActiveFont.Draw("Hot Reload History", new Vector2(pos.X + 10f, pos.Y + 5f), 
                       Vector2.Zero, Vector2.One * 0.45f, Color.White);
        
        // History entries
        float entryY = pos.Y + HEADER_HEIGHT + 5f;
        var history = HotReloadManager.ReloadHistory.Reverse().Take(8);
        
        foreach (var entry in history)
        {
            Color entryColor = Color.White * 0.8f;
            if (entry.Contains("FAILED") || entry.Contains("EXCEPTION"))
                entryColor = ErrorColor;
            else if (entry.Contains("Reloaded"))
                entryColor = SuccessColor;
            
            ActiveFont.Draw(entry, new Vector2(pos.X + 10f, entryY), 
                           Vector2.Zero, Vector2.One * 0.35f, entryColor);
            entryY += LINE_HEIGHT * 0.8f;
        }
        
        // Footer with keybind hints
        float footerY = pos.Y + panelHeight - 20f;
        string hints = "F5: Toggle | F6: Reload | F7: Reload All | F8: Hide";
        ActiveFont.Draw(hints, new Vector2(pos.X + 10f, footerY), 
                       Vector2.Zero, Vector2.One * 0.3f, Color.Gray);
        
        pos.Y += panelHeight + 4f;
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Get the current status color.
    /// </summary>
    private Color GetStatusColor()
    {
        if (!HotReloadManager.IsInitialized)
            return Color.Gray;
        
        if (HotReloadManager.IsCompiling)
            return CompilingColor;
        
        if (!HotReloadManager.IsEnabled)
            return WarningColor;
        
        return SuccessColor;
    }
    
    /// <summary>
    /// Get a short status text.
    /// </summary>
    private string GetStatusText()
    {
        if (!HotReloadManager.IsInitialized)
            return "Hot Reload: Not Initialized";
        
        if (HotReloadManager.IsCompiling)
            return "Hot Reload: Compiling...";
        
        if (!HotReloadManager.IsEnabled)
            return "Hot Reload: Paused";
        
        return $"Hot Reload: Watching";
    }
    
    /// <summary>
    /// Show a toast notification.
    /// </summary>
    public void ShowToast(string message, bool isError = false)
    {
        _toastMessage = message;
        _toastIsError = isError;
        _toastTimer = TOAST_DURATION;
    }
    
    /// <summary>
    /// Toggle the expanded state.
    /// </summary>
    public void ToggleExpanded()
    {
        _expanded = !_expanded;
    }
    
    /// <summary>
    /// Handle input for the UI.
    /// </summary>
    public void Update()
    {
        // Input handling is done in HotReloadSettingsHandler
    }
    
    #endregion
    
    #region IDisposable
    
    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        
        // Unsubscribe from events
        HotReloadManager.OnStatusChanged -= OnStatusChanged;
        HotReloadManager.OnAfterReload -= OnAfterReload;
        HotReloadManager.OnReloadFailed -= OnReloadFailed;
        
        // Remove render hook
        On.Celeste.Level.Render -= Level_Render;
        
        Logger.Log(LogLevel.Debug, "HotReload", "HotReloadUI disposed");
    }
    
    #endregion
}

/// <summary>
/// Entity component version of the Hot Reload UI for better integration with Monocle.
/// Add this to a scene to have the UI rendered as part of the entity system.
/// </summary>
public class HotReloadUIEntity : Entity
{
    private HotReloadUI _ui;
    
    public HotReloadUIEntity()
    {
        Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate | Tags.FrozenUpdate;
        Depth = -10000;
    }
    
    public override void Added(Scene scene)
    {
        base.Added(scene);
        _ui = new HotReloadUI();
    }
    
    public override void Removed(Scene scene)
    {
        base.Removed(scene);
        _ui?.Dispose();
        _ui = null;
    }
    
    public override void Update()
    {
        base.Update();
        _ui?.Update();
    }
    
    public override void Render()
    {
        base.Render();
        // UI renders via hook, not here
    }
}
