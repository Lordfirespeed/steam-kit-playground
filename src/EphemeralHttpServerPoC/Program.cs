using System;
using System.Threading;
using EphemeralHttpServerPoC;
using EphemeralHttpServerPoC.Extensions;

var server = new WebServer();
var cancellationSource = new CancellationTokenSource();
cancellationSource.CancelAfter(new TimeSpan(hours: 0, minutes: 0, seconds: 15));
var serverListenTask = server.Listen(cancellationSource.Token);
Console.WriteLine($"Listening on {server.ListenAddress}");
await serverListenTask.SuppressingCancellation();
Console.WriteLine("Server closed");
