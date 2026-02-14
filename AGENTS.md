# AGENTS.md — EchoUI

## Project Overview

EchoUI is a React-inspired declarative UI framework for .NET 9.0 written in C#. It features a
custom virtual DOM reconciler with diffing, a hooks system (State, Effect, Memo, Shared), and a
Roslyn incremental source generator (`IIncrementalGenerator`) that expands `[Element]`-annotated
methods into overloads with named parameters. The rendering backend is Blazor WebAssembly.

## Solution Structure

```
EchoUI.slnx
├── EchoUI.Core/                  # Core framework: Element, Props, Hooks, Reconciler, Types
│   └── Elements/                 # Built-in composite elements (Button, Input, Tabs, etc.)
├── EchoUI.Core.Abstractions/    # Unused — not in .slnx or referenced by any project
├── EchoUI.Generator/             # Roslyn incremental source generator (netstandard2.0)
├── EchoUI.Render.Web/            # Blazor WASM renderer (JSImport/JSExport)
├── EchoUI.Demo/                  # Shared demo components (App.cs, Dashboard.cs, MarkdownRenderer.cs)
└── EchoUI.Demo.Web/              # Web demo host (Blazor WASM)
```

Note: `EchoUI.Core.Abstractions` contains a duplicate `ElementAttribute` but is dead code. The
actual `[Element]` attribute used by the generator is defined in `EchoUI.Core/Element.cs`.

## Build / Run / Test Commands

```bash
# Restore all packages
dotnet restore EchoUI.slnx

# Build entire solution
dotnet build EchoUI.slnx

# Build a specific project
dotnet build EchoUI.Core/EchoUI.Core.csproj

# Run demo
dotnet run --project EchoUI.Demo.Web/EchoUI.Demo.Web.csproj
```

There is no test project or test framework configured. If adding tests, use xUnit targeting
net9.0 and follow the naming convention `EchoUI.Core.Tests/`.

There is no linter, formatter, CI/CD pipeline, or `.editorconfig`.

## Target Framework & Global Settings

- .NET 9.0 (`net9.0`) for all projects except the generator (`netstandard2.0`).
- Nullable reference types enabled globally via `Directory.Build.props`.
- Implicit usings enabled per-project in `EchoUI.Core` and `EchoUI.Demo` `.csproj` files.
- `EchoUI.Render.Web` does NOT have implicit usings — uses explicit `using` statements.
- `AllowUnsafeBlocks` is enabled in `EchoUI.Render.Web` and `EchoUI.Demo.Web`.

## Code Style Guidelines

### Language & Comments

- Code comments and XML doc summaries are written primarily in **Chinese**. Maintain this
  convention for new code. (A few files like `WebRenderer.cs` and `Dashboard.cs` have English
  comments — prefer Chinese for consistency.)
- Public API members should have `<summary>` XML doc comments.

### Naming Conventions

- **PascalCase** for all public types, methods, properties, and enum members.
- **camelCase** for local variables and parameters.
- **_camelCase** (underscore prefix) for private fields: `_renderer`, `_rootContainer`.
- Props classes: `{Component}Props` (e.g., `ContainerProps`, `ButtonProps`, `TextProps`).
- User-defined components: static methods with signature `Element? MethodName(Props props)`.
- Built-in element factories: return `Element` (non-nullable) and accept their specific Props
  type (e.g., `Button(ButtonProps props)` returns `Element`).
- Native element type names use prefix `EchoUI-` (e.g., `"EchoUI-Container"`, `"EchoUI-Text"`,
  `"EchoUI-Input"`). These are defined in `ElementCoreName` in `Elements.cs`.

### Types & Records

- Props are `record class` inheriting from `Props` base class.
- Value types (Color, Dimension, Spacing, Point, Transition) are `readonly record struct`.
- Use `init`-only properties on Props classes: `T? Property { get; init; }`.
- `ElementType` is a `readonly record struct` with implicit operators.
- `Ref<T>` has an implicit conversion operator to `T` — values can be used directly.
- `NativeProps` enables raw native element creation with arbitrary properties via
  `ValueDictionary<string, object?>`.

### Component Patterns

- Sync components use the `Component` delegate: `Element? Fn(Props props)`.
- Async components use the `AsyncComponent` delegate: `Task<Element?> Fn(Props props)`.
- Use `static EchoUI.Core.Elements` and `static EchoUI.Core.Hooks` imports in component files.
- Hooks are called via static methods: `State<T>()`, `Effect()`, `Memo<T>()`, `Shared<T>()`.
- State hook returns: `(Ref<T> Value, ValueSetter<T> SetValue, StateUpdater<T> UpdateValue)`.
- Access state values via `.Value` property on `Ref<T>` (or rely on implicit conversion).
- Children are passed as `IReadOnlyList<Element>` via collection expressions: `[el1, el2]`.

### Source Generator (`[Element]` attribute)

- The `[Element]` attribute is defined in `EchoUI.Core/Element.cs` (namespace `EchoUI.Core`).
- The generator targets metadata name `EchoUI.Core.ElementAttribute`.
- Mark component factory methods with `[Element]` to auto-generate named-parameter overloads.
- Use `DefaultProperty` to specify which prop becomes the first positional parameter:
  `[Element(DefaultProperty = nameof(ButtonProps.Text))]`.
- The containing class **must** be `partial` (diagnostic `EG001` if not).
- Generated files: `{ClassName}.{MethodName}.ElementOverload.g.cs`.

### Element Creation

- Native elements: `Container(new ContainerProps { ... })` or generated overload.
- Component elements: `Elements.Create(MyComponent, new Props { Key = "..." })`.
- Memoized elements: `Elements.Memo(MyComponent, props)` — uses record equality for skip check.

### Error Handling

- Hooks throw `InvalidOperationException` when called outside a component render context.
- Use null checks and `ArgumentNullException` for required parameters.
- The reconciler uses `try/finally` to restore `Hooks.Context` after rendering.
- Async components support `Fallback` elements shown during loading.

### Formatting

- Indentation: **4 spaces** in C# files. Prefer **tabs** in `.csproj` files (some use spaces).
- Braces on their own line (Allman style) for type and method declarations.
- Single-line expression bodies (`=>`) for simple properties and short methods.
- Collection expressions `[item1, item2]` preferred over `new List<T> { ... }`.

### Key Architectural Rules

- The `Reconciler` owns the component tree and schedules updates via `IUpdateScheduler`.
- `HookContext` is stored in a `[ThreadStatic]` field — hooks are not thread-safe across components.
- Props diffing uses reflection (`GetProperties()`) — keep Props classes lean.
- Delegate-typed props (event handlers) are compared by null-vs-non-null only, not by reference.
- `PropertyPatch` (defined in `IRenderer.cs`) carries changed properties as
  `Dictionary<string, object?>` to the renderer.
- `WebRenderer` uses `System.Text.Json` source generation (`WebRendererJsonContext`) for
  serialization in the rendering pipeline.

### File Organization

- One primary type per file, named to match the type (e.g., `Reconciler.cs`, `Hooks.cs`).
- Related small types (delegates, enums, helper structs) may share a file (e.g., `Types.cs`).
- `IRenderer` and `IUpdateScheduler` interfaces live alongside `PropertyPatch` in `IRenderer.cs`.
- Built-in composite elements live in `EchoUI.Core/Elements/` (Button, CheckBox, ComboBox,
  Input, RadioGroup, Switch, Tabs).
- Demo: `App.cs` is the entry point; `Dashboard.cs` contains the main UI; `MarkdownRenderer.cs`
  has the markdown component.
- Partial classes span files when the source generator extends them (e.g., `Elements` class).

### Dependencies

- **Markdig** 0.41.3 — Markdown parsing (EchoUI.Demo).
- **Microsoft.CodeAnalysis.CSharp** 4.14.0 — Roslyn APIs (EchoUI.Generator).
- **Microsoft.CodeAnalysis.Analyzers** 5.0.0-preview — analyzer support (EchoUI.Generator).
- No external test, lint, or formatting dependencies.
