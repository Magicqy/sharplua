-- test-utils.lua
-- Lua 测试工具库，提供友好的断言方法
local test = {}

-- 基础断言
function test.assert_equal(expected, actual, message)
    local msg = message or string.format("Expected %s, but got %s", tostring(expected), tostring(actual))
    assert(expected == actual, msg)
end

function test.assert_not_equal(expected, actual, message)
    local msg = message or string.format("Expected not %s, but got %s", tostring(expected), tostring(actual))
    assert(expected ~= actual, msg)
end

function test.assert_not_nil(value, message)
    local msg = message or "Expected value to not be nil"
    assert(value ~= nil, msg)
end

function test.assert_nil(value, message)
    local msg = message or string.format("Expected nil, but got %s", tostring(value))
    assert(value == nil, msg)
end

function test.assert_true(condition, message)
    local msg = message or "Expected condition to be true"
    assert(condition == true, msg)
end

function test.assert_false(condition, message)
    local msg = message or "Expected condition to be false"
    assert(condition == false, msg)
end

-- 类型断言
function test.assert_type(expected_type, value, message)
    local actual_type = type(value)
    local msg = message or string.format("Expected type %s, but got %s", expected_type, actual_type)
    assert(actual_type == expected_type, msg)
end

-- 表断言
function test.assert_table_size(expected_size, table_value, message)
    local actual_size = #table_value
    local msg = message or string.format("Expected table size %d, but got %d", expected_size, actual_size)
    assert(actual_size == expected_size, msg)
end

function test.assert_table_contains_key(table_value, key, message)
    local msg = message or string.format("Expected table to contain key %s", tostring(key))
    assert(table_value[key] ~= nil, msg)
end

-- 数值断言
function test.assert_greater_than(expected, actual, message)
    local msg = message or string.format("Expected %s to be greater than %s", tostring(actual), tostring(expected))
    assert(actual > expected, msg)
end

function test.assert_less_than(expected, actual, message)
    local msg = message or string.format("Expected %s to be less than %s", tostring(actual), tostring(expected))
    assert(actual < expected, msg)
end

-- 异常断言
function test.assert_error(func, message)
    local success, err = pcall(func)
    local msg = message or "Expected function to throw an error"
    assert(not success, msg)
end

function test.assert_no_error(func, message)
    local success, err = pcall(func)
    local msg = message or string.format("Expected no error, but got: %s", tostring(err))
    assert(success, msg)
end

-- 字符串断言
function test.assert_string_contains(haystack, needle, message)
    local msg = message or string.format("Expected string '%s' to contain '%s'", tostring(haystack), tostring(needle))
    assert(type(haystack) == "string", "haystack must be a string")
    assert(string.find(haystack, needle, 1, true) ~= nil, msg)
end

return test
