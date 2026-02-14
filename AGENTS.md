# AGENTS.md — EchoUI

## Project Overview

EchoUI is a React-inspired declarative UI framework for .NET 9.0 written in C#. It features a
custom virtual DOM reconciler with diffing, a hooks system (State, Effect, Memo, Shared), and a
Roslyn source generator that expands `[Element]`-annotated methods into overloads with named
parameters. The rendering backend is Blazor WebAssembly.

## Solution Structure

```
EchoUI.slnx
├── EchoUI.Core/                  # Core framework: Element, Props, Hooks, Reconciler, Types
├── EchoUI.Core.Abstractions/     # [Element] attribute (netstandard2.0)
├── EchoUI.Generator/             # Roslyn incremental source generator (netstandard2.0)
├── EchoUI.Render.Web/            # Blazor WASM renderer (JSImport/JSExport)
├── EchoUI.Demo/                  # Shared demo components
└── EchoUI.Demo.Web/              # Web demo (Blazor WASM)
```

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

There is no test project or test framework configured. If adding tests, use xUnit or NUnit
targeting net9.0 and follow the naming convention `EchoUI.Tests/` or `EchoUI.Core.Tests/`.

There is no linter, formatter, or CI/CD pipeline configured. No `.editorconfig` exists.

## Target Framework & Global Settings

- .NET 9.0 (`net9.0`) for all projects except the generator and abstractions (`netstandard2.0`).
- Nullable reference types are enabled globally via `Directory.Build.props`.
- Implicit usings are enabled in Core and demo projects.

## Code Style Guidelines

### Language & Comments

- Code comments and XML doc summaries are written in **Chinese**. Maintain this convention.
- Public API members should have `<summary>` XML doc comments.

### Naming Conventions

- **PascalCase** for all public types, methods, properties, and enum members.
- **camelCase** for local variables and parameters.
- **_camelCase** (underscore prefix) for private fields: `_renderer`, `_rootContainer`, `_sharedStates`.
- Props classes are named `{Component}Props` (e.g., `ContainerProps`, `ButtonProps`, `TextProps`).
- Component methods are static methods with signature `Element? MethodName(Props props)`.
- Native element type names use the prefix `EchoUI-` (e.g., `"EchoUI-Container"`, `"EchoUI-Text"`).

### Types & Records

- Props are defined as `record class` inheriting from `Props` base class.
- Value types (Color, Dimension, Spacing, Point, Transition) are `readonly record struct`.
- Use `init`-only properties on Props classes.
- Nullable properties on Props use `T?` with `{ get; init; }` pattern.
- The `ElementType` wrapper is a `readonly record struct` with implicit operators.

### Component Patterns

- Components are static methods returning `Element?` that accept `Props` as parameter.
- Sync components use the `Component` delegate: `Element? Fn(Props props)`.
- Async components use the `AsyncComponent` delegate: `Task<Element?> Fn(Props props)`.
- Use `static EchoUI.Core.Elements` and `static EchoUI.Core.Hooks` imports in component files.
- Hooks are called via static methods: `State<T>()`, `Effect()`, `Memo<T>()`, `Shared<T>()`.
- State hook returns a tuple: `(Ref<T> Value, ValueSetter<T> SetValue, StateUpdater<T> UpdateValue)`.
- Access state values via `.Value` property on the `Ref<T>`.

### Source Generator (`[Element]` attribute)

- Mark component factory methods with `[Element]` to auto-generate named-parameter overloads.
- Use `DefaultProperty` to specify which prop becomes the first positional parameter:
  `[Element(DefaultProperty = nameof(ButtonProps.Text))]`.
- The containing class **must** be `partial` for the generator to emit code.
- Generated overloads allow calling `Button(Text: "+", Width: Dimension.Pixels(30))` instead of
  constructing a `ButtonProps` manually.

### Element Creation

- Native elements: `Container(new ContainerProps { ... })` or generated overload `Container(Width: ..., Children: [...])`.
- Component elements: `Elements.Create(MyComponent, new Props { Key = "..." })`.
- Memoized elements: `Elements.Memo(MyComponent, props)` — uses record equality for skip check.
- Children are passed as `IReadOnlyList<Element>` via collection expressions: `Children: [el1, el2]`.

### Error Handling

- Hooks throw `InvalidOperationException` when called outside a component render context.
- Use null checks and `ArgumentNullException` for required parameters.
- The reconciler uses `try/finally` to restore `Hooks.Context` after rendering.
- Async components support `Fallback` elements shown during loading.

### Formatting

- Indentation: **tabs** in `.csproj` files, **4 spaces** in C# files.
- Braces on their own line (Allman style) for type and method declarations.
- Single-line expression bodies (`=>`) are used for simple properties and short methods.
- Collection expressions `[item1, item2]` preferred over `new List<T> { ... }` for children.

### Key Architectural Rules

- The `Reconciler` owns the component tree and schedules updates via `IUpdateScheduler`.
- `HookContext` is stored in a `[ThreadStatic]` field — hooks are not thread-safe across components.
- Props diffing uses reflection (`GetProperties()`) — keep Props classes lean.
- Delegate-typed props (event handlers) are compared by null-vs-non-null only, not by reference.
- `PropertyPatch` carries changed properties as `Dictionary<string, object?>` to the renderer.
- `NativeProps.Properties` is a `ValueDictionary<string, object?>` that gets unpacked during diffing.

### File Organization

- One primary type per file, named to match the type (e.g., `Reconciler.cs`, `Hooks.cs`).
- Related small types (delegates, enums, helper structs) may share a file (e.g., `Types.cs`).
- Demo components live in `EchoUI.Demo/App.cs`; custom elements in separate files like `MarkdownRenderer.cs`.
- Partial classes span files when the source generator needs to extend them (e.g., `Elements` class).

### Dependencies

- **Markdig** 0.41.3 for Markdown parsing in the demo.
- **Microsoft.CodeAnalysis.CSharp** 4.14.0 for the source generator.
- No external test, lint, or formatting dependencies.
