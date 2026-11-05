# Dateiänderungen WindowsService GUI V0001

## Neu

- `TKBase_RuntimeHost.cs`
- `Wichtig/SERVICE/Install-TK-Server-Service.ps1`
- `Wichtig/SERVICE/Uninstall-TK-Server-Service.ps1`
- `Wichtig/DOKU/TK_Server_ServiceRuntime_WindowsService_GUI_V0001.md`
- `Wichtig/DOKU/TK_Server_Datei_Aenderungen_WindowsService_GUI_V0001.md`

## Geändert

- `TKSTART.cs`
  - Service-Modus über `--service` oder `/service` ergänzt.
  - GUI-Start ohne Parameter bleibt unverändert.
  - GUI-Start bereinigt keine volatile Runtime-Daten mehr; das macht nur der Service-Prozess.

- `TK_Server.cs`
  - Service Start/Stop steuert den Windows-Service `TK-Server`.
  - GUI-Schließen stoppt den Dienst nicht mehr.
  - Statusleiste zeigt Windows-Service-Zustand.

- `TK-Server.csproj`
  - `System.ServiceProcess.ServiceController` ergänzt.
