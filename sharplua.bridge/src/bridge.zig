const std = @import("std");
pub const lua_State = opaque {};
pub const CSharpFunction = *const fn (*lua_State) callconv(.c) c_int;

// Lua API function pointer types
const pf_lua_touserdata = *const fn (*lua_State, c_int) callconv(.c) ?*anyopaque;
const pf_lua_pushlstring = *const fn (*lua_State, [*]const u8, usize) callconv(.c) [*]const u8;
const pf_lua_error = *const fn (*lua_State) callconv(.c) c_int;
const pf_lua_gettop = *const fn (*lua_State) callconv(.c) c_int;
const pf_lua_newuserdatauv = *const fn (*lua_State, usize, c_int) callconv(.c) ?*anyopaque;
const pf_lua_pushcclosure = *const fn (*lua_State, *const fn (*lua_State) callconv(.c) c_int, c_int) callconv(.c) void;
const pf_lua_setglobal = *const fn (*lua_State, [*:0]const u8) callconv(.c) void;

fn lua_upvalueindex(i: c_int) c_int {
	// LUA_REGISTRYINDEX - i; with LUA_REGISTRYINDEX = -1001000
	return -1001000 - i;
}

const FunctionInfo = extern struct {
	function: CSharpFunction,
	userdata: ?*anyopaque,
};

const Api = struct {
	initialized: bool = false,
	lib: std.DynLib = undefined,
	lua_touserdata: pf_lua_touserdata = undefined,
	lua_pushlstring: pf_lua_pushlstring = undefined,
	lua_error: pf_lua_error = undefined,
	lua_gettop: pf_lua_gettop = undefined,
	lua_newuserdatauv: pf_lua_newuserdatauv = undefined,
	lua_pushcclosure: pf_lua_pushcclosure = undefined,
	lua_setglobal: pf_lua_setglobal = undefined,
};

var g_api: Api = .{};

fn ensureApiLoaded() bool {
	if (g_api.initialized) return true;
	g_api.lib = std.DynLib.openZ("lua54.dll") catch return false;

	g_api.lua_touserdata = g_api.lib.lookup(pf_lua_touserdata, "lua_touserdata") orelse return false;
	g_api.lua_pushlstring = g_api.lib.lookup(pf_lua_pushlstring, "lua_pushlstring") orelse return false;
	g_api.lua_error = g_api.lib.lookup(pf_lua_error, "lua_error") orelse return false;
	g_api.lua_gettop = g_api.lib.lookup(pf_lua_gettop, "lua_gettop") orelse return false;
	g_api.lua_newuserdatauv = g_api.lib.lookup(pf_lua_newuserdatauv, "lua_newuserdatauv") orelse return false;
	g_api.lua_pushcclosure = g_api.lib.lookup(pf_lua_pushcclosure, "lua_pushcclosure") orelse return false;
	g_api.lua_setglobal = g_api.lib.lookup(pf_lua_setglobal, "lua_setglobal") orelse return false;

	g_api.initialized = true;
	return true;
}

fn pushCString(L: *lua_State, s: []const u8) void {
	_ = g_api.lua_pushlstring(L, s.ptr, s.len);
}

fn lua_closure_wrapper(L: *lua_State) callconv(.c) c_int {
	if (!ensureApiLoaded()) {
		pushCString(L, "Failed to load lua54.dll");
		return g_api.lua_error(L);
	}

	const info_ptr_any = g_api.lua_touserdata(L, lua_upvalueindex(1)) orelse {
		pushCString(L, "Invalid function pointer");
		return g_api.lua_error(L);
	};
	const info: *FunctionInfo = @ptrCast(@alignCast(info_ptr_any));

	const result = info.function(L);
	if (result == -1) {
		if (g_api.lua_gettop(L) == 0) {
			pushCString(L, "C# function returned error");
		}
		return g_api.lua_error(L);
	}

	return result;
}

// Exported: register C# function as global in Lua
pub export fn sharplua_register_function(L: *lua_State, name: [*:0]const u8, func: CSharpFunction, userdata: ?*anyopaque) callconv(.c) c_int {
	if (!ensureApiLoaded()) return 0;

	const ud = g_api.lua_newuserdatauv(L, @sizeOf(FunctionInfo), 1) orelse return 0;
	const info: *FunctionInfo = @ptrCast(@alignCast(ud));
	info.* = .{ .function = func, .userdata = userdata };

	g_api.lua_pushcclosure(L, lua_closure_wrapper, 1);
	g_api.lua_setglobal(L, name);
	return 1;
}

// Exported: raise a Lua error from C with a message
pub export fn sharplua_safe_error(L: *lua_State, message: ?[*:0]const u8) callconv(.c) void {
	if (!ensureApiLoaded()) return;

	const msg: []const u8 = if (message) |m| std.mem.span(m) else "Unknown error";
	pushCString(L, msg);
	_ = g_api.lua_error(L);
}

// Exported: set an explicit path to lua54.dll for loading symbols (security: avoid search path hijacking)
pub export fn sharplua_set_lua_dll_path(path_z: [*:0]const u8) callconv(.c) c_int {
	// Close previous lib if already loaded
	if (g_api.initialized) {
		g_api.lib.close();
		g_api = .{};
	}
	const path = std.mem.span(path_z);
	g_api.lib = std.DynLib.open(path) catch return 0;

	g_api.lua_touserdata = g_api.lib.lookup(pf_lua_touserdata, "lua_touserdata") orelse return 0;
	g_api.lua_pushlstring = g_api.lib.lookup(pf_lua_pushlstring, "lua_pushlstring") orelse return 0;
	g_api.lua_error = g_api.lib.lookup(pf_lua_error, "lua_error") orelse return 0;
	g_api.lua_gettop = g_api.lib.lookup(pf_lua_gettop, "lua_gettop") orelse return 0;
	g_api.lua_newuserdatauv = g_api.lib.lookup(pf_lua_newuserdatauv, "lua_newuserdatauv") orelse return 0;
	g_api.lua_pushcclosure = g_api.lib.lookup(pf_lua_pushcclosure, "lua_pushcclosure") orelse return 0;
	g_api.lua_setglobal = g_api.lib.lookup(pf_lua_setglobal, "lua_setglobal") orelse return 0;

	g_api.initialized = true;
	return 1;
}
