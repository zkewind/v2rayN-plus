using System;
using System.Linq;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using v2rayN.Desktop.Views;

namespace v2rayN.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // 去重合并的 ResourceDictionary，防止重复添加相同资源导致异常
        try
        {
            var merged = this.Resources?.MergedDictionaries;
            if (merged != null)
            {
                var existingKeys = new HashSet<object>();
                var toRemove = new List<IResourceDictionary>();

                foreach (var dict in merged.OfType<IResourceDictionary>().ToList())
                {
                    var keys = dict.Select(entry => entry.Key).ToList();

                    // 若任一键已存在，则认为这是重复字典，标记为移除
                    if (keys.Any(k => existingKeys.Contains(k)))
                    {
                        toRemove.Add(dict);
                    }
                    else
                    {
                        foreach (var k in keys)
                            existingKeys.Add(k);
                    }
                }

                foreach (var d in toRemove)
                    merged.Remove(d);
            }
        }
        catch (Exception ex)
        {
            // 记录但不阻塞启动
            Logging.SaveLog("Resource dedupe error", ex);
        }

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (!Design.IsDesignMode)
            {
                AppManager.Instance.InitComponents();
                DataContext = StatusBarViewModel.Instance;
            }

            desktop.Exit += OnExit;
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject != null)
        {
            Logging.SaveLog("CurrentDomain_UnhandledException", (Exception)e.ExceptionObject);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logging.SaveLog("TaskScheduler_UnobservedTaskException", e.Exception);
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
    }

    private async void MenuAddServerViaClipboardClick(object? sender, EventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow != null)
            {
                AppEvents.AddServerViaClipboardRequested.Publish();
                await Task.Delay(1000);
            }
        }
    }

    private async void MenuExit_Click(object? sender, EventArgs e)
    {
        await AppManager.Instance.AppExitAsync(false);
        AppManager.Instance.Shutdown(true);
    }
}
