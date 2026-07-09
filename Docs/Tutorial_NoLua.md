# Tutorial_NoLua — 纯 C# 命令模式

## 与 HelloWorld 最大的不同：不用写 Lua

NoLua **完全不写 Lua 脚本**，命令和数据操作全在 C# 中完成。

```csharp
context = new Context();                     // 无参数，空的 Lua table
context.AddCSharpModule("NoLua", this);      // 把 C# 方法注册为命令
context.Attach(gameObject);
```

---

## AddCSharpModule 做了什么

[ViewModel.cs:140-151](Assets/XUUI/Scripts/ViewModel.cs#L140-L151) 中，它通过反射找到所有带 `[Command]` 特性的方法：

```csharp
public void AddCSharpModule(string moduleName, object module)
{
    var methods = module.GetType().GetMethods(...);
    foreach(var cmd in methods.Where(m => m.IsDefined(typeof(CommandAttribute), false)))
    {
        commandSetter(options, moduleName, cmd.Name, module);
    }
}
```

`commandSetter` 在 Lua 侧执行（[ViewModel.cs:47-56](Assets/XUUI/Scripts/ViewModel.cs#L47-L56)）：

```lua
-- Lua 内部等价逻辑
options.data["NoLua"] = {}                   -- 创建模块数据表
options.commands["NoLua.Foo"] = function(...) -- 注册命令
    obj.Foo(...)
end
options.commands["NoLua.Bar"] = function(...)
    obj.Bar(...)
end
```

所以 BindTo 规则变为：**`"模块名.方法名"`**。

---

## Interface 参数的绑定

这是最核心的机制。看两个命令的签名：

```csharp
[Command]
public void Foo(Interface1 data)
{
    Debug.Log(string.Format("NoLua.Foo, got name: {0}", data.name));
    data.name = "Foo";
}
```

`Interface1` 定义在 [SomeClass.cs](Assets/Scripts/SomeClass.cs)：

```csharp
public interface Interface1
{
    string name { get; set; }
}
```

当按钮触发 `NoLua.Foo` 命令时，XLua 自动做了一层**接口代理**：

```
Button 点击 → commands["NoLua.Foo"](options.data["NoLua"])
                              ↓
              XLua 把 LuaTable 包装成 Interface1 代理
                              ↓
              Foo(Interface1 data) 被调用
                              ↓
              data.name → 读写的都是 Lua 表的 name 字段
```

**所以 `data` 指向的就是 Lua 模块数据表 `options.data["NoLua"]`**。你在 C# 里读写 `data.name`，等价于在 Lua 中读写 `data.NoLua.name`，并且会经过响应式系统触发 UI 刷新。

---

## 场景设置

对应的 UI 绑定关系（推测）：

| GameObject | 适配器 | BindTo | 作用 |
|---|---|---|---|
| Text | TextAdapter | `"NoLua.name"` | 显示 name 值 |
| InputField | InputFieldAdapter | `"NoLua.name"` | 输入 name |
| Button(Foo) | ButtonAdapter | `"NoLua.Foo"` | 触发 Foo 命令 |
| Button(Bar) | ButtonAdapter | `"NoLua.Bar"` | 触发 Bar 命令 |
| Dropdown | DropdownAdapter | `"NoLua.select"` | 选择项索引 |

BindTo 的规则变成了 `"模块名.字段/属性"` 或 `"模块名.方法名"`。

---

## 完整数据流

```
用户点击 "Foo" 按钮
  → ButtonAdapter.OnAction()
    → commands["NoLua.Foo"](options.data["NoLua"])
      → XLua 接口代理
        → Foo(Interface1 data) 被调用
          → Debug.Log(data.name)  ← 读取 data.NoLua.name
          → data.name = "Foo"     ← 写入 data.NoLua.name
            → 响应式系统触发
              → 关联的 Text 自动更新显示
              → InputField 自动更新内容
```

---

## HelloWorld vs NoLua 对比

| | HelloWorld | NoLua |
|---|---|---|
| ViewModel 定义 | Lua table 内联 | C# 类 + `[Command]` 特性 |
| 数据源 | `data.info.name` | `options.data["NoLua"]`（自动创建） |
| BindTo 示例 | `"message"`、`"click"` | `"NoLua.Foo"`、`"NoLua.name"` |
| 命令参数 | Lua function 接收 data | C# 方法接收 Interface 参数 |
| 适用场景 | 快速原型、动态逻辑 | 已有 C# 代码复用、类型安全 |

---

## 要点总结

1. `new Context()` 创建空的 Lua ViewModel，不执行任何脚本
2. `AddCSharpModule("NoLua", this)` 通过反射 + XLua 桥接，把 `[Command]` 方法注册到 Lua 命令表
3. 命令参数中的 `Interface1` / `Interface2` 由 XLua 自动代理，**读写接口属性就是读写 Lua 响应式数据**
4. BindTo 统一使用 `"模块名.路径"` 格式
