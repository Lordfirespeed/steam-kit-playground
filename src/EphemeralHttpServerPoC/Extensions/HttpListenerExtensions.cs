using System;
using System.Net;

namespace EphemeralHttpServerPoC.Extensions;

public static class HttpListenerExtensions
{
    // IANA suggested range for dynamic or private ports
    const int MinPort = 49215;
    const int MaxPort = 65535;

    public static void StartOnFreePrivatePort(this HttpListener listener, out int port)
    {
        for (port = MinPort; port < MaxPort; port++) {
            listener.Prefixes.Clear();
            listener.Prefixes.Add($"http://localhost:{port}/");
            try {
                listener.Start();
                return;
            }
            catch (HttpListenerException) { }
        }

        port = 0;
        throw new InvalidOperationException($"Couldn't find a free port to listen on in range [{MinPort},{MaxPort})");
    }
}
