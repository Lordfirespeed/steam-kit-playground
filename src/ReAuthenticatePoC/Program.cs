using System;
using System.Threading.Tasks;
using ReAuthenticatePoC;
using ReAuthenticatePoC.Commands;
using ReAuthenticatePoC.Extensions;
using ReAuthenticatePoC.Utils;
using SteamKit2;

var state = new ProgramState();
{
    var hasConnectedTaskSource = new TaskCompletionSource();
    using var _ = state.Manager.Subscribe<SteamClient.ConnectedCallback>(_ => hasConnectedTaskSource.SetResult());
    Console.WriteLine( "Connecting to Steam..." );
    state.SteamClient.Connect();
    await state.Manager.RunUntil(hasConnectedTaskSource.Task);
    Console.WriteLine( "Connected to Steam" );
}

var runManagerTask = Task.Run(async () => await state.Manager.RunForeverAsync(state.RunToken).SuppressingCancellation());

while ( state.IsRunning ) {
    ReadLine.HistoryEnabled = true;
    var input = ReadLine.Read("> ");
    switch (input) {
        case "qr-auth":
            await new QrAuthCommand(state).Run(state.RunToken);
            break;
        case "save-auth":
            await state.SaveAuthenticationData(state.RunToken);
            break;
        case "load-auth":
            await state.LoadAuthenticationData(state.RunToken);
            break;
        case "log-on":
            await new LogOnCommand(state).Run(state.RunToken);
            break;
        case "log-off":
            await new LogOffCommand(state).Run(state.RunToken).SuppressingCancellation();
            await runManagerTask;
            break;
        case "print-tokens":
            if (!state.HasAuthenticated) throw new InvalidOperationException("Not authenticated");
            Console.WriteLine($"access: {JwtHelpers.FormatJsonWebTokenContents(state.TokenSet.AccessToken)}");
            Console.WriteLine($"refresh: {JwtHelpers.FormatJsonWebTokenContents(state.TokenSet.RefreshToken)}");
            break;
        case "disconnect":
            state.SteamClient.Disconnect();
            await runManagerTask;
            break;
        default:
            Console.WriteLine("Unrecognised command");
            break;
    }
}
