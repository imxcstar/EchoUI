# EchoUI

**中文 | [English](./README.md)**

一个受 React 启发的 .NET 9.0 声明式 UI 框架。内置虚拟 DOM 协调器（支持 diff）、Hooks 系统、Roslyn 源生成器，以及多渲染后端（Blazor WebAssembly 和 Win32 GDI+）。

## 预览

![预览 1](./Preview/1.png)

![预览 2](./Preview/2.png)

## 特性

- **声明式组件** — 以纯函数定义 UI，返回元素树，体验类似 React
- **虚拟 DOM 与协调器** — 高效的 key 化 diff 和批量更新
- **Hooks** — `State`、`Effect`、`Memo`、`Shared`，用于状态管理和副作用
- **源生成器** — `[Element]` 特性通过 Roslyn 自动生成命名参数重载
- **多后端渲染** — 支持浏览器 DOM（Blazor WASM）和原生 Win32 窗口（GDI+）
- **内置元素** — Button、Input、CheckBox、ComboBox、RadioGroup、Switch、Tabs
- **Flexbox 布局** — 基于容器的布局，支持方向、对齐、间距、弹性伸缩

## 快速开始

### 环境要求

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### 构建

```bash
dotnet restore EchoUI.slnx
dotnet build EchoUI.slnx
```

### 运行示例

```bash
# Blazor WebAssembly 示例
dotnet run --project EchoUI.Demo.Web/EchoUI.Demo.Web.csproj

# Win32 GDI+ 示例（仅限 Windows）
dotnet run --project EchoUI.Demo.Win32/EchoUI.Demo.Win32.csproj
```

## 使用方式

### 定义组件

```csharp
using static EchoUI.Core.Elements;
using static EchoUI.Core.Hooks;

static Element? Counter(Props props)
{
    var (count, setCount, updateCount) = State(0);

    return Container(new ContainerProps
    {
        Direction = LayoutDirection.Horizontal,
        Gap = 10,
        AlignItems = AlignItems.Center,
        Children =
        [
            Button(new ButtonProps
            {
                Text = "-",
                OnClick = () => updateCount(c => c - 1)
            }),
            Text(new TextProps { Text = $"Count: {count.Value}" }),
            Button(new ButtonProps
            {
                Text = "+",
                OnClick = () => updateCount(c => c + 1)
            }),
        ]
    });
}
```

### Hooks

```csharp
// State — 响应式状态，包含 setter 和 updater
var (value, setValue, updateValue) = State(initialValue);

// Effect — 副作用，支持依赖追踪和清理函数
Effect(() =>
{
    Console.WriteLine($"值已变更: {value.Value}");
    return () => { /* 清理 */ };
}, [value.Value]);

// Memo — 记忆化计算
var expensive = Memo(() => ComputeSomething(value.Value), [value.Value]);

// Shared — 跨组件共享状态
var shared = Shared<MySharedState>();

// IsInitialRender — 判断是否为首次渲染
var isFirst = IsInitialRender();
```

### 源生成器

使用 `[Element]` 标记元素工厂方法，自动生成命名参数重载：

```csharp
public static partial class Elements
{
    [Element(DefaultProperty = nameof(ButtonProps.Text))]
    public static Element Button(ButtonProps props) { ... }
}

// 生成的重载允许这样调用：
Button("点击我", OnClick: () => DoSomething(), BackgroundColor: Color.Blue)
```

包含该方法的类必须声明为 `partial`。生成器输出文件格式为 `{类名}.{方法名}.ElementOverload.g.cs`。

### 挂载与渲染

```csharp
// Blazor WebAssembly
var renderer = new WebRenderer("app");
var reconciler = new Reconciler(renderer);
await reconciler.Mount(MyApp);

// Win32 GDI+
var window = new Win32Window(1200, 800, "My App");
var renderer = new Win32Renderer(window);
var reconciler = new Reconciler(renderer);
await reconciler.Mount(MyApp);
window.Run();
```

### 内置元素

| 元素 | 说明 |
|---|---|
| `Container` | Flexbox 布局容器，支持样式、事件、过渡动画 |
| `Text` | 文本显示，支持字体、颜色、字重 |
| `Button` | 交互按钮，支持悬停/按下状态 |
| `Input` | 文本输入框，支持双向绑定 |
| `CheckBox` | 复选框，支持标签 |
| `ComboBox` | 下拉选择框，带展开动画 |
| `RadioGroup` | 单选按钮组（垂直/水平） |
| `Switch` | 动画开关 |
| `Tabs` | 标签页，带切换动画 |

### 样式

```csharp
Container(new ContainerProps
{
    Width = Dimension.Pixels(300),
    Height = Dimension.Percent(100),
    Padding = new Spacing(16),
    BackgroundColor = Color.FromHex("#1a1a2e"),
    BorderRadius = 8,
    BorderWidth = 1,
    BorderColor = Color.Gray,
    Transitions = [new Transition(200, Easing.EaseInOut)],
    Overflow = Overflow.Scroll,
    Direction = LayoutDirection.Vertical,
    JustifyContent = JustifyContent.Center,
    AlignItems = AlignItems.Center,
    Gap = 12,
})
```

## 项目结构

```
EchoUI.slnx
├── EchoUI.Core/                  # 核心：Element、Props、Hooks、Reconciler、Types
│   └── Elements/                 # 内置元素（Button、Input、Tabs 等）
├── EchoUI.Generator/             # Roslyn 增量源生成器
├── EchoUI.Render.Web/            # Blazor WASM 渲染器
├── EchoUI.Render.Win32/          # Win32 GDI+ 渲染器
├── EchoUI.Demo/                  # 共享示例组件
├── EchoUI.Demo.Web/              # Web 示例宿主
└── EchoUI.Demo.Win32/            # Win32 示例宿主
```

## 架构

- **Reconciler** 管理组件树，通过 `IUpdateScheduler` 调度批量更新
- **HookContext** 使用 `[ThreadStatic]`，Hooks 作用域限定在当前渲染过程
- **Props diff** 基于反射；委托类型属性仅比较 null 与非 null
- **IRenderer** 接口抽象渲染层 — 实现该接口即可添加新的渲染后端
- **WebRenderer** 通过 `JSImport`/`JSExport` 桥接浏览器 DOM，使用 `System.Text.Json` 源生成序列化
- **Win32Renderer** 使用 GDI+ 自绘，内置简化版 Flexbox 布局引擎

## 许可证

详见 [LICENSE](./LICENSE)。
