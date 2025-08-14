//zig build-lib .\sharplua.bridge\src\bridge.zig -dynamic

const std = @import("std");
pub const lua_State = opaque {};

// 定义所有需要的Lua API函数下标
const LuaAPI = enum(u8) {
    lua_pushnil,
    lua_pushcclosure,
    lua_tocfunction,
    lua_error,
    // 这里可以继续添加其它API
    count,
};

// 定义函数指针类型
const pf_sharp_lua_func = *const fn (L: *lua_State) i32;

const pf_lua_pushnil = *const fn (L: *lua_State) void;
const pf_lua_pushcclosure = *const fn (L: *lua_State, func: usize, nupvalues: i32) void;
const pf_lua_tocfunction = *const fn (L: *lua_State, index: i32) usize;
const pf_lua_error = *const fn (L: *lua_State) i32;

// 全局函数指针数组
var g_func_ptrs: [@intFromEnum(LuaAPI.count)]usize = [_]usize{0} ** @intFromEnum(LuaAPI.count);

// 注册外部传入的函数指针数组
pub export fn RegistLuaFunc(func_ptrs: [*]const usize, len: usize) void {
    // 只拷贝允许的数量
    const n = if (len < g_func_ptrs.len) len else g_func_ptrs.len;
    for (g_func_ptrs[0..n], 0..) |*dst, i| {
        dst.* = func_ptrs[i];
    }
}

pub export fn PushSharpLuaFunc(L: *lua_State) void {
    const closurePtr = @intFromPtr(@funcAddr(sharpclosure));
    lua_pushcclosure(L, closurePtr, 1);
}

fn sharpclosure(L: *lua_State) i32 {
    const funcPtr = lua_tocfunction(L, lua_upvalueindex(1));
    const func = @as(pf_sharp_lua_func, funcPtr);

    const result = func(L);
    if (result < 0) {
        return lua_error(L);
    } else {
        return result;
    }
    return 0;
}

pub export fn TestLuaFunc(L: *lua_State) void {
    return lua_pushnil(L);
}

fn lua_upvalueindex(i: i32) i32 {
    const LUA_REGISTRYINDEX: i32 = -10000;
    return (LUA_REGISTRYINDEX - i);
}

// 包装函数：调用外部传入的lua_pushnil
pub fn lua_pushnil(L: *lua_State) void {
    const f = @as(pf_lua_pushnil, @ptrFromInt(g_func_ptrs[@intFromEnum(LuaAPI.lua_pushnil)]));
    return f(L);
}

pub fn lua_pushcclosure(L: *lua_State, func: usize, nupvalues: i32) void {
    const f = @as(pf_lua_pushcclosure, @ptrFromInt(g_func_ptrs[@intFromEnum(LuaAPI.lua_pushcclosure)]));
    return f(L, func, nupvalues);
}

pub fn lua_tocfunction(L: *lua_State, index: i32) usize {
    const f = @as(pf_lua_tocfunction, @ptrFromInt(g_func_ptrs[@intFromEnum(LuaAPI.lua_tocfunction)]));
    return f(L, index);
}

pub fn lua_error(L: *lua_State) i32 {
    const f = @as(pf_lua_error, @ptrFromInt(g_func_ptrs[@intFromEnum(LuaAPI.lua_error)]));
    return f(L);
}
