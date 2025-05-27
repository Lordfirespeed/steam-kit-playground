using System;
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

    public TokenSet? TokenSet { get; set; }

    private IDisposable[] _subscriptions;

    public ProgramState()
    {
        SteamClient = new SteamClient();
        Manager = new CallbackManager( SteamClient );
        SteamUser = SteamClient.GetHandler<SteamUser>() ?? throw new InvalidOperationException();
        _subscriptions = [
            Manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected),
        ];
    }

    private async ValueTask OnDisconnected(SteamClient.DisconnectedCallback callback)
    {
        Console.WriteLine("Disconnected from Steam");
        await Stop();
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
