namespace SharpLua;

using System;
using System.IO;
using System.Threading.Tasks;

using LuaState = KeraLua.Lua;
using LuaFunction = KeraLua.LuaFunction;
using LuaStatus = KeraLua.LuaStatus;

public delegate int SharpLuaFunction(LuaState state);

public static class SharpLuaState
{
    public const string LibName = "sharplua";
    public const string Version = "0.7.0";
    public const int LUA_MULTRET = -1;
    private const int EXIT_CODE_ERROR = 1;

    private static void AddPackagePath(LuaState lua, string searchPath)
    {
        if (string.IsNullOrEmpty(searchPath))
        {
            return;
        }

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
            return 1;
        }

        var entryFile = Path.GetFullPath(args[0]);
        if (!File.Exists(entryFile))
        {
            Console.Error.WriteLine("entry file not exists: {0}", entryFile);
            return 1;
        }

        try
        {
            var workingDir = Path.GetDirectoryName(entryFile);
            using var lua = NewState(workingDir);
            if (DoFile(lua, entryFile, out var nResults))
            {
                if (nResults > 0)
                {
                    return lua.IsInteger(-1) ? (int)lua.ToInteger(-1) : EXIT_CODE_ERROR;
                }
                return 0;
            }
            else
            {
                var error = lua.ToString(-1);
                Console.WriteLine(error);
                return EXIT_CODE_ERROR;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return EXIT_CODE_ERROR;
        }
    }

    public static LuaState NewState(string workingDir = null)
    {
        if (workingDir != null)
        {
            //设置工作目录，应该使用entryFile所在目录
            Directory.SetCurrentDirectory(workingDir);
        }

        var lua = new LuaState();
        lua.Encoding = System.Text.Encoding.UTF8;

        var processDirPath = Path.GetDirectoryName(Environment.ProcessPath);
        AddPackagePath(lua, processDirPath);
        OpenLibs(lua);

        return lua;
    }

    public static bool DoFile(LuaState lua, string entryFilePath, out int nResults)
    {
        var entryFullPath = Path.GetFullPath(entryFilePath);
        var top = lua.GetTop();
        lua.PushSharpLuaClosure(SharpLuaEntryFunc);
        lua.PushString(entryFullPath);
        var succ = lua.PCall(1, LUA_MULTRET, 0) == LuaStatus.OK;
        nResults = lua.GetTop() - top;
        return succ;
    }

    // 在NET中使用lua_error方法存在隐患，因为lua_error内部使用了long jump，方法调用后不会再返回
    // 这个行为会破坏NET的函数调用栈，造成NET异常以及程序崩溃
    // 运行时抛出System.Runtime.InteropServices.SEHException
    // TODO：在NET中不可以使用原生的lua_error方法，需要使用替代方案在离开NET托管环境以后再调用lua_error，可以参考tolua/xlua在c代码中实现的包装函数的方案
    static LuaFunction SharpLuaEntryFunc = (IntPtr statePtr) =>
    {
        var lua = LuaState.FromIntPtr(statePtr);
        try
        {
            var entryPath = lua.ToString(-1);
            var top = lua.GetTop();
            var entryBuffer = LoadLuaFile(entryPath);
            var succ = lua.LoadBuffer(entryBuffer, entryPath) == LuaStatus.OK
                && lua.PCall(0, LUA_MULTRET, 0) == LuaStatus.OK;
            return succ ? lua.GetTop() - top : lua.Error();
        }
        catch (Exception e)
        {
            return SharpLuaError(lua, e);
        }
    };

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
        lua.PushSharpLuaClosure((IntPtr statePtr) =>
        {
            var lua = LuaState.FromIntPtr(statePtr);
            try
            {
                return func(lua);
            }
            catch (Exception e)
            {
                return SharpLuaError(lua, e);
            }
        });
        lua.SetTable(-3);
    }

    static int SharpLuaError(LuaState lua, Exception e)
    {
        lua.PushBoolean(true);
        lua.Replace(LuaState.UpValueIndex(1));
        lua.PushString(e.ToString());
        return 1;
    }

    //provent registed lua function be collected by GC, and use Concurrent to support multiple threading registration
    static readonly System.Collections.Concurrent.ConcurrentBag<LuaFunction> registedFunctions = new ();

    private static void PushSharpLuaClosure(this LuaState lua, LuaFunction func)
    {
        lua.PushBoolean(false);
        lua.PushCFunction(func);
        lua.PushCClosure(SharpLuaClosure, 2);
        registedFunctions.Add(func);
    }

    // 包装函数将C#中的异常接入Lua的错误处理流程中去，将C#方法执行是否有异常的结果记录到第一个upvalue中
    private static LuaFunction SharpLuaClosure = delegate (IntPtr statePtr)
    {
        var lua = LuaState.FromIntPtr(statePtr);
        var func = lua.ToCFunction(LuaState.UpValueIndex(2));
        int result = func(statePtr);
        if (lua.ToBoolean(LuaState.UpValueIndex(1)))
        {
            lua.PushBoolean(false);
            lua.Replace(LuaState.UpValueIndex(1));
            //C# Exception message is on top of the stack, return it to Lua as Error
            var errMsg = lua.ToString(-1);
            //luaL_error adds at the beginning of the message the file name and the line number where the error occurred, if this information is available.
            return lua.Error(errMsg);
        }

        return result;
    };
}

static class SharpLuaExt
{
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
        return SharpLuaState.DoMain(args);
    }
}