using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using NexSpace.Domain;
using NexSpace.Windows;

namespace NexSpace.WebLab;

internal record Request(string Kind, int? Id, bool? Collapsed, int? Width, int? Height, int? X, int? Y, string? Group);

internal sealed class Program : System.Windows.Application
{
    private static readonly string[] Groups = ["files", "games", "apps"];
    private readonly Dictionary<string, Window> windows = new();
    private readonly WindowsContainerItemPlatformService shell = new();
    private readonly string root = Path.Combine(Path.GetTempPath(), $"NexSpace-WebLab-A-{Environment.ProcessId}-{Guid.NewGuid():N}");
    private CoreWebView2Environment environment = null!;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };
    [STAThread]
    public static void Main() => new Program().Run();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            if (Directory.Exists(root)) throw new IOException("Demo session already exists.");
            Directory.CreateDirectory(root);
            for (int i = 0; i < 18; i++) File.WriteAllText(Fixture(i), $"NexSpace · Ficha de demostración {i}");
            environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(root, "webview"));
            foreach (var role in Groups.Append("dock"))
            {
                if (e.Args.Contains("--one") && role != "files" || e.Args.Contains("--three") && role == "dock") continue;
                await ShowSurface(role);
            }
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(root, "startup-error.txt"), ex.ToString()); Shutdown(1); }
    }
    private string Fixture(int id) => Path.Combine(root, $"reference-{id}.txt");
    private static Rect Bounds(string role, string group)
    {
        var area = SystemParameters.WorkArea;
        var dockX = area.Left + (area.Width - 530) / 2;
        var dockY = area.Bottom - 100;
        return role switch
        {
            "apps" => new(area.Right - 410, area.Top + 24, 386, 272),
            "games" => new(area.Left + 24, area.Top + 314, 386, 272),
            "dock" => new(dockX, dockY, 530, 76),
            "popup" => new(Math.Clamp(dockX + 119 + 66 * Array.IndexOf(Groups, group) - 114, area.Left, Math.Max(area.Left, area.Right - 228)), dockY - 268, 228, 266),
            _ => new(area.Left + 24, area.Top + 24, 386, 272)
        };
    }
    private async Task ShowSurface(string role, string group = "files")
    {
        var bounds = Bounds(role, group);
        var view = new WebView2CompositionControl { DefaultBackgroundColor = System.Drawing.Color.Transparent };
        var window = new Window
        {
            Title = $"NexSpace A · {role}",
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            ResizeMode = ResizeMode.NoResize,
            Background = Brushes.Transparent,
            Content = view,
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height
        };
        windows.Add(role, window);
        var expanded = window.Height;
        window.Closed += (_, _) => { windows.Remove(role); view.Dispose(); };
        window.Show();
        await view.EnsureCoreWebView2Async(environment);
        var core = view.CoreWebView2;
        var url = $"https://nexspace.test/index.html?role={role}&group={group}&exercise={(Environment.GetCommandLineArgs().Contains("--exercise") ? 1 : 0)}";
        core.SetVirtualHostNameToFolderMapping("nexspace.test", Path.Combine(AppContext.BaseDirectory, "www"), CoreWebView2HostResourceAccessKind.DenyCors);
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.NewWindowRequested += (_, args) => args.Handled = true;
        core.NavigationStarting += (_, args) => args.Cancel = args.Uri != url;
        core.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
        core.WebMessageReceived += async (_, args) =>
        {
            if (args.Source != url) return;
            try
            {
                var json = args.WebMessageAsJson;
                if (json.Length > 512) return;
                var r = JsonSerializer.Deserialize<Request>(json, Json);
                bool panel = Groups.Contains(role);
                switch (r?.Kind)
                {
                    case "ready":
                        File.AppendAllText(Path.Combine(root, role + ".ready"), ".");
                        var accent = new global::Windows.UI.ViewManagement.UISettings().GetColorValue(global::Windows.UI.ViewManagement.UIColorType.Accent);
                        core.PostWebMessageAsJson(JsonSerializer.Serialize(new { accent = $"#{accent.R:X2}{accent.G:X2}{accent.B:X2}" }));
                        break;
                    case "open" when r.Id is >= 0 and < 18 && (role == "popup" || Array.IndexOf(Groups, role) == r.Id / 6):
                        var path = Fixture(r.Id.Value);
                        var item = new ContainerItemReference(Guid.NewGuid(), ContainerItemKind.File, path, path, Path.GetFileName(path), 0, DateTimeOffset.UtcNow);
                        if (!shell.Open(item).Succeeded) core.PostWebMessageAsJson("{\"error\":true}");
                        break;
                    case "move" when panel && r.X.HasValue && r.Y.HasValue:
                        var area = SystemParameters.WorkArea;
                        window.Left = Math.Clamp(r.X.Value, area.Left, Math.Max(area.Left, area.Right - window.Width));
                        window.Top = Math.Clamp(r.Y.Value, area.Top, Math.Max(area.Top, area.Bottom - window.Height));
                        break;
                    case "size" when panel && r.Width.HasValue && r.Height.HasValue:
                        window.Width = Math.Clamp(r.Width.Value, 220, 880);
                        window.Left = Math.Clamp(window.Left, SystemParameters.WorkArea.Left, Math.Max(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Right - window.Width));
                        window.Height = Math.Clamp(r.Height.Value, 150, 680);
                        window.Top = Math.Clamp(window.Top, SystemParameters.WorkArea.Top, Math.Max(SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Bottom - window.Height));
                        break;
                    case "collapse" when panel && r.Collapsed.HasValue:
                        if (r.Collapsed.Value && window.Height > 60) expanded = window.Height;
                        window.Height = r.Collapsed.Value ? 52 : expanded;
                        break;
                    case "popover" when role == "dock" && Groups.Contains(r.Group):
                        windows.GetValueOrDefault("popup")?.Close();
                        await ShowSurface("popup", r.Group!);
                        break;
                    case "dismiss" when role == "popup" || role == "dock": windows.GetValueOrDefault("popup")?.Close(); break;
                    case "recover":
                        windows.GetValueOrDefault("popup")?.Close();
                        foreach (var (name, w) in windows) { var b = Bounds(name, "files"); w.Left = b.Left; w.Top = b.Top; w.WindowState = WindowState.Normal; w.Activate(); }
                        break;
                    case "close" when role == "dock" || Environment.GetCommandLineArgs().Contains("--measure"): Shutdown(); break;
                }
            }
            catch (Exception ex) { File.AppendAllText(Path.Combine(root, "messages.log"), ex.GetType().Name + Environment.NewLine); }
        };
        core.Navigate(url);
    }

}
