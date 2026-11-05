# Datei-Aenderungen ServiceRuntime V0001

## Neu angelegt

- `TKBase_RuntimeHost.cs`
- `Wichtig/DOKU/TK_Server_ServiceRuntime_Umbau_V0001.md`
- `Wichtig/DOKU/TK_Server_Datei_Aenderungen_ServiceRuntime_V0001.md`
- `Wichtig/SERVICE/Install-TK-Server-Service.ps1`
- `Wichtig/SERVICE/Uninstall-TK-Server-Service.ps1`

## Geaendert

- `TKSTART.cs`
  - erkennt `--service` und `/service`
  - startet im Servicebetrieb `TKWindowsService`
  - GUI-Start bleibt unveraendert fuer normalen Start ohne Parameter

- `TK-Server.csproj`
  - PackageReference fuer `System.ServiceProcess.ServiceController` hinzugefuegt

## Fachliche Wirkung

- Der TK-Server kann als Windows-Service laufen.
- Beim Service-Start werden SIP, SipExt-Providerregistrierung, GSM und SVC-Bereitschaft zentral gestartet.
- Beim Service-Stop werden Providerregistrierung, GSM und SIP/SVC sauber gestoppt.
