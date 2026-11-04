# CLAUDE.md

Guidance for working in this repository. Keep it in sync with the code: update it whenever architecture, interfaces or conventions change.

## What ThixTunnel is

A Windows tray application around `ssh -N -J … -L …`: SSH profiles (one server each), tunnel routes (ordered hops + one local
TCP forwarding), connect/disconnect with status, automatic connection and reconnection, tray, autostart, installer, OTA updates.
Concept and acceptance criteria: `docs/konzept.md`. UI language: German. Code, comments and identifiers: English.

Stack: **.NET 10**, C#, **WPF** (+ WinForms only for `NotifyIcon`), xUnit, Inno Setup 6, Windows OpenSSH (`ssh.exe`).

## Build / run / test

- `dotnet build ThixTunnel.slnx` – expected: 0 warnings, 0 errors.
- `dotnet test ThixTunnel.slnx` – unit and business layer tests (each test gets its own data folder, `TestData`).
- `powershell -File scripts\e2e-docker.ps1` – real two-hop tunnel against Docker SSH servers (`tests/e2e/Dockerfile`);
  the user's `~/.ssh` is never touched (own key and known_hosts via `SshConfigWriter.KnownHostsFileForTests`).
- Run the app against sample data: set `THIXTUNNEL_DATA_DIR` to a folder (data, logs and generated ssh configs go there).
  Never let a test run write the real `%AppData%\ThixTunnel` or the `HKCU\…\Run` value.
- Real UI checks: start the app, drive it through UI Automation, take screenshots and look at them.
- `scripts/build-icons.ps1` renders `src/ThixTunnel/Assets/thixtunnel.ico` and `branding/png/*` from the logo geometry.

## Map

```
src/ThixTunnel.Core
  Models/            SshProfile, TunnelRoute, AppSettings, AppData (the JSON file), TunnelStatus, ListQuery, BusinessException
  Business/          THE business layer, static classes. Every change calls AccessPolicy first (permission check in the business layer):
                     AccessPolicy (HKLM policies LockConfiguration / DenyNetworkBinding / DisableUpdates), ProfileService, RouteService,
                     SettingsService, AutostartService (HKCU Run, "--autostart"), TunnelManager (sessions, states, retries, pause),
                     UpdateService (manifest, download, SHA-256, silent setup), SessionResume (reconnect after an update)
  Infrastructure/    DataStore (only the business layer uses it; atomic JSON writes), AppPaths, AppInfo (version/channel from
                     assembly metadata), SshConfigWriter (per-connection config; -F reaches the jump hosts, -o does not),
                     SshCommandBuilder, SshProcess, SshErrorClassifier, ProcessJob (kill-on-close job object), NetworkProbe,
                     SshExecutable, AskPassProtocol, Log
src/ThixTunnel
  App.xaml.cs        start-up: askpass mode, single instance (mutex + event), tray, autostart/tray arguments, network/power events
  MainWindow         side bar (navigation, status, version = link to the about page), page host; X hides when "keep running" is on
  Pages/             TunnelList / TunnelEdit, ProfileList / ProfileEdit (CRUD: list page + edit page with delete), Settings, About
  Controls/          ONE code base per control: ListToolbar (search/filter/sort), ActionBar (buttons, Separated gap before delete),
                     FormField (label/hint/error by Field name), StatusBadge, HopChain, SettingToggle, InfoBar, PageHeader, IconLabel
  Themes/            Dark.xaml (colours, icons, styles), Controls.xaml (FormField template)
  Tray/              TrayIcon (menu, notifications), TrayIconRenderer (glyph + status dot)
  Dialogs/           ConfirmDialog, AskPassWindow (SSH_ASKPASS: answer goes to stdout straight to ssh)
  UpdateCoordinator  check timer, full auto update
installer/ThixTunnel.iss   per-user setup; autostart task only on first install; uninstall removes autostart, asks about user data
```

## Conventions

- **Microsoft C# coding conventions**, file-scoped namespaces, `_camelCase` private fields, human-readable names, no needless nesting
  (early returns).
- **Business layer = static classes** in `ThixTunnel.Core.Business`. UI never touches `DataStore`, files or the registry directly.
  Validation and permission checks live in the business layer; errors are `BusinessException(message, field)` and the UI shows them
  at the `FormField` with that `Field` (`PageActions.Run`).
- Values that end up in an ssh config are validated as tokens (no spaces, quotes, `#`, `,`, `%`, no leading `-`).
- Automatic attempts (`interactive: false`) use `BatchMode yes` + `StrictHostKeyChecking yes`; only manual connects may prompt.
  Authentication and host key problems are never retried.
- **UI rules:** list pages have the toolbar (search, filter, sort) above the table and the action bar below, left-aligned, positive to
  negative, delete separated. Edit pages: tabs for areas, buttons `Speichern`, `Zurück`, gap, `Löschen`. Hide fields that do not apply
  (key file only for the key file method). Tiles always keep their margin. Icons from `Dark.xaml` (`Icon*` keys, Segoe Fluent Icons).
  The about page opens from the version number bottom left. No implicit `TextBlock` style – windows set font/foreground
  (`NativeWindow.ApplyTheme`) and text inherits it.
- Several instances of a control on one page must work (no static state in controls).

## Versioning, branches, CI

- `Directory.Build.props` holds major/minor. CI sets `Channel`, `BuildNumber` (GitHub run number, always increasing), `BuildDate`:
  release `0.1.42-20261104` (main), nightly `nightly-43-20261104` (dev), local `local-20261104` (everything else).
- Branches: `main` = release, `dev` = nightly. Commits are authored by Matthias Schmoldt only – no co-author trailers, no other names.
- `.github/workflows/build.yml` (windows-latest): build, test (without E2E), publish self-contained win-x64, Inno Setup,
  `thixtunnel-update.json` (version, channel, buildNumber, setup, sha256). Push to main/dev uploads setup, then manifest, to the rolling
  release `release-channel` / `nightly-channel`; main also creates `v<version>`.
