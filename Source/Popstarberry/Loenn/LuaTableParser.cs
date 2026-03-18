using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MaggyHelper.Popstarberry.Loenn;

/// <summary>
/// Simple Lua table parser for Loenn definitions.
/// This is a lightweight parser for basic Lua table structures.
/// For full Lua support, consider using NLua or MoonSharp.
/// </summary>
public class LuaTableParser
{
    #region Token Types
    
    private enum TokenType
    {
        String,
        Number,
        Boolean,
        Nil,
        Identifier,
        LeftBrace,
        RightBrace,
        LeftBracket,
        RightBracket,
        Equals,
        Comma,
        Dot,
        Colon,
        EOF
    }
    
    private class Token
    {
        public TokenType Type;
        public string Value;
        public int Line;
        public int Column;
    }
    
    #endregion

    #region Lexer
    
    private string source;
    private int position;
    private int line = 1;
    private int column = 1;
    
    private List<Token> Tokenize(string input)
    {
        source = input;
        position = 0;
        line = 1;
        column = 1;
        
        var tokens = new List<Token>();
        
        while (position < source.Length)
        {
            SkipWhitespaceAndComments();
            
            if (position >= source.Length) break;
            
            char c = source[position];
            
            if (c == '{')
            {
                tokens.Add(new Token { Type = TokenType.LeftBrace, Value = "{", Line = line, Column = column });
                Advance();
            }
            else if (c == '}')
            {
                tokens.Add(new Token { Type = TokenType.RightBrace, Value = "}", Line = line, Column = column });
                Advance();
            }
            else if (c == '[')
            {
                tokens.Add(new Token { Type = TokenType.LeftBracket, Value = "[", Line = line, Column = column });
                Advance();
            }
            else if (c == ']')
            {
                tokens.Add(new Token { Type = TokenType.RightBracket, Value = "]", Line = line, Column = column });
                Advance();
            }
            else if (c == '=')
            {
                tokens.Add(new Token { Type = TokenType.Equals, Value = "=", Line = line, Column = column });
                Advance();
            }
            else if (c == ',')
            {
                tokens.Add(new Token { Type = TokenType.Comma, Value = ",", Line = line, Column = column });
                Advance();
            }
            else if (c == '.')
            {
                tokens.Add(new Token { Type = TokenType.Dot, Value = ".", Line = line, Column = column });
                Advance();
            }
            else if (c == ':')
            {
                tokens.Add(new Token { Type = TokenType.Colon, Value = ":", Line = line, Column = column });
                Advance();
            }
            else if (c == '"' || c == '\'')
            {
                tokens.Add(ReadString(c));
            }
            else if (char.IsDigit(c) || (c == '-' && position + 1 < source.Length && char.IsDigit(source[position + 1])))
            {
                tokens.Add(ReadNumber());
            }
            else if (char.IsLetter(c) || c == '_')
            {
                tokens.Add(ReadIdentifier());
            }
            else
            {
                Advance(); // Skip unknown characters
            }
        }
        
        tokens.Add(new Token { Type = TokenType.EOF, Value = "", Line = line, Column = column });
        return tokens;
    }
    
    private void Advance()
    {
        if (position < source.Length)
        {
            if (source[position] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
            position++;
        }
    }
    
    private void SkipWhitespaceAndComments()
    {
        while (position < source.Length)
        {
            if (char.IsWhiteSpace(source[position]))
            {
                Advance();
            }
            else if (position + 1 < source.Length && source[position] == '-' && source[position + 1] == '-')
            {
                // Skip line comment
                while (position < source.Length && source[position] != '\n')
                {
                    Advance();
                }
            }
            else
            {
                break;
            }
        }
    }
    
    private Token ReadString(char quote)
    {
        int startColumn = column;
        StringBuilder sb = new StringBuilder();
        Advance(); // Skip opening quote
        
        while (position < source.Length && source[position] != quote)
        {
            if (source[position] == '\\' && position + 1 < source.Length)
            {
                Advance();
                char escaped = source[position];
                switch (escaped)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case '\\': sb.Append('\\'); break;
                    case '"': sb.Append('"'); break;
                    case '\'': sb.Append('\''); break;
                    default: sb.Append(escaped); break;
                }
            }
            else
            {
                sb.Append(source[position]);
            }
            Advance();
        }
        
        Advance(); // Skip closing quote
        
        return new Token { Type = TokenType.String, Value = sb.ToString(), Line = line, Column = startColumn };
    }
    
    private Token ReadNumber()
    {
        int startColumn = column;
        StringBuilder sb = new StringBuilder();
        
        if (source[position] == '-')
        {
            sb.Append('-');
            Advance();
        }
        
        while (position < source.Length && (char.IsDigit(source[position]) || source[position] == '.'))
        {
            sb.Append(source[position]);
            Advance();
        }
        
        // Handle scientific notation
        if (position < source.Length && (source[position] == 'e' || source[position] == 'E'))
        {
            sb.Append(source[position]);
            Advance();
            
            if (position < source.Length && (source[position] == '+' || source[position] == '-'))
            {
                sb.Append(source[position]);
                Advance();
            }
            
            while (position < source.Length && char.IsDigit(source[position]))
            {
                sb.Append(source[position]);
                Advance();
            }
        }
        
        return new Token { Type = TokenType.Number, Value = sb.ToString(), Line = line, Column = startColumn };
    }
    
    private Token ReadIdentifier()
    {
        int startColumn = column;
        StringBuilder sb = new StringBuilder();
        
        while (position < source.Length && (char.IsLetterOrDigit(source[position]) || source[position] == '_'))
        {
            sb.Append(source[position]);
            Advance();
        }
        
        string value = sb.ToString();
        
        // Check for keywords
        if (value == "true" || value == "false")
        {
            return new Token { Type = TokenType.Boolean, Value = value, Line = line, Column = startColumn };
        }
        else if (value == "nil")
        {
            return new Token { Type = TokenType.Nil, Value = value, Line = line, Column = startColumn };
        }
        
        return new Token { Type = TokenType.Identifier, Value = value, Line = line, Column = startColumn };
    }
    
    #endregion

    #region Parser
    
    private List<Token> tokens;
    private int tokenIndex;
    
    private Token Current => tokenIndex < tokens.Count ? tokens[tokenIndex] : tokens[^1];
    
    private Token Consume()
    {
        Token t = Current;
        if (tokenIndex < tokens.Count - 1) tokenIndex++;
        return t;
    }
    
    private bool Match(TokenType type)
    {
        if (Current.Type == type)
        {
            Consume();
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Parse a Lua file and return the resulting table.
    /// </summary>
    public Dictionary<string, object> Parse(string luaContent)
    {
        tokens = Tokenize(luaContent);
        tokenIndex = 0;
        
        var result = new Dictionary<string, object>();
        
        // Parse top-level assignments
        while (Current.Type != TokenType.EOF)
        {
            if (Current.Type == TokenType.Identifier)
            {
                string key = Consume().Value;
                
                if (Match(TokenType.Equals))
                {
                    result[key] = ParseValue();
                }
            }
            else
            {
                Consume(); // Skip unknown tokens
            }
            
            Match(TokenType.Comma);
        }
        
        return result;
    }
    
    private object ParseValue()
    {
        switch (Current.Type)
        {
            case TokenType.String:
                return Consume().Value;
                
            case TokenType.Number:
                string numStr = Consume().Value;
                if (numStr.Contains('.') || numStr.Contains('e') || numStr.Contains('E'))
                    return double.Parse(numStr);
                return long.Parse(numStr);
                
            case TokenType.Boolean:
                return Consume().Value == "true";
                
            case TokenType.Nil:
                Consume();
                return null;
                
            case TokenType.LeftBrace:
                return ParseTable();
                
            case TokenType.Identifier:
                // Could be a reference or function call
                return Consume().Value;
                
            default:
                Consume();
                return null;
        }
    }
    
    private object ParseTable()
    {
        Match(TokenType.LeftBrace);
        
        var dict = new Dictionary<string, object>();
        var list = new List<object>();
        bool isArray = true;
        int arrayIndex = 1;
        
        while (Current.Type != TokenType.RightBrace && Current.Type != TokenType.EOF)
        {
            if (Current.Type == TokenType.LeftBracket)
            {
                // Explicit key: [key] = value
                Consume();
                object key = ParseValue();
                Match(TokenType.RightBracket);
                Match(TokenType.Equals);
                object value = ParseValue();
                
                if (key is long idx)
                {
                    dict[idx.ToString()] = value;
                }
                else
                {
                    dict[key?.ToString() ?? ""] = value;
                    isArray = false;
                }
            }
            else if (Current.Type == TokenType.Identifier)
            {
                // Check if it's "key = value" or just a value
                int savedIndex = tokenIndex;
                string identifier = Consume().Value;
                
                if (Match(TokenType.Equals))
                {
                    // Named key
                    dict[identifier] = ParseValue();
                    isArray = false;
                }
                else
                {
                    // Just a value (identifier reference)
                    tokenIndex = savedIndex;
                    list.Add(ParseValue());
                }
            }
            else
            {
                // Array element
                list.Add(ParseValue());
            }
            
            Match(TokenType.Comma);
        }
        
        Match(TokenType.RightBrace);
        
        // If it looks like an array, return as list
        if (isArray && list.Count > 0 && dict.Count == 0)
        {
            return list;
        }
        
        // Merge list items into dict if needed
        foreach (var item in list)
        {
            dict[arrayIndex.ToString()] = item;
            arrayIndex++;
        }
        
        return dict;
    }
    
    #endregion
}

/// <summary>
/// Utility methods for Lua-style data conversion.
/// </summary>
public static class LuaUtils
{
    public static string ToLuaString(object value)
    {
        if (value == null) return "nil";
        if (value is bool b) return b ? "true" : "false";
        if (value is string s) return $"\"{EscapeString(s)}\"";
        if (value is IDictionary<string, object> dict) return TableToLua(dict);
        if (value is IList<object> list) return ListToLua(list);
        return value.ToString();
    }
    
    private static string EscapeString(string s)
    {
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
    }
    
    private static string TableToLua(IDictionary<string, object> dict)
    {
        var sb = new StringBuilder();
        sb.Append("{\n");
        
        foreach (var kvp in dict)
        {
            if (int.TryParse(kvp.Key, out _))
            {
                // Array-style
                sb.Append($"    {ToLuaString(kvp.Value)},\n");
            }
            else
            {
                // Named key
                sb.Append($"    {kvp.Key} = {ToLuaString(kvp.Value)},\n");
            }
        }
        
        sb.Append("}");
        return sb.ToString();
    }
    
    private static string ListToLua(IList<object> list)
    {
        var sb = new StringBuilder();
        sb.Append("{ ");
        
        for (int i = 0; i < list.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(ToLuaString(list[i]));
        }
        
        sb.Append(" }");
        return sb.ToString();
    }
}
