using ReadableHttp;
using ReadableHttp.AI;
using ReadableHttp.Execution;
using ReadableHttp.Storage;
using ReadableHttp.Try;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ReadableHttp.App.Maui.Components.ApiClient;

public sealed class ApiClientWorkspaceState
{
    private const string DiscoveredCollectionIdPrefix = "__discovered_collection__:";
    private const string RootCollectionId = "__collections_root__";
    private const string RootCollectionDirectory = "collections";
    public const string ThemeSystem = "system";
    public const string ThemeLight = "light";
    public const string ThemeDark = "dark";
    public const string ProxyNone = "none";
    public const string ProxySystem = "system";
    public const string ProxyCustom = "custom";
    public const string FontSmall = "small";
    public const string FontMedium = "medium";
    public const string FontLarge = "large";
    private static readonly JsonSerializerOptions JsonDisplayOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly AppSettingsStore _settingsStore;
    private readonly AppFilePicker _filePicker;
    private readonly IReadableHttpAiAgent _aiAgent;
    private readonly ReadableWorkspaceStore _workspaceStore = new();
    private bool _initialized;

    public ApiClientWorkspaceState(AppSettingsStore settingsStore, AppFilePicker filePicker, IReadableHttpAiAgent aiAgent)
    {
        _settingsStore = settingsStore;
        _filePicker = filePicker;
        _aiAgent = aiAgent;
    }

    public event Action? Changed;
    public event Func<Task>? ChangedAsync;

    public string WorkspacePath { get; set; } = string.Empty;
    public string WorkspaceStatus { get; private set; } = "No workspace loaded";
    public string TryFilePath { get; private set; } = string.Empty;
    public string ActiveSource { get; private set; } = "Request";
    public string DocumentTitle { get; private set; } = "Scratch Request";
    public string RawContent { get; private set; } = string.Empty;
    public string ViewMode { get; private set; } = "request";
    public string RequestTab => ActiveRequestTab?.RequestSectionTab ?? "Params";
    public string ResponseTab => ActiveRequestTab?.ResponseSectionTab ?? "Response";
    public string Method { get => ActiveRequestTab?.Method ?? "GET"; set => SetMethod(value); }
    public string Url { get => ActiveRequestTab?.Url ?? string.Empty; set => SetUrl(value); }
    public string BodyText { get => ActiveRequestTab?.BodyText ?? string.Empty; set => SetBodyText(value); }
    public string PreRequestScript { get => ActiveRequestTab?.PreRequestScript ?? string.Empty; set => SetPreRequestScript(value); }
    public string TestScript { get => ActiveRequestTab?.TestScript ?? string.Empty; set => SetTestScript(value); }
    public string AssertText { get => ActiveRequestTab?.AssertText ?? string.Empty; set => SetAssertText(value); }
    public string BodyType => ActiveRequestTab?.BodyType.ToString() ?? ReadableBodyType.None.ToString();
    public string BodyContentType => ActiveRequestTab?.BodyContentType ?? string.Empty;
    public IReadOnlyList<ReadableNameValue> RequestQuery => ActiveRequestTab?.Query ?? [];
    public IReadOnlyList<ReadableNameValue> RequestHeaders => ActiveRequestTab?.Headers ?? [];
    public IReadOnlyList<ReadableNameValue> RequestForm => ActiveRequestTab?.Form ?? [];
    public ReadableAuth? RequestAuth => ActiveRequestTab?.Auth;
    public string StatusText => ActiveRequestTab?.StatusText ?? "Ready";
    public string ResponseText => ActiveRequestTab?.ResponseText ?? string.Empty;
    public string ResponseNodePath => ActiveRequestTab?.ResponseNodePath ?? "root";
    public string Language { get; set; } = "system";
    public string ThemeMode { get; private set; } = ThemeSystem;
    public bool DarkMode { get; set; }
    public string FontSize { get; private set; } = FontMedium;
    public string FontSizeClass => $"font-{FontSize}";
    public bool UseChinese => Language.Equals("zh-CN", StringComparison.OrdinalIgnoreCase)
        || (Language.Equals("system", StringComparison.OrdinalIgnoreCase)
            && Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase));
    public string ProxyMode { get; private set; } = ProxySystem;
    public string CustomProxyUrl { get; private set; } = string.Empty;
    public string CustomProxyUsername { get; private set; } = string.Empty;
    public string CustomProxyPassword { get; private set; } = string.Empty;
    public bool UseSystemProxy => ProxyMode == ProxySystem;
    public bool DevToolsEnabled { get; set; } = true;
    public bool WorkspaceAutoInitialize { get; set; }
    public bool IsSending { get; private set; }
    public bool ShowExplorer { get; private set; } = true;
    public bool ShowInspector { get; private set; }
    public bool ShowWorkspaceSection { get; private set; } = true;
    public bool ShowCollectionsSection { get; private set; } = true;
    public bool ShowSpecsSection { get; private set; } = true;
    public bool ShowRecentSection { get; private set; }
    public RightPanelMode RightPanelMode { get; private set; } = RightPanelMode.Docs;
    public string AiDraft { get; set; } = string.Empty;
    public string? ExpandedOperationKey { get; private set; }
    public ReadableWorkspace? Workspace { get; private set; }
    public ReadableCollection? SelectedCollection { get; private set; }
    public ReadableRequest? SelectedRequest { get; private set; }
    public ReadableSpecification? SelectedSpecification { get; private set; }
    public List<ReadableCollection> Collections { get; private set; } = [];
    public IReadOnlyList<ReadableSpecification> Specifications => Workspace?.Specifications ?? [];

    public int VisibleSpecificationCount => Specifications.Count;
    public List<ReadableTryOperation> Operations { get; private set; } = [];
    public List<string> SpecFiles { get; private set; } = [];
    public List<string> RecentSessions { get; private set; } = ["Scratch Request"];
    public List<string> WorkspaceOptions { get; private set; } = [];
    public List<ActivityEntry> ActivityLog { get; private set; } = [new("Ready", "等待加载 workspace 或导入文件")];
    public List<AiChatMessage> AiMessages { get; private set; } = [new("assistant", "我可以根据当前 request、response 或 spec 帮你整理参数、生成测试用例和解释错误。")];
    public List<ReadableAiAction> PendingAiActions { get; private set; } = [];
    public List<HistoryRecord> HistoryRecords { get; private set; } = [];
    public string? ExpandedHistoryRecordId { get; private set; }
    public List<PipelinePhase> PipelinePhases => ActiveRequestTab?.PipelinePhases ?? [];
    public List<RequestWorkspaceTab> RequestTabs { get; private set; } = [];
    public string? ActiveRequestTabId { get; private set; }
    public string? PendingCloseTabId { get; private set; }
    public AppSettingsDraft SettingsDraft { get; private set; } = new();
    public string AboutDescription => ActiveRequestTab?.SourceKey switch
    {
        "collections" => T("collectionsAboutDescription"),
        "specs" => T("specsAboutDescription"),
        _ => string.Empty
    };
    public IReadOnlyList<KeyValuePair<string, string>> AboutStats => ActiveRequestTab?.SourceKey switch
    {
        "collections" =>
        [
            new(T("collections"), EnumerateCollections(Collections).Count(collection => !IsRootCollection(collection)).ToString()),
            new(T("requests"), EnumerateCollections(Collections).Sum(collection => collection.Requests.Count).ToString()),
            new(T("workspace"), WorkspaceName)
        ],
        "specs" =>
        [
            new(T("specifications"), VisibleSpecificationCount.ToString()),
            new(T("remoteEndpoint"), Specifications.Count(specification => specification.SourceType == ReadableSpecificationSourceType.RemoteEndpoint).ToString()),
            new(T("localFile"), Specifications.Count(specification => specification.SourceType != ReadableSpecificationSourceType.RemoteEndpoint).ToString())
        ],
        _ => []
    };

    public bool IsGitWorkspace => Workspace?.Type == ReadableWorkspaceType.Git;

    public string WorkspaceName => Workspace?.Name ?? "No workspace";

    public string CollectionRequestDirectory => SelectedCollection?.RequestDirectory ?? string.Empty;

    public string RequestName => ActiveRequestTab?.Title ?? string.Empty;

    public string WorkspaceRemoteUrl => Workspace?.Git?.RemoteUrl ?? string.Empty;

    public string WorkspaceTypeLabel => Workspace?.Type.ToString() ?? ReadableWorkspaceType.Local.ToString();

    public IReadOnlyList<KeyValuePair<string, ReadableVariable>> WorkspaceVariables => Workspace?.Variables.ToList() ?? [];

    public IReadOnlyList<KeyValuePair<string, ReadableVariable>> CollectionVariables => SelectedCollection?.Variables.ToList() ?? [];

    public IReadOnlyList<ReadableEnvironment> Environments => Workspace?.Environments ?? [];

    public IReadOnlyList<ReadableCollection> RequestCollectionOptions => EnumerateCollections(Collections).ToList();

    public string ActiveRequestCollectionId => ActiveRequestTab?.CollectionId ?? string.Empty;

    public string ActiveRequestCollectionName => FindCollectionById(ActiveRequestTab?.CollectionId)?.Name ?? T("notSaved");

    public string SelectedEnvironmentId { get; private set; } = string.Empty;

    public string SelectedEnvironmentName => SelectedEnvironment?.Name ?? T("noEnvironment");

    private ReadableEnvironment? SelectedEnvironment => Environments.FirstOrDefault(environment =>
        string.Equals(environment.Id, SelectedEnvironmentId, StringComparison.OrdinalIgnoreCase)
        || string.Equals(environment.Name, SelectedEnvironmentId, StringComparison.OrdinalIgnoreCase));

    public string SpecificationName => SelectedSpecification?.Name ?? string.Empty;

    public string SpecificationEndpoint => SelectedSpecification?.Remote?.Endpoint ?? string.Empty;

    public string SpecificationPath => SelectedSpecification?.Path ?? string.Empty;

    public string SpecificationCachePath => SelectedSpecification?.NormalizedPath ?? string.Empty;

    public string SpecificationCacheStatus
    {
        get
        {
            if (SelectedSpecification is null || string.IsNullOrWhiteSpace(SelectedSpecification.NormalizedPath))
            {
                return "Not cached";
            }

            return File.Exists(ResolveWorkspacePath(SelectedSpecification.NormalizedPath))
                ? "Cached"
                : "Missing";
        }
    }

    public string SpecificationLastRefreshedLabel => SelectedSpecification?.Remote?.LastRefreshedAt is { } refreshedAt
        ? refreshedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
        : "Never";

    public string SpecificationSourceLabel => SelectedSpecification?.SourceType.ToString() ?? string.Empty;

    public RequestWorkspaceTab? ActiveRequestTab => RequestTabs.FirstOrDefault(tab => tab.Id == ActiveRequestTabId)
        ?? RequestTabs.FirstOrDefault();

    public bool HasWorkspace => Workspace is not null;

    public bool HasSelectedCollection => SelectedCollection is not null;

    public bool HasSelectedRequest => SelectedRequest is not null;

    public bool IsDarkTheme => ThemeMode == ThemeDark
        || (ThemeMode == ThemeSystem && Application.Current?.RequestedTheme == AppTheme.Dark);

    public string StatusLabel => IsSending
        ? "Sending"
        : StatusText.StartsWith("HTTP 2", StringComparison.OrdinalIgnoreCase)
            ? "Success"
            : StatusText.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase)
                ? "Error"
                : "Ready";

    public string StatusClass => StatusLabel switch
    {
        "Success" => "status-chip success",
        "Error" => "status-chip error",
        "Sending" => "status-chip sending",
        _ => "status-chip"
    };

    public string PipelineTotalLabel
    {
        get
        {
            if (IsSending)
            {
                return "...";
            }

            var total = PipelinePhases.Sum(phase => phase.ElapsedMs);
            return total > 0 ? $"{total}ms" : "no trace";
        }
    }

    public List<string> ResponseNodeKeys => GetResponseNodeKeys();

    public string SelectedResponseText => GetSelectedResponseText();

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        var settings = _settingsStore.Load();
        WorkspacePath = settings.WorkspacePath;
        TryFilePath = string.IsNullOrWhiteSpace(settings.TryFilePath)
            ? TryFilePath
            : settings.TryFilePath;
        Language = settings.Language;
        FontSize = NormalizeFontSize(settings.FontSize);
        ThemeMode = NormalizeThemeMode(string.IsNullOrWhiteSpace(settings.ThemeMode)
            ? settings.DarkMode ? ThemeDark : ThemeSystem
            : settings.ThemeMode);
        DarkMode = ThemeMode == ThemeDark;
        ApplyApplicationTheme();
        ProxyMode = NormalizeProxyMode(settings.ProxyMode);
        CustomProxyUrl = settings.CustomProxyUrl;
        CustomProxyUsername = settings.CustomProxyUsername;
        CustomProxyPassword = settings.CustomProxyPassword;
        DevToolsEnabled = settings.DevToolsEnabled;
        ResetSettingsDraft();
        foreach (var workspace in settings.WorkspaceHistory.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            AddWorkspaceOption(workspace);
        }
        if (!string.IsNullOrWhiteSpace(settings.WorkspacePath))
        {
            AddWorkspaceOption(settings.WorkspacePath);
        }

        var unavailableWorkspaces = WorkspaceOptions
            .Where(path => !IsWorkspaceUsable(path))
            .ToList();

        if (!string.IsNullOrWhiteSpace(TryFilePath))
        {
            AddSpecFile(TryFilePath);
        }
        _initialized = true;

        if (!string.IsNullOrWhiteSpace(settings.WorkspacePath) && IsWorkspaceUsable(settings.WorkspacePath))
        {
            await LoadWorkspaceAsync();
            if (unavailableWorkspaces.Count > 0)
            {
                WorkspaceStatus = $"{WorkspaceStatus} · {unavailableWorkspaces.Count} workspace unavailable";
                AddActivity("Workspace", $"{unavailableWorkspaces.Count} 个历史工作区不可用，可恢复路径或在 more 中删除");
                NotifyChanged();
            }
            return;
        }

        if (unavailableWorkspaces.Count > 0)
        {
            WorkspaceStatus = string.IsNullOrWhiteSpace(settings.WorkspacePath)
                ? $"{unavailableWorkspaces.Count} workspace unavailable"
                : $"Last workspace unavailable: {WorkspaceDisplayName(settings.WorkspacePath)}";
            AddActivity("Workspace", "历史工作区中存在不可用路径，可恢复路径或在 more 中删除");
        }

        NotifyChanged();
    }

    public void SetMethod(string value)
    {
        var tab = EnsureActiveRequestTab();
        tab.Method = string.IsNullOrWhiteSpace(value) ? "GET" : value;
        tab.IsDirty = true;
        if (SelectedRequest is not null && tab.Origin == RequestTabOrigin.Collection)
        {
            SelectedRequest.Method = tab.Method;
        }
        NotifyChanged();
    }

    public void SetUrl(string value)
    {
        var tab = EnsureActiveRequestTab();
        tab.Url = value;
        tab.IsDirty = true;
        if (SelectedRequest is not null && tab.Origin == RequestTabOrigin.Collection)
        {
            SelectedRequest.Url = tab.Url;
        }
        NotifyChanged();
    }

    public void SetBodyText(string value)
    {
        var tab = EnsureActiveRequestTab();
        tab.BodyText = value;
        tab.IsDirty = true;
        if (SelectedRequest is not null && tab.Origin == RequestTabOrigin.Collection)
        {
            SelectedRequest.Body = BuildBodyFromTab(tab);
        }
        NotifyChanged();
    }

    public void SetPreRequestScript(string value)
    {
        var tab = EnsureActiveRequestTab();
        tab.PreRequestScript = value;
        tab.IsDirty = true;
        NotifyChanged();
    }

    public void SetTestScript(string value)
    {
        var tab = EnsureActiveRequestTab();
        tab.TestScript = value;
        tab.IsDirty = true;
        NotifyChanged();
    }

    public void SetAssertText(string value)
    {
        var tab = EnsureActiveRequestTab();
        tab.AssertText = value;
        tab.IsDirty = true;
        NotifyChanged();
    }

    public void SetBodyType(string value)
    {
        var tab = EnsureActiveRequestTab();
        tab.BodyType = Enum.TryParse<ReadableBodyType>(value, ignoreCase: true, out var type)
            ? type
            : ReadableBodyType.None;
        tab.BodyContentType = DefaultContentType(tab.BodyType);
        tab.IsDirty = true;
        if (SelectedRequest is not null && tab.Origin == RequestTabOrigin.Collection)
        {
            SelectedRequest.Body = BuildBodyFromTab(tab);
        }
        NotifyChanged();
    }

    public void SetBodyContentType(string value)
    {
        var tab = EnsureActiveRequestTab();
        tab.BodyContentType = value.Trim();
        tab.IsDirty = true;
        if (SelectedRequest is not null && tab.Origin == RequestTabOrigin.Collection)
        {
            SelectedRequest.Body = BuildBodyFromTab(tab);
        }
        NotifyChanged();
    }

    public void FormatBody()
    {
        var tab = EnsureActiveRequestTab();
        if (string.IsNullOrWhiteSpace(tab.BodyText))
        {
            return;
        }

        try
        {
            var formatted = FormatBodyText(tab.BodyText, tab.BodyType, tab.BodyContentType);

            if (!string.Equals(formatted, tab.BodyText, StringComparison.Ordinal))
            {
                tab.BodyText = formatted;
                tab.IsDirty = true;
                SyncSelectedRequestFromTab(tab);
                AddActivity("Body", "已格式化请求 body");
                NotifyChanged();
            }
        }
        catch (Exception exception) when (exception is JsonException or System.Xml.XmlException)
        {
            AddActivity("Body", $"格式化失败: {exception.Message}");
            NotifyChanged();
        }
    }

    private static string FormatBodyText(string text, ReadableBodyType bodyType, string? contentType)
    {
        if (bodyType == ReadableBodyType.Json || ShouldTreatAsJson(text, contentType))
        {
            return FormatJsonElement(JsonDocument.Parse(text).RootElement);
        }

        if (bodyType == ReadableBodyType.Xml || ShouldTreatAsXml(text, contentType))
        {
            return System.Xml.Linq.XDocument.Parse(text).ToString();
        }

        return text;
    }

    private static bool ShouldTreatAsJson(string text, string? contentType)
    {
        if (!string.IsNullOrWhiteSpace(contentType)
            && contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var trimmed = text.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    private static bool ShouldTreatAsXml(string text, string? contentType)
    {
        if (!string.IsNullOrWhiteSpace(contentType)
            && (contentType.Contains("xml", StringComparison.OrdinalIgnoreCase)
                || contentType.Contains("html", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return text.TrimStart().StartsWith('<');
    }

    public void AddNameValue(string kind)
    {
        var tab = EnsureActiveRequestTab();
        GetNameValueList(tab, kind).Add(new ReadableNameValue { Enabled = true });
        tab.IsDirty = true;
        SyncSelectedRequestFromTab(tab);
        NotifyChanged();
    }

    public void RemoveNameValue(EditableNameValueChange change)
    {
        var tab = EnsureActiveRequestTab();
        var list = GetNameValueList(tab, change.Kind);
        if (change.Index < 0 || change.Index >= list.Count)
        {
            return;
        }

        list.RemoveAt(change.Index);
        tab.IsDirty = true;
        SyncSelectedRequestFromTab(tab);
        NotifyChanged();
    }

    public void SetNameValue(EditableNameValueChange change)
    {
        var tab = EnsureActiveRequestTab();
        var list = GetNameValueList(tab, change.Kind);
        if (change.Index < 0 || change.Index >= list.Count)
        {
            return;
        }

        var item = list[change.Index];
        switch (change.Field)
        {
            case "enabled":
                item.Enabled = bool.TryParse(change.Value, out var enabled) && enabled;
                break;
            case "name":
                item.Name = change.Value ?? string.Empty;
                break;
            case "value":
                item.Value = change.Value;
                break;
        }

        tab.IsDirty = true;
        SyncSelectedRequestFromTab(tab);
        NotifyChanged();
    }

    public void SetAuth(AuthChange change)
    {
        var tab = EnsureActiveRequestTab();
        tab.Auth ??= new ReadableAuth();
        var auth = tab.Auth;

        switch (change.Field)
        {
            case "type":
                auth.Type = Enum.TryParse<ReadableAuthType>(change.Value, ignoreCase: true, out var authType)
                    ? authType
                    : ReadableAuthType.None;
                EnsureAuthOptions(auth);
                break;
            case "username":
                auth.Username = change.Value;
                break;
            case "password":
                auth.Password = change.Value;
                break;
            case "token":
                auth.Token = change.Value;
                break;
            case "oauth1.consumerKey":
                EnsureOAuth1(auth).ConsumerKey = change.Value;
                break;
            case "oauth1.consumerSecret":
                EnsureOAuth1(auth).ConsumerSecret = change.Value;
                break;
            case "oauth1.token":
                EnsureOAuth1(auth).Token = change.Value;
                break;
            case "oauth1.tokenSecret":
                EnsureOAuth1(auth).TokenSecret = change.Value;
                break;
            case "oauth1.signatureMethod":
                EnsureOAuth1(auth).SignatureMethod = Enum.TryParse<ReadableOAuth1SignatureMethod>(change.Value, ignoreCase: true, out var signatureMethod)
                    ? signatureMethod
                    : ReadableOAuth1SignatureMethod.HmacSha1;
                break;
            case "oauth1.placement":
                EnsureOAuth1(auth).Placement = Enum.TryParse<ReadableTokenPlacement>(change.Value, ignoreCase: true, out var oauth1Placement)
                    ? oauth1Placement
                    : ReadableTokenPlacement.Header;
                break;
            case "oauth2.grantType":
                EnsureOAuth2(auth).GrantType = Enum.TryParse<ReadableOAuth2GrantType>(change.Value, ignoreCase: true, out var grantType)
                    ? grantType
                    : ReadableOAuth2GrantType.AuthorizationCode;
                break;
            case "oauth2.authorizationUrl":
                EnsureOAuth2(auth).AuthorizationUrl = change.Value;
                break;
            case "oauth2.tokenUrl":
                EnsureOAuth2(auth).TokenUrl = change.Value;
                break;
            case "oauth2.clientId":
                EnsureOAuth2(auth).ClientId = change.Value;
                break;
            case "oauth2.clientSecret":
                EnsureOAuth2(auth).ClientSecret = change.Value;
                break;
            case "oauth2.scope":
                EnsureOAuth2(auth).Scopes = (change.Value ?? string.Empty)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
                break;
            case "oauth2.tokenId":
                EnsureOAuth2(auth).TokenId = string.IsNullOrWhiteSpace(change.Value) ? "credentials" : change.Value.Trim();
                break;
            case "oauth2.tokenSource":
                EnsureOAuth2(auth).TokenSource = Enum.TryParse<ReadableOAuth2TokenSource>(change.Value, ignoreCase: true, out var tokenSource)
                    ? tokenSource
                    : ReadableOAuth2TokenSource.AccessToken;
                break;
            case "oauth2.placement":
                EnsureOAuth2(auth).TokenPlacement = Enum.TryParse<ReadableTokenPlacement>(change.Value, ignoreCase: true, out var oauth2Placement)
                    ? oauth2Placement
                    : ReadableTokenPlacement.Header;
                break;
        }

        if (auth.Type == ReadableAuthType.None)
        {
            tab.Auth = null;
        }

        tab.IsDirty = true;
        SyncSelectedRequestFromTab(tab);
        NotifyChanged();
    }

    public void SetSelectedEnvironment(string value)
    {
        SelectedEnvironmentId = value ?? string.Empty;
        AddActivity("Environment", string.IsNullOrWhiteSpace(SelectedEnvironmentId)
            ? T("noEnvironment")
            : SelectedEnvironmentName);
        NotifyChanged();
    }

    public void SetRequestName(string value)
    {
        var tab = EnsureActiveRequestTab();
        tab.Title = string.IsNullOrWhiteSpace(value) ? "Untitled Request" : value.Trim();
        tab.IsDirty = true;
        if (SelectedRequest is not null && tab.Origin == RequestTabOrigin.Collection)
        {
            SelectedRequest.Name = tab.Title;
        }
        NotifyChanged();
    }

    public void SetWorkspaceName(string value)
    {
        if (Workspace is null)
        {
            return;
        }

        Workspace.Name = string.IsNullOrWhiteSpace(value) ? "ReadableHttp Workspace" : value.Trim();
        if (ActiveRequestTab?.Origin == RequestTabOrigin.Workspace)
        {
            ActiveRequestTab.Title = Workspace.Name;
        }
        NotifyChanged();
    }

    public void SetCollectionName(string value)
    {
        if (SelectedCollection is null)
        {
            return;
        }

        var oldDirectory = GetCollectionDirectory(SelectedCollection);
        var name = string.IsNullOrWhiteSpace(value) ? "Collection" : value.Trim();
        SelectedCollection.Name = name;
        if (ShouldRenameCollectionDirectory(SelectedCollection))
        {
            var newRelativeDirectory = $"collections/{ToFileName(name)}";
            var newDirectory = Path.Combine(WorkspacePath, newRelativeDirectory);
            if (!string.Equals(NormalizePath(oldDirectory), NormalizePath(newDirectory), StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(oldDirectory) && !Directory.Exists(newDirectory))
                {
                    Directory.Move(oldDirectory, newDirectory);
                }

                if (!Directory.Exists(oldDirectory) || Directory.Exists(newDirectory))
                {
                    SelectedCollection.RequestDirectory = newRelativeDirectory;
                    if (SelectedCollection.Id.StartsWith(DiscoveredCollectionIdPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedCollection.Id = $"{DiscoveredCollectionIdPrefix}{newRelativeDirectory}";
                    }
                }
            }
        }

        if (ActiveRequestTab?.Origin == RequestTabOrigin.CollectionConfig)
        {
            ActiveRequestTab.Title = SelectedCollection.Name;
        }
        NotifyChanged();
    }

    public void SetCollectionRequestDirectory(string value)
    {
        if (SelectedCollection is null)
        {
            return;
        }

        SelectedCollection.RequestDirectory = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        NotifyChanged();
    }

    public void AddVariable(string scope)
    {
        var variables = GetVariableDictionary(scope);
        if (variables is null)
        {
            return;
        }

        var name = NextVariableName(variables, "variable");
        variables[name] = string.Empty;
        NotifyChanged();
    }

    public void RemoveVariable(VariableChange change)
    {
        var variables = GetVariableDictionary(change.Scope);
        var entry = GetVariableEntry(variables, change.Index);
        if (variables is null || entry is null)
        {
            return;
        }

        variables.Remove(entry.Value.Key);
        NotifyChanged();
    }

    public void SetVariable(VariableChange change)
    {
        var variables = GetVariableDictionary(change.Scope);
        var entry = GetVariableEntry(variables, change.Index);
        if (variables is null || entry is null)
        {
            return;
        }

        var key = entry.Value.Key;
        var variable = entry.Value.Value;
        switch (change.Field)
        {
            case "enabled":
                variable.Enabled = bool.TryParse(change.Value, out var enabled) && enabled;
                break;
            case "name":
                var nextKey = string.IsNullOrWhiteSpace(change.Value) ? key : change.Value.Trim();
                if (!string.Equals(key, nextKey, StringComparison.Ordinal)
                    && !variables.ContainsKey(nextKey))
                {
                    variables.Remove(key);
                    variables[nextKey] = variable;
                }
                break;
            case "value":
                variable.Value = change.Value is null ? null : System.Text.Json.Nodes.JsonValue.Create(change.Value);
                variable.Type = ReadableVariableType.String;
                variable.Source = ReadableVariableSource.Fixed;
                break;
        }

        NotifyChanged();
    }

    public void AddEnvironment()
    {
        if (Workspace is null)
        {
            return;
        }

        var name = NextEnvironmentName("environment");
        var environment = new ReadableEnvironment
        {
            Name = name
        };
        Workspace.Environments.Add(environment);
        SelectedEnvironmentId = environment.Id;
        AddActivity("Environment", $"已新增 {environment.Name}");
        NotifyChanged();
    }

    public void SetEnvironment(EnvironmentChange change)
    {
        var environment = FindEnvironment(change.EnvironmentId);
        if (environment is null)
        {
            return;
        }

        if (change.Field == "name")
        {
            environment.Name = string.IsNullOrWhiteSpace(change.Value) ? "Environment" : change.Value.Trim();
        }

        NotifyChanged();
    }

    public void RemoveEnvironment(string environmentId)
    {
        if (Workspace is null)
        {
            return;
        }

        var environment = FindEnvironment(environmentId);
        if (environment is null)
        {
            return;
        }

        Workspace.Environments.Remove(environment);
        if (string.Equals(SelectedEnvironmentId, environment.Id, StringComparison.OrdinalIgnoreCase))
        {
            SelectedEnvironmentId = string.Empty;
        }

        AddActivity("Environment", $"已删除 {environment.Name}");
        NotifyChanged();
    }

    public void SetWorkspaceRemoteUrl(string value)
    {
        if (Workspace is null)
        {
            return;
        }

        Workspace.Git ??= new ReadableGitOptions();
        Workspace.Git.RemoteUrl = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        NotifyChanged();
    }

    public void SetSpecificationName(string value)
    {
        if (SelectedSpecification is null)
        {
            return;
        }

        SelectedSpecification.Name = string.IsNullOrWhiteSpace(value) ? "Document" : value.Trim();
        if (ActiveRequestTab?.Origin == RequestTabOrigin.SpecificationConfig)
        {
            ActiveRequestTab.Title = SelectedSpecification.Name;
        }
        NotifyChanged();
    }

    public void SetSpecificationEndpoint(string value)
    {
        if (SelectedSpecification is null)
        {
            return;
        }

        SelectedSpecification.SourceType = ReadableSpecificationSourceType.RemoteEndpoint;
        SelectedSpecification.Remote ??= new ReadableRemoteSpecificationOptions();
        SelectedSpecification.Remote.Endpoint = value.Trim();
        NotifyChanged();
    }

    public void SetDraftThemeMode(string value) => UpdateSettingsDraft(SettingsDraft with { ThemeMode = NormalizeThemeMode(value) });

    public void SetDraftProxyMode(string value) => UpdateSettingsDraft(SettingsDraft with { ProxyMode = NormalizeProxyMode(value) });

    public void SetDraftCustomProxyUrl(string value) => UpdateSettingsDraft(SettingsDraft with { CustomProxyUrl = value });

    public void SetDraftCustomProxyUsername(string value) => UpdateSettingsDraft(SettingsDraft with { CustomProxyUsername = value });

    public void SetDraftCustomProxyPassword(string value) => UpdateSettingsDraft(SettingsDraft with { CustomProxyPassword = value });

    public void SetDraftDevToolsEnabled(bool value) => UpdateSettingsDraft(SettingsDraft with { DevToolsEnabled = value });

    public void SetDraftLanguage(string value) => UpdateSettingsDraft(SettingsDraft with { Language = string.IsNullOrWhiteSpace(value) ? "system" : value });

    public void SetDraftFontSize(string value) => UpdateSettingsDraft(SettingsDraft with { FontSize = NormalizeFontSize(value) });

    public string T(string key)
    {
        if (!UseChinese)
        {
            return key switch
            {
                "settings" => "Settings",
                "global" => "Global",
                "preferences" => "Preferences",
                "theme" => "Theme",
                "language" => "Language",
                "fontSize" => "Font size",
                "system" => "System",
                "light" => "Light",
                "dark" => "Dark",
                "small" => "Small",
                "medium" => "Medium",
                "large" => "Large",
                "proxy" => "Proxy",
                "noProxy" => "No proxy",
                "systemProxy" => "System proxy",
                "customProxy" => "Custom proxy",
                "send" => "Send",
                "close" => "Close",
                "save" => "Save",
                "closeOthers" => "Close Others",
                "closeAll" => "Close All",
                "discard" => "Discard",
                "cancel" => "Cancel",
                "unsaved" => "Unsaved changes",
                "docs" => "Documentation",
                "parameters" => "Parameters",
                "hidePanel" => "Hide panel",
                "operations" => "Operations",
                "importSpecHint" => "Import a spec to view operations.",
                "askRequest" => "Ask about this request...",
                "workspaces" => "Workspaces",
                "noWorkspace" => "No workspace",
                "search" => "Search",
                "clearSearch" => "Clear search",
                "newWorkspace" => "New workspace",
                "localWorkspace" => "Local workspace",
                "remoteWorkspace" => "Remote workspace",
                "removeUnavailable" => "Remove unavailable workspace",
                "more" => "More",
                "configure" => "Configure",
                "openInExplorer" => "Open in explorer",
                "removeFromList" => "Remove from list",
                "deleteWorkspaceFolder" => "Delete folder",
                "confirmDeleteWorkspace" => "Confirm delete folder",
                "collections" => "Collections",
                "newCollection" => "New collection",
                "reloadCollections" => "Reload collections",
                "collapseAll" => "Collapse all",
                "newRequest" => "New request",
                "duplicate" => "Duplicate",
                "copy" => "Copy",
                "wrapResponse" => "Wrap response",
                "delete" => "Delete",
                "noMatchingRequests" => "No matching requests",
                "open" => "Open",
                "openDefault" => "Open default",
                "moveTo" => "Move to",
                "specifications" => "Specs",
                "newSpecification" => "New spec",
                "localFile" => "Local file",
                "remoteEndpoint" => "Remote endpoint",
                "reloadSpecifications" => "Reload specs",
                "addSpecHint" => "Add a local or remote spec.",
                "refreshRemoteDocument" => "Refresh remote document",
                "switchWorkspace" => "Switch workspace",
                "unavailable" => "Unavailable",
                "hideSidebar" => "Hide sidebar",
                "resizeSidebar" => "Resize sidebar",
                "resizeInspector" => "Resize inspector",
                "gitWorkspace" => "Git workspace",
                "noMatchingCollections" => "No matching collections.",
                "createOrLoadCollection" => "Create or load a collection.",
                "workspaceUnavailableHint" => "Workspace unavailable",
                "requestLower" => "request",
                "requestsLower" => "requests",
                "local" => "Local",
                "workspace" => "Workspace",
                "collection" => "Collection",
                "specification" => "Spec",
                "scope" => "Scope",
                "name" => "Name",
                "type" => "Type",
                "path" => "Path",
                "environment" => "Environment",
                "environments" => "Environments",
                "noEnvironment" => "No environment",
                "newEnvironment" => "New environment",
                "workspaceVariables" => "Workspace variables",
                "runtime" => "Runtime",
                "requestInfo" => "Info",
                "currentCollection" => "Current collection",
                "notSaved" => "Not saved",
                "saveWorkspace" => "Save Workspace",
                "requestDirectory" => "Request directory",
                "source" => "Source",
                "requests" => "Requests",
                "saveCollection" => "Save Collection",
                "document" => "Document",
                "lastRefresh" => "Last refresh",
                "cachedDocument" => "Cached document",
                "saveSpecification" => "Save Spec",
                "format" => "Format",
                "about" => "About",
                "sort" => "Sort",
                "sortByName" => "Name",
                "sortBySize" => "Size",
                "sortByUpdated" => "Updated time",
                "collectionsAboutDescription" => "Collections are local request assets stored in the workspace. They can carry scoped environment variables and request files.",
                "specsAboutDescription" => "Specs are API documents such as OpenAPI, Swagger, HTTP, curl, or remote documents. They are normalized for preview and request generation.",
                "refresh" => "Refresh",
                "method" => "Method",
                "activity" => "Activity",
                "empty" => "Empty",
                "chars" => "chars",
                _ => key
            };
        }

        return key switch
        {
            "settings" => "设置",
            "global" => "全局",
            "preferences" => "偏好",
            "theme" => "主题",
            "language" => "语言",
            "fontSize" => "字体大小",
            "system" => "跟随系统",
            "light" => "亮色",
            "dark" => "暗色",
            "small" => "小",
            "medium" => "中",
            "large" => "大",
            "proxy" => "代理",
            "noProxy" => "无代理",
            "systemProxy" => "系统代理",
            "customProxy" => "自定义代理",
            "send" => "发送",
            "close" => "关闭",
            "save" => "保存",
            "closeOthers" => "关闭其他",
            "closeAll" => "全部关闭",
            "discard" => "不保存",
            "cancel" => "取消",
            "unsaved" => "有未保存的修改",
            "docs" => "文档",
            "parameters" => "参数",
            "hidePanel" => "隐藏面板",
            "operations" => "接口",
            "importSpecHint" => "导入文档后查看接口。",
            "askRequest" => "询问当前请求...",
            "workspaces" => "工作区",
            "noWorkspace" => "未加载工作区",
            "search" => "搜索",
            "clearSearch" => "清除搜索",
            "newWorkspace" => "新增工作区",
            "localWorkspace" => "本地工作区",
            "remoteWorkspace" => "远程工作区",
            "removeUnavailable" => "移除不可用工作区",
            "more" => "更多",
            "configure" => "配置",
            "openInExplorer" => "在文件管理器中打开",
            "removeFromList" => "从列表移除",
            "deleteWorkspaceFolder" => "删除文件夹",
            "confirmDeleteWorkspace" => "确认删除文件夹",
            "collections" => "集合",
            "newCollection" => "新增集合",
            "reloadCollections" => "重新加载集合",
            "collapseAll" => "全部收起",
            "newRequest" => "新增请求",
            "duplicate" => "复制",
            "copy" => "复制",
            "wrapResponse" => "响应自动换行",
            "delete" => "删除",
            "noMatchingRequests" => "没有匹配的请求",
            "open" => "打开",
            "openDefault" => "默认打开",
            "moveTo" => "移动到",
            "specifications" => "文档",
            "newSpecification" => "新增文档",
            "localFile" => "本地文件",
            "remoteEndpoint" => "远程端点",
            "reloadSpecifications" => "重新加载文档",
            "addSpecHint" => "添加本地或远程文档。",
            "refreshRemoteDocument" => "刷新远程文档",
            "switchWorkspace" => "快速切换工作区",
            "unavailable" => "不可用",
            "hideSidebar" => "隐藏左栏",
            "resizeSidebar" => "调整左栏宽度",
            "resizeInspector" => "调整右栏宽度",
            "gitWorkspace" => "Git 工作区",
            "noMatchingCollections" => "没有匹配的集合。",
            "createOrLoadCollection" => "创建或加载集合。",
            "workspaceUnavailableHint" => "工作区不可用",
            "requestLower" => "个请求",
            "requestsLower" => "个请求",
            "local" => "本地",
            "workspace" => "工作区",
            "collection" => "集合",
            "specification" => "文档",
            "scope" => "范围",
            "name" => "名称",
            "type" => "类型",
            "path" => "路径",
            "environment" => "环境",
            "environments" => "环境",
            "noEnvironment" => "无环境",
            "newEnvironment" => "新增环境",
            "workspaceVariables" => "工作区变量",
            "runtime" => "运行",
            "requestInfo" => "信息",
            "currentCollection" => "当前集合",
            "notSaved" => "未保存",
            "saveWorkspace" => "保存工作区",
            "requestDirectory" => "请求目录",
            "source" => "来源",
            "requests" => "请求",
            "saveCollection" => "保存集合",
            "document" => "文档",
            "lastRefresh" => "最近刷新",
            "cachedDocument" => "缓存文档",
            "saveSpecification" => "保存文档",
            "format" => "格式化",
            "about" => "关于",
            "sort" => "排序",
            "sortByName" => "名称",
            "sortBySize" => "大小",
            "sortByUpdated" => "更新时间",
            "collectionsAboutDescription" => "集合是工作区里的本地请求资产，可以承载分层环境变量和请求文件。",
            "specsAboutDescription" => "文档用于承载 OpenAPI、Swagger、HTTP、curl 或远程文档，会转换为可预览和生成请求的中间态。",
            "refresh" => "刷新",
            "method" => "方法",
            "activity" => "活动",
            "empty" => "空",
            "chars" => "字符",
            _ => key
        };
    }

    public void SetAiDraft(string value)
    {
        AiDraft = value;
        NotifyChanged();
    }

    public async Task NewWorkspaceAsync()
    {
        try
        {
            var path = await _filePicker.PickFolderAsync("New workspace folder");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            WorkspacePath = path;
            var workspaceFile = Path.Combine(WorkspacePath, "workspace.json");
            if (File.Exists(workspaceFile))
            {
                await LoadWorkspaceAsync();
                return;
            }

            Directory.CreateDirectory(WorkspacePath);
            EnsureWorkspaceLayout(WorkspacePath);
            Workspace = new ReadableWorkspace
            {
                Name = Path.GetFileName(WorkspacePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                Type = IsGitRepository(WorkspacePath) ? ReadableWorkspaceType.Git : ReadableWorkspaceType.Local
            };
            if (Workspace.Type == ReadableWorkspaceType.Git)
            {
                Workspace.Git = new ReadableGitOptions();
            }
            Collections = Workspace.Collections;
            SelectedCollection = null;
            SelectedRequest = null;
            await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);
            WorkspaceStatus = $"Created: {Workspace.Name}";
            AddWorkspaceOption(WorkspacePath);
            AddActivity("Workspace", "已创建空工作区");
            SaveSettings();
            NotifyChanged();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            WorkspaceStatus = $"Create failed: {exception.Message}";
            AddActivity("Workspace", WorkspaceStatus);
            NotifyChanged();
        }
    }

    public async Task NewRemoteWorkspaceAsync()
    {
        try
        {
            var path = await _filePicker.PickFolderAsync("New remote workspace folder");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            WorkspacePath = path;
            Directory.CreateDirectory(WorkspacePath);
            EnsureWorkspaceLayout(WorkspacePath);
            Workspace = new ReadableWorkspace
            {
                Name = Path.GetFileName(WorkspacePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                Type = ReadableWorkspaceType.Git,
                Git = new ReadableGitOptions()
            };
            Collections = Workspace.Collections;
            SelectedCollection = null;
            SelectedRequest = null;
            await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);
            AddWorkspaceOption(WorkspacePath);
            OpenWorkspaceConfigTab();
            WorkspaceStatus = $"Created remote workspace: {Workspace.Name}";
            AddActivity("Workspace", "已创建远程工作区配置，请填写 Remote URL");
            SaveSettings();
            NotifyChanged();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            WorkspaceStatus = $"Create failed: {exception.Message}";
            AddActivity("Workspace", WorkspaceStatus);
            NotifyChanged();
        }
    }

    public async Task LoadWorkspaceAsync()
    {
        if (!ValidateWorkspacePath(WorkspacePath, out var message))
        {
            WorkspaceStatus = message;
            AddActivity("Workspace", message);
            NotifyChanged();
            return;
        }

        try
        {
            AddWorkspaceOption(WorkspacePath);
            Workspace = await _workspaceStore.LoadWorkspaceAsync(WorkspacePath);
            EnsureWorkspaceLayout(WorkspacePath);
            var workspaceTypeChanged = ApplyGitWorkspaceType(WorkspacePath, Workspace);
            Collections = BuildCollectionTree(Workspace.Collections).ToList();
            await AddDiscoveredCollectionsAsync();
            await LoadAllCollectionRequestsAsync();
            AddDiscoveredEnvironments();
            AddDiscoveredSpecifications();
            if (!string.IsNullOrWhiteSpace(SelectedEnvironmentId) && SelectedEnvironment is null)
            {
                SelectedEnvironmentId = string.Empty;
            }

            SelectedCollection = null;
            SelectedRequest = null;
            WorkspaceStatus = $"Loaded: {Workspace.Name}";
            AddActivity("Workspace", WorkspaceStatus);
            if (workspaceTypeChanged)
            {
                await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);
            }

            SaveSettings();
            NotifyChanged();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            WorkspaceStatus = $"Load failed: {exception.Message}";
            AddActivity("Workspace", WorkspaceStatus);
            NotifyChanged();
        }
    }

    public async Task LoadTryDocumentAsync()
    {
        try
        {
            var document = await new ReadableTryDocumentLoader().LoadAsync(TryFilePath);
            ActiveSource = document.SourceType.ToString();
            DocumentTitle = document.Title ?? document.FileName ?? "Try Document";
            RawContent = document.RawContent;
            Operations = document.Operations;
            ViewMode = "preview";
            AddSpecFile(TryFilePath);
            OpenSpecificationDocumentTab(DocumentTitle, TryFilePath);
            AddActivity("Import", $"已转换 {Operations.Count} 个 operation");
            SaveSettings();
            NotifyChanged();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            Operations = [];
            RawContent = string.Empty;
            AddActivity("Spec", $"加载失败：{exception.Message}");
            NotifyChanged();
        }
    }

    public async Task PickWorkspaceAsync()
    {
        var path = await _filePicker.PickWorkspaceAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await SelectWorkspaceAsync(path);
            return;
        }

        WorkspaceStatus = "请选择有效的 workspace.json";
        AddActivity("Workspace", WorkspaceStatus);
        NotifyChanged();
    }

    public async Task PickTryFileAsync()
    {
        if (Workspace is null)
        {
            WorkspaceStatus = "请先新建或加载 workspace";
            AddActivity("Workspace", WorkspaceStatus);
            NotifyChanged();
            return;
        }

        var path = await _filePicker.PickTryFileAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            var storedPath = await CopySpecificationIntoWorkspaceAsync(path);
            TryFilePath = ResolveWorkspacePath(storedPath);
            AddSpecFile(TryFilePath);
            AddLocalSpecification(storedPath);
            ActiveSource = "API Spec";
            DocumentTitle = Path.GetFileName(storedPath);
            ViewMode = "preview";
            SaveSettings();
            await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);
            NotifyChanged();
        }
    }

    public async Task PasteImportAsync()
    {
        var text = await Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.GetTextAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            AddActivity("Import", "剪贴板没有可导入的文本");
            NotifyChanged();
            return;
        }

        var fileName = $"clipboard-{DateTime.Now:yyyyMMdd-HHmmss}{GuessClipboardImportExtension(text)}";
        string importPath;
        if (Workspace is not null)
        {
            EnsureWorkspaceLayout(WorkspacePath);
            var targetDirectory = Path.Combine(WorkspacePath, "specs");
            Directory.CreateDirectory(targetDirectory);
            importPath = NextAvailablePath(targetDirectory, fileName);
            await File.WriteAllTextAsync(importPath, text);

            var storedPath = Path.GetRelativePath(WorkspacePath, importPath).Replace('\\', '/');
            TryFilePath = importPath;
            AddSpecFile(TryFilePath);
            AddLocalSpecification(storedPath);
            await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);
        }
        else
        {
            var targetDirectory = Path.Combine(Microsoft.Maui.Storage.FileSystem.Current.CacheDirectory, "ReadableHttp", "clipboard-imports");
            Directory.CreateDirectory(targetDirectory);
            importPath = NextAvailablePath(targetDirectory, fileName);
            await File.WriteAllTextAsync(importPath, text);
            TryFilePath = importPath;
            AddSpecFile(TryFilePath);
        }

        AddActivity("Import", $"已粘贴导入 {Path.GetFileName(importPath)}");
        await LoadTryDocumentAsync();
    }

    public async Task NewRemoteSpecificationAsync()
    {
        if (Workspace is null)
        {
            WorkspaceStatus = "请先新建或加载 workspace";
            AddActivity("Workspace", WorkspaceStatus);
            NotifyChanged();
            return;
        }

        var specification = new ReadableSpecification
        {
            Name = NextSpecificationName("Remote Document"),
            SourceType = ReadableSpecificationSourceType.RemoteEndpoint,
            Format = ReadableSpecificationFormat.OpenApi,
            Remote = new ReadableRemoteSpecificationOptions(),
            Path = $"specs/remote-{Guid.NewGuid():N}.json"
        };
        Workspace.Specifications.Add(specification);
        SelectedSpecification = specification;
        await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);
        OpenSpecificationConfigTab(specification);
        AddActivity("Spec", "已创建远程文档，请填写 Endpoint");
        NotifyChanged();
    }

    public void ToggleExplorer()
    {
        ShowExplorer = !ShowExplorer;
        NotifyChanged();
    }

    public void ToggleInspector()
    {
        ShowInspector = !ShowInspector;
        NotifyChanged();
    }

    public void ToggleWorkspaceSection()
    {
        ShowWorkspaceSection = !ShowWorkspaceSection;
        NotifyChanged();
    }

    public void ToggleCollectionsSection()
    {
        ShowCollectionsSection = !ShowCollectionsSection;
        NotifyChanged();
    }

    public void ToggleSpecsSection()
    {
        ShowSpecsSection = !ShowSpecsSection;
        NotifyChanged();
    }

    public void ToggleRecentSection()
    {
        ShowRecentSection = !ShowRecentSection;
        NotifyChanged();
    }

    public void OpenCollectionsAbout()
    {
        OpenAboutTab("collections", T("collections"));
        NotifyChanged();
    }

    public void OpenSpecsAbout()
    {
        OpenAboutTab("specs", T("specifications"));
        NotifyChanged();
    }

    public void ShowRightPanel(RightPanelMode mode)
    {
        if (mode == RightPanelMode.Settings)
        {
            OpenSettingsTab();
            NotifyChanged();
            return;
        }

        if (mode is not (RightPanelMode.Ai or RightPanelMode.History))
        {
            mode = RightPanelMode.Ai;
        }

        RightPanelMode = mode;
        ShowInspector = true;
        NotifyChanged();
    }

    public async Task SelectCollectionAsync(ReadableCollection collection)
    {
        SelectedCollection = collection;
        await LoadCollectionRequestsAsync(collection);
        SelectedRequest = null;
        DocumentTitle = collection.Name;
        ActiveSource = collection.SourceType.ToString();
        AddSession(collection.Name);
        NotifyChanged();
    }

    public async Task SelectCollectionAsync(CollectionEventArgs args) => await SelectCollectionAsync(args.Collection);

    public async Task OpenCollectionConfigAsync(ReadableCollection collection)
    {
        SelectedCollection = collection;
        await LoadCollectionRequestsAsync(collection);
        SelectedRequest = null;
        DocumentTitle = collection.Name;
        ActiveSource = collection.SourceType.ToString();
        OpenCollectionConfigTab(collection);
        NotifyChanged();
    }

    public async Task OpenCollectionConfigAsync(CollectionEventArgs args) => await OpenCollectionConfigAsync(args.Collection);

    public async Task SelectRequestAsync(ReadableCollection collection, ReadableRequest request)
    {
        SelectedCollection = collection;
        await LoadCollectionRequestsAsync(collection);
        SelectedRequest = collection.Requests.FirstOrDefault(item => string.Equals(item.Id, request.Id, StringComparison.OrdinalIgnoreCase))
            ?? request;
        OpenCollectionRequestTab(collection, SelectedRequest);
        NotifyChanged();
    }

    public async Task SelectRequestAsync(RequestEventArgs args) => await SelectRequestAsync(args.Collection, args.Request);

    public async Task GitStatusAsync()
    {
        WorkspaceStatus = await new ReadableWorkspaceGitService().StatusAsync(WorkspacePath);
        AddActivity("Git status", FirstLine(WorkspaceStatus));
        NotifyChanged();
    }

    public async Task GitPullAsync()
    {
        WorkspaceStatus = await new ReadableWorkspaceGitService().PullAsync(WorkspacePath);
        AddActivity("Git pull", FirstLine(WorkspaceStatus));
        NotifyChanged();
    }

    public async Task GitPushAsync()
    {
        WorkspaceStatus = await new ReadableWorkspaceGitService().PushAsync(WorkspacePath);
        AddActivity("Git push", FirstLine(WorkspaceStatus));
        NotifyChanged();
    }

    public void SaveSettings()
    {
        var draftProxyMode = NormalizeProxyMode(SettingsDraft.ProxyMode);
        if (draftProxyMode == ProxyCustom && string.IsNullOrWhiteSpace(SettingsDraft.CustomProxyUrl))
        {
            AddActivity("Settings", "Custom proxy 需要填写 URL");
            NotifyChanged();
            return;
        }

        ThemeMode = NormalizeThemeMode(SettingsDraft.ThemeMode);
        DarkMode = ThemeMode == ThemeDark;
        ApplyApplicationTheme();
        FontSize = NormalizeFontSize(SettingsDraft.FontSize);
        ProxyMode = draftProxyMode;
        CustomProxyUrl = SettingsDraft.CustomProxyUrl.Trim();
        CustomProxyUsername = SettingsDraft.CustomProxyUsername.Trim();
        CustomProxyPassword = SettingsDraft.CustomProxyPassword;
        Language = string.IsNullOrWhiteSpace(SettingsDraft.Language) ? "system" : SettingsDraft.Language;
        DevToolsEnabled = SettingsDraft.DevToolsEnabled;
        PersistSettings();
        AddActivity("Settings", "配置已保存");
        NotifyChanged();
    }

    public void SelectOperation(ReadableTryOperation operation)
    {
        SelectedRequest = null;
        OpenSpecificationRequestTab(operation);
        AddSession(operation.Name);
        NotifyChanged();
    }

    public async Task ImportSpecAsCollectionAsync()
    {
        if (Workspace is null)
        {
            AddActivity("Spec", "请先加载 workspace 后再导入 collection");
            NotifyChanged();
            return;
        }

        if (Operations.Count == 0)
        {
            AddActivity("Spec", "当前文档没有可导入的请求");
            NotifyChanged();
            return;
        }

        var collection = new ReadableCollection
        {
            Name = string.IsNullOrWhiteSpace(DocumentTitle) ? "Imported Spec" : DocumentTitle,
            SourceType = ReadableCollectionSourceType.Local,
            RequestDirectory = $"collections/{ToFileName(DocumentTitle)}",
            Requests = Operations.Select(operation =>
            {
                var request = CloneRequest(operation.Request);
                request.Name = string.IsNullOrWhiteSpace(operation.Name) ? $"{operation.Method} {operation.Path}" : operation.Name;
                return request;
            }).ToList()
        };

        Collections.Add(collection);
        Workspace.Collections.Add(collection);
        SelectedCollection = collection;
        await SaveWorkspaceAsync();
        AddActivity("Spec", $"已导入 {collection.Requests.Count} 个请求到 collection");
        NotifyChanged();
    }

    public async Task SendAsync()
    {
        IsSending = true;
        var tab = EnsureActiveRequestTab();
        var startedAt = DateTimeOffset.UtcNow;
        tab.StatusText = "Sending...";
        tab.ResponseText = string.Empty;
        tab.ResponseNodePath = "root";
        tab.PipelinePhases = CreateStreamingPipelinePhases(startedAt, "Sending");
        await NotifyChangedAsync();

        try
        {
            var request = BuildRequestFromTab(tab);
            await foreach (var message in new ReadableHttpExecutor().StreamAsync(request, CreateExecutionContext()))
            {
                ApplyStreamMessage(tab, message);
                tab.PipelinePhases = CreateStreamingPipelinePhases(startedAt, message.Type.ToString());
                await NotifyChangedAsync();
                await Task.Yield();
            }

            AddActivity(tab.Method, $"{tab.StatusText} {tab.Url}");
            await SaveActiveRequestAfterSendAsync(tab);
        }
        catch (Exception exception) when (IsSendFailure(exception))
        {
            tab.StatusText = $"ERROR {exception.GetType().Name}";
            tab.ResponseText = FormatSendFailure(exception);
            AddActivity(tab.Method, $"{tab.StatusText} {tab.Url}");
        }
        finally
        {
            IsSending = false;
            tab.PipelinePhases = CreateStreamingPipelinePhases(startedAt, "Completed");
            AddHistoryRecord(tab, startedAt);
            await NotifyChangedAsync();
        }
    }

    public void NewScratchRequest()
    {
        SelectedRequest = null;
        OpenScratchTab();
        AddSession(DocumentTitle);
        NotifyChanged();
    }

    public void NewCollection()
    {
        if (Workspace is null || !ValidateWorkspacePath(WorkspacePath, out _))
        {
            WorkspaceStatus = "请先新建或加载 workspace";
            AddActivity("Workspace", WorkspaceStatus);
            NotifyChanged();
            return;
        }

        var collection = new ReadableCollection
        {
            Name = $"New Collection {EnumerateCollections(Collections).Count() + 1}",
            SourceType = ReadableCollectionSourceType.Local,
            RequestDirectory = $"collections/collection-{EnumerateCollections(Collections).Count() + 1}"
        };
        Collections.Add(collection);
        Workspace.Collections = Collections;
        SelectedCollection = collection;
        SelectedRequest = null;
        DocumentTitle = collection.Name;
        ActiveSource = collection.SourceType.ToString();
        OpenCollectionConfigTab(collection);
        AddActivity("Collection", $"已创建 {collection.Name}");
        NotifyChanged();
    }

    public async Task NewRequestInCollectionAsync(ReadableCollection collection)
    {
        SelectedCollection = collection;
        await LoadCollectionRequestsAsync(collection);
        var request = new ReadableRequest
        {
            Name = $"{collection.Name} Request {collection.Requests.Count + 1}",
            Method = "GET",
            Url = "https://httpbin.org/get"
        };
        collection.Requests.Add(request);
        SelectedRequest = request;
        OpenCollectionRequestTab(collection, request);
        ActiveSource = "Collection";
        AddActivity("Request", $"已在 {collection.Name} 中新增请求");
        await SaveWorkspaceAsync();
        NotifyChanged();
    }

    public async Task NewRequestInCollectionAsync(CollectionEventArgs args) => await NewRequestInCollectionAsync(args.Collection);

    public async Task SelectSpecAsync(string path)
    {
        TryFilePath = path;
        DocumentTitle = Path.GetFileName(path);
        ActiveSource = "API Spec";
        ViewMode = "preview";
        Operations = [];
        RawContent = string.Empty;
        AddActivity("Spec", $"切换到 {DocumentTitle}");
        await LoadTryDocumentAsync();
    }

    public async Task SelectSpecAsync(SpecSelectedEventArgs args)
    {
        await SelectSpecAsync(args.Path);
    }

    public async Task SelectSpecificationAsync(ReadableSpecification specification)
    {
        SelectedSpecification = specification;
        if (specification.SourceType == ReadableSpecificationSourceType.RemoteEndpoint)
        {
            if (string.IsNullOrWhiteSpace(specification.Remote?.Endpoint))
            {
                OpenSpecificationConfigTab(specification);
                AddActivity("Spec", $"{specification.Name} 需要填写 Endpoint");
                NotifyChanged();
                return;
            }

            if (await OpenCachedSpecificationDocumentAsync(specification))
            {
                return;
            }

            await RefreshSpecificationAsync(specification);
            return;
        }

        if (string.IsNullOrWhiteSpace(specification.Path))
        {
            OpenSpecificationConfigTab(specification);
            AddActivity("Spec", $"{specification.Name} has no local document path");
            NotifyChanged();
            return;
        }

        TryFilePath = ResolveWorkspacePath(specification.Path);
        DocumentTitle = specification.Name;
        ActiveSource = specification.SourceType.ToString();
        ViewMode = "preview";
        AddActivity("Spec", $"打开 {specification.Name}");
        await LoadTryDocumentAsync();
    }

    public async Task SelectSpecificationAsync(SpecificationEventArgs args) => await SelectSpecificationAsync(args.Specification);

    public async Task RefreshSpecificationAsync(ReadableSpecification specification)
    {
        if (Workspace is null)
        {
            AddActivity("Spec", "请先加载 workspace");
            NotifyChanged();
            return;
        }

        try
        {
            var document = await new ReadableSpecificationRefresher().RefreshAsync(WorkspacePath, specification);
            ApplySpecificationDocument(specification, document, ResolveWorkspacePath(specification.NormalizedPath ?? specification.Path ?? string.Empty));
            await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);
            AddActivity("Spec", specification.Remote?.UpdateAvailable == true
                ? $"已刷新 {specification.Name}，发现变更"
                : $"已刷新 {specification.Name}");
            NotifyChanged();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or HttpRequestException or JsonException)
        {
            OpenSpecificationConfigTab(specification);
            AddActivity("Spec", $"刷新失败：{exception.Message}");
            NotifyChanged();
        }
    }

    public async Task RefreshSpecificationAsync(SpecificationEventArgs args) => await RefreshSpecificationAsync(args.Specification);

    public void OpenSpecificationConfig(ReadableSpecification specification)
    {
        SelectedSpecification = specification;
        OpenSpecificationConfigTab(specification);
        NotifyChanged();
    }

    public void OpenSpecificationConfigAsync(SpecificationEventArgs args) => OpenSpecificationConfig(args.Specification);

    public async Task RefreshSelectedSpecificationAsync()
    {
        if (SelectedSpecification is null)
        {
            AddActivity("Spec", "没有选中的 specification");
            NotifyChanged();
            return;
        }

        await RefreshSpecificationAsync(SelectedSpecification);
    }

    private async Task<bool> OpenCachedSpecificationDocumentAsync(ReadableSpecification specification)
    {
        if (string.IsNullOrWhiteSpace(specification.NormalizedPath))
        {
            return false;
        }

        var cachePath = ResolveWorkspacePath(specification.NormalizedPath);
        if (!File.Exists(cachePath))
        {
            return false;
        }

        try
        {
            var document = await new ReadableHttpJsonStorage().LoadAsync<ReadableTryDocument>(cachePath);
            ApplySpecificationDocument(specification, document, cachePath);
            AddActivity("Spec", $"已打开缓存 {specification.Name}");
            NotifyChanged();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            OpenSpecificationConfigTab(specification);
            AddActivity("Spec", $"缓存读取失败：{exception.Message}");
            NotifyChanged();
            return true;
        }
    }

    public async Task DuplicateSpecificationAsync(ReadableSpecification specification)
    {
        if (Workspace is null)
        {
            return;
        }

        var json = JsonSerializer.Serialize(specification, ReadableHttpJsonStorage.JsonOptions);
        var clone = JsonSerializer.Deserialize<ReadableSpecification>(json, ReadableHttpJsonStorage.JsonOptions) ?? new ReadableSpecification();
        clone.Id = Guid.NewGuid().ToString("N");
        clone.Name = NextSpecificationName($"{specification.Name} Copy");
        Workspace.Specifications.Add(clone);
        await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);
        AddActivity("Spec", $"已复制 {specification.Name}");
        NotifyChanged();
    }

    public async Task DuplicateSpecificationAsync(SpecificationEventArgs args) => await DuplicateSpecificationAsync(args.Specification);

    public async Task DeleteSpecificationAsync(ReadableSpecification specification)
    {
        if (Workspace is null)
        {
            return;
        }

        Workspace.Specifications.RemoveAll(item => string.Equals(item.Id, specification.Id, StringComparison.Ordinal));
        DeleteSpecificationFiles(specification);
        await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);
        RequestTabs.RemoveAll(tab =>
            tab.Origin == RequestTabOrigin.SpecificationDocument
            && !string.IsNullOrWhiteSpace(specification.Path)
            && string.Equals(tab.SourceKey, ResolveWorkspacePath(specification.Path), StringComparison.OrdinalIgnoreCase));
        AddActivity("Spec", $"已删除 {specification.Name}");
        NotifyChanged();
    }

    public async Task DeleteSpecificationAsync(SpecificationEventArgs args) => await DeleteSpecificationAsync(args.Specification);

    public async Task SaveSpecificationAsync()
    {
        if (Workspace is null || SelectedSpecification is null)
        {
            return;
        }

        await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);
        AddActivity("Spec", $"已保存 {SelectedSpecification.Name}");
        NotifyChanged();
    }

    public async Task SelectWorkspaceAsync(string path)
    {
        if (!ValidateWorkspacePath(path, out var message))
        {
            WorkspaceStatus = message;
            AddActivity("Workspace", message);
            NotifyChanged();
            return;
        }

        WorkspacePath = path;
        WorkspaceStatus = $"Selected: {WorkspaceDisplayName(path)}";
        await LoadWorkspaceAsync();
    }

    public async Task SelectWorkspaceAsync(WorkspaceSelectedEventArgs args) => await SelectWorkspaceAsync(args.Path);

    public async Task OpenWorkspaceConfigAsync(string path)
    {
        if (!string.Equals(WorkspacePath, path, StringComparison.OrdinalIgnoreCase))
        {
            await SelectWorkspaceAsync(path);
        }

        if (Workspace is null)
        {
            WorkspaceStatus = "没有可配置的 workspace";
            AddActivity("Workspace", WorkspaceStatus);
            NotifyChanged();
            return;
        }

        OpenWorkspaceConfigTab();
        NotifyChanged();
    }

    public async Task OpenWorkspaceConfigAsync(WorkspaceSelectedEventArgs args) => await OpenWorkspaceConfigAsync(args.Path);

    public void OpenWorkspaceInExplorer(string path)
    {
        ExplorePathInShell(path);
    }

    public void OpenWorkspaceInExplorer(WorkspaceSelectedEventArgs args) => OpenWorkspaceInExplorer(args.Path);

    public void RemoveWorkspaceOption(string path)
    {
        WorkspaceOptions.RemoveAll(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase));
        if (string.Equals(WorkspacePath, path, StringComparison.OrdinalIgnoreCase))
        {
            WorkspacePath = string.Empty;
            Workspace = null;
            Collections = [];
            SelectedCollection = null;
            SelectedRequest = null;
            SelectedSpecification = null;
            Operations = [];
            RawContent = string.Empty;
            DocumentTitle = "Scratch Request";
            ActiveSource = "Request";
            WorkspaceStatus = "Workspace removed from history";
            RequestTabs.RemoveAll(tab => tab.Origin is RequestTabOrigin.Workspace
                or RequestTabOrigin.CollectionConfig
                or RequestTabOrigin.Collection
                or RequestTabOrigin.SpecificationConfig
                or RequestTabOrigin.SpecificationDocument);
            ActiveRequestTabId = RequestTabs.FirstOrDefault()?.Id;
        }

        PersistSettings();
        AddActivity("Workspace", $"已从历史移除 {WorkspaceDisplayName(path)}");
        NotifyChanged();
    }

    public void RemoveWorkspaceOption(WorkspaceSelectedEventArgs args) => RemoveWorkspaceOption(args.Path);

    public Task DeleteWorkspaceFolderAsync(WorkspaceSelectedEventArgs args) => DeleteWorkspaceFolderAsync(args.Path);

    public Task DeleteWorkspaceFolderAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            RemoveWorkspaceOption(path);
            return Task.CompletedTask;
        }

        var workspaceFile = Path.Combine(path, "workspace.json");
        if (!File.Exists(workspaceFile))
        {
            WorkspaceStatus = "拒绝删除：目标不是有效 workspace";
            AddActivity("Workspace", WorkspaceStatus);
            NotifyChanged();
            return Task.CompletedTask;
        }

        try
        {
            Directory.Delete(path, recursive: true);
            RemoveWorkspaceOption(path);
            WorkspaceStatus = $"Deleted workspace folder: {Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}";
            AddActivity("Workspace", WorkspaceStatus);
            NotifyChanged();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            WorkspaceStatus = $"Delete failed: {exception.Message}";
            AddActivity("Workspace", WorkspaceStatus);
            NotifyChanged();
        }

        return Task.CompletedTask;
    }

    public async Task SaveWorkspaceAsync()
    {
        if (Workspace is null)
        {
            WorkspaceStatus = "没有可保存的 workspace";
            AddActivity("Workspace", WorkspaceStatus);
            NotifyChanged();
            return;
        }

        Workspace.Collections = Collections.ToList();
        await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);

        WorkspaceStatus = $"Saved: {Workspace.Name}";
        AddActivity("Workspace", WorkspaceStatus);
        NotifyChanged();
    }

    public async Task DeleteSelectedCollectionAsync()
    {
        if (Workspace is null || SelectedCollection is null)
        {
            return;
        }

        var collection = SelectedCollection;
        RemoveCollectionFromTree(collection);
        Workspace.Collections = Collections;
        await _workspaceStore.DeleteCollectionRequestsAsync(WorkspacePath, collection);
        await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);
        SelectedCollection = FirstVisibleCollection();
        if (SelectedCollection is not null)
        {
            await LoadCollectionRequestsAsync(SelectedCollection);
        }

        SelectedRequest = null;
        if (SelectedCollection is not null)
        {
            OpenCollectionConfigTab(SelectedCollection);
        }
        else
        {
            OpenWorkspaceConfigTab();
        }
        WorkspaceStatus = $"Deleted collection: {collection.Name}";
        AddActivity("Collection", WorkspaceStatus);
        NotifyChanged();
    }

    public async Task DeleteSelectedRequestAsync()
    {
        if (SelectedCollection is null || SelectedRequest is null)
        {
            return;
        }

        var request = SelectedRequest;
        SelectedCollection.Requests.Remove(request);
        await _workspaceStore.DeleteRequestAsync(WorkspacePath, SelectedCollection, request);
        CloseTabsForRequest(request.Id);
        SelectedRequest = SelectedCollection.Requests.FirstOrDefault();
        if (SelectedRequest is not null)
        {
            OpenCollectionRequestTab(SelectedCollection, SelectedRequest);
        }
        else
        {
            OpenCollectionConfigTab(SelectedCollection);
        }
        WorkspaceStatus = $"Deleted request: {request.Name}";
        AddActivity("Request", WorkspaceStatus);
        NotifyChanged();
    }

    public async Task DuplicateCollectionAsync(ReadableCollection collection)
    {
        if (Workspace is null)
        {
            return;
        }

        await LoadCollectionRequestsAsync(collection);
        var clone = new ReadableCollection
        {
            Name = NextCollectionName($"{collection.Name} Copy"),
            SourceType = collection.SourceType,
            RequestDirectory = $"collections/{ToFileName(collection.Name)}-copy-{EnumerateCollections(Collections).Count() + 1}",
            Requests = collection.Requests.Select(CloneRequest).ToList()
        };
        Collections.Add(clone);
        Workspace.Collections = Collections;
        await SaveWorkspaceAsync();
        SelectedCollection = clone;
        SelectedRequest = null;
        OpenCollectionConfigTab(clone);
        AddActivity("Collection", $"已复制 {collection.Name}");
        NotifyChanged();
    }

    public async Task DuplicateCollectionAsync(CollectionEventArgs args) => await DuplicateCollectionAsync(args.Collection);

    public async Task DeleteCollectionAsync(ReadableCollection collection)
    {
        if (Workspace is null)
        {
            return;
        }

        RemoveCollectionFromTree(collection);
        Workspace.Collections = Collections;
        await _workspaceStore.DeleteCollectionRequestsAsync(WorkspacePath, collection);
        await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);
        if (ReferenceEquals(SelectedCollection, collection))
        {
            SelectedCollection = FirstVisibleCollection();
            SelectedRequest = null;
            if (SelectedCollection is not null)
            {
                OpenCollectionConfigTab(SelectedCollection);
            }
            else
            {
                OpenWorkspaceConfigTab();
            }
        }
        AddActivity("Collection", $"已删除 {collection.Name}");
        NotifyChanged();
    }

    public async Task DeleteCollectionAsync(CollectionEventArgs args) => await DeleteCollectionAsync(args.Collection);

    public async Task DuplicateRequestAsync(ReadableCollection collection, ReadableRequest request)
    {
        await LoadCollectionRequestsAsync(collection);
        var clone = CloneRequest(request);
        clone.Name = NextRequestName(collection, $"{request.Name} Copy");
        collection.Requests.Add(clone);
        await _workspaceStore.SaveRequestAsync(WorkspacePath, collection, clone);
        SelectedCollection = collection;
        SelectedRequest = clone;
        OpenCollectionRequestTab(collection, clone);
        AddActivity("Request", $"已复制 {request.Name}");
        NotifyChanged();
    }

    public async Task DuplicateRequestAsync(RequestEventArgs args) => await DuplicateRequestAsync(args.Collection, args.Request);

    public async Task MoveRequestAsync(RequestMoveEventArgs args)
    {
        if (ReferenceEquals(args.SourceCollection, args.TargetCollection)
            || string.Equals(args.SourceCollection.Id, args.TargetCollection.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await LoadCollectionRequestsAsync(args.SourceCollection);
        await LoadCollectionRequestsAsync(args.TargetCollection);
        var request = args.SourceCollection.Requests.FirstOrDefault(item =>
            string.Equals(item.Id, args.Request.Id, StringComparison.OrdinalIgnoreCase));
        if (request is null)
        {
            return;
        }

        args.SourceCollection.Requests.Remove(request);
        request.Name = NextRequestName(args.TargetCollection, request.Name);
        await _workspaceStore.MoveRequestAsync(WorkspacePath, args.SourceCollection, args.TargetCollection, request);
        args.TargetCollection.Requests.Add(request);
        CloseTabsForRequest(request.Id);
        SelectedCollection = args.TargetCollection;
        SelectedRequest = request;
        OpenCollectionRequestTab(args.TargetCollection, request);
        AddActivity("Request", $"已移动到 {args.TargetCollection.Name}");
        NotifyChanged();
    }

    public async Task MoveActiveRequestToCollectionAsync(string targetCollectionId)
    {
        var targetCollection = FindCollectionById(targetCollectionId);
        if (targetCollection is null)
        {
            return;
        }

        var tab = EnsureActiveRequestTab();
        if (tab.Origin == RequestTabOrigin.Collection)
        {
            var sourceCollection = FindCollectionById(tab.CollectionId);
            var request = sourceCollection?.Requests.FirstOrDefault(item =>
                string.Equals(GetRequestSourceKey(sourceCollection, item), tab.SourceKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.Id, tab.RequestId, StringComparison.OrdinalIgnoreCase));
            if (sourceCollection is not null && request is not null)
            {
                await MoveRequestAsync(new RequestMoveEventArgs(sourceCollection, request, targetCollection));
                return;
            }
        }

        await LoadCollectionRequestsAsync(targetCollection);
        var newRequest = new ReadableRequest
        {
            Name = NextRequestName(targetCollection, tab.Title)
        };
        ApplyTabToRequest(tab, newRequest);
        targetCollection.Requests.Add(newRequest);
        await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace!);
        await _workspaceStore.SaveRequestAsync(WorkspacePath, targetCollection, newRequest);

        tab.Origin = RequestTabOrigin.Collection;
        tab.CollectionId = targetCollection.Id;
        tab.RequestId = newRequest.Id;
        tab.SourceKey = GetRequestSourceKey(targetCollection, newRequest);
        tab.Title = newRequest.Name;
        tab.ActiveSource = "Collection";
        tab.IsDirty = false;
        SelectedCollection = targetCollection;
        SelectedRequest = newRequest;
        AddActivity("Request", $"已保存到 {targetCollection.Name}");
        NotifyChanged();
    }

    public async Task DeleteRequestAsync(ReadableCollection collection, ReadableRequest request)
    {
        await LoadCollectionRequestsAsync(collection);
        collection.Requests.RemoveAll(item => string.Equals(item.Id, request.Id, StringComparison.OrdinalIgnoreCase));
        await _workspaceStore.DeleteRequestAsync(WorkspacePath, collection, request);
        CloseTabsForRequest(request.Id);
        if (SelectedRequest is not null && string.Equals(SelectedRequest.Id, request.Id, StringComparison.OrdinalIgnoreCase))
        {
            SelectedRequest = null;
        }
        AddActivity("Request", $"已删除 {request.Name}");
        NotifyChanged();
    }

    public async Task DeleteRequestAsync(RequestEventArgs args) => await DeleteRequestAsync(args.Collection, args.Request);

    public void OpenCollectionInExplorer(ReadableCollection collection)
    {
        ExplorePathInShell(GetCollectionDirectory(collection));
    }

    public void OpenCollectionInExplorer(CollectionEventArgs args) => OpenCollectionInExplorer(args.Collection);

    public void OpenRequestDefault(ReadableCollection collection, ReadableRequest request)
    {
        OpenPathDefault(GetRequestPath(collection, request));
    }

    public void OpenRequestDefault(RequestEventArgs args) => OpenRequestDefault(args.Collection, args.Request);

    public void OpenSpecificationInExplorer(ReadableSpecification specification)
    {
        ExplorePathInShell(Path.Combine(WorkspacePath, "specs"));
    }

    public void OpenSpecificationInExplorer(SpecificationEventArgs args) => OpenSpecificationInExplorer(args.Specification);

    public async Task SelectRequestTab(string tabId)
    {
        var tab = RequestTabs.FirstOrDefault(item => item.Id == tabId);
        if (tab is null)
        {
            return;
        }

        ActivateTab(tab);
        if (tab.Origin == RequestTabOrigin.Workspace
            && !string.IsNullOrWhiteSpace(tab.SourceKey)
            && !string.Equals(WorkspacePath, tab.SourceKey, StringComparison.OrdinalIgnoreCase))
        {
            WorkspacePath = tab.SourceKey;
            await LoadWorkspaceAsync();
            return;
        }

        SyncSelectionFromTab(tab);
        NotifyChanged();
    }

    public void CloseRequestTab(string tabId)
    {
        var tab = RequestTabs.FirstOrDefault(item => item.Id == tabId);
        if (tab?.IsDirty == true)
        {
            PendingCloseTabId = tabId;
            NotifyChanged();
            return;
        }

        ForceCloseRequestTab(tabId);
    }

    public void ConfirmCloseRequestTab(string tabId)
    {
        PendingCloseTabId = null;
        ForceCloseRequestTab(tabId);
    }

    public void CancelCloseRequestTab()
    {
        PendingCloseTabId = null;
        NotifyChanged();
    }

    public async Task SaveRequestTabAsync(string tabId)
    {
        await SelectRequestTab(tabId);
        await SaveActiveRequestAsync();
    }

    public void CloseOtherRequestTabs(string tabId)
    {
        var dirtyTab = RequestTabs.FirstOrDefault(tab => tab.Id != tabId && tab.IsDirty);
        if (dirtyTab is not null)
        {
            PendingCloseTabId = dirtyTab.Id;
            NotifyChanged();
            return;
        }

        RequestTabs.RemoveAll(tab => tab.Id != tabId);
        ActiveRequestTabId = RequestTabs.FirstOrDefault()?.Id;
        if (ActiveRequestTab is not null)
        {
            ActivateTab(ActiveRequestTab);
            SyncSelectionFromTab(ActiveRequestTab);
        }

        NotifyChanged();
    }

    public void CloseAllRequestTabs()
    {
        var dirtyTab = RequestTabs.FirstOrDefault(tab => tab.IsDirty);
        if (dirtyTab is not null)
        {
            PendingCloseTabId = dirtyTab.Id;
            NotifyChanged();
            return;
        }

        RequestTabs.Clear();
        ActiveRequestTabId = null;
        SelectedRequest = null;
        NotifyChanged();
    }

    private void ForceCloseRequestTab(string tabId)
    {
        var index = RequestTabs.FindIndex(tab => tab.Id == tabId);
        if (index < 0)
        {
            return;
        }

        if (string.Equals(PendingCloseTabId, tabId, StringComparison.Ordinal))
        {
            PendingCloseTabId = null;
        }

        var wasActive = string.Equals(ActiveRequestTabId, tabId, StringComparison.Ordinal);
        RequestTabs.RemoveAt(index);
        if (wasActive && RequestTabs.Count > 0)
        {
            ActivateTab(RequestTabs[Math.Clamp(index - 1, 0, RequestTabs.Count - 1)]);
            if (ActiveRequestTab is not null)
            {
                SyncSelectionFromTab(ActiveRequestTab);
            }
        }
        else if (RequestTabs.Count == 0)
        {
            ActiveRequestTabId = null;
            SelectedRequest = null;
        }

        NotifyChanged();
    }

    public async Task SaveActiveRequestAsync()
    {
        var tab = EnsureActiveRequestTab();
        if (tab.Origin != RequestTabOrigin.Collection)
        {
            await SaveActiveRequestAsNewAsync();
            return;
        }

        var collection = FindCollectionById(tab.CollectionId);
        var request = collection?.Requests.FirstOrDefault(item =>
            string.Equals(GetRequestSourceKey(collection, item), tab.SourceKey, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.Id, tab.RequestId, StringComparison.OrdinalIgnoreCase));
        if (collection is null || request is null)
        {
            await SaveActiveRequestAsNewAsync();
            return;
        }

        ApplyTabToRequest(tab, request);
        await _workspaceStore.SaveRequestAsync(WorkspacePath, collection, request);
        tab.RequestId = request.Id;
        tab.SourceKey = GetRequestSourceKey(collection, request);
        tab.IsDirty = false;
        SelectedCollection = collection;
        SelectedRequest = request;
        AddActivity("Request", $"已保存 {request.Name}");
        NotifyChanged();
    }

    public async Task SaveActiveRequestAsNewAsync()
    {
        var tab = EnsureActiveRequestTab();
        if (Workspace is null)
        {
            WorkspaceStatus = "请先新建或加载 workspace";
            AddActivity("Workspace", WorkspaceStatus);
            NotifyChanged();
            return;
        }

        var collection = GetOrCreateRootCollection();

        await LoadCollectionRequestsAsync(collection);
        var request = new ReadableRequest
        {
            Name = NextRequestName(collection, tab.Title)
        };
        ApplyTabToRequest(tab, request);
        collection.Requests.Add(request);
        await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);
        await _workspaceStore.SaveRequestAsync(WorkspacePath, collection, request);

        tab.Origin = RequestTabOrigin.Collection;
        tab.CollectionId = collection.Id;
        tab.RequestId = request.Id;
        tab.SourceKey = GetRequestSourceKey(collection, request);
        tab.Title = request.Name;
        tab.ActiveSource = "Collection";
        tab.IsDirty = false;
        ActivateTab(tab);
        SelectedCollection = collection;
        SelectedRequest = request;
        AddActivity("Request", $"已另存为 {request.Name}");
        NotifyChanged();
    }

    public void SelectSession(string title)
    {
        DocumentTitle = title;
        ActiveSource = title.Contains("Collection", StringComparison.OrdinalIgnoreCase) ? "Collection" : "Session";
        NotifyChanged();
    }

    public void SelectSession(SessionSelectedEventArgs args) => SelectSession(args.Title);

    public void ShowRaw()
    {
        ViewMode = "raw";
        NotifyChanged();
    }

    public void ShowRequest()
    {
        ViewMode = "request";
        NotifyChanged();
    }

    public void ToggleEndpoint(ReadableTryOperation operation)
    {
        var key = OperationKey(operation);
        ExpandedOperationKey = ExpandedOperationKey == key ? null : key;
        NotifyChanged();
    }

    public void SetRequestTab(string key)
    {
        EnsureActiveRequestTab().RequestSectionTab = key;
        NotifyChanged();
    }

    public void SetResponseTab(string key)
    {
        EnsureActiveRequestTab().ResponseSectionTab = key;
        NotifyChanged();
    }

    public void ToggleHistoryRecord(string id)
    {
        ExpandedHistoryRecordId = string.Equals(ExpandedHistoryRecordId, id, StringComparison.Ordinal)
            ? null
            : id;
        NotifyChanged();
    }

    public void SelectResponseNode(string path)
    {
        var tab = EnsureActiveRequestTab();
        if (string.IsNullOrWhiteSpace(path) || path == "root")
        {
            tab.ResponseNodePath = "root";
        }
        else if (path.StartsWith("/", StringComparison.Ordinal))
        {
            tab.ResponseNodePath = path[1..];
            if (string.IsNullOrWhiteSpace(tab.ResponseNodePath))
            {
                tab.ResponseNodePath = "root";
            }
        }
        else
        {
            tab.ResponseNodePath = tab.ResponseNodePath == "root"
                ? path
                : $"{tab.ResponseNodePath}.{path}";
        }
        NotifyChanged();
    }

    public void SelectResponseNode(ResponseNodeSelectedEventArgs args) => SelectResponseNode(args.Path);

    public async Task CopySelectedResponseAsync()
    {
        var text = SelectedResponseText;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        await Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.SetTextAsync(text);
        AddActivity("Response", "已复制当前响应内容");
        NotifyChanged();
    }

    public async Task SendAiMessage()
    {
        var prompt = AiDraft.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        AiMessages.Add(new AiChatMessage("user", prompt));
        AiDraft = string.Empty;
        NotifyChanged();

        try
        {
            var result = await _aiAgent.SendAsync(new ReadableAiTurnRequest
            {
                Prompt = prompt,
                Context = BuildAiContext(),
                Messages = AiMessages
                    .Select(message => new ReadableHttp.AI.ReadableAiChatMessage
                    {
                        Role = message.Role,
                        Content = message.Content
                    })
                    .ToList()
            });

            AiMessages.Add(new AiChatMessage("assistant", result.AssistantMessage));
            PendingAiActions = result.Actions
                .Where(action => action.Confirmation.RequiresConfirmation)
                .ToList();
            AddActivity("AI", PendingAiActions.Count == 0
                ? "已完成只读分析"
                : $"生成 {PendingAiActions.Count} 个待确认操作");
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or HttpRequestException)
        {
            AiMessages.Add(new AiChatMessage("assistant", $"AI 调用失败：{exception.Message}"));
            AddActivity("AI", "调用失败");
        }

        NotifyChanged();
    }

    public async Task SendAiQuickPrompt(string prompt)
    {
        AiDraft = prompt;
        await SendAiMessage();
    }

    public void ApplyAiAction(string actionId)
    {
        var action = PendingAiActions.FirstOrDefault(item => string.Equals(item.Id, actionId, StringComparison.Ordinal));
        if (action is null)
        {
            return;
        }

        if (action.Kind == ReadableAiActionKind.UpdateCurrentRequest && action.RequestPatch is not null)
        {
            ApplyAiRequestPatch(action.RequestPatch);
            AiMessages.Add(new AiChatMessage("assistant", $"已应用：{action.Title}"));
            AddActivity("AI", $"已应用 {action.Title}");
        }

        PendingAiActions.Remove(action);
        NotifyChanged();
    }

    public void DismissAiAction(string actionId)
    {
        PendingAiActions.RemoveAll(action => string.Equals(action.Id, actionId, StringComparison.Ordinal));
        NotifyChanged();
    }

    private ReadableAiWorkspaceContext BuildAiContext()
    {
        return new ReadableAiWorkspaceContext
        {
            WorkspaceName = WorkspaceName,
            WorkspacePath = WorkspacePath,
            CurrentRequest = ActiveRequestTab is null ? null : BuildRequestFromTab(ActiveRequestTab),
            CurrentExchange = BuildCurrentExchangeSnapshot(),
            RecentExchanges = HistoryRecords.Select(record => record.Exchange).Take(8).ToList(),
            Collections = Collections,
            Specifications = Specifications,
            ViewMode = ViewMode
        };
    }

    private ReadableExchange? BuildCurrentExchangeSnapshot()
    {
        if (ActiveRequestTab is null || string.IsNullOrWhiteSpace(ActiveRequestTab.ResponseText))
        {
            return null;
        }

        return BuildExchangeSnapshot(ActiveRequestTab);
    }

    private void AddHistoryRecord(RequestWorkspaceTab tab, DateTimeOffset startedAt)
    {
        var exchange = BuildExchangeSnapshot(tab);
        var record = new HistoryRecord(
            Guid.NewGuid().ToString("N"),
            startedAt,
            tab.Title,
            tab.Method,
            tab.Url,
            tab.StatusText,
            tab.CollectionId,
            tab.RequestId,
            exchange);

        HistoryRecords.Insert(0, record);
        if (HistoryRecords.Count > 80)
        {
            HistoryRecords.RemoveRange(80, HistoryRecords.Count - 80);
        }
    }

    private static int ParseStatusCode(string statusText)
    {
        var digits = new string(statusText.SkipWhile(character => !char.IsDigit(character))
            .TakeWhile(char.IsDigit)
            .ToArray());
        return int.TryParse(digits, out var statusCode) ? statusCode : 0;
    }

    private ReadableExchange BuildExchangeSnapshot(RequestWorkspaceTab tab)
    {
        return new ReadableExchange
        {
            Request = BuildRequestFromTab(tab),
            Response = new ReadableResponse
            {
                StatusCode = ParseStatusCode(tab.StatusText),
                ReasonPhrase = tab.StatusText,
                BodyText = tab.ResponseText,
                Size = tab.ResponseText.Length
            }
        };
    }

    private void ApplyAiRequestPatch(ReadableAiRequestPatch patch)
    {
        var tab = EnsureActiveRequestTab();
        if (!string.IsNullOrWhiteSpace(patch.Method))
        {
            tab.Method = patch.Method.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(patch.Url))
        {
            tab.Url = patch.Url.Trim();
        }

        if (!string.IsNullOrWhiteSpace(patch.BodyType)
            && Enum.TryParse<ReadableBodyType>(patch.BodyType, ignoreCase: true, out var bodyType))
        {
            tab.BodyType = bodyType;
        }

        if (patch.BodyText is not null)
        {
            tab.BodyText = patch.BodyText;
        }

        if (patch.BodyContentType is not null)
        {
            tab.BodyContentType = patch.BodyContentType;
        }

        ApplyAiParameterChanges(tab.Query, patch.QueryChanges);
        ApplyAiParameterChanges(tab.Headers, patch.HeaderChanges);
        ApplyAiParameterChanges(tab.Form, patch.FormChanges);
        tab.IsDirty = true;
        SyncSelectedRequestFromTab(tab);
    }

    private static void ApplyAiParameterChanges(List<ReadableNameValue> target, IReadOnlyList<ReadableAiParameterChange> changes)
    {
        foreach (var change in changes)
        {
            if (string.IsNullOrWhiteSpace(change.Name))
            {
                continue;
            }

            var existing = target.FirstOrDefault(item => item.Name.Equals(change.Name, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                target.Add(new ReadableNameValue
                {
                    Name = change.Name,
                    Value = change.SuggestedValue,
                    Enabled = true,
                    Description = change.Reason
                });
                continue;
            }

            existing.Value = change.SuggestedValue;
            existing.Enabled = true;
            existing.Description = change.Reason;
        }
    }

    private async Task LoadCollectionRequestsAsync(ReadableCollection collection)
    {
        if (Workspace is null)
        {
            return;
        }

        var requests = await _workspaceStore.LoadCollectionRequestsAsync(WorkspacePath, collection);
        collection.Requests = requests.ToList();
    }

    private async Task LoadAllCollectionRequestsAsync()
    {
        foreach (var collection in EnumerateCollections(Collections))
        {
            await LoadCollectionRequestsAsync(collection);
        }
    }

    private async Task AddDiscoveredCollectionsAsync()
    {
        var knownDirectories = EnumerateCollections(Collections)
            .Select(collection => NormalizePath(GetCollectionDirectory(collection)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in DiscoverCollectionRequestDirectories())
        {
            if (knownDirectories.Contains(NormalizePath(directory))
                || !Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories).Any())
            {
                continue;
            }

            var relativeDirectory = Path.GetRelativePath(WorkspacePath, directory).Replace('\\', '/');
            var collection = new ReadableCollection
            {
                Id = string.Equals(relativeDirectory, RootCollectionDirectory, StringComparison.OrdinalIgnoreCase)
                    ? RootCollectionId
                    : $"{DiscoveredCollectionIdPrefix}{relativeDirectory}",
                Name = string.Equals(relativeDirectory, RootCollectionDirectory, StringComparison.OrdinalIgnoreCase)
                    ? RootCollectionDirectory
                    : Path.GetFileName(directory),
                SourceType = ReadableCollectionSourceType.Local,
                RequestDirectory = relativeDirectory
            };
            collection.Requests = (await _workspaceStore.LoadCollectionRequestsAsync(WorkspacePath, collection)).ToList();
            AddCollectionToTree(collection);
            knownDirectories.Add(NormalizePath(directory));
        }
    }

    private IEnumerable<string> DiscoverCollectionRequestDirectories()
    {
        var collectionsDirectory = Path.Combine(WorkspacePath, "collections");
        if (!Directory.Exists(collectionsDirectory))
        {
            yield break;
        }

        foreach (var collectionDirectory in Directory.EnumerateDirectories(collectionsDirectory, "*", SearchOption.AllDirectories)
            .Prepend(collectionsDirectory))
        {
            if (Directory.EnumerateFiles(collectionDirectory, "*.json", SearchOption.AllDirectories).Any())
            {
                yield return collectionDirectory;
            }
        }
    }

    private void AddCollectionToTree(ReadableCollection collection)
    {
        var parent = FindParentCollection(collection);
        if (parent is null)
        {
            Collections.Add(collection);
        }
        else if (!parent.Children.Any(item => string.Equals(item.Id, collection.Id, StringComparison.OrdinalIgnoreCase)))
        {
            parent.Children.Add(collection);
        }
    }

    private static IReadOnlyList<ReadableCollection> BuildCollectionTree(IEnumerable<ReadableCollection> collections)
    {
        var all = EnumerateCollections(collections)
            .GroupBy(collection => CollectionDirectoryKey(collection), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        foreach (var collection in all)
        {
            collection.Children = [];
        }

        var byDirectory = all
            .Where(collection => !string.IsNullOrWhiteSpace(CollectionDirectoryKey(collection)))
            .ToDictionary(CollectionDirectoryKey, StringComparer.OrdinalIgnoreCase);
        var roots = new List<ReadableCollection>();
        foreach (var collection in all)
        {
            var key = CollectionDirectoryKey(collection);
            var parentKey = ParentDirectoryKey(key);
            if (!string.IsNullOrWhiteSpace(parentKey) && byDirectory.TryGetValue(parentKey, out var parent))
            {
                parent.Children.Add(collection);
            }
            else
            {
                roots.Add(collection);
            }
        }

        return roots;
    }

    private static IEnumerable<ReadableCollection> EnumerateCollections(IEnumerable<ReadableCollection> collections)
    {
        foreach (var collection in collections)
        {
            yield return collection;
            foreach (var child in EnumerateCollections(collection.Children))
            {
                yield return child;
            }
        }
    }

    private ReadableCollection? FindCollectionById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return EnumerateCollections(Collections)
            .FirstOrDefault(collection => string.Equals(collection.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private ReadableEnvironment? FindEnvironment(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return Environments.FirstOrDefault(environment =>
            string.Equals(environment.Id, id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(environment.Name, id, StringComparison.OrdinalIgnoreCase));
    }

    private ReadableCollection? FirstVisibleCollection()
    {
        return EnumerateCollections(Collections)
            .FirstOrDefault(collection => !IsRootCollection(collection));
    }

    private bool RemoveCollectionFromTree(ReadableCollection collection)
    {
        if (Collections.Remove(collection))
        {
            return true;
        }

        foreach (var parent in EnumerateCollections(Collections))
        {
            if (parent.Children.Remove(collection))
            {
                return true;
            }
        }

        return false;
    }

    private ReadableCollection? FindParentCollection(ReadableCollection collection)
    {
        var relativeDirectory = CollectionDirectoryKey(collection);
        if (string.IsNullOrWhiteSpace(relativeDirectory) || string.Equals(relativeDirectory, RootCollectionDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parentDirectory = ParentDirectoryKey(relativeDirectory);
        if (string.IsNullOrWhiteSpace(parentDirectory) || string.Equals(parentDirectory, RootCollectionDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return Collections.FirstOrDefault(item => string.Equals(item.RequestDirectory, RootCollectionDirectory, StringComparison.OrdinalIgnoreCase));
        }

        return EnumerateCollections(Collections)
            .FirstOrDefault(item => string.Equals(CollectionDirectoryKey(item), parentDirectory, StringComparison.OrdinalIgnoreCase));
    }

    private static string CollectionDirectoryKey(ReadableCollection collection)
    {
        return (collection.RequestDirectory ?? string.Empty).Replace('\\', '/').TrimEnd('/');
    }

    private static string ParentDirectoryKey(string relativeDirectory)
    {
        return Path.GetDirectoryName(relativeDirectory)?.Replace('\\', '/').TrimEnd('/') ?? string.Empty;
    }

    private static bool IsRootCollection(ReadableCollection collection)
    {
        return string.Equals(collection.Id, RootCollectionId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(CollectionDirectoryKey(collection), RootCollectionDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private void AddDiscoveredSpecifications()
    {
        if (Workspace is null)
        {
            return;
        }

        foreach (var directoryName in new[] { "specs" })
        {
            var directory = Path.Combine(WorkspacePath, directoryName);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            {
                var format = GuessSpecificationFormat(file);
                if (format == ReadableSpecificationFormat.Unknown || IsGeneratedSpecificationCache(file))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(WorkspacePath, file).Replace('\\', '/');
                if (Workspace.Specifications.Any(specification => SpecificationPathMatches(specification, relativePath)))
                {
                    continue;
                }

                Workspace.Specifications.Add(new ReadableSpecification
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    SourceType = ReadableSpecificationSourceType.LocalFile,
                    Format = format,
                    Path = relativePath
                });
            }
        }
    }

    private void AddDiscoveredEnvironments()
    {
        if (Workspace is null)
        {
            return;
        }

        var environmentDirectory = Path.Combine(WorkspacePath, "environments");
        if (!Directory.Exists(environmentDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(environmentDirectory, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                using var stream = File.OpenRead(file);
                var environment = JsonSerializer.Deserialize<ReadableEnvironment>(stream, ReadableHttpJsonStorage.JsonOptions);
                if (environment is null
                    || Workspace.Environments.Any(item =>
                        string.Equals(item.Id, environment.Id, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(item.Name, environment.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                Workspace.Environments.Add(environment);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
            }
        }
    }

    private static bool IsGeneratedSpecificationCache(string file)
    {
        return file.Contains($"{Path.DirectorySeparatorChar}.readablehttp{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith(".trydoc.json", StringComparison.OrdinalIgnoreCase);
    }

    private RequestWorkspaceTab EnsureActiveRequestTab()
    {
        if (ActiveRequestTab is { } tab)
        {
            return tab;
        }

        return OpenScratchTab();
    }

    private RequestWorkspaceTab OpenScratchTab()
    {
        var tab = new RequestWorkspaceTab
        {
            Title = "Scratch Request",
            Origin = RequestTabOrigin.Scratch,
            ActiveSource = "Request"
        };
        RequestTabs.Add(tab);
        ActivateTab(tab);
        return tab;
    }

    private RequestWorkspaceTab OpenWorkspaceConfigTab()
    {
        var key = string.IsNullOrWhiteSpace(WorkspacePath) ? "workspace" : WorkspacePath;
        var existing = RequestTabs.FirstOrDefault(tab =>
            tab.Origin == RequestTabOrigin.Workspace
            && string.Equals(tab.SourceKey, key, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Title = WorkspaceName;
            ActivateTab(existing);
            return existing;
        }

        var tab = new RequestWorkspaceTab
        {
            Title = WorkspaceName,
            Origin = RequestTabOrigin.Workspace,
            SourceKey = key,
            ActiveSource = "Workspace"
        };
        RequestTabs.Add(tab);
        ActivateTab(tab);
        return tab;
    }

    private RequestWorkspaceTab OpenCollectionConfigTab(ReadableCollection collection)
    {
        var existing = RequestTabs.FirstOrDefault(tab =>
            tab.Origin == RequestTabOrigin.CollectionConfig
            && string.Equals(tab.CollectionId, collection.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SelectedCollection = collection;
            SelectedRequest = null;
            ActivateTab(existing);
            return existing;
        }

        SelectedCollection = collection;
        SelectedRequest = null;
        var tab = new RequestWorkspaceTab
        {
            Title = collection.Name,
            Origin = RequestTabOrigin.CollectionConfig,
            CollectionId = collection.Id,
            ActiveSource = "Collection"
        };
        RequestTabs.Add(tab);
        ActivateTab(tab);
        return tab;
    }

    private RequestWorkspaceTab OpenCollectionRequestTab(ReadableCollection collection, ReadableRequest request)
    {
        var sourceKey = GetRequestSourceKey(collection, request);
        var existing = RequestTabs.FirstOrDefault(tab =>
            tab.Origin == RequestTabOrigin.Collection
            && string.Equals(tab.CollectionId, collection.Id, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(tab.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(tab.RequestId, request.Id, StringComparison.OrdinalIgnoreCase)));
        if (existing is not null)
        {
            SelectedCollection = collection;
            SelectedRequest = request;
            existing.RequestId = request.Id;
            existing.SourceKey = sourceKey;
            ActivateTab(existing);
            return existing;
        }

        SelectedCollection = collection;
        SelectedRequest = request;
        var tab = CreateTabFromRequest(request, RequestTabOrigin.Collection, "Collection");
        tab.CollectionId = collection.Id;
        tab.RequestId = request.Id;
        tab.SourceKey = sourceKey;
        RequestTabs.Add(tab);
        ActivateTab(tab);
        AddSession(request.Name);
        return tab;
    }

    private RequestWorkspaceTab OpenSpecificationRequestTab(ReadableTryOperation operation)
    {
        var key = OperationKey(operation);
        var existing = RequestTabs.FirstOrDefault(tab =>
            tab.Origin == RequestTabOrigin.Specification
            && string.Equals(tab.SourceKey, key, StringComparison.Ordinal));
        if (existing is not null)
        {
            ActivateTab(existing);
            return existing;
        }

        var tab = CreateTabFromRequest(operation.Request, RequestTabOrigin.Specification, "Spec");
        tab.Title = operation.Name;
        tab.SourceKey = key;
        RequestTabs.Add(tab);
        ActivateTab(tab);
        return tab;
    }

    private RequestWorkspaceTab OpenSpecificationDocumentTab(string title, string path)
    {
        var key = string.IsNullOrWhiteSpace(path) ? title : path;
        var existing = RequestTabs.FirstOrDefault(tab =>
            tab.Origin == RequestTabOrigin.SpecificationDocument
            && string.Equals(tab.SourceKey, key, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Title = title;
            ActivateTab(existing);
            return existing;
        }

        var tab = new RequestWorkspaceTab
        {
            Title = title,
            Origin = RequestTabOrigin.SpecificationDocument,
            SourceKey = key,
            ActiveSource = "Spec"
        };
        RequestTabs.Add(tab);
        ActivateTab(tab);
        return tab;
    }

    private RequestWorkspaceTab OpenSpecificationConfigTab(ReadableSpecification specification)
    {
        var existing = RequestTabs.FirstOrDefault(tab =>
            tab.Origin == RequestTabOrigin.SpecificationConfig
            && string.Equals(tab.SourceKey, specification.Id, StringComparison.Ordinal));
        if (existing is not null)
        {
            SelectedSpecification = specification;
            existing.Title = specification.Name;
            ActivateTab(existing);
            return existing;
        }

        SelectedSpecification = specification;
        var tab = new RequestWorkspaceTab
        {
            Title = specification.Name,
            Origin = RequestTabOrigin.SpecificationConfig,
            SourceKey = specification.Id,
            ActiveSource = "Spec"
        };
        RequestTabs.Add(tab);
        ActivateTab(tab);
        return tab;
    }

    private RequestWorkspaceTab OpenSettingsTab()
    {
        var existing = RequestTabs.FirstOrDefault(tab => tab.Origin == RequestTabOrigin.Settings);
        if (existing is not null)
        {
            ActivateTab(existing);
            return existing;
        }

        var tab = new RequestWorkspaceTab
        {
            Title = "Settings",
            Origin = RequestTabOrigin.Settings,
            ActiveSource = "Settings"
        };
        RequestTabs.Add(tab);
        ActivateTab(tab);
        return tab;
    }

    private RequestWorkspaceTab OpenAboutTab(string key, string title)
    {
        var existing = RequestTabs.FirstOrDefault(tab =>
            tab.Origin == RequestTabOrigin.About
            && string.Equals(tab.SourceKey, key, StringComparison.Ordinal));
        if (existing is not null)
        {
            existing.Title = title;
            ActivateTab(existing);
            return existing;
        }

        var tab = new RequestWorkspaceTab
        {
            Title = title,
            Origin = RequestTabOrigin.About,
            SourceKey = key,
            ActiveSource = "About"
        };
        RequestTabs.Add(tab);
        ActivateTab(tab);
        return tab;
    }

    private void ApplySpecificationDocument(ReadableSpecification specification, ReadableTryDocument document, string path)
    {
        SelectedSpecification = specification;
        Operations = document.Operations;
        RawContent = document.RawContent;
        DocumentTitle = document.Title ?? specification.Name;
        ActiveSource = specification.SourceType.ToString();
        ViewMode = "preview";
        TryFilePath = path;
        OpenSpecificationDocumentTab(DocumentTitle, TryFilePath);
    }

    private static RequestWorkspaceTab CreateTabFromRequest(ReadableRequest request, RequestTabOrigin origin, string activeSource)
    {
        return new RequestWorkspaceTab
        {
            Title = request.Name,
            Origin = origin,
            ActiveSource = activeSource,
            Method = request.Method,
            Url = request.Url,
            BodyText = request.Body?.Content ?? string.Empty,
            BodyType = request.Body?.Type ?? ReadableBodyType.None,
            BodyContentType = request.Body?.ContentType ?? DefaultContentType(request.Body?.Type ?? ReadableBodyType.None),
            Query = request.Query.Select(CloneNameValue).ToList(),
            Headers = request.Headers.Select(CloneNameValue).ToList(),
            Form = request.Body?.Type == ReadableBodyType.MultipartFormData
                ? request.Body.Multipart.Select(CloneMultipartAsNameValue).ToList()
                : request.Body?.Form.Select(CloneNameValue).ToList() ?? [],
            Auth = CloneAuth(request.Auth)
        };
    }

    private void ActivateTab(RequestWorkspaceTab tab)
    {
        ActiveRequestTabId = tab.Id;
        DocumentTitle = tab.Title;
        ActiveSource = tab.ActiveSource;
        ViewMode = tab.Origin == RequestTabOrigin.SpecificationDocument ? "preview" : "request";
    }

    private void CloseTabsForRequest(string requestId)
    {
        RequestTabs.RemoveAll(tab => string.Equals(tab.RequestId, requestId, StringComparison.OrdinalIgnoreCase));
        if (ActiveRequestTab is null)
        {
            ActiveRequestTabId = RequestTabs.FirstOrDefault()?.Id;
        }
    }

    private void SyncSelectionFromTab(RequestWorkspaceTab tab)
    {
        if (tab.Origin == RequestTabOrigin.CollectionConfig)
        {
            SelectedCollection = FindCollectionById(tab.CollectionId);
            SelectedRequest = null;
            return;
        }

        if (tab.Origin == RequestTabOrigin.SpecificationConfig)
        {
            SelectedSpecification = Specifications.FirstOrDefault(specification => string.Equals(specification.Id, tab.SourceKey, StringComparison.Ordinal));
            SelectedRequest = null;
            return;
        }

        if (tab.Origin != RequestTabOrigin.Collection)
        {
            SelectedRequest = null;
            return;
        }

        SelectedCollection = FindCollectionById(tab.CollectionId);
        SelectedRequest = SelectedCollection?.Requests.FirstOrDefault(request =>
            string.Equals(GetRequestSourceKey(SelectedCollection, request), tab.SourceKey, StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.Id, tab.RequestId, StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyTabToRequest(RequestWorkspaceTab tab, ReadableRequest request)
    {
        var updated = BuildRequestFromTab(tab);
        request.Name = updated.Name;
        request.Method = updated.Method;
        request.Url = updated.Url;
        request.Query = updated.Query;
        request.Headers = updated.Headers;
        request.Body = updated.Body;
        request.Auth = updated.Auth;
    }

    private static ReadableRequest BuildRequestFromTab(RequestWorkspaceTab tab)
    {
        return new ReadableRequest
        {
            Name = tab.Title,
            Method = tab.Method,
            Url = tab.Url,
            Query = tab.Query.Select(CloneNameValue).ToList(),
            Headers = tab.Headers.Select(CloneNameValue).ToList(),
            Body = BuildBodyFromTab(tab),
            Auth = CloneAuth(tab.Auth)
        };
    }

    private static void ApplyStreamMessage(RequestWorkspaceTab tab, ReadableStreamMessage message)
    {
        switch (message.Type)
        {
            case ReadableStreamMessageType.Headers:
                tab.StatusText = $"HTTP {message.StatusCode} {message.ReasonPhrase}".TrimEnd();
                break;
            case ReadableStreamMessageType.Data:
                AppendStreamText(tab, FormatStreamData(message), ShouldAppendStreamLine(message));
                break;
            case ReadableStreamMessageType.Error:
                tab.StatusText = $"ERROR {message.Error?.Type}";
                AppendStreamText(tab, message.Error?.Message ?? "Stream error", appendLine: true);
                break;
            case ReadableStreamMessageType.Completed:
                if (string.IsNullOrWhiteSpace(tab.ResponseText))
                {
                    tab.ResponseText = "<empty response>";
                }
                break;
        }
    }

    private static void AppendStreamText(RequestWorkspaceTab tab, string? value, bool appendLine)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        tab.ResponseText += value;
        if (appendLine && !value.EndsWith(Environment.NewLine, StringComparison.Ordinal))
        {
            tab.ResponseText += Environment.NewLine;
        }
    }

    private static string? FormatStreamData(ReadableStreamMessage message)
    {
        if (!string.IsNullOrEmpty(message.Data))
        {
            return message.Data;
        }

        return message.Raw;
    }

    private static bool ShouldAppendStreamLine(ReadableStreamMessage message)
    {
        if (string.IsNullOrEmpty(message.Data))
        {
            return false;
        }

        if (string.IsNullOrEmpty(message.Raw))
        {
            return false;
        }

        return !IsJsonStringStreamElement(message);
    }

    private static bool IsJsonStringStreamElement(ReadableStreamMessage message)
    {
        return message.Raw?.TrimStart().StartsWith('"') == true;
    }

    private static bool IsSendFailure(Exception exception)
    {
        return exception is HttpRequestException
            or TaskCanceledException
            or IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or FormatException
            or JsonException
            or NotSupportedException;
    }

    private static string FormatSendFailure(Exception exception)
    {
        return $"""
        Request failed before or during send.

        Type: {exception.GetType().Name}
        Message: {exception.Message}

        Check URL, environment variables, headers, body content type, file paths, and proxy settings.
        """;
    }

    private static ReadableBody? BuildBodyFromTab(RequestWorkspaceTab tab)
    {
        if (tab.BodyType == ReadableBodyType.None)
        {
            return null;
        }

        var body = new ReadableBody
        {
            Type = tab.BodyType,
            ContentType = string.IsNullOrWhiteSpace(tab.BodyContentType)
                ? DefaultContentType(tab.BodyType)
                : tab.BodyContentType,
            Content = tab.BodyText
        };
        if (tab.BodyType == ReadableBodyType.FormUrlEncoded)
        {
            body.Form = tab.Form.Select(CloneNameValue).ToList();
            body.Content = null;
        }
        else if (tab.BodyType == ReadableBodyType.MultipartFormData)
        {
            body.Multipart = tab.Form
                .Where(item => item.Enabled)
                .Select(item => new ReadableMultipartItem
                {
                    Name = item.Name,
                    Value = item.Value,
                    Type = ReadableMultipartItemType.Text
                })
                .ToList();
            body.Content = null;
        }

        return body;
    }

    private void SyncSelectedRequestFromTab(RequestWorkspaceTab tab)
    {
        if (SelectedRequest is not null && tab.Origin == RequestTabOrigin.Collection)
        {
            ApplyTabToRequest(tab, SelectedRequest);
        }
    }

    private static List<ReadableNameValue> GetNameValueList(RequestWorkspaceTab tab, string kind)
    {
        return kind.ToLowerInvariant() switch
        {
            "headers" => tab.Headers,
            "form" => tab.Form,
            _ => tab.Query
        };
    }

    private static ReadableNameValue CloneNameValue(ReadableNameValue item)
    {
        return new ReadableNameValue
        {
            Name = item.Name,
            Value = item.Value,
            Enabled = item.Enabled,
            Description = item.Description
        };
    }

    private static ReadableNameValue CloneMultipartAsNameValue(ReadableMultipartItem item)
    {
        return new ReadableNameValue
        {
            Name = item.Name,
            Value = item.Type == ReadableMultipartItemType.File ? item.FilePath ?? item.FileName ?? string.Empty : item.Value ?? string.Empty,
            Enabled = true,
            Description = item.Type.ToString()
        };
    }

    private static ReadableAuth? CloneAuth(ReadableAuth? auth)
    {
        if (auth is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(auth, ReadableHttpJsonStorage.JsonOptions);
        return JsonSerializer.Deserialize<ReadableAuth>(json, ReadableHttpJsonStorage.JsonOptions);
    }

    private static void EnsureAuthOptions(ReadableAuth auth)
    {
        if (auth.Type == ReadableAuthType.OAuth1)
        {
            auth.OAuth1 ??= new ReadableOAuth1Options();
        }
        else if (auth.Type == ReadableAuthType.OAuth2)
        {
            auth.OAuth2 ??= new ReadableOAuth2Options();
        }
    }

    private static ReadableOAuth1Options EnsureOAuth1(ReadableAuth auth)
    {
        auth.Type = ReadableAuthType.OAuth1;
        auth.OAuth1 ??= new ReadableOAuth1Options();
        return auth.OAuth1;
    }

    private static ReadableOAuth2Options EnsureOAuth2(ReadableAuth auth)
    {
        auth.Type = ReadableAuthType.OAuth2;
        auth.OAuth2 ??= new ReadableOAuth2Options();
        return auth.OAuth2;
    }

    private static string DefaultContentType(ReadableBodyType type)
    {
        return type switch
        {
            ReadableBodyType.Json => "application/json",
            ReadableBodyType.Xml => "application/xml",
            ReadableBodyType.Html => "text/html",
            ReadableBodyType.Javascript => "application/javascript",
            ReadableBodyType.Graphql => "application/json",
            ReadableBodyType.MultipartFormData => "multipart/form-data",
            ReadableBodyType.BinaryFile => "application/octet-stream",
            ReadableBodyType.FormUrlEncoded => "application/x-www-form-urlencoded",
            ReadableBodyType.Raw => "text/plain",
            _ => string.Empty
        };
    }

    private static ReadableRequest CloneRequest(ReadableRequest request)
    {
        var json = JsonSerializer.Serialize(request, ReadableHttpJsonStorage.JsonOptions);
        var clone = JsonSerializer.Deserialize<ReadableRequest>(json, ReadableHttpJsonStorage.JsonOptions) ?? new ReadableRequest();
        clone.Id = Guid.NewGuid().ToString("N");
        return clone;
    }

    private async Task SaveActiveRequestAfterSendAsync(RequestWorkspaceTab tab)
    {
        if (Workspace is null)
        {
            tab.IsDirty = false;
            return;
        }

        if (tab.Origin == RequestTabOrigin.Collection)
        {
            await SaveActiveRequestAsync();
            return;
        }

        await SaveActiveRequestAsNewAsync();
    }

    private ReadableCollection GetOrCreateRootCollection()
    {
        var collection = Collections.FirstOrDefault(item =>
            string.Equals(item.Id, RootCollectionId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.RequestDirectory, RootCollectionDirectory, StringComparison.OrdinalIgnoreCase));
        if (collection is not null)
        {
            return collection;
        }

        collection = new ReadableCollection
        {
            Id = RootCollectionId,
            Name = RootCollectionDirectory,
            SourceType = ReadableCollectionSourceType.Local,
            RequestDirectory = RootCollectionDirectory
        };
        Collections.Insert(0, collection);
        if (Workspace is not null)
        {
            Workspace.Collections = Collections;
        }

        return collection;
    }

    private static bool ShouldRenameCollectionDirectory(ReadableCollection collection)
    {
        if (string.IsNullOrWhiteSpace(collection.RequestDirectory)
            || string.Equals(collection.RequestDirectory, RootCollectionDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalized = collection.RequestDirectory.Replace('\\', '/').TrimEnd('/');
        return normalized.StartsWith("collections/", StringComparison.OrdinalIgnoreCase)
            && !normalized.Contains("/requests/", StringComparison.OrdinalIgnoreCase)
            && !normalized.EndsWith("/requests", StringComparison.OrdinalIgnoreCase);
    }

    private string NextCollectionName(string baseName)
    {
        var name = baseName;
        var index = 2;
        while (EnumerateCollections(Collections).Any(collection => string.Equals(collection.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            name = $"{baseName} {index}";
            index++;
        }

        return name;
    }

    private string NextSpecificationName(string baseName)
    {
        var name = baseName;
        var index = 2;
        while (Specifications.Any(specification => string.Equals(specification.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            name = $"{baseName} {index}";
            index++;
        }

        return name;
    }

    private string GetCollectionDirectory(ReadableCollection collection)
    {
        return string.IsNullOrWhiteSpace(collection.RequestDirectory)
            ? Path.Combine(WorkspacePath, "collections", ToFileName(collection.Name))
            : Path.Combine(WorkspacePath, collection.RequestDirectory);
    }

    private string GetRequestSourceKey(ReadableCollection collection, ReadableRequest request)
    {
        return GetRequestPath(collection, request);
    }

    private string GetRequestPath(ReadableCollection collection, ReadableRequest request)
    {
        return string.IsNullOrWhiteSpace(request.SourcePath)
            ? Path.Combine(GetCollectionDirectory(collection), $"{ToFileName(request.Name)}.json")
            : Path.Combine(WorkspacePath, request.SourcePath);
    }

    private static void ExplorePathInShell(string path)
    {
        var target = Directory.Exists(path) ? path : File.Exists(path) ? path : Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        var arguments = File.Exists(path)
            ? $"/select,\"{Path.GetFullPath(path)}\""
            : $"\"{Path.GetFullPath(target)}\"";

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = arguments,
            UseShellExecute = true
        });
    }

    private static void OpenPathDefault(string path)
    {
        var target = File.Exists(path) || Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        });
    }

    private static string ToFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "item" : sanitized;
    }

    private static string NextRequestName(ReadableCollection collection, string title)
    {
        var baseName = string.IsNullOrWhiteSpace(title) ? "Request" : title;
        var name = baseName;
        var index = 2;
        while (collection.Requests.Any(request => string.Equals(request.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            name = $"{baseName} {index}";
            index++;
        }

        return name;
    }

    private ReadableExecutionContext CreateExecutionContext()
    {
        var context = new ReadableExecutionContext
        {
            Proxy = ProxyMode switch
            {
                ProxyNone => new ReadableProxyOptions { Mode = ReadableProxyMode.None },
                ProxyCustom => new ReadableProxyOptions
                {
                    Mode = ReadableProxyMode.Custom,
                    Url = CustomProxyUrl,
                    Username = string.IsNullOrWhiteSpace(CustomProxyUsername) ? null : CustomProxyUsername,
                    Password = string.IsNullOrWhiteSpace(CustomProxyPassword) ? null : CustomProxyPassword
                },
                _ => new ReadableProxyOptions { Mode = ReadableProxyMode.System }
            }
        };
        MergeVariables(context.Variables, Workspace?.Variables);
        MergeVariables(context.Variables, SelectedEnvironment?.Variables);
        MergeVariables(context.Variables, SelectedCollection?.Variables);
        if (ActiveRequestTab is { } tab)
        {
            var request = tab.Origin == RequestTabOrigin.Collection
                ? SelectedRequest
                : null;
            MergeVariables(context.Variables, request?.Variables);
        }

        return context;
    }

    private static void MergeVariables(
        Dictionary<string, ReadableVariable> target,
        IReadOnlyDictionary<string, ReadableVariable>? source)
    {
        if (source is null)
        {
            return;
        }

        foreach (var (key, value) in source)
        {
            target[key] = value;
        }
    }

    private static bool ValidateWorkspacePath(string path, out string message)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            message = "workspace 路径为空";
            return false;
        }

        var workspaceFile = Path.Combine(path, "workspace.json");
        if (!File.Exists(workspaceFile))
        {
            message = $"无效 workspace：未找到 {workspaceFile}";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static void EnsureWorkspaceLayout(string workspacePath)
    {
        Directory.CreateDirectory(workspacePath);
        Directory.CreateDirectory(Path.Combine(workspacePath, "collections"));
        Directory.CreateDirectory(Path.Combine(workspacePath, "environments"));
        Directory.CreateDirectory(Path.Combine(workspacePath, "specs"));
    }

    private static bool IsGitRepository(string workspacePath)
    {
        return Directory.Exists(Path.Combine(workspacePath, ".git"));
    }

    private static bool ApplyGitWorkspaceType(string workspacePath, ReadableWorkspace workspace)
    {
        if (!IsGitRepository(workspacePath) || workspace.Type == ReadableWorkspaceType.Git)
        {
            return false;
        }

        workspace.Type = ReadableWorkspaceType.Git;
        workspace.Git ??= new ReadableGitOptions();
        return true;
    }

    private async Task<string> CopySpecificationIntoWorkspaceAsync(string sourcePath)
    {
        EnsureWorkspaceLayout(WorkspacePath);
        var sourceFullPath = Path.GetFullPath(sourcePath);
        var workspaceFullPath = Path.GetFullPath(WorkspacePath);
        if (sourceFullPath.StartsWith(workspaceFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetRelativePath(WorkspacePath, sourceFullPath).Replace('\\', '/');
        }

        var targetDirectory = Path.Combine(WorkspacePath, "specs");
        Directory.CreateDirectory(targetDirectory);
        var targetPath = NextAvailablePath(targetDirectory, Path.GetFileName(sourcePath));
        await using (var source = File.OpenRead(sourceFullPath))
        await using (var target = File.Create(targetPath))
        {
            await source.CopyToAsync(target);
        }

        return Path.GetRelativePath(WorkspacePath, targetPath).Replace('\\', '/');
    }

    private static string NextAvailablePath(string directory, string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var candidate = Path.Combine(directory, fileName);
        var index = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{name}-{index}{extension}");
            index++;
        }

        return candidate;
    }

    private void ResetSettingsDraft()
    {
        SettingsDraft = new AppSettingsDraft(
            ThemeMode,
            ProxyMode,
            CustomProxyUrl,
            CustomProxyUsername,
            CustomProxyPassword,
            Language,
            FontSize,
            DevToolsEnabled);
    }

    private void UpdateSettingsDraft(AppSettingsDraft draft)
    {
        SettingsDraft = draft;
        NotifyChanged();
    }

    private void PersistSettings()
    {
        _settingsStore.Save(new AppSettings
        {
            WorkspacePath = WorkspacePath,
            TryFilePath = TryFilePath,
            WorkspaceHistory = string.Join('|', WorkspaceOptions),
            Language = Language,
            FontSize = FontSize,
            ThemeMode = ThemeMode,
            DarkMode = DarkMode,
            ProxyMode = ProxyMode,
            CustomProxyUrl = CustomProxyUrl,
            CustomProxyUsername = CustomProxyUsername,
            CustomProxyPassword = CustomProxyPassword,
            UseSystemProxy = UseSystemProxy,
            DevToolsEnabled = DevToolsEnabled
        });
    }

    private static string NormalizeThemeMode(string value)
    {
        return value switch
        {
            ThemeLight => ThemeLight,
            ThemeDark => ThemeDark,
            _ => ThemeSystem
        };
    }

    private void ApplyApplicationTheme()
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.UserAppTheme = ThemeMode switch
        {
            ThemeLight => AppTheme.Light,
            ThemeDark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }

    private static string NormalizeProxyMode(string value)
    {
        return value switch
        {
            ProxyNone => ProxyNone,
            ProxyCustom => ProxyCustom,
            _ => ProxySystem
        };
    }

    private static string NormalizeFontSize(string value)
    {
        return value switch
        {
            FontSmall => FontSmall,
            FontLarge => FontLarge,
            _ => FontMedium
        };
    }

    private void AddSpecFile(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && !SpecFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            SpecFiles.Insert(0, path);
        }
    }

    private Dictionary<string, ReadableVariable>? GetVariableDictionary(string scope)
    {
        if (scope.StartsWith("environment:", StringComparison.OrdinalIgnoreCase))
        {
            return FindEnvironment(scope["environment:".Length..])?.Variables;
        }

        return scope.Equals("collection", StringComparison.OrdinalIgnoreCase)
            ? SelectedCollection?.Variables
            : Workspace?.Variables;
    }

    private static KeyValuePair<string, ReadableVariable>? GetVariableEntry(
        Dictionary<string, ReadableVariable>? variables,
        int index)
    {
        if (variables is null || index < 0 || index >= variables.Count)
        {
            return null;
        }

        return variables.ElementAt(index);
    }

    private static string NextVariableName(Dictionary<string, ReadableVariable> variables, string baseName)
    {
        var name = baseName;
        var index = 2;
        while (variables.ContainsKey(name))
        {
            name = $"{baseName}{index}";
            index++;
        }

        return name;
    }

    private string NextEnvironmentName(string baseName)
    {
        var name = baseName;
        var index = 2;
        while (Environments.Any(environment => string.Equals(environment.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            name = $"{baseName}{index}";
            index++;
        }

        return name;
    }

    private void DeleteSpecificationFiles(ReadableSpecification specification)
    {
        DeleteWorkspaceFile(specification.Path);
        if (!string.Equals(specification.Path, specification.NormalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            DeleteWorkspaceFile(specification.NormalizedPath);
        }
    }

    private void DeleteWorkspaceFile(string? relativeOrAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
        {
            return;
        }

        var path = ResolveWorkspacePath(relativeOrAbsolutePath);
        var workspaceRoot = Path.GetFullPath(WorkspacePath);
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            return;
        }

        File.Delete(fullPath);
    }

    private bool SpecificationPathMatches(ReadableSpecification specification, string path)
    {
        if (string.IsNullOrWhiteSpace(specification.Path))
        {
            return false;
        }

        var specificationPath = specification.Path;
        var resolvedSpecificationPath = ResolveWorkspacePath(specificationPath);
        var resolvedCandidatePath = ResolveWorkspacePath(path);
        return string.Equals(specificationPath, path, StringComparison.OrdinalIgnoreCase)
            || string.Equals(resolvedSpecificationPath, path, StringComparison.OrdinalIgnoreCase)
            || string.Equals(specificationPath, resolvedCandidatePath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(resolvedSpecificationPath, resolvedCandidatePath, StringComparison.OrdinalIgnoreCase);
    }

    private void AddLocalSpecification(string path)
    {
        if (Workspace is null)
        {
            return;
        }

        var storedPath = Path.IsPathRooted(path)
            ? Path.GetRelativePath(WorkspacePath, path)
            : path;
        if (storedPath.StartsWith("..", StringComparison.Ordinal))
        {
            storedPath = path;
        }

        if (Workspace.Specifications.Any(specification =>
            string.Equals(specification.Path, storedPath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(specification.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Workspace.Specifications.Add(new ReadableSpecification
        {
            Name = Path.GetFileNameWithoutExtension(path),
            SourceType = ReadableSpecificationSourceType.LocalFile,
            Format = GuessSpecificationFormat(path),
            Path = storedPath
        });
    }

    private string ResolveWorkspacePath(string path)
    {
        return Path.IsPathRooted(path) ? path : Path.Combine(WorkspacePath, path);
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static ReadableSpecificationFormat GuessSpecificationFormat(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.ToLowerInvariant() switch
        {
            ".http" => ReadableSpecificationFormat.Http,
            ".curl" => ReadableSpecificationFormat.Curl,
            ".har" => ReadableSpecificationFormat.Har,
            ".json" or ".yaml" or ".yml" => ReadableSpecificationFormat.OpenApi,
            _ => ReadableSpecificationFormat.Unknown
        };
    }

    private static string GuessClipboardImportExtension(string text)
    {
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith("curl ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("curl\t", StringComparison.OrdinalIgnoreCase))
        {
            return ".curl";
        }

        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return ".json";
        }

        var firstLine = trimmed.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var firstToken = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        if (IsHttpMethod(firstToken))
        {
            return ".http";
        }

        if (trimmed.StartsWith("openapi:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("swagger:", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("\nopenapi:", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("\nswagger:", StringComparison.OrdinalIgnoreCase))
        {
            return ".yaml";
        }

        return ".http";
    }

    private static bool IsHttpMethod(string value)
    {
        return value.Equals("GET", StringComparison.OrdinalIgnoreCase)
            || value.Equals("POST", StringComparison.OrdinalIgnoreCase)
            || value.Equals("PUT", StringComparison.OrdinalIgnoreCase)
            || value.Equals("PATCH", StringComparison.OrdinalIgnoreCase)
            || value.Equals("DELETE", StringComparison.OrdinalIgnoreCase)
            || value.Equals("HEAD", StringComparison.OrdinalIgnoreCase)
            || value.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase);
    }

    private void AddWorkspaceOption(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && !WorkspaceOptions.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            WorkspaceOptions.Insert(0, path);
        }
    }

    private static bool IsWorkspaceUsable(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(Path.Combine(path, "workspace.json"));
    }

    private static string WorkspaceDisplayName(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? "workspace" : name;
    }

    private void AddSession(string title)
    {
        RecentSessions.RemoveAll(item => string.Equals(item, title, StringComparison.OrdinalIgnoreCase));
        RecentSessions.Insert(0, title);
        if (RecentSessions.Count > 8)
        {
            RecentSessions.RemoveAt(RecentSessions.Count - 1);
        }
    }

    private void AddActivity(string title, string detail)
    {
        ActivityLog.Insert(0, new ActivityEntry(title, detail));
        if (ActivityLog.Count > 8)
        {
            ActivityLog.RemoveAt(ActivityLog.Count - 1);
        }
    }

    private List<string> GetResponseNodeKeys()
    {
        if (string.IsNullOrWhiteSpace(ResponseText))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(ResponseText);
            return GetJsonNodeKeys(ResolveResponseElement(document.RootElement, ResponseNodePath));
        }
        catch (JsonException)
        {
            if (TryGetStreamedJsonNodeKeys(ResponseText, ResponseNodePath, out var streamedKeys))
            {
                return streamedKeys;
            }

            EnsureActiveRequestTab().ResponseNodePath = "root";
        }
        catch (InvalidOperationException)
        {
            EnsureActiveRequestTab().ResponseNodePath = "root";
        }

        return [];
    }

    private string GetSelectedResponseText()
    {
        if (string.IsNullOrWhiteSpace(ResponseText) || ResponseNodePath == "root")
        {
            return TryFormatJson(ResponseText);
        }

        try
        {
            using var document = JsonDocument.Parse(ResponseText);
            return FormatResponseElement(ResolveResponseElement(document.RootElement, ResponseNodePath));
        }
        catch (JsonException)
        {
            var streamedText = GetStreamedJsonSelectedText(ResponseText, ResponseNodePath);
            if (streamedText is not null)
            {
                return streamedText;
            }

            EnsureActiveRequestTab().ResponseNodePath = "root";
        }
        catch (InvalidOperationException)
        {
            EnsureActiveRequestTab().ResponseNodePath = "root";
        }

        return TryFormatJson(ResponseText);
    }

    private static List<string> GetJsonNodeKeys(JsonElement selected)
    {
        if (selected.ValueKind == JsonValueKind.Object)
        {
            return selected.EnumerateObject()
                .Select(property => property.Name)
                .Take(24)
                .ToList();
        }

        if (selected.ValueKind == JsonValueKind.Array)
        {
            return selected.EnumerateArray()
                .Select((_, index) => $"[{index}]")
                .Take(24)
                .ToList();
        }

        return [];
    }

    private static bool TryGetStreamedJsonNodeKeys(string responseText, string responseNodePath, out List<string> keys)
    {
        keys = [];
        var values = ParseStreamedJsonValues(responseText);
        if (values.Count == 0)
        {
            return false;
        }

        using var document = JsonDocument.Parse("[" + string.Join(",", values) + "]");
        keys = GetJsonNodeKeys(ResolveResponseElement(document.RootElement, responseNodePath));
        return true;
    }

    private static string? GetStreamedJsonSelectedText(string responseText, string responseNodePath)
    {
        var values = ParseStreamedJsonValues(responseText);
        if (values.Count == 0)
        {
            return null;
        }

        using var document = JsonDocument.Parse("[" + string.Join(",", values) + "]");
        return FormatResponseElement(ResolveResponseElement(document.RootElement, responseNodePath));
    }

    private static List<string> ParseStreamedJsonValues(string responseText)
    {
        var values = new List<string>();
        foreach (var line in responseText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                ? line["data:".Length..].Trim()
                : line;
            if (string.IsNullOrWhiteSpace(candidate) || candidate == "[DONE]")
            {
                continue;
            }

            try
            {
                using var _ = JsonDocument.Parse(candidate);
                values.Add(candidate);
            }
            catch (JsonException)
            {
                return [];
            }
        }

        return values;
    }

    private static JsonElement ResolveResponseElement(JsonElement root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "root")
        {
            return root;
        }

        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out var property))
            {
                current = property;
                continue;
            }

            if (current.ValueKind == JsonValueKind.Array
                && segment.StartsWith("[", StringComparison.Ordinal)
                && segment.EndsWith("]", StringComparison.Ordinal)
                && int.TryParse(segment[1..^1], out var index))
            {
                var items = current.EnumerateArray().ToList();
                if (index >= 0 && index < items.Count)
                {
                    current = items[index];
                    continue;
                }
            }

            return root;
        }

        return current;
    }

    private static string FirstLine(string value)
    {
        return value.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? value;
    }

    private static string OperationKey(ReadableTryOperation operation) => $"{operation.Method}:{operation.Path}:{operation.Name}";

    private static string TryFormatJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return FormatJsonElement(document.RootElement);
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static string FormatJsonElement(JsonElement element)
    {
        return JsonSerializer.Serialize(element, JsonDisplayOptions);
    }

    private static string FormatResponseElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => element.GetRawText(),
            _ => FormatJsonElement(element)
        };
    }

    private static List<PipelinePhase> CreatePipelinePhases(IReadOnlyCollection<ReadableExchangeTiming> timings)
    {
        if (timings.Count == 0)
        {
            return [];
        }

        var colors = new[] { "#22d3ee", "#3b82f6", "#8b5cf6", "#10b981", "#f59e0b", "#ef4444" };
        var totalMs = Math.Max(1, timings.Sum(timing => Math.Max(1, (int)Math.Round(timing.Duration.TotalMilliseconds))));
        var index = 0;

        return timings.Select(timing =>
        {
            var elapsedMs = Math.Max(1, (int)Math.Round(timing.Duration.TotalMilliseconds));
            var width = Math.Max(5, (int)Math.Round(elapsedMs * 100d / totalMs));
            var phase = new PipelinePhase(
                timing.Name,
                width,
                colors[index % colors.Length],
                $"{elapsedMs}ms",
                elapsedMs);
            index++;
            return phase;
        }).ToList();
    }

    private static List<PipelinePhase> CreateStreamingPipelinePhases(DateTimeOffset startedAt, string currentStage)
    {
        var elapsedMs = Math.Max(1, (int)Math.Round((DateTimeOffset.UtcNow - startedAt).TotalMilliseconds));
        var phases = new List<PipelinePhase>
        {
            new("Build request", 14, "#22d3ee", "ready", 1)
        };

        var streamLabel = currentStage switch
        {
            nameof(ReadableStreamMessageType.Headers) => "Headers",
            nameof(ReadableStreamMessageType.Data) => "Streaming",
            nameof(ReadableStreamMessageType.Error) => "Error",
            nameof(ReadableStreamMessageType.Completed) or "Completed" => "Complete",
            _ => "Sending"
        };

        phases.Add(new(streamLabel, 72, streamLabel == "Error" ? "#ef4444" : "#3b82f6", $"{elapsedMs}ms", elapsedMs));
        phases.Add(new(streamLabel == "Complete" ? "Rendered" : "UI update", 14, "#10b981", streamLabel == "Complete" ? "done" : "live", 1));
        return phases;
    }

    private void NotifyChanged()
    {
        Changed?.Invoke();
    }

    private async Task NotifyChangedAsync()
    {
        var handlers = ChangedAsync;
        if (handlers is null)
        {
            NotifyChanged();
            return;
        }

        foreach (Func<Task> handler in handlers.GetInvocationList().Cast<Func<Task>>())
        {
            await handler();
        }
    }
}
