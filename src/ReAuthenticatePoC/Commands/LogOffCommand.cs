using System;
using System.Threading;
using System.Threading.Tasks;
using ReAuthenticatePoC.Extensions;

namespace ReAuthenticatePoC.Commands;

public class LogOffCommand(ProgramState state)
{
    public async ValueTask Run(CancellationToken cancellationToken = default)
    {
        if (!state.IsLoggedOn) throw new InvalidOperationException("Not logged on");

        Console.WriteLine("Logging off...");
        state.SteamUser.LogOff();
        await state.Manager.RunForeverAsync(cancellationToken);
    }
}
