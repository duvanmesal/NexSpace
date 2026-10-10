using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using NexSpace.Domain;
using NexSpace.Presentation;
using NexSpace.UI.Visuals;

namespace NexSpace.UI.Controls;

public sealed partial class ReferenceTile : UserControl
{
    private CancellationTokenSource? _lifetime;
    public ReferenceTile()
    {
        InitializeComponent();
        Loaded += (_, _) => BindItem();
        DataContextChanged += (_, _) => { if (IsLoaded) BindItem(); };
        Unloaded += (_, _) => CancelIcon();
    }

    private void CancelIcon() { _lifetime?.Cancel(); _lifetime?.Dispose(); _lifetime = null; NativeIcon.Source = null; }
    private async void BindItem()
    {
        CancelIcon();
        if (DataContext is not ContainerItemViewModel item) return;
        AutomationProperties.SetName(this, item.AccessibleName);
        ToolTipService.SetToolTip(this, item.AccessibleName);
        var grid = item.IsGrid;
        LayoutRoot.RowDefinitions[1].Height = grid ? GridLength.Auto : new GridLength(0);
        LayoutRoot.ColumnDefinitions[0].Width = grid ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        LayoutRoot.ColumnDefinitions[1].Width = grid ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetRow(Labels, grid ? 1 : 0); Grid.SetColumn(Labels, grid ? 0 : 1);
        NameLabel.TextAlignment = grid ? TextAlignment.Center : TextAlignment.Left;
        IconHost.Width = IconHost.Height = grid ? 40 : 28;
        ErrorLabel.Visibility = item.Status.Exists ? Visibility.Collapsed : Visibility.Visible;
        FallbackIcon.Glyph = item.Reference.Kind switch { ContainerItemKind.Folder => "\uE8B7", ContainerItemKind.Application => "\uE71D", ContainerItemKind.Shortcut => "\uE8A7", ContainerItemKind.SystemResource => "\uE80F", _ => "\uE8A5" };
        FallbackIcon.Visibility = Visibility.Visible;
        _lifetime = new CancellationTokenSource();
        var token = _lifetime.Token;
        try
        {
            var icon = await ShellIconLoader.LoadAsync(item.Reference, token);
            if (token.IsCancellationRequested || !IsLoaded || !ReferenceEquals(DataContext, item)) return;
            NativeIcon.Source = icon;
            if (icon is not null) FallbackIcon.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException) { }
        catch { /* A system glyph remains available if icon decoding fails. */ }
    }
}
