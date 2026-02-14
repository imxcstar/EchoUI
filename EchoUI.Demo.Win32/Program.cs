using System.Threading;
using EchoUI.Core;
using EchoUI.Demo;
using EchoUI.Render.Win32;

// Win32 消息循环需要 SynchronizationContext 来正确处理 async/await
var syncCtx = new Win32SynchronizationContext();
SynchronizationContext.SetSynchronizationContext(syncCtx);

var window = new Win32Window("EchoUI Win32 Demo", 1200, 800);
window.Create();

var renderer = new Win32Renderer(window);
var reconciler = new Reconciler(renderer, "root");

await reconciler.Mount(Demo.Render);

// 初始布局
renderer.RequestRelayout();

// 进入消息循环（阻塞）
window.Run();
