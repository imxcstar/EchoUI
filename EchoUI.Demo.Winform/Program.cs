using EchoUI.Core;
using EchoUI.Render.Winform;
using System.Windows.Forms;

namespace EchoUI.Demo;

internal static class Program
{
    private static Reconciler? _reconciler;
    private static Form? _mainForm;

    [STAThread]
    static async Task Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        _mainForm = new Form
        {
            Text = "EchoUI Demo",
            Size = new System.Drawing.Size(760, 800),
        };

        var renderer = new WinformRenderer(_mainForm);
        _reconciler = new Reconciler(renderer, _mainForm);

        await _reconciler.Mount(Demo.Render);

        Application.Run(_mainForm);
    }
}