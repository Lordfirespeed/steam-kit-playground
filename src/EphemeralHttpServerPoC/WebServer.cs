using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EphemeralHttpServerPoC.Extensions;

namespace EphemeralHttpServerPoC;

public class WebServer
{
    private readonly HttpListener _listener = new();
    private readonly ConcurrentDictionary<Guid, Task> _ongoingRequests = new();

    public string? ListenAddress { get; private set; }

    [MemberNotNullWhen(true, nameof(ListenAddress))]
    public bool IsListening => _listener.IsListening;

    async Task HandleRequest(HttpListenerContext ctx, CancellationToken cancellationToken = default)
    {
        await ctx.Response.SendPlain("Hello, world!", cancellationToken: cancellationToken);
    }

    async Task HandleRequestTrackingProgress(HttpListenerContext ctx, CancellationToken cancellationToken = default)
    {
        var requestTask = Task.Run(async () => await HandleRequest(ctx, cancellationToken), cancellationToken);
        _ongoingRequests[ctx.Request.RequestTraceIdentifier] = requestTask;
        try {
            await requestTask;
        }
        finally {
            _ongoingRequests.Remove(ctx.Request.RequestTraceIdentifier, out _);
        }
    }

    public async Task Listen(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try {
            _listener.StartOnFreePrivatePort(out var port);
            ListenAddress = $"http://localhost:{port}/";
            while (true) {
                var ctx = await _listener.GetContextAsync().WaitAsync(cancellationToken);
                _ = Task.Run(async () => await HandleRequestTrackingProgress(ctx));
            }
        }
        finally {
            await Task.WhenAll(_ongoingRequests.Values).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            ListenAddress = null;
            if (_listener.IsListening) _listener.Stop();
        }
    }
}
