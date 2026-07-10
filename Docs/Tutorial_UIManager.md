# XUUI MVVM UI Framework Tutorial

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Core Classes](#2-core-classes)
   - [UIBase](#21-uibase)
   - [UIPanel](#22-uipanel)
   - [UIView](#23-uiview)
   - [UIManager](#24-uimanager)
   - [Context](#25-context)
3. [Data Binding System](#3-data-binding-system)
   - [Adapters](#31-adapters)
   - [Lua Data Binding](#32-lua-data-binding)
4. [Creating a Panel](#4-creating-a-panel)
5. [Creating a View](#5-creating-a-view)
6. [Case Study: Login & Registration](#6-case-study-login--registration)
   - [Login Scene (Simple Binding)](#61-login-scene-simple-binding)
   - [RegisterPanel (Managed Panel)](#62-registerpanel-managed-panel)
   - [End-to-End Flow](#63-end-to-end-flow)
7. [Lifecycle Management](#7-lifecycle-management)
8. [Best Practices](#8-best-practices)

---

## 1. Architecture Overview

The XUUI MVVM framework provides a **panel-view** hierarchy with **Lua-driven data binding**. The architecture follows a layered design:

```
┌─────────────────────────────────────────────────────────┐
│                      UIManager                           │
│              (Singleton, owns LuaEnv)                     │
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

### Key Concepts

- **Panel** (`UIPanel`): A top-level UI container (e.g., login window, main menu). Loaded and managed by `UIManager`. Each panel has its own Lua Context and can contain multiple child Views.
- **View** (`UIView`): A reusable sub-component within a Panel (e.g., a character selection list, a loading spinner). Has its own Lua Context but shares the LuaEnv from the panel.
- **UIManager**: Singleton that owns the shared `LuaEnv` (Lua virtual machine), manages panel lifecycle (open/close/preload), and acts as the central registry.
- **Context**: The MVVM bridge. Compiles Lua configuration tables (data, commands, computed properties), creates observable data, and binds them to Unity UI elements via adapters.

### Directory Structure

```
Assets/
├── Resources/
│   └── UI/
│       ├── Panels/           # Panel prefabs + Lua scripts
│       │   ├── RegisterPanel.prefab
│       │   └── RegisterPanel.lua.txt
│       └── Views/            # View prefabs + Lua scripts
│
├── Scripts/
│   └── UI/
│       ├── Core/             # Framework core
│       │   ├── UIBase.cs
│       │   ├── UIPanel.cs
│       │   ├── UIView.cs
│       │   └── UIManager.cs
│       └── Panels/           # Panel C# implementations
│           └── RegisterPanel.cs
│
├── XUUI/
│   └── Scripts/
│       ├── ViewModel.cs          # Context class
│       ├── AdapterBase.cs        # Base adapter
│       ├── UGUIAdapter/          # UGUI-specific adapters
│       │   ├── TextAdapter.cs
│       │   ├── InputFieldAdapter.cs
│       │   ├── ButtonAdapter.cs
│       │   └── ...
│       └── Editor/               # XLua gen config
│
└── XUUI/Resources/
    └── xuui.lua.txt              # XUUI Lua engine
    └── observeable.lua.txt       # Observable data system
    └── binding.lua.txt           # C# ↔ Lua binding layer
```

---

## 2. Core Classes

### 2.1 UIBase

**File**: `Assets/Scripts/UI/Core/UIBase.cs`

The abstract root class for all UI elements. Provides lifecycle state and basic GameObject management.

```csharp
public abstract class UIBase
{
    public bool IsOpen { get; protected set; }
    public bool IsLoaded { get; protected set; }
    public GameObject GameObject { get; protected set; }
    public Transform Transform { get; protected set; }
    public string Name { get; protected set; }

    public virtual void Init(GameObject go);       // Initialize with GameObject
    public virtual void Open(params object[] args); // Activate (SetActive(true))
    public virtual void Close();                    // Deactivate (SetActive(false))
    public virtual void Dispose();                  // Close + Destroy GameObject
}
```

### 2.2 UIPanel

**File**: `Assets/Scripts/UI/Core/UIPanel.cs`

A panel is a top-level UI container that owns a Lua Context and can manage child Views.

```csharp
public class UIPanel : UIBase
{
    protected Context context;
    protected LuaEnv luaEnv;
    protected Dictionary<string, UIView> views;
    protected Transform viewContainer;          // "Views" child or self

    protected virtual string PrefabPath => "UI/Panels/" + GetType().Name;
    protected virtual string LuaModulePath => "UI.Panels." + GetType().Name;

    public void InitWithLua(GameObject go, LuaEnv luaEnv);

    // View management
    public T OpenView<T>(params object[] args) where T : UIView, new();
    public void CloseView(string viewName);
    public void CloseAllViews();
    public T GetView<T>() where T : UIView;

    // Lifecycle
    public override void Close();    // Close all views + self
    public override void Dispose();  // Dispose views → Context → LuaEnv cleanup
}
```

#### Convention

- Prefab is loaded from `Resources/UI/Panels/{TypeName}`
- Lua module is required from `UI.Panels.{TypeName}`
- If the panel GameObject has a child named `"Views"`, new Views are parented there; otherwise parented to the panel root

### 2.3 UIView

**File**: `Assets/Scripts/UI/Core/UIView.cs`

A lightweight sub-component within a panel. It has its own Lua Context but shares the LuaEnv.

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

Use `OpenView<T>()` on a `UIPanel` to instantiate and open a View.

### 2.4 UIManager

**File**: `Assets/Scripts/UI/Core/UIManager.cs`

Singleton that owns the shared LuaEnv and manages panel lifecycle.

```csharp
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; }
    public Transform PanelRoot { get; }  // Canvas or auto-created root

    // Registration (optional, for pre-declaring panels)
    public void Register<T>() where T : UIPanel, new();
    public void Register<T>(string prefabPath) where T : UIPanel, new();

    // Panel operations
    public T OpenPanel<T>(params object[] args) where T : UIPanel, new();
    public void ClosePanel<T>(bool destroy = true);
    public T GetPanel<T>() where T : UIPanel;
    public bool IsPanelOpen<T>() where T : UIPanel;
    public void PreloadPanel<T>() where T : UIPanel, new();
}
```

#### OpenPanel Flow

```
UIManager.OpenPanel<T>()
  ├── Check if panel already active → if open, return it; if closed, call Open()
  ├── Resolve prefab path (from override or default convention)
  ├── Resources.Load<GameObject>(path)
  ├── Instantiate under PanelRoot
  ├── new T() → panel.InitWithLua(go, luaEnv)
  │     ├── base.Init(go)
  │     ├── Locate "Views" container
  │     ├── require Lua module → new Context(script, luaEnv)
  │     ├── RegisterCSharpModules()  [virtual hook]
  │     └── context.Attach(go)       [bind data to UI]
  └── panel.Open(args)
```

### 2.5 Context

**File**: `Assets/XUUI/Scripts/ViewModel.cs`

The Context is the MVVM bridge between C# and Lua.

```csharp
public class Context : IDisposable
{
    public LuaTable options;

    // Constructors
    public Context(string script, LuaEnv luaEnv);  // Compile Lua, create from table
    public Context(LuaEnv luaEnv);                   // Empty context, add data later

    // C# → Lua module registration
    public void AddCSharpModule(string name, object obj);

    // Binding
    public Action Attach(GameObject go);   // Returns a detach function
    public void Detach(GameObject go);

    // Hot-reload
    public void ReloadModule(string name, bool reloadData);

    public void Dispose();
}
```

The Lua-side configuration table returned by `require`:

```lua
return {
    -- Observable data: fields become bindable to UI adapters
    data = {
        username = '',
        score = 0,
        items = {},
    },

    -- Commands: functions callable from UI (e.g., button clicks)
    commands = {
        login = function(data)
            -- "data" is the observable data table
        end,
    },

    -- Computed properties: functions that derive values from data
    computed = {
        fullName = function(data)
            return data.firstName .. ' ' .. data.lastName
        end,
    },
}
```

---

## 3. Data Binding System

### 3.1 Adapters

Adapters bridge between UGUI components and Lua observable data. Each adapter has a `BindTo` string field that maps to a path in the Lua `data` table.

| Adapter | UGUI Component | Interface | Direction |
|---|---|---|---|
| `TextAdapter` | `Text` | `DataConsumer<string>` | Lua → UI (read-only) |
| `InputFieldAdapter` | `InputField` | `DataConsumer<string>` + `DataProducer<string>` | Bidirectional |
| `ButtonAdapter` | `Button` | `EventEmitter` | UI → Lua (command) |
| `DropdownAdapter` | `Dropdown` | `DataConsumer<int>` + `DataProducer<int>` | Bidirectional |
| `DropdownOptionsAdapter` | `Dropdown` | `DataConsumer<LuaTable>` | Lua → UI (options) |

**Raw variants** (e.g., `RawTextAdapter`, `RawButtonAdapter`) are non-MonoBehaviour adapters used within the `ViewBinding` system for dynamic binding at runtime.

### 3.2 Lua Data Binding

When `context.Attach(gameObject)` is called, the framework:

1. Scans the GameObject and its children for adapters (via `Collector.Collect()`)
2. For each `DataConsumer` — subscribes to Lua observable changes (data → UI)
3. For each `DataProducer` — watches for UI changes and updates Lua (UI → data)
4. For each `EventEmitter` — binds button clicks to Lua command functions

```
┌─────────┐    data.username     ┌──────────────┐
│  Lua    │ ◄──────────────────► │ InputField    │
│  data   │                      │ (username)    │
│         │    data.password     └──────────────┘
│         │ ◄──────────────────► ┌──────────────┐
│         │                      │ InputField    │
│         │                      │ (password)    │
│         │    data.message      └──────────────┘
│         │ ──────────────────►  ┌──────────────┐
│         │                      │ Text          │
│         │                      │ (message)     │
│ commands│                      └──────────────┘
│         │                      ┌──────────────┐
│  login  │ ◄──────────────────  │ Button        │
│         │    (onClick)         │ (login)       │
└─────────┘                      └──────────────┘
```

#### Setting Up Binding in the Prefab

Add adapter components to each UGUI element in the prefab through the Inspector:

1. Select the UGUI element (e.g., `username_InputField`)
2. Add `InputFieldAdapter` component
3. Set `BindTo` = `"username"` (must match the field name in Lua `data`)
4. For buttons, add `ButtonAdapter` and set `BindTo` = `"login"` (must match the command name)

---

## 4. Creating a Panel

A panel requires three parts: a C# class, a prefab, and a Lua module.

### Step 1: Create the C# Class

```csharp
// Assets/Scripts/UI/Panels/MyPanel.cs
using XUUI.UI;

public class MyPanel : UIPanel
{
    // Optional: Override prefab path convention
    // protected override string PrefabPath => "UI/Panels/MyCustomPath";

    // Optional: Override Lua module path convention
    // protected override string LuaModulePath => "UI.Panels.MyCustomModule";

    // Optional: Register C# modules to make them callable from Lua
    protected override void RegisterCSharpModules()
    {
        // context.AddCSharpModule("myModule", this);
    }

    // Convenience open/close helpers (for calling from Lua)
    public static void OpenSelf() { UIManager.Instance.OpenPanel<MyPanel>(); }
    public static void CloseSelf() { UIManager.Instance.ClosePanel<MyPanel>(); }
}
```

### Step 2: Create the Lua Module

```lua
-- Assets/Resources/UI/Panels/MyPanel.lua.txt
return {
    data = {
        -- Declare all bindable data fields here
        title = 'Welcome',
        counter = 0,
    },
    commands = {
        onClick = function(data)
            data.counter = data.counter + 1
            print('Clicked! Counter:', data.counter)
        end,
    },
    computed = {
        displayText = function(data)
            return data.title .. ' (count: ' .. data.counter .. ')'
        end,
    },
}
```

### Step 3: Create the Prefab

1. Create a UI GameObject (e.g., Panel) in the scene
2. Add adapter components and set `BindTo` fields
3. Save as prefab to `Assets/Resources/UI/Panels/MyPanel.prefab`

### Step 4: Open the Panel

```csharp
// From any C# code:
UIManager.Instance.OpenPanel<MyPanel>();

// Or from Lua:
CS.XUUI.UI.MyPanel.OpenSelf()
```

---

## 5. Creating a View

Views are sub-components managed within a panel.

### Step 1: Create the C# Class

```csharp
// Assets/Scripts/UI/Views/CharacterView.cs
using XUUI.UI;

public class CharacterView : UIView
{
    // Custom logic specific to this view
    protected override void RegisterCSharpModules()
    {
        // Register C# methods for Lua callbacks
    }
}
```

### Step 2: Create the Lua Module

```lua
-- Assets/Resources/UI/Views/CharacterView.lua.txt
return {
    data = {
        selectedIndex = 1,
        characters = {},
    },
    commands = {
        select = function(data)
            -- Handle character selection
        end,
    },
}
```

### Step 3: Create the Prefab

Save to `Assets/Resources/UI/Views/CharacterView.prefab`.

### Step 4: Open from a Panel

```csharp
// Inside a UIPanel subclass:
var view = OpenView<CharacterView>("optional_arg");
```

The view is parented to the panel's `"Views"` child transform (or the panel root if no `"Views"` child exists).

---

## 6. Case Study: Login & Registration

This case study demonstrates a complete login flow with two implementations: a simple Login scene with inline Lua, and a managed RegisterPanel with its own prefab and Lua module.

### 6.1 Login Scene (Simple Binding)

**Scene**: `Assets/Scenes/Login.unity`

A lightweight MonoBehaviour with inline Lua configuration. Good for simple, standalone screens.

#### C# Code (`Login.cs`)

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
                    message = 'Please login'
                },
                commands = {
                    login = function(data)
                        if data.username == 'admin' and data.password == '123456' then
                            data.message = 'Login successful!'
                        else
                            data.message = 'Invalid username or password!'
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

#### Prefab Setup

Attach `Login.cs` to the root GameObject. Add UGUI components with adapters:

| UI Element | Adapter | BindTo |
|---|---|---|
| Username InputField | `InputFieldAdapter` | `username` |
| Password InputField | `InputFieldAdapter` | `password` |
| Message Text | `TextAdapter` | `message` |
| Login Button | `ButtonAdapter` | `login` |

#### Flow

1. User enters username/password
2. Clicks Login → button fires `commands.login(data)` in Lua
3. Lua validates credentials
4. On failure: shows error and opens RegisterPanel (via `CS.XUUI.UI.RegisterPanel.OpenSelf()`)
5. On success: shows success message

### 6.2 RegisterPanel (Managed Panel)

A proper panel managed by UIManager, with separate prefab and Lua module.

#### C# Code (`RegisterPanel.cs`)

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

#### Lua Module (`RegisterPanel.lua.txt`)

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
                data.message = 'Please fill in all fields!'
            elseif #data.password < 6 then
                data.message = 'Password must be at least 6 characters!'
            else
                data.message = 'Registration successful! You can now login.'
                CS.XUUI.UI.RegisterPanel.CloseSelf()
                -- Clear Lua module cache so next open gets fresh state
                package.loaded['UI.Panels.RegisterPanel'] = nil
            end
        end,
    }
}
```

#### Prefab Setup (`RegisterPanel.prefab`)

Create a UI panel prefab at `Assets/Resources/UI/Panels/RegisterPanel.prefab`:

| UI Element | Adapter | BindTo |
|---|---|---|
| Username InputField | `InputFieldAdapter` | `username` |
| Password InputField | `InputFieldAdapter` | `password` |
| Message Text | `TextAdapter` | `message` |
| Register Button | `ButtonAdapter` | `register` |

#### Opening RegisterPanel from C#

```csharp
// From any C# code:
UIManager.Instance.OpenPanel<RegisterPanel>();

// Or with arguments:
// UIManager.Instance.OpenPanel<RegisterPanel>("arg1", 42);
```

#### Opening RegisterPanel from Lua

```lua
-- In any Lua module (e.g., the Login scene's Lua):
CS.XUUI.UI.RegisterPanel.OpenSelf()
```

### 6.3 End-to-End Flow

```
User clicks Login (wrong credentials)
  │
  ▼
Login Lua: data.message = "Invalid credentials"
Login Lua: CS.XUUI.UI.RegisterPanel.OpenSelf()
  │
  ▼
UIManager.OpenPanel<RegisterPanel>()
  ├── Loads Resources/UI/Panels/RegisterPanel.prefab
  ├── Instantiate under canvas
  ├── new RegisterPanel()
  │     └── InitWithLua → require 'UI.Panels.RegisterPanel'
  │           ├── Lua returns {data, commands}
  │           ├── xuui.new() creates observable data
  │           └── context.Attach(go) → bind adapters
  └── panel.Open() → SetActive(true)

User fills fields, clicks Register
  │
  ▼
Register Lua: validates fields
  ├── Empty fields → data.message = "Please fill in all fields!"
  └── Success → data.message = "Registration successful!"
        CS.XUUI.UI.RegisterPanel.CloseSelf()
        package.loaded['UI.Panels.RegisterPanel'] = nil
          │
          ▼
        UIManager.ClosePanel<RegisterPanel>(destroy: true)
          └── panel.Dispose()
                ├── CloseAllViews()
                ├── context.Dispose()
                ├── Evict Lua module from package.loaded
                ├── Destroy GameObject
                └── Remove from activePanels
```

### Dispose Detail

When a panel is disposed, the `UIPanel.Dispose()` method performs cleanup in order:

```csharp
// UIPanel.Dispose() pseudo-flow:
1. Close and dispose all child UIView instances
2. Clear views dictionary
3. Dispose Context (unbinds all adapters, releases Lua references)
4. Evict Lua module from package.loaded for fresh state next time
5. Call base.Dispose() → Close() + Destroy(GameObject)
```

The `UIManager.OnDestroy()` handles global cleanup:

```csharp
// UIManager.OnDestroy() pseudo-flow:
1. Dispose all panels in reverse order
2. Force GC.Collect() + GC.WaitForPendingFinalizers()
3. luaEnv.Dispose()  ← destroys the shared Lua VM
```

---

## 7. Lifecycle Management

### State Diagram

```
         ┌──────────────────────────────────┐
         │         UIBase (abstract)         │
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
                    │ Open() again possible
                    │  ... or Dispose()
                    ▼
         ┌──────────────────────────────────┐
         │         IsLoaded = false          │
         │         IsOpen = false            │
         │         GO Destroyed              │
         └──────────────────────────────────┘
```

### Panel Reuse

When `ClosePanel<T>(destroy: false)` is called, the panel is deactivated but kept in memory. A subsequent `OpenPanel<T>()` reuses the existing instance and calls `Open()` on it.

When `ClosePanel<T>(destroy: true)` (default), the panel is fully disposed and destroyed.

### Preloading

`PreloadPanel<T>()` instantiates the prefab and initializes Lua without activating the GameObject. This is useful for panels that need to appear immediately on demand.

```csharp
// Preload during loading screen
UIManager.Instance.PreloadPanel<InventoryPanel>();

// Later — instant open (no Resources.Load overhead)
UIManager.Instance.OpenPanel<InventoryPanel>();
```

---

## 8. Best Practices

### Panel Design

- **One Lua module per panel**: Each `UIPanel` or `UIView` should have its own Lua module for data, commands, and computed properties.
- **Use `CloseSelf()` from Lua**: Panels that can close themselves (e.g., confirmation dialogs, registration success) should expose static `OpenSelf()` / `CloseSelf()` helpers for Lua callability.
- **Clear module cache on close**: After a panel is disposed, set `package.loaded[modulePath] = nil` so the next open gets a fresh Lua state. This is handled automatically in `UIPanel.Dispose()`.

### Data Binding

- **Match `BindTo` exactly**: The adapter's `BindTo` string must match the Lua `data` field name. Nested paths are supported (e.g., `BindTo = "player.name"`).
- **One adapter per component**: Each adapter wraps a single UGUI component. Do not attach multiple adapters of the same type to one GameObject.
- **Use `commands` for button actions**: Commands receive the observable data table as their first parameter. Mutations to data automatically update bound UI elements.

### Performance

- **Preload frequently used panels**: Use `PreloadPanel<T>()` for panels that appear often (inventory, settings) to avoid runtime `Resources.Load`.
- **Destroy panels when done**: Use `ClosePanel<T>(destroy: true)` (the default) for temporary panels like dialogs. Panels that are frequently toggled (e.g., inventory) can use `destroy: false`.
- **One LuaEnv**: All panels share the single `LuaEnv` owned by `UIManager`. Never create additional `LuaEnv` instances unless isolating a specific subsystem.

### Lua Module Patterns

**Simple data + commands** (most panels):
```lua
return {
    data = { ... },
    commands = { ... },
}
```

**With computed properties**:
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

**Multi-module (app pattern)**:
```lua
return {
    name = 'myapp',
    modules = {'module1', 'module2'},
}
```
Each sub-module gets its own namespace. Commands are called as `module1.doSomething`. Data is namespaced as `data.module1.fieldName`.

### Error Handling

- Check prefab paths: Panels loaded via `UIManager` look for `Resources/UI/Panels/{TypeName}.prefab`. Mismatches produce clear error logs.
- Validate adapter binding: If a `BindTo` path doesn't match Lua data, the binding system logs a warning.
- Avoid null LuaEnv: Always pass the shared `luaEnv` from `UIManager` when calling `InitWithLua`.

### Extending the Framework

- **New panel types**: Subclass `UIPanel` and override `RegisterCSharpModules()` to expose C# methods to Lua.
- **Custom adapters**: Implement `DataConsumer<T>`, `DataProducer<T>`, or `EventEmitter` interfaces for custom UGUI components.
- **Custom prefab paths**: Override `PrefabPath` or use `UIManager.Register<T>(prefabPath)` for non-convention layouts.
