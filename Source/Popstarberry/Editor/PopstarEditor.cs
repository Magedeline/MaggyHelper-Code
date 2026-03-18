using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace MaggyHelper.Popstarberry.Editor;

/// <summary>
/// Main editor scene for Popstarberry.
/// Based on Snowberry's Editor class with enhanced features.
/// </summary>
public class PopstarEditor : Scene
{
    #region Camera
    
    public class BufferCamera
    {
        public float Scale { get; private set; } = 6f;
        public Vector2 Position { get; set; }
        
        public RenderTarget2D Buffer { get; private set; }
        public Matrix Matrix => Matrix.CreateTranslation(-Position.X, -Position.Y, 0) 
            * Matrix.CreateScale(Scale) 
            * Matrix.CreateTranslation(Buffer.Width / 2f, Buffer.Height / 2f, 0);
        
        public Matrix ScreenView => Matrix.CreateTranslation(-Position.X, -Position.Y, 0) 
            * Matrix.CreateScale(Scale) 
            * Matrix.CreateTranslation(Engine.ViewWidth / 2f, Engine.ViewHeight / 2f, 0);
        
        public Rectangle ViewRect
        {
            get
            {
                int w = (int)Math.Ceiling(Buffer.Width / Scale);
                int h = (int)Math.Ceiling(Buffer.Height / Scale);
                return new Rectangle(
                    (int)Position.X - w / 2,
                    (int)Position.Y - h / 2,
                    w, h
                );
            }
        }
        
        public void Zoom(float by)
        {
            Scale = MathHelper.Clamp(Scale + by, 1f, 24f);
        }
        
        public void UpdateBuffer()
        {
            int w = Engine.ViewWidth / 2;
            int h = Engine.ViewHeight / 2;
            
            if (Buffer == null || Buffer.Width != w || Buffer.Height != h)
            {
                Buffer?.Dispose();
                Buffer = new RenderTarget2D(Engine.Instance.GraphicsDevice, w, h);
            }
        }
        
        public Vector2 ScreenToWorld(Vector2 screen)
        {
            return (screen - new Vector2(Engine.ViewWidth / 2f, Engine.ViewHeight / 2f)) / Scale + Position;
        }
        
        public Vector2 WorldToScreen(Vector2 world)
        {
            return (world - Position) * Scale + new Vector2(Engine.ViewWidth / 2f, Engine.ViewHeight / 2f);
        }
    }
    
    #endregion

    #region Mouse Helper
    
    public static class Mouse
    {
        public static Vector2 Screen => MInput.Mouse.Position;
        public static Vector2 World => Instance?.Camera?.ScreenToWorld(Screen) ?? Vector2.Zero;
        public static Vector2 WorldLast { get; internal set; }
        public static bool LeftClicked => MInput.Mouse.PressedLeftButton;
        public static bool RightClicked => MInput.Mouse.PressedRightButton;
        public static bool MiddleClicked => MInput.Mouse.PressedMiddleButton;
        public static int ScrollDelta => MInput.Mouse.WheelDelta;
    }
    
    #endregion

    #region Static Instance
    
    public static PopstarEditor Instance { get; private set; }
    
    #endregion

    #region Properties
    
    public BufferCamera Camera { get; private set; }
    public PopstarMap Map { get; private set; }
    
    public PopstarRoom SelectedRoom { get; set; }
    public int SelectedFillerIndex { get; set; } = -1;
    public List<EntitySelection> SelectedEntities { get; set; } = new();
    public Rectangle? Selection { get; set; }
    
    public Tools.Tool CurrentTool { get; private set; }
    public int CurrentToolIndex { get; private set; }
    
    public static AreaKey? From { get; private set; }
    
    #endregion

    #region UI
    
    private RenderTarget2D uiBuffer;
    private UI.UIElement rootUI = new();
    public UI.UIToolbar Toolbar { get; private set; }
    public UI.UIElement ToolPanel { get; private set; }
    public UI.UIMessage Message { get; private set; }
    
    #endregion

    #region Rendering
    
    private static readonly Color BackgroundColor = Calc.HexToColor("060607");
    
    public static bool FancyRender { get; set; } = true;
    
    #endregion

    #region Playtest Support
    
    internal static Session PlaytestSession;
    internal static MapData PlaytestMapData;
    
    #endregion

    #region Initialization
    
    private PopstarEditor(PopstarMap map)
    {
        Instance = this;
        Map = map;
    }
    
    public static void Initialize()
    {
        // Hook into MapData loading for playtest support
        On.Celeste.LevelEnter.Routine += OnLevelEnterRoutine;
        
        PopstarberryModule.Log(LogLevel.Info, "PopstarEditor initialized.");
    }
    
    public static void Cleanup()
    {
        On.Celeste.LevelEnter.Routine -= OnLevelEnterRoutine;
        Instance = null;
        
        PopstarberryModule.Log(LogLevel.Info, "PopstarEditor cleaned up.");
    }
    
    #endregion

    #region Opening the Editor
    
    /// <summary>
    /// Open the editor with an existing map.
    /// </summary>
    public static void Open(MapData data)
    {
        Audio.Stop(Audio.CurrentAmbienceEventInstance);
        Audio.Stop(Audio.CurrentMusicEventInstance);
        
        PopstarMap map = null;
        
        if (data != null)
        {
            PopstarberryModule.Log(LogLevel.Info, $"Opening editor with map: {data.Area.GetSID()}");
            From = data.Area;
            map = new PopstarMap(data);
            map.InitializeAllEntities();
        }
        else
        {
            From = null;
        }
        
        Engine.Scene = new PopstarEditor(map);
    }
    
    /// <summary>
    /// Open the editor with a new empty map.
    /// </summary>
    public static void OpenNew()
    {
        Audio.Stop(Audio.CurrentAmbienceEventInstance);
        Audio.Stop(Audio.CurrentMusicEventInstance);
        
        PopstarberryModule.Log(LogLevel.Info, "Opening editor with new map.");
        From = null;
        
        var map = new PopstarMap("popstarberry_map");
        map.InitializeAllEntities();
        
        Engine.Scene = new PopstarEditor(map);
    }
    
    /// <summary>
    /// Open the editor with a fade transition.
    /// </summary>
    public static void OpenFancy(MapData data)
    {
        Audio.Stop(Audio.CurrentAmbienceEventInstance);
        Audio.Stop(Audio.CurrentMusicEventInstance);
        
        PopstarMap map = null;
        if (data != null)
        {
            map = new PopstarMap(data);
        }
        
        var wipe = new FadeWipe(Engine.Scene, false, () =>
        {
            var editor = new PopstarEditor(map);
            Engine.Scene = editor;
        })
        {
            Duration = 0.85f
        };
    }
    
    #endregion

    #region Scene Lifecycle
    
    public override void Begin()
    {
        base.Begin();
        
        Camera = new BufferCamera();
        Camera.UpdateBuffer(); // Initialize buffer immediately to prevent null reference in Render
        
        // Create UI buffer at half resolution
        uiBuffer = new RenderTarget2D(Engine.Instance.GraphicsDevice, Engine.ViewWidth / 2, Engine.ViewHeight / 2);
        rootUI.Width = uiBuffer.Width;
        rootUI.Height = uiBuffer.Height;
        
        if (Map == null)
        {
            CreateMainMenuUI();
        }
        else
        {
            CreateMappingUI();
        }
        
        // Add message overlay
        Message = new UI.UIMessage
        {
            Width = rootUI.Width,
            Height = rootUI.Height
        };
        rootUI.Add(Message);
    }
    
    public override void End()
    {
        base.End();
        
        Camera.Buffer?.Dispose();
        uiBuffer.Dispose();
        rootUI.Destroy();
        
        Instance = null;
    }
    
    #endregion

    #region UI Creation
    
    private void CreateMainMenuUI()
    {
        rootUI.Add(new UI.UIMainMenu(uiBuffer.Width, uiBuffer.Height));
    }
    
    private void CreateMappingUI()
    {
        // Create toolbar
        Toolbar = new UI.UIToolbar(this);
        rootUI.Add(Toolbar);
        Toolbar.Width = uiBuffer.Width;
        
        // Map info labels
        var mapLabel = new UI.UILabel($"Map: {From?.SID ?? "(new map)"}")
        {
            Position = new Vector2(10, Toolbar.Height + 10)
        };
        rootUI.Add(mapLabel);
        
        var roomLabel = new UI.UILabel(() => $"Room: {SelectedRoom?.Name ?? "(none)"}")
        {
            Position = new Vector2(10, Toolbar.Height + 30)
        };
        rootUI.Add(roomLabel);
        
        // Editor buttons
        CreateEditorButtons();
        
        // Set initial tool
        SwitchTool(0);
    }
    
    private void CreateEditorButtons()
    {
        int y = Toolbar.Height + 60;
        
        // Return to game button
        if (From.HasValue)
        {
            var returnBtn = new UI.UIButton("Return to Map", 6, 6)
            {
                Position = new Vector2(10, y),
                OnPress = ReturnToGame
            };
            rootUI.Add(returnBtn);
            y += 30;
        }
        
        // Playtest button
        var testBtn = new UI.UIButton("Playtest", 6, 6)
        {
            Position = new Vector2(10, y),
            OnPress = StartPlaytest
        };
        rootUI.Add(testBtn);
        y += 30;
        
        // Export button
        var exportBtn = new UI.UIButton("Export Map", 6, 6)
        {
            Position = new Vector2(10, y),
            OnPress = ExportMap
        };
        rootUI.Add(exportBtn);
    }
    
    #endregion

    #region Tool Management
    
    public void SwitchTool(int index)
    {
        CurrentTool?.Unselected();
        
        CurrentToolIndex = index;
        CurrentTool = Tools.ToolRegistry.GetTool(index);
        
        // Remove old panel
        ToolPanel?.RemoveSelf();
        
        // Create new panel
        ToolPanel = CurrentTool?.CreatePanel();
        if (ToolPanel != null)
        {
            ToolPanel.Position = new Vector2(uiBuffer.Width - ToolPanel.Width - 10, Toolbar.Height + 10);
            rootUI.Add(ToolPanel);
        }
        
        CurrentTool?.Selected();
    }
    
    #endregion

    #region Update
    
    public override void Update()
    {
        base.Update();
        
        // Update camera buffer size
        Camera.UpdateBuffer();
        
        // Store last mouse position
        Mouse.WorldLast = Mouse.World;
        
        // Handle input
        HandleInput();
        
        // Update UI
        rootUI.Update(Vector2.Zero);
        
        // Update current tool
        CurrentTool?.Update();
    }
    
    private void HandleInput()
    {
        // Camera zoom with mouse wheel
        if (Mouse.ScrollDelta != 0)
        {
            Camera.Zoom(Mouse.ScrollDelta * 0.01f);
        }
        
        // Camera pan with middle mouse
        if (MInput.Mouse.CheckMiddleButton)
        {
            Camera.Position -= (Mouse.World - Mouse.WorldLast);
        }
        
        // Keyboard shortcuts
        HandleKeyboardShortcuts();
    }
    
    private void HandleKeyboardShortcuts()
    {
        var kb = MInput.Keyboard;
        bool ctrl = kb.Check(Keys.LeftControl) || kb.Check(Keys.RightControl);
        bool shift = kb.Check(Keys.LeftShift) || kb.Check(Keys.RightShift);
        
        // Save
        if (ctrl && kb.Pressed(Keys.S))
        {
            ExportMap();
        }
        
        // Undo/Redo (placeholder)
        if (ctrl && kb.Pressed(Keys.Z))
        {
            if (shift)
                PerformRedo();
            else
                PerformUndo();
        }
        
        // Tool switching with number keys
        for (int i = 0; i < 9; i++)
        {
            if (kb.Pressed((Keys)((int)Keys.D1 + i)))
            {
                SwitchTool(i);
            }
        }
        
        // Delete selected
        if (kb.Pressed(Keys.Delete))
        {
            DeleteSelected();
        }
        
        // Escape to deselect
        if (kb.Pressed(Keys.Escape))
        {
            SelectedEntities.Clear();
            Selection = null;
        }
    }
    
    #endregion

    #region Actions
    
    private void ReturnToGame()
    {
        Audio.SetMusic(null);
        Audio.SetAmbience(null);
        
        SaveData.InitializeDebugMode();
        LevelEnter.Go(new Session(From.Value), true);
    }
    
    private void StartPlaytest()
    {
        if (Map == null) return;
        
        Audio.SetMusic(null);
        Audio.SetAmbience(null);
        
        SaveData.InitializeDebugMode();

        PlaytestMapData = new MapData(Map.From);
        Map.GenerateMapData(PlaytestMapData);
        PlaytestSession = new Session(Map.From);
        LevelEnter.Go(PlaytestSession, true);
    }
    
    private void ExportMap()
    {
        if (Map == null) return;
        
        try
        {
            var element = Map.Export();
            BinaryExporter.ExportMap(element, Map.Name);
            PopstarberryModule.Log(LogLevel.Info, "Map exported successfully.");
        }
        catch (Exception ex)
        {
            PopstarberryModule.Log(LogLevel.Error, $"Failed to export map: {ex}");
        }
    }
    
    private void PerformUndo()
    {
        // TODO: Implement undo system
        PopstarberryModule.Log(LogLevel.Warn, "Undo not yet implemented.");
    }
    
    private void PerformRedo()
    {
        // TODO: Implement redo system
        PopstarberryModule.Log(LogLevel.Warn, "Redo not yet implemented.");
    }
    
    private void DeleteSelected()
    {
        if (SelectedEntities.Count == 0) return;
        
        foreach (var selection in SelectedEntities)
        {
            selection.Entity.Room?.RemoveEntity(selection.Entity);
        }
        
        SelectedEntities.Clear();
    }
    
    #endregion

    #region Render
    
    public override void Render()
    {
        // Render map to buffer
        Engine.Instance.GraphicsDevice.SetRenderTarget(Camera.Buffer);
        Engine.Instance.GraphicsDevice.Clear(BackgroundColor);
        
        if (Map != null)
        {
            Map.Render(Camera);
            
            if (FancyRender)
            {
                Map.HQRender(Camera);
            }
        }
        
        // Render current tool overlays
        Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Camera.Matrix);
        CurrentTool?.Render();
        RenderSelection();
        Draw.SpriteBatch.End();
        
        // Render UI to buffer
        Engine.Instance.GraphicsDevice.SetRenderTarget(uiBuffer);
        Engine.Instance.GraphicsDevice.Clear(Color.Transparent);
        
        Draw.SpriteBatch.Begin();
        rootUI.Render(Vector2.Zero);
        Draw.SpriteBatch.End();
        
        // Compose to screen
        Engine.Instance.GraphicsDevice.SetRenderTarget(null);
        Engine.Instance.GraphicsDevice.Clear(BackgroundColor);
        
        // Draw map buffer
        Draw.SpriteBatch.Begin();
        Draw.SpriteBatch.Draw(Camera.Buffer, Vector2.Zero, null, Color.White, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
        Draw.SpriteBatch.End();
        
        // Draw UI buffer
        Draw.SpriteBatch.Begin();
        Draw.SpriteBatch.Draw(uiBuffer, Vector2.Zero, null, Color.White, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
        Draw.SpriteBatch.End();
    }
    
    private void RenderSelection()
    {
        // Draw selection rectangle
        if (Selection.HasValue)
        {
            var sel = Selection.Value;
            Draw.HollowRect(sel.X, sel.Y, sel.Width, sel.Height, Color.White * 0.5f);
        }
        
        // Draw selected entities
        foreach (var entity in SelectedEntities)
        {
            entity.Render();
        }
    }
    
    #endregion

    #region Hooks
    
    private static System.Collections.IEnumerator OnLevelEnterRoutine(
        On.Celeste.LevelEnter.orig_Routine orig, 
        LevelEnter self)
    {
        // Check if trying to enter playtest map
        // Access session through reflection since it may be private
        var sessionField = typeof(LevelEnter).GetField("session", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var session = sessionField?.GetValue(self) as Session;
        
        if (PlaytestSession != null && session == PlaytestSession)
        {
            // Use our playtest map data
            yield return orig(self);
        }
        else
        {
            yield return orig(self);
        }
    }
    
    #endregion
}

/// <summary>
/// Represents a selected entity in the editor.
/// </summary>
public class EntitySelection
{
    public Entities.PopstarEntity Entity { get; }
    public Rectangle Bounds { get; }
    
    public EntitySelection(Entities.PopstarEntity entity, Rectangle bounds)
    {
        Entity = entity;
        Bounds = bounds;
    }
    
    public void Render()
    {
        Draw.HollowRect(Bounds, Color.Yellow);
    }
}

/// <summary>
/// Utility for exporting maps to binary format.
/// </summary>
public static class BinaryExporter
{
    public static void ExportMap(BinaryPacker.Element element, string name)
    {
        // TODO: Implement proper binary export
        PopstarberryModule.Log(LogLevel.Info, $"Would export map '{name}' here.");
    }
}
