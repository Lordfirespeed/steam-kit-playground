// See https://aka.ms/new-console-template for more information

using System;
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
Console.CancelKeyPress += (_, args) => cancellationSource.Cancel();

var runTask = app.RunAsync(cancellationSource.Token);
Config.ConfigureSocket();

await runTask;
