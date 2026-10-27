using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MeowTools;

internal sealed class LicenseWindow : Window
{
    internal static readonly (string Label, string Resource)[] Documents =
    [
        ("项目许可证", "Project.txt"), ("第三方说明", "Notices.txt"), (".NET Runtime", "DotNet.txt"),
        (".NET 第三方声明", "DotNetNotices.txt"), ("WPF", "Wpf.txt")
    ];
    internal static Stream OpenDocument(string resource) => typeof(LicenseWindow).Assembly.GetManifestResourceStream("MeowTools.Licenses." + resource)
        ?? throw new InvalidDataException("内置许可证缺失：" + resource);

    public LicenseWindow(Window owner)
    {
        Owner = owner; Icon = owner.Icon; Title = "关于喵喵小工具";
        Width = 640; Height = 500; MinWidth = 460; MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "CanvasBrush");
        SetResourceReference(ForegroundProperty, "PrimaryBrush");
        FontFamily = new FontFamily("Segoe UI Variable Text, Microsoft YaHei UI"); FontSize = 12;
        var root = new DockPanel { Margin = new Thickness(16) };
        var heading = new TextBlock { Text = "喵喵小工具 1.0.0 · 本地工具启动器", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var selection = new ComboBox { ItemsSource = Documents.Select(d => d.Label).ToArray(), Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(selection, Dock.Top); root.Children.Add(selection);
        var content = new ContentControl(); root.Children.Add(content); Content = root;
        selection.SelectionChanged += (_, _) =>
        {
            if (selection.SelectedIndex < 0) return;
            string name = Documents[selection.SelectedIndex].Resource;
            using var stream = OpenDocument(name);
            if (name.EndsWith(".rtf", StringComparison.OrdinalIgnoreCase))
            {
                var text = new RichTextBox { IsReadOnly = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(10) };
                new TextRange(text.Document.ContentStart, text.Document.ContentEnd).Load(stream, DataFormats.Rtf);
                content.Content = text;
            }
            else
            {
                using var reader = new StreamReader(stream);
                content.Content = new TextBox { Text = reader.ReadToEnd(), IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(10), FontFamily = new FontFamily("Consolas"), FontSize = 12 };
            }
        };
        selection.SelectedIndex = 0;
    }
}

public partial class MainWindow
{
    private void Licenses_Click(object sender, RoutedEventArgs e) => new LicenseWindow(this).ShowDialog();
}
