namespace SharpLua;

using LuaState = KeraLua.Lua;
using System;
using Sharprompt;
using System.Collections.Generic;

static class SharpAPI_Prompt
{
    internal static void Register(LuaState lua)
    {
        Prompt.Culture = System.Globalization.CultureInfo.InvariantCulture;
        Prompt.ThrowExceptionOnCancel = true;

        lua.RegistSharpLuaFunction(nameof(PromptSetColor), PromptSetColor);
        lua.RegistSharpLuaFunction(nameof(PromptSetSymbol), PromptSetSymbol);
        lua.RegistSharpLuaFunction(nameof(PromptInput), PromptInput);
        lua.RegistSharpLuaFunction(nameof(PromptConfirm), PromptConfirm);
        lua.RegistSharpLuaFunction(nameof(PromptPassword), PromptPassword);
        lua.RegistSharpLuaFunction(nameof(PromptList), PromptList);
        lua.RegistSharpLuaFunction(nameof(PromptSelect), PromptSelect);
        lua.RegistSharpLuaFunction(nameof(PromptMultiSelect), PromptMultiSelect);
    }

    static int PromptSetColor(LuaState lua)
    {
        lua.PushNil();
        while (lua.Next(1))
        {
            var color = (ConsoleColor)lua.ToNumber(-1);
            var schema = lua.ToString(-2);
            switch (schema)
            {
                case nameof(Prompt.ColorSchema.DoneSymbol):
                    Prompt.ColorSchema.DoneSymbol = color;
                    break;
                case nameof(Prompt.ColorSchema.PromptSymbol):
                    Prompt.ColorSchema.PromptSymbol = color;
                    break;
                case nameof(Prompt.ColorSchema.Answer):
                    Prompt.ColorSchema.Answer = color;
                    break;
                case nameof(Prompt.ColorSchema.Select):
                    Prompt.ColorSchema.Select = color;
                    break;
                case nameof(Prompt.ColorSchema.Error):
                    Prompt.ColorSchema.Error = color;
                    break;
                case nameof(Prompt.ColorSchema.Hint):
                    Prompt.ColorSchema.Hint = color;
                    break;
                case nameof(Prompt.ColorSchema.DisabledOption):
                    Prompt.ColorSchema.DisabledOption = color;
                    break;
            }
            lua.Pop(1);
        }
        return 0;
    }

    static int PromptSetSymbol(LuaState lua)
    {
        lua.PushNil();
        while (lua.Next(1))
        {
            var chars = lua.ToString(-1);
            var symbol = lua.ToString(-2);
            switch (symbol)
            {
                case nameof(Prompt.Symbols.Prompt):
                    Prompt.Symbols.Prompt = new Symbol(chars, chars);
                    break;
                case nameof(Prompt.Symbols.Done):
                    Prompt.Symbols.Done = new Symbol(chars, chars);
                    break;
                case nameof(Prompt.Symbols.Error):
                    Prompt.Symbols.Error = new Symbol(chars, chars);
                    break;
                case nameof(Prompt.Symbols.Selector):
                    Prompt.Symbols.Selector = new Symbol(chars, chars);
                    break;
                case nameof(Prompt.Symbols.Selected):
                    Prompt.Symbols.Selected = new Symbol(chars, chars);
                    break;
                case nameof(Prompt.Symbols.NotSelect):
                    Prompt.Symbols.NotSelect = new Symbol(chars, chars);
                    break;
            }
            lua.Pop(1);
        }
        return 0;
    }

    static int PromptInput(LuaState lua)
    {
        var prompt = lua.ToString(1);
        var defaultValue = lua.GetTop() > 1 ? lua.ToString(2) : null;
        var placeholder = lua.GetTop() > 2 ? lua.ToString(3) : null;

        var result = Prompt.Input<string>(prompt, defaultValue, placeholder);
        lua.PushString(result);
        return 1;
    }

    static int PromptConfirm(LuaState lua)
    {
        var prompt = lua.ToString(1);
        var defaultValue = lua.ToBoolean(2);

        var result = Prompt.Confirm(prompt, defaultValue);
        lua.PushBoolean(result);
        return 1;
    }

    static int PromptPassword(LuaState lua)
    {
        var prompt = lua.ToString(1);
        var passwordChar = lua.GetTop() > 1 ? lua.ToString(2) : "*";
        string placeholder = lua.GetTop() > 2 ? lua.ToString(3) : null;

        var result = Prompt.Password(prompt, passwordChar, placeholder);
        lua.PushString(result);
        return 1;
    }

    static int PromptList(LuaState lua)
    {
        var prompt = lua.ToString(1);
        int min = lua.GetTop() > 1 ? (int)lua.ToNumber(2) : 1;
        int max = lua.GetTop() > 2 ? (int)lua.ToNumber(3) : int.MaxValue;

        var result = Prompt.List<string>(prompt, min, max);
        lua.NewTable();
        {
            var i = 1;
            foreach (var item in result)
            {
                lua.PushNumber(i++);
                lua.PushString(item);
                lua.SetTable(-3);
            }
        }
        return 1;
    }

    static int PromptSelect(LuaState lua)
    {
        var prompt = lua.ToString(1);
        var items = new List<string>();
        {
            var len = lua.Length(2);
            for (int i = 1; i <= len; i++)
            {
                lua.PushNumber(i);
                lua.GetTable(2);
                items.Add(lua.ToString(-1));
                lua.Pop(1);
            }
        }
        var selector = delegate (string item)
        {
            lua.PushString(item);
            var valType = lua.GetTable(2);
            var result = valType == KeraLua.LuaType.Nil ? item : lua.ToString(-1);
            lua.Pop(1);
            return result;
        };
        var defaultValue = lua.GetTop() >= 3 ? lua.ToString(3) : null;
        int? pageSize = lua.GetTop() >= 4 ? (int)lua.ToNumber(4) : null;

        var result = Prompt.Select<string>(prompt, items, pageSize, defaultValue, selector);
        lua.PushString(result);
        return 1;
    }

    static int PromptMultiSelect(LuaState lua)
    {
        var prompt = lua.ToString(1);
        var items = new List<string>();
        {
            var len = lua.Length(2);
            for (int i = 1; i <= len; i++)
            {
                lua.PushNumber(i);
                lua.GetTable(2);
                items.Add(lua.ToString(-1));
                lua.Pop(1);
            }
        }
        var selector = delegate (string item)
        {
            lua.PushString(item);
            var valType = lua.GetTable(2);
            var result = valType == KeraLua.LuaType.Nil ? item : lua.ToString(-1);
            lua.Pop(1);
            return result;
        };
        List<string> defaultValues = null;
        if (lua.GetTop() > 2)
        {
            defaultValues = new List<string>();
            var len = lua.Length(3);
            for (int i = 1; i <= len; i++)
            {
                lua.PushNumber(i);
                lua.GetTable(2);
                defaultValues.Add(lua.ToString(-1));
                lua.Pop(1);
            }
        }
        int min = lua.GetTop() > 3 ? (int)lua.ToNumber(4) : 1;
        int max = lua.GetTop() > 4 ? (int)lua.ToNumber(5) : int.MaxValue;
        int? pageSize = lua.GetTop() > 5 ? (int)lua.ToNumber(6) : null;

        var result = Prompt.MultiSelect(prompt, items, pageSize, min, max, defaultValues, selector);
        lua.NewTable();
        {
            var i = 1;
            foreach (var item in result)
            {
                lua.PushNumber(i++);
                lua.PushString(item);
                lua.SetTable(-3);
            }
        }
        return 1;
    }
}