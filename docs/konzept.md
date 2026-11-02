# ThixTunnel – Anwendungskonzept

**Status: Konzeptphase.** Dieses Dokument beschreibt das geplante Verhalten von ThixTunnel. Es ist keine Aussage über bereits implementierte Funktionen oder einen verfügbaren Installer.

[Projektübersicht](../README.md) · [Mockup-Galerie](mockups/README.md)

## 1. Ziel und Anwendungsfall

ThixTunnel soll eine Windows-Anwendung zur Verwaltung von SSH-Profilen und lokalen TCP-Portweiterleitungen werden. Wiederkehrende Verbindungen werden als benannte Tunnelrouten gespeichert und bei Bedarf mit einem Klick aufgebaut. Eine Route kann über mehrere SSH-Server führen, etwa über einen öffentlich erreichbaren Sprungserver zu einem internen Server.

Die Anwendung soll im Benutzerkonto laufen, über ein Tray-Icon erreichbar sein und ausgewählte Routen nach der Windows-Anmeldung automatisch verbinden können. Ein Windows-Installer soll die Einrichtung und spätere Deinstallation übernehmen. Ein dauerhaft laufender Windows-Dienst ist für diesen Anwendungsfall keine Voraussetzung.

## 2. SSH-Profile und Tunnelrouten

Ein **SSH-Profil** beschreibt die Anmeldung an genau einem SSH-Server: Anzeigename, Hostname oder IP-Adresse, SSH-Port, Benutzername und Authentifizierung, beispielsweise die Referenz auf einen SSH-Schlüssel. Dasselbe Profil darf in mehreren Routen verwendet werden.

Eine **Tunnelroute** enthält einen eigenen Namen, eine geordnete Liste von SSH-Profilen und die Portweiterleitung. Dazu gehören lokale Bind-Adresse und lokaler Port sowie Zielhost und Zielport. Alle Profile vor dem letzten dienen als Sprungserver; das letzte Profil bezeichnet den abschließenden SSH-Server. Der Zielhost wird aus dessen Sicht erreicht und kann auch ein weiterer Rechner in dessen Netzwerk sein.

Auto-Verbindung und Wiederverbindung werden an der vollständigen Route gespeichert. So kann ein gemeinsam verwendetes Profil Teil sowohl manuell als auch automatisch gestarteter Routen sein.

## 3. Beispiel einer Verbindung

Die Beispielroute führt vom lokalen Anschluss **127.0.0.1:4455** über **Profil 1** und **Profil 2** zum Dienst **127.0.0.1:4453 auf Profil 2**. Die beiden Portnummern sind unabhängig von den SSH-Ports der Server.

Beispielhafte Host-Aliase in einer OpenSSH-Konfiguration:

```sshconfig
Host profil1
    HostName gateway.example.com
    User tunneluser
    Port 22

Host profil2
    HostName 10.0.0.24
    User tunneluser
    Port 22
```

Der passende Befehl lautet:

```powershell
ssh -N -J profil1 -L 127.0.0.1:4455:127.0.0.1:4453 profil2
```

`-J` verwendet Profil 1 als Sprungserver, `profil2` ist das letzte SSH-Ziel und `-N` verhindert den Start einer entfernten Shell. Bei `-L` bezeichnet das erste `127.0.0.1` den Windows-PC und das zweite den Server aus Profil 2. Voraussetzung sind Erreichbarkeit zwischen den Hops, erfolgreiche Authentifizierung und erlaubte TCP-Weiterleitungen.

Grundlagen: [OpenSSH ssh](https://man.openbsd.org/ssh) und [OpenSSH ssh_config](https://man.openbsd.org/ssh_config).

## 4. Geplante Hauptansichten

- **Tunnel:** Gespeicherte Routen, Verbindungsstatus, lokale Anschlüsse und Aktionen zum Verbinden, Trennen und Bearbeiten. Eine Detailansicht zeigt die vollständige Route und das Weiterleitungsziel.
- **SSH-Profile:** Profile anlegen und bearbeiten; Server-, Benutzer- und Authentifizierungsangaben pflegen.
- **Tunnelroute bearbeiten:** Profile auswählen, ihre Reihenfolge ändern, Hops ergänzen und lokale sowie entfernte Endpunkte festlegen.
- **Einstellungen:** Autostart, Start im Tray, Verhalten beim Schließen und Benachrichtigungen konfigurieren.

Die [vier Mockups](mockups/README.md) zeigen Tunnelübersicht, Routen-Editor, Einstellungen mit Tray-Menü und Installer. Sie entstanden unter dem früheren Arbeitstitel **ThixHop**, der in den Bildern noch sichtbar ist. Der Projektname lautet **ThixTunnel**. Die Bilder sind frühe visuelle Entwürfe mit Beispieldaten.

## 5. Autostart und automatische Verbindungen

| Ebene | Einstellung | Geplante Wirkung |
|---|---|---|
| Anwendung | Bei Windows-Anmeldung starten | Startet ThixTunnel nach der Anmeldung des Benutzerkontos. |
| Anwendung | Im Tray starten | Startet ohne geöffnetes Hauptfenster. |
| Anwendung | Beim Schließen im Tray weiterlaufen | Blendet beim Fenster-X nur das Hauptfenster aus. |
| Tunnelroute | Beim App-Start automatisch verbinden | Baut diese Route beim Start der Anwendung auf. |
| Tunnelroute | Nach Verbindungsabbruch erneut verbinden | Stellt eine unbeabsichtigt unterbrochene Verbindung wieder her. |

Die Option „Beim App-Start automatisch verbinden“ gilt auch bei einem manuellen Programmstart. Zusammen mit dem Windows-Autostart erfüllt sie den gewünschten Ablauf nach der Anmeldung. Eine zusätzliche Startverzögerung kann später ergänzt werden.

## 6. Tray und Verbindungslebenszyklus

Das Tray-Icon soll aktive Verbindungen und Fehler erkennen lassen. Der Status wird auch als Text angezeigt. Das Kontextmenü bietet „Öffnen“, die einzelnen Tunnel mit Verbinden/Trennen, „Automatische Verbindungen pausieren“, „Einstellungen“ und „Beenden“.

Bei aktivierter Tray-Option lässt das Fenster-X die Tunnel weiterlaufen. **„Beenden“ stoppt alle von ThixTunnel verwalteten Tunnel und beendet die Anwendung.** Ein zweiter Programmstart soll die vorhandene Instanz öffnen.

**Manuelles Trennen beendet auch laufende Wiederholungsversuche.** Die Route bleibt für die laufende Sitzung getrennt, bis der Nutzer sie erneut verbindet. Nach einem späteren App-Neustart gilt ihre gespeicherte Auto-Verbindungsoption wieder.

Fehlt nach der Anmeldung das Netzwerk, soll die App mit nachvollziehbarem Status warten und Versuche in zunehmenden Abständen wiederholen. Nach Standby oder Netzwerkwechsel dürfen zuvor gewünschte aktive Routen wiederhergestellt werden. Endlose Dialogfolgen sind dabei zu vermeiden.

Automatische Verbindungen setzen für alle Hops verfügbare Anmeldemittel voraus. Schlüssel über einen SSH-Agenten sind dafür ein vorgesehener Ansatz. Benötigte Passworteingaben oder Hostschlüsselbestätigungen führen zu einem sichtbaren Handlungsbedarf; geänderte Hostschlüssel werden nicht automatisch akzeptiert. Geheimnisse gehören nicht in Protokolle oder ungeschützte Profilexporte. Ein laufender SSH-Prozess allein bestätigt außerdem noch nicht die Erreichbarkeit des Zieldienstes.

## 7. Installer, Updates und Daten

Geplant ist eine Setup-EXE mit Startmenüeintrag, optionaler Desktop-Verknüpfung, optionalem Autostart und Deinstallation über Windows. Eine Installation für das eigene Benutzerkonto soll geprüft werden. Die Einrichtung muss die Verfügbarkeit von OpenSSH berücksichtigen.

Programmdateien und Benutzerdaten werden getrennt behandelt. Updates sollen Profile, Routen und Einstellungen erhalten. Die Deinstallation entfernt den Autostarteintrag; das Löschen gespeicherter Benutzerdaten erfordert eine eigene bewusste Auswahl. Ein automatischer Update-Dienst ist damit noch nicht festgelegt.

## 8. Matmon als konkrete Referenz

[Matmon](https://github.com/Real-TTX/Matmon) ist die Referenz für den Windows-Installationsassistenten und die Hintergrundbedienung über ein Tray-Icon. Die geprüfte Umsetzung verwendet ein **eigenes .NET-/WinForms-Setup**, das zugleich Tray-Programm und Uninstaller ist. Belege am geprüften Commit:

- [Matmon.Setup.csproj](https://github.com/Real-TTX/Matmon/blob/f29c3adbb3edcc93dd4b1c967712de491dd16a92/src/Matmon.Setup/Matmon.Setup.csproj): WinForms, .NET 10 und Veröffentlichung als selbstständige Setup-Datei.
- [TrayApp.cs](https://github.com/Real-TTX/Matmon/blob/f29c3adbb3edcc93dd4b1c967712de491dd16a92/src/Matmon.Setup/TrayApp.cs): `NotifyIcon`, Kontextmenü, Statusanzeige und Schutz vor doppelten Tray-Instanzen.
- [WindowsInstallation.cs](https://github.com/Real-TTX/Matmon/blob/f29c3adbb3edcc93dd4b1c967712de491dd16a92/src/Matmon.Host/Services/SelfUpdate/WindowsInstallation.cs): Installation, Deinstallation und optionaler Tray-Start bei Anmeldung.

Matmon betreibt seine Hauptanwendung als Windows-Dienst. ThixTunnel übernimmt zunächst das Bedienkonzept für eine Anwendung im Benutzerkonto. Ob Teile des Setups wiederverwendet werden, ist offen. Inno Setup war ein früher Vorschlag für ThixTunnel und ist keine belegte Matmon-Komponente.

## 9. Technischer Vorschlag und erster Umfang

**C#/.NET und OpenSSH über `ssh.exe`** sind ein Umsetzungsvorschlag. UI-Framework, Installer und mögliche Wiederverwendung aus Matmon werden vor Implementierungsbeginn festgelegt. Die App würde SSH-Konfiguration und Prozesse verwalten, einschließlich Zuständen, Fehlermeldungen und Aufräumen beim Beenden.

Der erste nutzbare Umfang umfasst lokale SSH-Profile, gespeicherte Multi-Hop-Routen mit jeweils einer TCP-Portweiterleitung, manuelles Verbinden/Trennen, Tray-Bedienung, Autostart, Auto-Verbindung, Wiederverbindung und einen Installer. Weitere Tunnelarten, Cloud-Synchronisierung und automatische Updates sind vorerst keine MVP-Voraussetzung.

### Abnahmekriterien für den MVP

1. Profile und Routen bleiben nach einem Neustart erhalten und lassen sich bearbeiten.
2. Eine Route mit zwei SSH-Profilen leitet den lokalen Port 4455 korrekt zum Zielport 4453 weiter.
3. Status und verständliche Fehler sind sowohl im Hauptfenster als auch über das Tray erreichbar.
4. Autostart der App und Auto-Verbindung jeder Route sind unabhängig einstellbar.
5. Fenster-X und „Beenden“ verhalten sich entsprechend der Tray-Einstellung; Beenden hinterlässt keine verwalteten Tunnel.
6. Manuelles Trennen stoppt Wiederholungen; unbeabsichtigte Abbrüche erlauben die aktivierte Wiederverbindung.
7. Fehlende Authentifizierung und Hostschlüsselprobleme werden sichtbar behandelt, ohne sie automatisch zu umgehen.
8. Installation, Aktualisierung und Deinstallation funktionieren nachvollziehbar; Updates erhalten die gespeicherte Konfiguration.
