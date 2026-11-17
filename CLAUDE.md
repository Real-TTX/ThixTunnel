# CLAUDE.md

Guidance for working in this repository. Keep it in sync with the code: update it whenever architecture, interfaces or conventions change.

## What ThixTunnel is

A tray / menu bar application for **Windows and macOS** around `ssh -N -J … -L …`: SSH profiles (one server each), tunnel routes
(ordered hops + one local TCP forwarding), connect/disconnect with status, automatic connection and reconnection, tray, autostart,
setup (Windows) / app bundle (macOS), OTA updates. Concept and acceptance criteria: `docs/konzept.md`. UI language: German.
Code, comments and identifiers: English.

Stack: **.NET 10** (`net10.0`, one code base), C#, **Avalonia 11.3** with **MatDesktop 0.2** (the desktop toolkit of MatDesign,
copied into `src/ThixTunnel/MatDesktop/`: Fluent underneath, MatUi's tokens, light and dark, the "Apple-like" flat look), xUnit,
the system OpenSSH (Windows OpenSSH `ssh.exe`, macOS `/usr/bin/ssh`). The desktop app follows MatDesign's rules DESK, TRAY and SETUP
(`docs/DESKTOP.md` in MatDesign; MatDesktop and its README are on MatDesign's branch `feature/desktop-and-services`).

## Build / run / test

- `dotnet build ThixTunnel.slnx` – expected: 0 warnings, 0 errors (CA1416: Windows-only code is guarded by
  `OperatingSystem.IsWindows()` or marked `[SupportedOSPlatform("windows")]`).
- `dotnet test ThixTunnel.slnx` – business layer tests and the **UI rendered headless** (`UiTests`, Avalonia.Headless + Skia: the
  real pages driven like a user would, the snapshot of every window). Each test gets its own data folder and LaunchAgents folder
  (`TestData`); UI tests also `SandboxScope` (no policy, autostart entry in memory) and `SampleData`. `[WindowsFact]` / `[MacFact]` run
  only on their system; CI runs the tests on both.
- **Snapshots:** `ThixTunnel --snapshot docs/images` draws every window headless (light and dark): `setup-*` (Windows),
  `main-{connections,folded,route-edit,profile-edit,settings,routes,profiles,updates,about}`, `info`, `askpass-{password,hostkey}`,
  `toast`, `tray-icon-{windows,macos}` - from `SampleData` in a temporary data folder, version 1.0.0, never data of the machine.
  A new window or page: add it to `Snapshot.cs` and look at the pictures.
- `powershell -File scripts\e2e-docker.ps1` – real two-hop tunnel against Docker SSH servers (`tests/e2e/Dockerfile`), Windows;
  the user's `~/.ssh` is never touched (own key and known_hosts via `SshConfigWriter.KnownHostsFileForTests`).
- Run the app against sample data: set `THIXTUNNEL_DATA_DIR` to a folder (data, logs and generated ssh configs go there).
  Never let a test run write the real data folder, the `HKCU\…\Run` value or `~/Library/LaunchAgents`.
- Real UI checks: the snapshots and `UiTests` first; then start the app (own `THIXTUNNEL_DATA_DIR`), take screenshots and look at them. macOS cannot be checked from the Windows machine – CI builds and tests it, the owner tries it on a Mac.
- **MatDesktop is a COPY**: never edit `src/ThixTunnel/MatDesktop/` (`matdesktop.version` lists the SHA-256 of each file). Update it from
  MatDesign: `node tools/matdesktop-copy.js <path>/ThixTunnel/src/ThixTunnel` there. An icon the sprite lacks goes into MatDesign first.
- `scripts/build-icons.ps1` (`powershell -ExecutionPolicy Bypass -File …`) renders `src/ThixTunnel/Assets/thixtunnel.ico`, `logo-256.png`,
  the tray marks `tray-windows.png` (64 px, small design) and `tray-macos.png` (template image) and `branding/png/*` from the logo geometry;
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
                     macOS ~/Library/Application Support), AppInfo (version/channel from assembly metadata; Pin for snapshots), SshConfigWriter
                     (per-connection config; -F reaches the jump hosts, -o does not), SshCommandBuilder, SshProcess,
                     SshErrorClassifier, ProcessJob (Windows kill-on-close job object), NetworkProbe, SshExecutable, AskPassProtocol, Log,
                     SandboxScope (snapshots/UI tests: no policies, autostart entry in memory)
  Business/SampleData the sample profiles, connections and states of the snapshots and UI tests (own data folder only)
src/ThixTunnel  (Avalonia app on MatDesktop, the same program on Windows and macOS)
  Program.cs / App.axaml(.cs)  start-up: snapshot mode, askpass mode, setup mode (Windows: --uninstall / --update / single-file exe
                     outside the program folder), MatTheme with the accent #1A6FD1 and the appearance setting, MatSingleInstance per
                     data folder (Windows: exit signal for the setup), tray / menu bar, Info (TRAY-05), Dock "reopen", network events
  MainWindow         THE window (MatWindow): MatTitleBar (logo + word mark, the state of all tunnels and "Automatik pausieren" in the
                     middle), MatNav (Verbindungen; Einstellungen below the line; version = About at the foot; folds to a rail), the
                     page host with a back stack (OpenPage / GoBack, Esc). X / Esc hide (DESK-06). Strg/⌘+1, Strg/⌘+, Strg/⌘+B, Strg/⌘+F.
                     AppEvents.DataChanged: a save/delete/import of the UI redraws lists and the tray menu
  Pages/             TunnelList (main page: connect/disconnect only, toolbar above, no add/edit/delete - managing happens only in
                     the settings, by decision of the owner), Settings (tabs Allgemein | Verbindungen | SSH-Profile | Import / Export |
                     Updates | Erweitert; switches apply at once), RouteList / ProfileList (the tabs), TunnelEdit / ProfileEdit (pages
                     with tabs), About. All built in code from the controls below.
  Setup/             ThixTunnelSetup (Windows): MatSetupWindow for install / update / uninstall (SETUP-01 … SETUP-04)
  Controls/          ONE code base per control: ListToolbar (search, filter, sort + direction -> ListQuery), EntityList (+ EntityRow:
                     MatList + MatCardRow, state tint and word, trailing button, chevron), ListLayout / EditLayout (the list page and
                     the create/update page; EditLayout.Run shows a BusinessException at the MatFormField named by FormErrors.Field
                     and chooses its tab), ActionBar (Separated gap before delete, wraps), InfoBar, IconLabel, HopChain, FormErrors
  Tray/              TrayIcon (MatTray + MatTrayMenu in the order of TRAY-01, own items: every connection, Automatik pausieren;
                     MatTrayIcons draw the state dot; MatNotification only for what the user did not cause)
  Dialogs/           ConfirmDialog (async), AskPassWindow (SSH_ASKPASS in MatDialogChrome: answer goes to stdout straight to ssh)
  TunnelStates.cs    a tunnel's state in words / colour token, ThixTunnel's overall MatState and summary (list, title bar, tray, Info)
  Snapshot.cs        "--snapshot <folder>": every window headless, light and dark
  Ui.cs              logo/icon, UI thread, OpenExternal/OpenFolder, form helpers (Button, IconButton, Field, Row, Switch, Card, Page)
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
  at the `MatFormField` with that `FormErrors.Field` (`EditLayout.Run` / `ShowError`; lists: `ListLayout.Run` shows it above the list).
- Values that end up in an ssh config are validated as tokens (no spaces, quotes, `#`, `,`, `%`, no leading `-`).
- Automatic attempts (`interactive: false`) use `BatchMode yes` + `StrictHostKeyChecking yes`; only manual connects may prompt.
  Authentication and host key problems are never retried.
- **Platforms:** one code base. Platform differences live in Core behind `OperatingSystem.IsWindows()/IsMacOS()` (AppPaths,
  AutostartService, AccessPolicy, ProcessJob, SshExecutable, UpdateService/MacAppBundle) or in Windows-only types
  (`InstallService`, `ThixTunnelSetup`). UI texts are platform-neutral ("Bei der Anmeldung starten", "ssh", "Infobereich / Menüleiste").
- **UI rules (owner):** list pages have the toolbar (search, filter, sort) above the list and the action bar below, left-aligned,
  positive to negative, delete separated. Edit pages: tabs for areas, buttons `Speichern`, `Zurück`, gap, `Löschen`. Hide fields that do
  not apply (key file only for the key file method, key setup only for passwords). Cards keep their distance. The about page opens
  from the version at the foot of the menu. Every page is a page of the main window - no separate windows except dialogs, Info and the setup.
- **Look (MatDesign):** colours only from MatDesktop's theme (`{DynamicResource Mat.Surface}`, `control.Use(property, "Mat.TextMuted")`) -
  never a literal colour, no theme file of our own (DESK-02); light, dark or like the system (setting "Erscheinungsbild"). Icons only from
  MatUi's sprite (`MatIcon Icon="route"`, DESK-09) - no icon font. Named looks are MatDesktop's control themes (`Themed("MatButton.Primary")`,
  `MatCard`, `MatList` + `MatCardRow` / `MatChoiceRow`, `MatBadge.Soft`, `MatText.*`). Settings are `MatSettingRow`s, fields `MatFormField`s.
  The tray menu, Info, notifications and the setup are MatDesktop's (TRAY-01 … TRAY-05, SETUP-01 … SETUP-07). German only for now
  (DESK-08 is a Should).
- **Avalonia:** `IsVisible` instead of Visibility, `ToolTip.Tip`, dialogs are async (`ConfirmDialog.AskAsync`), clipboard and file
  pickers through `TopLevel`. Icon-only buttons (`Ui.IconButton`) and tab items get `AutomationProperties.Name` and a tool tip.
- Several instances of a control on one page must work (no static state in controls).

## Versioning, branches, CI

- `Directory.Build.props` holds major/minor. CI sets `Channel`, `BuildNumber` (GitHub run number, always increasing), `BuildDate`:
  release `0.2.42-20261117` (main), nightly `nightly-43-20261104` (dev), local `local-20261104` (everything else).
- Branches: `main` = release, `dev` = nightly. Commits are authored by Matthias Schmoldt only – no co-author trailers, no other names.
- `.github/workflows/build.yml`: job `version` (one version for the run), `windows` (windows-latest: build, test without E2E, publish
  win-x64 single file = ThixTunnelSetup.exe), `macos` (macos-latest: build, test, publish osx-arm64 + osx-x64,
  `scripts/package-macos.sh` → ThixTunnel-macOS-<arch>.dmg/.zip), `publish` (manifest `thixtunnel-update.json`: version, channel,
  buildNumber, setup + sha256 for Windows, `packages` per platform). Push to main/dev uploads the packages, then the manifest, to the
  rolling release `release-channel` / `nightly-channel`; main also creates `v<version>` with setup and both dmgs.
