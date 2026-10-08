using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Documents;
using System.Xml.Linq;
using BetterClipboard;
using BetterClipboard.Models;
using BetterClipboard.Services;

internal static class Program
{
    private static int checks;

    [STAThread]
    private static void Main(string[] args)
    {
        // Keep every persisted test file inside the caller's scratch directory.
        var root = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(root);
        var paths = (AppPaths)RuntimeHelpers.GetUninitializedObject(typeof(AppPaths));
        typeof(AppPaths).GetField("<Root>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(paths, root);
        Directory.CreateDirectory(paths.LogDirectory);
        Directory.CreateDirectory(paths.ImageDirectory);
        File.WriteAllText(paths.SettingsFile, "{}");
        var settings = new SettingsService(paths);
        // WPF otherwise resolves relative theme URIs against this test executable.
        typeof(Application).GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(field => field.FieldType == typeof(Assembly)).SetValue(null, typeof(App).Assembly);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        // Load the real styles without starting clipboard monitoring or opening windows.
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var resources = XDocument.Load("BetterClipboard/App.xaml").Root!
            .Element(wpf + "Application.Resources")!.Element(wpf + "ResourceDictionary")!;
        resources.SetAttributeValue(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml");
        app.Resources = (ResourceDictionary)XamlReader.Parse(resources.ToString());
        ThemeManager.Apply(settings.Settings);
        var log = new DiagnosticLog(paths);
        var store = new ClipboardStore(paths, new EncryptionService(), log);
        var copied = new ClipboardItem { PreviewText = "用户内容 unchanged", ContentLength = 12 };
        store.AddOrUpdate(ClipboardItemKind.Text, copied.PreviewText, copied.PreviewText,
            new SourceAppInfo("test", "test"), DateTimeOffset.Now.AddDays(20), false, "");
        var window = new PopupWindow(store, _ => Task.CompletedTask, settings, log);
        var language = Control<ComboBox>(window, "LanguageBox");
        var tabs = Control<TabControl>(window, "Tabs");
        Check(((TabItem)tabs.Items[0]).Header?.ToString() == "全部", "old settings default to Chinese");
        Control<TextBox>(window, "RetentionDaysBox").Text = "123";
        tabs.SelectedIndex = 2;
        language.SelectedIndex = 1;
        Pump(window);
        Check(((TabItem)tabs.Items[0]).Header?.ToString() == "All", "live tab translation");
        Check(Control<TextBlock>(window, "StatusText").Text == "Language updated.", "live status translation");
        Check(Control<TextBlock>(window, "MemoryUsageText").Text.StartsWith("Memory "), "live memory translation");
        Check(Control<TextBox>(window, "RetentionDaysBox").Text == "123", "unsaved input survives switching");
        Check(new SettingsService(paths).Settings.Language == AppLanguage.English, "language persists");
        Check(new SettingsService(paths).Settings.RetentionDays == 20, "language does not save other pending edits");
        var listItem = new ClipboardListItem(copied);
        Check(listItem.TimeGroup == "Today" && listItem.IsToday, "translated group keeps today identity");
        Check(listItem.KindText == "Text" && listItem.PreviewText == copied.PreviewText, "metadata translated; content preserved");
        Check(AppLocalization.Text("确定删除收藏夹“{0}”吗？其中 {1} 条收藏将移动到“基础收藏夹”。", "工作", 3)
            == "Delete folder “工作”? 3 favorites will move to the default folder.", "dialog formatting preserves user names");
        Check(Control<ComboBox>(window, "FavoriteDestinationBox").Items[0].ToString()!.Contains("Default folder"), "default folder translated");
        Render(window, Path.Combine(root, "settings-en.png"));
        tabs.SelectedIndex = 1;
        Render(window, Path.Combine(root, "favorites-en.png"));
        tabs.SelectedIndex = 0;
        Render(window, Path.Combine(root, "history-en.png"));
        var expanders = Descendants((Visual)window.Content).OfType<Expander>().ToList();
        Check(expanders.Any(expander => expander.IsExpanded), "today remains expanded in English");
        Check(Descendants((Visual)window.Content).OfType<TextBlock>()
            .Any(text => string.Concat(text.Inlines.OfType<Run>().Select(run => run.Text)) == "1 items"),
            "translated group count renders");
        language.SelectedIndex = 0;
        Pump(window);
        Check(((TabItem)tabs.Items[0]).Header?.ToString() == "全部", "switch back to Chinese");
        Check(new SettingsService(paths).Settings.Language == AppLanguage.Chinese, "Chinese persists");
        language.SelectedIndex = 1;
        Pump(window);
        window.Close();
        var reopened = new PopupWindow(store, _ => Task.CompletedTask, new SettingsService(paths), log);
        Check(Control<ComboBox>(reopened, "LanguageBox").SelectedIndex == 1, "reopened window restores English");
        Control<TabControl>(reopened, "Tabs").SelectedIndex = 2;
        var reset = typeof(PopupWindow).GetMethod("ResetDefaults_Click", BindingFlags.Instance | BindingFlags.NonPublic)!;
        reset.Invoke(reopened, [reopened, new RoutedEventArgs(Button.ClickEvent)]);
        Check(Control<TextBlock>(reopened, "DialogTitleText").Text == "Reset defaults", "dialog uses English");
        typeof(PopupWindow).GetMethod("DialogConfirm_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(reopened, [reopened, new RoutedEventArgs(Button.ClickEvent)]);
        Pump(reopened);
        Check(Control<ComboBox>(reopened, "LanguageBox").SelectedIndex == 0 && AppLocalization.Language == AppLanguage.Chinese,
            "reset synchronizes stored language and UI");
        Check(Control<TextBlock>(reopened, "StatusText").Text == "已恢复默认值。", "reset status is Chinese");
        Render(reopened, Path.Combine(root, "settings-zh.png"));
        reopened.Close();
        Console.WriteLine($"Passed {checks} localization integration checks.");
        app.Shutdown();
    }

    private static T Control<T>(Window window, string name) where T : class => (T)window.FindName(name);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        checks++;
    }
    private static void Pump(Window window)
    {
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        if (window.Content is not FrameworkElement content) throw new InvalidOperationException("Missing content");
        Layout(content);
    }
    private static bool Layout(FrameworkElement content)
    {
        content.Measure(new Size(390, 520));
        content.Arrange(new Rect(0, 0, 390, 520));
        content.UpdateLayout();
        return true;
    }
    private static void Render(Window window, string path)
    {
        Pump(window);
        var bitmap = new RenderTargetBitmap(390, 520, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render((Visual)window.Content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(path);
        encoder.Save(output);
    }
}
