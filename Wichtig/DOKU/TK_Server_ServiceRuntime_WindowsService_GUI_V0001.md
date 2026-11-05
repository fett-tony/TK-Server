# TK-Server ServiceRuntime WindowsService GUI V0001

Ziel: Die GUI startet/stoppt nicht mehr die Runtime im eigenen Prozess, sondern steuert den Windows-Service `TK-Server`.

Damit bleibt der TK-Server weiter aktiv, wenn die GUI geschlossen wird.

## Verhalten

- Normaler GUI-Start ohne Parameter bleibt erhalten.
- Windows-Service-Start erfolgt nur mit `--service` oder `/service`.
- `Service Start` startet den installierten Windows-Service.
- `Service Stop` stoppt den installierten Windows-Service.
- Beim Schließen der GUI wird der Windows-Service nicht gestoppt.

## Service-Prozess

Der Service-Prozess startet zentral:

- SIP / SipInt / SipExt
- ProviderRegistration
- GSM
- SVC-Bereitschaft über SIP-Service-Routing

## Installation

Skript:

`Wichtig\SERVICE\Install-TK-Server-Service.ps1`

Der Dienst wird mit folgendem Startparameter installiert:

`TK-Server.exe --service`

## Deinstallation

Skript:

`Wichtig\SERVICE\Uninstall-TK-Server-Service.ps1`
