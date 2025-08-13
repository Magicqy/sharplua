namespace SharpLua;

using LuaState = KeraLua.Lua;
using System;
using System.Collections;

static class SharpAPI_System
{
    public static void Register(LuaState lua)
    {
        lua.RegistSharpLuaFunction(nameof(Exit), Exit);
        lua.RegistSharpLuaFunction(nameof(ReadKey), ReadKey);
        lua.RegistSharpLuaFunction(nameof(ReadLine), ReadLine);
        lua.RegistSharpLuaFunction(nameof(GetOSType), GetOSType);
        lua.RegistSharpLuaFunction(nameof(GetCommandLineArgs), GetCommandLineArgs);
        lua.RegistSharpLuaFunction(nameof(GetEnvironmentVariable), GetEnvironmentVariable);
        lua.RegistSharpLuaFunction(nameof(GetEnvironmentVariables), GetEnvironmentVariables);
    }

    private static int Exit(IntPtr statePtr)
    {
        var lua = LuaState.FromIntPtr(statePtr);
        try
        {
            var exitCode = (int)lua.ToNumber(1);
            Environment.Exit(exitCode);
            return 0;
        }
        catch (Exception e)
        {
            return lua.SharpLuaError(e);
        }
    }
    static int ReadKey(IntPtr statePtr)
    {
        var lua = LuaState.FromIntPtr(statePtr);
        try
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
        catch (Exception e)
        {
            return lua.SharpLuaError(e);
        }
    }

    static int ReadLine(IntPtr statePtr)
    {
        var lua = LuaState.FromIntPtr(statePtr);
        try
        {
            var line = Console.ReadLine();
            lua.PushString(line);
            return 1;
        }
        catch (Exception e)
        {
            return lua.SharpLuaError(e);
        }
    }

    private static int GetOSType(IntPtr statePtr)
    {
        var lua = LuaState.FromIntPtr(statePtr);
        try
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
        catch (Exception e)
        {
            return lua.SharpLuaError(e);
        }
    }

    private static int GetCommandLineArgs(IntPtr statePtr)
    {
        var lua = LuaState.FromIntPtr(statePtr);
        try
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
        catch (Exception e)
        {
            return lua.SharpLuaError(e);
        }
    }

    static int GetEnvironmentVariable(IntPtr statePtr)
    {
        var lua = LuaState.FromIntPtr(statePtr);
        try
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
        catch (Exception e)
        {
            return lua.SharpLuaError(e);
        }
    }

    static int GetEnvironmentVariables(IntPtr statePtr)
    {
        var lua = LuaState.FromIntPtr(statePtr);
        try
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
        catch (Exception e)
        {
            return lua.SharpLuaError(e);
        }
    }
}