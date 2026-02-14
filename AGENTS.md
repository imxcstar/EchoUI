# AGENTS.md — EchoUI

## Project Overview

EchoUI is a React-inspired declarative UI framework for .NET 9.0 written in C#. It features a
custom virtual DOM reconciler with diffing, a hooks system (State, Effect, Memo, Shared), and a
Roslyn incremental source generator (`IIncrementalGenerator`) that expands `[Element]`-annotated
methods into overloads with named parameters. Rendering backends: Blazor WebAssembly and Win32 GDI+.

## Solution Structure

```
EchoUI.slnx
├── EchoUI.Core/                  # Core framework: Element, Props, Hooks, Reconciler, Types
│   └── Elements/                 # Built-in composite elements (Button, Input, Tabs, etc.)
├── EchoUI.Core.Abstractions/    # Dead code — not in .slnx, not referenced by any project
├── EchoUI.Generator/             # Roslyn incremental source generator (netstandard2.0)
├── EchoUI.Render.Web/            # Blazor WASM renderer (JSImport/JSExport)
├── EchoUI.Render.Win32/          # Win32 GDI+ renderer (net9.0-windows, self-drawing)
├── EchoUI.Demo/                  # Shared demo components (App.cs, Dashboard.cs, MarkdownRenderer.cs)
├── EchoUI.Demo.Web/              # Web demo host (Blazor WASM)
└── EchoUI.Demo.Win32/            # Win32 demo host (WinExe, PublishAot)
```

The `[Element]` attribute used by the generator is in `EchoUI.Core/Element.cs`.
`EchoUI.Core.Abstractions` contains a duplicate — ignore it.

## Build / Run Commands

```bash
dotnet restore EchoUI.slnx
dotnet build EchoUI.slnx
dotnet build EchoUI.Core/EchoUI.Core.csproj          # single project
dotnet run --project EchoUI.Demo.Web/EchoUI.Demo.Web.csproj    # web demo
dotnet run --project EchoUI.Demo.Win32/EchoUI.Demo.Win32.csproj # win32 demo
```

No test project exists. If adding tests, use xUnit targeting net9.0 in `EchoUI.Core.Tests/`.
No linter, formatter, CI/CD, or `.editorconfig` is configured.

## Target Frameworks & Global Settings

| Project | TFM | ImplicitUsings | AllowUnsafe | Notes |
|---|---|---|---|---|
| EchoUI.Core | net9.0 | enable | no | References Generator as Analyzer |
| EchoUI.Generator | netstandard2.0 | no | no | LangVersion=latest, Roslyn 4.14.0 |
| EchoUI.Render.Web | net9.0 | **no** | yes | Explicit `using` statements required |
| EchoUI.Render.Win32 | net9.0-windows | enable | yes | System.Drawing.Common 9.0.0 |
| EchoUI.Demo | net9.0 | enable | no | Markdig 0.41.3 |
| EchoUI.Demo.Web | net9.0 | no | yes | SDK: Microsoft.NET.Sdk.WebAssembly |
| EchoUI.Demo.Win32 | net9.0-windows | no | yes | OutputType=WinExe, PublishAot |

Nullable reference types enabled globally via `Directory.Build.props`.

## Code Style

### Language & Comments

- Comments and XML doc summaries are primarily in **Chinese**. Maintain this for new code.
- A few files (WebRenderer.cs, Dashboard.cs, Tabs.cs) have English comments — prefer Chinese.
- Public API members should have `<summary>` XML doc comments.

### Naming Conventions

- **PascalCase**: public types, methods, properties, enum members.
- **camelCase**: local variables and parameters.
- **_camelCase**: private fields (`_renderer`, `_rootContainer`).
- Props classes: `{Component}Props` (e.g., `ContainerProps`, `ButtonProps`).
- User components: `static Element? MethodName(Props props)`.
- Built-in factories: return `Element` (non-nullable), accept specific Props type.
- Native element type names: `"EchoUI-"` prefix (e.g., `"EchoUI-Container"`).

### Types & Records

- Props: `record class` inheriting from `Props` base. Use `init`-only properties.
- Value types (Color, Dimension, Spacing, Point, Transition): `readonly record struct`.
- `ElementType`: `readonly record struct` with implicit operators from `string`/`Component`/`AsyncComponent`.
- `Ref<T>`: has implicit conversion to `T`.
- `NativeProps`: arbitrary properties via `ValueDictionary<string, object?>`.

### Component Patterns

- Sync: `Component` delegate — `Element? Fn(Props props)`.
- Async: `AsyncComponent` delegate — `Task<Element?> Fn(Props props)`.
- Import `static EchoUI.Core.Elements` and `static EchoUI.Core.Hooks` in component files.
- Hooks: `State<T>()`, `Effect()`, `Memo<T>()`, `Shared<T>()` — static methods.
- State returns: `(Ref<T> Value, ValueSetter<T> SetValue, StateUpdater<T> UpdateValue)`.
- Children: `IReadOnlyList<Element>` via collection expressions `[el1, el2]`.

### Source Generator (`[Element]`)

- Attribute: `EchoUI.Core/Element.cs`, namespace `EchoUI.Core`.
- Containing class **must** be `partial` (diagnostic `EG001`).
- `DefaultProperty` sets the first positional parameter.
- Generated files: `{ClassName}.{MethodName}.ElementOverload.g.cs`.

### Element Creation

- Native: `Container(new ContainerProps { ... })` or generated overload.
- Component: `Elements.Create(MyComponent, new Props { Key = "..." })`.
- Memoized: `Elements.Memo(MyComponent, props)` — record equality for skip check.

### Error Handling

- Hooks throw `InvalidOperationException` outside render context.
- Use `ArgumentNullException` for required parameters.
- Reconciler uses `try/finally` to restore `Hooks.Context`.
- Async components support `Fallback` elements during loading.

### Formatting

- **4 spaces** indentation in C#. Tabs in `.csproj` (some inconsistency).
- Allman-style braces for type/method declarations.
- Expression bodies (`=>`) for simple properties and short methods.
- Collection expressions `[a, b]` preferred over `new List<T> { ... }`.

## Key Architecture

- `Reconciler` owns the component tree, schedules updates via `IUpdateScheduler`.
- `HookContext` is `[ThreadStatic]` — hooks are not thread-safe across components.
- Props diffing uses reflection (`GetProperties()`) — keep Props lean.
- Delegate props compared by null-vs-non-null only, not by reference.
- `PropertyPatch` carries `Dictionary<string, object?>` to the renderer.
- `WebRenderer` uses `System.Text.Json` source gen (`WebRendererJsonContext`).
- `Win32Renderer` uses GDI+ self-drawing with a simplified Flexbox layout engine.

## File Organization

- One primary type per file, named to match (e.g., `Reconciler.cs`, `Hooks.cs`).
- Small related types may share a file (e.g., `Types.cs`).
- `IRenderer`/`IUpdateScheduler`/`PropertyPatch` share `IRenderer.cs`.
- Built-in elements: `EchoUI.Core/Elements/` (Button, CheckBox, ComboBox, Input, RadioGroup, Switch, Tabs).
- Win32 renderer files: `Win32Renderer.cs`, `Win32Element.cs`, `Win32Window.cs`, `GdiPainter.cs`,
  `FlexLayout.cs`, `HitTest.cs`, `NativeInterop.cs`, `Win32UpdateScheduler.cs`, `Win32SynchronizationContext.cs`.
- Demo entry: `App.cs`; main UI: `Dashboard.cs`; markdown: `MarkdownRenderer.cs`.
- Partial classes span files when the source generator extends them (e.g., `Elements` class).

## Dependencies

- **Markdig** 0.41.3 — Markdown parsing (EchoUI.Demo).
- **Microsoft.CodeAnalysis.CSharp** 4.14.0 — Roslyn APIs (EchoUI.Generator).
- **System.Drawing.Common** 9.0.0 — GDI+ rendering (EchoUI.Render.Win32).
- No test, lint, or formatting dependencies.
