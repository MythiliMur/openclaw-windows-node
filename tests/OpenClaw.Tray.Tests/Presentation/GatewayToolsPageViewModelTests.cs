using OpenClawTray.Presentation;

namespace OpenClaw.Tray.Tests.Presentation;

public sealed class GatewayToolsPageViewModelTests
{
    [Fact]
    public async Task RefreshAsync_SortsBrokerTools_AndSelectsFirstTool()
    {
        var broker = new FakeBroker
        {
            Tools =
            [
                Tool("zeta", "2"),
                Tool("alpha", "1"),
            ],
        };
        var viewModel = new GatewayToolsPageViewModel(broker);

        await viewModel.RefreshAsync();

        Assert.Collection(viewModel.Tools,
            first => Assert.Equal("alpha", first.Command),
            second => Assert.Equal("zeta", second.Command));
        Assert.Equal("1", viewModel.SelectedRegistrationId);
        Assert.True(viewModel.HasTools);
    }

    [Fact]
    public async Task VerifySelectedAsync_UsesBrokerRegistrationId_ThenRefreshes()
    {
        var broker = new FakeBroker { Tools = [Tool("tool", "registered")] };
        var viewModel = new GatewayToolsPageViewModel(broker);
        await viewModel.RefreshAsync();

        await viewModel.VerifySelectedAsync();

        Assert.Equal("registered", broker.VerifiedRegistrationId);
        Assert.Equal(2, broker.ListCalls);
    }

    [Fact]
    public async Task RegisterDesktopToolAsync_SendsOnlyExplicitUserSelection()
    {
        var broker = new FakeBroker();
        var viewModel = new GatewayToolsPageViewModel(broker);

        await viewModel.RegisterDesktopToolAsync("sample", "chosen-by-user.exe");

        Assert.Equal("sample", broker.RegisterRequest?.Command);
        Assert.Equal(GatewayToolSource.Desktop, broker.RegisterRequest?.Source);
        Assert.Equal("chosen-by-user.exe", broker.RegisterRequest?.SelectedExecutablePath);
    }

    private static GatewayToolSummary Tool(string command, string id) =>
        new(id, command, "Ready", "Created", "Not configured", true);

    private sealed class FakeBroker : IGatewayToolsBrokerClient
    {
        public IReadOnlyList<GatewayToolSummary> Tools { get; set; } = [];
        public int ListCalls { get; private set; }
        public string? VerifiedRegistrationId { get; private set; }
        public RegisterGatewayToolRequest? RegisterRequest { get; private set; }
        public Task<IReadOnlyList<GatewayToolSummary>> ListToolsAsync(CancellationToken cancellationToken = default) { ListCalls++; return Task.FromResult(Tools); }
        public Task<GatewayToolOperationResult> RegisterToolAsync(RegisterGatewayToolRequest request, CancellationToken cancellationToken = default) { RegisterRequest = request; return Success(); }
        public Task<GatewayToolOperationResult> ScanGatewayToolsAsync(CancellationToken cancellationToken = default) => Success();
        public Task<GatewayToolOperationResult> VerifyToolAsync(string registrationId, CancellationToken cancellationToken = default) { VerifiedRegistrationId = registrationId; return Success(); }
        public Task<GatewayToolOperationResult> SetToolEnabledAsync(string registrationId, bool enabled, CancellationToken cancellationToken = default) => Success();
        public Task<GatewayToolOperationResult> UnregisterToolAsync(string registrationId, CancellationToken cancellationToken = default) => Success();
        public Task<GatewayToolOperationResult> CreateRuntimeProfileAsync(string registrationId, CancellationToken cancellationToken = default) => Success();
        public Task<GatewayToolOperationResult> StartInteractiveSetupAsync(string registrationId, CancellationToken cancellationToken = default) => Success();
        private static Task<GatewayToolOperationResult> Success() => Task.FromResult(new GatewayToolOperationResult(true));
    }
}
