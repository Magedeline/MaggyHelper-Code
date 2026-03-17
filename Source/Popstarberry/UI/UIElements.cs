using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper.Popstarberry.UI;

/// <summary>
/// Base class for all UI elements in Popstarberry.
/// Based on Snowberry's UIElement system.
/// </summary>
public class UIElement
{
    #region Properties
    
    public Vector2 Position { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Visible { get; set; } = true;
    public bool Active { get; set; } = true;
    
    public UIElement Parent { get; private set; }
    public List<UIElement> Children { get; } = new();
    
    public Rectangle Bounds => new Rectangle((int)Position.X, (int)Position.Y, Width, Height);
    
    #endregion

    #region Child Management
    
    public void Add(UIElement child)
    {
        child.Parent?.Remove(child);
        child.Parent = this;
        Children.Add(child);
    }
    
    public void Remove(UIElement child)
    {
        if (Children.Remove(child))
        {
            child.Parent = null;
        }
    }
    
    public void RemoveSelf()
    {
        Parent?.Remove(this);
    }
    
    public void Clear()
    {
        foreach (var child in Children)
        {
            child.Parent = null;
        }
        Children.Clear();
    }
    
    public void Destroy()
    {
        Clear();
        RemoveSelf();
    }
    
    public void AddBelow(UIElement child, float spacing = 4)
    {
        if (Children.Count > 0)
        {
            var last = Children[^1];
            child.Position = new Vector2(last.Position.X, last.Position.Y + last.Height + spacing);
        }
        Add(child);
    }
    
    public void AddRight(UIElement child, float spacing = 4)
    {
        if (Children.Count > 0)
        {
            var last = Children[^1];
            child.Position = new Vector2(last.Position.X + last.Width + spacing, last.Position.Y);
        }
        Add(child);
    }
    
    #endregion

    #region Update & Render
    
    public virtual void Update(Vector2 position = default)
    {
        if (!Active) return;
        
        foreach (var child in Children)
        {
            if (child.Active)
            {
                child.Update(position + Position);
            }
        }
    }
    
    public virtual void Render(Vector2 position = default)
    {
        if (!Visible) return;
        
        foreach (var child in Children)
        {
            if (child.Visible)
            {
                child.Render(position + Position);
            }
        }
    }
    
    #endregion

    #region Hit Testing
    
    public bool ContainsPoint(Vector2 point, Vector2 parentPos = default)
    {
        Vector2 absPos = parentPos + Position;
        return point.X >= absPos.X && point.X < absPos.X + Width &&
               point.Y >= absPos.Y && point.Y < absPos.Y + Height;
    }
    
    #endregion
}

/// <summary>
/// Simple text label UI element.
/// </summary>
public class UILabel : UIElement
{
    public string Text { get; set; }
    public Func<string> DynamicText { get; set; }
    public Color FG { get; set; } = Color.White;
    public float Scale { get; set; } = 0.6f;
    public bool Underline { get; set; } = false;
    
    public UILabel(string text)
    {
        Text = text;
        UpdateSize();
    }
    
    public UILabel(Func<string> dynamicText)
    {
        DynamicText = dynamicText;
        Text = dynamicText();
        UpdateSize();
    }
    
    private void UpdateSize()
    {
        var size = ActiveFont.Measure(Text) * Scale;
        Width = (int)size.X;
        Height = (int)size.Y;
    }
    
    public override void Update(Vector2 position = default)
    {
        base.Update(position);
        
        if (DynamicText != null)
        {
            string newText = DynamicText();
            if (newText != Text)
            {
                Text = newText;
                UpdateSize();
            }
        }
    }
    
    public override void Render(Vector2 position = default)
    {
        base.Render(position);
        
        Vector2 pos = position + Position;
        ActiveFont.Draw(Text, pos, Vector2.Zero, Vector2.One * Scale, FG);
        
        if (Underline)
        {
            Draw.Line(pos + new Vector2(0, Height), pos + new Vector2(Width, Height), FG);
        }
    }
}

/// <summary>
/// Clickable button UI element.
/// </summary>
public class UIButton : UIElement
{
    public string Text { get; set; }
    public Action OnPress { get; set; }
    
    public Color FG { get; set; } = Color.White;
    public Color BG { get; set; } = new Color(60, 60, 70);
    public Color HoveredBG { get; set; } = new Color(80, 80, 100);
    public Color PressedBG { get; set; } = new Color(100, 100, 130);
    public Color PressedFG { get; set; } = Color.White;
    
    public float Scale { get; set; } = 0.6f;
    
    private bool isHovered;
    private bool isPressed;
    
    public UIButton(string text, int paddingX = 6, int paddingY = 4)
    {
        Text = text;
        var size = ActiveFont.Measure(text) * Scale;
        Width = (int)size.X + paddingX * 2;
        Height = (int)size.Y + paddingY * 2;
    }
    
    public override void Update(Vector2 position = default)
    {
        base.Update(position);
        
        Vector2 absPos = position + Position;
        Rectangle bounds = new Rectangle((int)absPos.X, (int)absPos.Y, Width, Height);
        
        var mousePos = new Point((int)MInput.Mouse.Position.X, (int)MInput.Mouse.Position.Y);
        isHovered = bounds.Contains(mousePos);
        
        if (isHovered)
        {
            if (MInput.Mouse.PressedLeftButton)
            {
                isPressed = true;
            }
            
            if (isPressed && MInput.Mouse.ReleasedLeftButton)
            {
                OnPress?.Invoke();
                isPressed = false;
            }
        }
        
        if (MInput.Mouse.ReleasedLeftButton)
        {
            isPressed = false;
        }
    }
    
    public override void Render(Vector2 position = default)
    {
        Vector2 pos = position + Position;
        
        Color bgColor = isPressed ? PressedBG : (isHovered ? HoveredBG : BG);
        Color fgColor = isPressed ? PressedFG : FG;
        
        Draw.Rect(pos.X, pos.Y, Width, Height, bgColor);
        Draw.HollowRect(pos.X, pos.Y, Width, Height, Color.White * 0.3f);
        
        var textSize = ActiveFont.Measure(Text) * Scale;
        Vector2 textPos = pos + new Vector2((Width - textSize.X) / 2, (Height - textSize.Y) / 2);
        ActiveFont.Draw(Text, textPos, Vector2.Zero, Vector2.One * Scale, fgColor);
        
        base.Render(position);
    }
}

/// <summary>
/// Ribbon-style UI element for menus.
/// </summary>
public class UIRibbon : UIElement
{
    public string Text { get; set; }
    public Color BG { get; set; } = new Color(50, 50, 60);
    public Color BGAccent { get; set; } = new Color(255, 105, 180);
    public Color FG { get; set; } = Color.White;
    public float Scale { get; set; } = 0.5f;
    
    public UIRibbon(string text)
    {
        Text = text;
        var size = ActiveFont.Measure(text) * Scale;
        Width = (int)size.X + 16;
        Height = (int)size.Y + 4;
    }
    
    public void SetText(string text)
    {
        Text = text;
        var size = ActiveFont.Measure(text) * Scale;
        Width = (int)size.X + 16;
        Height = (int)size.Y + 4;
    }
    
    public override void Render(Vector2 position = default)
    {
        Vector2 pos = position + Position;
        
        // Draw ribbon shape
        Draw.Rect(pos.X, pos.Y, Width - 4, Height, BG);
        Draw.Rect(pos.X + Width - 4, pos.Y, 4, Height, BGAccent);
        
        // Draw text
        ActiveFont.Draw(Text, pos + new Vector2(4, 2), Vector2.Zero, Vector2.One * Scale, FG);
        
        base.Render(position);
    }
}

/// <summary>
/// Message overlay for dialogs and confirmations.
/// </summary>
public class UIMessage : UIElement
{
    private class MessageElement
    {
        public UIElement Element;
        public Vector2 ShownJustify;
        public Vector2 HiddenJustify;
    }
    
    private List<MessageElement> messages = new();
    private float lerp;
    
    public bool Shown { get; set; }
    
    public new void Clear()
    {
        messages.Clear();
        base.Clear();
    }
    
    public void AddElement(UIElement element, float justifyX, float justifyY, float hiddenJustifyX, float hiddenJustifyY)
    {
        Add(element);
        messages.Add(new MessageElement
        {
            Element = element,
            ShownJustify = new Vector2(justifyX, justifyY),
            HiddenJustify = new Vector2(hiddenJustifyX, hiddenJustifyY)
        });
    }
    
    public override void Update(Vector2 position = default)
    {
        base.Update(position);
        
        lerp = Calc.Approach(lerp, Shown ? 1f : 0f, Engine.DeltaTime * 2f);
        float ease = Ease.ExpoOut(lerp);
        
        foreach (var msg in messages)
        {
            Vector2 justify = Vector2.Lerp(msg.HiddenJustify, msg.ShownJustify, ease);
            msg.Element.Position = new Vector2(
                (Width - msg.Element.Width) * justify.X,
                (Height - msg.Element.Height) * justify.Y
            );
        }
        
        if (MInput.Keyboard.Check(Microsoft.Xna.Framework.Input.Keys.Escape))
        {
            Shown = false;
        }
    }
    
    public override void Render(Vector2 position = default)
    {
        if (lerp <= 0) return;
        
        // Dark overlay
        Draw.Rect(0, 0, Width, Height, Color.Black * lerp * 0.7f);
        
        base.Render(position);
    }
    
    public static UIElement YesAndNoButtons(Action yesPress = null, Action noPress = null)
    {
        var container = new UIElement { Width = 120, Height = 24 };
        
        var yes = new UIButton("Yes", 8, 4)
        {
            BG = new Color(50, 150, 50),
            HoveredBG = new Color(70, 180, 70),
            OnPress = yesPress
        };
        container.Add(yes);
        
        var no = new UIButton("No", 8, 4)
        {
            Position = new Vector2(yes.Width + 8, 0),
            BG = new Color(150, 50, 50),
            HoveredBG = new Color(180, 70, 70),
            OnPress = noPress
        };
        container.Add(no);
        
        container.Width = yes.Width + no.Width + 8;
        container.Height = Math.Max(yes.Height, no.Height);
        
        return container;
    }
}

/// <summary>
/// Toolbar for the editor with tool buttons.
/// </summary>
public class UIToolbar : UIElement
{
    private Editor.PopstarEditor editor;
    private List<UIButton> toolButtons = new();
    
    public UIToolbar(Editor.PopstarEditor editor)
    {
        this.editor = editor;
        Height = 30;
        
        CreateToolButtons();
    }
    
    private void CreateToolButtons()
    {
        string[] toolNames = { "Select", "Place", "Tiles", "Styles", "Decals", "Room" };
        int x = 4;
        
        for (int i = 0; i < toolNames.Length; i++)
        {
            int idx = i;
            var btn = new UIButton(toolNames[i], 8, 4)
            {
                Position = new Vector2(x, 4),
                OnPress = () => editor.SwitchTool(idx)
            };
            toolButtons.Add(btn);
            Add(btn);
            x += btn.Width + 4;
        }
    }
    
    public override void Render(Vector2 position = default)
    {
        // Draw toolbar background
        Draw.Rect(position.X, position.Y, Width, Height, new Color(40, 40, 45));
        Draw.Line(position.X, position.Y + Height, position.X + Width, position.Y + Height, new Color(60, 60, 70));
        
        // Highlight active tool
        if (editor.CurrentToolIndex >= 0 && editor.CurrentToolIndex < toolButtons.Count)
        {
            var btn = toolButtons[editor.CurrentToolIndex];
            Draw.Rect(position.X + btn.Position.X - 2, position.Y + btn.Position.Y - 2, 
                btn.Width + 4, btn.Height + 4, new Color(255, 105, 180) * 0.3f);
        }
        
        base.Render(position);
    }
}

/// <summary>
/// Main menu UI for Popstarberry.
/// </summary>
public class UIMainMenu : UIElement
{
    public static UIMainMenu Instance { get; private set; }
    
    public enum States { Start, Create, Load, Exiting, Settings }
    private States state = States.Start;
    private float[] stateLerp = new float[] { 1f, 0f, 0f, 0f, 0f };
    
    private UIRibbon titleRibbon;
    private UIRibbon versionRibbon;
    private UIButton createButton, loadButton, exitButton;
    
    public UIMainMenu(int width, int height)
    {
        Instance = this;
        Width = width;
        Height = height;
        
        // Title
        titleRibbon = new UIRibbon("Popstarberry")
        {
            Position = new Vector2(0, 8),
            BGAccent = new Color(255, 105, 180)
        };
        Add(titleRibbon);
        
        // Version
        versionRibbon = new UIRibbon($"v{PopstarberryModule.Version}")
        {
            Position = new Vector2(0, 23),
            BGAccent = new Color(255, 105, 180)
        };
        Add(versionRibbon);
        
        // Buttons
        float centerX = width / 2f;
        float buttonY = height / 2f - 40;
        
        createButton = new UIButton("Create New Map", 16, 8)
        {
            BG = new Color(60, 180, 180),
            HoveredBG = new Color(80, 200, 200),
            OnPress = () => state = States.Create
        };
        createButton.Position = new Vector2(centerX - createButton.Width / 2, buttonY);
        Add(createButton);
        
        loadButton = new UIButton("Load Map", 16, 8)
        {
            BG = new Color(60, 60, 180),
            HoveredBG = new Color(80, 80, 200),
            OnPress = () => state = States.Load
        };
        loadButton.Position = new Vector2(centerX - loadButton.Width / 2, buttonY + 36);
        Add(loadButton);
        
        exitButton = new UIButton("Exit", 16, 8)
        {
            BG = new Color(180, 60, 60),
            HoveredBG = new Color(200, 80, 80),
            OnPress = () => state = States.Exiting
        };
        exitButton.Position = new Vector2(centerX - exitButton.Width / 2, buttonY + 72);
        Add(exitButton);
    }
    
    public override void Update(Vector2 position = default)
    {
        base.Update(position);
        
        // Update state lerps
        for (int i = 0; i < stateLerp.Length; i++)
        {
            stateLerp[i] = Calc.Approach(stateLerp[i], ((int)state == i) ? 1f : 0f, Engine.DeltaTime * 2f);
        }
        
        switch (state)
        {
            case States.Create:
                if (stateLerp[1] >= 0.9f)
                {
                    Editor.PopstarEditor.OpenNew();
                }
                break;
                
            case States.Exiting:
                if (stateLerp[3] >= 0.9f)
                {
                    if (SaveData.Instance == null)
                    {
                        SaveData.InitializeDebugMode();
                        SaveData.Instance.CurrentSession_Safe = new Session(AreaKey.Default);
                    }
                    Engine.Scene = new OverworldLoader(Overworld.StartMode.MainMenu);
                }
                break;
        }
    }
    
    public override void Render(Vector2 position = default)
    {
        // Background gradient
        Draw.Rect(0, 0, Width, Height, new Color(20, 20, 25));
        
        // Star pattern (subtle)
        float time = Engine.Scene?.TimeActive ?? 0;
        for (int i = 0; i < 20; i++)
        {
            float x = (i * 47 + time * 10) % Width;
            float y = (i * 31 + time * 5) % Height;
            float alpha = 0.1f + 0.1f * (float)Math.Sin(time * 2 + i);
            Draw.Point(new Vector2(x, y), Color.White * alpha);
        }
        
        base.Render(position);
    }
}
