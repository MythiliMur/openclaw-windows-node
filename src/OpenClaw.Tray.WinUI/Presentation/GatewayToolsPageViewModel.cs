using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenClawTray.Presentation;

/// <summary>WinUI-free presentation owner for the Gateway Tools Hub page.</summary>
internal sealed class GatewayToolsPageViewModel : INotifyPropertyChanged
{
    private readonly IGatewayToolsBrokerClient _broker;
    private bool _isBusy;
    private string? _errorMessage;
    private string? _selectedRegistrationId;

    public GatewayToolsPageViewModel(IGatewayToolsBrokerClient broker)
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<GatewayToolSummary> Tools { get; } = [];
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }
    public string? ErrorMessage { get => _errorMessage; private set => Set(ref _errorMessage, value); }
    public string? SelectedRegistrationId { get => _selectedRegistrationId; set => Set(ref _selectedRegistrationId, value); }
    public GatewayToolSummary? SelectedTool => Tools.FirstOrDefault(x => x.RegistrationId == SelectedRegistrationId);
    public bool HasTools => Tools.Count != 0;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var tools = await _broker.ListToolsAsync(cancellationToken).ConfigureAwait(false);
            ReplaceTools(tools);
        }).ConfigureAwait(false);
    }

    public Task RegisterDesktopToolAsync(string command, string executablePath, CancellationToken cancellationToken = default) =>
        RegisterAsync(new RegisterGatewayToolRequest(command, GatewayToolSource.Desktop, executablePath), cancellationToken);

    public Task ScanGatewayToolsAsync(CancellationToken cancellationToken = default) =>
        RunOperationAsync(() => _broker.ScanGatewayToolsAsync(cancellationToken), refresh: true);

    public Task VerifySelectedAsync(CancellationToken cancellationToken = default) =>
        WithSelectedAsync(id => _broker.VerifyToolAsync(id, cancellationToken));

    public Task SetSelectedEnabledAsync(bool enabled, CancellationToken cancellationToken = default) =>
        WithSelectedAsync(id => _broker.SetToolEnabledAsync(id, enabled, cancellationToken));

    public Task UnregisterSelectedAsync(CancellationToken cancellationToken = default) =>
        WithSelectedAsync(id => _broker.UnregisterToolAsync(id, cancellationToken));

    public Task CreateRuntimeProfileForSelectedAsync(CancellationToken cancellationToken = default) =>
        WithSelectedAsync(id => _broker.CreateRuntimeProfileAsync(id, cancellationToken));

    public Task StartInteractiveSetupForSelectedAsync(CancellationToken cancellationToken = default) =>
        WithSelectedAsync(id => _broker.StartInteractiveSetupAsync(id, cancellationToken));

    private Task RegisterAsync(RegisterGatewayToolRequest request, CancellationToken cancellationToken) =>
        RunOperationAsync(() => _broker.RegisterToolAsync(request, cancellationToken), refresh: true);

    private Task WithSelectedAsync(Func<string, Task<GatewayToolOperationResult>> operation)
    {
        var selected = SelectedRegistrationId;
        if (string.IsNullOrWhiteSpace(selected))
        {
            ErrorMessage = "Select a tool first.";
            return Task.CompletedTask;
        }
        return RunOperationAsync(() => operation(selected), refresh: true);
    }

    private async Task RunOperationAsync(Func<Task<GatewayToolOperationResult>> operation, bool refresh)
    {
        await RunAsync(async () =>
        {
            var result = await operation().ConfigureAwait(false);
            if (!result.Succeeded)
            {
                ErrorMessage = result.UserMessage ?? "The broker could not complete this operation.";
                return;
            }
            if (refresh)
                ReplaceTools(await _broker.ListToolsAsync().ConfigureAwait(false));
        }).ConfigureAwait(false);
    }

    private async Task RunAsync(Func<Task> operation)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try { await operation().ConfigureAwait(false); }
        catch (Exception ex) { ErrorMessage = ex is OperationCanceledException ? "The request was cancelled." : "The Gateway Tools broker is unavailable."; }
        finally { IsBusy = false; }
    }

    private void ReplaceTools(IReadOnlyList<GatewayToolSummary> tools)
    {
        var previousSelection = SelectedRegistrationId;
        Tools.Clear();
        foreach (var tool in tools.OrderBy(x => x.Command, StringComparer.OrdinalIgnoreCase)) Tools.Add(tool);
        OnPropertyChanged(nameof(HasTools));
        SelectedRegistrationId = Tools.Any(x => x.RegistrationId == previousSelection)
            ? previousSelection : Tools.FirstOrDefault()?.RegistrationId;
        OnPropertyChanged(nameof(SelectedTool));
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
        if (name == nameof(SelectedRegistrationId)) OnPropertyChanged(nameof(SelectedTool));
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
