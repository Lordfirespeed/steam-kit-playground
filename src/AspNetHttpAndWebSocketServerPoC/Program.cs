// See https://aka.ms/new-console-template for more information

using System;
using System.Runtime.InteropServices;
using System.Threading;
using AspNetEphemeralHttpServerPoC;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateSlimBuilder();
builder.WebHost.ConfigureKestrel(options => {
    options.ListenUnixSocket(Config.SocketInfo.FullName);
});
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

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
