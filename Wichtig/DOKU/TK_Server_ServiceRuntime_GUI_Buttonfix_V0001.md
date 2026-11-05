# TK-Server ServiceRuntime GUI Buttonfix V0001

## Ziel

Der normale GUI-Start wurde wieder auf den ursprünglichen Startpfad zurückgesetzt.

Es gibt keine automatische Service-Erkennung, keinen Windows-Service-Einstieg und keinen RuntimeHost beim Programmstart.

## Umsetzung

In `TK_Server` wurden zwei Buttons im linken Menü ergänzt:

- `Service Start`
- `Service Stop`

`Service Start` startet aus der GUI heraus:

- `TK_GuiCommand.Start(_logger)` für SIP/SipInt/SipExt-Basis
- `TKBase_RegistrationManager.Start()` für ProviderRegistration/SipExt
- `TKBase_GsmGateway.Start()` für GSM

SVC wird nicht separat gestartet, sondern bleibt wie bisher über SIP und ServiceLeg/Routing erreichbar.

`Service Stop` stoppt in umgekehrter Reihenfolge:

- GSM
- ProviderRegistration/SipExt
- SIP

## Wichtig

Es wurde keine neue `.cs`-Datei angelegt.

`TKSTART.cs` und `TK-Server.csproj` bleiben aus dem funktionierenden Basisstand.

