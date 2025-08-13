namespace SharpLua;

using LuaState = KeraLua.Lua;
using System;
using System.Threading.Tasks;
using System.Threading;

static class SharpAPI_Task
{
    public static void Register(LuaState lua)
    {
        lua.RegistSharpLuaFunction(nameof(TaskDelay), TaskDelay);

        lua.RegistSharpLuaFunction(nameof(TaskStatus), TaskStatus);
        lua.RegistSharpLuaFunction(nameof(TaskWait), TaskWait);
        lua.RegistSharpLuaFunction(nameof(TaskRun), TaskRun);
    }

    static int TaskDelay(IntPtr statePtr)
    {
        var lua = LuaState.FromIntPtr(statePtr);
        try
        {
            var delay = (int)lua.ToNumber(1);
            var task = Task.Delay(delay);
            lua.PushObject(task);
            return 1;
        }
        catch (Exception e)
        {
            return lua.SharpLuaError(e);
        }
    }

    static int TaskStatus(IntPtr statePtr)
    {
        var lua = LuaState.FromIntPtr(statePtr);
        try
        {
            var task = lua.ToObject<Task>(1, false);
            var status = (int)task.Status;
            lua.PushInteger(status);
            return 1;
        }
        catch (Exception e)
        {
            return lua.SharpLuaError(e);
        }
    }

    static int TaskWait(IntPtr statePtr)
    {
        var lua = LuaState.FromIntPtr(statePtr);
        try
        {
            var task = lua.ToObject<Task>(1, true);
            var type = lua.IsString(2) ? lua.ToString(2) : "void";
            return lua.PushTaskResult(task, type);
        }
        catch (Exception e)
        {
            return lua.SharpLuaError(e);
        }
    }

    /*
        参数说明：
        1.任务迭代函数，每次迭代返回一个任务以及任务结束后的回调函数，也可以使用协程来实现迭代器
        2.任务执行并发数量，可选，默认值为8
    */
    static int TaskRun(IntPtr statePtr)
    {
        var lua = LuaState.FromIntPtr(statePtr);
        try
        {
            var top = lua.GetTop();
            var maxConcurrentCount = 8;
            if (top >= 2)
            {
                maxConcurrentCount = (int)lua.ToNumber(2);
            }

            var taskArray = new Task[maxConcurrentCount];
            var taskArrayIndex = 0;
            var taskConcurrentCount = 0L;
            var taskResultType = new string[maxConcurrentCount];
            var taskCbRef = new int[maxConcurrentCount];

            static bool ProcessTaskResult(LuaState lua, Task[] taskArray, string[] taskResultType, int[] taskCbRef, int index)
            {
                var task = taskArray[index];
                var type = taskResultType[index];
                var cbRef = taskCbRef[index];

                lua.PushInteger(cbRef);
                lua.GetTable(KeraLua.LuaRegistry.Index);
                var nRet = lua.PushTaskResult(task, type);
                var hasError = lua.PCall(nRet, 0, 0) != KeraLua.LuaStatus.OK;
                lua.Unref(KeraLua.LuaRegistry.Index, cbRef);
                taskArray[index] = null;
                return hasError;
            }

            //是否出现错误
            var hasError = false;
            while (true)
            {
                //执行迭代器以获得任务，若迭代器执行出现错误，需要返回错误信息并停止循环
                lua.PushCopy(1);
                if (lua.PCall(0, 3, 0) != KeraLua.LuaStatus.OK)
                {
                    hasError = true;
                    break;
                }
                if (lua.IsNil(top + 1))
                {
                    //等待所有已完成的任务执行结果回调方法
                    using var taskWait = new AutoResetEvent(false);
                    for (var index = 0; index < taskArray.Length; index++)
                    {
                        var t = taskArray[index];
                        if (t != null)
                        {
                            t.ContinueWith(t =>
                            {
                                Interlocked.Decrement(ref taskConcurrentCount);
                                taskWait.Set();
                            });
                        }
                    }

                    while (Interlocked.Read(ref taskConcurrentCount) > 0)
                    {
                        taskWait.WaitOne();
                        for (var i = 0; i < taskArray.Length; i++)
                        {
                            var t = taskArray[i];
                            if (t == null)
                            {
                                continue;
                            }
                            if (t.IsCompleted)
                            {
                                if (ProcessTaskResult(lua, taskArray, taskResultType, taskCbRef, i))
                                {
                                    taskArray[i] = null;
                                    hasError = true;
                                    goto TaskHasError;
                                }
                                taskArray[i] = null;
                            }
                        }
                    }

                TaskHasError://回调处理时发生错误
                    break;
                }

                //迭代器结果：任务对象、任务返回值类型、任务结束后的回调
                var task = lua.ToObject<Task>(top + 1, true);
                var type = lua.ToString(top + 2);
                var cbRef = lua.Ref(KeraLua.LuaRegistry.Index);
                lua.SetTop(top);

                taskArray[taskArrayIndex] = task;
                taskResultType[taskArrayIndex] = type;
                taskCbRef[taskArrayIndex] = cbRef;
                taskConcurrentCount++;

                if (taskConcurrentCount < maxConcurrentCount)
                {
                    taskArrayIndex++;
                }
                else
                {
                    var completeIndex = Task.WaitAny(taskArray);
                    if (ProcessTaskResult(lua, taskArray, taskResultType, taskCbRef, completeIndex))
                    {
                        hasError = true;
                        break;
                    }
                    taskArrayIndex = completeIndex;
                    taskConcurrentCount--;
                }
            }

            if (hasError)
            {
                return lua.Error();
            }
            else
            {
                return 0;
            }
        }
        catch (Exception e)
        {
            return lua.SharpLuaError(e);
        }
    }
}