using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using ReAuthenticatePoC.Extensions;

namespace ReAuthenticatePoC;

public class ProgramState : IDisposable
{
    private static readonly FileInfo AuthenticationDataFile = new(Path.Join(Environment.CurrentDirectory, "auth.json"));
    private static readonly FileInfo LicensesDataFile = new(Path.Join(Environment.CurrentDirectory, "licenses.json"));

    private readonly CancellationTokenSource _runTokenSource = new();
    public CancellationToken RunToken => _runTokenSource.Token;
    public bool IsRunning => !_runTokenSource.IsCancellationRequested;

    public SteamClient SteamClient { get; }
    public CallbackManager Manager { get; }
    public SteamUser SteamUser { get; }

    [MemberNotNullWhen(true, nameof(AccountName), nameof(TokenSet))]
    public bool HasAuthenticated { get; set; } = false;
    public string? AccountName { get; set; }
    public TokenSet? TokenSet { get; set; }

    [MemberNotNullWhen(true, nameof(ClientSteamId))]
    public bool IsLoggedOn { get; set; } = false;
    public SteamID? ClientSteamId { get; set; }

    private readonly IDisposable[] _subscriptions;

    public ProgramState()
    {
        SteamClient = new SteamClient();
        Manager = new CallbackManager( SteamClient );
        SteamUser = SteamClient.GetHandler<SteamUser>() ?? throw new InvalidOperationException();
        _subscriptions = [
            Manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected),
            Manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff),
            Manager.Subscribe<SteamApps.LicenseListCallback>(OnLicenseList),
        ];
    }

    private async ValueTask OnDisconnected(SteamClient.DisconnectedCallback callback)
    {
        Console.WriteLine("Disconnected from Steam");
        await Stop();
    }

    private async ValueTask OnLoggedOff(SteamUser.LoggedOffCallback callback)
    {
        Console.WriteLine($"Logged off: {callback.Result}");
        IsLoggedOn = false;
        ClientSteamId = null;
    }

    private async ValueTask OnLicenseList(SteamApps.LicenseListCallback callback)
    {
        Console.WriteLine($"Got license list: {callback.Result}");
        if (callback.Result is not EResult.OK) return;

        await using var fileStream = LicensesDataFile.OpenWrite();
        await JsonSerializer.SerializeAsync(
            fileStream,
            callback.LicenseList,
            new JsonSerializerOptions { WriteIndented = true }
        );
        Console.WriteLine("Saved license list data");
    }

    public async ValueTask Stop()
    {
        await _runTokenSource.CancelAsync();
    }

    public void Dispose()
    {
        _runTokenSource.Dispose();
        foreach (var subscription in _subscriptions) subscription.Dispose();
    }

    private record AuthenticationData
    {
        public required string AccountName { get; init; }
        public required string AccessToken { get; init; }
        public required string RefreshToken { get; init; }
    }

    public async ValueTask SaveAuthenticationData(CancellationToken cancellationToken = default)
    {
        if (!HasAuthenticated) return;
        var data = new AuthenticationData {
            AccountName = AccountName,
            AccessToken = TokenSet.AccessToken,
            RefreshToken = TokenSet.RefreshToken,
        };
        await using var fileStream = AuthenticationDataFile.OpenWrite();
        await JsonSerializer.SerializeAsync(fileStream, data, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
        Console.WriteLine("Saved authentication data");
    }

    public async ValueTask LoadAuthenticationData(CancellationToken cancellationToken = default)
    {
        if (HasAuthenticated) return;
        await using var fileStream = AuthenticationDataFile.OpenRead();
        var data = await JsonSerializer.DeserializeAsync<AuthenticationData>(fileStream, cancellationToken: cancellationToken);
        if (data is null) throw new InvalidOperationException("No authentication data found");
        AccountName = data.AccountName;
        TokenSet = new TokenSet(data.AccessToken, data.RefreshToken);
        HasAuthenticated = true;
        Console.WriteLine("Loaded authentication data");
    }
}
