using System;
using System.Threading;
using System.Threading.Tasks;
using ReAuthenticatePoC.Extensions;
using SteamKit2;

namespace ReAuthenticatePoC.Commands;

public class LogOffCommand(ProgramState state)
{
    public async ValueTask Run(CancellationToken cancellationToken = default)
    {
        if (!state.IsLoggedOn) throw new InvalidOperationException("Not logged on");

        var loggedOffTaskSource = new TaskCompletionSource<SteamUser.LoggedOffCallback>();
        using var _ = state.Manager.Subscribe<SteamUser.LoggedOffCallback>(loggedOffTaskSource.SetResult);
        Console.WriteLine("Logging off...");
        state.SteamUser.LogOff();
        await state.Manager.RunUntil(loggedOffTaskSource.Task, cancellationToken: cancellationToken);
        var callback = loggedOffTaskSource.Task.Result;
        Console.WriteLine($"Logged off: {callback.Result}");
        state.IsLoggedOn = false;
        state.ClientSteamId = null;
    }
}
