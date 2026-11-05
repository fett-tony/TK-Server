# TK-Server ServiceRuntime Umbau V0001

## Ziel

Der TK-Server kann jetzt ohne WinForms-Oberflaeche als Windows-Service gestartet werden.

Der Service-Einstieg startet zentral:

- SIP Runtime ueber `TKBase_Server` / `TK_GuiCommand`
- SipExt Provider-Registrierung ueber `TKBase_RegistrationManager`
- GSM Runtime ueber `TKBase_GsmGateway`
- SVC bleibt als Leg-D-Servicebereich ueber den SIP-Server und das bestehende Service-Routing erreichbar

## Neue zentrale Runtime

Neu angelegt:

`TKBase_RuntimeHost.cs`

Aufgabe:

- `StartAll()` startet die komplette Server-Runtime.
- `StopAll()` stoppt ProviderRegistration, GSM und SIP/SVC sauber in Rueckwaertsreihenfolge.
- Volatile Runtime-Daten werden beim Start und Stop ueber `TKSQL_Commands.ResetVolatileRuntimeState()` bereinigt.
- Logging laeuft weiter ausschliesslich ueber `TKBase_LogManager`.
- Im Servicebetrieb wird ohne RichTextBox in `log/TK-Server.Service.log` geschrieben.

## Windows-Service-Einstieg

`TKSTART.cs` wurde erweitert.

Wenn die EXE mit `--service` oder `/service` gestartet wird, wird keine GUI geoeffnet. Stattdessen wird `ServiceBase.Run(new TKWindowsService())` verwendet.

Ohne Serviceparameter startet weiterhin die bisherige WinForms-GUI mit Splashscreen.

## Service-Startreihenfolge

```text
TKWindowsService.OnStart
 -> TKBase_RuntimeHost.StartAll
    -> ResetVolatileRuntimeState(Start)
    -> StartSipRuntime
    -> StartProviderRuntime
    -> StartGsmRuntime
```

## Service-Stoppreihenfolge

```text
TKWindowsService.OnStop / OnShutdown
 -> TKBase_RuntimeHost.StopAll
    -> StopProviderRuntime
    -> StopGsmRuntime
    -> StopSipRuntime
    -> ResetVolatileRuntimeState(Stop)
```

## Installation

Nach dem Build kann der Dienst mit dem Script unter `Wichtig/SERVICE/Install-TK-Server-Service.ps1` registriert werden.

Der Dienst wird mit folgendem Startparameter registriert:

```text
--service
```

Damit bleibt dieselbe EXE fuer GUI und Service nutzbar.

## Nicht geaendert

- Keine SIP-Leg-Logik wurde verschoben.
- Keine GSM-AT-Logik wurde verschoben.
- Keine SVC-Menue- oder Recording-Logik wurde geaendert.
- Keine SQL-Statements wurden ausserhalb von `TKSQL_Commands.cs` angelegt.
- Kein neuer Logger wurde angelegt.
