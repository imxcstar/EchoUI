using System;
using System.Linq;
using System.Threading;
using EchoUI.Core;
using EchoUI.Demo;
using EchoUI.Render.Win32;

// Win32 消息循环需要 SynchronizationContext 来正确处理 async/await
var syncCtx = new Win32SynchronizationContext();
SynchronizationContext.SetSynchronizationContext(syncCtx);

var window = new Win32Window("EchoUI Win32 Demo", 1200, 800);
window.Create();

var backendKind = ResolveBackendKind(args);
var renderer = new Win32Renderer(window, backendKind);
var reconciler = new Reconciler(renderer, "root");

await reconciler.Mount(Demo.Render);

// 初始布局
renderer.RequestRelayout();

// 进入消息循环（阻塞）
window.Run();

static Win32RenderBackendKind ResolveBackendKind(string[] args)
{
    foreach (var arg in args)
    {
        const string prefix = "--backend=";
        if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return ParseBackendKind(arg[prefix.Length..]);
        }
    }

    if (args.Any(static arg => string.Equals(arg, "--cpu", StringComparison.OrdinalIgnoreCase)))
        return Win32RenderBackendKind.Cpu;
    if (args.Any(static arg => string.Equals(arg, "--skia", StringComparison.OrdinalIgnoreCase)))
        return Win32RenderBackendKind.Skia;
    if (args.Any(static arg => string.Equals(arg, "--direct2d", StringComparison.OrdinalIgnoreCase)))
        return Win32RenderBackendKind.Direct2D;

    var fromEnvironment = Environment.GetEnvironmentVariable("ECHOUI_WIN32_BACKEND");
    return string.IsNullOrWhiteSpace(fromEnvironment)
        ? Win32RenderBackendKind.Direct2D
        : ParseBackendKind(fromEnvironment);
}

static Win32RenderBackendKind ParseBackendKind(string value)
{
    return value.Trim().ToLowerInvariant() switch
    {
        "cpu" => Win32RenderBackendKind.Cpu,
        "skia" => Win32RenderBackendKind.Skia,
        "direct2d" or "d2d" => Win32RenderBackendKind.Direct2D,
        _ => Win32RenderBackendKind.Direct2D
    };
}
