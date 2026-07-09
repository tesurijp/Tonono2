using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Tonono2.SKKEngine;
using Tonono2.Win32;

namespace Tonono2.UI;

public partial class TononoUI : Window
{
    private SkkContext? context;
    private bool nativeStyleApplied;

    public TononoUI()
    {
        InitializeComponent();

        Opened += (_, _) =>
        {
            ApplyNativeWindowStyles();
            UpdatePosition();
        };
        PositionChanged += (_, _) => ApplyNativeWindowStyles();
        Resized += (_, _) =>
        {
            if (IsVisible)
            {
                UpdatePosition();
            }
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (context is not null)
        {
            context.PropertyChanged -= OnContextPropertyChanged;
        }

        context = DataContext as SkkContext;
        if (context is not null)
        {
            context.PropertyChanged += OnContextPropertyChanged;
        }

        SyncVisibility();
        base.OnDataContextChanged(e);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SkkContext.IsVisible)
            or nameof(SkkContext.Composition)
            or nameof(SkkContext.CandidateList)
            or nameof(SkkContext.RegistrationReading)
            or nameof(SkkContext.RegistrationWord))
        {
            SyncVisibility();
        }
    }

    private void SyncVisibility()
    {
        if (context is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (context.IsVisible)
            {
                if (!IsVisible)
                {
                    Show();
                }

                ApplyNativeWindowStyles();
                UpdatePosition();
            }
            else if (IsVisible)
            {
                Hide();
            }
        });
    }

    private void ApplyNativeWindowStyles()
    {
        if (!nativeStyleApplied)
        {
            WindowPositioner.SetNonActiveWindow(this);
            nativeStyleApplied = true;
        }
    }

    private void UpdatePosition()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var scale = RenderScaling;
            var (posX, posY) = WindowPositioner.GetTargetPosition(scale, scale, Bounds.Width, Bounds.Height);
            if (double.IsNaN(posX) || double.IsNaN(posY))
            {
                return;
            }

            Position = new PixelPoint(
                (int)Math.Round(posX * scale),
                (int)Math.Round(posY * scale));
        });
    }
}
