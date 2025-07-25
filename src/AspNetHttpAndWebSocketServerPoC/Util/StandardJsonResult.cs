using System;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace AspNetEphemeralHttpServerPoC.Util;

public class StandardJsonResult : JsonResult
{
    private const int DefaultStatusCode = 200;

    private readonly ObjectWrapper _wrapper;

    public StandardJsonResult(object? value, object? serializerSettings = null) : base(null, serializerSettings)
    {
        _wrapper = new() {
            Data = value,
        };
        StatusCode = DefaultStatusCode;
        Value = _wrapper;
    }

    public new int StatusCode {
        get => _wrapper.StatusCode ?? throw new InvalidOperationException();
        set {
            base.StatusCode = value;
            _wrapper.StatusCode = value;
        }
    }

    public string? Message {
        get => _wrapper.Message;
        set => _wrapper.Message = value;
    }

    public string? Detail {
        get => _wrapper.Detail;
        set => _wrapper.Detail = value;
    }

    class ObjectWrapper
    {
        [JsonPropertyName("status")]
        public int? StatusCode { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("detail")]
        public string? Detail { get; set; }

        [JsonPropertyName("data")]
        public object? Data { get; set; }
    }
}
