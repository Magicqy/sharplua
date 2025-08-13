namespace SharpLua;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using KeraLua;
using LuaState = KeraLua.Lua;

class SharpAPI_Process
{

    private static int batchId = 0;
    private static Dictionary<int, BatchProcess> batchMap = new Dictionary<int, BatchProcess>();

    internal static void Register(LuaState lua)
    {
        lua.SharpLuaRegistFunction(nameof(StartProcess), StartProcess);
        lua.RegistSharpLuaFunction(nameof(CreateBatchProcess), CreateBatchProcess);
        lua.RegistSharpLuaFunction(nameof(AddBatchProcess), AddBatchProcess);
        lua.RegistSharpLuaFunction(nameof(RunBatchProcess), RunBatchProcess);
    }

    static int StartProcess(LuaState lua)
    {
        using var pdata = CreateProcessData(lua, 0);
        var exitCode = 0;
        try
        {
            var ptask = pdata.StartAsync();
            ptask.Wait();
            exitCode = pdata.process.ExitCode;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            exitCode = 1;
        }

        lua.PushInteger(exitCode);
        if (pdata.redirectOutput && pdata.outputBuffer != null)
        {
            lua.PushString(pdata.outputBuffer.ToString());
            return 2;
        }
        else
        {
            return 1;
        }
    }

    private static ProcessData CreateProcessData(LuaState luaState, int startOffset)
    {
        var argCount = luaState.GetTop() - startOffset;

        var fileName = luaState.ToString(startOffset + 1);

        var args = string.Empty;
        if (argCount >= 2)
        {
            args = luaState.ToString(startOffset + 2);
        }

        var redirectOutput = false;
        if (argCount >= 3)
        {
            redirectOutput = luaState.ToBoolean(startOffset + 3);
        }

        int? redirectFuncRef = null;
        string workingDir = null;
        if (redirectOutput)
        {
            if (argCount >= 4)
            {
                if (luaState.IsFunction(startOffset + 4))
                {
                    luaState.PushCopy(startOffset + 4);
                    redirectFuncRef = luaState.Ref(LuaRegistry.Index);
                }
            }
            if (argCount >= 5)
            {
                workingDir = luaState.ToString(startOffset + 5);
            }
        }
        else
        {
            if (argCount >= 4)
            {
                workingDir = luaState.ToString(startOffset + 4);
            }
        }

        return new ProcessData(fileName, args, workingDir, redirectOutput, luaState, redirectFuncRef);
    }

    static int CreateBatchProcess(IntPtr statePtr)
    {
        var lua = LuaState.FromIntPtr(statePtr);
        try
        {
            if (lua.IsFunction(1) == false)
            {
                lua.PushNil();
                return 1;
            }

            int cbFuncRef = lua.Ref(LuaRegistry.Index);

            batchId++;
            BatchProcess batch = new BatchProcess(batchId, cbFuncRef);
            batchMap.Add(batchId, batch);

            lua.PushInteger(batchId);
            return 1;
        }
        catch (Exception e)
        {
            return lua.SharpLuaError(e);
        }
    }

    static int AddBatchProcess(IntPtr statePtr)
    {
        var lua = LuaState.FromIntPtr(statePtr);
        try
        {
            int batchId = (int)lua.ToInteger(1);
            BatchProcess batch = null;
            if (!batchMap.TryGetValue(batchId, out batch))
            {
                lua.PushBoolean(false);
                return 1;
            }

            var procData = CreateProcessData(lua, 1);
            batch.procDataList.Add(procData);

            var procIndex = batch.procDataList.Count;
            lua.PushInteger(procIndex);
            return 1;
        }
        catch (Exception e)
        {
            return lua.SharpLuaError(e);
        }
    }

    static int maxProcessNum = 32;
    static int RunBatchProcess(IntPtr statePtr)
    {
        var lua = LuaState.FromIntPtr(statePtr);
        try
        {
            var batchId = (int)lua.ToInteger(1);
            BatchProcess batch = null;
            if (!batchMap.TryGetValue(batchId, out batch))
            {
                lua.PushBoolean(false);
                return 1;
            }
            var processDataList = batch.procDataList;
            var cbFuncRef = batch.luaCallbackRef;
            try
            {
                int len = Math.Min(maxProcessNum, processDataList.Count);
                Task[] taskArray = new Task[len];
                int[] taskIndexArray = new int[len];
                for (int i = 0; i < len; i++)
                {
                    var processData = processDataList[i];
                    processData.StartProcess();
                    var task = processData.process.WaitForExitAsync();
                    taskArray[i] = task;
                    taskIndexArray[i] = i;
                }
                int runningIndex = len;
                var hasExitCount = 0;
                while (true)
                {
                    if (runningIndex == processDataList.Count)
                    {
                        Task.WaitAll(taskArray);
                        for (int index = 0; index < taskArray.Length; index++)
                        {
                            int processDataIndex = taskIndexArray[index];
                            var processData = processDataList[processDataIndex];
                            BatchCallback(lua, batchId, processDataIndex);
                            hasExitCount++;
                        }
                    }
                    else
                    {
                        int index = Task.WaitAny(taskArray);

                        int processDataIndex = taskIndexArray[index];
                        var processData = processDataList[processDataIndex];
                        BatchCallback(lua, batchId, processDataIndex);

                        var newProcessData = processDataList[runningIndex];
                        newProcessData.StartProcess();
                        var task = newProcessData.process.WaitForExitAsync();
                        taskArray[index] = task;
                        taskIndexArray[index] = runningIndex;

                        runningIndex++;
                        hasExitCount++;
                    }
                    if (hasExitCount == processDataList.Count)
                    {
                        break;
                    }
                }

                lua.PushBoolean(true);
                return 1;
            }
            catch (Exception e)
            {
                return lua.SharpLuaError(e);
            }
            finally
            {
                lua.Unref(LuaRegistry.Index, cbFuncRef);
                batchMap.Remove(batchId);
                foreach (var processData in processDataList)
                {
                    processData?.Dispose();
                }
            }
        }
        catch (Exception e)
        {
            return lua.SharpLuaError(e);
        }
    }

    static void BatchCallback(LuaState lua, int batchId, int index)
    {
        if (batchMap.TryGetValue(batchId, out var batch))
        {
            var processDataList = batch.procDataList;
            var processData = processDataList[index];
            var cbFuncRef = batch.luaCallbackRef;
            lua.RawGetInteger(LuaRegistry.Index, cbFuncRef);
            lua.PushInteger(batchId);
            var indexForLua = index + 1;
            lua.PushInteger(indexForLua);
            lua.PushInteger(processData.process.ExitCode);
            if (processData.redirectOutput && processData.outputBuffer != null)
            {
                lua.PushString(processData.outputBuffer.ToString());
                if (lua.PCall(4, 0, 0) != LuaStatus.OK)
                {
                    var luaError = lua.ToString(-1);
                    Console.WriteLine(luaError);
                }
            }
            else
            {
                if (lua.PCall(3, 0, 0) != LuaStatus.OK)
                {
                    var luaError = lua.ToString(-1);
                    Console.WriteLine(luaError);
                }
            }
        }
    }
}

class BatchProcess : IDisposable
{
    public int batchId;
    public List<ProcessData> procDataList = new List<ProcessData>();
    public int luaCallbackRef;
    public BatchProcess(int id, int reference)
    {
        batchId = id;
        luaCallbackRef = reference;
    }

    public void Dispose()
    {
        foreach (var processData in procDataList)
        {
            processData.Dispose();
        }
        procDataList.Clear();
    }
}

class ProcessData : IDisposable
{
    public string fileName;
    public string args;
    public string workingDir;
    public bool redirectOutput;
    public Process process;
    public StringBuilder outputBuffer;
    public LuaState luaState;
    public int? luaRedirectFuncRef;
    public ProcessData(string fileName, string args, string workingDir, bool redirectOutput, LuaState luaState, int? luaRedirectFuncRef)
    {
        this.fileName = fileName;
        this.args = args;
        this.workingDir = workingDir;
        this.redirectOutput = redirectOutput;
        this.luaState = luaState;
        this.luaRedirectFuncRef = luaRedirectFuncRef;
    }

    public async Task StartAsync(bool continueOnCapturedContext = false)
    {
        StartProcess();
        await process.WaitForExitAsync().ConfigureAwait(continueOnCapturedContext);
    }

    public void StartProcess()
    {
        var process = new Process();
        this.process = process;

        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = args;
        if (workingDir != null)
        {
            process.StartInfo.WorkingDirectory = workingDir;
        }
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardInput = false;
        //默认开启重定向输出，参数只控制是否回调输出结果，避免进程的输出内容污染主程序自身输出
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        if (redirectOutput)
        {
            //如果有回调函数，就不需要缓存输出
            if (luaRedirectFuncRef == null)
            {
                outputBuffer = new StringBuilder();
            }
            //重定向输出
            process.OutputDataReceived += OutputHandler;
            process.ErrorDataReceived += OutputHandler;
        }

        process.Start();
        //因为默认开启了重定向输出，所以需要先开始读取输出，然后再WaitForExit避免死锁
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    private void OutputHandler(object sender, DataReceivedEventArgs args)
    {
        var line = args.Data;
        if (line != null)
        {
            if (outputBuffer != null)
            {
                lock (outputBuffer)
                {
                    outputBuffer.AppendLine(line);
                }
            }
            else if (luaRedirectFuncRef != null)
            {
                lock (luaState)
                {
                    var top = luaState.GetTop();
                    try
                    {
                        luaState.PushInteger(luaRedirectFuncRef.Value);
                        luaState.GetTable(LuaRegistry.Index);
                        luaState.PushString(line);
                        if (luaState.PCall(1, 0, 0) != LuaStatus.OK)
                        {
                            var luaError = luaState.ToString(-1);
                            Console.WriteLine(luaError);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    finally
                    {
                        luaState.SetTop(top);
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        process?.Dispose();
        if (luaState != null)
        {
            if (luaRedirectFuncRef != null)
            {
                luaState.Unref(LuaRegistry.Index, luaRedirectFuncRef.Value);
                luaRedirectFuncRef = null;
            }
            luaState = null;
        }
    }
}