

using System.Runtime.Loader;

public class HotReloadAssemblyContext : AssemblyLoadContext
{
    public HotReloadAssemblyContext() : base(isCollectible: true) { }
}

//// --- DevTools/HotReloader.cs ---
//namespace EchoUI.DevTools
//{
//    using Microsoft.CodeAnalysis;
//    using Microsoft.CodeAnalysis.CSharp;
//    using System.Reflection;

//    public class HotReloader
//    {
//        public event Action<Assembly>? AssemblyReloaded;
//        private readonly FileSystemWatcher _watcher;
//        private DateTime _lastRead = DateTime.MinValue;

//        public HotReloader(string sourceFilePathToWatch)
//        {
//            var fullPath = Path.GetFullPath(sourceFilePathToWatch);
//            _watcher = new(Path.GetDirectoryName(fullPath)!, Path.GetFileName(fullPath))
//            {
//                NotifyFilter = NotifyFilters.LastWrite,
//                EnableRaisingEvents = true
//            };
//            _watcher.Changed += OnSourceFileChanged;
//        }

//        private async void OnSourceFileChanged(object sender, FileSystemEventArgs e)
//        {
//            if (DateTime.Now - _lastRead < TimeSpan.FromSeconds(1)) return;
//            _lastRead = DateTime.Now;
//            Console.WriteLine($"[HotReload] File changed: {e.Name}. Recompiling...");
//            await Task.Delay(100);

//            try
//            {
//                var sourceCode = File.ReadAllText(e.FullPath);
//                var references = AppDomain.CurrentDomain.GetAssemblies()
//                    .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
//                    .Select(a => MetadataReference.CreateFromFile(a.Location));

//                var compilation = CSharpCompilation.Create(
//                    $"HotReload_{Guid.NewGuid()}",
//                    new[] { CSharpSyntaxTree.ParseText(sourceCode) },
//                    references,
//                    new(OutputKind.DynamicallyLinkedLibrary));

//                using var ms = new MemoryStream();
//                var result = compilation.Emit(ms);

//                if (!result.Success)
//                {
//                    result.Diagnostics
//                        .Where(d => d.Severity == DiagnosticSeverity.Error)
//                        .ToList()
//                        .ForEach(d => Console.Error.WriteLine($"[HotReload] Compile Error: {d.GetMessage()}"));
//                    return;
//                }

//                Console.ForegroundColor = ConsoleColor.Green;
//                Console.WriteLine("[HotReload] Compile successful. Reloading assembly...");
//                Console.ResetColor();

//                ms.Seek(0, SeekOrigin.Begin);
//                AssemblyReloaded?.Invoke(Assembly.Load(ms.ToArray()));
//            }
//            catch (Exception ex)
//            {
//                Console.Error.WriteLine($"[HotReload] Failed: {ex.Message}");
//            }
//        }
//    }
//}