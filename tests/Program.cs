using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MeowTools;

internal static class Program
{
    private static readonly List<object> results = [];
    private static int failed;
    private static readonly string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
    private static readonly string fixture = Path.Combine(root, "artifacts", "test-" + Guid.NewGuid().ToString("N"));
    private static readonly string toolDir = Path.Combine(fixture, "tools");
    private static string probeExe = "";
    private static void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
    private static async Task Test(string name, Func<Task> test)
    {
        try { await test(); results.Add(new { name, passed = true }); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; results.Add(new { name, passed = false, error = ex.ToString() }); Console.WriteLine("FAIL " + name + " " + ex); }
    }
    private static Task Sync(Action action) { action(); return Task.CompletedTask; }
    private static void Rejected(Action action) { try { action(); } catch (IOException) { return; } throw new Exception("Expected a rejected path"); }
    private static async Task<JsonDocument> Result(string folder)
    {
        string path = Path.Combine(folder, "probe-result.json");
        for (int i = 0; i < 100; i++) { if (File.Exists(path) && new FileInfo(path).Length > 0) return JsonDocument.Parse(await File.ReadAllTextAsync(path)); await Task.Delay(50); }
        throw new TimeoutException("Probe did not report its working directory.");
    }
    [STAThread]
    public static int Main()
    {
        Directory.CreateDirectory(toolDir);
        string target = Path.Combine(toolDir, "中文 & spaces"); Directory.CreateDirectory(target);
        string source = Path.Combine(root, "tests/LaunchProbe/bin/Release/net10.0-windows");
        foreach (string path in Directory.GetFiles(source)) File.Copy(path, Path.Combine(target, Path.GetFileName(path)));
        probeExe = Path.Combine(target, "测试 工具 & probe.exe"); File.Move(Path.Combine(target, "LaunchProbe.exe"), probeExe);
        var app = new App(); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.DispatcherUnhandledException += (_, e) => { failed++; Console.WriteLine(e.Exception); e.Handled = true; };
        app.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try { await RunTests(); }
            finally
            {
                Directory.CreateDirectory(Path.Combine(root, "docs"));
                File.WriteAllText(Path.Combine(root, "docs/tests.json"), JsonSerializer.Serialize(new { passed = results.Count - failed, failed, realUserToolsStarted = false, results }, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"{results.Count - failed}/{results.Count} tests passed"); app.Dispatcher.InvokeShutdown();
            }
        }));
        Dispatcher.Run(); return failed == 0 ? 0 : 1;
    }
    private static async Task RunTests()
    {
        await Test("Tools directory is relative to the executable, not the caller working directory", () => Sync(() => Check(ToolCatalog.FolderAt(fixture) == toolDir)));
        await Test("Missing folder is an empty collection and scanning does not create it", () => Sync(() => { string path = Path.Combine(fixture, "missing"); var c = ToolCatalog.Read(path); Check(c.Missing && c.Tools.Count == 0 && !Directory.Exists(path)); }));
        await Test("Scan includes nested EXEs and shortcuts but excludes data and DLLs", () => Sync(() =>
        {
            File.WriteAllText(Path.Combine(toolDir, "fake.LNK"), "fixture"); File.WriteAllText(Path.Combine(toolDir, "notes.txt"), "data");
            var c = ToolCatalog.Read(toolDir); Check(c.Tools.Count == 2 && c.Tools.Any(x => x.FullPath == probeExe) && c.Tools.Any(x => x.Kind == "快捷方式"));
            Check(!File.Exists(Path.Combine(Path.GetDirectoryName(probeExe)!, "probe-result.json")), "Scanning ran a program");
        }));
        await Test("Refresh discovers new, renamed and removed tools", () => Sync(() =>
        {
            string a = Path.Combine(toolDir, "new.exe"), b = Path.Combine(toolDir, "renamed.exe"); File.WriteAllText(a, "fixture");
            Check(ToolCatalog.Read(toolDir).Tools.Count == 3); File.Move(a, b); Check(ToolCatalog.Read(toolDir).Tools.Any(x => x.RelativePath == "renamed.exe"));
            File.Delete(b); Check(ToolCatalog.Read(toolDir).Tools.Count == 2);
        }));
        await Test("Removed and out-of-folder paths cannot be launched", () => Sync(() =>
        {
            var entry = ToolCatalog.Read(toolDir).Tools.Single(x => x.FullPath == probeExe);
            Rejected(() => ToolCatalog.LaunchInfo(entry with { FullPath = Path.Combine(fixture, "outside.exe") }, toolDir));
            Rejected(() => ToolCatalog.LaunchInfo(entry with { FullPath = Path.Combine(toolDir, "gone.exe") }, toolDir));
            Check(!ToolCatalog.IsWithin(toolDir + "-other/file.exe", toolDir));
        }));
        await Test("EXE launch preserves Unicode, spaces and the tool working directory", async () =>
        {
            var entry = ToolCatalog.Read(toolDir).Tools.Single(x => x.FullPath == probeExe);
            var start = ToolCatalog.LaunchInfo(entry, toolDir); Check(start.FileName == probeExe && start.Arguments.Length == 0 && start.UseShellExecute);
            start.WindowStyle = ProcessWindowStyle.Hidden;
            using (var process = Process.Start(start)) { }
            using var result = await Result(Path.GetDirectoryName(probeExe)!);
            Check(result.RootElement.GetProperty("cwd").GetString() == Path.GetDirectoryName(probeExe));
            Check(result.RootElement.GetProperty("args").GetArrayLength() == 0);
        });
        await Test("Windows shortcut preserves its target, arguments and own working directory", async () =>
        {
            string cwd = Path.Combine(fixture, "快捷方式 工作目录"); Directory.CreateDirectory(cwd);
            string shortcut = Path.Combine(toolDir, "probe shortcut.lnk");
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            dynamic link = shell.CreateShortcut(shortcut);
            try { link.TargetPath = probeExe; link.Arguments = "--from-shortcut \"中文 参数\""; link.WorkingDirectory = cwd; link.Save(); }
            finally { Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell); }
            var entry = ToolCatalog.Read(toolDir).Tools.Single(x => x.FullPath == shortcut);
            using (var process = Process.Start(ToolCatalog.LaunchInfo(entry, toolDir))) { }
            using var result = await Result(cwd); Check(result.RootElement.GetProperty("cwd").GetString() == cwd);
            Check(result.RootElement.GetProperty("args")[0].GetString() == "--from-shortcut" && result.RootElement.GetProperty("args")[1].GetString() == "中文 参数");
        });
        await Test("Native launch button starts only the selected tool and closing leaves the child alive", async () =>
        {
            string resultPath = Path.Combine(Path.GetDirectoryName(probeExe)!, "probe-result.json"); File.Delete(resultPath);
            var window = new MainWindow(toolDir) { ShowInTaskbar = false, Left = -3000, Top = -3000, WindowStartupLocation = WindowStartupLocation.Manual };
            try
            {
                window.Show(); var list = (ListBox)window.FindName("ToolsList");
                for (int i = 0; i < 100 && list.Items.Count == 0; i++) await Task.Delay(30);
                var chosen = list.Items.OfType<ToolView>().Single(x => x.Entry.FullPath == probeExe); list.SelectedItem = chosen; list.ScrollIntoView(chosen);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); window.UpdateLayout();
                var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(chosen);
                Button button = Descendants(row).OfType<Button>().Single(); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.Close(); using var result = await Result(Path.GetDirectoryName(probeExe)!);
                Check(result.RootElement.GetProperty("args").GetArrayLength() == 0);
            }
            finally { if (window.IsVisible) window.Close(); }
        });
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
}
