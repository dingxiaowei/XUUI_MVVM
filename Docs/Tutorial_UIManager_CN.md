# XUUI MVVM UI 框架教程

## 目录

1. [架构概览](#1-架构概览)
2. [核心类](#2-核心类)
   - [UIBase](#21-uibase)
   - [UIPanel](#22-uipanel)
   - [UIView](#23-uiview)
   - [UIManager](#24-uimanager)
   - [Context](#25-context)
3. [数据绑定系统](#3-数据绑定系统)
   - [适配器](#31-适配器)
   - [Lua 数据绑定](#32-lua-数据绑定)
4. [创建面板](#4-创建面板)
5. [创建视图](#5-创建视图)
6. [实战案例：登录与注册](#6-实战案例登录与注册)
   - [登录场景（简单绑定）](#61-登录场景简单绑定)
   - [注册面板（托管面板）](#62-注册面板托管面板)
   - [完整流程](#63-完整流程)
7. [生命周期管理](#7-生命周期管理)
8. [最佳实践](#8-最佳实践)

---

## 1. 架构概览

XUUI MVVM 框架采用 **面板-视图（Panel-View）** 层级结构，配合 **Lua 驱动的数据绑定** 实现 UI 逻辑分离。整体架构如下：

```
┌─────────────────────────────────────────────────────────┐
│                      UIManager                           │
│              (单例，持有 LuaEnv)                          │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  ┌──────────────────┐    ┌──────────────────┐           │
│  │    UIPanel A      │    │    UIPanel B      │   ...     │
│  │  - Context (Lua)  │    │  - Context (Lua)  │           │
│  │  - UIView[]       │    │  - UIView[]       │           │
│  └──────────────────┘    └──────────────────┘           │
│         │                       │                        │
│         ▼                       ▼                        │
│  ┌──────────────┐      ┌──────────────┐                 │
│  │   UIView 1    │      │   UIView 2    │                 │
│  │ - Context(Lua)│      │ - Context(Lua)│                 │
│  └──────────────┘      └──────────────┘                 │
└─────────────────────────────────────────────────────────┘
```

### 核心概念

- **面板（Panel）**：顶层 UI 容器（如登录窗口、主菜单），由 `UIManager` 加载管理。每个面板拥有独立的 Lua Context，可包含多个子视图。
- **视图（View）**：面板内的可复用子组件（如角色选择列表、加载动画），拥有独立的 Lua Context，但与面板共享 LuaEnv。
- **UIManager**：全局单例，持有共享的 `LuaEnv`（Lua 虚拟机），管理面板的打开/关闭/预加载，是面板系统的中央注册表。
- **Context**：MVVM 桥梁。编译 Lua 配置表（data, commands, computed），创建可观察数据，通过适配器将数据绑定到 Unity UI 元素。

### 目录结构

```
Assets/
├── Resources/
│   └── UI/
│       ├── Panels/           # 面板预制体 + Lua 脚本
│       │   ├── RegisterPanel.prefab
│       │   └── RegisterPanel.lua.txt
│       └── Views/            # 视图预制体 + Lua 脚本
│
├── Scripts/
│   └── UI/
│       ├── Core/             # 框架核心
│       │   ├── UIBase.cs
│       │   ├── UIPanel.cs
│       │   ├── UIView.cs
│       │   └── UIManager.cs
│       └── Panels/           # 面板 C# 实现
│           └── RegisterPanel.cs
│
├── XUUI/
│   └── Scripts/
│       ├── ViewModel.cs          # Context 类
│       ├── AdapterBase.cs        # 适配器基类
│       ├── UGUIAdapter/          # UGUI 适配器
│       │   ├── TextAdapter.cs
│       │   ├── InputFieldAdapter.cs
│       │   ├── ButtonAdapter.cs
│       │   └── ...
│       └── Editor/               # XLua 生成配置
│
└── XUUI/Resources/
    └── xuui.lua.txt              # XUUI Lua 引擎
    └── observeable.lua.txt       # 可观察数据系统
    └── binding.lua.txt           # C# ↔ Lua 绑定层
```

---

## 2. 核心类

### 2.1 UIBase

**文件**：`Assets/Scripts/UI/Core/UIBase.cs`

所有 UI 元素的抽象基类，提供生命周期状态和基本的 GameObject 管理。

```csharp
public abstract class UIBase
{
    public bool IsOpen { get; protected set; }      // 是否打开
    public bool IsLoaded { get; protected set; }    // 是否已加载
    public GameObject GameObject { get; protected set; }
    public Transform Transform { get; protected set; }
    public string Name { get; protected set; }

    public virtual void Init(GameObject go);           // 用 GameObject 初始化
    public virtual void Open(params object[] args);    // 激活（SetActive(true)）
    public virtual void Close();                       // 停用（SetActive(false)）
    public virtual void Dispose();                     // 关闭 + 销毁 GameObject
}
```

### 2.2 UIPanel

**文件**：`Assets/Scripts/UI/Core/UIPanel.cs`

面板是顶层 UI 容器，持有一个 Lua Context，可以管理子视图。

```csharp
public class UIPanel : UIBase
{
    protected Context context;
    protected LuaEnv luaEnv;
    protected Dictionary<string, UIView> views;
    protected Transform viewContainer;          // "Views" 子节点或自身

    protected virtual string PrefabPath => "UI/Panels/" + GetType().Name;
    protected virtual string LuaModulePath => "UI.Panels." + GetType().Name;

    public void InitWithLua(GameObject go, LuaEnv luaEnv);

    // 视图管理
    public T OpenView<T>(params object[] args) where T : UIView, new();
    public void CloseView(string viewName);
    public void CloseAllViews();
    public T GetView<T>() where T : UIView;

    // 生命周期
    public override void Close();    // 关闭所有视图 + 自身
    public override void Dispose();  // 释放视图 → Context → LuaEnv 清理
}
```

#### 命名约定

- 预制体默认加载路径：`Resources/UI/Panels/{类型名}`
- Lua 模块默认加载路径：`UI.Panels.{类型名}`
- 如果面板 GameObject 存在名为 `"Views"` 的子节点，新视图将挂载到该节点下；否则直接挂载到面板根节点

### 2.3 UIView

**文件**：`Assets/Scripts/UI/Core/UIView.cs`

面板内的轻量子组件。拥有独立的 Lua Context，但共享 LuaEnv。

```csharp
public class UIView : UIBase
{
    protected Context context;
    protected LuaEnv luaEnv;
    protected string luaModulePath;

    public void InitWithLua(GameObject go, LuaEnv luaEnv, string luaModulePath);
    protected virtual void RegisterCSharpModules();
    public override void Dispose();
}
```

通过 `UIPanel.OpenView<T>()` 来实例化并打开一个视图。

### 2.4 UIManager

**文件**：`Assets/Scripts/UI/Core/UIManager.cs`

全局单例，持有共享的 LuaEnv，管理面板生命周期。

```csharp
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; }
    public Transform PanelRoot { get; }  // Canvas 或自动创建的根节点

    // 注册（可选，用于预声明面板）
    public void Register<T>() where T : UIPanel, new();
    public void Register<T>(string prefabPath) where T : UIPanel, new();

    // 面板操作
    public T OpenPanel<T>(params object[] args) where T : UIPanel, new();
    public void ClosePanel<T>(bool destroy = true);
    public T GetPanel<T>() where T : UIPanel;
    public bool IsPanelOpen<T>() where T : UIPanel;
    public void PreloadPanel<T>() where T : UIPanel, new();
}
```

#### OpenPanel 流程

```
UIManager.OpenPanel<T>()
  ├── 检查面板是否已激活 → 已打开则返回，已关闭则调用 Open()
  ├── 解析预制体路径（从覆盖路径或默认约定）
  ├── Resources.Load<GameObject>(path)
  ├── 在 PanelRoot 下实例化
  ├── new T() → panel.InitWithLua(go, luaEnv)
  │     ├── base.Init(go)
  │     ├── 定位 "Views" 容器
  │     ├── require Lua 模块 → new Context(script, luaEnv)
  │     ├── RegisterCSharpModules()  [虚方法钩子]
  │     └── context.Attach(go)       [绑定数据到 UI]
  └── panel.Open(args)
```

### 2.5 Context

**文件**：`Assets/XUUI/Scripts/ViewModel.cs`

Context 是 C# 和 Lua 之间的 MVVM 桥梁。

```csharp
public class Context : IDisposable
{
    public LuaTable options;

    // 构造函数
    public Context(string script, LuaEnv luaEnv);  // 编译 Lua，从返回的表创建
    public Context(LuaEnv luaEnv);                   // 空 Context，后续添加数据

    // C# → Lua 模块注册
    public void AddCSharpModule(string name, object obj);

    // 绑定
    public Action Attach(GameObject go);   // 返回一个 detach 函数
    public void Detach(GameObject go);

    // 热重载
    public void ReloadModule(string name, bool reloadData);

    public void Dispose();
}
```

Lua 侧的配置表结构如下，由 `require` 返回：

```lua
return {
    -- 可观察数据：字段名与 UI 适配器的 BindTo 对应
    data = {
        username = '',
        score = 0,
        items = {},
    },

    -- 命令：可从 UI 调用的函数（如按钮点击）
    commands = {
        login = function(data)
            -- "data" 是可观察数据表
        end,
    },

    -- 计算属性：从 data 派生值的函数
    computed = {
        fullName = function(data)
            return data.firstName .. ' ' .. data.lastName
        end,
    },
}
```

---

## 3. 数据绑定系统

### 3.1 适配器

适配器在 UGUI 组件和 Lua 可观察数据之间建立桥梁。每个适配器通过 `BindTo` 字段映射到 Lua `data` 表中的字段路径。

| 适配器 | UGUI 组件 | 接口 | 方向 |
|---|---|---|---|
| `TextAdapter` | `Text` | `DataConsumer<string>` | Lua → UI（只读） |
| `InputFieldAdapter` | `InputField` | `DataConsumer<string>` + `DataProducer<string>` | 双向 |
| `ButtonAdapter` | `Button` | `EventEmitter` | UI → Lua（命令） |
| `DropdownAdapter` | `Dropdown` | `DataConsumer<int>` + `DataProducer<int>` | 双向 |
| `DropdownOptionsAdapter` | `Dropdown` | `DataConsumer<LuaTable>` | Lua → UI（选项列表） |

**Raw 变体**（如 `RawTextAdapter`、`RawButtonAdapter`）是非 MonoBehaviour 适配器，用于 `ViewBinding` 系统中的动态绑定。

### 3.2 Lua 数据绑定

当调用 `context.Attach(gameObject)` 时，框架执行以下操作：

1. 扫描 GameObject 及其子节点查找适配器（通过 `Collector.Collect()`）
2. 对每个 `DataConsumer` — 订阅 Lua 可观察数据的变更（数据 → UI）
3. 对每个 `DataProducer` — 监听 UI 输入变化并更新 Lua（UI → 数据）
4. 对每个 `EventEmitter` — 将按钮点击绑定到 Lua 命令函数

```
┌─────────┐    data.username     ┌──────────────┐
│  Lua    │ ◄──────────────────► │ InputField    │
│  data   │                      │ (用户名)      │
│         │    data.password     └──────────────┘
│         │ ◄──────────────────► ┌──────────────┐
│         │                      │ InputField    │
│         │                      │ (密码)        │
│         │    data.message      └──────────────┘
│         │ ──────────────────►  ┌──────────────┐
│         │                      │ Text          │
│         │                      │ (提示信息)    │
│ commands│                      └──────────────┘
│         │                      ┌──────────────┐
│  login  │ ◄──────────────────  │ Button        │
│         │    (onClick)         │ (登录按钮)    │
└─────────┘                      └──────────────┘
```

#### 在预制体中设置绑定

在预制体中为每个 UGUI 元素添加适配器组件：

1. 选中 UGUI 元素（如 `username_InputField`）
2. 添加 `InputFieldAdapter` 组件
3. 设置 `BindTo` = `"username"`（必须与 Lua `data` 中的字段名完全匹配）
4. 对于按钮，添加 `ButtonAdapter` 并设置 `BindTo` = `"login"`（必须与命令名匹配）

---

## 4. 创建面板

创建一个面板需要三部分：C# 类、预制体、Lua 模块。

### 第一步：创建 C# 类

```csharp
// Assets/Scripts/UI/Panels/MyPanel.cs
using XUUI.UI;

public class MyPanel : UIPanel
{
    // 可选：覆盖预制体路径约定
    // protected override string PrefabPath => "UI/Panels/MyCustomPath";

    // 可选：覆盖 Lua 模块路径约定
    // protected override string LuaModulePath => "UI.Panels.MyCustomModule";

    // 可选：注册 C# 模块，使其可从 Lua 调用
    protected override void RegisterCSharpModules()
    {
        // context.AddCSharpModule("myModule", this);
    }

    // 便捷打开/关闭辅助方法（供 Lua 调用）
    public static void OpenSelf() { UIManager.Instance.OpenPanel<MyPanel>(); }
    public static void CloseSelf() { UIManager.Instance.ClosePanel<MyPanel>(); }
}
```

### 第二步：创建 Lua 模块

```lua
-- Assets/Resources/UI/Panels/MyPanel.lua.txt
return {
    data = {
        -- 在此声明所有可绑定的数据字段
        title = '欢迎',
        counter = 0,
    },
    commands = {
        onClick = function(data)
            data.counter = data.counter + 1
            print('点击！计数:', data.counter)
        end,
    },
    computed = {
        displayText = function(data)
            return data.title .. '（计数: ' .. data.counter .. '）'
        end,
    },
}
```

### 第三步：创建预制体

1. 在场景中创建一个 UI GameObject（如 Panel）
2. 添加适配器组件并设置 `BindTo` 字段
3. 保存为预制体到 `Assets/Resources/UI/Panels/MyPanel.prefab`

### 第四步：打开面板

```csharp
// 从任意 C# 代码：
UIManager.Instance.OpenPanel<MyPanel>();

// 或者从 Lua：
CS.XUUI.UI.MyPanel.OpenSelf()
```

---

## 5. 创建视图

视图是面板内的子组件。

### 第一步：创建 C# 类

```csharp
// Assets/Scripts/UI/Views/CharacterView.cs
using XUUI.UI;

public class CharacterView : UIView
{
    // 特定于该视图的自定义逻辑
    protected override void RegisterCSharpModules()
    {
        // 为 Lua 回调注册 C# 方法
    }
}
```

### 第二步：创建 Lua 模块

```lua
-- Assets/Resources/UI/Views/CharacterView.lua.txt
return {
    data = {
        selectedIndex = 1,
        characters = {},
    },
    commands = {
        select = function(data)
            -- 处理角色选择
        end,
    },
}
```

### 第三步：创建预制体

保存到 `Assets/Resources/UI/Views/CharacterView.prefab`。

### 第四步：从面板中打开

```csharp
// 在 UIPanel 子类中：
var view = OpenView<CharacterView>("可选参数");
```

视图将挂载到面板的 `"Views"` 子节点下（如果没有则挂载到面板根节点）。

---

## 6. 实战案例：登录与注册

本案例演示一个完整的登录注册流程，包含两种实现：简单的 Login 场景（内联 Lua）和完整的 RegisterPanel（托管面板）。

### 6.1 登录场景（简单绑定）

**场景**：`Assets/Scenes/Login.unity`

一个轻量的 MonoBehaviour，使用内联 Lua 配置。适用于简单的独立界面。

#### C# 代码（`Login.cs`）

```csharp
using UnityEngine;
using XUUI;

public class Login : MonoBehaviour
{
    Context context = null;

    void Start()
    {
        context = new Context(@"
            return {
                data = {
                    username = '',
                    password = '',
                    message = '请输入账号密码'
                },
                commands = {
                    login = function(data)
                        if data.username == 'admin' and data.password == '123456' then
                            data.message = '登录成功！'
                        else
                            data.message = '账号或密码错误！'
                            CS.XUUI.UI.RegisterPanel.OpenSelf()
                        end
                    end,
                },
            }
        ");

        context.Attach(gameObject);
    }

    void OnDestroy()
    {
        context.Dispose();
    }
}
```

#### 预制体设置

将 `Login.cs` 挂载到根 GameObject。添加带适配器的 UGUI 组件：

| UI 元素 | 适配器 | BindTo |
|---|---|---|
| 用户名输入框 | `InputFieldAdapter` | `username` |
| 密码输入框 | `InputFieldAdapter` | `password` |
| 提示文字 | `TextAdapter` | `message` |
| 登录按钮 | `ButtonAdapter` | `login` |

#### 流程

1. 用户输入用户名和密码
2. 点击登录 → 按钮触发 Lua 中的 `commands.login(data)`
3. Lua 验证凭据
4. 验证失败：显示错误信息并通过 `CS.XUUI.UI.RegisterPanel.OpenSelf()` 打开注册面板
5. 验证成功：显示成功信息

### 6.2 注册面板（托管面板）

一个由 UIManager 管理的完整面板，拥有独立的预制体和 Lua 模块。

#### C# 代码（`RegisterPanel.cs`）

```csharp
namespace XUUI.UI
{
    public class RegisterPanel : UIPanel
    {
        public static void OpenSelf()
        {
            UIManager.Instance.OpenPanel<RegisterPanel>();
        }

        public static void CloseSelf()
        {
            UIManager.Instance.ClosePanel<RegisterPanel>();
        }
    }
}
```

#### Lua 模块（`RegisterPanel.lua.txt`）

```lua
return {
    data = {
        username = '',
        password = '',
        message = ''
    },
    commands = {
        register = function(data)
            if data.username == '' or data.password == '' then
                data.message = '请填写所有字段！'
            elseif #data.password < 6 then
                data.message = '密码长度不能少于6位！'
            else
                data.message = '注册成功！现在可以登录了。'
                CS.XUUI.UI.RegisterPanel.CloseSelf()
                -- 清除 Lua 模块缓存，下次打开时获取全新状态
                package.loaded['UI.Panels.RegisterPanel'] = nil
            end
        end,
    }
}
```

#### 预制体设置（`RegisterPanel.prefab`）

在 `Assets/Resources/UI/Panels/RegisterPanel.prefab` 创建 UI 面板预制体：

| UI 元素 | 适配器 | BindTo |
|---|---|---|
| 用户名输入框 | `InputFieldAdapter` | `username` |
| 密码输入框 | `InputFieldAdapter` | `password` |
| 提示文字 | `TextAdapter` | `message` |
| 注册按钮 | `ButtonAdapter` | `register` |

#### 从 C# 打开注册面板

```csharp
// 从任意 C# 代码：
UIManager.Instance.OpenPanel<RegisterPanel>();

// 或者带参数：
// UIManager.Instance.OpenPanel<RegisterPanel>("arg1", 42);
```

#### 从 Lua 打开注册面板

```lua
-- 在任意 Lua 模块中（如登录场景的 Lua）：
CS.XUUI.UI.RegisterPanel.OpenSelf()
```

### 6.3 完整流程

```
用户点击登录（凭据错误）
  │
  ▼
登录场景 Lua：data.message = "账号或密码错误！"
登录场景 Lua：CS.XUUI.UI.RegisterPanel.OpenSelf()
  │
  ▼
UIManager.OpenPanel<RegisterPanel>()
  ├── 加载 Resources/UI/Panels/RegisterPanel.prefab
  ├── 在 Canvas 下实例化
  ├── new RegisterPanel()
  │     └── InitWithLua → require 'UI.Panels.RegisterPanel'
  │           ├── Lua 返回 {data, commands}
  │           ├── xuui.new() 创建可观察数据
  │           └── context.Attach(go) → 绑定适配器
  └── panel.Open() → SetActive(true)

用户填写字段，点击注册
  │
  ▼
注册面板 Lua：验证字段
  ├── 空字段 → data.message = "请填写所有字段！"
  └── 成功 → data.message = "注册成功！现在可以登录了。"
        CS.XUUI.UI.RegisterPanel.CloseSelf()
        package.loaded['UI.Panels.RegisterPanel'] = nil
          │
          ▼
        UIManager.ClosePanel<RegisterPanel>(destroy: true)
          └── panel.Dispose()
                ├── CloseAllViews()
                ├── context.Dispose()
                ├── 从 package.loaded 清除 Lua 模块
                ├── 销毁 GameObject
                └── 从 activePanels 移除
```

### Dispose 详细流程

面板销毁时，`UIPanel.Dispose()` 按顺序执行清理：

```csharp
// UIPanel.Dispose() 伪代码流程：
1. 关闭并释放所有子 UIView
2. 清空 views 字典
3. 释放 Context（解绑所有适配器，释放 Lua 引用）
4. 从 package.loaded 清除 Lua 模块，确保下次打开获得全新状态
5. 调用 base.Dispose() → Close() + Destroy(GameObject)
```

`UIManager.OnDestroy()` 处理全局清理：

```csharp
// UIManager.OnDestroy() 伪代码流程：
1. 按倒序释放所有面板
2. 强制 GC.Collect() + GC.WaitForPendingFinalizers()
3. luaEnv.Dispose()  ← 销毁共享的 Lua 虚拟机
```

---

## 7. 生命周期管理

### 状态图

```
         ┌──────────────────────────────────┐
         │         UIBase（抽象类）           │
         ├──────────────────────────────────┤
         │  IsLoaded = false                 │
         │  IsOpen = false                   │
         └──────────┬───────────────────────┘
                    │ Init(go)
                    ▼
         ┌──────────────────────────────────┐
         │         IsLoaded = true           │
         │         IsOpen = false            │
         └──────────┬───────────────────────┘
                    │ Open(args)
                    ▼
         ┌──────────────────────────────────┐
         │         IsLoaded = true           │
         │         IsOpen = true             │
         └──────────┬───────────────────────┘
                    │ Close()
                    ▼
         ┌──────────────────────────────────┐
         │         IsLoaded = true           │
         │         IsOpen = false            │
         └──────────┬───────────────────────┘
                    │ 可再次 Open()
                    │  或 Dispose()
                    ▼
         ┌──────────────────────────────────┐
         │         IsLoaded = false          │
         │         IsOpen = false            │
         │         GameObject 已销毁         │
         └──────────────────────────────────┘
```

### 面板复用

当调用 `ClosePanel<T>(destroy: false)` 时，面板被停用但保留在内存中。后续的 `OpenPanel<T>()` 会复用该实例并调用 `Open()`。

当调用 `ClosePanel<T>(destroy: true)`（默认值）时，面板被彻底释放并销毁。

### 预加载

`PreloadPanel<T>()` 实例化预制体并初始化 Lua，但不激活 GameObject。适用于需要立即显示的面板。

```csharp
// 在加载界面预加载
UIManager.Instance.PreloadPanel<InventoryPanel>();

// 稍后 — 瞬间打开（无需 Resources.Load 开销）
UIManager.Instance.OpenPanel<InventoryPanel>();
```

---

## 8. 最佳实践

### 面板设计

- **每面板一个 Lua 模块**：每个 `UIPanel` 或 `UIView` 应有独立的 Lua 模块管理数据、命令和计算属性。
- **通过 Lua 调用 `CloseSelf()`**：可自行关闭的面板（如确认对话框、注册成功页）应暴露静态 `OpenSelf()` / `CloseSelf()` 辅助方法供 Lua 调用。
- **关闭时清除模块缓存**：面板释放后将 `package.loaded[modulePath]` 置为 `nil`，确保下次打开获得全新的 Lua 状态。`UIPanel.Dispose()` 会自动处理。

### 数据绑定

- **BindTo 需精确匹配**：适配器的 `BindTo` 字符串必须与 Lua `data` 中的字段名完全一致。支持嵌套路径（如 `BindTo = "player.name"`）。
- **每组件一个适配器**：每个适配器包装一个 UGUI 组件。不要在同一 GameObject 上附加多个同类型适配器。
- **使用 `commands` 处理按钮动作**：命令函数接收可观察数据表作为第一个参数。对数据的修改会自动更新绑定的 UI 元素。

### 性能

- **预加载频繁使用的面板**：对经常出现的面板（背包、设置）使用 `PreloadPanel<T>()`，避免运行时 `Resources.Load`。
- **用完即毁**：临时面板（如对话框）使用 `ClosePanel<T>(destroy: true)`（默认值）。频繁切换的面板（如背包）可使用 `destroy: false`。
- **单一 LuaEnv**：所有面板共享 `UIManager` 持有的单个 `LuaEnv`。除非有特殊隔离需求，不要创建额外的 `LuaEnv` 实例。

### Lua 模块模式

**纯数据 + 命令**（大多数面板）：
```lua
return {
    data = { ... },
    commands = { ... },
}
```

**带计算属性**：
```lua
return {
    data = { firstName = '', lastName = '' },
    computed = {
        fullName = function(data)
            return data.firstName .. ' ' .. data.lastName
        end,
    },
    commands = { ... },
}
```

**多模块（应用模式）**：
```lua
return {
    name = 'myapp',
    modules = {'module1', 'module2'},
}
```
每个子模块拥有独立的命名空间。命令调用方式为 `module1.doSomething`。数据按命名空间隔离为 `data.module1.fieldName`。

### 错误处理

- **检查预制体路径**：通过 `UIManager` 加载的面板查找 `Resources/UI/Panels/{类型名}.prefab`。路径不匹配会在控制台输出错误日志。
- **验证适配器绑定**：如果 `BindTo` 路径与 Lua 数据不匹配，绑定系统会输出警告。
- **避免空 LuaEnv**：调用 `InitWithLua` 时始终传入来自 `UIManager` 的共享 `luaEnv`。

### 扩展框架

- **新面板类型**：继承 `UIPanel` 并重写 `RegisterCSharpModules()` 来向 Lua 暴露 C# 方法。
- **自定义适配器**：为自定义 UGUI 组件实现 `DataConsumer<T>`、`DataProducer<T>` 或 `EventEmitter` 接口。
- **自定义预制体路径**：重写 `PrefabPath` 属性或使用 `UIManager.Register<T>(prefabPath)` 来覆盖默认路径约定。
