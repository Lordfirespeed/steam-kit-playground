using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EphemeralHttpServerPoC.Extensions;

public static class HttpListenerResponseExtensions
{
    public static async ValueTask Send(this HttpListenerResponse response, byte[] contentBytes, CancellationToken cancellationToken = default)
    {
        response.ContentLength64 = contentBytes.LongLength;
        await response.OutputStream.WriteAsync(contentBytes, cancellationToken);
        response.Close();
    }

    public static async ValueTask Send(this HttpListenerResponse response, Stream contentStream, CancellationToken cancellationToken = default)
    {
        response.ContentLength64 = contentStream.Length;
        await contentStream.CopyToAsync(response.OutputStream, cancellationToken);
        response.Close();
    }

    public static async ValueTask Send(this HttpListenerResponse response, string content, CancellationToken cancellationToken = default)
    {
        var contentBytes = Encoding.UTF8.GetBytes(content);
        await response.Send(contentBytes, cancellationToken);
    }

    public static async ValueTask SendPlain(this HttpListenerResponse response, string content, CancellationToken cancellationToken = default)
    {
        response.ContentType = "text/plain";
        await response.Send(content, cancellationToken);
    }

    public static async ValueTask SendJson(this HttpListenerResponse response, object content, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, content, JsonSerializerOptions.Web, cancellationToken);
        response.ContentType = "application/json";
        await response.Send(stream, cancellationToken);
    }
}
