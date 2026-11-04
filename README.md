<p align="center">
  <img src="branding/logo-full.svg" alt="ThixTunnel" width="440">
</p>

**SSH-Profile, mehrere Hops und lokale Portweiterleitungen als Windows-Tray-App.**

ThixTunnel bündelt wiederkehrende SSH-Tunnel in einer kleinen Windows-Anwendung. Ein **SSH-Profil** beschreibt die Anmeldung an einem Server; eine **Verbindung** reiht Profile in fester Reihenfolge auf (Sprungserver → … → Zielserver) und macht einen entfernten Dienst über einen lokalen Port erreichbar. Unter der Haube arbeitet der Windows-OpenSSH-Client – im Kern `ssh -N -J … -L …`.

**Projektstatus: Version 0.1.** Profile, Verbindungen über mehrere Hops, Verbinden/Trennen mit Status, Tray, Autostart, Auto-Verbindung, Wiederverbindung, Schlüssel-Einrichtung, Import/Export, eigenes Setup und Updates sind umgesetzt. Grundlage sind das [Anwendungskonzept](docs/konzept.md) und die [Mockups](docs/mockups/README.md).

<p align="center">
  <img src="docs/screenshots/01-tunnel.png" alt="Hauptfenster: schmale Liste der Verbindungen mit Statuspunkt und Fehlermeldung" width="300">
</p>

## Die Idee

**Lokaler Port 4455 → Server A → Server B → Ziel 127.0.0.1:4453.** A ist der Sprungserver, B der letzte SSH-Server; das Ziel wird aus Sicht von B angesprochen (`127.0.0.1` = B selbst). ThixTunnel startet dafür

```powershell
ssh -N -J A -L 127.0.0.1:4455:127.0.0.1:4453 B
```

hält den Prozess im Hintergrund am Leben und zeigt den Zustand im Fenster und im Tray. Jeder Hop meldet sich mit seinem eigenen Profil an; alle Schlüssel bleiben auf dem PC – auf A muss kein Schlüssel für B liegen.

## Schnellstart

1. `ThixTunnelSetup.exe` aus dem [Release-Kanal](https://github.com/Real-TTX/ThixTunnel/releases/tag/release-channel) laden und starten – installiert für das eigene Benutzerkonto, ohne Adminrechte.
2. Zahnrad unten rechts → **Einstellungen › SSH-Profile**: je Server ein Profil (Adresse, Port, Benutzer, Anmeldung).
3. **Einstellungen › Verbindungen › Neue Verbindung**: Hops in Reihenfolge wählen, lokalen Port und Ziel eintragen, speichern.
4. Im Hauptfenster auf ▷ klicken (oder doppelklicken). Beim ersten Mal fragt ThixTunnel unbekannte Hostschlüssel und ggf. Passwörter ab.

Bestehende Verbindungen eines anderen PCs übernimmst du über **Einstellungen › Import / Export**.

## Funktionen

| Bereich | Verhalten |
| --- | --- |
| Hauptfenster | Schmal und hoch wie eine Kontaktliste: nur die Verbindungen mit Statuspunkt, Adressen bzw. Fehlermeldung und Knopf zum Verbinden/Trennen (auch Doppelklick oder Enter). Unten links die Version (öffnet „Über“), unten rechts die Einstellungen. Verwaltet wird nur in den Einstellungen. |
| Verbindungen | Einstellungen › „Verbindungen“: Suche, Filter, Sortierung; anlegen, bearbeiten (eigenes Fenster mit den Reitern Route, Portweiterleitung, Automatisierung), ssh-Befehl kopieren, löschen. Bis zu 10 Hops je Verbindung, je eine lokale TCP-Portweiterleitung. |
| SSH-Profile | Einstellungen › „SSH-Profile“: Server, SSH-Port, Benutzer und Anmeldung – SSH-Agent, bestimmte Schlüsseldatei oder Passwort. Ein Profil kann in vielen Verbindungen stecken. |
| Anmeldung | Passwörter, Passphrasen und unbekannte Hostschlüssel werden nur beim **manuellen** Verbinden per Dialog abgefragt (SSH_ASKPASS) und nie gespeichert. Geänderte Hostschlüssel werden immer abgelehnt. |
| Schlüssel einrichten | Passwort-Profil mit „Beim ersten Anmelden einen Schlüssel anlegen und hinterlegen“: beim nächsten manuellen Verbinden einmal das Passwort, dann erzeugt ThixTunnel einen eigenen ed25519-Schlüssel (`%AppData%\ThixTunnel\keys`), trägt ihn – auch durch Sprungserver hindurch – in `~/.ssh/authorized_keys` ein und stellt das Profil auf diesen Schlüssel um. Danach verbindet es ohne Passwort, auch automatisch. Für Linux-/Unix-Server. |
| Import / Export | Einstellungen › „Import / Export“: alle Profile und Verbindungen als Datei (`*.thixtunnel.json`) – für Umzug, Teilen oder Sicherung. Ohne Passwörter, Schlüsseldateien nur als Pfad. Vorhandene Einträge bleiben oder werden auf Wunsch ersetzt; was nicht passt (z. B. belegter Port), wird benannt. |
| Status | Je Verbindung: Getrennt, Verbindet, Verbunden, Wartet, Fehler – mit verständlicher Meldung (z. B. „Lokaler Port 4455 ist bereits belegt“). |
| Automatik | Je Verbindung: beim App-Start verbinden und nach Abbruch erneut verbinden (wachsende Pausen, sofortiger Versuch bei Netzwerkwechsel und nach Standby). Automatische Versuche fragen nie nach; Anmelde- und Hostschlüsselfehler werden nicht wiederholt. |
| Tray | Statuspunkt im Symbol, Tooltip, Menü mit allen Verbindungen (verbinden/trennen), „Automatik pausieren“, Einstellungen, Beenden. |
| Fenster | Jedes Fenster passt auch auf kleine Bildschirme. Fenster-X lässt die Tunnel im Tray weiterlaufen (einstellbar); „Beenden“ trennt alle. Ein zweiter Programmstart öffnet das vorhandene Fenster. |
| Aufräumen | Alle ssh-Prozesse hängen an einem Windows-Job-Objekt – auch nach einem Absturz bleibt kein Tunnel zurück. |
| Autostart | „Bei Windows-Anmeldung starten“ (HKCU\…\Run) und „Im Tray starten“ – unabhängig von der Auto-Verbindung je Verbindung. |
| Setup | Die Programmdatei ist ihr eigenes Setup (wie beim Matgate-Agent): Installation für das eigene Benutzerkonto ohne Adminrechte, Startmenü, optionale Desktop-Verknüpfung, optionaler Autostart, Eintrag unter „Installierte Apps“; Deinstallation mit Rückfrage zu den Benutzerdaten. |
| Updates (OTA) | Prüfung beim Start und alle 6 Stunden, Installation mit einem Klick. Optional **voll-automatisch**: installiert selbst, startet neu und verbindet vorher aktive Tunnel wieder. |
| Richtlinien | Administratoren können unter `HKLM\SOFTWARE\Policies\ThixTunnel` (DWORD = 1) sperren: `LockConfiguration`, `DenyNetworkBinding` (nur Loopback), `DisableUpdates`. Geprüft wird im Business-Layer. |

| Verbindung bearbeiten (eigenes Fenster) | Einstellungen › Verbindungen |
| --- | --- |
| ![Fenster einer Verbindung mit zwei SSH-Hops](docs/screenshots/02-route-bearbeiten.png) | ![Einstellungen mit den Reitern Verbindungen, SSH-Profile und Import / Export](docs/screenshots/03-einstellungen.png) |

| Profil: Schlüssel beim ersten Anmelden | Setup |
| --- | --- |
| ![Passwort-Profil mit der Option, beim ersten Anmelden einen Schlüssel anzulegen](docs/screenshots/04-schluessel-einrichten.png) | ![Setup-Assistent mit Autostart und Desktop-Verknüpfung](docs/screenshots/05-setup.png) |

## Installation und Updates

Jeder Build wird in GitHub Actions gebaut und als Setup veröffentlicht:

| Kanal | Branch | Version | Download |
| --- | --- | --- | --- |
| Release | `main` | `<major>.<minor>.<build>-<datum>`, z. B. `0.1.42-20261104` | [release-channel](https://github.com/Real-TTX/ThixTunnel/releases/tag/release-channel) und je Version `v<version>` |
| Nightly | `dev` | `nightly-<build>-<datum>`, z. B. `nightly-43-20261104` | [nightly-channel](https://github.com/Real-TTX/ThixTunnel/releases/tag/nightly-channel) (Vorabversion) |
| Lokal | jeder andere Build | `local-<datum>` | – (Workflow-Artefakt bei Pull Requests) |

`ThixTunnelSetup.exe` ist das Programm selbst: aus dem Download gestartet, zeigt es den Setup-Assistenten und installiert sich nach `%LocalAppData%\Programs\ThixTunnel`. Ist ThixTunnel schon installiert, wird daraus ein Update (Autostart und Verknüpfungen bleiben, wie sie sind). Mit `--update` läuft es still als Update, mit `--uninstall` als Deinstallation (so steht es unter „Installierte Apps“). Eine installierte Version prüft nur ihren eigenen Kanal (`thixtunnel-update.json` im Kanal-Release, mit SHA-256 des Setups). Benutzerdaten liegen getrennt unter `%AppData%\ThixTunnel` und bleiben bei Updates erhalten.

Voraussetzung ist der **Windows-OpenSSH-Client** (`C:\Windows\System32\OpenSSH\ssh.exe`, optionales Windows-Feature). Auf Sprungservern muss TCP-Weiterleitung erlaubt sein (`AllowTcpForwarding`, meist Standard).

## Entwicklung

```powershell
dotnet build ThixTunnel.slnx                         # 0 Warnungen, 0 Fehler
dotnet test ThixTunnel.slnx                          # Unit- und Business-Layer-Tests
powershell -File scripts\e2e-docker.ps1              # echter 2-Hop-Tunnel und Schlüssel-Einrichtung gegen Docker-SSH-Server
powershell -File scripts\build-icons.ps1             # Icon (.ico) und PNGs aus der Logo-Geometrie
$env:THIXTUNNEL_DATA_DIR = "C:\temp\thixtunnel-demo" # App mit eigenem Datenordner starten (eigene Instanz neben der installierten)
```

Ein Debug-Build ist nie ein Setup; nur die veröffentlichte Einzeldatei (`dotnet publish -r win-x64`) installiert sich selbst.

| Pfad | Inhalt |
| --- | --- |
| `src/ThixTunnel.Core` | Modelle, Business-Layer (statische Klassen, Berechtigungsprüfung in jeder Änderung: Profile, Verbindungen, Tunnel, Import/Export, Schlüssel-Einrichtung, Setup, Updates), Infrastruktur (JSON-Speicher, ssh-Konfiguration, Prozesse, Job-Objekt) |
| `src/ThixTunnel` | WPF-Oberfläche (Verbindungsliste, Einstellungen, eigene Fenster), Setup-Assistent, Tray, SSH_ASKPASS-Dialog, Update-Steuerung, wiederverwendbare Controls |
| `tests/ThixTunnel.Tests` | xUnit-Tests; `EndToEndTests` laufen nur über `scripts/e2e-docker.ps1` (`tests/e2e/Dockerfile`) |
| `branding/` | Logo in Detailstufen: voll (`logo-full.svg`), App-Icon (`logo-icon.svg`), klein/Favicon (`logo-small.svg`), Tray-Glyph (`logo-tray.svg`) |
| `.github/workflows/build.yml` | Build, Test, Setup-Datei, Update-Manifest und Veröffentlichung je Kanal |

Branches: `main` (Release) und `dev` (Nightly). Entwicklungsregeln stehen in [CLAUDE.md](CLAUDE.md).

## Orientierung an Matmon und Matgate

[Matmon](https://github.com/Real-TTX/Matmon) war die Referenz für Tray-Bedienung, Installationsablauf und das Update-Prinzip über rollierende GitHub-Releases je Kanal. ThixTunnel läuft im Benutzerkonto statt als Windows-Dienst; wie beim [Matgate](https://github.com/Real-TTX/Matgate)-Agent ist die Programmdatei selbst das Setup – ohne fremdes Installer-Werkzeug.
