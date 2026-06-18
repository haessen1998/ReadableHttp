namespace ReadableHttp.Core;

public sealed class ReadableExecutionContext
{
    public Uri? BaseAddress { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    public ReadableProxyOptions? Proxy { get; set; }

    public Dictionary<string, ReadableVariable> Variables { get; set; } = [];

    public ReadableAuth? Auth { get; set; }

    public bool FollowRedirects { get; set; } = true;

    public bool UseCookies { get; set; } = true;

    public bool IgnoreSslErrors { get; set; }

    public List<ReadableCookie> Cookies { get; set; } = [];
}
