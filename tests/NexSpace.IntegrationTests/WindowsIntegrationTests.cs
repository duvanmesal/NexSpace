using NexSpace.Windows;
using NexSpace.Application;
using NexSpace.Domain;
using System.Runtime.InteropServices;

namespace NexSpace.IntegrationTests;

public class WindowsIntegrationTests
{
    [Fact]
    public void Monitor_probe_returns_valid_work_areas_without_mutating_windows()
    {
        var displays = new WindowsDisplayService().GetDisplays();
        Assert.NotEmpty(displays);
        Assert.Contains(displays, display => display.IsPrimary);
        Assert.All(displays, display =>
        {
            Assert.True(display.Width > 0);
            Assert.True(display.Height > 0);
            Assert.True(display.WorkWidth > 0);
            Assert.True(display.WorkHeight > 0);
            Assert.True(display.Dpi > 0);
        });
    }

    [Fact]
    public void Resource_probe_reports_missing_and_allowlisted_system_targets_without_opening_them()
    {
        var platform = new WindowsContainerItemPlatformService();
        var missing = new ContainerItemReference(Guid.NewGuid(), ContainerItemKind.File,
            Path.Combine(Path.GetTempPath(), "NexSpace-does-not-exist-" + Guid.NewGuid().ToString("N")), "missing", "Missing", 0, DateTimeOffset.UtcNow);
        var missingResult = platform.Inspect(missing);
        Assert.False(missingResult.Exists);
        Assert.Equal("missing", missingResult.Code);

        var system = missing with { Kind = ContainerItemKind.SystemResource, Target = "shell:Downloads" };
        Assert.True(platform.Inspect(system).Exists);
        var unsupported = system with { Target = "file:///C:/not-an-approved-system-target" };
        Assert.Equal("unsupported-resource", platform.Inspect(unsupported).Code);
    }

    [Fact]
    public void Emergency_hotkey_routes_to_an_unfocused_owned_window_and_unregisters()
    {
        var hwnd = CreateWindowEx(0, "STATIC", "NexSpace recovery test", 0, 0, 0, 1, 1, 0, 0, GetModuleHandle(null), 0);
        Assert.NotEqual(nint.Zero, hwnd);
        try
        {
            var calls = 0;
            const int testHotkeyId = 0x4E59;
            const uint testChord = 0x4000 | 0x0001 | 0x0002 | 0x0004;
            var result = GlobalEmergencyHotkey.Register(hwnd, () => calls++, testHotkeyId, testChord, 0x7A, out var registration);
            Assert.True(result.Succeeded, result.Message);
            using (registration)
            {
                Assert.True(registration!.IsRegistered);
                SendMessage(hwnd, 0x0312, (nint)testHotkeyId, 0);
                Assert.Equal(1, calls);
            }
            Assert.False(registration.IsRegistered);
        }
        finally { DestroyWindow(hwnd); }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowEx(uint exStyle, string className, string windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [Fact]
    public void Tray_registers_routes_v4_callback_and_disposes_repeatedly_on_own_test_hwnd()
    {
        var hwnd = CreateWindowEx(0,"STATIC","NexSpace isolated tray test",0,0,0,1,1,0,0,GetModuleHandle(null),0);
        Assert.NotEqual(nint.Zero,hwnd);
        try
        {
            var calls=0;
            for(var i=0;i<3;i++)
            {
                var icon=new WindowsTrayIcon(hwnd,()=>calls++,()=>{},()=>{},()=>{},()=>[],_=>{});
                SendMessage(hwnd,0x8000+70,0,(nint)0x400); Assert.Equal(i+1,calls);
                icon.Dispose();icon.Dispose();SendMessage(hwnd,0x8000+70,0,(nint)0x400);Assert.Equal(i+1,calls);
            }
        }
        finally { DestroyWindow(hwnd); }
    }
    [Fact]
    public void Startup_rejects_untrusted_path_without_writing_registration()
    {
        var service=new WindowsStartupService();var before=service.GetStatus();
        Assert.False(service.Enable("relative.exe").Succeeded);
        Assert.False(service.Enable(Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".exe")).Succeeded);
        Assert.Equal(before,service.GetStatus());
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? moduleName);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
}
