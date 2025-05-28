using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using ReAuthenticatePoC.Extensions;

namespace ReAuthenticatePoC;

public class ProgramState : IDisposable
{
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

    private IDisposable[] _subscriptions;

    public ProgramState()
    {
        SteamClient = new SteamClient();
        Manager = new CallbackManager( SteamClient );
        SteamUser = SteamClient.GetHandler<SteamUser>() ?? throw new InvalidOperationException();
        _subscriptions = [
            Manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected),
            Manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff),
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

    public async ValueTask Stop()
    {
        await _runTokenSource.CancelAsync();
    }

    public void Dispose()
    {
        _runTokenSource.Dispose();
        foreach (var subscription in _subscriptions) subscription.Dispose();
    }
}
