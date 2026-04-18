namespace v2rayN.Desktop.Tests;

[TestFixture]
public sealed class ChromeTabsMarkupTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
    private static readonly string MainWindowPath = Path.Combine(RepoRoot, "v2rayN.Desktop", "Views", "MainWindow.axaml");
    private static readonly string GlobalStylesPath = Path.Combine(RepoRoot, "v2rayN.Desktop", "Assets", "GlobalStyles.axaml");
    private static readonly string GlobalResourcesPath = Path.Combine(RepoRoot, "v2rayN.Desktop", "Assets", "GlobalResources.axaml");

    [Test]
    public void MainWindow_horizontal_status_tabs_use_the_chrome_tabs_class()
    {
        var markup = File.ReadAllText(MainWindowPath);

        Assert.That(markup, Does.Match("<TabControl[\\s\\S]*?Classes=\"[^\"]*\\bchrome-tabs\\b\"[\\s\\S]*?x:Name=\"tabMain\""));
        Assert.That(markup, Does.Match("<TabControl[\\s\\S]*?Classes=\"[^\"]*\\bchrome-tabs\\b\"[\\s\\S]*?x:Name=\"tabTopMain1\""));
    }

    [Test]
    public void Global_styles_define_a_dedicated_chrome_tabs_theme()
    {
        var stylesMarkup = File.ReadAllText(GlobalStylesPath);
        var resourcesMarkup = File.ReadAllText(GlobalResourcesPath);

        Assert.That(resourcesMarkup, Does.Contain("x:Key=\"ChromeTabControlTheme\""));
        Assert.That(stylesMarkup, Does.Contain("Selector=\"TabControl.chrome-tabs TabItem:selected\""));
    }
}
