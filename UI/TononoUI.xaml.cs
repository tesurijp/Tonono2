using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Tonono2.SKKEngine;
using Tonono2.Win32;

namespace Tonono2.UI;

public partial class TononoUI : Window
{
    public TononoUI()
    {
        InitializeComponent();

        Loaded += (_, _) => WindowPositioner.SetNonActiveWindow(this);
        IsVisibleChanged += (_, e) => _ = e.NewValue is true && UpdatePosition();
    }

    private bool UpdatePosition()
    {
        Dispatcher.InvokeAsync(() =>
        {
            var source = PresentationSource.FromVisual(this);
            var m = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
            var (posX, posY) = WindowPositioner.GetTargetPosition(m.M11, m.M22, ActualWidth, ActualHeight);
            if (!double.IsNaN(posX))
            {
                Left = posX;
            }
            if (!double.IsNaN(posY))
            {
                Top = posY;
            }
        });
        return true;
    }
}
