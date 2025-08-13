namespace SharpLua
{
    using LuaState = KeraLua.Lua;
    using System.Net.Http;
    using System.IO;
    using System.Threading.Tasks;
    using SuperSimpleTcp;
    using System.Text;
    using System;
    using System.Collections.Generic;
    using CommunityToolkit.HighPerformance;

    static class SharpAPI_Network
    {
        public static void Register(LuaState lua)
        {
            lua.SharpLuaRegistFunction(nameof(HttpRequest), HttpRequest);
            lua.SharpLuaRegistFunction(nameof(HttpDownload), HttpDownload);

            lua.SharpLuaRegistFunction(nameof(TcpClientStart), TcpClientStart);
            lua.SharpLuaRegistFunction(nameof(TcpClientDispose), TcpClientDispose);
            lua.SharpLuaRegistFunction(nameof(TcpClientSend), TcpClientSend);
            lua.SharpLuaRegistFunction(nameof(TcpServerStart), TcpServerStart);
            lua.SharpLuaRegistFunction(nameof(TcpServerDispose), TcpServerDispose);
            lua.SharpLuaRegistFunction(nameof(TcpServerSend), TcpServerSend);
            lua.SharpLuaRegistFunction(nameof(TcpProcessEvent), TcpProcessEvent);

            lua.SharpLuaRegistValue(nameof(TcpSendTypeBytes), TcpSendTypeBytes);
            lua.SharpLuaRegistValue(nameof(TcpSendTypeUTF8String), TcpSendTypeUTF8String);
        }

        static SharpLuaFunction HttpRequest = (LuaState lua) =>
        {
            using (var client = new HttpClient())
            {
                var uri = lua.ToString(1);
                var method = lua.ToString(2);
                var request = new HttpRequestMessage(new HttpMethod(method), uri);

                var bodyAsText = true;
                var resultAsText = true;
                if (lua.GetTop() > 4)
                {
                    var mode = lua.ToInteger(5);
                    switch (mode)
                    {
                        case 1:
                            bodyAsText = false;
                            break;
                        case 2:
                            resultAsText = false;
                            break;
                        case 3:
                            bodyAsText = false;
                            resultAsText = false;
                            break;
                    }
                }
                request.Content = bodyAsText ? new StringContent(lua.ToString(3)) : new ByteArrayContent(lua.ToBuffer(3));

                if (lua.GetTop() > 3)
                {
                    lua.PushNil();
                    while (lua.Next(4))
                    {
                        var name = lua.ToString(-2);
                        var value = lua.ToString(-1);
                        lua.Pop(1);
                        request.Headers.Add(name, value);
                    }
                }

                var response = client.Send(request);
                if (resultAsText)
                {
                    var result = response.Content.ReadAsStringAsync().Result;
                    lua.PushString(result);
                }
                else
                {
                    var result = response.Content.ReadAsByteArrayAsync().Result;
                    lua.PushBuffer(result);
                }
                return 1;
            }
        };

        static SharpLuaFunction HttpDownload = (LuaState lua) =>
        {
            var downloadUrl = lua.ToString(1);
            var savePath = lua.ToString(2);

            var task = StartDownloadTask(downloadUrl, savePath);
            lua.PushObject(task);
            return 1;
        };

        async static Task StartDownloadTask(string downloadUrl, string savePath)
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                {
                    using (var fileStream = File.OpenWrite(savePath))
                    {
                        await contentStream.CopyToAsync(fileStream);
                    }
                }
            }
        }

        const int SizeofPacketSize = sizeof(int);
        const int SizeofDataType = sizeof(byte);
        const byte TcpSendTypeBytes = 0;
        const byte TcpSendTypeUTF8String = 1;

        class TcpRecvBuffer
        {
            MemoryStream recvBuffer = new MemoryStream();
            int recvBufferCount = 0;
            Queue<MsgItem> recvMsg { get; } = new Queue<MsgItem>();
            class MsgItem
            {
                public string type;
                public byte[] dataBytes;
                public string dataStr;
                public MsgItem(string type, byte[] dataBytes, string dataStr)
                {
                    this.type = type;
                    this.dataBytes = dataBytes;
                    this.dataStr = dataStr;
                }
            }

            public void PushMsg(string type, string data)
            {
                lock (recvMsg)
                {
                    recvMsg.Enqueue(new MsgItem(type, null, data));
                }
            }

            public void PushMsg(string type, byte[] data)
            {
                lock (recvMsg)
                {
                    recvMsg.Enqueue(new MsgItem(type, data, null));
                }
            }

            public bool PopMsg(out string type, out byte[] dataBytes, out string dataStr)
            {
                lock (recvMsg)
                {
                    if (recvMsg.Count > 0)
                    {
                        var item = recvMsg.Dequeue();
                        type = item.type;
                        dataBytes = item.dataBytes;
                        dataStr = item.dataStr;
                        return true;
                    }
                    type = null;
                    dataBytes = null;
                    dataStr = null;
                    return false;
                }
            }

            public void OnRecvData(string ipPort, ArraySegment<byte> data)
            {
                lock (recvBuffer)
                {
                    WriteBuffer(data);
                    ProcessBuffer();
                }
            }

            public void ClearBuffer()
            {
                lock (recvBuffer)
                {
                    recvBuffer.Seek(0, SeekOrigin.Begin);
                    recvBuffer.SetLength(0);
                    recvBufferCount = 0;
                }
            }

            void WriteBuffer(ArraySegment<byte> data)
            {
                recvBuffer.Seek(0, SeekOrigin.End);
                recvBuffer.Write(data.Array, data.Offset, data.Count);
                recvBufferCount += data.Count;
            }

            void ProcessBuffer()
            {
                while (true)
                {
                    if (recvBufferCount < SizeofPacketSize)
                    {
                        break;
                    }

                    recvBuffer.Seek(0, SeekOrigin.Begin);
                    var packetSize = recvBuffer.Read<int>();
                    if (recvBufferCount < packetSize)
                    {
                        recvBuffer.Seek(-SizeofPacketSize, SeekOrigin.Current);
                        break;
                    }

                    var dataType = recvBuffer.Read<byte>();
                    var dataSize = packetSize - SizeofPacketSize - SizeofDataType;
                    switch (dataType)
                    {
                        case TcpSendTypeBytes:
                        {
                            var data = new byte[dataSize];
                            recvBuffer.Read(data, 0, dataSize);
                            PushMsg("Received", data);
                            break;
                        }
                        case TcpSendTypeUTF8String:
                        {
                            var data = Encoding.UTF8.GetString(recvBuffer.GetBuffer(), (int)recvBuffer.Position, dataSize);
                            PushMsg("Received", data);
                            recvBuffer.Seek(dataSize, SeekOrigin.Current);
                            break;
                        }
                        default:
                        {
                            Console.WriteLine("unknown packet type {0}", dataType);
                            recvBuffer.Seek(dataSize, SeekOrigin.Current);
                            break;
                        }
                    }
                    recvBufferCount -= packetSize;

                    if (recvBufferCount > 0)
                    {
                        var bytes = recvBuffer.GetBuffer();
                        Buffer.BlockCopy(bytes, (int)recvBuffer.Position, bytes, 0, recvBufferCount);
                    }
                    recvBuffer.Seek(0, SeekOrigin.Begin);
                    recvBuffer.SetLength(recvBufferCount);
                }
            }
        }

        class TcpConnectEndpoint
        {
            public static TcpConnectEndpoint ClientStart(LuaState lua)
            {
                lua.GetField(1, "ipPort");
                var ipPort = lua.ToString(-1);
                var client = new SimpleTcpClient(ipPort);

                lua.GetField(1, "cbSelf");
                var refSelf = lua.Ref(KeraLua.LuaRegistry.Index);
                lua.GetField(1, "cbRecv");
                var refRecv = lua.Ref(KeraLua.LuaRegistry.Index);
                lua.GetField(1, "cbDis");
                var refDis = lua.Ref(KeraLua.LuaRegistry.Index);
                lua.GetField(1, "cbConn");
                var refConn = lua.Ref(KeraLua.LuaRegistry.Index);

                //可选参数
                if (lua.GetField(1, "bufferSize") == KeraLua.LuaType.Number)
                {
                    var bufferSize = (int)lua.ToInteger(-1);
                    client.Settings.StreamBufferSize = bufferSize;
                }
                if (lua.GetField(1, "noDelay") == KeraLua.LuaType.Boolean)
                {
                    var noDelay = lua.ToBoolean(-1);
                    client.Settings.NoDelay = noDelay;
                }

                var conn = new TcpConnectEndpoint()
                {
                    tcpClient = client,
                    refConn = refConn,
                    refDis = refDis,
                    refRecv = refRecv,
                    refSelf = refSelf,
                };

                client.Events.Connected += (sender, args) => conn.OnConn(args.IpPort);
                client.Events.Disconnected += (sender, args) => conn.OnDis(args.IpPort);
                client.Events.DataReceived += (sender, args) => conn.OnRecv(args.IpPort, args.Data);

                try
                {
                    client.Connect();
                }
                catch
                {
                    conn.Dispose(lua);
                    conn = null;
                }
                return conn;
            }

            public static TcpConnectEndpoint ServerStart(LuaState lua)
            {
                lua.GetField(1, "ipPort");
                var ipPort = lua.ToString(-1);
                var server = new SimpleTcpServer(ipPort);

                lua.GetField(1, "cbSelf");
                var refSelf = lua.Ref(KeraLua.LuaRegistry.Index);
                lua.GetField(1, "cbRecv");
                var refRecv = lua.Ref(KeraLua.LuaRegistry.Index);
                lua.GetField(1, "cbDis");
                var refDis = lua.Ref(KeraLua.LuaRegistry.Index);
                lua.GetField(1, "cbConn");
                var refConn = lua.Ref(KeraLua.LuaRegistry.Index);

                //可选参数
                if (lua.GetField(1, "bufferSize") == KeraLua.LuaType.Number)
                {
                    var bufferSize = (int)lua.ToInteger(-1);
                    server.Settings.StreamBufferSize = bufferSize;
                }
                if (lua.GetField(1, "noDelay") == KeraLua.LuaType.Boolean)
                {
                    var noDelay = lua.ToBoolean(-1);
                    server.Settings.NoDelay = noDelay;
                }

                var conn = new TcpConnectEndpoint()
                {
                    tcpServer = server,
                    refConn = refConn,
                    refDis = refDis,
                    refRecv = refRecv,
                    refSelf = refSelf,
                };

                server.Events.ClientConnected += (sender, args) => conn.OnConn(args.IpPort);
                server.Events.ClientDisconnected += (sender, args) => conn.OnDis(args.IpPort);
                server.Events.DataReceived += (sender, args) => conn.OnRecv(args.IpPort, args.Data);

                try
                {
                    server.Start();
                }
                catch
                {
                    conn.Dispose(lua);
                    conn = null;
                }
                return conn;
            }

            public void Dispose(LuaState lua)
            {
                tcpClient?.Dispose();
                tcpServer?.Dispose();
                lua.Unref(KeraLua.LuaRegistry.Index, refConn);
                lua.Unref(KeraLua.LuaRegistry.Index, refDis);
                lua.Unref(KeraLua.LuaRegistry.Index, refRecv);
                lua.Unref(KeraLua.LuaRegistry.Index, refSelf);
            }

            public void ClientSend(byte dataType, byte[] data)
            {
                if (!tcpClient.IsConnected)
                {
                    return;
                }

                var dataSize = data.Length;
                var packetSize = SizeofPacketSize + SizeofDataType + dataSize;

                using var buffer = new MemoryStream(packetSize);
                buffer.SetLength(packetSize);
                buffer.Write(packetSize);
                buffer.Write(dataType);
                buffer.Write(data);
                buffer.Seek(0, SeekOrigin.Begin);

                tcpClient.Send(buffer.Length, buffer);
            }

            public void ServerSend(string ipPort, byte dataType, byte[] data)
            {
                if (!tcpServer.IsConnected(ipPort))
                {
                    return;
                }

                var dataSize = data.Length;
                var packetSize = SizeofPacketSize + SizeofDataType + dataSize;

                using var buffer = new MemoryStream(packetSize);
                buffer.SetLength(packetSize);
                buffer.Write(packetSize);
                buffer.Write(dataType);
                buffer.Write(data);
                buffer.Seek(0, SeekOrigin.Begin);

                tcpServer.Send(ipPort, packetSize, buffer);
            }

            private void OnConn(string ipPort)
            {
                lock (recvBufferEntry)
                {
                    if (recvBufferEntry.TryGetValue(ipPort, out var buffer))
                    {
                        Console.WriteLine("duplicate connection {0}", ipPort);
                    }
                    else
                    {
                        var recvBuffer = new TcpRecvBuffer();
                        recvBuffer.PushMsg("Connected", ipPort);
                        recvBufferEntry.Add(ipPort, recvBuffer);
                    }
                }
            }

            private void OnDis(string ipPort)
            {
                lock (recvBufferEntry)
                {
                    if (recvBufferEntry.TryGetValue(ipPort, out var buffer))
                    {
                        buffer.PushMsg("Disconnected", ipPort);
                        buffer.ClearBuffer();
                    }
                    else
                    {
                        Console.WriteLine("unknown disconnection {0}", ipPort);
                    }
                }
            }

            private void OnRecv(string ipPort, ArraySegment<byte> data)
            {
                lock (recvBufferEntry)
                {
                    if (recvBufferEntry.TryGetValue(ipPort, out var buffer))
                    {
                        buffer.OnRecvData(ipPort, data);
                    }
                    else
                    {
                        Console.WriteLine("unknown data received {0}, count {1}", ipPort, data.Count);
                    }
                }
            }
            
            public void ProcessEvent(LuaState lua)
            {
                lock (recvBufferEntry)
                {
                    foreach (var item in recvBufferEntry)
                    {
                        var ipPort = item.Key;
                        var buffer = item.Value;
                        while (buffer.PopMsg(out string type, out byte[] dataBytes, out string dataStr))
                        {
                            switch (type)
                            {
                                case "Connected":
                                    lua.PushInteger(refConn);
                                    lua.GetTable(KeraLua.LuaRegistry.Index);
                                    break;
                                case "Disconnected":
                                    lua.PushInteger(refDis);
                                    lua.GetTable(KeraLua.LuaRegistry.Index);
                                    break;
                                case "Received":
                                    lua.PushInteger(refRecv);
                                    lua.GetTable(KeraLua.LuaRegistry.Index);
                                    break;
                            }
                            var top = lua.GetTop();
                            lua.PushInteger(refSelf);
                            lua.GetTable(KeraLua.LuaRegistry.Index);
                            
                            if (tcpServer != null)
                            {
                                lua.PushString(ipPort);
                            }
                            if (dataBytes != null)
                            {
                                lua.PushBuffer(dataBytes);
                            }
                            else if (dataStr != null)
                            {
                                lua.PushString(dataStr);
                            }
                            else
                            {
                                lua.PushNil();
                            }

                            var nArgs = lua.GetTop() - top;
                            if (lua.PCall(nArgs, 0, 0) != KeraLua.LuaStatus.OK)
                            {
                                Console.WriteLine(lua.ToString(-1));
                            }
                        }
                    }
                }
            }

            private SimpleTcpClient tcpClient { get; set; }
            private SimpleTcpServer tcpServer { get; set; }
            private int refConn { get; set; }
            private int refDis { get; set; }
            private int refRecv { get; set; }
            private int refSelf { get; set; }
            private Dictionary<string, TcpRecvBuffer> recvBufferEntry = new Dictionary<string, TcpRecvBuffer>();
        }

        static SharpLuaFunction TcpClientStart = (LuaState lua) =>
        {
            var conn = TcpConnectEndpoint.ClientStart(lua);
            lua.PushObject(conn);
            return 1;
        };

        static SharpLuaFunction TcpClientDispose = (LuaState lua) =>
        {
            var conn = lua.ToObject<TcpConnectEndpoint>(1, true);
            conn.Dispose(lua);
            return 0;
        };

        static SharpLuaFunction TcpClientSend = (LuaState lua) =>
        {
            var conn = lua.ToObject<TcpConnectEndpoint>(1, false);
            var type = (byte)lua.ToInteger(2);
            var data = lua.ToBuffer(3);
            conn.ClientSend(type, data);
            return 0;
        };

        static SharpLuaFunction TcpServerStart = (LuaState lua) =>
        {
            var conn = TcpConnectEndpoint.ServerStart(lua);
            lua.PushObject(conn);
            return 1;
        };

        static SharpLuaFunction TcpServerDispose = (LuaState lua) =>
        {
            var conn = lua.ToObject<TcpConnectEndpoint>(1, true);
            conn.Dispose(lua);
            return 0;
        };

        static SharpLuaFunction TcpServerSend = (LuaState lua) =>
        {
            var conn = lua.ToObject<TcpConnectEndpoint>(1, false);
            var ipPort = lua.ToString(2);
            var type = (byte)lua.ToInteger(3);
            var data = lua.ToBuffer(4);
            conn.ServerSend(ipPort, type, data);
            return 0;
        };

        static SharpLuaFunction TcpProcessEvent = (LuaState lua) =>
        {
            var conn = lua.ToObject<TcpConnectEndpoint>(1, false);
            conn.ProcessEvent(lua);
            return 0;
        };
    }
}