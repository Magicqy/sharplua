local JSON = require("modules.json")

local StateTag = "MainState"
local state = sharplua.LuaNewState()
print(StateTag, "new state", state)
print(StateTag, "new state do file", sharplua.LuaDoFile(state, "test-lua/lua-state-sync.lua"))

local function syncCbFunc(...)
    print(StateTag, "on recv", ...)
end

local syncArgs = JSON.encode({
    a = 1,
    b = true,
    c = 3.5,
    d = {a=1, b=2, c=3},
    e = "hello",
    f = "world"
})
print(StateTag, "sync state", sharplua.LuaSyncState(state, "OnSync", syncCbFunc, syncArgs), syncArgs)
sharplua.LuaCloseState(state)