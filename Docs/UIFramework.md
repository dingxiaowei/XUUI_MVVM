# UI 框架

## 概述

UI 框架提供了一套层级化的 UI 管理系统，基于 XUUI MVVM 框架构建。架构分为四个层级：

```
UIManager (单例)
  └── UIPanel (一级UI，由UIManager管理)
        └── UIView (二级UI，由UIPanel管理)
```

所有 UI 组件均继承自 `UIBase`，提供统一的 `Init` / `Open` / `Close` / `Dispose` 生命周期。

---

## UIBase

所有 UI 组件的抽象基类。

```csharp
public abstract class UIBase
{
    // 属性
    public bool IsOpen { get; }        // 是否已打开
    public bool IsLoaded { get; }      // 是否已加载
    public GameObject GameObject { get; }
    public Transform Transform { get; }
    public string Name { get; }

    // 生命周期方法
    public virtual void Init(GameObject go);           // 初始化：接收实例化后的GameObject
    public virtual void Open(params object[] args);    // 打开/显示
    public virtual void Close();                       // 关闭/隐藏（不销毁）
    public virtual void Dispose();                     // 销毁：关闭并释放所有资源
}
```

调用顺序：`Init` → `Open` → `Close` → `Open` ... → `Dispose`

---

## UIManager

UIManager 是全局单例 MonoBehaviour，管理所有一级 UIPanel。

### 基本用法

```csharp
// 初始化（自动创建单例）
UIManager.Instance

// 注册Panel（使用默认路径约定）
UIManager.Instance.Register<LoginPanel>();

// 打开Panel
UIManager.Instance.OpenPanel<LoginPanel>();

// 关闭Panel（销毁）
UIManager.Instance.ClosePanel<LoginPanel>();

// 关闭Panel（保留，下次打开复用）
UIManager.Instance.ClosePanel<LoginPanel>(destroy: false);

// 获取Panel引用
var panel = UIManager.Instance.GetPanel<LoginPanel>();

// 预加载Panel（后台加载但不显示）
UIManager.Instance.PreloadPanel<LoginPanel>();
```

### 路径约定

Panel 的 Prefab 和 Lua 模块路径根据类名自动生成：

| 项目 | 路径 |
|------|------|
| C# 类 | `Assets/Scripts/UI/Panels/LoginPanel.cs` |
| Prefab | `Assets/Resources/UI/Panels/LoginPanel.prefab` |
| Lua 模块 | `Assets/Resources/UI/Panels/LoginPanel.lua.txt` |
| require 路径 | `UI.Panels.LoginPanel` |

通过 `Register<T>(prefabPath)` 可自定义 Prefab 路径。

### XLua 生命周期管理

UIManager 持有全局唯一的 `LuaEnv`，在 `OnDestroy` 时按正确顺序释放：

1. 逐个 Dispose 所有 Panel（级联释放所有 View 和 Context）
2. `GC.Collect()` + `GC.WaitForPendingFinalizers()`
3. `luaEnv.Dispose()`

这确保了 XLua 的 delegate bridge 在 LuaEnv 销毁前被正确清理，避免 `"try to dispose a LuaEnv with C# callback!"` 错误。

---

## UIPanel

UIPanel 继承 UIBase，代表一级 UI 容器。每个 Panel 拥有独立的 XUUI Context 和 Lua 模块，可以动态加载/卸载二级 UIView。

### 创建 Panel

```csharp
public class LoginPanel : UIPanel
{
    protected override void RegisterCSharpModules()
    {
        // 将C#方法注册为Lua可调用的命令
        context.AddCSharpModule("LoginPanel", this);
    }

    [Command]
    public void ShowRegisterForm()
    {
        OpenView<RegisterFormView>();
    }
}
```

### Lua 模块

```lua
-- Assets/Resources/UI/Panels/LoginPanel.lua.txt
return {
    data = {
        title = "Login System",
        isBusy = false,
    },
    commands = {
        showRegister = function(data)
            CS.LoginPanel.ShowRegisterForm()
        end,
    },
}
```

### Prefab 结构

Panel 的 Prefab 应包含 `ViewBinding` 组件绑定 UI 元素到 Lua data/commands。可选创建一个名为 `Views` 的子节点作为 View 容器：

```
LoginPanel (Root)
  ├── ViewBinding 组件
  ├── Background
  ├── Title (Text)
  └── Views      ← 二级UIView会挂载在此节点下
```

### 管理 UIView

```csharp
// 打开/创建 View
var view = panel.OpenView<LoginFormView>();

// 关闭 View（保留）
panel.CloseView("LoginFormView");

// 关闭所有 View
panel.CloseAllViews();

// 获取已打开的 View
var view = panel.GetView<LoginFormView>();
```

---

## UIView

UIView 继承 UIBase，代表二级 UI 组件，由 UIPanel 管理。

### 创建 View

```csharp
public class LoginFormView : UIView
{
    // 可选的C#逻辑
}
```

### Lua 模块

```lua
-- Assets/Resources/UI/Views/LoginFormView.lua.txt
return {
    data = {
        username = "",
        password = "",
        message = "",
    },
    commands = {
        login = function(data)
            if data.username == "admin" and data.password == "123456" then
                data.message = "Login successful!"
                CS.LoginPanel.OnLoginSuccess()
            else
                data.message = "Invalid credentials!"
            end
        end,
    },
}
```

### 路径约定

| 项目 | 路径 |
|------|------|
| C# 类 | `Assets/Scripts/UI/Views/LoginFormView.cs` |
| Prefab | `Assets/Resources/UI/Views/LoginFormView.prefab` |
| Lua 模块 | `Assets/Resources/UI/Views/LoginFormView.lua.txt` |
| require 路径 | `UI.Views.LoginFormView` |

---

## 生命周期详解

### Panel 打开流程

```
UIManager.OpenPanel<LoginPanel>()
  ├─ (1) Resources.Load("UI/Panels/LoginPanel")  加载Prefab
  ├─ (2) Instantiate 到 panelRoot 下
  ├─ (3) panel.InitWithLua(go, luaEnv)
  │     ├─ base.Init(go)                          设置GameObject/Transform
  │     ├─ new Context("require 'UI.Panels.LoginPanel'", luaEnv)
  │     ├─ RegisterCSharpModules()                注册C# Command/Export
  │     └─ context.Attach(go)                     绑定UI到数据
  └─ (4) panel.Open(args)                         显示Panel
```

### View 打开流程

```
panel.OpenView<LoginFormView>()
  ├─ (1) Load Prefab: Resources.Load("UI/Views/LoginFormView")
  ├─ (2) Instantiate 到 viewContainer 下
  ├─ (3) view.InitWithLua(go, luaEnv, "UI.Views.LoginFormView")
  │     ├─ base.Init(go)
  │     ├─ new Context("require 'UI.Views.LoginFormView'", luaEnv)
  │     └─ context.Attach(go)
  ├─ (4) view.Open(args)
  └─ (5) views["LoginFormView"] = view
```

### Panel 销毁流程

```
UIManager.ClosePanel<LoginPanel>(destroy: true)
  └─ panel.Dispose()
        ├─ CloseAllViews()              关闭所有View
        ├─ 遍历 views: view.Dispose()
        │     ├─ context.Dispose()       清除Lua引用（不销毁共享LuaEnv）
        │     └─ Destroy(GameObject)
        ├─ views.Clear()
        ├─ context.Dispose()             清除Panel的Lua引用
        └─ base.Dispose()                销毁Panel GameObject
```

### 应用关闭流程

```
UIManager.OnDestroy()
  ├─ 遍历所有Panel: panel.Dispose()
  │     └─ 级联销毁所有子View和Context
  ├─ activePanels.Clear()
  ├─ GC.Collect()
  ├─ GC.WaitForPendingFinalizers()
  ├─ luaEnv.Dispose()
  └─ _instance = null
```

---

## 完整示例

### 启动场景

```csharp
public class Bootstrap : MonoBehaviour
{
    void Start()
    {
        UIManager.Instance.OpenPanel<LoginPanel>();
    }
}
```

### LoginPanel.cs

```csharp
public class LoginPanel : UIPanel
{
    protected override void RegisterCSharpModules()
    {
        context.AddCSharpModule("LoginPanel", this);
    }

    [Command]
    public void ShowRegisterForm()
    {
        OpenView<RegisterFormView>();
    }

    [Command]
    public void OnLoginSuccess()
    {
        // 关闭所有子View，导航到主菜单等
        CloseAllViews();
    }
}
```

### RegisterFormView.cs

```csharp
public class RegisterFormView : UIView
{
}
```

---

## 与 XLua 集成说明

1. **共享 LuaEnv**：所有 Panel 和 View 共享 UIManager 持有的根 LuaEnv，避免创建多个 Lua VM
2. **资源加载**：Lua 模块通过 `require('UI.Panels.XXX')` 加载，XLua 的 `LoadFromResource` searcher 将 `.` 转为 `/`，自动查找 `.lua.txt` 文件
3. **ViewBinding**：每个 Prefab 上需要添加 `ViewBinding` 组件来声明 UI 组件与 Lua data/commands 的绑定关系
4. **内存安全**：UIManager.OnDestroy 中严格按照 `Dispose Context → GC.Collect → LuaEnv.Dispose` 顺序释放资源
