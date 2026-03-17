using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Monocle;
using LogLevel = Celeste.Mod.LogLevel;

namespace MaggyHelper.Popstarberry.ImGui;

/// <summary>
/// ImGui-inspired immediate mode UI system for Popstarberry.
/// Provides a lightweight, flexible UI framework for editor tools.
/// </summary>
public static class PopstarImGui
{
    #region State
    
    // Current frame state
    private static int currentId;
    private static int hotId;
    private static int activeId;
    
    // Layout state
    private static Stack<LayoutContext> layoutStack = new();
    private static LayoutContext currentLayout;
    
    // Styling
    public static ImGuiStyle Style { get; set; } = new ImGuiStyle();
    
    // Input state
    private static Vector2 mousePos;
    private static bool mouseDown;
    private static bool mousePressed;
    private static bool mouseReleased;
    private static int mouseScroll;
    private static Keys[] pressedKeys;
#pragma warning disable CS0414
    private static string textInput = "";
#pragma warning restore CS0414
    
    // Window state
    private static Dictionary<int, WindowState> windowStates = new();
    
    #endregion

    #region Initialization
    
    public static void Initialize()
    {
        PopstarberryModule.Log(LogLevel.Info, "PopstarImGui initialized.");
    }
    
    public static void Cleanup()
    {
        layoutStack.Clear();
        windowStates.Clear();
        PopstarberryModule.Log(LogLevel.Info, "PopstarImGui cleaned up.");
    }
    
    #endregion

    #region Frame Management
    
    public static void BeginFrame()
    {
        // Update input state
        var ms = Microsoft.Xna.Framework.Input.Mouse.GetState();
        mousePos = new Vector2(ms.X, ms.Y);
        bool wasDown = mouseDown;
        mouseDown = ms.LeftButton == ButtonState.Pressed;
        mousePressed = mouseDown && !wasDown;
        mouseReleased = !mouseDown && wasDown;
        mouseScroll = ms.ScrollWheelValue;
        
        var kb = Keyboard.GetState();
        pressedKeys = kb.GetPressedKeys();
        
        // Reset frame state
        currentId = 0;
        hotId = 0;
        
        // Reset layout
        layoutStack.Clear();
        currentLayout = new LayoutContext
        {
            Position = Vector2.Zero,
            Size = new Vector2(Engine.ViewWidth, Engine.ViewHeight),
            CursorX = 0,
            CursorY = 0
        };
    }
    
    public static void EndFrame()
    {
        // Clear active if mouse released
        if (mouseReleased)
        {
            activeId = 0;
        }
    }
    
    #endregion

    #region Helper Methods
    
    /// <summary>
    /// Convert Vector2 to Point for Rectangle.Contains checks
    /// </summary>
    private static Point ToPoint(Vector2 v) => new Point((int)v.X, (int)v.Y);
    
    #endregion

    #region ID System
    
    private static int GetId(string label)
    {
        return label.GetHashCode() ^ (currentId * 31);
    }
    
    private static int GetId()
    {
        return currentId++;
    }
    
    public static void PushId(int id)
    {
        currentId = id;
    }
    
    public static void PopId()
    {
        currentId = 0;
    }
    
    #endregion

    #region Layout
    
    public static void BeginGroup()
    {
        layoutStack.Push(currentLayout);
        currentLayout = new LayoutContext
        {
            Position = currentLayout.Position + new Vector2(currentLayout.CursorX, currentLayout.CursorY),
            Size = currentLayout.Size,
            CursorX = 0,
            CursorY = 0
        };
    }
    
    public static void EndGroup()
    {
        if (layoutStack.Count > 0)
        {
            currentLayout = layoutStack.Pop();
        }
    }
    
    public static void SameLine(float spacing = 4f)
    {
        currentLayout.SameLine = true;
        currentLayout.SameLineSpacing = spacing;
    }
    
    public static void Indent(float amount = 16f)
    {
        currentLayout.IndentLevel += amount;
    }
    
    public static void Unindent(float amount = 16f)
    {
        currentLayout.IndentLevel -= amount;
    }
    
    public static void Spacing(float amount = 8f)
    {
        currentLayout.CursorY += amount;
    }
    
    public static void Separator()
    {
        float y = currentLayout.Position.Y + currentLayout.CursorY;
        float x1 = currentLayout.Position.X + currentLayout.IndentLevel;
        float x2 = currentLayout.Position.X + currentLayout.Size.X - Style.WindowPadding;
        
        Draw.Line(x1, y + 4, x2, y + 4, Style.SeparatorColor);
        currentLayout.CursorY += 12;
    }
    
    private static void AdvanceCursor(float width, float height)
    {
        if (currentLayout.SameLine)
        {
            currentLayout.CursorX += width + currentLayout.SameLineSpacing;
            currentLayout.SameLine = false;
        }
        else
        {
            currentLayout.CursorX = currentLayout.IndentLevel;
            currentLayout.CursorY += height + Style.ItemSpacing;
        }
        
        currentLayout.MaxWidth = Math.Max(currentLayout.MaxWidth, currentLayout.CursorX + width);
        currentLayout.MaxHeight = Math.Max(currentLayout.MaxHeight, currentLayout.CursorY);
    }
    
    private static Vector2 GetCursorPos()
    {
        return currentLayout.Position + new Vector2(currentLayout.CursorX + currentLayout.IndentLevel, currentLayout.CursorY);
    }
    
    #endregion

    #region Basic Widgets
    
    public static void Text(string text)
    {
        Vector2 pos = GetCursorPos();
        Vector2 size = ActiveFont.Measure(text) * Style.FontScale;
        
        ActiveFont.Draw(text, pos, Vector2.Zero, Vector2.One * Style.FontScale, Style.TextColor);
        
        AdvanceCursor(size.X, size.Y);
    }
    
    public static void TextColored(string text, Color color)
    {
        Vector2 pos = GetCursorPos();
        Vector2 size = ActiveFont.Measure(text) * Style.FontScale;
        
        ActiveFont.Draw(text, pos, Vector2.Zero, Vector2.One * Style.FontScale, color);
        
        AdvanceCursor(size.X, size.Y);
    }
    
    public static void TextWrapped(string text, float maxWidth)
    {
        // Simple word wrapping
        Vector2 pos = GetCursorPos();
        float x = 0;
        float y = 0;
        float lineHeight = ActiveFont.LineHeight * Style.FontScale;
        
        string[] words = text.Split(' ');
        foreach (string word in words)
        {
            Vector2 wordSize = ActiveFont.Measure(word + " ") * Style.FontScale;
            
            if (x + wordSize.X > maxWidth && x > 0)
            {
                x = 0;
                y += lineHeight;
            }
            
            ActiveFont.Draw(word + " ", pos + new Vector2(x, y), Vector2.Zero, Vector2.One * Style.FontScale, Style.TextColor);
            x += wordSize.X;
        }
        
        AdvanceCursor(maxWidth, y + lineHeight);
    }
    
    public static bool Button(string label, float width = 0, float height = 0)
    {
        int id = GetId(label);
        Vector2 pos = GetCursorPos();
        Vector2 textSize = ActiveFont.Measure(label) * Style.FontScale;
        
        if (width <= 0) width = textSize.X + Style.FramePadding * 2;
        if (height <= 0) height = textSize.Y + Style.FramePadding * 2;
        
        Rectangle rect = new Rectangle((int)pos.X, (int)pos.Y, (int)width, (int)height);
        bool hovered = rect.Contains(ToPoint(mousePos));
        bool held = activeId == id;
        bool pressed = false;
        
        // Handle interaction
        if (hovered)
        {
            hotId = id;
            if (mousePressed)
            {
                activeId = id;
            }
        }
        
        if (held && mouseReleased && hovered)
        {
            pressed = true;
        }
        
        // Draw
        Color bgColor = held ? Style.ButtonActiveColor : (hovered ? Style.ButtonHoveredColor : Style.ButtonColor);
        Draw.Rect(rect, bgColor);
        Draw.HollowRect(rect, Style.BorderColor);
        
        Vector2 textPos = pos + new Vector2((width - textSize.X) / 2, (height - textSize.Y) / 2);
        ActiveFont.Draw(label, textPos, Vector2.Zero, Vector2.One * Style.FontScale, Style.TextColor);
        
        AdvanceCursor(width, height);
        return pressed;
    }
    
    public static bool Checkbox(string label, ref bool value)
    {
        int id = GetId(label);
        Vector2 pos = GetCursorPos();
        float boxSize = 16;
        Vector2 textSize = ActiveFont.Measure(label) * Style.FontScale;
        
        Rectangle boxRect = new Rectangle((int)pos.X, (int)pos.Y, (int)boxSize, (int)boxSize);
        bool hovered = boxRect.Contains(ToPoint(mousePos));
        bool pressed = false;
        
        if (hovered && mousePressed)
        {
            value = !value;
            pressed = true;
        }
        
        // Draw checkbox
        Color bgColor = hovered ? Style.FrameHoveredColor : Style.FrameColor;
        Draw.Rect(boxRect, bgColor);
        Draw.HollowRect(boxRect, Style.BorderColor);
        
        if (value)
        {
            // Draw checkmark
            Draw.Line(pos + new Vector2(3, 8), pos + new Vector2(6, 12), Style.CheckmarkColor, 2);
            Draw.Line(pos + new Vector2(6, 12), pos + new Vector2(13, 3), Style.CheckmarkColor, 2);
        }
        
        // Draw label
        ActiveFont.Draw(label, pos + new Vector2(boxSize + 4, (boxSize - textSize.Y) / 2), Vector2.Zero, Vector2.One * Style.FontScale, Style.TextColor);
        
        AdvanceCursor(boxSize + 4 + textSize.X, Math.Max(boxSize, textSize.Y));
        return pressed;
    }
    
    public static bool SliderFloat(string label, ref float value, float min, float max, float width = 150)
    {
        int id = GetId(label);
        Vector2 pos = GetCursorPos();
        float height = 20;
        
        Vector2 labelSize = ActiveFont.Measure(label) * Style.FontScale;
        
        Rectangle sliderRect = new Rectangle((int)(pos.X + labelSize.X + 8), (int)pos.Y, (int)width, (int)height);
        bool hovered = sliderRect.Contains(ToPoint(mousePos));
        bool changed = false;
        
        if (hovered && mousePressed)
        {
            activeId = id;
        }
        
        if (activeId == id)
        {
            float t = MathHelper.Clamp((mousePos.X - sliderRect.X) / sliderRect.Width, 0, 1);
            float newValue = min + t * (max - min);
            if (newValue != value)
            {
                value = newValue;
                changed = true;
            }
        }
        
        // Draw
        ActiveFont.Draw(label, pos + new Vector2(0, (height - labelSize.Y) / 2), Vector2.Zero, Vector2.One * Style.FontScale, Style.TextColor);
        
        Draw.Rect(sliderRect, Style.FrameColor);
        
        float fillWidth = ((value - min) / (max - min)) * width;
        Draw.Rect(sliderRect.X, sliderRect.Y, (int)fillWidth, sliderRect.Height, Style.SliderFillColor);
        Draw.HollowRect(sliderRect, Style.BorderColor);
        
        // Value text
        string valueText = value.ToString("F2");
        Vector2 valueSize = ActiveFont.Measure(valueText) * Style.FontScale * 0.8f;
        ActiveFont.Draw(valueText, new Vector2(sliderRect.X + (width - valueSize.X) / 2, sliderRect.Y + (height - valueSize.Y) / 2), 
            Vector2.Zero, Vector2.One * Style.FontScale * 0.8f, Style.TextColor);
        
        AdvanceCursor(labelSize.X + 8 + width, height);
        return changed;
    }
    
    public static bool SliderInt(string label, ref int value, int min, int max, float width = 150)
    {
        float f = value;
        bool changed = SliderFloat(label, ref f, min, max, width);
        value = (int)f;
        return changed;
    }
    
    public static bool InputText(string label, ref string value, float width = 200)
    {
        int id = GetId(label);
        Vector2 pos = GetCursorPos();
        float height = 20;
        
        Vector2 labelSize = ActiveFont.Measure(label) * Style.FontScale;
        Rectangle inputRect = new Rectangle((int)(pos.X + labelSize.X + 8), (int)pos.Y, (int)width, (int)height);
        bool hovered = inputRect.Contains(ToPoint(mousePos));
        bool focused = activeId == id;
        bool changed = false;
        
        if (hovered && mousePressed)
        {
            activeId = id;
        }
        
        // Handle text input when focused
        if (focused)
        {
            // Handle backspace
            if (MInput.Keyboard.Pressed(Keys.Back) && value.Length > 0)
            {
                value = value.Substring(0, value.Length - 1);
                changed = true;
            }
            
            // Handle character input (simplified)
            foreach (var key in pressedKeys)
            {
                if (key >= Keys.A && key <= Keys.Z)
                {
                    bool shift = MInput.Keyboard.Check(Keys.LeftShift) || MInput.Keyboard.Check(Keys.RightShift);
                    char c = (char)('a' + (key - Keys.A));
                    if (shift) c = char.ToUpper(c);
                    value += c;
                    changed = true;
                }
                else if (key >= Keys.D0 && key <= Keys.D9 && MInput.Keyboard.Pressed(key))
                {
                    value += (char)('0' + (key - Keys.D0));
                    changed = true;
                }
            }
        }
        
        // Draw
        ActiveFont.Draw(label, pos + new Vector2(0, (height - labelSize.Y) / 2), Vector2.Zero, Vector2.One * Style.FontScale, Style.TextColor);
        
        Color bgColor = focused ? Style.FrameActiveColor : (hovered ? Style.FrameHoveredColor : Style.FrameColor);
        Draw.Rect(inputRect, bgColor);
        Draw.HollowRect(inputRect, focused ? Style.AccentColor : Style.BorderColor);
        
        // Draw text with cursor
        string displayText = value;
        if (focused && (Engine.Scene?.TimeActive ?? 0) % 1.0f < 0.5f)
        {
            displayText += "|";
        }
        ActiveFont.Draw(displayText, new Vector2(inputRect.X + 4, inputRect.Y + (height - labelSize.Y) / 2), 
            Vector2.Zero, Vector2.One * Style.FontScale, Style.TextColor);
        
        AdvanceCursor(labelSize.X + 8 + width, height);
        return changed;
    }
    
    #endregion

    #region Combo/Dropdown
    
    public static bool BeginCombo(string label, string previewValue, float width = 150)
    {
        int id = GetId(label);
        Vector2 pos = GetCursorPos();
        float height = 20;
        
        Vector2 labelSize = ActiveFont.Measure(label) * Style.FontScale;
        Rectangle comboRect = new Rectangle((int)(pos.X + labelSize.X + 8), (int)pos.Y, (int)width, (int)height);
        bool hovered = comboRect.Contains(ToPoint(mousePos));
        
        if (!windowStates.ContainsKey(id))
        {
            windowStates[id] = new WindowState();
        }
        
        var state = windowStates[id];
        
        if (hovered && mousePressed)
        {
            state.IsOpen = !state.IsOpen;
        }
        
        // Draw combo box
        ActiveFont.Draw(label, pos + new Vector2(0, (height - labelSize.Y) / 2), Vector2.Zero, Vector2.One * Style.FontScale, Style.TextColor);
        
        Color bgColor = hovered ? Style.FrameHoveredColor : Style.FrameColor;
        Draw.Rect(comboRect, bgColor);
        Draw.HollowRect(comboRect, Style.BorderColor);
        
        ActiveFont.Draw(previewValue, new Vector2(comboRect.X + 4, comboRect.Y + (height - labelSize.Y) / 2), 
            Vector2.Zero, Vector2.One * Style.FontScale, Style.TextColor);
        
        // Draw arrow
        Vector2 arrowPos = new Vector2(comboRect.Right - 12, comboRect.Y + height / 2);
        Draw.Line(arrowPos + new Vector2(-3, -2), arrowPos + new Vector2(0, 2), Style.TextColor);
        Draw.Line(arrowPos + new Vector2(0, 2), arrowPos + new Vector2(3, -2), Style.TextColor);
        
        AdvanceCursor(labelSize.X + 8 + width, height);
        
        if (state.IsOpen)
        {
            state.DropdownRect = new Rectangle(comboRect.X, comboRect.Bottom, comboRect.Width, 0);
            BeginGroup();
            currentLayout.Position = new Vector2(comboRect.X, comboRect.Bottom);
        }
        
        return state.IsOpen;
    }
    
    public static bool Selectable(string label, bool selected)
    {
        int id = GetId(label);
        Vector2 pos = GetCursorPos();
        Vector2 size = ActiveFont.Measure(label) * Style.FontScale;
        float height = size.Y + 4;
        float width = currentLayout.Size.X - currentLayout.CursorX;
        
        Rectangle rect = new Rectangle((int)pos.X, (int)pos.Y, (int)width, (int)height);
        bool hovered = rect.Contains(ToPoint(mousePos));
        bool pressed = hovered && mousePressed;
        
        Color bgColor = selected ? Style.HeaderActiveColor : (hovered ? Style.HeaderHoveredColor : Color.Transparent);
        Draw.Rect(rect, bgColor);
        
        ActiveFont.Draw(label, pos + new Vector2(4, 2), Vector2.Zero, Vector2.One * Style.FontScale, Style.TextColor);
        
        // Track dropdown height
        if (layoutStack.Count > 0)
        {
            // We're in a dropdown
        }
        
        AdvanceCursor(width, height);
        return pressed;
    }
    
    public static void EndCombo()
    {
        EndGroup();
    }
    
    #endregion

    #region Windows
    
    public static bool BeginWindow(string title, ref bool open, float x = 100, float y = 100, float width = 300, float height = 200)
    {
        if (!open) return false;
        
        int id = GetId(title);
        
        if (!windowStates.ContainsKey(id))
        {
            windowStates[id] = new WindowState
            {
                Position = new Vector2(x, y),
                Size = new Vector2(width, height)
            };
        }
        
        var state = windowStates[id];
        Rectangle windowRect = new Rectangle((int)state.Position.X, (int)state.Position.Y, (int)state.Size.X, (int)state.Size.Y);
        Rectangle titleRect = new Rectangle(windowRect.X, windowRect.Y, windowRect.Width, 24);
        
        // Handle dragging
        bool titleHovered = titleRect.Contains(ToPoint(mousePos));
        if (titleHovered && mousePressed)
        {
            state.IsDragging = true;
            state.DragOffset = mousePos - state.Position;
        }
        
        if (state.IsDragging)
        {
            if (mouseDown)
            {
                state.Position = mousePos - state.DragOffset;
            }
            else
            {
                state.IsDragging = false;
            }
        }
        
        // Draw window
        Draw.Rect(windowRect, Style.WindowBgColor);
        Draw.Rect(titleRect, Style.TitleBgColor);
        Draw.HollowRect(windowRect, Style.BorderColor);
        
        // Draw title
        ActiveFont.Draw(title, state.Position + new Vector2(8, 4), Vector2.Zero, Vector2.One * Style.FontScale, Style.TextColor);
        
        // Close button
        Rectangle closeRect = new Rectangle(windowRect.Right - 20, windowRect.Y + 4, 16, 16);
        bool closeHovered = closeRect.Contains(ToPoint(mousePos));
        if (closeHovered)
        {
            Draw.Rect(closeRect, Style.ButtonHoveredColor);
        }
        ActiveFont.Draw("X", new Vector2(closeRect.X + 4, closeRect.Y), Vector2.Zero, Vector2.One * Style.FontScale, Style.TextColor);
        
        if (closeHovered && mousePressed)
        {
            open = false;
            return false;
        }
        
        // Set up layout for window content
        layoutStack.Push(currentLayout);
        currentLayout = new LayoutContext
        {
            Position = state.Position + new Vector2(Style.WindowPadding, 24 + Style.WindowPadding),
            Size = state.Size - new Vector2(Style.WindowPadding * 2, 24 + Style.WindowPadding * 2),
            CursorX = 0,
            CursorY = 0
        };
        
        return true;
    }
    
    public static void EndWindow()
    {
        if (layoutStack.Count > 0)
        {
            currentLayout = layoutStack.Pop();
        }
    }
    
    #endregion

    #region Collapsing Headers
    
    public static bool CollapsingHeader(string label, bool defaultOpen = false)
    {
        int id = GetId(label);
        
        if (!windowStates.ContainsKey(id))
        {
            windowStates[id] = new WindowState { IsOpen = defaultOpen };
        }
        
        var state = windowStates[id];
        Vector2 pos = GetCursorPos();
        Vector2 size = ActiveFont.Measure(label) * Style.FontScale;
        float height = size.Y + 8;
        float width = currentLayout.Size.X - currentLayout.CursorX;
        
        Rectangle rect = new Rectangle((int)pos.X, (int)pos.Y, (int)width, (int)height);
        bool hovered = rect.Contains(ToPoint(mousePos));
        
        if (hovered && mousePressed)
        {
            state.IsOpen = !state.IsOpen;
        }
        
        Color bgColor = hovered ? Style.HeaderHoveredColor : Style.HeaderColor;
        Draw.Rect(rect, bgColor);
        
        // Draw arrow
        string arrow = state.IsOpen ? "▼" : "▶";
        ActiveFont.Draw(arrow, pos + new Vector2(4, 4), Vector2.Zero, Vector2.One * Style.FontScale * 0.8f, Style.TextColor);
        
        // Draw label
        ActiveFont.Draw(label, pos + new Vector2(20, 4), Vector2.Zero, Vector2.One * Style.FontScale, Style.TextColor);
        
        AdvanceCursor(width, height);
        
        if (state.IsOpen)
        {
            Indent();
        }
        
        return state.IsOpen;
    }
    
    public static void EndCollapsingHeader()
    {
        Unindent();
    }
    
    #endregion

    #region Color Picker
    
    public static bool ColorEdit(string label, ref Color color)
    {
        Vector2 pos = GetCursorPos();
        Vector2 labelSize = ActiveFont.Measure(label) * Style.FontScale;
        float previewSize = 20;
        
        // Draw label
        ActiveFont.Draw(label, pos + new Vector2(0, (previewSize - labelSize.Y) / 2), Vector2.Zero, Vector2.One * Style.FontScale, Style.TextColor);
        
        // Draw color preview
        Rectangle previewRect = new Rectangle((int)(pos.X + labelSize.X + 8), (int)pos.Y, (int)previewSize, (int)previewSize);
        Draw.Rect(previewRect, color);
        Draw.HollowRect(previewRect, Style.BorderColor);
        
        AdvanceCursor(labelSize.X + 8 + previewSize, previewSize);
        
        // RGB sliders
        int r = color.R, g = color.G, b = color.B;
        bool changed = false;
        
        changed |= SliderInt("R", ref r, 0, 255, 100);
        changed |= SliderInt("G", ref g, 0, 255, 100);
        changed |= SliderInt("B", ref b, 0, 255, 100);
        
        if (changed)
        {
            color = new Color(r, g, b);
        }
        
        return changed;
    }
    
    #endregion
}

#region Supporting Types

public class LayoutContext
{
    public Vector2 Position;
    public Vector2 Size;
    public float CursorX;
    public float CursorY;
    public float IndentLevel;
    public bool SameLine;
    public float SameLineSpacing;
    public float MaxWidth;
    public float MaxHeight;
}

public class WindowState
{
    public Vector2 Position;
    public Vector2 Size;
    public bool IsOpen;
    public bool IsDragging;
    public Vector2 DragOffset;
    public Rectangle DropdownRect;
}

public class ImGuiStyle
{
    // Colors
    public Color WindowBgColor { get; set; } = new Color(30, 30, 35, 240);
    public Color TitleBgColor { get; set; } = new Color(45, 45, 55);
    public Color BorderColor { get; set; } = new Color(80, 80, 90);
    public Color TextColor { get; set; } = Color.White;
    public Color TextDisabledColor { get; set; } = new Color(150, 150, 150);
    
    public Color ButtonColor { get; set; } = new Color(60, 60, 70);
    public Color ButtonHoveredColor { get; set; } = new Color(80, 80, 100);
    public Color ButtonActiveColor { get; set; } = new Color(100, 100, 130);
    
    public Color FrameColor { get; set; } = new Color(40, 40, 50);
    public Color FrameHoveredColor { get; set; } = new Color(55, 55, 65);
    public Color FrameActiveColor { get; set; } = new Color(70, 70, 85);
    
    public Color HeaderColor { get; set; } = new Color(50, 50, 60);
    public Color HeaderHoveredColor { get; set; } = new Color(65, 65, 80);
    public Color HeaderActiveColor { get; set; } = new Color(80, 80, 100);
    
    public Color SliderFillColor { get; set; } = new Color(255, 105, 180); // Pink accent
    public Color CheckmarkColor { get; set; } = new Color(255, 105, 180);
    public Color SeparatorColor { get; set; } = new Color(80, 80, 90);
    public Color AccentColor { get; set; } = new Color(255, 105, 180);
    
    // Sizing
    public float WindowPadding { get; set; } = 8;
    public float FramePadding { get; set; } = 4;
    public float ItemSpacing { get; set; } = 4;
    public float FontScale { get; set; } = 0.6f;
}

#endregion
