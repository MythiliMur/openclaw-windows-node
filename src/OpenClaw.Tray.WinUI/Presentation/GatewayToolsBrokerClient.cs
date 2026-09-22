using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace OpenClawTray.Presentation;

/// <summary>
/// Bounded, secret-free state returned by the package-owned Gateway Tools broker.
/// The Hub intentionally never receives an executable path, raw diagnostics, profile
/// contents, tokens, or an MXC identity.
/// </summary>
internal sealed record GatewayToolSummary(
    string RegistrationId,
    string Command,
    string ExecutableStatus,
    string RuntimeProfileStatus,
    string AuthorizationStatus,
    bool Enabled,
    string? Detail = null);

internal enum GatewayToolSource { Desktop, GatewayToolsFolder }

internal sealed record RegisterGatewayToolRequest(
    string Command,
    GatewayToolSource Source,
    string? SelectedExecutablePath);

internal sealed record GatewayToolOperationResult(bool Succeeded, string? UserMessage = null);

/// <summary>
/// Constrained client contract for the package-owned Gateway Tools broker. The Hub can
/// request user-approved operations, but never owns PATH, shims, ACLs, profiles, or
/// Gateway lifecycle policy.
/// </summary>
internal interface IGatewayToolsBrokerClient
{
    Task<IReadOnlyList<GatewayToolSummary>> ListToolsAsync(CancellationToken cancellationToken = default);
    Task<GatewayToolOperationResult> RegisterToolAsync(RegisterGatewayToolRequest request, CancellationToken cancellationToken = default);
    Task<GatewayToolOperationResult> ScanGatewayToolsAsync(CancellationToken cancellationToken = default);
    Task<GatewayToolOperationResult> VerifyToolAsync(string registrationId, CancellationToken cancellationToken = default);
    Task<GatewayToolOperationResult> SetToolEnabledAsync(string registrationId, bool enabled, CancellationToken cancellationToken = default);
    Task<GatewayToolOperationResult> UnregisterToolAsync(string registrationId, CancellationToken cancellationToken = default);
    Task<GatewayToolOperationResult> CreateRuntimeProfileAsync(string registrationId, CancellationToken cancellationToken = default);
    Task<GatewayToolOperationResult> StartInteractiveSetupAsync(string registrationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Local-only named-pipe transport. The packaging broker owns pipe creation, its ACL,
/// caller authentication, validation, and all privileged operations. This client sends
/// a single JSON request and consumes a bounded response; it performs no elevation or
/// direct system mutation.
/// </summary>
internal sealed class NamedPipeGatewayToolsBrokerClient : IGatewayToolsBrokerClient
{
    private const string PipeName = "OpenClaw.GatewayToolsBroker";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<GatewayToolSummary>> ListToolsAsync(CancellationToken cancellationToken = default) =>
        (await SendAsync<GatewayToolSummary[]>("listTools", null, cancellationToken).ConfigureAwait(false)).Value ?? [];

    public Task<GatewayToolOperationResult> RegisterToolAsync(RegisterGatewayToolRequest request, CancellationToken cancellationToken = default) =>
        OperationAsync("registerTool", request, cancellationToken);
    public Task<GatewayToolOperationResult> ScanGatewayToolsAsync(CancellationToken cancellationToken = default) =>
        OperationAsync("scanGatewayTools", null, cancellationToken);
    public Task<GatewayToolOperationResult> VerifyToolAsync(string registrationId, CancellationToken cancellationToken = default) =>
        OperationAsync("verifyTool", new { registrationId }, cancellationToken);
    public Task<GatewayToolOperationResult> SetToolEnabledAsync(string registrationId, bool enabled, CancellationToken cancellationToken = default) =>
        OperationAsync("setToolEnabled", new { registrationId, enabled }, cancellationToken);
    public Task<GatewayToolOperationResult> UnregisterToolAsync(string registrationId, CancellationToken cancellationToken = default) =>
        OperationAsync("unregisterTool", new { registrationId }, cancellationToken);
    public Task<GatewayToolOperationResult> CreateRuntimeProfileAsync(string registrationId, CancellationToken cancellationToken = default) =>
        OperationAsync("createRuntimeProfile", new { registrationId }, cancellationToken);
    public Task<GatewayToolOperationResult> StartInteractiveSetupAsync(string registrationId, CancellationToken cancellationToken = default) =>
        OperationAsync("startInteractiveSetup", new { registrationId }, cancellationToken);

    private async Task<GatewayToolOperationResult> OperationAsync(string operation, object? payload, CancellationToken cancellationToken)
    {
        var response = await SendAsync<GatewayToolOperationResult>(operation, payload, cancellationToken).ConfigureAwait(false);
        return response.Value ?? new GatewayToolOperationResult(false, response.Error ?? "The Gateway Tools broker did not return a result.");
    }

    private static async Task<BrokerResponse<T>> SendAsync<T>(string operation, object? payload, CancellationToken cancellationToken)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(2_000, cancellationToken).ConfigureAwait(false);
            await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            await writer.WriteLineAsync(JsonSerializer.Serialize(new BrokerRequest(operation, payload), JsonOptions)).ConfigureAwait(false);
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(line)
                ? new BrokerResponse<T>(null, "The Gateway Tools broker returned an empty response.")
                : JsonSerializer.Deserialize<BrokerResponse<T>>(line, JsonOptions) ?? new BrokerResponse<T>(null, "The Gateway Tools broker returned an invalid response.");
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException)
        {
            return new BrokerResponse<T>(null, ex is OperationCanceledException ? "The request was cancelled." : "The Gateway Tools broker is unavailable.");
        }
    }

    private sealed record BrokerRequest(string Operation, object? Payload);
    private sealed record BrokerResponse<T>(T? Value, string? Error);
}
