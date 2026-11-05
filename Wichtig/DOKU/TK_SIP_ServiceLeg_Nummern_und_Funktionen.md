# TK_SIP_ServiceLeg – Servicenummern und Funktionen

> **Entwicklungsstand:** Der TK-Server und seine Servicefunktionen befinden sich noch im Aufbau und sind **noch nicht zu 100 % einsatzbereit**. Funktionen können unvollständig sein oder noch Fehler enthalten. Die aufgeführten Nummern beschreiben die vorhandene Konfiguration; auch ein als „Aktiv“ markierter Dienst ist damit nicht automatisch vollständig funktionsfähig oder für den produktiven Einsatz freigegeben.

Stand der Auswertung: 05.11.2026. Grundlage ist die Tabelle `dbo.TK_SIP_ServiceLeg` aus dem Originalexport `DB-Backup/TK-Server_DB-ORIGINAL.sql` im DEV-Projekt.

Diese Übersicht enthält **alle 19 Datensätze** des Exports: **11 aktiv, 8 deaktiviert**. Status und Funktionsbeschreibung entsprechen der gespeicherten Konfiguration; sie sind kein zusätzlicher Funktionstest des Quellcodes.

## Nummernübersicht

Die vollständige Servicenummer steht in `InternalNumber`. Die Menüauswahl wird aus `MenuOption` mit abschließendem `#` dargestellt. Menüwurzel ist `*100`.

| Vollständige Servicenummer | Auswahl im Menü | Dienst / ServiceCode | Funktion | Kontext | Menüzuordnung | Status |
|---|---|---|---|---|---|---|
| `*100` | – | Hauptmenue (`MAIN_MENU`) | Service-Hauptmenü starten. | Service-Aufruf / Menü | Menüwurzel | Aktiv |
| `*100*1#` | `*1#` | Mailbox abrufen (`VOICEMAIL`) | Mailbox öffnen bzw. abfragen. | Service-Aufruf / Menü | Hauptmenü | Aktiv |
| `*100*2#` | `*2#` | Mailboxansage speichern (`VOICEMAIL_GREETING_RECORD`) | Neue persönliche Mailboxansage aufnehmen und speichern; laut Konfiguration beendet und speichert # die Aufnahme. | Service-Aufruf / Menü | Hauptmenü | Aktiv |
| `*100*21#` | `*21#` | Mailboxansage anhören (`VOICEMAIL_GREETING_PLAYBACK`) | Die aktuelle persönliche Mailboxansage anhören; danach zum Hauptmenü zurückkehren laut Konfiguration. | Service-Aufruf / Menü | Hauptmenü | Aktiv |
| `*100*3#` | `*3#` | Gespraech weiterleiten (`BLIND_TRANSFER`) | Laufendes Gespräch ohne vorherige Rückfrage weiterleiten. Zielnummer nach der Auswahl eingeben und mit # bestätigen. | Laufendes Gespräch | Hauptmenü | Aktiv |
| `*100*4#` | `*4#` | Gespraech aufnehmen (`CALL_RECORDING_TOGGLE`) | Aufnahme des laufenden Gesprächs ein- oder ausschalten. Konfiguration: beide Legs aufnehmen, Ankündigung nur für den auslösenden Teilnehmer. | Laufendes Gespräch | Hauptmenü | Aktiv |
| `*100*5#` | `*5#` | Halten fortsetzen (`HOLD_RESUME`) | Laufendes Gespräch halten oder fortsetzen. | Laufendes Gespräch | Hauptmenü | Aktiv |
| `*100*6#` | `*6#` | Rueckfrage Transfer (`ATTENDED_TRANSFER`) | Rückfrage bzw. angekündigte Weiterleitung starten. Rückfrageziel eingeben und mit # bestätigen. | Laufendes Gespräch | Hauptmenü | Aktiv |
| `*100*7#` | `*7#` | An Service uebergeben (`TRANSFER_TO_SERVICE`) | Gesprächspartner an einen Service übergeben. Zielservice eingeben und mit # bestätigen. | Laufendes Gespräch | Hauptmenü | Aktiv |
| `*100*8#` | `*8#` | Zeitansage (`TIMESPEECH`) | Aktuelle Zeit ansagen; Spracheinstellung de-DE. | Service-Aufruf / Menü | Hauptmenü | Aktiv |
| `*100*9#` | `*9#` | Weitere Menuepunkte (`MENU_MORE`) | Untermenü für weitere Menüeinträge und technische Dienste öffnen. | Service-Aufruf / Menü | Hauptmenü | Aktiv |
| `*100*99#` | `*99#` | Hilfe (`MENU_HELP`) | Bedienhinweise zum Service-Menü ansagen. | Service-Aufruf / Menü | Hauptmenü | **Deaktiviert** |
| `*100*0#` | `*0#` | Transfer abbrechen (`CANCEL_TRANSFER`) | Laufenden Transfer abbrechen. | Laufendes Gespräch | Hauptmenü | **Deaktiviert** |
| `*100*90#` | `*90#` | Transfer bestaetigen (`CONFIRM_TRANSFER`) | Laufenden Transfer bestätigen. | Laufendes Gespräch | Weitere Menüpunkte | **Deaktiviert** |
| `*100*91#` | `*91#` | Playback Test (`PLAYBACK`) | Testdienst für Audio-Wiedergabe. | Service-Aufruf / Menü | Weitere Menüpunkte | **Deaktiviert** |
| `*100*92#` | `*92#` | Recorder Test (`RECORDER`) | Testdienst für Audio-Aufnahme; Aufnahme nach Signalton, Beenden/Speichern mit # laut Ansage. | Service-Aufruf / Menü | Weitere Menüpunkte | **Deaktiviert** |
| `*100*93#` | `*93#` | Tone Test (`TONE_TEST`) | Ton-Testdienst. | Service-Aufruf / Menü | Weitere Menüpunkte | **Deaktiviert** |
| `*100*94#` | `*94#` | Echo Test (`ECHO_TEST`) | Echo-Testdienst. | Service-Aufruf / Menü | Weitere Menüpunkte | **Deaktiviert** |
| `*100*95#` | `*95#` | Blackhole Test (`BLACKHOLE`) | Als Blackhole-Testdienst konfigurierter technischer Dienst; der Export beschreibt keine weiteren Einzelheiten. | Service-Aufruf / Menü | Weitere Menüpunkte | **Deaktiviert** |

## Bedienung und Einordnung

- `*100` öffnet das Hauptmenü. Eine vollständige Nummer wie `*100*8#` bezeichnet die Zeitansage. Im laufenden Menü entspricht die konfigurierte Auswahl `*8#`.
- Dienste mit dem Kontext **Laufendes Gespräch** sind für die Steuerung eines bestehenden Gesprächs vorgesehen; das gilt insbesondere für Transfer, Halten und Gesprächsaufnahme.
- Die Bedeutung von `#` hängt vom Zustand ab: Auswahl/Zieleingabe bestätigen oder eine laufende Aktion beenden und gegebenenfalls speichern. Es ist kein in jedem Zustand gleichbedeutender allgemeiner Abbruch.
- **Deaktivierte** Einträge sind im Export vorhanden, werden aber vom regulären aktiven Service-Lader nicht als aktive Dienste geladen. Ihre Nennung in einer Optionsliste aktiviert sie nicht automatisch.
- Die Funktionen gehören zum SVC-Bereich und werden über ein Service-Leg D ausgeführt.

## Auffälligkeiten der gespeicherten Menükonfiguration

- Das Hauptmenü nennt in seiner JSON-Optionsliste `*1` bis `*9`, `*99` und `*0`. Die beiden Datensätze für Hilfe (`*99`) und Transferabbruch (`*0`) sind im Export deaktiviert.
- Die aktive Funktion zum Anhören der Mailboxansage (`*100*21#`) ist dem Hauptmenü zugeordnet, steht jedoch nicht in dessen gespeicherter JSON-Optionsliste. Der Eintrag ist in dieser Übersicht trotzdem vollständig enthalten.
- Das Untermenü „Weitere Menüpunkte“ nennt `*90` bis `*95`; sämtliche sechs zugehörigen Datensätze sind im Export deaktiviert.
- Beim Aufnehmen einer Mailboxansage nennt die JSON-Konfiguration `StoreMode=FileSystem`. Diese Übersicht übernimmt die Konfigurationsaussage; daraus wird keine zusätzliche Zusicherung über den tatsächlichen Speicherpfad im aktuellen Code abgeleitet.

## Quellen und Bezug

- Originaldaten: `dbo.TK_SIP_ServiceLeg` im DEV-Originalexport unter `Wichtig/SQL+LDAP(ADS)/DB-Backup/TK-Server_DB-ORIGINAL.sql`.
- Ausgewertete Felder: `InternalNumber`, `DisplayName`, `Description`, `ServiceCode`, `ServiceType`, `Active`, `MenuOption`, `MenuParentCode`, `SortOrder` und `JsonConfig`.
- [Vollständige Schemadokumentation](SQL_SCHEMA_VOLLSTAENDIG.MD#tk_sip_serviceleg).
- [Installationsvorlage](TK-Server_DB-INSTALL.sql).
