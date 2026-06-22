using ReadableHttp.Core;
using ReadableHttp.Execution;
using ReadableHttp.Storage;
using ReadableHttp.Try;
using System.Diagnostics;
using System.Text.Json;

namespace ReadableHttp.App.Maui.Components.ApiClient;

public sealed class ApiClientWorkspaceState
{
    public const string ThemeSystem = "system";
    public const string ThemeLight = "light";
    public const string ThemeDark = "dark";
    public const string ProxyNone = "none";
    public const string ProxySystem = "system";
    public const string ProxyCustom = "custom";

    private readonly AppSettingsStore _settingsStore;
    private readonly AppFilePicker _filePicker;
    private readonly ReadableWorkspaceStore _workspaceStore = new();
    private bool _initialized;

    public ApiClientWorkspaceState(AppSettingsStore settingsStore, AppFilePicker filePicker)
    {
        _settingsStore = settingsStore;
        _filePicker = filePicker;
    }

    public event Action? Changed;

    public string WorkspacePath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ReadableHttp Workspace");
    public string WorkspaceStatus { get; private set; } = "No workspace loaded";
    public string TryFilePath { get; private set; } = "samples/openapi/httpbin.openapi.json";
    public string ActiveSource { get; private set; } = "Request";
    public string DocumentTitle { get; private set; } = "Scratch Request";
    public string RawContent { get; private set; } = string.Empty;
    public string ViewMode { get; private set; } = "request";
    public string RequestTab => ActiveRequestTab?.RequestSectionTab ?? "Params";
    public string Method { get => ActiveRequestTab?.Method ?? "GET"; set => SetMethod(value); }
    public string Url { get => ActiveRequestTab?.Url ?? string.Empty; set => SetUrl(value); }
    public string BodyText { get => ActiveRequestTab?.BodyText ?? string.Empty; set => SetBodyText(value); }
    public string StatusText => ActiveRequestTab?.StatusText ?? "Ready";
    public string ResponseText => ActiveRequestTab?.ResponseText ?? string.Empty;
    public string ResponseNodePath => ActiveRequestTab?.ResponseNodePath ?? "root";
    public string Language { get; set; } = "system";
    public string ThemeMode { get; private set; } = ThemeSystem;
    public bool DarkMode { get; set; }
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
    public List<ReadableCollection> Collections { get; private set; } = [];
    public IReadOnlyList<ReadableSpecification> Specifications => Workspace?.Specifications ?? [];
    public List<ReadableTryOperation> Operations { get; private set; } = [];
    public List<string> SpecFiles { get; private set; } = [];
    public List<string> RecentSessions { get; private set; } = ["Scratch Request"];
    public List<string> WorkspaceOptions { get; private set; } = [];
    public List<ActivityEntry> ActivityLog { get; private set; } = [new("Ready", "等待加载 workspace 或导入文件")];
    public List<AiChatMessage> AiMessages { get; private set; } = [new("assistant", "我可以根据当前 request、response 或 spec 帮你整理参数、生成测试用例和解释错误。")];
    public List<PipelinePhase> PipelinePhases => ActiveRequestTab?.PipelinePhases ?? [];
    public List<RequestWorkspaceTab> RequestTabs { get; private set; } = [];
    public string? ActiveRequestTabId { get; private set; }
    public AppSettingsDraft SettingsDraft { get; private set; } = new();

    public bool IsGitWorkspace => Workspace?.Type == ReadableWorkspaceType.Git;

    public string WorkspaceName => Workspace?.Name ?? "scratch";

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

    public void Initialize()
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
        ThemeMode = NormalizeThemeMode(string.IsNullOrWhiteSpace(settings.ThemeMode)
            ? settings.DarkMode ? ThemeDark : ThemeSystem
            : settings.ThemeMode);
        DarkMode = ThemeMode == ThemeDark;
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
        AddWorkspaceOption(WorkspacePath);
        AddSpecFile(TryFilePath);
        EnsureScratchTab();
        _initialized = true;
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
            SelectedRequest.Body = string.IsNullOrWhiteSpace(value)
                ? null
                : new ReadableBody
                {
                    Type = ReadableBodyType.Json,
                    ContentType = "application/json",
                    Content = value
                };
        }
        NotifyChanged();
    }

    public void SetDraftThemeMode(string value) => UpdateSettingsDraft(SettingsDraft with { ThemeMode = NormalizeThemeMode(value) });

    public void SetDraftProxyMode(string value) => UpdateSettingsDraft(SettingsDraft with { ProxyMode = NormalizeProxyMode(value) });

    public void SetDraftCustomProxyUrl(string value) => UpdateSettingsDraft(SettingsDraft with { CustomProxyUrl = value });

    public void SetDraftCustomProxyUsername(string value) => UpdateSettingsDraft(SettingsDraft with { CustomProxyUsername = value });

    public void SetDraftCustomProxyPassword(string value) => UpdateSettingsDraft(SettingsDraft with { CustomProxyPassword = value });

    public void SetDraftDevToolsEnabled(bool value) => UpdateSettingsDraft(SettingsDraft with { DevToolsEnabled = value });

    public void SetDraftLanguage(string value) => UpdateSettingsDraft(SettingsDraft with { Language = string.IsNullOrWhiteSpace(value) ? "system" : value });

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
            Workspace = new ReadableWorkspace
            {
                Name = Path.GetFileName(WorkspacePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            };
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
            Collections = Workspace.Collections;
            SelectedCollection = Collections.FirstOrDefault();
            if (SelectedCollection is not null)
            {
                await LoadCollectionRequestsAsync(SelectedCollection);
            }
            SelectedRequest = SelectedCollection?.Requests.FirstOrDefault();
            if (SelectedCollection is not null && SelectedRequest is not null)
            {
                OpenCollectionRequestTab(SelectedCollection, SelectedRequest);
            }
            WorkspaceStatus = $"Loaded: {Workspace.Name}";
            AddActivity("Workspace", WorkspaceStatus);
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
        var document = await new ReadableTryDocumentLoader().LoadAsync(TryFilePath);
        ActiveSource = document.SourceType.ToString();
        DocumentTitle = document.Title ?? document.FileName ?? "Try Document";
        RawContent = document.RawContent;
        Operations = document.Operations;
        ViewMode = "preview";
        AddSpecFile(TryFilePath);
        AddActivity("Import", $"已转换 {Operations.Count} 个 operation");
        SaveSettings();
        NotifyChanged();
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
        var path = await _filePicker.PickTryFileAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            TryFilePath = path;
            AddSpecFile(path);
            AddLocalSpecification(path);
            ActiveSource = "API Spec";
            DocumentTitle = Path.GetFileName(path);
            ViewMode = "preview";
            SaveSettings();
            if (Workspace is not null)
            {
                await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);
            }
            NotifyChanged();
        }
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

    public void ShowRightPanel(RightPanelMode mode)
    {
        if (mode == RightPanelMode.Settings)
        {
            OpenSettingsTab();
            NotifyChanged();
            return;
        }

        RightPanelMode = mode;
        ShowInspector = true;
        NotifyChanged();
    }

    public async Task SelectCollectionAsync(ReadableCollection collection)
    {
        SelectedCollection = collection;
        await LoadCollectionRequestsAsync(collection);
        SelectedRequest = collection.Requests.FirstOrDefault();
        DocumentTitle = collection.Name;
        ActiveSource = collection.SourceType.ToString();
        AddSession(collection.Name);
        if (SelectedRequest is not null)
        {
            OpenCollectionRequestTab(collection, SelectedRequest);
        }
        NotifyChanged();
    }

    public async Task SelectCollectionAsync(CollectionEventArgs args) => await SelectCollectionAsync(args.Collection);

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
        ProxyMode = draftProxyMode;
        CustomProxyUrl = SettingsDraft.CustomProxyUrl.Trim();
        CustomProxyUsername = SettingsDraft.CustomProxyUsername.Trim();
        CustomProxyPassword = SettingsDraft.CustomProxyPassword;
        Language = string.IsNullOrWhiteSpace(SettingsDraft.Language) ? "system" : SettingsDraft.Language;
        DevToolsEnabled = SettingsDraft.DevToolsEnabled;
        _settingsStore.Save(new AppSettings
        {
            WorkspacePath = WorkspacePath,
            TryFilePath = TryFilePath,
            WorkspaceHistory = string.Join('|', WorkspaceOptions),
            Language = Language,
            ThemeMode = ThemeMode,
            DarkMode = DarkMode,
            ProxyMode = ProxyMode,
            CustomProxyUrl = CustomProxyUrl,
            CustomProxyUsername = CustomProxyUsername,
            CustomProxyPassword = CustomProxyPassword,
            UseSystemProxy = UseSystemProxy,
            DevToolsEnabled = DevToolsEnabled
        });
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

    public async Task SendAsync()
    {
        IsSending = true;
        var tab = EnsureActiveRequestTab();
        tab.StatusText = "Sending...";
        tab.ResponseText = string.Empty;
        tab.ResponseNodePath = "root";
        tab.PipelinePhases = [];
        NotifyChanged();

        try
        {
            var request = new ReadableRequest
            {
                Method = tab.Method,
                Url = tab.Url,
                Body = string.IsNullOrWhiteSpace(tab.BodyText)
                    ? null
                    : new ReadableBody
                    {
                        Type = ReadableBodyType.Json,
                        ContentType = "application/json",
                        Content = tab.BodyText
                    }
            };

            var exchange = await new ReadableHttpExecutor().SendExchangeAsync(request, CreateExecutionContext());
            tab.StatusText = exchange.Error is not null
                ? $"ERROR {exchange.Error.Type}"
                : $"HTTP {exchange.Response?.StatusCode} {exchange.Response?.ReasonPhrase}";
            tab.ResponseText = exchange.Error?.Message
                ?? exchange.Response?.BodyText
                ?? "<binary or empty response>";
            tab.PipelinePhases = CreatePipelinePhases(exchange.Timings);
            AddActivity(tab.Method, $"{tab.StatusText} {tab.Url}");
        }
        finally
        {
            IsSending = false;
            NotifyChanged();
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
        if (Workspace is null)
        {
            WorkspaceStatus = "请先新建或加载 workspace";
            AddActivity("Workspace", WorkspaceStatus);
            NotifyChanged();
            return;
        }

        var collection = new ReadableCollection
        {
            Name = $"New Collection {Collections.Count + 1}",
            SourceType = ReadableCollectionSourceType.Local,
            RequestDirectory = $"requests/collection-{Collections.Count + 1}"
        };
        Collections.Add(collection);
        Workspace.Collections = Collections;
        SelectedCollection = collection;
        SelectedRequest = null;
        DocumentTitle = collection.Name;
        ActiveSource = collection.SourceType.ToString();
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
        if (string.IsNullOrWhiteSpace(specification.Path))
        {
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
            Operations = document.Operations;
            RawContent = document.RawContent;
            DocumentTitle = document.Title ?? specification.Name;
            ActiveSource = specification.SourceType.ToString();
            ViewMode = "preview";
            await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);
            AddActivity("Spec", specification.Remote?.UpdateAvailable == true
                ? $"已刷新 {specification.Name}，发现变更"
                : $"已刷新 {specification.Name}");
            NotifyChanged();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or HttpRequestException)
        {
            AddActivity("Spec", $"刷新失败：{exception.Message}");
            NotifyChanged();
        }
    }

    public async Task RefreshSpecificationAsync(SpecificationEventArgs args) => await RefreshSpecificationAsync(args.Specification);

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

    public void OpenWorkspaceInExplorer(string path)
    {
        OpenPathInShell(path);
    }

    public void OpenWorkspaceInExplorer(WorkspaceSelectedEventArgs args) => OpenWorkspaceInExplorer(args.Path);

    public void RemoveWorkspaceOption(string path)
    {
        WorkspaceOptions.RemoveAll(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase));
        SaveSettings();
        NotifyChanged();
    }

    public void RemoveWorkspaceOption(WorkspaceSelectedEventArgs args) => RemoveWorkspaceOption(args.Path);

    public async Task SaveWorkspaceAsync()
    {
        if (Workspace is null)
        {
            WorkspaceStatus = "没有可保存的 workspace";
            AddActivity("Workspace", WorkspaceStatus);
            NotifyChanged();
            return;
        }

        Workspace.Collections = Collections;
        await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);
        foreach (var collection in Collections)
        {
            await _workspaceStore.SaveCollectionRequestsAsync(WorkspacePath, collection, collection.Requests, replaceExisting: true);
        }

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
        Collections.Remove(collection);
        Workspace.Collections = Collections;
        await _workspaceStore.DeleteCollectionRequestsAsync(WorkspacePath, collection);
        await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);
        SelectedCollection = Collections.FirstOrDefault();
        if (SelectedCollection is not null)
        {
            await LoadCollectionRequestsAsync(SelectedCollection);
        }

        SelectedRequest = SelectedCollection?.Requests.FirstOrDefault();
        if (SelectedCollection is not null && SelectedRequest is not null)
        {
            OpenCollectionRequestTab(SelectedCollection, SelectedRequest);
        }
        else
        {
            OpenScratchTab();
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
        await _workspaceStore.SaveCollectionRequestsAsync(WorkspacePath, SelectedCollection, SelectedCollection.Requests, replaceExisting: true);
        CloseTabsForRequest(request.Id);
        SelectedRequest = SelectedCollection.Requests.FirstOrDefault();
        if (SelectedRequest is not null)
        {
            OpenCollectionRequestTab(SelectedCollection, SelectedRequest);
        }
        else
        {
            OpenScratchTab();
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
            RequestDirectory = $"requests/{ToFileName(collection.Name)}-copy-{Collections.Count + 1}",
            Requests = collection.Requests.Select(CloneRequest).ToList()
        };
        Collections.Add(clone);
        Workspace.Collections = Collections;
        await SaveWorkspaceAsync();
        SelectedCollection = clone;
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

        Collections.Remove(collection);
        Workspace.Collections = Collections;
        await _workspaceStore.DeleteCollectionRequestsAsync(WorkspacePath, collection);
        await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);
        if (ReferenceEquals(SelectedCollection, collection))
        {
            SelectedCollection = Collections.FirstOrDefault();
            SelectedRequest = null;
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
        await _workspaceStore.SaveCollectionRequestsAsync(WorkspacePath, collection, collection.Requests, replaceExisting: true);
        SelectedCollection = collection;
        SelectedRequest = clone;
        OpenCollectionRequestTab(collection, clone);
        AddActivity("Request", $"已复制 {request.Name}");
        NotifyChanged();
    }

    public async Task DuplicateRequestAsync(RequestEventArgs args) => await DuplicateRequestAsync(args.Collection, args.Request);

    public async Task DeleteRequestAsync(ReadableCollection collection, ReadableRequest request)
    {
        await LoadCollectionRequestsAsync(collection);
        collection.Requests.RemoveAll(item => string.Equals(item.Id, request.Id, StringComparison.OrdinalIgnoreCase));
        await _workspaceStore.SaveCollectionRequestsAsync(WorkspacePath, collection, collection.Requests, replaceExisting: true);
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
        OpenPathInShell(GetCollectionDirectory(collection));
    }

    public void OpenCollectionInExplorer(CollectionEventArgs args) => OpenCollectionInExplorer(args.Collection);

    public void OpenRequestDefault(ReadableCollection collection, ReadableRequest request)
    {
        OpenPathInShell(Path.Combine(GetCollectionDirectory(collection), $"{ToFileName(request.Name)}.json"));
    }

    public void OpenRequestDefault(RequestEventArgs args) => OpenRequestDefault(args.Collection, args.Request);

    public void OpenSpecificationInExplorer(ReadableSpecification specification)
    {
        if (!string.IsNullOrWhiteSpace(specification.Path))
        {
            OpenPathInShell(ResolveWorkspacePath(specification.Path));
        }
    }

    public void OpenSpecificationInExplorer(SpecificationEventArgs args) => OpenSpecificationInExplorer(args.Specification);

    public void SelectRequestTab(string tabId)
    {
        var tab = RequestTabs.FirstOrDefault(item => item.Id == tabId);
        if (tab is null)
        {
            return;
        }

        ActivateTab(tab);
        SyncSelectionFromTab(tab);
        NotifyChanged();
    }

    public void CloseRequestTab(string tabId)
    {
        var index = RequestTabs.FindIndex(tab => tab.Id == tabId);
        if (index < 0)
        {
            return;
        }

        var wasActive = string.Equals(ActiveRequestTabId, tabId, StringComparison.Ordinal);
        RequestTabs.RemoveAt(index);
        if (RequestTabs.Count == 0)
        {
            OpenScratchTab();
        }
        else if (wasActive)
        {
            ActivateTab(RequestTabs[Math.Clamp(index - 1, 0, RequestTabs.Count - 1)]);
            if (ActiveRequestTab is not null)
            {
                SyncSelectionFromTab(ActiveRequestTab);
            }
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

        var collection = Collections.FirstOrDefault(item => string.Equals(item.Id, tab.CollectionId, StringComparison.OrdinalIgnoreCase));
        var request = collection?.Requests.FirstOrDefault(item => string.Equals(item.Id, tab.RequestId, StringComparison.OrdinalIgnoreCase));
        if (collection is null || request is null)
        {
            await SaveActiveRequestAsNewAsync();
            return;
        }

        ApplyTabToRequest(tab, request);
        await _workspaceStore.SaveCollectionRequestsAsync(WorkspacePath, collection, collection.Requests, replaceExisting: true);
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

        var collection = SelectedCollection ?? Collections.FirstOrDefault();
        if (collection is null)
        {
            collection = new ReadableCollection
            {
                Name = "Saved Requests",
                SourceType = ReadableCollectionSourceType.Local,
                RequestDirectory = "requests/saved"
            };
            Collections.Add(collection);
            Workspace.Collections = Collections;
        }

        await LoadCollectionRequestsAsync(collection);
        var request = new ReadableRequest
        {
            Name = NextRequestName(collection, tab.Title)
        };
        ApplyTabToRequest(tab, request);
        collection.Requests.Add(request);
        await _workspaceStore.SaveWorkspaceAsync(WorkspacePath, Workspace);
        await _workspaceStore.SaveCollectionRequestsAsync(WorkspacePath, collection, collection.Requests, replaceExisting: true);

        tab.Origin = RequestTabOrigin.Collection;
        tab.CollectionId = collection.Id;
        tab.RequestId = request.Id;
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

    public void SelectResponseNode(string path)
    {
        EnsureActiveRequestTab().ResponseNodePath = string.IsNullOrWhiteSpace(path) ? "root" : path;
        NotifyChanged();
    }

    public void SelectResponseNode(ResponseNodeSelectedEventArgs args) => SelectResponseNode(args.Path);

    public void SendAiMessage()
    {
        var prompt = AiDraft.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        AiMessages.Add(new AiChatMessage("user", prompt));
        AiMessages.Add(new AiChatMessage("assistant", BuildLocalAiReply(prompt)));
        AiDraft = string.Empty;
        AddActivity("AI", "已根据当前请求生成本地建议");
        NotifyChanged();
    }

    private string BuildLocalAiReply(string prompt)
    {
        if (ResponseText.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase) || StatusText.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return $"当前请求失败：{StatusText}。建议先检查 URL、代理、证书和环境变量。你刚才问：{prompt}";
        }

        if (Operations.Count > 0 && ViewMode == "preview")
        {
            return $"当前 spec 有 {Operations.Count} 个 operation。可以选择一个 operation 打开到 Request，再根据参数和响应生成测试用例。";
        }

        return $"当前请求是 {Method} {Url}。我可以继续帮你整理 headers、body schema、环境变量或错误排查步骤。";
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

    private RequestWorkspaceTab EnsureActiveRequestTab()
    {
        if (ActiveRequestTab is { } tab)
        {
            return tab;
        }

        return OpenScratchTab();
    }

    private void EnsureScratchTab()
    {
        if (RequestTabs.Count == 0)
        {
            OpenScratchTab();
        }
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

    private RequestWorkspaceTab OpenCollectionRequestTab(ReadableCollection collection, ReadableRequest request)
    {
        var existing = RequestTabs.FirstOrDefault(tab =>
            tab.Origin == RequestTabOrigin.Collection
            && string.Equals(tab.CollectionId, collection.Id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(tab.RequestId, request.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SelectedCollection = collection;
            SelectedRequest = request;
            ActivateTab(existing);
            return existing;
        }

        SelectedCollection = collection;
        SelectedRequest = request;
        var tab = CreateTabFromRequest(request, RequestTabOrigin.Collection, "Collection");
        tab.CollectionId = collection.Id;
        tab.RequestId = request.Id;
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

        var tab = CreateTabFromRequest(operation.Request, RequestTabOrigin.Specification, "Specification");
        tab.Title = operation.Name;
        tab.SourceKey = key;
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

    private static RequestWorkspaceTab CreateTabFromRequest(ReadableRequest request, RequestTabOrigin origin, string activeSource)
    {
        return new RequestWorkspaceTab
        {
            Title = request.Name,
            Origin = origin,
            ActiveSource = activeSource,
            Method = request.Method,
            Url = request.Url,
            BodyText = request.Body?.Content ?? string.Empty
        };
    }

    private void ActivateTab(RequestWorkspaceTab tab)
    {
        ActiveRequestTabId = tab.Id;
        DocumentTitle = tab.Title;
        ActiveSource = tab.ActiveSource;
        ViewMode = "request";
    }

    private void CloseTabsForRequest(string requestId)
    {
        RequestTabs.RemoveAll(tab => string.Equals(tab.RequestId, requestId, StringComparison.OrdinalIgnoreCase));
        if (ActiveRequestTab is null)
        {
            OpenScratchTab();
        }
    }

    private void SyncSelectionFromTab(RequestWorkspaceTab tab)
    {
        if (tab.Origin != RequestTabOrigin.Collection)
        {
            SelectedRequest = null;
            return;
        }

        SelectedCollection = Collections.FirstOrDefault(collection => string.Equals(collection.Id, tab.CollectionId, StringComparison.OrdinalIgnoreCase));
        SelectedRequest = SelectedCollection?.Requests.FirstOrDefault(request => string.Equals(request.Id, tab.RequestId, StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyTabToRequest(RequestWorkspaceTab tab, ReadableRequest request)
    {
        request.Name = tab.Title;
        request.Method = tab.Method;
        request.Url = tab.Url;
        request.Body = string.IsNullOrWhiteSpace(tab.BodyText)
            ? null
            : new ReadableBody
            {
                Type = ReadableBodyType.Json,
                ContentType = "application/json",
                Content = tab.BodyText
            };
    }

    private static ReadableRequest CloneRequest(ReadableRequest request)
    {
        var json = JsonSerializer.Serialize(request, ReadableHttpJsonStorage.JsonOptions);
        var clone = JsonSerializer.Deserialize<ReadableRequest>(json, ReadableHttpJsonStorage.JsonOptions) ?? new ReadableRequest();
        clone.Id = Guid.NewGuid().ToString("N");
        return clone;
    }

    private string NextCollectionName(string baseName)
    {
        var name = baseName;
        var index = 2;
        while (Collections.Any(collection => string.Equals(collection.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            name = $"{baseName} {index}";
            index++;
        }

        return name;
    }

    private string GetCollectionDirectory(ReadableCollection collection)
    {
        return string.IsNullOrWhiteSpace(collection.RequestDirectory)
            ? Path.Combine(WorkspacePath, "requests", ToFileName(collection.Name))
            : Path.Combine(WorkspacePath, collection.RequestDirectory);
    }

    private static void OpenPathInShell(string path)
    {
        var target = Directory.Exists(path) ? path : File.Exists(path) ? path : Path.GetDirectoryName(path);
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
        return new ReadableExecutionContext
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

    private void ResetSettingsDraft()
    {
        SettingsDraft = new AppSettingsDraft(
            ThemeMode,
            ProxyMode,
            CustomProxyUrl,
            CustomProxyUsername,
            CustomProxyPassword,
            Language,
            DevToolsEnabled);
    }

    private void UpdateSettingsDraft(AppSettingsDraft draft)
    {
        SettingsDraft = draft;
        NotifyChanged();
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

    private static string NormalizeProxyMode(string value)
    {
        return value switch
        {
            ProxyNone => ProxyNone,
            ProxyCustom => ProxyCustom,
            _ => ProxySystem
        };
    }

    private void AddSpecFile(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && !SpecFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            SpecFiles.Insert(0, path);
        }
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

    private void AddWorkspaceOption(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && !WorkspaceOptions.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            WorkspaceOptions.Insert(0, path);
        }
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
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                return root.EnumerateObject()
                    .Select(property => property.Name)
                    .Take(24)
                    .ToList();
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                return root.EnumerateArray()
                    .Select((_, index) => $"[{index}]")
                    .Take(24)
                    .ToList();
            }
        }
        catch (JsonException)
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
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty(ResponseNodePath, out var property))
            {
                return FormatJsonElement(property);
            }

            if (root.ValueKind == JsonValueKind.Array
                && ResponseNodePath.StartsWith("[", StringComparison.Ordinal)
                && ResponseNodePath.EndsWith("]", StringComparison.Ordinal)
                && int.TryParse(ResponseNodePath[1..^1], out var index))
            {
                var items = root.EnumerateArray().ToList();
                if (index >= 0 && index < items.Count)
                {
                    return FormatJsonElement(items[index]);
                }
            }
        }
        catch (JsonException)
        {
            EnsureActiveRequestTab().ResponseNodePath = "root";
        }

        return TryFormatJson(ResponseText);
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
        return JsonSerializer.Serialize(element, new JsonSerializerOptions { WriteIndented = true });
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

    private void NotifyChanged()
    {
        Changed?.Invoke();
    }
}
