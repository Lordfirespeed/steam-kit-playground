using System;
using System.Threading.Tasks;

namespace AspNetEphemeralHttpServerPoC.Extensions;

public static class TaskExtensions
{
    public static async ValueTask SuppressingCancellation(this Task task)
    {
        await task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (task.IsFaulted) throw new Exception("", task.Exception!);
    }

    public static async ValueTask SuppressingCancellation<T>(this Task<T> task)
    {
        await SuppressingCancellation((Task)task);
    }

    public static async ValueTask SuppressingCancellation(this ValueTask task)
    {
        await SuppressingCancellation(task.AsTask());
    }

    public static async ValueTask SuppressingCancellation<T>(this ValueTask<T> task)
    {
        await SuppressingCancellation((Task)task.AsTask());
    }
}
