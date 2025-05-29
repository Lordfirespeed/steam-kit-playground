// See https://aka.ms/new-console-template for more information

using System;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Mono.Unix;

var socketInfo = new UnixFileInfo("/tmp/steam-auth.sock");

var builder = WebApplication.CreateSlimBuilder();
builder.WebHost.ConfigureKestrel(options => {
    options.ListenUnixSocket(socketInfo.FullName);
});
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

var cancellationSource = new CancellationTokenSource();
cancellationSource.CancelAfter(new TimeSpan(hours: 0, minutes: 1, seconds: 0));

var runTask = app.RunAsync(cancellationSource.Token);

await runTask;
