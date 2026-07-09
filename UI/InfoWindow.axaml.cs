using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Tonono2.UI;

public partial class InfoWindow : Window
{
    public InfoWindow()
    {
        InitializeComponent();
        Icon = SystemMenu.LoadWindowIcon();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
