using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using MatDesktop;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;
using ThixTunnel.Pages;

[assembly: AvaloniaTestApplication(typeof(ThixTunnel.Tests.UiTestApp))]

namespace ThixTunnel.Tests;

/// <summary>The real app (App.axaml, MatDesktop's theme, the pages) rendered headless with Skia: real pixels, no screen needed.</summary>
public static class UiTestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>
/// The main window on MatDesktop driven like a user would: the owner's rules of the UI (toolbar above the lists, actions below,
/// edit pages with tabs and Speichern, Zurück … Löschen, hidden fields that do not apply), the keyboard (DESK-07) and the
/// snapshot of every window. Every test has its own data folder; no policy, autostart entry or ssh of the machine is touched.
/// </summary>
[Collection(SharedStateCollection.Name)]
public sealed class UiTests
{
    [AvaloniaFact]
    public void TheConnectionsOnlyConnectAndHaveTheToolbarAbove()
    {
        using var data = new UiData();
        var window = Open();

        var page = window.ConnectionsPage;
        Assert.Same(page, window.CurrentPage);
        Assert.Equal(["Grafana", "Jenkins", "Postgres Produktion", "Redis Staging"], page.List.Titles);

        // owner's decision: managing happens in the settings only - no Neu / Bearbeiten / Löschen on the main page
        var texts = page.GetLogicalDescendants().OfType<IconLabel>().Where(label => label.IsEffectivelyVisible).Select(label => label.Text).ToList();
        Assert.DoesNotContain("Neue Verbindung", texts);
        Assert.DoesNotContain("Löschen", texts);
        Assert.DoesNotContain("Bearbeiten", texts);
        Assert.Contains("Verbinden", texts);
        Assert.Contains("Trennen", texts);

        // the toolbar filters through the business layer
        page.Toolbar.FilterBox.SelectedIndex = Array.FindIndex(TunnelListPage.Filters, option => option.Value == RouteService.FilterProblem);
        Settle(window);
        Assert.Equal(["Jenkins", "Redis Staging"], page.List.Titles);
        page.Toolbar.FilterBox.SelectedIndex = 0;
        page.Toolbar.SearchBox.Text = "postgres";
        Settle(window);
        Assert.Equal(["Postgres Produktion"], page.List.Titles);
        Close(window);
    }

    [AvaloniaFact]
    public void ANewConnectionShowsItsErrorAtTheFieldAndSaves()
    {
        using var data = new UiData();
        var window = Open();
        window.ShowSettings(SettingsPage.RoutesTab);
        var edit = new TunnelEditPage(window, null);
        window.OpenPage(edit);
        Settle(window);

        // Speichern without a name: the error stands at the field Name, its tab is chosen
        edit.Layout.Tabs.SelectedIndex = 1;
        Click(edit, "Speichern");
        Settle(window);
        var nameField = Field(edit, nameof(TunnelRoute.Name));
        Assert.False(string.IsNullOrEmpty(nameField.Error));
        Assert.Equal(0, edit.Layout.Tabs.SelectedIndex);
        Assert.Same(edit, window.CurrentPage);

        edit.NameBox.Text = "Mail";
        edit.LocalPortBox.Text = "1143";
        edit.TargetPortBox.Text = "143";
        Click(edit, "Speichern");
        Settle(window);
        Assert.Null(nameField.Error);
        Assert.Same(window.SettingsPage, window.CurrentPage);
        Assert.Contains(RouteService.List(), route => route.Name == "Mail" && route.LocalPort == 1143);
        Assert.Contains("Mail", window.SettingsPage.Routes.List.Titles);

        // the buttons of an edit page: Speichern, …, Zurück, then Löschen with the gap
        var existing = new TunnelEditPage(window, SampleData.GrafanaRouteId);
        var buttons = existing.Layout.Actions.Children.OfType<Button>().ToList();
        Assert.Equal("Speichern", ((IconLabel)buttons[0].Content!).Text);
        Assert.Equal("Löschen", ((IconLabel)buttons[^1].Content!).Text);
        Assert.True(ActionBar.GetSeparated(buttons[^1]));
        Close(window);
    }

    [AvaloniaFact]
    public void TheKeyFileShowsOnlyForTheKeyFileMethod()
    {
        using var data = new UiData();
        var window = Open();
        var edit = new ProfileEditPage(window, SampleData.GatewayId);
        window.OpenPage(edit);
        Settle(window);
        Assert.False(edit.IsKeyFileShown);
        Assert.False(edit.IsKeySetupShown);

        edit.Methods.SelectedIndex = 1;
        Settle(window);
        Assert.True(edit.IsKeyFileShown);
        Assert.False(edit.IsKeySetupShown);

        edit.Methods.SelectedIndex = 2;
        Settle(window);
        Assert.False(edit.IsKeyFileShown);
        Assert.True(edit.IsKeySetupShown);
        Close(window);
    }

    [AvaloniaFact]
    public void TheSettingsApplyAtOnceAndShowAWrongSshAtItsField()
    {
        using var data = new UiData();
        var window = Open();
        window.ShowSettings();
        var settings = window.SettingsPage;
        Assert.Equal(
            [SettingsPage.GeneralTab, SettingsPage.RoutesTab, SettingsPage.ProfilesTab, SettingsPage.TransferTab, SettingsPage.UpdatesTab, SettingsPage.AdvancedTab],
            settings.Tabs.Items.OfType<TabItem>().Select(tab => (string)tab.Header!).ToList());

        settings.AppearanceChoice.SelectedIndex = (int)AppearanceChoice.Dark;
        Settle(window);
        Assert.Equal(AppearanceChoice.Dark, SettingsService.Get().Appearance);
        Assert.Equal(MatAppearance.Dark, MatTheme.Appearance);

        settings.AutostartSwitch.IsChecked = false;
        Settle(window);
        Assert.False(AutostartService.IsEnabled());

        settings.SelectTab(SettingsPage.AdvancedTab);
        Settle(window);
        settings.SshPathBox.Text = Path.Combine(data.Directory, "no-ssh.exe");
        settings.SshPathBox.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle(window);
        Assert.False(string.IsNullOrEmpty(settings.SshPathField.Error));
        Assert.Equal(SettingsPage.AdvancedTab, (settings.Tabs.SelectedItem as TabItem)?.Header);
        Assert.Equal("", SettingsService.Get().SshExecutablePath);
        App.ApplyAppearance(AppearanceChoice.System);
        Close(window);
    }

    [AvaloniaFact]
    public void EscapeGoesBackThenHidesAndTheKeysNavigate()
    {
        using var data = new UiData();
        var window = Open();
        var command = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

        window.KeyPressQwerty(PhysicalKey.Comma, command);
        Settle(window);
        Assert.Same(window.SettingsPage, window.CurrentPage);

        window.OpenPage(new ProfileEditPage(window, null));
        Settle(window);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Settle(window);
        Assert.Same(window.SettingsPage, window.CurrentPage);

        window.KeyPressQwerty(PhysicalKey.Digit1, command);
        Settle(window);
        Assert.Same(window.ConnectionsPage, window.CurrentPage);

        // Esc on a page without one before hides the window (DESK-06): the tunnels keep running
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Settle(window);
        Assert.False(window.IsVisible);

        window.ShowInFront();
        window.ShowAbout();
        Settle(window);
        Assert.IsType<AboutPage>(window.CurrentPage);
        Close(window);
    }

    [AvaloniaFact]
    public void TheSnapshotDrawsEveryWindow()
    {
        using var data = new TestData();
        var folder = Environment.GetEnvironmentVariable("THIXTUNNEL_SCREENSHOTS") is { Length: > 0 } shots ? shots : Path.Combine(data.Directory, "shots");
        var report = Snapshot.Render(folder);
        Assert.Equal(Snapshot.FileNames.Count, report.Written.Count + report.Skipped.Count);
        foreach (var file in report.Written)
        {
            Assert.True(new FileInfo(Path.Combine(folder, file)).Length > 500, $"{file} is empty");
        }
    }

    private static MainWindow Open()
    {
        var window = new MainWindow { Width = 1100, Height = 720 };
        window.Show();
        Settle(window);
        return window;
    }

    private static void Close(MainWindow window)
    {
        window.IsExiting = true;
        window.Close();
    }

    private static MatFormField Field(Control root, string field) =>
        root.GetLogicalDescendants().OfType<MatFormField>().First(formField => FormErrors.GetField(formField) == field);

    private static void Click(Control root, string text)
    {
        var button = root.GetLogicalDescendants().OfType<Button>().First(candidate => candidate.Content is IconLabel label && label.Text == text);
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
    }

    private static void Settle(TopLevel topLevel)
    {
        for (var round = 0; round < 5; round++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        topLevel.UpdateLayout();
    }

    /// <summary>A data folder of its own with the sample setup, the autostart entry in memory, version 1.0.0.</summary>
    private sealed class UiData : IDisposable
    {
        private readonly TestData _data = new();
        private readonly IDisposable _sandbox = SandboxScope.Begin();

        public UiData()
        {
            AppInfo.Pin("1.0.0", "release");
            SampleData.Create();
            TunnelManager.ShowStatusesForTests(SampleData.Statuses(DateTimeOffset.Now));
        }

        public string Directory => _data.Directory;

        public void Dispose()
        {
            TunnelManager.ShowStatusesForTests(null);
            AppInfo.Pin(null);
            _sandbox.Dispose();
            _data.Dispose();
        }
    }
}
