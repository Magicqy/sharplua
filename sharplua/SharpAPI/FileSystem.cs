namespace SharpLua;

using LuaState = KeraLua.Lua;
using System;

static class SharpAPI_FileSystem
{
    internal static void Register(LuaState lua)
    {
        lua.RegistSharpLuaFunction(nameof(FileExists), FileExists);
        lua.RegistSharpLuaFunction(nameof(FileDelete), FileDelete);
        lua.RegistSharpLuaFunction(nameof(FileCopy), FileCopy);
        lua.RegistSharpLuaFunction(nameof(FileMove), FileMove);
        lua.RegistSharpLuaFunction(nameof(FileGetSize), FileGetSize);
        lua.RegistSharpLuaFunction(nameof(FileReadAllText), FileReadAllText);
        lua.RegistSharpLuaFunction(nameof(FileWriteAllText), FileWriteAllText);
        lua.RegistSharpLuaFunction(nameof(FileComputeMD5), FileComputeMD5);

        lua.RegistSharpLuaFunction(nameof(DirectoryCreate), DirectoryCreate);
        lua.RegistSharpLuaFunction(nameof(DirectoryDelete), DirectoryDelete);
        lua.RegistSharpLuaFunction(nameof(DirectoryExists), DirectoryExists);
        lua.RegistSharpLuaFunction(nameof(DirectoryGetCurrent), DirectoryGetCurrent);
        lua.RegistSharpLuaFunction(nameof(DirectorySetCurrent), DirectorySetCurrent);
        lua.RegistSharpLuaFunction(nameof(DirectoryCopy), DirectoryCopy);
        lua.RegistSharpLuaFunction(nameof(DirectoryMove), DirectoryMove);
        lua.RegistSharpLuaFunction(nameof(DirectoryGetFiles), DirectoryGetFiles);
        lua.RegistSharpLuaFunction(nameof(DirectoryGetDirs), DirectoryGetDirs);
    }

    static int FileExists(LuaState lua)
    {
        var path = lua.ToString(1);
        var exists = System.IO.File.Exists(path);
        lua.PushBoolean(exists);
        return 1;
    }

    static int FileDelete(LuaState lua)
    {
        var path = lua.ToString(1);
        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
        }
        return 0;
    }

    static int DirectoryCreate(LuaState lua)
    {
        var path = lua.ToString(1);
        if (!System.IO.Directory.Exists(path))
        {
            System.IO.Directory.CreateDirectory(path);
        }
        return 0;
    }

    static int DirectoryDelete(LuaState lua)
    {
        var path = lua.ToString(1);
        var recursive = lua.ToBoolean(2);
        if (System.IO.Directory.Exists(path))
        {
            System.IO.Directory.Delete(path, recursive);
        }
        return 0;
    }

    static int DirectoryExists(LuaState lua)
    {
        var path = lua.ToString(1);
        var exists = System.IO.Directory.Exists(path);
        lua.PushBoolean(exists);
        return 1;
    }

    static int DirectoryGetCurrent(LuaState lua)
    {
        var path = System.IO.Directory.GetCurrentDirectory();
        lua.PushString(path);
        return 1;
    }

    static int DirectorySetCurrent(LuaState lua)
    {
        var path = lua.ToString(1);
        System.IO.Directory.SetCurrentDirectory(path);
        return 0;
    }

    static int DirectoryMove(LuaState lua)
    {
        var srcPath = lua.ToString(1);
        var destPath = lua.ToString(2);
        System.IO.Directory.Move(srcPath, destPath);
        return 0;
    }

    static int FileCopy(LuaState lua)
    {
        var srcPath = lua.ToString(1);
        var tarPath = lua.ToString(2);
        var overwrite = false;
        if (lua.GetTop() > 2)
        {
            overwrite = lua.ToBoolean(3);
        }
        System.IO.File.Copy(srcPath, tarPath, overwrite);
        return 0;
    }

    static int FileMove(LuaState lua)
    {
        var srcPath = lua.ToString(1);
        var tarPath = lua.ToString(2);
        var overwrite = false;
        if (lua.GetTop() > 2)
        {
            overwrite = lua.ToBoolean(3);
        }
        System.IO.File.Move(srcPath, tarPath, overwrite);
        return 0;
    }

    static int FileGetSize(LuaState lua)
    {
        var path = lua.ToString(1);
        var size = new System.IO.FileInfo(path).Length;
        lua.PushInteger(size);
        return 1;
    }

    static int DirectoryCopy(LuaState lua)
    {
        var srcDirPath = lua.ToString(1);
        var tarDirPath = lua.ToString(2);
        InternalDirectoryCopy(srcDirPath, tarDirPath);
        return 0;
    }

    private static void InternalDirectoryCopy(string sourceFolder, string destFolder)
    {
        if (!System.IO.Directory.Exists(sourceFolder)) return;
        string folderName = System.IO.Path.GetFileName(sourceFolder);
        string destfolderdir = System.IO.Path.Combine(destFolder, folderName);
        string[] filenames = System.IO.Directory.GetFileSystemEntries(sourceFolder);
        foreach (string file in filenames)// �������е��ļ���Ŀ¼
        {
            if (System.IO.Directory.Exists(file))
            {
                string currentdir = System.IO.Path.Combine(destfolderdir, System.IO.Path.GetFileName(file));
                if (!System.IO.Directory.Exists(currentdir))
                {
                    System.IO.Directory.CreateDirectory(currentdir);
                }
                InternalDirectoryCopy(file, destfolderdir);
            }
            else
            {
                string srcfileName = System.IO.Path.Combine(destfolderdir, System.IO.Path.GetFileName(file));
                if (!System.IO.Directory.Exists(destfolderdir))
                {
                    System.IO.Directory.CreateDirectory(destfolderdir);
                }
                System.IO.File.Copy(file, srcfileName, true);
            }
        }
    }

    static int FileReadAllText(LuaState lua)
    {
        var path = lua.ToString(1);
        var text = System.IO.File.ReadAllText(path);
        if (text != null)
        {
            lua.PushString(text);
        }
        else
        {
            lua.PushNil();
        }
        return 1;
    }

    static int FileWriteAllText(LuaState lua)
    {
        var path = lua.ToString(1);
        var contents = lua.ToString(2);
        var append = false;
        if (lua.GetTop() > 2)
        {
            append = lua.ToBoolean(3);
        }
        if (append)
        {
            System.IO.File.AppendAllText(path, contents);
        }
        else
        {
            System.IO.File.WriteAllText(path, contents);
        }
        return 0;
    }

    static int FileComputeMD5(LuaState lua)
    {
        var path = lua.ToString(1);
        using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read))
        {
            System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] hash = md5.ComputeHash(fs);
            var sb = new System.Text.StringBuilder();
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            lua.PushString(sb.ToString());
            return 1;
        }
    }

    static int DirectoryGetFiles(LuaState lua)
    {
        var path = lua.ToString(1);
        var searchPattern = lua.ToString(2);
        var searchTopDirOnly = lua.ToBoolean(3);
        if (lua.GetTop() == 3)
        {
            lua.NewTable();
        }
        else
        {
            if (lua.IsTable(4) == false)
            {
                throw new ArgumentException("invalid argument type, table expected.");
            }
        }
        var files = System.IO.Directory.EnumerateFiles(path, searchPattern, searchTopDirOnly ? System.IO.SearchOption.TopDirectoryOnly : System.IO.SearchOption.AllDirectories);
        var index = 1;
        foreach (var item in files)
        {
            lua.PushString(item);
            lua.RawSetInteger(4, index++);
        }
        return 1;
    }

    static int DirectoryGetDirs(LuaState lua)
    {
        var path = lua.ToString(1);
        var searchPattern = lua.ToString(2);
        var searchTopDirOnly = lua.ToBoolean(3);
        if (lua.GetTop() == 3)
        {
            lua.NewTable();
        }
        else
        {
            if (lua.IsTable(4) == false)
            {
                throw new ArgumentException("invalid argument type, table expected.");
            }
        }
        var dirs = System.IO.Directory.GetDirectories(path, searchPattern, searchTopDirOnly ? System.IO.SearchOption.TopDirectoryOnly : System.IO.SearchOption.AllDirectories);
        var index = 1;
        foreach (var item in dirs)
        {
            lua.PushString(item);
            lua.RawSetInteger(4, index++);
        }
        return 1;
    }
}