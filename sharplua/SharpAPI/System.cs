namespace SharpLua;

using LuaState = KeraLua.Lua;
using System;
using System.Collections;

static class SharpAPI_System
{
    internal static void Register(LuaState lua)
    {
        lua.RegistSharpLuaFunction(nameof(Exit), Exit);
        lua.RegistSharpLuaFunction(nameof(ReadKey), ReadKey);
        lua.RegistSharpLuaFunction(nameof(ReadLine), ReadLine);
        lua.RegistSharpLuaFunction(nameof(GetOSType), GetOSType);
        lua.RegistSharpLuaFunction(nameof(GetCommandLineArgs), GetCommandLineArgs);
        lua.RegistSharpLuaFunction(nameof(GetEnvironmentVariable), GetEnvironmentVariable);
        lua.RegistSharpLuaFunction(nameof(GetEnvironmentVariables), GetEnvironmentVariables);
    }

    private static int Exit(LuaState lua)
    {
        var exitCode = (int)lua.ToNumber(1);
        Environment.Exit(exitCode);
        return 0;
    }

    static int ReadKey(LuaState lua)
    {
        var info = Console.ReadKey();
        var keyChar = info.KeyChar.ToString();
        var hasAlt = info.Modifiers.HasFlag(ConsoleModifiers.Alt);
        var hasShift = info.Modifiers.HasFlag(ConsoleModifiers.Shift);
        var hasCtrl = info.Modifiers.HasFlag(ConsoleModifiers.Control);
        lua.PushString(keyChar);
        lua.PushBoolean(hasAlt);
        lua.PushBoolean(hasShift);
        lua.PushBoolean(hasCtrl);
        return 4;
    }

    static int ReadLine(LuaState lua)
    {
        var line = Console.ReadLine();
        lua.PushString(line);
        return 1;
    }

    private static int GetOSType(LuaState lua)
    {
        if (OperatingSystem.IsWindows())
        {
            lua.PushString("Windows");
        }
        else if (OperatingSystem.IsMacOS())
        {
            lua.PushString("MacOS");
        }
        else if (OperatingSystem.IsAndroid())
        {
            lua.PushString("Android");
        }
        else if (OperatingSystem.IsIOS())
        {
            lua.PushString("iOS");
        }
        else if (OperatingSystem.IsLinux())
        {
            lua.PushString("Linux");
        }
        else
        {
            lua.PushNil();
        }
        return 1;
    }

    private static int GetCommandLineArgs(LuaState lua)
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length <= 2)
            return 0;

        lua.CreateTable(args.Length - 1, 0);
        for (int i = 2; i < args.Length; i++)
        {
            lua.PushString(args[i]);
            lua.RawSetInteger(-2, i - 1);
        }

        return 1;
    }

    static int GetEnvironmentVariable(LuaState lua)
    {
        var key = lua.ToString(1);
        var val = Environment.GetEnvironmentVariable(key);
        if (val != null)
        {
            lua.PushString(val);
        }
        else
        {
            lua.PushNil();
        }
        return 1;
    }

    static int GetEnvironmentVariables(LuaState lua)
    {
        var variables = Environment.GetEnvironmentVariables();
        lua.CreateTable(variables.Count, 0);

        foreach (DictionaryEntry de in variables)
        {
            lua.PushString(de.Key.ToString());
            lua.PushString(de.Value.ToString());
            lua.SetTable(-3);
        }
        return 1;
    }
}