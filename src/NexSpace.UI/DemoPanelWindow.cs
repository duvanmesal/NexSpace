using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NexSpace.Application;
using Windows.Graphics;

namespace NexSpace.UI;

public sealed class DemoPanelWindow : Window
{
    public DemoPanelWindow(WindowDescriptor descriptor)
    {
        Title = descriptor.Title;
        ExtendsContentIntoTitleBar = true;
        AppWindow.Resize(new SizeInt32((int)descriptor.Width, (int)descriptor.Height));
        AppWindow.Move(new PointInt32(80, 100));

        var root = new Grid { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var chrome = new Grid { Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(245, 23, 34, 41)), Padding = new Thickness(16, 12, 10, 12) };
        chrome.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        chrome.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel { Spacing = 2 };
        title.Children.Add(new TextBlock { Text = "ESPACIO DE TRABAJO", FontSize = 10, CharacterSpacing = 90, Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 101, 214, 194)) });
        title.Children.Add(new TextBlock { Text = "Panel de prueba", FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        chrome.Children.Add(title);
        var close = new Button { Content = "Cerrar", FontSize = 12, Padding = new Thickness(8, 0, 8, 2), VerticalAlignment = VerticalAlignment.Center };
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 1); chrome.Children.Add(close);
        Grid.SetRow(chrome, 0); root.Children.Add(chrome);
        var content = new StackPanel { Spacing = 12, Padding = new Thickness(18), Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(232, 20, 29, 35)) };
        content.Children.Add(new TextBlock { Text = "Un lugar para cada cosa.", FontSize = 19, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        content.Children.Add(new TextBlock { Text = "Esta muestra utiliza contenido ficticio. No se leen, copian ni cambian archivos del usuario.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 183, 197, 201)) });
        var tag = new Border { Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 30, 51, 56)), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8, 12, 8), HorizontalAlignment = HorizontalAlignment.Left };
        tag.Child = new TextBlock { Text = "●  FLOATING · interactivo", Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 101, 214, 194)), FontSize = 12 };
        content.Children.Add(tag);
        Grid.SetRow(content, 1); root.Children.Add(content);
        Content = root;
        SetTitleBar(chrome);
    }

    public void MoveAndResize(int x, int y, int width, int height) => AppWindow.MoveAndResize(new global::Windows.Graphics.RectInt32(x, y, width, height));
}
