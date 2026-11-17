# CLAUDE.md

Guidance for working in this repository. Keep it in sync with the code: update it whenever architecture, interfaces or conventions change.

## What ThixTunnel is

A tray / menu bar application for **Windows and macOS** around `ssh -N -J … -L …`: SSH profiles (one server each), tunnel routes
(ordered hops + one local TCP forwarding), connect/disconnect with status, automatic connection and reconnection, tray, autostart,
setup (Windows) / app bundle (macOS), OTA updates. Concept and acceptance criteria: `docs/konzept.md`. UI language: German.
Code, comments and identifiers: English.

Stack: **.NET 10** (`net10.0`, one code base), C#, **Avalonia 11.3** (Fluent theme underneath our own look), xUnit,
the system OpenSSH (Windows OpenSSH `ssh.exe`, macOS `/usr/bin/ssh`).

## Build / run / test

- `dotnet build ThixTunnel.slnx` – expected: 0 warnings, 0 errors (CA1416: Windows-only code is guarded by
  `OperatingSystem.IsWindows()` or marked `[SupportedOSPlatform("windows")]`).
- `dotnet test ThixTunnel.slnx` – unit and business layer tests (each test gets its own data folder and LaunchAgents folder,
  `TestData`). `[WindowsFact]` / `[MacFact]` run only on their system; CI runs the tests on both.
- `powershell -File scripts\e2e-docker.ps1` – real two-hop tunnel against Docker SSH servers (`tests/e2e/Dockerfile`), Windows;
  the user's `~/.ssh` is never touched (own key and known_hosts via `SshConfigWriter.KnownHostsFileForTests`).
- Run the app against sample data: set `THIXTUNNEL_DATA_DIR` to a folder (data, logs and generated ssh configs go there).
  Never let a test run write the real data folder, the `HKCU\…\Run` value or `~/Library/LaunchAgents`.
- Real UI checks: start the app, drive it through UI Automation (Avalonia exposes `AutomationProperties.Name`), take screenshots
  and look at them. macOS cannot be checked from the Windows machine – CI builds and tests it, the owner tries it on a Mac.
- `scripts/build-icons.ps1` renders `src/ThixTunnel/Assets/thixtunnel.ico` and `branding/png/*` from the logo geometry;
  `scripts/package-macos.sh` builds `ThixTunnel.app` (Info.plist, icns, ad-hoc signature), the update zip and the dmg.

## Map

```
src/ThixTunnel.Core  (net10.0, no UI; Windows-only parts behind OperatingSystem checks)
  Models/            SshProfile, TunnelRoute, AppSettings, AppData (the JSON file), TunnelStatus, ListQuery, BusinessException
  Business/          THE business layer, static classes. Every change calls AccessPolicy first (permission check in the business layer):
                     AccessPolicy (policies LockConfiguration / DenyNetworkBinding / DisableUpdates: Windows HKLM\SOFTWARE\Policies\
                     ThixTunnel, macOS configuration profile info.schmoldt.thixtunnel), ProfileService, RouteService, SettingsService,
                     AutostartService (Windows HKCU Run, macOS LaunchAgent; "--autostart"), TunnelManager (sessions, states,
                     retries, pause, leftover ssh processes after a crash on macOS), UpdateService (manifest with one package per
                     platform, download, SHA-256), MacAppBundle (macOS: the .app, update = swap the bundle by script),
                     SessionResume (reconnect after an update), TransferService (import/export *.thixtunnel.json, same checks as
                     saving, one atomic write), InstallService (Windows only: self-installing exe, program folder, shortcuts,
                     "Installierte Apps" entry, uninstall), KeySetupService (password profile -> own ed25519 key via ssh-keygen,
                     authorized_keys through the jump hosts at the first manual connect, profile switched to the key; POSIX servers)
  Infrastructure/    DataStore (only the business layer uses it; atomic JSON writes), AppPaths (Windows %AppData%/%LocalAppData%,
                     macOS ~/Library/Application Support), AppInfo (version/channel from assembly metadata), SshConfigWriter
                     (per-connection config; -F reaches the jump hosts, -o does not), SshCommandBuilder, SshProcess,
                     SshErrorClassifier, ProcessJob (Windows kill-on-close job object), NetworkProbe, SshExecutable, AskPassProtocol, Log
src/ThixTunnel  (Avalonia app, the same program on Windows and macOS)
  Program.cs / App.axaml(.cs)  start-up: askpass mode, setup mode (Windows: --uninstall / --update / single-file exe outside the
                     program folder), single instance per data folder (mutex + pipe; Windows: exit signal for the setup),
                     tray / menu bar, Dock "reopen" (macOS), network events
  MainWindow         narrow connection list like a contact list (TunnelListPage: connect/disconnect only, no toolbar, no
                     add/edit/delete - managing happens only in the settings window, by decision of the
                     owner), status in the head, version (= about) bottom left, settings bottom right; X hides when "keep running" is on
  ThemedWindow       base of every window: colours, font, icon, dark title bar (Windows); WorkArea(), BringToFront()
  AppWindows/PageWindow  every other page opens in its own window (once per content), size clamped to the work area;
                     AppWindows.DataChanged refreshes open lists/editors after save, delete or import
  Pages/             TunnelList (main window), RouteList (tab in Settings) / TunnelEdit (own window),
                     ProfileList (tab in Settings) / ProfileEdit (own window),
                     Settings (tabs: Start, Verbindungen, SSH-Profile, Import / Export, Updates, Erweitert), About
  Setup/             SetupWindow (Windows): install / update / uninstall assistant (after the Matgate agent and the MatMon setup)
  Controls/          ONE code base per control: ListToolbar (search/filter/sort, two rows when narrow), ActionBar (buttons, Separated gap before delete, wraps when narrow),
                     FormField (label/hint/error by Field name), StatusBadge, HopChain, SettingToggle, InfoBar, PageHeader, IconLabel
  Themes/            Dark.axaml (colours, icon glyphs, ControlThemes, Fluent brushes), Controls.axaml (FormField template)
  Tray/              TrayIcon (Avalonia TrayIcon + NativeMenu), TrayIconRenderer (glyph + status dot), NotificationToast
  Dialogs/           ConfirmDialog (async), AskPassWindow (SSH_ASKPASS: answer goes to stdout straight to ssh)
  Ui.cs              resources by key (Brush/Icon/Theme), UI thread, OpenExternal (browser, Explorer/Finder)
  UpdateCoordinator  check timer, full auto update
```

Packages: Windows – no installer tool. The published single-file ThixTunnel.exe (`SINGLE_FILE` constant, win-* RIDs) is the setup
when started outside `%LocalAppData%\Programs\ThixTunnel`; CI ships it as `ThixTunnelSetup.exe`. An update keeps autostart and
shortcuts as they are. macOS – `ThixTunnel.app` (bundle id `info.schmoldt.thixtunnel`) in a dmg (drag into "Programme") and a
zip for the OTA update; signed ad hoc (no Apple Developer ID yet, Gatekeeper asks once).
Development builds are never a setup; a run with its own `THIXTUNNEL_DATA_DIR` is its own instance and never meets the installed one.

## Conventions

- **Microsoft C# coding conventions**, file-scoped namespaces, `_camelCase` private fields, human-readable names, no needless nesting
  (early returns).
- **Business layer = static classes** in `ThixTunnel.Core.Business`. UI never touches `DataStore`, files or the registry directly.
  Validation and permission checks live in the business layer; errors are `BusinessException(message, field)` and the UI shows them
  at the `FormField` with that `Field` (`PageActions.Run`).
- Values that end up in an ssh config are validated as tokens (no spaces, quotes, `#`, `,`, `%`, no leading `-`).
- Automatic attempts (`interactive: false`) use `BatchMode yes` + `StrictHostKeyChecking yes`; only manual connects may prompt.
  Authentication and host key problems are never retried.
- **Platforms:** one code base. Platform differences live in Core behind `OperatingSystem.IsWindows()/IsMacOS()` (AppPaths,
  AutostartService, AccessPolicy, ProcessJob, SshExecutable, UpdateService/MacAppBundle) or in Windows-only types
  (`InstallService`, `SetupWindow`). UI texts are platform-neutral ("Bei der Anmeldung starten", "ssh", "Infobereich / Menüleiste").
- **UI rules:** list pages have the toolbar (search, filter, sort) above the list (exception: the main connection list) and the action bar below, left-aligned, positive to
  negative, delete separated. Edit pages: tabs for areas, buttons `Speichern`, `Zurück`, gap, `Löschen`. Hide fields that do not apply
  (key file only for the key file method). Tiles always keep their margin. Icons from `Dark.axaml` (`Icon*` keys, the bundled
  Fluent UI System Icons font, MIT – never a system icon font, macOS has none). The about page opens from the version number bottom left.
- **Avalonia:** named looks are ControlThemes (`Theme="{StaticResource PrimaryButton}"`, list rows `ItemContainerTheme`), no
  implicit TextBlock theme – `ThemedWindow` sets font/foreground and text inherits it. `IsVisible` instead of Visibility,
  `ToolTip.Tip`, dialogs are async (`ConfirmDialog.AskAsync`), clipboard and file pickers through `TopLevel`. Icon-only buttons
  and tab items get `AutomationProperties.Name`.
- Several instances of a control on one page must work (no static state in controls).

## Versioning, branches, CI

- `Directory.Build.props` holds major/minor. CI sets `Channel`, `BuildNumber` (GitHub run number, always increasing), `BuildDate`:
  release `0.1.42-20261104` (main), nightly `nightly-43-20261104` (dev), local `local-20261104` (everything else).
- Branches: `main` = release, `dev` = nightly. Commits are authored by Matthias Schmoldt only – no co-author trailers, no other names.
- `.github/workflows/build.yml`: job `version` (one version for the run), `windows` (windows-latest: build, test without E2E, publish
  win-x64 single file = ThixTunnelSetup.exe), `macos` (macos-latest: build, test, publish osx-arm64 + osx-x64,
  `scripts/package-macos.sh` → ThixTunnel-macOS-<arch>.dmg/.zip), `publish` (manifest `thixtunnel-update.json`: version, channel,
  buildNumber, setup + sha256 for Windows, `packages` per platform). Push to main/dev uploads the packages, then the manifest, to the
  rolling release `release-channel` / `nightly-channel`; main also creates `v<version>` with setup and both dmgs.
