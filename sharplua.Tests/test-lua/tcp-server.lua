local server = {}

function server:OnConn(ipPort)
    print("server connected:", ipPort)
end

function server:OnDis(ipPort)
    print("server disconnected:", ipPort)
end

function server:OnRecv(ipPort, recvData)
    print("server recv:", ipPort, #recvData, recvData)

    local sendData = recvData
    sharplua.TcpServerSend(server.conn, ipPort, sharplua.TcpSendTypeBytes, sendData)
    print("server send:", ipPort, #sendData, sendData)
end

repeat
    local ipPort = "127.0.0.1:8899"
    local conn = sharplua.TcpServerStart({
        ipPort = ipPort,
        bufferSize = 256,
        cbConn = server.OnConn,
        cbDis = server.OnDis,
        cbRecv = server.OnRecv,
        cbSelf = server,
    })
    if conn then
        server.conn = conn
        print("server start success:", ipPort)
    else
        sharplua.TaskWait(sharplua.TaskDelay(1000))
        print("server start failed, delay and retry", ipPort)
    end
until server.conn

while true do
    sharplua.TaskWait(sharplua.TaskDelay(1000))
    if server.conn then
        sharplua.TcpProcessEvent(server.conn)
    end
end