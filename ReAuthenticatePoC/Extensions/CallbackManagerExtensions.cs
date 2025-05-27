using System;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;

namespace ReAuthenticatePoC.Extensions;

public static class CallbackManagerExtensions
{
    private sealed class CancellingDisposable(CancellationTokenSource cts, IDisposable? disposable = null) : IDisposable
    {
        private bool _disposed = false;
        public CancellationToken Token => cts.Token;

        public void Dispose()
        {
            if (_disposed) return;
            cts.Cancel();
            disposable?.Dispose();
            _disposed = true;
        }
    }

    public static IDisposable Subscribe<TCallback>(
        this CallbackManager manager,
        Func<TCallback, Task> callbackFunc
    )
        where TCallback : CallbackMsg
    {
        var cts = new CancellationTokenSource();
        var disposable = manager.Subscribe<TCallback>(callback => {
            Task.Run(async () => await callbackFunc(callback), cts.Token);
        });
        return new CancellingDisposable(cts, disposable);
    }

    public static IDisposable Subscribe<TCallback>(
        this CallbackManager manager,
        Func<TCallback, ValueTask> callbackFunc
    )
        where TCallback : CallbackMsg
    {
        return Subscribe<TCallback>(manager, callback => callbackFunc(callback).AsTask());
    }

    public static IDisposable Subscribe<TCallback>(
        this CallbackManager manager,
        JobID jobId,
        Func<TCallback, Task> callbackFunc
    )
        where TCallback : CallbackMsg
    {
        var cts = new CancellationTokenSource();
        var disposable = manager.Subscribe<TCallback>(callback => {
            Task.Run(async () => await callbackFunc(callback), cts.Token);
        });
        return new CancellingDisposable(cts, disposable);
    }

    public static IDisposable Subscribe<TCallback>(
        this CallbackManager manager,
        JobID jobId,
        Func<TCallback, ValueTask> callbackFunc
    )
        where TCallback : CallbackMsg
    {
        return Subscribe<TCallback>(manager, jobId, callback => callbackFunc(callback).AsTask());
    }
}
