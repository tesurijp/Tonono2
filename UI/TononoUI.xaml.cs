using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Tonono2.SKKEngine;
using Tonono2.Win32;

namespace Tonono2.UI;

public class TononoViewModel(SkkEngine engine) : INotifyPropertyChanged
{
    public string StatusText => $"[{GetStateDisplay()}]{(engine.RecursionDepth > 0 ? $":{engine.RecursionDepth}" : "")}";
    public string InputText => engine.Composition;
    public string CandidateListText => engine.CandidateList;
    public bool IsInRegistrationMode => engine.IsInRegistrationMode;
    public string RegistrationReading => engine.RegistrationReading;
    public string RegistrationWord => engine.RegistrationWord;

    public bool IsVisible => engine.IsInRegistrationMode || !string.IsNullOrEmpty(engine.Composition);

    private string GetStateDisplay() => engine.State switch
    {
        SkkState.Hiragana => "あ",
        SkkState.Katakana => "ア",
        SkkState.Zenkaku => "全",
        _ => "？"
    };

    public void Update()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(InputText));
        OnPropertyChanged(nameof(CandidateListText));
        OnPropertyChanged(nameof(IsInRegistrationMode));
        OnPropertyChanged(nameof(RegistrationReading));
        OnPropertyChanged(nameof(RegistrationWord));
        OnPropertyChanged(nameof(IsVisible));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new(propertyName));
}

public partial class TononoUI : Window
{
    private readonly TononoViewModel _viewModel;

    public TononoUI(SkkController controller)
    {
        InitializeComponent();
        _viewModel = new(controller.Engine);
        DataContext = _viewModel;

        controller.RequestUiUpdate = () => Dispatcher.Invoke(UpdateUI);
        UpdateUI();
    }

    private void UpdateUI()
    {
        _viewModel.Update();
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        var source = PresentationSource.FromVisual(this);
        var m = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var (posX, posY) = WindowPositioner.GetTargetPosition(m.M11, m.M22, ActualWidth, ActualHeight);
        Left = !double.IsNaN(posX) ? posX : Left;
        Top = !double.IsNaN(posY) ? posY : Top;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) => WindowPositioner.SetNonActiveWindow(this);
}
