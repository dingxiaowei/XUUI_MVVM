# Tutorial_App — 多模块应用框架

## 与之前教程最大的不同：模块化架构

Helloworld 和 MoreComplex 都是单个 ViewModel 的简单示例。**App 模式**引入了真正的模块化架构：

- 将 UI 逻辑拆分为多个 **模块（module）**，各自独立
- 每个模块有自己的 `data` / `computed` / `commands`，互不干扰
- 模块之间通过 `exports` 可控交互
- 支持模块热刷新（reload），不影响其他模块

---

## 1. 场景搭建

### 1.1 创建 GameObject 层级

```
App (GameObject)
├── Canvas
│   ├── Module1InfoText (Text)       ← 显示 module1.info
│   ├── Module1ClickBtn (Button)     ← 触发 module1.click
│   ├── Module2InfoText (Text)       ← 显示 module2.info
│   ├── Module2ClickBtn (Button)     ← 触发 module2.click
│   ├── SayHelloBtn (Button)         ← 触发 module2.say_hello_to_csharp
│   ├── ReloadModule1Btn (Button)    ← 重载 module1
│   └── ReloadModule2Btn (Button)    ← 重载 module2
```

### 1.2 挂载适配器

| GameObject | 适配器 | BindTo | 说明 |
|---|---|---|---|
| Module1InfoText | TextAdapter | `"module1.info"` | 显示 module1 的 computed.info |
| Module1ClickBtn | ButtonAdapter | `"module1.click"` | 触发 module1 的 click 命令 |
| Module2InfoText | TextAdapter | `"module2.info"` | 显示 module2 的 computed.info |
| Module2ClickBtn | ButtonAdapter | `"module2.click"` | 触发 module2 的 click 命令 |
| SayHelloBtn | ButtonAdapter | `"module2.say_hello_to_csharp"` | 调用 C# 的 HelloCSharp |
| ReloadModule1Btn | ButtonAdapter | `"ReloadModule1"` | App.cs 的 Command，重载 module1 |
| ReloadModule2Btn | ButtonAdapter | `"ReloadModule2"` | App.cs 的 Command，重载 module2 |

关键区别：BindTo 的格式变为 **`"模块名.路径"`**，因为 App 模式下有多个模块，UI 需要显式指定数据来自哪个模块。

---

## 2. 代码解析

### 2.1 App.cs —— C# 入口

```csharp
using UnityEngine;
using XUUI;

public class App : MonoBehaviour
{
    Context context = null;

    void Start()
    {
        context = new Context(@"
            return {
                name  = 'myapp', 
                modules = {'module1', 'module2'},
           }
        ");

        context.AddCSharpModule("ModuleManager", this);
        context.Attach(gameObject);
    }

    void OnDestroy()
    {
        context.Dispose();
    }

    [Export]
    public void HelloCSharp(Interface2 data, int p)
    {
        Debug.Log("data.select=" + data.select + ", p=" + p);
    }

    [Command]
    public void ReloadModule1()
    {
        context.ReloadModule("module1");
    }

    [Command]
    public void ReloadModule2()
    {
        context.ReloadModule("module2");
    }
}
```

#### 逐行解释

| 代码 | 作用 |
|---|---|
| `name = 'myapp'` | 定义应用名称，模块加载时会按 `myapp.module1`、`myapp.module2` 的路径 require |
| `modules = {'module1', 'module2'}` | 声明两个模块，框架自动加载并为每个创建独立沙盒 |
| `context.AddCSharpModule("ModuleManager", this)` | 将当前 C# 对象注册为名为 "ModuleManager" 的模块，其 `[Export]` 方法可在 Lua 中调用 |
| `context.Attach(gameObject)` | 绑定 UI，开始数据监听 |
| `[Export] public void HelloCSharp(...)` | 标记为导出函数，Lua 代码可以调用 `ModuleManager.HelloCSharp(data, 1024)` |
| `[Command] public void ReloadModule1()` | 标记为命令，UI 可通过 BindTo="ReloadModule1" 绑定按钮 |

### 2.2 module1.lua.txt

```lua
return {
    data = {
        name = "haha",
        select = 0,
    },

    commands = {
        click = function(data)
            module2.set_select(data.select)    -- 跨模块调用：调用 module2 的 exports
            data.select = data.select == 0 and 1 or 0
        end,
    },

    computed = {
        info = function(data)
            return string.format('i am %s, my select is %d', data.name, data.select)
        end,
    },

    exports = {
        hello = function(p)
            print('hello, p = ' .. p)
        end,
    },
}
```

### 2.3 module2.lua.txt

```lua
local data = {
    message = "hehe",
    select = 1,
}

return {
    data = data,

    commands = {
        click = function(data)
            module1.hello(1)               -- 跨模块调用：调用 module1 的 exports
            data.select = data.select == 0 and 1 or 0
        end,

        say_hello_to_csharp = function(data)
            ModuleManager.HelloCSharp(data, 1024)  -- 调用 C# 的 Export 方法
        end,
    },

    computed = {
        info = function(data)
            return string.format('message is %s, select is %d', data.message, data.select)
        end,
    },

    exports = {
        set_select = function(p)
            data.select = p
        end,
    },
}
```

---

## 3. 核心概念详解

### 3.1 模块沙盒与数据隔离

每个模块运行在**独立的沙盒**中：

```
沙盒 module1:
  data = { name = "haha", select = 0 }
  commands.click 只能读写 module1 的 data
  computed.info  只能读取 module1 的 data

沙盒 module2:
  data = { message = "hehe", select = 1 }
  commands.click 只能读写 module2 的 data
  computed.info  只能读取 module2 的 data
```

这意味着：
- 即使两个模块定义了同名变量（如 `select`），也不会冲突
- command 函数收到的是**自己模块的 data**，不能直接修改其他模块的数据
- 不同开发团队可以独立开发各自模块，不用担心全局命名冲突

### 3.2 模块间通信：exports

模块通过 `exports` 暴露接口供其他模块调用：

```
module2.commands.click 调用 module1.hello(1)
                     ↓
          调用 module1.exports.hello(p)
                     ↓
          打印 "hello, p = 1"


module1.commands.click 调用 module2.set_select(data.select)
                     ↓
          调用 module2.exports.set_select(p)
                     ↓
          设置 module2 的 data.select = p
```

注意 `module2.exports.set_select` 使用了闭包引用外部的 `local data`：
```lua
local data = { ... }           -- 闭包变量
return {
    data = data,
    exports = {
        set_select = function(p)
            data.select = p     -- 直接操作闭包引用的 data
        end,
    },
}
```

这种方式绕过了 command 的"只能看到自己 data"的限制——因为 `set_select` 不是 command，不接收 `data` 参数，而是通过 Lua 闭包直接访问原始 `data` 表。这在需要允许其他模块修改本模块数据时非常有用。

### 3.3 C# 桥接：Export 与 AddCSharpModule

App.cs 中 `AddCSharpModule("ModuleManager", this)` 注册后：

```
Lua 侧调用:
  ModuleManager.HelloCSharp(data, 1024)
              ↓
XLua 桥接:
  data → XLua 自动包装为 Interface2 代理
              ↓
C# 侧:
  HelloCSharp(Interface2 data, int p)
  → Debug.Log("data.select=" + data.select + ", p=" + 1024)
```

这里的 `Interface2` 是一个 C# 接口，XLua 自动将 Lua table 代理为实现了该接口的对象。所以 `data.select` 读取的就是 Lua 模块数据表中的 `select` 字段。

### 3.4 模块热刷新（Reload）

```csharp
[Command]
public void ReloadModule1()
{
    context.ReloadModule("module1");
}
```

调用 `context.ReloadModule("module1")` 时：

1. 重新 require `myapp.module1`，加载最新的 Lua 脚本
2. 保留旧的 `data` 状态（数据不丢失）
3. 用新的 `commands` 替换旧的——UI 上已绑定的按钮自动关联新命令
4. 重新计算所有 `computed`——UI 自动更新
5. `module2` 完全不受影响

这非常适合需要热更新的场景：修改 Lua 脚本后，点击 Reload 按钮立即生效，无需重新运行 Unity。

---

## 4. 完整数据流

### 初始化流程

```
App.Start()
  └─ new Context(luaScript)
       ├─ 解析 name='myapp', modules={'module1','module2'}
       ├─ require 'myapp.module1' → 创建沙盒1，包装响应式数据
       ├─ require 'myapp.module2' → 创建沙盒2，包装响应式数据
       └─ 注册模块间引用（module1 中可访问 module2，反之亦然）
  └─ context.AddCSharpModule("ModuleManager", this)
       └─ 注册 [Export] 方法到 Lua 环境
  └─ context.Attach(gameObject)
       └─ 扫描适配器，按 "模块名.路径" 绑定
```

### 点击 module1.click 时

```
用户点击 Module1ClickBtn
  → ButtonAdapter.OnAction()
    → commands["module1.click"](module1.data)
      → module2.set_select(module1.data.select)  -- 跨模块调用
        → module1.data.select = 0 → module2 的 data 变为 0
        → module2 的 UI 自动更新
      → module1.data.select = 1                   -- 修改自己的数据
        → module1 的 UI 自动更新
```

### 点击 SayHelloBtn 时

```
用户点击 SayHelloBtn
  → ButtonAdapter.OnAction()
    → commands["module2.say_hello_to_csharp"](module2.data)
      → ModuleManager.HelloCSharp(data, 1024)
        → XLua 接口代理 → C# HelloCSharp(Interface2 data, int p)
          → Debug.Log("data.select=0, p=1024")
```

---

## 5. 绑定关系总结

| GameObject | 适配器 | BindTo | ViewModel 映射 | 方向 |
|---|---|---|---|---|
| Module1InfoText | TextAdapter | `"module1.info"` | module1 的 computed.info | VM → View |
| Module1ClickBtn | ButtonAdapter | `"module1.click"` | module1 的 commands.click | View → VM |
| Module2InfoText | TextAdapter | `"module2.info"` | module2 的 computed.info | VM → View |
| Module2ClickBtn | ButtonAdapter | `"module2.click"` | module2 的 commands.click | View → VM |
| SayHelloBtn | ButtonAdapter | `"module2.say_hello_to_csharp"` | module2 的 commands.say_hello_to_csharp | View → VM |
| ReloadModule1Btn | ButtonAdapter | `"ReloadModule1"` | App.cs 的 [Command] ReloadModule1 | View → C# |
| ReloadModule2Btn | ButtonAdapter | `"ReloadModule2"` | App.cs 的 [Command] ReloadModule2 | View → C# |

---

## 6. 跟之前教程的对比

| 特性 | Helloworld | MoreComplex | App 模式 |
|---|---|---|---|
| 模块数 | 1 个隐含模块 | 1 个隐含模块 | 多个独立模块 |
| BindTo 格式 | `"info"` | `"options"` | `"module1.info"` |
| 数据隔离 | 无 | 无 | 各模块沙盒隔离 |
| 模块通信 | N/A | N/A | 通过 exports |
| C# 集成 | 无 | 无 | AddCSharpModule + Export |
| 热刷新 | 不支持 | 不支持 | ReloadModule 支持 |
| Lua 文件位置 | 内联在 C# 中 | 内联在 C# 中 | 独立的 .lua.txt 文件 |

---

## 7. 动手实验

### 7.1 观察模块隔离

点击 module1 的 Click 按钮，观察两个 InfoText 的变化。你会看到：
- Module1InfoText 里的 `select` 在 0 和 1 之间切换
- Module2InfoText 里的 `select` 可能也被 module1 的跨模块调用改变了

### 7.2 尝试跨模块调用链

修改 `module1.commands.click`，让它调用 `module2.hello()`（如果 module2 也有 exports.hello 的话），观察调用链如何串联两个模块。

### 7.3 测试热刷新

1. 运行场景，观察 UI 显示
2. 修改 `module1.lua.txt` 中的 `data.name = "haha"` 改为 `data.name = "xixi"`
3. 点击 **ReloadModule1** 按钮
4. 观察 Module1InfoText 的显示更新为 "i am xixi, ..." 而 Module2InfoText 不受影响

### 7.4 新增一个 module3

1. 在 `Resources/myapp/` 下创建 `module3.lua.txt`
2. 在 App.cs 的 modules 列表中加入 `'module3'`
3. 添加对应的 UI 和适配器绑定

---

## 8. 完整文件清单

```
Assets/
├── Scenes/
│   └── App.unity                         ← 本示例的场景
├── Scripts/
│   └── App.cs                            ← C# 入口，App 模式启动器
├── Resources/
│   └── myapp/
│       ├── module1.lua.txt               ← 模块1：data/commands/computed/exports
│       └── module2.lua.txt               ← 模块2：data/commands/computed/exports + C# 桥接
└── XUUI/
    ├── Scripts/
    │   ├── ViewModel.cs                  ← Context 类，包含 AddCSharpModule / ReloadModule
    │   ├── AdapterBase.cs
    │   └── UGUIAdapter/
    │       ├── TextAdapter.cs
    │       ├── ButtonAdapter.cs
    │       └── ...
    └── Resources/
        ├── xuui.lua.txt                  ← XUUI 主模块，包含 app 模式的初始化逻辑
        ├── binding.lua.txt               ← 绑定引擎
        ├── observeable.lua.txt           ← 响应式数据系统
        └── ...
```

---

## 9. 常见问题

### Q: module1 和 module2 是全局变量吗？

是的，但它们是**沙盒内的全局变量**。框架在加载每个模块时，会把其他模块名注册到当前沙盒的全局环境中，所以 `module1` 中可以直接用 `module2.set_select(...)`。这本质上是框架管理的命名空间，不是真正的 Lua 全局变量，不会污染全局环境。

### Q: 为什么 exports 里的函数能修改 data 而 command 不行？

command 的函数签名是 `function(data)`，这里的 `data` 是框架传入的**当前模块的 data 副本引用**，command 只能读写本模块的 data。而 exports 里的函数通过 Lua **闭包**捕获了外部的 `data` 变量，可以自由操作。这是设计上的有意区别：command 是"模块对外的行为接口"，exports 是"模块间的编程接口"。

### Q: Reload 后 data 会重置吗？

不会。`ReloadModule` 会保留当前的 data 状态，只替换 commands、computed 和 exports。所以你在 Reload 前修改的数据会保持不变。

### Q: 模块脚本必须放在 Resources/myapp 目录下吗？

不是必须的。默认按 Lua 的 require 规则查找，但可以通过 `CustomLoader` 自定义加载路径和文件后缀。为了演示方便，本示例放在 `Resources/myapp` 目录下。
