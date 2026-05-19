using System;
using System.Threading;
using EchoUI.Core;
using EchoUI.Demo;
using EchoUI.Render.Win32;
using EchoUI.Render.WebGPU;

// Win32 消息循环需要 SynchronizationContext 来正确处理 async/await
var syncCtx = new Win32SynchronizationContext();
SynchronizationContext.SetSynchronizationContext(syncCtx);

var window = new Win32Window("EchoUI Win32 Demo (WebGPU)", 1200, 800);
window.Create();

// 选择渲染后端：默认 WebGPU；设置 ECHOUI_GDI=1 回退 GDI+。
bool useGdi = Environment.GetEnvironmentVariable("ECHOUI_GDI") == "1";
Win32Renderer renderer = useGdi
    ? new Win32Renderer(window)
    : WebGpuRenderer.Create(window);

var reconciler = new Reconciler(renderer, "root");

await reconciler.Mount(Demo.Render);

// 初始布局
renderer.RequestRelayout();

// 进入消息循环（阻塞）
window.Run();
