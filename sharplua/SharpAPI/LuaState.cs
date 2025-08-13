namespace SharpLua;

using KeraLua;
using LuaState = KeraLua.Lua;

class SharpAPI_LuaState
{
    internal static void Register(LuaState lua)
    {
        lua.RegistSharpLuaFunction(nameof(LuaNewState), LuaNewState);
        lua.RegistSharpLuaFunction(nameof(LuaCloseState), LuaCloseState);
        lua.RegistSharpLuaFunction(nameof(LuaDoFile), LuaDoFile);
        lua.RegistSharpLuaFunction(nameof(LuaSyncState), LuaSyncState);
    }

    static int LuaNewState(LuaState lua)
    {
        var newState = SharpLuaState.NewState();
        lua.PushObject(newState);
        return 1;
    }

    static int LuaCloseState(LuaState lua)
    {
        var state = lua.ToObject<LuaState>(1, true);
        state.Dispose();
        return 0;
    }

    static int LuaDoFile(LuaState lua)
    {
        var state = lua.ToObject<LuaState>(1, false);
        var entryFile = lua.ToString(2);
        var entryFullPath = System.IO.Path.GetFullPath(entryFile);
        if (SharpLuaState.DoFile(state, entryFullPath, out var nRet))
        {
            state.XMove(lua, nRet);
            return nRet;
        }
        else
        {
            state.XMove(lua, 1);
            return lua.SharpLuaError();
        }
    }

    static int LuaSyncState(LuaState lua)
    {
        var state = lua.ToObject<LuaState>(1, false);
        var entryName = lua.ToString(2);
        var nArgs = lua.GetTop() - 2;

        var stateTop = state.GetTop();
        state.GetGlobal(entryName);
        lua.XMove(state, nArgs);

        if (state.PCall(nArgs, SharpLuaState.LUA_MULTRET, 0) == LuaStatus.OK)
        {
            var nResults = state.GetTop() - stateTop;
            state.XMove(lua, nResults);
            return nResults;
        }
        else
        {
            state.XMove(lua, 1);
            return lua.SharpLuaError();
        }
    }
}