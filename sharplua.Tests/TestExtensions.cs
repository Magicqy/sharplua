namespace sharplua.Tests;

using SharpLua;
using LuaState = KeraLua.Lua;
using System;

/// <summary>
/// Test-specific extension APIs for validating error handling behavior
/// </summary>
static class TestExtensions
{
    internal static void RegisterTestExtensions(LuaState lua)
    {
        // 获取 sharplua 全局表，如果不存在则创建
        var type = lua.GetGlobal("sharplua");
        if (type == KeraLua.LuaType.Table)
        {
            lua.RegistSharpLuaFunction(nameof(CSharpException), CSharpException);
        }
        else
        {
            Console.WriteLine("sharplua not found");
            throw new Exception();
        }
        
        lua.Pop(1); // 弹出 sharplua 表
    }

    /// <summary>
    /// Always throws a generic exception
    /// </summary>
    static int CSharpException(LuaState lua)
    {
        throw new InvalidOperationException("Test exception from C# function");
    }
}
