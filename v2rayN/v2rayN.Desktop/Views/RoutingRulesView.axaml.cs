namespace v2rayN.Desktop.Views;

public partial class RoutingRulesView : ReactiveUserControl<RoutingRulesViewModel>
{
    public RoutingRulesView()
    {
        InitializeComponent();

        ViewModel = new RoutingRulesViewModel(UpdateViewHandler);

        trvRoutingSets.SelectionChanged += TrvRoutingSets_SelectionChanged;
        trvRoutingSets.DoubleTapped += TrvRoutingSets_DoubleTapped;
        lstRoutingRules.DoubleTapped += LstRoutingRules_DoubleTapped;

        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.OpenRoutingSettingCmd, v => v.btnOpenRoutingSetting).DisposeWith(disposables);
        });
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.RoutingSettingWindow:
                if (TopLevel.GetTopLevel(this) is Window owner)
                {
                    return await new RoutingSettingWindow(obj as string).ShowDialog<bool>(owner);
                }
                break;
        }

        return await Task.FromResult(true);
    }

    private void TrvRoutingSets_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.SelectedTreeNode = trvRoutingSets.SelectedItem as RoutingRulesTreeNode;
        }
    }

    private void TrvRoutingSets_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel?.SelectedTreeNode?.IsLeaf == true)
        {
            ViewModel.OpenRoutingSettingAsync(ViewModel.SelectedTreeNode.RoutingItemId).ContinueWith(_ => { });
        }
    }

    private void LstRoutingRules_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel?.SelectedRuleItem?.RoutingItemId.IsNotEmpty() == true)
        {
            ViewModel.OpenRoutingSettingAsync(ViewModel.SelectedRuleItem.RoutingItemId).ContinueWith(_ => { });
        }
    }
}
