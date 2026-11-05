# Datei-Aenderungen NoServiceProcess Fix V0001

## Geaendert

- `TKBase_RuntimeHost.cs`
  - `System.ServiceProcess` entfernt.
  - `TKWindowsService : ServiceBase` entfernt.
  - RuntimeHost bleibt als Start-/Stop-Orchestrierung erhalten.

- `TKSTART.cs`
  - `ServiceBase.Run(...)` entfernt.
  - Runtime-Modus ueber `--runtime` / `--service` eingebaut.
  - Runtime-Prozess schreibt PID-Datei und wartet auf Stop-Datei.

- `TK_Server.cs`
  - `ServiceController` entfernt.
  - Buttons starten/stoppen jetzt den separaten Runtime-Prozess.
  - GUI schliesst die Runtime nicht.

- `TK-Server.csproj`
  - PackageReference `System.ServiceProcess.ServiceController` entfernt.

## Nicht geaendert

- Keine neue `.cs`-Datei.
- Keine SQL-Aenderung.
- Keine neue DB-Verbindung.
- Kein neuer Logger.
