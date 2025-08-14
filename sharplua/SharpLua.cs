namespace SharpLua;

using System;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using KeraLua;
using LuaState = KeraLua.Lua;

public delegate int SharpLuaFunction(LuaState state);

static class SharpLua
{
    public const string LibName = "sharplua";
    public const string Version = "0.7.0";
    public const int LUA_MULTRET = -1;
    private const int EXIT_CODE_SUCCESS = 0;
    private const int EXIT_CODE_ERROR = 1;
    private const string DEFAULT_ERROR_MESSAGE = "sharplua: unknown error";

    // Track all GCHandles allocated for closures per Lua state (keyed by native handle)
    private static readonly ConcurrentDictionary<IntPtr, ConcurrentBag<GCHandle>> s_stateHandles = new();

    private static void AddPackagePath(LuaState lua, string searchPath)
    {
        lua.GetGlobal("package");
        var pkgIndex = lua.GetTop();
        var sepChar = Path.DirectorySeparatorChar;

        lua.GetField(pkgIndex, "path");
        var path = lua.ToString(-1);
        path = $"{searchPath}{sepChar}?.lua;{searchPath}{sepChar}?{sepChar}init.lua;{path}";
        lua.PushString(path);
        lua.SetField(pkgIndex, "path");

        lua.GetField(pkgIndex, "cpath");
        var cpath = lua.ToString(-1);

        if (OperatingSystem.IsWindows())
        {
            cpath = $"{searchPath}{sepChar}?.dll;{cpath}";
        }
        else if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            cpath = $"{searchPath}{sepChar}?.so;{searchPath}{sepChar}?.dylib;{cpath}";
        }

        lua.PushString(cpath);
        lua.SetField(pkgIndex, "cpath");

        lua.SetTop(pkgIndex - 1);
    }

    private static void OpenLibs(LuaState lua)
    {
        lua.OpenLibs();

        lua.NewTable();
        lua.SharpLuaRegistValue(nameof(Version), Version);

        SharpAPI_System.Register(lua);
        SharpAPI_FileSystem.Register(lua);
        SharpAPI_Network.Register(lua);
        SharpAPI_Minio.Register(lua);
        SharpAPI_Compress.Register(lua);
        SharpAPI_Process.Register(lua);
        SharpAPI_Task.Register(lua);
        SharpAPI_Prompt.Register(lua);
        SharpAPI_LuaState.Register(lua);

        lua.SetGlobal(LibName);
    }

    public static int DoMain(string[] args)
    {
        if (args.Length <= 0)
        {
            Console.Error.WriteLine($"{LibName} version {Version}, usage: {LibName} entry-lua-file-path");
            return EXIT_CODE_ERROR;
        }

        var entryFile = args[0];
        if (!File.Exists(entryFile))
        {
            Console.Error.WriteLine("entry file not exists: {0}", entryFile);
            return EXIT_CODE_ERROR;
        }

        try
        {
            var workingDir = Path.GetDirectoryName(entryFile);
            using var lua = SharpLuaNewState(workingDir);
            try
            {
                if (SharpLuaDoFile(lua, entryFile, out var nResults))
                {
                    if (nResults > 0)
                    {
                        return lua.IsInteger(-1) ? (int)lua.ToInteger(-1) : EXIT_CODE_ERROR;
                    }
                    return EXIT_CODE_SUCCESS;
                }
                else
                {
                    Console.Error.WriteLine(nResults > 0 ? lua.ToString(-1) : DEFAULT_ERROR_MESSAGE);
                    return EXIT_CODE_ERROR;
                }
            }
            finally
            {
                ReleaseSharpLuaHandles(lua);
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return EXIT_CODE_ERROR;
        }
    }

    internal static LuaState SharpLuaNewState(string workingDir = null)
    {
        if (workingDir != null)
        {
            //设置工作路径，与当前运行的entry文件同级
            Directory.SetCurrentDirectory(workingDir);
        }

        var lua = new LuaState();
        lua.Encoding = System.Text.Encoding.UTF8;

        var processDirPath = Path.GetDirectoryName(Environment.ProcessPath);
        AddPackagePath(lua, processDirPath);
        OpenLibs(lua);

        return lua;
    }

    internal static bool SharpLuaDoFile(LuaState lua, string entryFilePath, out int nResults)
    {
        var top = lua.GetTop();
        var entryFullPath = Path.GetFullPath(entryFilePath);
        var entryBuffer = LoadLuaFile(entryFullPath);
        var succ = lua.LoadBuffer(entryBuffer, entryFullPath) == LuaStatus.OK
            && lua.PCall(0, LUA_MULTRET, 0) == LuaStatus.OK;
        nResults = lua.GetTop() - top;
        return succ;
    }

    static byte[] LoadLuaFile(string path)
    {
        return File.ReadAllBytes(path);
    }

    public static void SharpLuaRegistValue(this LuaState lua, string name, string value)
    {
        lua.PushString(name);
        lua.PushString(value);
        lua.SetTable(-3);
    }
    public static void SharpLuaRegistValue(this LuaState lua, string name, long value)
    {
        lua.PushString(name);
        lua.PushInteger(value);
        lua.SetTable(-3);
    }
    public static void SharpLuaRegistValue(this LuaState lua, string name, double value)
    {
        lua.PushString(name);
        lua.PushNumber(value);
        lua.SetTable(-3);
    }
    public static void SharpLuaRegistValue(this LuaState lua, string name, bool value)
    {
        lua.PushString(name);
        lua.PushBoolean(value);
        lua.SetTable(-3);
    }

    public static void RegistSharpLuaFunction(this LuaState lua, string name, SharpLuaFunction func)
    {
        lua.PushString(name);
        lua.PushSharpLuaClosure(func);
        lua.SetTable(-3);
    }

    // 直接调用 Lua 原生 API
    private static class NativeLuaMethods
    {
        private const string LuaLibraryName = "lua54";

        [DllImport(LuaLibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int lua_error(IntPtr luaState);

    // lua_tocfunction is not used in current design

        [DllImport(LuaLibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void lua_pushcclosure(IntPtr luaState, IntPtr func, int nupvalues);

        [DllImport(LuaLibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void lua_pushlightuserdata(IntPtr luaState, IntPtr p);

        [DllImport(LuaLibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr lua_touserdata(IntPtr luaState, int index);
        internal static int lua_upvalueindex(int i)
        {
            return (int)LuaRegistry.Index - i;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    static unsafe int SharpLuaClosure(IntPtr statePtr)
    {
        // Retrieve GCHandle to managed SharpLuaFunction from upvalue
        var handlePtr = NativeLuaMethods.lua_touserdata(statePtr, NativeLuaMethods.lua_upvalueindex(1));
        var handle = GCHandle.FromIntPtr(handlePtr);
        var target = (SharpLuaFunction)handle.Target;

        var lua = LuaState.FromIntPtr(statePtr);
        try
        {
            var result = target(lua);
            if (result < 0)
            {
                if (lua.GetTop() == 0)
                {
                    lua.PushString(DEFAULT_ERROR_MESSAGE);
                }
                return NativeLuaMethods.lua_error(statePtr);
            }
            else
            {
                return result;
            }
        }
        catch (Exception e)
        {
            // Push error string and raise a Lua error (longjmp)
            lua.PushString(e.ToString());
            return NativeLuaMethods.lua_error(statePtr);
        }
    }

    private static void PushSharpLuaClosure(this LuaState lua, SharpLuaFunction func)
    {
        // Capture the managed delegate via GCHandle in a Lua upvalue (as lightuserdata)
        var handle = GCHandle.Alloc(func, GCHandleType.Normal);
        NativeLuaMethods.lua_pushlightuserdata(lua.Handle, GCHandle.ToIntPtr(handle));

        // Register handle for batch release when the Lua state is disposed/closed
        var bag = s_stateHandles.GetOrAdd(lua.Handle, static _ => new ConcurrentBag<GCHandle>());
        bag.Add(handle);

        // 获取 SharpLuaClosure 的函数指针并创建闭包（带 1 个 upvalue: GCHandle 指针）
        unsafe
        {
            delegate* unmanaged[Cdecl]<IntPtr, int> nativeClosurePtr = &SharpLuaClosure;
            NativeLuaMethods.lua_pushcclosure(lua.Handle, (IntPtr)nativeClosurePtr, 1);
        }
    }

    /// <summary>
    /// Release all GCHandles registered for the given Lua state. Call on state dispose if you host SharpLua yourself.
    /// </summary>
    public static void ReleaseSharpLuaHandles(LuaState lua)
    {
        if (lua == null) return;
        if (s_stateHandles.TryRemove(lua.Handle, out var bag))
        {
            while (bag.TryTake(out var h))
            {
                if (h.IsAllocated) h.Free();
            }
        }
    }
}

static class SharpLuaExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SharpLuaError(this LuaState lua, Exception e)
    {
        return lua.SharpLuaError(e.ToString());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SharpLuaError(this LuaState lua, string message)
    {
        lua.PushString(message);
        return -1;
    }

    public static int PushTaskResult(this LuaState lua, Task task, string type)
    {
        //make sure the task is completed
        task.Wait();

        if (task.Exception != null)
        {
            throw task.Exception.InnerException;
        }

        switch (type)
        {
            case "int":
                if (task is Task<int> taskInt)
                {
                    lua.PushInteger(taskInt.Result);
                    return 1;
                }
                return 0;
            case "long":
                if (task is Task<long> taskLong)
                {
                    lua.PushInteger(taskLong.Result);
                    return 1;
                }
                return 0;
            case "double":
                if (task is Task<double> taskDouble)
                {
                    lua.PushNumber(taskDouble.Result);
                    return 1;
                }
                return 0;
            case "float":
                if (task is Task<float> taskFloat)
                {
                    lua.PushNumber(taskFloat.Result);
                    return 1;
                }
                return 0;
            case "bool":
                if (task is Task<bool> taskBool)
                {
                    lua.PushBoolean(taskBool.Result);
                    return 1;
                }
                return 0;
            case "string":
                if (task is Task<string> taskStr)
                {
                    lua.PushString(taskStr.Result);
                    return 1;
                }
                return 0;
            default:
                return 0;
        }
    }
}

class Program
{
    static int Main(string[] args)
    {
        return SharpLua.DoMain(args);
    }
}