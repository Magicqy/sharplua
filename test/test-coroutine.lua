function test(...)
    print("coroutine function args", ...)

    for i = 1, 3 do
        print("coroutine.yield", i)
        local r = coroutine.yield(i)
        print("coroutine.yield return", i)
    end
end

local co = coroutine.create(test)

for r = 1, 3 do
    print("coroutine.resume", r)
    local suc, i = coroutine.resume(co, r)
    print("coroutine.resume return", i)
    if i == nil then
        break
    end
end

print("test coroutine done")