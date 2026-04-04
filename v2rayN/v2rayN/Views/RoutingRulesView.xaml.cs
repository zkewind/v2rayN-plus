namespace v2rayN.Views;

public partial class RoutingRulesView
{
    private RoutingRulesViewModel ViewModel => (RoutingRulesViewModel)DataContext;

    public RoutingRulesView()
    {
        InitializeComponent();

        DataContext = new RoutingRulesViewModel(UpdateViewHandler);

        trvRoutingSets.SelectedItemChanged += TrvRoutingSets_SelectedItemChanged;
        trvRoutingSets.MouseDoubleClick += TrvRoutingSets_MouseDoubleClick;
        lstRoutingRules.MouseDoubleClick += LstRoutingRules_MouseDoubleClick;
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.RoutingSettingWindow:
                return new RoutingSettingWindow(obj as string).ShowDialog() ?? false;
        }

        return await Task.FromResult(true);
    }

    private void TrvRoutingSets_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (ViewModel != null)
        {
            ViewModel.SelectedTreeNode = e.NewValue as RoutingRulesTreeNode;
        }
    }

    private void TrvRoutingSets_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel?.SelectedTreeNode?.IsLeaf == true)
        {
            ViewModel.OpenRoutingSettingAsync(ViewModel.SelectedTreeNode.RoutingItemId).ContinueWith(_ => { });
        }
    }

    private void LstRoutingRules_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel?.SelectedRuleItem?.RoutingItemId.IsNotEmpty() == true)
        {
            ViewModel.OpenRoutingSettingAsync(ViewModel.SelectedRuleItem.RoutingItemId).ContinueWith(_ => { });
        }
    }
}
