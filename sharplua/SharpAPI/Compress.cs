namespace SharpLua;

using LuaState = KeraLua.Lua;
using System;
using System.IO.Compression;
using System.IO;

public static class SharpAPI_Compress
{
    public static void Register(LuaState lua)
    {
        lua.RegistSharpLuaFunction(nameof(Zip), Zip);
        lua.RegistSharpLuaFunction(nameof(UnZip), UnZip);
    }

    static int Zip(IntPtr statePtr)
    {
        var lua = LuaState.FromIntPtr(statePtr);
        try
        {
            var sourcePath = lua.ToString(1);
            var targetPath = lua.ToString(2);
            var compressLevel = lua.IsNumber(3) ? (CompressionLevel)lua.ToInteger(3) : CompressionLevel.Optimal;

            if (File.Exists(sourcePath))
            {
                using (var fs = new FileStream(targetPath, FileMode.Create))
                {
                    using (var arch = new ZipArchive(fs, ZipArchiveMode.Create))
                    {
                        string entryName = Path.GetFileName(sourcePath);
                        arch.CreateEntryFromFile(sourcePath, entryName, compressLevel);
                    }
                }
            }
            else if (Directory.Exists(sourcePath))
            {
                var includeBaseDir = lua.IsBoolean(4) ? lua.ToBoolean(4) : true;
                ZipFile.CreateFromDirectory(sourcePath, targetPath, compressLevel, includeBaseDir);
            }
            return 0;
        }
        catch (Exception e)
        {
            return lua.SharpLuaError(e);
        }
    }

    static int UnZip(IntPtr statePtr)
    {
        var lua = LuaState.FromIntPtr(statePtr);
        try
        {
            var zipPath = lua.ToString(1);
            var dirPath = lua.ToString(2);
            ZipFile.ExtractToDirectory(zipPath, dirPath, true);
            return 0;
        }
        catch (Exception e)
        {
            return lua.SharpLuaError(e);
        }
    }
}