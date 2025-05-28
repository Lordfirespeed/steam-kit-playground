using System;
using System.Threading.Tasks;
using ReAuthenticatePoC;
using ReAuthenticatePoC.Commands;
using ReAuthenticatePoC.Extensions;
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

while ( state.IsRunning ) {
    var input = ReadLine.Read("> ");
    switch (input) {
        case "qr-auth":
            await new QrAuthCommand(state).Run();
            break;
        case "disconnect":
            state.SteamClient.Disconnect();
            await state.Manager.RunForeverAsync(state.RunToken).SuppressingCancellation();
            break;
        default:
            Console.WriteLine("Unrecognised command");
            break;
    }
}
