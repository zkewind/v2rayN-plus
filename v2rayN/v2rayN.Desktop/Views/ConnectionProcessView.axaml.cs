namespace v2rayN.Desktop.Views;

public partial class ConnectionProcessView : ReactiveUserControl<ConnectionProcessViewModel>
{
    public ConnectionProcessView()
    {
        InitializeComponent();

        ViewModel = new ConnectionProcessViewModel(UpdateViewHandler);
        lstProcessConnections.Sorting += LstProcessConnections_Sorting;

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.ProcessItems, v => v.lstProcessConnections.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource, v => v.lstProcessConnections.SelectedItem).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.ConnectionCloseAllCmd, v => v.btnConnectionCloseAll).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ConnectionCloseAllCmd, v => v.menuConnectionCloseAll).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddRoutingRuleCmd, v => v.menuAddRoutingRule).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.HostFilter, v => v.txtHostFilter.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ShowDirectConnections, v => v.btnShowDirectConnections.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ShowProxyConnections, v => v.btnShowProxyConnections.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AutoRefresh, v => v.togAutoRefresh.IsChecked).DisposeWith(disposables);
        });
    }

    private void LstProcessConnections_Sorting(object? sender, DataGridColumnEventArgs e)
    {
        e.Handled = true;
        ViewModel?.ApplyColumnSort(e.Column?.Tag?.ToString());
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.RoutingRuleDetailsWindow:
                if (obj is RulesItem rule)
                {
                    var owner = (Window?)TopLevel.GetTopLevel(this);
                    if (owner == null)
                    {
                        return false;
                    }

                    return await new RoutingRuleDetailsWindow(rule).ShowDialog<bool>(owner);
                }
                break;
        }

        return await Task.FromResult(true);
    }
}
