do
    local i = 0
    local function taskIterFunc()
        if i == 10 then
            return nil
        end
        i = i + 1
        local delay = math.random(0, 1000)
        if i == 10 then
            delay = 2000
        end
        local task = sharplua.TaskDelay(delay)
        local taskIdx = i
        print("iter task", taskIdx, delay, os.clock(), 'start')
        return task, "void", function()
            print("iter task", taskIdx, delay, os.clock(), 'finish')
        end
    end
    sharplua.TaskRun(taskIterFunc, 4)
end

do
    local taskCoFunc = coroutine.wrap(function()
        for i = 1, 10 do
            local delay = math.random(0, 1000)
            if i == 10 then
                delay = 2000
            end
            local task = sharplua.TaskDelay(delay)
            print("co task", i, delay, os.clock(), 'start')
            coroutine.yield(task, "void", function()
                print("co task", i, delay, os.clock(), 'finish')
            end)
        end
    end)
    sharplua.TaskRun(taskCoFunc, 4)
end