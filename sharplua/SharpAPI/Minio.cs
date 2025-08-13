namespace SharpLua
{
    /*
        documentation: https://minio.org.cn/docs/minio/linux/developers/dotnet/minio-dotnet.html#id3 
        demon: https://github.com/minio/minio-dotnet.git
    */

    using LuaState = KeraLua.Lua;
    using MinioAPI;
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Threading;

    static class SharpAPI_MinIO
    {
        private static MinioInstance instance;

        private static DateTime StartTimeUTC = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public static void Register(LuaState lua)
        {
            lua.RegistSharpLuaFunction(nameof(MinIO_Init), MinIO_Init);
            lua.RegistSharpLuaFunction(nameof(MinIO_Dispose), MinIO_Dispose);
            lua.RegistSharpLuaFunction(nameof(MinIO_UploadFile), MinIO_UploadFile);
            lua.RegistSharpLuaFunction(nameof(MinIO_UploadFileAsync), MinIO_UploadFileAsync);
            lua.RegistSharpLuaFunction(nameof(MinIO_UploadDirectory), MinIO_UploadDirectory);
            lua.RegistSharpLuaFunction(nameof(MinIO_DeleteFile), MinIO_DeleteFile);
            lua.RegistSharpLuaFunction(nameof(MinIO_DeleteFileAsync), MinIO_DeleteFileAsync);
            lua.RegistSharpLuaFunction(nameof(MinIO_DownloadFile), MinIO_DownloadFile);
            lua.RegistSharpLuaFunction(nameof(MinIO_DownloadFileAsync), MinIO_DownloadFileAsync);
            lua.RegistSharpLuaFunction(nameof(MinIO_ListBuckets), MinIO_ListBuckets);
            lua.RegistSharpLuaFunction(nameof(MinIO_ListFiles), MinIO_ListFiles);
        }

        //功能：初始化，需要提供服务器地址和密钥参数
        //lua调用示例: sharplua.MinIO_Init(endPoint, accessKey, secretKey)
        static int MinIO_Init(IntPtr statePtr)
        {
            var lua = LuaState.FromIntPtr(statePtr);
            try
            {
                var endPoint = lua.ToString(1);
                var accessKey = lua.ToString(2);
                var secretKey = lua.ToString(3);
                instance = new MinioInstance(endPoint, accessKey, secretKey);
                return 0;
            }
            catch (Exception e)
            {
                return lua.SharpLuaError(e);
            }
        }

        static int MinIO_Dispose(IntPtr statePtr)
        {
            var lua = LuaState.FromIntPtr(statePtr);
            try
            {
                if (instance != null)
                {
                    instance.Dispose();
                    instance = null;
                }
                return 0;
            }
            catch (Exception e)
            {
                return lua.SharpLuaError(e);
            }
        }

        //功能：上传文件（同名则直接替换）
        //lua调用示例: sharplua.MinIO_UploadFile(bucketName, srcPath, tarPath)
        //调用结果: MinIO远端会添加bucketName/tarPath,tarPath相当于是objName
        //注意：bucket只包含小写字母、数字和短横线（-），不支持其他字符;同名文件会直接覆盖，如果bucket开启版本控制，能看到同名文件的不同历史版本
        static int MinIO_UploadFile(IntPtr statePtr)
        {
            var lua = LuaState.FromIntPtr(statePtr);
            try
            {
                var bucketName = lua.ToString(1);
                var srcPath = lua.ToString(2);
                var tarPath = lua.ToString(3);
                var result = instance.UploadFileAsync(bucketName, srcPath, tarPath).Result;
                lua.PushInteger(result);
                return 1;
            }
            catch (Exception e)
            {
                return lua.SharpLuaError(e);
            }
        }

        static int MinIO_UploadFileAsync(IntPtr statePtr)
        {
            var lua = LuaState.FromIntPtr(statePtr);
            try
            {
                var bucketName = lua.ToString(1);
                var srcPath = lua.ToString(2);
                var tarPath = lua.ToString(3);
                var task = instance.UploadFileAsync(bucketName, srcPath, tarPath);
                lua.PushObject(task);
                return 1;
            }
            catch (Exception e)
            {
                return lua.SharpLuaError(e);
            }
        }

        //功能：上传目录下所有文件（同名则直接替换）
        //lua调用示例: sharplua.MinIO_UploadDirectory(bucketName, folderPath, searchPattern, searchTopDirOnly)
        //调用结果: 将folderPath下匹配到的所有文件，按文件路径上传
        static int MinIO_UploadDirectory(IntPtr statePtr)
        {
            var lua = LuaState.FromIntPtr(statePtr);
            try
            {
                var bucketName = lua.ToString(1);
                var folderPath = lua.ToString(2);
                var searchPattern = lua.ToString(3);
                var searchTopDirOnly = lua.ToBoolean(4);
                var errorCode = 0;
                var files = Directory.EnumerateFiles(folderPath, searchPattern, searchTopDirOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories);
                if (files != null)
                {
                    var batchTaskList = new List<Task<int>>();
                    foreach (var item in files)
                    {
                        var task = instance.UploadFileAsync(bucketName, item, item.Replace("\\", "/"));
                        batchTaskList.Add(task);
                    }
                    Task.WhenAll(batchTaskList).Wait();
                    foreach (var item in batchTaskList)
                    {
                        if (item.Result != 0)
                        {
                            errorCode = item.Result;
                            break;
                        }
                    }
                }
                lua.PushInteger(errorCode);
                return 1;
            }
            catch (Exception e)
            {
                return lua.SharpLuaError(e);
            }
        }

        //功能：删除远端文件
        //lua调用示例: sharplua.MinIO_DeleteFile(bucketName, tarPath)
        //调用结果: MinIO远端会移除bucketName/tarPath
        static int MinIO_DeleteFile(IntPtr statePtr)
        {
            var lua = LuaState.FromIntPtr(statePtr);
            try
            {
                var bucketName = lua.ToString(1);
                var objName = lua.ToString(2);
                var errorCode = instance.DeleteFileAsync(bucketName, objName).Result;
                lua.PushInteger(errorCode);
                return 1;
            }
            catch (Exception e)
            {
                return lua.SharpLuaError(e);
            }
        }

        static int MinIO_DeleteFileAsync(IntPtr statePtr)
        {
            var lua = LuaState.FromIntPtr(statePtr);
            try
            {
                var bucketName = lua.ToString(1);
                var objName = lua.ToString(2);
                var task = instance.DeleteFileAsync(bucketName, objName);
                lua.PushObject(task);
                return 1;
            }
            catch (Exception e)
            {
                return lua.SharpLuaError(e);
            }
        }

        //功能：下载文件
        //lua调用示例: sharplua.MinIO_DownloadFile(bucketName, tarPath, filePath)
        //调用结果: 从远端的bucketName/tarPath下载文件到filePath
        static int MinIO_DownloadFile(IntPtr statePtr)
        {
            var lua = LuaState.FromIntPtr(statePtr);
            try
            {
                var bucketName = lua.ToString(1);
                var objName = lua.ToString(2);
                var fileName = lua.ToString(3);
                var result = instance.DownloadFileAsync(bucketName, objName, fileName).Result;
                lua.PushInteger(result);
                return 1;
            }
            catch (Exception e)
            {
                return lua.SharpLuaError(e);
            }
        }

        static int MinIO_DownloadFileAsync(IntPtr statePtr)
        {
            var lua = LuaState.FromIntPtr(statePtr);
            try
            {
                var bucketName = lua.ToString(1);
                var objName = lua.ToString(2);
                var fileName = lua.ToString(3);
                var task = instance.DownloadFileAsync(bucketName, objName, fileName);
                lua.PushObject(task);
                return 1;
            }
            catch (Exception e)
            {
                return lua.SharpLuaError(e);
            }
        }

        static int MinIO_DownloadFileAll(IntPtr statePtr)
        {
            var lua = LuaState.FromIntPtr(statePtr);
            try
            {
                //最大并发任务数
                const int CoCurrentTaskCount = 8;
                //当前任务数
                var taskCount = 0L;
                //任务数信号量
                using var taskWaitEvent = new AutoResetEvent(false);
                //存放任务结果的队列
                var taskResultQueue = new Queue<(int, int)>(CoCurrentTaskCount);

                bool ProcessTaskResult(ref int resultCode)
                {
                    var hasError = false;
                    while (true)
                    {
                        var cbRef = 0;
                        var cbResult = 0;
                        lock (taskResultQueue)
                        {
                            if (taskResultQueue.Count == 0)
                            {
                                break;
                            }
                            var item = taskResultQueue.Dequeue();
                            cbRef = item.Item1;
                            cbResult = item.Item2;
                        }
                        lua.PushInteger(cbRef);
                        lua.GetTable(KeraLua.LuaRegistry.Index);
                        lua.PushInteger(cbResult);
                        if (lua.PCall(1, 0, 0) != KeraLua.LuaStatus.OK)
                        {
                            hasError = true;
                        }
                        lua.Unref(KeraLua.LuaRegistry.Index, cbRef);
                        if (cbResult != 0)
                        {
                            resultCode = cbResult;
                        }
                        //如果有错误则中断执行
                        if (hasError)
                        {
                            break;
                        }
                    }
                    return hasError;
                }

                //是否出现错误
                var hasError = false;
                //任务执行结果
                int resultCode = 0;
                while (true)
                {
                    var top = lua.GetTop();
                    lua.PushCopy(1);
                    //当执行中出现错误时协程会被关闭，需要返回错误信息并停止循环
                    if (lua.PCall(0, 4, 0) != KeraLua.LuaStatus.OK)
                    {
                        hasError = true;
                        break;
                    }
                    if (lua.IsNil(top + 1))
                    {
                        //结束之前需要等待所有任务完成，然后执行结果回调方法
                        while (Interlocked.Read(ref taskCount) > 0)
                        {
                            taskWaitEvent.WaitOne();
                            if (ProcessTaskResult(ref resultCode))
                            {
                                hasError = true;
                                break;
                            }
                        }
                        break;
                    }

                    var bucketName = lua.ToString(top + 1);
                    var objName = lua.ToString(top + 2);
                    var fileName = lua.ToString(top + 3);
                    var cbRef = lua.Ref(KeraLua.LuaRegistry.Index);//pop 4th parameter
                    lua.Pop(3);
                    //开始下载任务，将结果放入队列
                    var task = instance.DownloadFileAsync(bucketName, objName, fileName);
                    task.ContinueWith(task =>
                    {
                        var cbResult = task.Result;
                        //多个任务同时操作结果队列，需要加锁
                        lock (taskResultQueue)
                        {
                            taskResultQueue.Enqueue((cbRef, cbResult));
                        }
                        //任务完成，减少任务数，标记信号量
                        Interlocked.Decrement(ref taskCount);
                        taskWaitEvent.Set();
                    });

                    //任务数超过最大并发数则等待
                    if (Interlocked.Increment(ref taskCount) > CoCurrentTaskCount)
                    {
                        //重置信号量，等待至少一个运行中的任务完成后标记信号量
                        taskWaitEvent.WaitOne();
                    }

                    //处理已完成的任务的结果，如果有错误则中断执行返回错误
                    if (ProcessTaskResult(ref resultCode))
                    {
                        hasError = true;
                        break;
                    }
                }

                if (hasError)
                {
                    return lua.Error();
                }
                else
                {
                    lua.PushInteger(resultCode);
                    return 1;
                }
            }
            catch (Exception e)
            {
                return lua.SharpLuaError(e);
            }
        }

        //功能：返回所有bucket的集合，包含bucketName和bucket创建时间
        //lua调用示例: sharplua.MinIO_ListBuckets()
        static int MinIO_ListBuckets(IntPtr statePtr)
        {
            var lua = LuaState.FromIntPtr(statePtr);
            try
            {
                List<MinioListInfo> objList = instance.ListBucketAsync().Result;
                if (objList != null && objList.Count > 0)
                {
                    lua.NewTable();
                    var index = 1;
                    foreach (var item in objList)
                    {
                        lua.PushNumber(index++);
                        lua.NewTable();
                        //k[1] = bucketName
                        lua.PushNumber(1);
                        lua.PushString(item.FileName);
                        lua.SetTable(-3);
                        //k[2] = timestamp
                        lua.PushNumber(2);
                        lua.PushInteger(item.DateTime.HasValue ? (long)(item.DateTime.Value - StartTimeUTC).TotalSeconds : 0);
                        lua.SetTable(-3);
                        lua.SetTable(-3);
                    }
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

        //功能：返回bucket下前缀匹配的文件列表，包含文件名、上次修改时间、文件大小等
        static int MinIO_ListFiles(IntPtr statePtr)
        {
            var lua = LuaState.FromIntPtr(statePtr);
            try
            {
                var bucketName = lua.ToString(1);
                var prefix = lua.IsString(2) ? lua.ToString(2) : null;
                var recursive = lua.IsBoolean(3) ? lua.ToBoolean(3) : false;
                List<MinioListInfo> objList = instance.ListFileAsync(bucketName, prefix, recursive).Result;
                if (objList != null && objList.Count > 0)
                {
                    lua.NewTable();
                    var index = 1;
                    foreach (var item in objList)
                    {
                        lua.PushNumber(index++);
                        lua.NewTable();
                        //k[1] = fileName
                        lua.PushNumber(1);
                        lua.PushString(item.FileName);
                        lua.SetTable(-3);
                        //k[2] = timestamp
                        lua.PushNumber(2);
                        lua.PushInteger(item.DateTime.HasValue ? (long)(item.DateTime.Value - StartTimeUTC).TotalSeconds : 0);
                        lua.SetTable(-3);
                        //k[3] = size
                        lua.PushNumber(3);
                        lua.PushInteger((long)item.Size);
                        lua.SetTable(-3);
                        lua.SetTable(-3);
                    }
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
    }

    namespace MinioAPI
    {
        using Minio;
        using Minio.DataModel.Args;
        using System;
        using System.Collections.Generic;
        using System.IO;
        using System.Threading.Tasks;

        class MinioListInfo
        {
            public string FileName;
            public ulong Size;
            public DateTime? DateTime;
        }

        class MinioInstance
        {
            private IMinioClient client;

            const int ErrorCodeWithException = 1;

            public MinioInstance(string endPoint, string accessKey, string secretKey)
            {
                client = new MinioClient()
                    .WithEndpoint(endPoint)
                    .WithCredentials(accessKey, secretKey)
                    .WithSSL(false)
                    .Build();
            }

            public void Dispose()
            {
                client?.Dispose();
                client = null;
            }

            public async Task<int> UploadFileAsync(string bucketName, string srcPath, string tarPath)
            {
                var errorCode = 0;
                try
                {
                    // Make a bucket on the server, if not already present.
                    var beArgs = new BucketExistsArgs()
                        .WithBucket(bucketName);
                    bool found = await client.BucketExistsAsync(beArgs).ConfigureAwait(false);
                    if (!found)
                    {
                        var mbArgs = new MakeBucketArgs()
                            .WithBucket(bucketName);
                        await client.MakeBucketAsync(mbArgs).ConfigureAwait(false);
                    }

                    //使用WithFileName制定文件时，源码中只打开了文件，没有及时关闭，导致文件被占用，如果后续代码，尝试删除就会报错，所以这里使用WithStreamData自行关闭
                    using var streamData = File.OpenRead(srcPath);
                    // Upload a file to bucket.
                    var putObjectArgs = new PutObjectArgs()
                        .WithBucket(bucketName)
                        .WithObject(tarPath)
                        .WithStreamData(streamData)
                        .WithObjectSize(streamData.Length);

                    //TODO 当设置目标为NET8.0并开启PublishTrim后，这个方法在处理大文件分段上传的时候会出现问题，暂时不知道原因
                    await client.PutObjectAsync(putObjectArgs).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                    errorCode = ErrorCodeWithException;
                }
                return errorCode;
            }

            public async Task<int> DeleteFileAsync(string bucketName, string objectName)
            {
                var errorCode = 0;
                try
                {
                    var args = new RemoveObjectArgs()
                        .WithBucket(bucketName)
                        .WithObject(objectName);
                    await client.RemoveObjectAsync(args).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                    errorCode = ErrorCodeWithException;
                }
                return errorCode;
            }

            public async Task<int> DownloadFileAsync(string bucketName, string objectName, string fileName)
            {
                var errorCode = 0;
                try
                {
                    var args = new GetObjectArgs()
                        .WithBucket(bucketName)
                        .WithObject(objectName)
                        .WithFile(fileName);
                    await client.GetObjectAsync(args).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                    errorCode = ErrorCodeWithException;
                }
                return errorCode;
            }

            public async Task<List<MinioListInfo>> ListBucketAsync()
            {
                List<MinioListInfo> objList = null;
                try
                {
                    var list = await client.ListBucketsAsync().ConfigureAwait(false);
                    objList = new List<MinioListInfo>();
                    foreach (var bucket in list.Buckets)
                    {
                        objList.Add(new MinioListInfo { FileName = bucket.Name, DateTime = bucket.CreationDateDateTime });
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                    //异常时返回null和空数组的情况做个区分
                    objList = null;
                }
                return objList;
            }

            public async Task<List<MinioListInfo>> ListFileAsync(string bucketName, string prefix, bool recursive)
            {
                List<MinioListInfo> results;
                try
                {
                    var listArgs = new ListObjectsArgs()
                        .WithBucket(bucketName)
                        .WithPrefix(prefix)
                        .WithRecursive(recursive);
                    results = new List<MinioListInfo>();
                    await foreach (var item in client.ListObjectsEnumAsync(listArgs))
                    {
                        var info = new MinioListInfo
                        {
                            FileName = item.Key,
                            Size = item.Size,
                            DateTime = item.LastModifiedDateTime,
                        };
                        results.Add(info);
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                    //异常时返回null和空数组的情况做个区分
                    results = null;
                }
                return results;
            }
        }
    }
}