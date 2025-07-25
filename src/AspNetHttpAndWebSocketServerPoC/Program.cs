// See https://aka.ms/new-console-template for more information

using System;
using System.Runtime.InteropServices;
using System.Threading;
using AspNetEphemeralHttpServerPoC;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


var builder = WebApplication.CreateSlimBuilder();
builder.WebHost.ConfigureKestrel(options => {
    options.ListenUnixSocket(Config.SocketInfo.FullName);
});
builder.Services.AddControllers();

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions {
    KeepAliveInterval = TimeSpan.FromMinutes(2),
});

app.MapGet("/", () => "Hello World!");
app.MapControllers();

var cancellationSource = new CancellationTokenSource();
var cancel = (PosixSignalContext ctx) => {
    Console.WriteLine($"Got ${ctx.Signal}");
    cancellationSource.Cancel();
};
using (PosixSignalRegistration.Create(PosixSignal.SIGINT, cancel))
using (PosixSignalRegistration.Create(PosixSignal.SIGTERM, cancel)) {
    var runTask = app.RunAsync(cancellationSource.Token);
    Config.ConfigureSocket();

    await runTask;
}
