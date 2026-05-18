using EchoUI.Core;
using EchoUI.Render.Win32;
using EchoUI.Demo;

var syncCtx = new Win32SynchronizationContext();
SynchronizationContext.SetSynchronizationContext(syncCtx);

var window = new Win32Window("EchoUI · Showcase", 1280, 860);
window.Create();

var renderer = new Win32Renderer(window)
{
    SmoothScrollEnabled = true
};
var reconciler = new Reconciler(renderer, "root");

await reconciler.Mount(Showcase.Render);
renderer.RequestRelayout();
window.Run();
