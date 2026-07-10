XUUI HelloWorld 新手教程 —— Unity MVVM 从零入门

写在前面

本教程将带你从零搭建 XUUI 框架的第一个示例 —— HelloWorld。你将学会：

• 在 Unity 场景中创建 UI 并挂载适配器组件
• 编写 C# 入口脚本创建 XUUI Context
• 理解 Lua ViewModel 的 data / computed / commands 结构
• 理解整个 MVVM 数据绑定流程

建議閱讀時間：15 分鐘

----------------------------------------

一、XUUI 是什么

XUUI 是一个轻量级的 Unity MVVM 框架，使用 XLua 作为 ViewModel 层，Unity uGUI 作为 View 层。

核心思想：

View (uGUI) ↔ Adapter (C#) ↔ Binding Engine (Lua) ↔ ViewModel (Lua Table)

• View：Unity 场景中的 Text、Button 等 uGUI 组件
• Adapter：挂在 uGUI 组件上的 C# 脚本，提供统一的数据/事件接口
• Binding Engine：Lua 侧的绑定引擎，负责将 Lua 数据推送到 Adapter，或将 UI 事件传回 ViewModel
• ViewModel：纯 Lua table，包含 data（数据）、computed（计算属性）、commands（命令）

----------------------------------------

二、场景搭建

2.1 创建 GameObject 层级

在 Hierarchy 窗口中创建如下结构：

┌─ HelloWorld (GameObject)
│  └─ Canvas
│     ├─ MessageText (Text)
│     └─ ClickButton (Button)

具体操作：

1. 创建空 GameObject，命名为 HelloWorld —— 这就是入口 GameObject
2. 在 HelloWorld 下创建 Canvas
3. 在 Canvas 下创建 Text，命名为 MessageText，调整好位置和字体大小
4. 在 Canvas 下创建 Button，命名为 ClickButton，修改按钮上的 Text 为 "Click Me"

[圖片建議：此處可以放一張 Hierarchy 窗口截圖]


2.2 挂载适配器组件

给 MessageText 挂载 TextAdapter：

1. 选中 MessageText GameObject
2. 在 Inspector 中点击 Add Component
3. 搜索并添加 TextAdapter 组件
4. 在 TextAdapter 的 BindTo 字段中填入：message
5. 确认 Target 已自动指向自身的 Text 组件

给 ClickButton 挂载 ButtonAdapter：

1. 选中 ClickButton GameObject
2. 点击 Add Component → 搜索 ButtonAdapter
3. 在 ButtonAdapter 的 BindTo 字段中填入：click

[圖片建議：此處可以放一張 Inspector 截圖，展示 TextAdapter 的 BindTo 設置]


2.3 挂载 Helloworld 脚本

选中 HelloWorld（根 GameObject），点击 Add Component → 搜索并添加 Helloworld 脚本。

2.4 场景结构总览

最终 Inspector 结构如下：

HelloWorld (GameObject)
 └─ Helloworld (Script)

Canvas
 ├─ MessageText (GameObject)
 │   ├─ Text (UI.Text)
 │   └─ TextAdapter (Script)
 │      ├─ BindTo: "message"
 │      └─ Target: Text (auto)
 │
 └─ ClickButton (GameObject)
     ├─ Button (UI.Button)
     ├─ Text (UI.Text)
     └─ ButtonAdapter (Script)
        ├─ BindTo: "click"
        └─ Target: Button (auto)

----------------------------------------

三、代码解析

3.1 Helloworld.cs

以下为完整的 C# 代码：

【代码块开始】
using UnityEngine;
using XUUI;

public class Helloworld : MonoBehaviour
{
    Context context = null;

    void Start()
    {
        context = new Context(@"
            return {
                data = {
                    info = {
                        name = 'John',
                    },
                },
                computed = {
                    message = function(data)
                        return 'Hello ' .. data.info.name .. '!'
                    end
                },
                commands = {
                    click = function(data)
                        print(data.info.name)
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
【代码块结束】

逐行解释：

Context context = null;
→ 声明一个 Context 引用，它是 XUUI 的核心管理器

context = new Context( ... Lua 脚本 ... )
→ 传入一段 Lua 脚本字符串。构造函数会：1）初始化 XLua 环境；2）编译执行脚本返回 Lua table；3）调用 xuui.new(options) 创建 ViewModel

context.Attach(gameObject)
→ 将当前 GameObject（及其子物体）绑定到 ViewModel。这一步触发 Binding Engine 扫描适配器、注册数据监听和命令绑定

context.Dispose()
→ 在对象销毁时清理所有 Lua 引用和事件监听，防止内存泄漏


3.2 Lua ViewModel 结构

Lua 脚本返回的 table 是 ViewModel 的定义，包含三个核心部分：

（1）data —— 数据模型（Model）

【代码块开始】
data = {
    info = {
        name = 'John',
    },
}
【代码块结束】

• 这是 MVVM 中的 Model 层
• 数据被 observeable.lua 用 metatable 包装成响应式数据
• 当 data.info.name 的值改变时，所有依赖它的 UI 会自动更新

（2）computed —— 计算属性

【代码块开始】
computed = {
    message = function(data)
        return 'Hello ' .. data.info.name .. '!'
    end,
}
【代码块结束】

• 这是一个纯函数，接收 data 作为参数，返回计算后的值
• TextAdapter.BindTo = "message" 告诉引擎：这个 Text 显示 computed.message 的结果
• 自动依赖追踪：引擎在执行 message(data) 时，会记录它访问了 data.info.name，并自动注册监听。当 name 变化时，message 重新计算，UI 自动刷新

（3）commands —— 命令

【代码块开始】
commands = {
    click = function(data)
        print(data.info.name)
    end,
}
【代码块结束】

• ButtonAdapter.BindTo = "click" 告诉引擎：这个按钮触发 commands.click
• 点击按钮时，Binding Engine 调用 click(data)，打印 "John"

----------------------------------------

四、运行流程

程序启动时的完整调用链：

Helloworld.Start()
 └─ new Context(luaScript)
    ├─ LuaEnv.LoadString(script)
    │   → 编译 Lua 返回 table
    └─ xuui.new(options)
        → 包装响应式数据，返回 attach 函数

 └─ context.Attach(gameObject)
    └─ binding.bind(data, observe, computed, commands, root)
       ├─ Collector.Collect(root)
       │   → 扫描子物体，收集所有 Adapter
       ├─ listen_to()
       │   → 为每个 DataConsumer（TextAdapter）注册数据监听
       │   ├─ 发现 BindTo="message" 在 computed 中
       │   ├─ 执行 message(data) → 依赖追踪 → 注册 watcher
       │   └─ 设置 TextAdapter.Value = "Hello John!"
       ├─ watch_to()
       │   → 为每个 DataProducer 绑定"UI → data"监听
       └─ bind_action()
           → 为每个 EventEmitter（ButtonAdapter）绑定命令
           └─ button.OnAction = function()
                  commands.click(data)
              end

运行后效果：
• 屏幕上显示 "Hello John!"
• 点击按钮，Console 打印 "John"

----------------------------------------

五、绑定关系总结

┌─────────────┬───────────────┬────────┬──────────────────┬──────────┐
│  GameObject  │    适配器     │ BindTo │  ViewModel 映射   │   方向   │
├─────────────┼───────────────┼────────┼──────────────────┼──────────┤
│ MessageText │ TextAdapter   │ message│ computed.message  │ VM→View  │
│ ClickButton │ ButtonAdapter │ click  │ commands.click    │ View→VM  │
└─────────────┴───────────────┴────────┴──────────────────┴──────────┘

----------------------------------------

六、动手实验

理解了基本流程后，可以尝试以下修改来加深理解：

6.1 修改显示的数据

将 name = 'John' 改为 name = 'World'，运行后 Text 变为 "Hello World!"

6.2 新增一个显示字段

在 data 中添加 age = 25：

【代码块开始】
data = {
    info = {
        name = 'John',
        age = 25,      -- 新增
    },
},
【代码块结束】

添加一个 computed：

【代码块开始】
ageInfo = function(data)
    return data.info.name .. ' is '
           .. data.info.age .. ' years old'
end,
【代码块结束】

在场景中新建一个 Text，挂载 TextAdapter，BindTo 设为 "ageInfo"。

6.3 修改命令行为

把 click 命令改为修改数据：

【代码块开始】
click = function(data)
    data.info.name = 'World'  -- 改数据
end,
【代码块结束】

点击按钮后，Text 会自动更新为 "Hello World!" —— 这就是 MVVM 的威力，修改 Model 自动驱动 View 更新。

6.4 使用 InputField 实现双向绑定

在 Canvas 下创建一个 InputField，挂载 InputFieldAdapter，BindTo 设为 "info.name"。运行时在输入框中打字，Text 的文字会随之实时变化。因为：

1. InputFieldAdapter 是 DataProducer<string> — 用户输入触发 OnValueChange
2. 引擎将输入值写回 data.info.name
3. 响应式系统检测到 name 变化，触发 computed.message 重新计算
4. TextAdapter 的 Value 被更新，UI 刷新

[圖片建議：此處可以放一張運行效果的 GIF 或截圖]

----------------------------------------

七、常见问题

Q：BindTo 的值如何对应到 Lua data？

A：BindTo 使用点号分隔路径。例如：

┌──────────────┬────────────────────────────┐
│   BindTo     │        Lua 路径            │
├──────────────┼────────────────────────────┤
│ "message"    │ computed.message           │
│ "click"      │ commands.click             │
│ "info.name"  │ data.info.name             │
│ "module1.clk"│ commands["module1.click"]  │
└──────────────┴────────────────────────────┘

Q：为什么 TextAdapter 的 BindTo 是 "message" 却显示 computed 的结果？

A：Binding Engine 的 listen_to 函数会先检查 BindTo 是否在 computed 表中。如果在，就执行计算属性函数而不是直接读取 data。如果在 computed 和 commands 中都找不到，则直接从 data 中按路径读取。

Q：适配器只有 Text 和 Button 吗？

A：XUUI 还内置了 InputFieldAdapter（双向绑定）、DropdownAdapter 等。你也可以自己继承 AdapterBase<T> 创建自定义适配器。

----------------------------------------

八、完整文件清单

Assets/
├─ Scenes/
│  └─ Helloworld.unity
├─ Scripts/
│  └─ Helloworld.cs
└─ XUUI/
   ├─ Scripts/
   │  ├─ ViewModel.cs
   │  ├─ AdapterBase.cs
   │  ├─ DataConsumer.cs
   │  ├─ DataProducer.cs
   │  ├─ EventEmitter.cs
   │  └─ UGUIAdapter/
   │     ├─ TextAdapter.cs
   │     ├─ ButtonAdapter.cs
   │     ├─ InputFieldAdapter.cs
   │     ├─ DropdownAdapter.cs
   │     ├─ ViewBinding.cs
   │     └─ Collector.cs
   └─ Resources/
      ├─ xuui.lua.txt
      ├─ binding.lua.txt
      ├─ observeable.lua.txt
      ├─ collector.lua.txt
      └─ xuui_utils.lua.txt

----------------------------------------

写在最后

这是 XUUI 框架的第一个入门教程，后续还会带来：

• UIManager 面板管理与注册登录实战
• 多模块 App 模式详解
• 自定义适配器与高级绑定
• Lua 热重载与调试技巧

如果本文对你有帮助，欢迎点赞、在看、转发，让更多朋友少走弯路。

有任何问题欢迎在评论区留言交流！
