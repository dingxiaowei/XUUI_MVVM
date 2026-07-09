# Tutorial_MoreComplex — 下拉框与 Lua 表格操作

## 与 HelloWorld 最大的不同：Dropdown 的 options 数据源

MoreComplex 引入了一个新组件 **Dropdown（下拉框）**，它需要两种数据：

| 数据 | 作用 | 消费方式 |
|---|---|---|
| `data.options` | 下拉列表的选项内容 | DropdownOptionsAdapter（DataConsumer\<LuaTable\>） |
| `data.select` | 当前选中的索引（从 0 开始） | DropdownAdapter（DataConsumer\<int\> + DataProducer\<int\>） |

### data.options 有什么用

```lua
data = {
    options = {'vegetables', 'meat'},  -- 下拉列表显示的内容
    select = 0,                         -- 当前选中第 0 项（'vegetables'）
}
```

`data.options` 是一个 Lua table，会被 `DropdownOptionsAdapter` 消费：

```csharp
public class DropdownOptionsAdapter : AdapterBase<Dropdown>, DataConsumer<LuaTable>
{
    public LuaTable Value
    {
        set
        {
            Target.options.Clear();
            value.ForEach<int, string>((k, v) =>
            {
                Target.options.Add(new Dropdown.OptionData(v));
            });
        }
    }
}
```

引擎绑定时的数据流：

```
初始化:
  observe:getter("options")(data) → {'vegetables', 'meat'}
  → DropdownOptionsAdapter.Value = LuaTable
  → Dropdown 显示 "vegetables"、"meat" 两个选项

用户选中第二项 "meat":
  → DropdownAdapter.OnValueChange(1)    ← 索引从 0 开始
  → data.select = 1                     写回响应式数据
  → computed.message 里读取 options[data.select + 1] → "meat"
```

### 没有 BindTo，那怎么绑定的？

`data.options` 并不需要写 BindTo 字符串。它的绑定是通过 **DropdpownOptionsAdapter 本身继承自 AdapterBase** 来完成的。

查看 [AdapterBase.cs](Assets/XUUI/Scripts/AdapterBase.cs)：

```csharp
public class AdapterBase : MonoBehaviour
{
    [TextArea]
    public string BindTo;
}
```

所以 DropdownOptionsAdapter 也能设置 BindTo。实际上在场景中你需要给它设置 `BindTo = "options"`。而 `data.select` 则由另一个 DropdownAdapter（同样挂载在 Dropdown 上）设置 `BindTo = "select"`。

> 注：Dropdown 组件上挂了**两个**适配器 — 一个 DropdownOptionsAdapter（BindTo = "options"）负责选项列表，一个 DropdownAdapter（BindTo = "select"）负责选中值和交互。

---

## add_option 命令：为什么需要 observeable.raw

这是第二个关键不同。命令里操作 Lua 表格时有个坑：

```lua
add_option = function(data)
    local tmp = observeable.raw(data.options)  -- 先解包成普通表
    table.insert(tmp, 'Option #' .. (#tmp + 1)) -- 操作普通表
    data.options = tmp                           -- 赋值回去，触发响应
end,
```

### 为什么要 raw？

`data.options` 被 `observeable.new()` 包装过，是个带 metatable 的响应式对象。Lua 的 `table.insert` 对这种表**不生效**（它操作的是 metatable 背后隐藏的 `_obj`，不会触发 __newindex）。

所以步骤必须是这样：

```
data.options (响应式表)
    → observeable.raw() → tmp (普通 Lua 表，可正常操作)
    → table.insert(tmp, 'Option #3') → tmp = {'vegetables', 'meat', 'Option #3'}
    → data.options = tmp (赋值回去，触发 __newindex → UI 刷新)
```

如果跳过 `observeable.raw` 直接 `table.insert(data.options, ...)`，数据改了但 UI 不会更新。

---

## 场景设置要点

在 MoreComplex 场景的 Canvas 下，你需要：

| GameObject | 适配器 | BindTo |
|---|---|---|
| Dropdown | DropdownAdapter | `"select"` |
| Dropdown | DropdownOptionsAdapter | `"options"` |
| Text | TextAdapter | `"message"` |
| Button（添加选项） | ButtonAdapter | `"add_option"` |

下拉框绑定了两个适配器，一个管选项内容、一个管选中索引。

---

## 总结

| 概念 | 说明 |
|---|---|
| `data.options` | 下拉框的选项数据源，被 DropdownOptionsAdapter 消费 |
| `data.select` | 选中索引，DropdownAdapter 双向绑定 |
| `observeable.raw()` | 操作响应式 Lua 表前必须先解包，改完再赋回去 |
