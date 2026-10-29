using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PhoneAudioBluetooth.ViewModels;

namespace PhoneAudioBluetooth;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel(DispatcherQueue.GetForCurrentThread());
        RootGrid.DataContext = _viewModel;

        Activated += OnActivated;
        Closed += OnClosed;
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _viewModel.InitializeAsync();
    }

    private async void RefreshDevicesButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshDevicesAsync();
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.ConnectAsync();
    }

    private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.DisconnectAsync();
    }

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        await _viewModel.DisposeAsync();
    }
}
