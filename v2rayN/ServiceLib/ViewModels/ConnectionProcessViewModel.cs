namespace ServiceLib.ViewModels;

public class ConnectionProcessViewModel : MyReactiveObject
{
    public IObservableCollection<ConnectionProcessModel> ProcessItems { get; } = new ObservableCollectionExtended<ConnectionProcessModel>();

    [Reactive]
    public string HostFilter { get; set; }

    [Reactive]
    public bool AutoRefresh { get; set; }

    public ReactiveCommand<Unit, Unit> ConnectionCloseAllCmd { get; }
    public ReactiveCommand<ConnectionProcessModel, Unit> ToggleExpandCmd { get; }

    // 内部存储所有进程分组（含子行），用于展开/折叠时重建列表
    private readonly List<ConnectionProcessModel> _processGroups = new();

    public ConnectionProcessViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        _updateView = updateView;
        AutoRefresh = _config.ClashUIItem.ConnectionsAutoRefresh;

        this.WhenAnyValue(
           x => x.AutoRefresh,
           y => y == true)
               .Subscribe(c => { _config.ClashUIItem.ConnectionsAutoRefresh = AutoRefresh; });

        ConnectionCloseAllCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            ProcessItems.Clear();
            _processGroups.Clear();
            await ClashApiManager.Instance.ClashConnectionClose(string.Empty);
            await GetClashConnections();
        });

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

    /// <summary>
    /// 获取进程分组的键，优先使用 process 字段，
    /// 如果为空则从 processPath 提取进程名，
    /// 都为空则返回 "(unknown)"
    /// </summary>
    private static string GetProcessGroupKey(MetadataItem? metadata)
    {
        if (metadata == null)
            return "(unknown)";

        // 优先使用 process 字段
        if (!string.IsNullOrEmpty(metadata.process))
            return metadata.process;

        // 如果 process 为空，尝试从 processPath 提取进程名
        if (!string.IsNullOrEmpty(metadata.processPath))
        {
            // 获取路径的最后一部分（进程名）
            var path = metadata.processPath.Replace('/', '\\');
            var lastSlash = path.LastIndexOf('\\');
            if (lastSlash >= 0 && lastSlash < path.Length - 1)
            {
                var processName = path.Substring(lastSlash + 1);
                if (!string.IsNullOrEmpty(processName))
                    return processName;
            }
        }

        return "(unknown)";
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

        RxSchedulers.MainThreadScheduler.Schedule(ret?.connections, (scheduler, connections) =>
        {
            _ = RefreshProcessGroups(connections);
            return Disposable.Empty;
        });
    }

    public async Task RefreshProcessGroups(List<ConnectionItem>? connections)
    {
        _processGroups.Clear();

        var dtNow = DateTime.Now;
        var grouped = (connections ?? new())
            .GroupBy(c => GetProcessGroupKey(c.metadata));

        foreach (var grp in grouped.OrderBy(g => g.Key))
        {
            var children = new List<ConnectionProcessModel>();
            foreach (var item in grp)
            {
                var host = $"{(item.metadata?.host.IsNullOrEmpty() == false ? item.metadata.host : item.metadata?.destinationIP)}:{item.metadata?.destinationPort}";
                if (HostFilter.IsNotEmpty() && !host.Contains(HostFilter)) continue;

                children.Add(new ConnectionProcessModel
                {
                    IsProcess = false,
                    Id = item.id,
                    Host = host,
                    Network = item.metadata?.network,
                    Type = item.metadata?.type,
                    Elapsed = (dtNow - item.start).ToString(@"hh\:mm\:ss"),
                    Chain = $"{item.rule} , {string.Join("->", item.chains ?? new())}",
                    Upload = item.upload,
                    Download = item.download,
                });
            }

            if (children.Count == 0 && HostFilter.IsNotEmpty()) continue;

            _processGroups.Add(new ConnectionProcessModel
            {
                IsProcess = true,
                ProcessName = grp.Key,
                ConnectionCount = children.Count,
                IsExpanded = true,
                Children = children,
            });
        }

        RebuildFlatList();
        await Task.CompletedTask;
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
