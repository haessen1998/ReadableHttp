using System.Text.Json.Serialization;

namespace ReadableHttp.Core;

public sealed class ReadableExecutionContext
{
    private TimeSpan? _timeout;

    public Uri? BaseAddress { get; set; }

    public TimeSpan Timeout
    {
        get => _timeout ?? TimeSpan.FromSeconds(60);
        set
        {
            _timeout = value;
            HasTimeoutOverride = true;
        }
    }

    [JsonIgnore]
    public bool HasTimeoutOverride { get; private set; }

    public ReadableProxyOptions? Proxy { get; set; }

    public Dictionary<string, ReadableVariable> Variables { get; set; } = [];

    public ReadableAuth? Auth { get; set; }

    public bool FollowRedirects { get; set; } = true;

    public bool UseCookies { get; set; } = true;

    public bool IgnoreSslErrors { get; set; }

    public List<ReadableCookie> Cookies { get; set; } = [];
}
