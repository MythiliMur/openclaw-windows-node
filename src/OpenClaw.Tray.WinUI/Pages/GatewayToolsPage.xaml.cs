using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.IO;
using OpenClawTray.Helpers;
using OpenClawTray.Presentation;

namespace OpenClawTray.Pages;

/// <summary>Thin WinUI host for the broker-backed, provider-neutral Gateway Tools view model.</summary>
public sealed partial class GatewayToolsPage : Page
{
    private GatewayToolsPageViewModel? _viewModel;

    public GatewayToolsPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public void Initialize() => _ = _viewModel?.RefreshAsync();

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        _viewModel = args.NewValue as GatewayToolsPageViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(GatewayToolsPageViewModel.ErrorMessage))
                {
                    ErrorInfoBar.Message = _viewModel.ErrorMessage ?? string.Empty;
                    ErrorInfoBar.IsOpen = !string.IsNullOrWhiteSpace(_viewModel.ErrorMessage);
                }
            };
        }
    }

    private async void OnAddDesktopToolClick(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        var owner = app.ActiveHubWindow is null ? IntPtr.Zero : WinRT.Interop.WindowNative.GetWindowHandle(app.ActiveHubWindow);
        var path = await Win32FilePickerHelper.PickSingleFileAsync(owner, "Choose tool executable");
        if (string.IsNullOrWhiteSpace(path) || _viewModel is null) return;
        var command = Path.GetFileNameWithoutExtension(path);
        await _viewModel.RegisterDesktopToolAsync(command, path);
    }

    private async void OnScanGatewayToolsClick(object sender, RoutedEventArgs e) { if (_viewModel is not null) await _viewModel.ScanGatewayToolsAsync(); }
    private async void OnRefreshClick(object sender, RoutedEventArgs e) { if (_viewModel is not null) await _viewModel.RefreshAsync(); }
    private async void OnVerifyClick(object sender, RoutedEventArgs e) { if (_viewModel is not null) await _viewModel.VerifySelectedAsync(); }
    private async void OnCreateProfileClick(object sender, RoutedEventArgs e) { if (_viewModel is not null) await _viewModel.CreateRuntimeProfileForSelectedAsync(); }
    private async void OnInteractiveSetupClick(object sender, RoutedEventArgs e) { if (_viewModel is not null) await _viewModel.StartInteractiveSetupForSelectedAsync(); }
    private async void OnEnableClick(object sender, RoutedEventArgs e) { if (_viewModel is not null) await _viewModel.SetSelectedEnabledAsync(true); }
    private async void OnDisableClick(object sender, RoutedEventArgs e) { if (_viewModel is not null) await _viewModel.SetSelectedEnabledAsync(false); }
    private async void OnRemoveClick(object sender, RoutedEventArgs e) { if (_viewModel is not null) await _viewModel.UnregisterSelectedAsync(); }
}
