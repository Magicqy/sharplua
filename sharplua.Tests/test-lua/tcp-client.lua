local client = {
    client_id = 0,
}

function client:CreateID()
    self.client_id = self.client_id + 1
    return self.client_id    
end

function client:OnConn(ipPort)
    print("client connected:", ipPort)
    client.isConn = true
end

function client:OnDis(ipPort)
    print("client disconnected:", ipPort)
    client.isConn = false
end

function client:OnRecv(recvData)
    print("client recv:", #recvData, recvData)
end

local lastTick = 0

while true do
    sharplua.TaskWait(sharplua.TaskDelay(1000))
    
    if client.conn then
        sharplua.TcpProcessEvent(client.conn)
    else
        repeat
            local ipPort = "127.0.0.1:8899"
            local conn = sharplua.TcpClientStart({
                ipPort = ipPort,
                bufferSize = 256,
                cbConn = client.OnConn,
                cbDis = client.OnDis,
                cbRecv = client.OnRecv,
                cbSelf = client,
            })
            if conn then
                client.conn = conn
                client.id = client:CreateID()
                print("client start success:", ipPort, client.id)
            else
                sharplua.TaskWait(sharplua.TaskDelay(1000))
                print("client start failed, delay and retry", ipPort)
            end
        until client.conn
    end

    if os.clock() - lastTick > 10 then
        lastTick = os.clock()
        print("client dispose:", client.id)
        sharplua.TcpClientDispose(client.conn)
        client.conn = nil
        client.isConn = nil
    end

    if client.isConn then
        local sendData = string.format("id: %s msg: %s", client.id, string.rep("i", math.random(1, 10), ""))
        print("client send:", #sendData, sendData)
        sharplua.TcpClientSend(client.conn, sharplua.TcpSendTypeBytes, sendData)
    end
end