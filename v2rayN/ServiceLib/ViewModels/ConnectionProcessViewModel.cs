using System.Net;
using ServiceLib.Events;
using ServiceLib.Services;

namespace ServiceLib.ViewModels;

public class ConnectionProcessViewModel : MyReactiveObject
{
    private const string ProcessSortColumn = "Process";
    private const string DirectOutboundDisplay = "Direct";
    private const string ProxyOutboundDisplay = "Proxy";
    private const string ActiveStatusDisplay = "Active";

    private bool _processGroupSortAscending = true;
    private string? _childSortColumn;
    private bool _childSortAscending = true;

    public IObservableCollection<ConnectionProcessModel> ProcessItems { get; } = new ObservableCollectionExtended<ConnectionProcessModel>();

    [Reactive]
    public string HostFilter { get; set; }

    [Reactive]
    public bool AutoRefresh { get; set; }

    [Reactive]
    public bool ShowDirectConnections { get; set; } = true;

    [Reactive]
    public bool ShowProxyConnections { get; set; } = true;

    [Reactive]
    public ConnectionProcessModel? SelectedSource { get; set; }

    public ReactiveCommand<Unit, Unit> ConnectionCloseAllCmd { get; }
    public ReactiveCommand<Unit, Unit> AddRoutingRuleCmd { get; }
    public ReactiveCommand<ConnectionProcessModel, Unit> ToggleExpandCmd { get; }

    private readonly List<ConnectionProcessModel> _processGroups = new();
    private List<ConnectionItem> _allConnections = new();

    public ConnectionProcessViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        _updateView = updateView;
        AutoRefresh = _config.ClashUIItem.ConnectionsAutoRefresh;

        this.WhenAnyValue(
           x => x.AutoRefresh,
           y => y == true)
               .Subscribe(_ => { _config.ClashUIItem.ConnectionsAutoRefresh = AutoRefresh; });

        this.WhenAnyValue(
           x => x.HostFilter,
           x => x.ShowDirectConnections,
           x => x.ShowProxyConnections)
               .Skip(1)
               .Subscribe(_ => ApplyFilters());

        AppEvents.ConnectionFailuresChanged
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => ApplyFilters());

        ConnectionCloseAllCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            ProcessItems.Clear();
            _processGroups.Clear();
            _allConnections.Clear();
            ConnectionFailureTracker.Instance.Clear();
            await ClashApiManager.Instance.ClashConnectionClose(string.Empty);
            await GetClashConnections();
        });

        var canAddRoutingRule = this.WhenAnyValue(
            x => x.SelectedSource)
            .Select(selected => selected != null);
        AddRoutingRuleCmd = ReactiveCommand.CreateFromTask(AddSelectedToRoutingAsync, canAddRoutingRule);

        ToggleExpandCmd = ReactiveCommand.Create<ConnectionProcessModel>(ToggleExpand);

        _ = Init();
    }

    private async Task Init()
    {
        await DelayTestTask();
    }

    private void ToggleExpand(ConnectionProcessModel group)
    {
        if (!group.IsProcess) return;

        group.IsExpanded = !group.IsExpanded;
        RebuildFlatList();
    }

    private static string GetProcessGroupKey(MetadataItem? metadata)
    {
        if (metadata == null)
            return "(unknown)";

        if (!string.IsNullOrEmpty(metadata.process))
            return metadata.process;

        if (!string.IsNullOrEmpty(metadata.processPath))
        {
            var path = metadata.processPath.Replace('/', '\\');
            var lastSlash = path.LastIndexOf('\\');
            if (lastSlash >= 0 && lastSlash < path.Length - 1)
            {
                var processName = path[(lastSlash + 1)..];
                if (!string.IsNullOrEmpty(processName))
                    return processName;
            }
        }

        return "(unknown)";
    }

    private static string GetOutboundDisplay(ConnectionItem item)
    {
        return IsDirectConnection(item) ? DirectOutboundDisplay : ProxyOutboundDisplay;
    }

    private static bool IsDirectConnection(ConnectionItem item)
    {
        var finalOutbound = item.chains?
            .LastOrDefault(x => x.IsNotEmpty())?
            .Trim();

        return string.Equals(finalOutbound, "direct", StringComparison.OrdinalIgnoreCase);
    }

    private void RebuildFlatList()
    {
        var flat = new List<ConnectionProcessModel>();
        foreach (var group in _processGroups)
        {
            flat.Add(group);
            if (group.IsExpanded && group.Children != null)
            {
                flat.AddRange(group.Children);
            }
        }

        ProcessItems.Clear();
        ProcessItems.AddRange(flat);
    }

    private async Task GetClashConnections()
    {
        var ret = await ClashApiManager.Instance.GetClashConnectionsAsync();
        if (ret == null) return;

        RxSchedulers.MainThreadScheduler.Schedule(ret.connections, (scheduler, connections) =>
        {
            _ = RefreshProcessGroups(connections);
            return Disposable.Empty;
        });
    }

    public async Task RefreshProcessGroups(List<ConnectionItem>? connections)
    {
        _allConnections = connections?.ToList() ?? new();
        ConnectionFailureTracker.Instance.UpdateActiveConnections(_allConnections);
        ApplyFilters();
        await Task.CompletedTask;
    }

    private void ApplyFilters()
    {
        var expandedStates = _processGroups
            .Where(g => g.IsProcess)
            .ToDictionary(g => g.ProcessName ?? string.Empty, g => g.IsExpanded);

        _processGroups.Clear();

        var dtNow = DateTime.Now;
        var groupedRows = new Dictionary<string, List<ConnectionProcessModel>>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in _allConnections)
        {
            var row = CreateActiveConnectionRow(item, dtNow);
            if (!ShouldIncludeRow(row))
            {
                continue;
            }

            AddGroupedRow(groupedRows, row.ProcessName ?? "(unknown)", row);
        }

        foreach (var item in ConnectionFailureTracker.Instance.GetRecentFailures())
        {
            var row = CreateFailedConnectionRow(item, dtNow);
            if (!ShouldIncludeRow(row))
            {
                continue;
            }

            AddGroupedRow(groupedRows, row.ProcessName ?? "(unknown)", row);
        }

        var orderedGroups = _processGroupSortAscending
            ? groupedRows.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            : groupedRows.OrderByDescending(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var grp in orderedGroups)
        {
            var children = SortChildren(grp.Value);

            if (children.Count == 0) continue;

            _processGroups.Add(new ConnectionProcessModel
            {
                IsProcess = true,
                ProcessName = grp.Key,
                ConnectionCount = children.Count,
                IsExpanded = expandedStates.GetValueOrDefault(grp.Key, true),
                Children = children,
            });
        }

        RebuildFlatList();
    }

    public void ApplyColumnSort(string? columnTag)
    {
        if (columnTag.IsNullOrEmpty())
        {
            return;
        }

        if (string.Equals(columnTag, ProcessSortColumn, StringComparison.OrdinalIgnoreCase))
        {
            _processGroupSortAscending = !_processGroupSortAscending;
        }
        else if (string.Equals(_childSortColumn, columnTag, StringComparison.OrdinalIgnoreCase))
        {
            _childSortAscending = !_childSortAscending;
        }
        else
        {
            _childSortColumn = columnTag;
            _childSortAscending = true;
        }

        ApplyFilters();
    }

    private List<ConnectionProcessModel> SortChildren(IEnumerable<ConnectionProcessModel> children)
    {
        if (_childSortColumn.IsNullOrEmpty())
        {
            return children
                .OrderByDescending(x => x.IsFailed)
                .ThenByDescending(x => x.SortTime)
                .ToList();
        }

        IOrderedEnumerable<ConnectionProcessModel> ordered = _childSortColumn switch
        {
            "Host" => OrderByString(children, x => x.Host),
            "Network" => OrderByString(children, x => x.Network),
            "Type" => OrderByString(children, x => x.Type),
            "Outbound" => OrderByString(children, x => x.Outbound),
            "Status" => OrderByString(children, x => x.Status),
            "Chain" => OrderByString(children, x => x.Chain),
            "Elapsed" => _childSortAscending
                ? children.OrderByDescending(x => x.SortTime)
                : children.OrderBy(x => x.SortTime),
            _ => children
                .OrderByDescending(x => x.IsFailed)
                .ThenByDescending(x => x.SortTime),
        };

        return ordered
            .ThenByDescending(x => x.IsFailed)
            .ThenByDescending(x => x.SortTime)
            .ToList();
    }

    private IOrderedEnumerable<ConnectionProcessModel> OrderByString(IEnumerable<ConnectionProcessModel> children, Func<ConnectionProcessModel, string?> selector)
    {
        return _childSortAscending
            ? children.OrderBy(x => selector(x) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            : children.OrderByDescending(x => selector(x) ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    private static void AddGroupedRow(IDictionary<string, List<ConnectionProcessModel>> groupedRows, string processName, ConnectionProcessModel row)
    {
        if (!groupedRows.TryGetValue(processName, out var rows))
        {
            rows = new List<ConnectionProcessModel>();
            groupedRows[processName] = rows;
        }

        rows.Add(row);
    }

    private ConnectionProcessModel CreateActiveConnectionRow(ConnectionItem item, DateTime dtNow)
    {
        var metadata = item.metadata;
        var ruleDomain = metadata?.host;
        var ruleIp = metadata?.destinationIP;
        var host = BuildHost(ruleDomain, ruleIp, metadata?.destinationPort);
        var chain = $"{item.rule} , {string.Join("->", item.chains ?? new())}".Trim(' ', ',');

        return new ConnectionProcessModel
        {
            IsProcess = false,
            IsFailed = false,
            Id = item.id,
            ProcessName = GetProcessGroupKey(metadata),
            Host = host,
            Network = metadata?.network,
            Type = metadata?.type,
            Outbound = GetOutboundDisplay(item),
            Elapsed = (dtNow - item.start).ToString(@"hh\:mm\:ss"),
            Status = ActiveStatusDisplay,
            Chain = chain,
            RuleDomain = ruleDomain,
            RuleIP = ruleIp,
            SortTime = item.start,
            Upload = item.upload,
            Download = item.download,
        };
    }

    private ConnectionProcessModel CreateFailedConnectionRow(FailedConnectionItem item, DateTime dtNow)
    {
        var outbound = string.Equals(item.FinalOutbound, "direct", StringComparison.OrdinalIgnoreCase)
            ? DirectOutboundDisplay
            : ProxyOutboundDisplay;

        return new ConnectionProcessModel
        {
            IsProcess = false,
            IsFailed = true,
            ProcessName = item.ProcessName.IsNotEmpty() ? item.ProcessName : "(unknown)",
            Host = BuildHost(item.TargetHost, item.RawTarget, item.TargetPort),
            Network = item.Network,
            Type = item.Type,
            Outbound = outbound,
            Elapsed = (dtNow - item.LastSeen).ToString(@"hh\:mm\:ss"),
            Status = item.ErrorMessage,
            Chain = item.OutboundTag.IsNotEmpty() ? $"{item.OutboundTag}->{item.FinalOutbound}" : item.FinalOutbound,
            RuleDomain = IsLikelyIp(item.TargetHost) ? null : item.TargetHost,
            RuleIP = IsLikelyIp(item.TargetHost) ? item.TargetHost : null,
            SortTime = item.LastSeen,
        };
    }

    private static string BuildHost(string? preferredHost, string? fallbackHost, string? port)
    {
        var host = preferredHost.IsNotEmpty() ? preferredHost : fallbackHost;
        return port.IsNotEmpty() ? $"{host}:{port}" : host ?? string.Empty;
    }

    private bool ShouldIncludeRow(ConnectionProcessModel row)
    {
        if (HostFilter.IsNotEmpty() && !(row.Host?.Contains(HostFilter, StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return false;
        }

        var isDirectConnection = string.Equals(row.Outbound, DirectOutboundDisplay, StringComparison.OrdinalIgnoreCase);
        if ((isDirectConnection && !ShowDirectConnections) || (!isDirectConnection && !ShowProxyConnections))
        {
            return false;
        }

        return true;
    }

    private static bool IsLikelyIp(string? host)
    {
        return host.IsNotEmpty() && IPAddress.TryParse(host, out _);
    }

    private async Task AddSelectedToRoutingAsync()
    {
        var selected = SelectedSource;
        if (selected is null)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectRules);
            return;
        }

        var rule = new RulesItem
        {
            Id = Utils.GetGuid(false),
            Enabled = true,
            OutboundTag = Global.ProxyTag,
        };

        if (selected.IsProcess)
        {
            // 选中进程组：只设置进程名称
            rule.Process = selected.ProcessName.IsNotEmpty() && selected.ProcessName != "(unknown)"
                ? [selected.ProcessName]
                : null;
            rule.Remarks = selected.ProcessName;
        }
        else
        {
            // 选中具体连接项：保持原有逻辑
            rule.Remarks = selected.Host;
            rule.Domain = selected.RuleDomain.IsNotEmpty() ? [selected.RuleDomain] : null;
            rule.Ip = selected.RuleIP.IsNotEmpty() ? [selected.RuleIP] : null;
            rule.Process = selected.ProcessName.IsNotEmpty() && selected.ProcessName != "(unknown)"
                ? [selected.ProcessName]
                : null;
        }

        if (await _updateView?.Invoke(EViewAction.RoutingRuleDetailsWindow, rule) != true)
        {
            return;
        }

        var routing = await ConfigHandler.GetDefaultRouting(_config);
        if (routing == null)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
            return;
        }

        var rules = JsonUtils.Deserialize<List<RulesItem>>(routing.RuleSet) ?? [];
        rules.Insert(0, rule);
        routing.RuleNum = rules.Count;
        routing.RuleSet = JsonUtils.Serialize(rules, false);

        if (await ConfigHandler.SaveRoutingItem(_config, routing) == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
            AppEvents.ReloadRequested.Publish(Unit.Default);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }

    public async Task DelayTestTask()
    {
        _ = Task.Run(async () =>
        {
            var numOfExecuted = 1;
            while (true)
            {
                await Task.Delay(1000 * 5);
                numOfExecuted++;
                if (!(AutoRefresh && AppManager.Instance.ShowInTaskbar && AppManager.Instance.IsRunningCore(ECoreType.sing_box)))
                {
                    continue;
                }

                if (_config.ClashUIItem.ConnectionsRefreshInterval <= 0)
                {
                    continue;
                }

                if (numOfExecuted % _config.ClashUIItem.ConnectionsRefreshInterval != 0)
                {
                    continue;
                }

                await GetClashConnections();
            }
        });

        await Task.CompletedTask;
    }
}
