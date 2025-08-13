namespace SharpLua.Tests;

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
    public void Test_SyntaxError()
    {
        // Arrange
        var scriptPath = Path.Combine(_testLuaPath, "syntax-error.lua");

        // Act
        var result = DoLuaFile(scriptPath);

        // Assert
        Assert.False(result.Success, "Script with syntax error should fail to execute");
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("syntax error", result.ErrorMessage.ToLower());
    }
    
    [Fact]
    public void Test_RaiseError()
    {
        // Arrange
        var scriptPath = Path.Combine(_testLuaPath, "raise-error.lua");

        // Act
        var result = DoLuaFile(scriptPath);

        // Assert
        Assert.False(result.Success, "Script that raises an error should fail to execute");
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("make error", result.ErrorMessage);
    }

    [Fact]
    public void Test_BasicScript_ShouldExecuteSuccessfully()
    {
        // Arrange
        var scriptPath = Path.Combine(_testLuaPath, "basic.lua");

        // Act & Assert
        var result = DoLuaFile(scriptPath);
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
    public void Test_SystemScript_ShouldExecuteSuccessfully()
    {
        // Arrange
        var scriptPath = Path.Combine(_testLuaPath, "system.lua");

        // Act & Assert
        var result = DoLuaFile(scriptPath);
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
    public void Test_CSharpException_ShouldBeCaughtByLua()
    {
        // Arrange
        var scriptPath = Path.Combine(_testLuaPath, "csharp-exception.lua");

        // Act & Assert
        var result = DoLuaFile(scriptPath);
        if (!result.Success && result.IsAssertionFailure)
        {
            Assert.True(result.Success, $"Assertion failed in Lua script: {result.AssertionDetails}");
        }
        else
        {
            Assert.True(result.Success, $"C# exception test failed: {result.ErrorMessage}");
        }
    }

    /// <summary>
    /// 执行Lua脚本并返回结果
    /// </summary>
    private DoFileResult DoLuaFile(string scriptPath)
    {
        if (!File.Exists(scriptPath))
        {
            return new DoFileResult { Success = false, ErrorMessage = $"Script file not found: {scriptPath}" };
        }

        try
        {
            var entryFile = Path.GetFullPath(scriptPath);
            var workingDir = Path.GetDirectoryName(entryFile);

            var lua = SharpLuaState.NewState(workingDir);

            // Register test extensions for testing purposes
            TestExtensions.RegisterTestExtensions(lua);

            var success = SharpLuaState.DoFile(lua, entryFile, out var nResults);

            if (!success)
            {
                string errorMessage = "unknown error";
                if (nResults > 0)
                {
                    errorMessage = lua.ToString(-1) ?? "no error message available";
                }
                return new DoFileResult { Success = false, ErrorMessage = errorMessage };
            }

            return new DoFileResult { Success = true, ResultCount = nResults };
        }
        catch (Exception ex)
        {
            return new DoFileResult { Success = false, ErrorMessage = ex.ToString() };
        }
    }

    /// <summary>
    /// 脚本执行结果
    /// </summary>
    private record DoFileResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public int ResultCount { get; init; }
        public bool IsAssertionFailure => ErrorMessage?.Contains("assertion failed") == true;
        public string? AssertionDetails => IsAssertionFailure ?
            ErrorMessage?.Replace("assertion failed: ", "").Trim() : null;
    }
}
