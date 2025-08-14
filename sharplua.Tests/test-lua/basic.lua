-- 基础功能测试脚本，使用断言验证结果

-- 直接加载同目录下的 test-utils.lua
local test = require("test-utils")

print("Basic test script running")

-- 测试 SharpLua 库基础信息
print("Testing SharpLua library...")
test.assert_not_nil(sharplua, "SharpLua library should be available")
test.assert_not_nil(sharplua.Version, "SharpLua Version should exist")
test.assert_type("string", sharplua.Version, "Version should be string type")
test.assert_string_contains(sharplua.Version, "0.", "Version should contain '0.'")
print("SharpLua library version:", sharplua.Version)

-- 测试基本计算
print("Testing basic calculations...")
local result = 1 + 2 + 3
test.assert_equal(6, result, "Simple addition should equal 6")
test.assert_type("number", result, "Calculation result should be number")
print("Simple calculation result:", result)

-- 测试表操作
print("Testing table operations...")
local testTable = {a = 1, b = 2, c = 3}
test.assert_type("table", testTable, "testTable should be table type")
test.assert_table_contains_key(testTable, "a", "testTable should contain key 'a'")
test.assert_table_contains_key(testTable, "b", "testTable should contain key 'b'")
test.assert_table_contains_key(testTable, "c", "testTable should contain key 'c'")
test.assert_equal(1, testTable.a, "testTable.a should equal 1")
test.assert_equal(2, testTable.b, "testTable.b should equal 2")
test.assert_equal(3, testTable.c, "testTable.c should equal 3")

for k, v in pairs(testTable) do
    print("Table entry:", k, v)
    test.assert_type("number", v, string.format("Table value %s should be number", k))
end

-- 测试数值比较
print("Testing number comparisons...")
test.assert_greater_than(5, 6, "6 should be greater than 5")
test.assert_less_than(10, 8, "8 should be less than 10")

-- 测试类型验证
print("Testing type validations...")
test.assert_type("string", "hello", "String literal should have string type")
test.assert_type("number", 42, "Number literal should have number type")
test.assert_type("boolean", true, "Boolean literal should have boolean type")

print("Basic test completed successfully - All assertions passed!")
