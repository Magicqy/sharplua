local JSON = require("modules.json")
local StateTag = "ChildState"

function OnSync(cbFunc, syncArgs)
    local args = JSON.decode(syncArgs)
    print(StateTag, "sync func args", cbFunc, syncArgs)

    for k,v in pairs(args) do
        print(StateTag, "on sync", k, v)
        cbFunc(k, v)
    end
    return cbFunc, syncArgs
end