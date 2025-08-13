namespace sharplua.Tests;

/// <summary>
/// 基于Lua脚本的集成测试
/// </summary>
public class LuaScriptTests
{
    private readonly string _testLuaPath;

    public LuaScriptTests()
    {
        _testLuaPath = GetTestLuaPath();
        if (!Directory.Exists(_testLuaPath))
        {
            throw new DirectoryNotFoundException($"Test Lua directory not found: {_testLuaPath}");
        }
    }

    private string GetTestLuaPath()
    {
        // 方案1：优先使用环境变量
        var envTestLuaPath = Environment.GetEnvironmentVariable("TEST_LUA_PATH");
        if (!string.IsNullOrEmpty(envTestLuaPath))
        {
            return envTestLuaPath;
        }

        // 方案2：回退到程序集路径方法
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var assemblyLocation = assembly.Location;
        var projectDir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(assemblyLocation))))!;
        return Path.Combine(projectDir, "test-lua");
    }

    [Fact]
    public void Test_BasicScript_ShouldExecuteSuccessfully()
    {
        // Arrange
        var scriptPath = Path.Combine(_testLuaPath, "basic.lua");

        // Act & Assert
        var result = ExecuteLuaScript(scriptPath);
        if (!result.Success && result.IsAssertionFailure)
        {
            Assert.True(result.Success, $"Assertion failed in Lua script: {result.AssertionDetails}");
        }
        else
        {
            Assert.True(result.Success, $"Script execution failed: {result.ErrorMessage}");
        }
    }

    [Fact]
    public void Test_CoroutineScript_ShouldExecuteSuccessfully()
    {
        // Arrange
        var scriptPath = Path.Combine(_testLuaPath, "coroutine.lua");

        // Act & Assert
        var result = ExecuteLuaScript(scriptPath);
        Assert.True(result.Success, $"Script execution failed: {result.ErrorMessage}");
    }

    [Fact]
    public void Test_TaskScript_ShouldExecuteSuccessfully()
    {
        // Arrange
        var scriptPath = Path.Combine(_testLuaPath, "task.lua");

        // Act & Assert
        var result = ExecuteLuaScript(scriptPath);
        Assert.True(result.Success, $"Script execution failed: {result.ErrorMessage}");
    }

    [Fact]
    public void Test_LuaStateScript_ShouldExecuteSuccessfully()
    {
        // Arrange
        var scriptPath = Path.Combine(_testLuaPath, "lua-state.lua");

        // Act & Assert
        var result = ExecuteLuaScript(scriptPath);
        Assert.True(result.Success, $"Script execution failed: {result.ErrorMessage}");
    }

    [Fact]
    public void Test_LuaStateSyncScript_ShouldExecuteSuccessfully()
    {
        // Arrange
        var scriptPath = Path.Combine(_testLuaPath, "lua-state-sync.lua");

        // Act & Assert
        var result = ExecuteLuaScript(scriptPath);
        Assert.True(result.Success, $"Script execution failed: {result.ErrorMessage}");
    }

    /// <summary>
    /// 执行Lua脚本并返回结果
    /// </summary>
    private ScriptExecutionResult ExecuteLuaScript(string scriptPath)
    {
        if (!File.Exists(scriptPath))
        {
            return new ScriptExecutionResult { Success = false, ErrorMessage = $"Script file not found: {scriptPath}" };
        }

        try
        {
            var workingDir = Path.GetDirectoryName(scriptPath);

            var lua = SharpLua.SharpLua.SharpLuaNewState(workingDir);

            var success = SharpLua.SharpLua.SharpLuaDoFile(lua, scriptPath, out var nResults);

            if (!success)
            {
                string errorMessage = "Unknown error";
                if (lua.GetTop() > 0)
                {
                    errorMessage = lua.ToString(-1) ?? "No error message available";
                }
                return new ScriptExecutionResult { Success = false, ErrorMessage = errorMessage };
            }

            return new ScriptExecutionResult { Success = true, ResultCount = nResults };
        }
        catch (Exception ex)
        {
            return new ScriptExecutionResult { Success = false, ErrorMessage = ex.ToString() };
        }
    }

    /// <summary>
    /// 脚本执行结果
    /// </summary>
    private record ScriptExecutionResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public int ResultCount { get; init; }
        public bool IsAssertionFailure => ErrorMessage?.Contains("assertion failed") == true;
        public string? AssertionDetails => IsAssertionFailure ?
            ErrorMessage?.Replace("assertion failed: ", "").Trim() : null;
    }
}
