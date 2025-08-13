-- system.lua
-- 测试 SharpAPI_System 功能

-- 加载测试工具
local test = require("test-utils")

print("=== Testing SharpAPI_System ===")

-- 验证 sharplua 库是否可用
test.assert_not_nil(sharplua, "SharpLua library should be available")

-- 测试 GetOSType 功能
print("Testing GetOSType...")
test.assert_not_nil(sharplua.GetOSType, "GetOSType function should exist")
local osType = sharplua.GetOSType()
test.assert_not_nil(osType, "GetOSType should return a value")
test.assert_type("string", osType, "OS type should be a string")

-- 验证返回的操作系统类型是有效值
local validOSTypes = {
    ["Windows"] = true,
    ["MacOS"] = true,
    ["Linux"] = true,
    ["Android"] = true,
    ["iOS"] = true
}
test.assert_true(validOSTypes[osType] ~= nil, "OS type should be one of: Windows, MacOS, Linux, Android, iOS")
print("Current OS type:", osType)

-- 测试 GetCommandLineArgs 功能
print("Testing GetCommandLineArgs...")
test.assert_not_nil(sharplua.GetCommandLineArgs, "GetCommandLineArgs function should exist")
local args = sharplua.GetCommandLineArgs()

-- 如果有命令行参数，应该返回一个表
if args ~= nil then
    test.assert_type("table", args, "Command line args should be a table when present")
    print("Command line args count:", #args)
    
    -- 验证表中的元素都是字符串
    for i, arg in ipairs(args) do
        test.assert_type("string", arg, string.format("Command line arg %d should be string", i))
        print("  Arg " .. i .. ":", arg)
    end
else
    print("No command line arguments")
end

-- 测试 GetEnvironmentVariable 功能
print("Testing GetEnvironmentVariable...")
test.assert_not_nil(sharplua.GetEnvironmentVariable, "GetEnvironmentVariable function should exist")

-- 测试获取 PATH 环境变量（所有操作系统都应该有）
local pathVar = sharplua.GetEnvironmentVariable("PATH")
test.assert_not_nil(pathVar, "PATH environment variable should exist")
test.assert_type("string", pathVar, "PATH should be a string")
test.assert_true(string.len(pathVar) > 0, "PATH should not be empty")
print("PATH variable length:", string.len(pathVar))

-- 测试不存在的环境变量
local nonExistentVar = sharplua.GetEnvironmentVariable("THIS_VAR_SHOULD_NOT_EXIST_123456")
test.assert_nil(nonExistentVar, "Non-existent environment variable should return nil")

-- 测试 GetEnvironmentVariables 功能
print("Testing GetEnvironmentVariables...")
test.assert_not_nil(sharplua.GetEnvironmentVariables, "GetEnvironmentVariables function should exist")
local envVars = sharplua.GetEnvironmentVariables()
test.assert_not_nil(envVars, "Environment variables should not be nil")
test.assert_type("table", envVars, "Environment variables should be a table")

-- 验证环境变量表包含一些基本变量
-- 注意：在 Windows 上环境变量名可能是大小写敏感的
local hasPath = envVars.PATH ~= nil or envVars.Path ~= nil or envVars.path ~= nil
test.assert_true(hasPath, "Environment variables should contain PATH (case insensitive)")

-- 获取 PATH 变量（不区分大小写）
local pathEnvValue = envVars.PATH or envVars.Path or envVars.path
test.assert_not_nil(pathEnvValue, "PATH environment variable should exist")
test.assert_type("string", pathEnvValue, "PATH environment variable should be string")

-- 计算环境变量数量
local envCount = 0
for k, v in pairs(envVars) do
    envCount = envCount + 1
    test.assert_type("string", k, "Environment variable key should be string")
    test.assert_type("string", v, "Environment variable value should be string")
end
test.assert_true(envCount > 0, "Should have at least one environment variable")
print("Total environment variables:", envCount)

-- 验证 Exit 函数存在（但不调用它，因为会终止程序）
print("Testing Exit function existence...")
test.assert_not_nil(sharplua.Exit, "Exit function should exist")
test.assert_type("function", sharplua.Exit, "Exit should be a function")

-- 验证 ReadKey 函数存在（但不调用，因为是交互式的）
print("Testing ReadKey function existence...")
test.assert_not_nil(sharplua.ReadKey, "ReadKey function should exist")
test.assert_type("function", sharplua.ReadKey, "ReadKey should be a function")

-- 验证 ReadLine 函数存在（但不调用，因为是交互式的）
print("Testing ReadLine function existence...")
test.assert_not_nil(sharplua.ReadLine, "ReadLine function should exist")
test.assert_type("function", sharplua.ReadLine, "ReadLine should be a function")

print("=== SharpAPI_System tests completed successfully ===")
