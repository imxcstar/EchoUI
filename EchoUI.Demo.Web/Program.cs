using EchoUI.Core;
using EchoUI.Demo;
using EchoUI.Render.Web;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

Console.WriteLine("Start!");
await EchoUIHelper.Init();

partial class EchoUIHelper
{
    private static WebRenderer? _renderer;
    private static Reconciler? _reconciler;

    internal static async Task Init()
    {
        var root = "app";
        _renderer = new WebRenderer(root);
        _reconciler = new Reconciler(_renderer, root);

        await _reconciler.Mount(Demo.Render);
        Console.WriteLine("Init");
    }

    [JSExport]
    public static async Task RaiseEventAsync(string elementId, string eventName, string eventArgsJson)
    {
        await WebRenderer.RaiseEventAsync(elementId, eventName, eventArgsJson);
    }
}