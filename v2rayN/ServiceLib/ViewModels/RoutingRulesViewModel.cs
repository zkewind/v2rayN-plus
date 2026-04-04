namespace ServiceLib.ViewModels;

public class RoutingRulesViewModel : MyReactiveObject
{
    public IObservableCollection<RoutingRulesTreeNode> TreeNodes { get; } = new ObservableCollectionExtended<RoutingRulesTreeNode>();
    public IObservableCollection<RoutingRuleListItemModel> RuleItems { get; } = new ObservableCollectionExtended<RoutingRuleListItemModel>();

    [Reactive]
    public RoutingRulesTreeNode? SelectedTreeNode { get; set; }

    [Reactive]
    public RoutingRuleListItemModel? SelectedRuleItem { get; set; }

    public ReactiveCommand<Unit, Unit> OpenRoutingSettingCmd { get; }

    public RoutingRulesViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        _updateView = updateView;

        OpenRoutingSettingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await OpenRoutingSettingAsync();
        });

        this.WhenAnyValue(x => x.SelectedTreeNode)
            .Subscribe(_ => RefreshRuleItems());

        AppEvents.RoutingsMenuRefreshRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async _ => await RefreshDataAsync());

        _ = RefreshDataAsync();
    }

    public async Task RefreshDataAsync(string? preferredRoutingId = null)
    {
        await ConfigHandler.InitBuiltinRouting(_config);

        var checkedIds = GetCheckedRoutingIds();
        var preferredId = preferredRoutingId
                          ?? SelectedRuleItem?.RoutingItemId
                          ?? GetSelectedLeafNode()?.RoutingItemId
                          ?? checkedIds.FirstOrDefault();

        var routings = await AppManager.Instance.RoutingItems() ?? [];
        var rootNode = new RoutingRulesTreeNode(ResUI.menuRoutingSetting)
        {
            IsExpanded = true,
        };

        foreach (var item in routings.OrderBy(t => t.Sort))
        {
            var rules = JsonUtils.Deserialize<List<RulesItem>>(item.RuleSet) ?? [];
            var childNode = new RoutingRulesTreeNode(item.Remarks, RefreshRuleItems)
            {
                RoutingItemId = item.Id,
                IsLeaf = true,
                IsChecked = checkedIds.Contains(item.Id),
                IsCurrent = item.IsActive,
                IsExpanded = true,
                RuleCount = rules.Count,
                Rules = rules,
                Sort = item.Sort,
            };
            rootNode.Children.Add(childNode);
        }

        TreeNodes.Clear();
        TreeNodes.Add(rootNode);

        SelectedTreeNode = FindLeafNode(preferredId)
                           ?? rootNode.Children.FirstOrDefault(t => t.IsCurrent)
                           ?? rootNode.Children.FirstOrDefault()
                           ?? rootNode;

        RefreshRuleItems();
    }

    public async Task OpenRoutingSettingAsync(string? routingItemId = null)
    {
        var targetRoutingId = routingItemId
                              ?? SelectedRuleItem?.RoutingItemId
                              ?? GetSelectedLeafNode()?.RoutingItemId
                              ?? GetCheckedLeafNodes().FirstOrDefault()?.RoutingItemId
                              ?? TreeNodes.SelectMany(GetLeafNodes).FirstOrDefault(t => t.IsCurrent)?.RoutingItemId
                              ?? TreeNodes.SelectMany(GetLeafNodes).FirstOrDefault()?.RoutingItemId;

        if (await _updateView?.Invoke(EViewAction.RoutingSettingWindow, targetRoutingId) == true)
        {
            AppEvents.RoutingsMenuRefreshRequested.Publish();
            AppEvents.ReloadRequested.Publish();
            await RefreshDataAsync(targetRoutingId);
        }
    }

    private void RefreshRuleItems()
    {
        var checkedNodes = GetCheckedLeafNodes().ToList();
        var sourceNodes = checkedNodes.Count > 0
            ? checkedNodes
            : GetLeafNodes(SelectedTreeNode).ToList();

        if (sourceNodes.Count == 0)
        {
            sourceNodes = TreeNodes.SelectMany(GetLeafNodes).Where(t => t.IsCurrent).ToList();
        }
        if (sourceNodes.Count == 0)
        {
            sourceNodes = TreeNodes.SelectMany(GetLeafNodes).Take(1).ToList();
        }

        RuleItems.Clear();

        foreach (var routingNode in sourceNodes.OrderBy(t => t.Sort))
        {
            foreach (var (rule, index) in routingNode.Rules.Select((rule, index) => (rule, index)))
            {
                RuleItems.Add(new RoutingRuleListItemModel
                {
                    RoutingItemId = routingNode.RoutingItemId,
                    RoutingRemarks = routingNode.Title,
                    Enabled = rule.Enabled,
                    Remarks = rule.Remarks,
                    RuleTypeName = rule.RuleType?.ToString(),
                    OutboundTag = rule.OutboundTag,
                    Port = rule.Port,
                    Protocols = Utils.List2String(rule.Protocol),
                    InboundTags = Utils.List2String(rule.InboundTag),
                    Network = rule.Network,
                    MatchItems = Utils.List2String((rule.Domain ?? []).Concat(rule.Ip ?? []).Concat(rule.Process ?? []).ToList()),
                    RuleIndex = index + 1,
                    Sort = routingNode.Sort,
                });
            }
        }
    }

    private RoutingRulesTreeNode? GetSelectedLeafNode()
    {
        if (SelectedTreeNode == null)
        {
            return null;
        }

        if (SelectedTreeNode.IsLeaf)
        {
            return SelectedTreeNode;
        }

        return GetLeafNodes(SelectedTreeNode).FirstOrDefault();
    }

    private HashSet<string> GetCheckedRoutingIds()
    {
        return GetCheckedLeafNodes()
            .Select(t => t.RoutingItemId)
            .Where(t => t.IsNotEmpty())
            .Cast<string>()
            .ToHashSet();
    }

    private IEnumerable<RoutingRulesTreeNode> GetCheckedLeafNodes()
    {
        return TreeNodes
            .SelectMany(GetLeafNodes)
            .Where(t => t.IsLeaf && t.IsChecked && t.RoutingItemId.IsNotEmpty());
    }

    private RoutingRulesTreeNode? FindLeafNode(string? routingItemId)
    {
        if (routingItemId.IsNullOrEmpty())
        {
            return null;
        }

        return TreeNodes
            .SelectMany(GetLeafNodes)
            .FirstOrDefault(t => t.RoutingItemId == routingItemId);
    }

    private static IEnumerable<RoutingRulesTreeNode> GetLeafNodes(RoutingRulesTreeNode? node)
    {
        if (node == null)
        {
            yield break;
        }

        if (node.IsLeaf)
        {
            yield return node;
            yield break;
        }

        foreach (var child in node.Children)
        {
            foreach (var leaf in GetLeafNodes(child))
            {
                yield return leaf;
            }
        }
    }
}

public class RoutingRulesTreeNode : MyReactiveObject
{
    private readonly Action? _checkedChanged;
    private bool _isChecked;
    private bool _isExpanded;

    public RoutingRulesTreeNode(string title, Action? checkedChanged = null)
    {
        Title = title;
        _checkedChanged = checkedChanged;
    }

    public string Title { get; set; }
    public string? RoutingItemId { get; set; }
    public bool IsLeaf { get; set; }
    public bool IsCurrent { get; set; }
    public int RuleCount { get; set; }
    public int Sort { get; set; }
    public List<RulesItem> Rules { get; set; } = [];
    public IObservableCollection<RoutingRulesTreeNode> Children { get; } = new ObservableCollectionExtended<RoutingRulesTreeNode>();

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isChecked, value))
            {
                _checkedChanged?.Invoke();
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public string RuleCountText => IsLeaf ? $"({RuleCount})" : string.Empty;
}

public class RoutingRuleListItemModel
{
    public string? RoutingItemId { get; set; }
    public string RoutingRemarks { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string? Remarks { get; set; }
    public string? RuleTypeName { get; set; }
    public string? OutboundTag { get; set; }
    public string? Port { get; set; }
    public string? Protocols { get; set; }
    public string? InboundTags { get; set; }
    public string? Network { get; set; }
    public string? MatchItems { get; set; }
    public int RuleIndex { get; set; }
    public int Sort { get; set; }
}
