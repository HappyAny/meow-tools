using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace MeowTools;

public record ToolView(ToolEntry Entry, ImageSource? Icon)
{
    public string Detail => (string.IsNullOrEmpty(Entry.Version) ? Entry.Kind : "v" + Entry.Version) + "  ·  " + Entry.RelativePath;
    public string LaunchLabel => "启动 " + Entry.Name;
}

public partial class MainWindow : Window
{
    private readonly string dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeowTools");
    private readonly string toolsDirectory;
    private readonly bool verification = Environment.GetCommandLineArgs().Contains("--verify-native");
    private readonly CancellationTokenSource lifetime = new();
    private IReadOnlyList<ToolView> allTools = [];
    private CatalogResult? catalog;
    private bool scanning, loaded, closing;
    private DateTime lastScan = DateTime.MinValue;
    private readonly Dictionary<string, long> lastLaunch = new(StringComparer.OrdinalIgnoreCase);

    public MainWindow() : this(null) { }
    public MainWindow(string? testToolsDirectory)
    {
        toolsDirectory = testToolsDirectory ?? (verification && Environment.GetEnvironmentVariable("MEOW_VERIFY_TOOLS") is { Length: > 0 } folder
            ? Path.GetFullPath(folder) : ToolCatalog.FolderAt(AppContext.BaseDirectory));
        InitializeComponent();
        string mode = "System";
        try { string saved = File.ReadAllText(Path.Combine(dataDirectory, "theme.txt")).Trim(); if (saved is "System" or "Light" or "Dark") mode = saved; } catch (IOException) { } catch (UnauthorizedAccessException) { }
        ApplyTheme(mode); SystemEvents.UserPreferenceChanged += ThemePreferenceChanged;
        Loaded += OnLoaded;
        Activated += async (_, _) => { if (loaded && !scanning && !verification && DateTime.UtcNow - lastScan > TimeSpan.FromSeconds(2)) await RefreshAsync(); };
        Closing += (_, _) => { closing = true; lifetime.Cancel(); SystemEvents.UserPreferenceChanged -= ThemePreferenceChanged; };
        PreviewKeyDown += async (_, e) =>
        {
            if (e.Key == Key.F5) { e.Handled = true; await RefreshAsync(); }
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; SearchBox.Focus(); SearchBox.SelectAll(); }
        };
    }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        loaded = true;
        await RefreshAsync();
        if (verification)
        {
            try { await VerifyNativeAsync(); Close(); }
            catch (Exception ex)
            {
                string? output = Environment.GetEnvironmentVariable("MEOW_VERIFY_OUTPUT");
                if (output != null) { Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "failure.txt"), ex.ToString()); }
                Application.Current.Shutdown(1);
            }
        }
    }
    private async Task RefreshAsync()
    {
        if (scanning || closing) return;
        scanning = true; RefreshButton.IsEnabled = false; StatusText.Text = "正在读取 tools 文件夹…";
        string? selected = (ToolsList.SelectedItem as ToolView)?.Entry.FullPath;
        try
        {
            var result = await Task.Run(() =>
            {
                var scan = ToolCatalog.Read(toolsDirectory, lifetime.Token);
                var views = scan.Tools.Select(entry => new ToolView(entry, ToolIcon.Read(entry.FullPath))).ToArray();
                return (scan, views);
            }, lifetime.Token);
            if (closing) return;
            catalog = result.scan; allTools = result.views;
            Filter();
            if (selected != null) ToolsList.SelectedItem = ToolsList.Items.OfType<ToolView>().FirstOrDefault(x => x.Entry.FullPath == selected);
            StatusText.Text = catalog.Warnings.Count > 0 ? string.Join("；", catalog.Warnings.Take(2)) : "新增工具放入 tools，刷新即可。";
            lastScan = DateTime.UtcNow;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { if (!closing) StatusText.Text = "读取失败：" + ex.Message; }
        finally { scanning = false; if (!closing) RefreshButton.IsEnabled = true; }
    }
    private void Filter()
    {
        if (ToolsList == null) return;
        string query = SearchBox.Text.Trim(); SearchPlaceholder.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var matches = allTools.Where(x => x.Entry.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || x.Entry.RelativePath.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToArray();
        ToolsList.ItemsSource = matches;
        CountText.Text = query.Length == 0 ? $"我的工具  ·  {allTools.Count} 个" : $"找到 {matches.Length} 个  /  共 {allTools.Count} 个";
        EmptyPanel.Visibility = matches.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyTitle.Text = query.Length > 0 ? "没有找到匹配的工具" : catalog?.Missing == true ? "还没有 tools 文件夹" : "还没有工具";
        EmptyText.Text = query.Length > 0 ? "换一个名称，或清空搜索查看全部工具。" : "把 EXE 或快捷方式放进同目录的 tools 文件夹，再点击刷新。也可以按工具分子文件夹存放。";
    }
    private void Search_Changed(object sender, TextChangedEventArgs e) => Filter();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button { Tag: ToolView tool }) StartTool(tool);
    }
    private void StartTool(ToolView tool)
    {
        long now = Environment.TickCount64;
        if (lastLaunch.TryGetValue(tool.Entry.FullPath, out long previous) && now - previous < 1000) return;
        lastLaunch[tool.Entry.FullPath] = now;
        try
        {
            using var child = Process.Start(ToolCatalog.LaunchInfo(tool.Entry, toolsDirectory));
            StatusText.Text = "已启动 " + tool.Entry.Name;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { StatusText.Text = "已取消启动。"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { StatusText.Text = "无法启动 " + tool.Entry.Name + "：" + ex.Message; }
    }
    private void Tools_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ToolsList.SelectedItem is ToolView tool) { e.Handled = true; StartTool(tool); }
    }
    private void Tools_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? source = e.OriginalSource as DependencyObject;
        while (source != null && source != ToolsList)
        {
            if (source is ButtonBase) return;
            if (source is ListBoxItem item && item.Content is ToolView tool) { e.Handled = true; StartTool(tool); return; }
            source = source is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
    }
    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(toolsDirectory); Process.Start(new ProcessStartInfo(toolsDirectory) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { StatusText.Text = "无法打开工具文件夹：" + ex.Message; }
    }
    private async Task VerifyNativeAsync()
    {
        string output = Environment.GetEnvironmentVariable("MEOW_VERIFY_OUTPUT") ?? Path.Combine(AppContext.BaseDirectory, "verification");
        Directory.CreateDirectory(output); var captures = new List<string>();
        async Task Capture(string name)
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); UpdateLayout();
            var surface = (FrameworkElement)Content;
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth + surface.Margin.Left + surface.Margin.Right), (int)Math.Ceiling(surface.ActualHeight + surface.Margin.Top + surface.Margin.Bottom), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(this); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name)); png.Save(stream); captures.Add(name);
        }
        ApplyTheme("Light"); await Capture("launcher-light.png");
        if (allTools.Count > 0)
        {
            SearchBox.Text = allTools[0].Entry.RelativePath;
            if (ToolsList.Items.Count != 1) throw new InvalidOperationException("搜索未准确匹配工具。");
            await Capture("launcher-search.png");
        }
        SearchBox.Text = "not-a-tool-74db8b0e";
        if (ToolsList.Items.Count != 0 || EmptyPanel.Visibility != Visibility.Visible) throw new InvalidOperationException("搜索空状态错误。");
        await Capture("launcher-empty.png"); SearchBox.Text = "";
        ApplyTheme("Dark"); await Capture("launcher-dark.png");
        Width = MinWidth; Height = MinHeight; await Capture("launcher-small.png");
        foreach (var document in LicenseWindow.Documents) { using var stream = LicenseWindow.OpenDocument(document.Resource); if (stream.Length < 20) throw new InvalidOperationException("许可证缺失。"); }
        if (Icon == null) throw new InvalidOperationException("窗口图标缺失。");
        File.WriteAllText(Path.Combine(output, "verification.json"), JsonSerializer.Serialize(new
        {
            passed = true, version = "1.0.0", toolsDirectory, count = allTools.Count, toolNames = allTools.Select(x => x.Entry.Name),
            relativePaths = allTools.Select(x => x.Entry.RelativePath), warnings = catalog?.Warnings, captures,
            licenses = LicenseWindow.Documents.Length, childToolsStarted = false, hardwareAccessed = false
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
