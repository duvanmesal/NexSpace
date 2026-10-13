using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using NexSpace.Domain;

namespace NexSpace.UI.Visuals;

/// <summary>
/// Maps a workspace/category <see cref="Guid"/> to its stable identity color (see
/// <see cref="WorkspaceColor"/>) as a SolidColorBrush, so XAML templates can show the
/// per-workspace color stripe/dot without any code-behind per item.
/// </summary>
public sealed class WorkspaceColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var id = value is Guid g ? g : Guid.Empty;
        var hex = WorkspaceColor.ForId(id);
        return new SolidColorBrush(global::Windows.UI.Color.FromArgb(255,
            System.Convert.ToByte(hex.Substring(1, 2), 16),
            System.Convert.ToByte(hex.Substring(3, 2), 16),
            System.Convert.ToByte(hex.Substring(5, 2), 16)));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
