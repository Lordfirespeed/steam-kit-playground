using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ReAuthenticatePoC.Extensions;
using SteamKit2;

namespace ReAuthenticatePoC.Commands;

public class LogOnCommand(ProgramState state)
{
    public async ValueTask Run(CancellationToken cancellationToken = default)
    {
        if (!state.HasAuthenticated) throw new InvalidOperationException("Not authenticated");

        var loggedOnTaskSource = new TaskCompletionSource<SteamUser.LoggedOnCallback>();
        using var _ = state.Manager.Subscribe<SteamUser.LoggedOnCallback>(loggedOnTaskSource.SetResult);
        Console.WriteLine("Logging on...");
        state.SteamUser.LogOn(new SteamUser.LogOnDetails {
            LoginID = (uint)new Random().Next(int.MinValue, int.MaxValue),
            Username = state.AccountName,
            AccessToken = state.TokenSet?.RefreshToken,
            ShouldRememberPassword = true,
        });
        await state.Manager.RunUntil(loggedOnTaskSource.Task, cancellationToken: cancellationToken);
        var callback = loggedOnTaskSource.Task.Result;
        if (callback.Result is not EResult.OK) {
            Console.WriteLine($"Unable to logon to steam: {callback.Result} / {callback.ExtendedResult}");
            return;
        }
        Debug.Assert(callback.ClientSteamID is not null, $"{nameof(callback.ClientSteamID)} should not be null");
        Debug.Assert(state.TokenSet is not null, $"{nameof(state.TokenSet)} should not be null");

        state.ClientSteamId = callback.ClientSteamID;
        state.IsLoggedOn = true;
        Console.WriteLine($"Logged on as {state.AccountName}, ID {callback.ClientSteamID}");

        // proof-of-concept: refresh access token
        // Access tokens apparently last for 24h; we can parse the tokens (they're JWTs) to get expiration timestamps
        var response = await state.SteamClient.Authentication.GenerateAccessTokenForAppAsync(
            callback.ClientSteamID,
            state.TokenSet.RefreshToken,
            allowRenewal: true
        );
        state.TokenSet.AccessToken = response.AccessToken;
        if (response.RefreshToken is not "") state.TokenSet.AccessToken = response.RefreshToken;
        Console.WriteLine($"Refreshed token set: new access token={response.AccessToken is not ""}, new refresh token={response.RefreshToken is not ""}");
    }
}
