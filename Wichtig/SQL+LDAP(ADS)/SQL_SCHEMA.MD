# SQL_SCHEMA

Aktualisiert auf Basis von `TK-Server.sql` (Ist-Stand des Schemas) und erweitert um die textliche Tabellenbeschreibung aus `TK_Server_SQL_Tabellenbeschreibung_V0001.txt`.

Hinweis: Diese Datei beschreibt den aktuellen Stand der Datenbankstruktur aus dem SQL-Skript. Ältere Abweichungen aus früheren Ständen wurden hier nicht übernommen.

## Funktionen

### `dbo.TK_GUID(@Input)`

Zweck heute:
Deterministische GUID-Funktion zur stabilen Bildung eines `uniqueidentifier` aus einem Textwert bzw. einer zusammengesetzten Zeichenkette.

Wofür die Funktion verwendet wird:
- Berechnung persistierter GUIDs in mehreren Tabellen.
- Stabile technische Identifikation von Datensätzen über fachliche Merkmale statt zufälligem `NEWID()`.
- Wiedererkennbare Zuordnung bei Rebuild, Import, Vergleich und Synchronisierung.

Aktuell im Schema referenziert von:
- `TK_SIP_ServiceLeg.GUID`
- `TK_GSM_Modems.GUID`
- `TK_GSM_Sim.GUID`

Zukünftige Nutzung:
- Einheitliche deterministische Schlüsselerzeugung auch für weitere technische Laufzeitobjekte.
- Hilfreich für Migrationen, Dublettenprüfung und Wiederzusammenführung logisch identischer Datensätze.

## Tabellenübersicht

- `TK_SIP_User`
- `TK_SIP_Registration`
- `TK_InternalRegistrationState`
- `TK_RuntimeRegistration`
- `TK_ProviderAccounts`
- `TK_ProviderRegistrationState`
- `TK_ProviderRegistrationLog`
- `TK_SIP_CallLog`
- `TK_ExternalNumbers`
- `TK_ExternalNumberRouting`
- `TK_UserForwarding`
- `TK_SIP_ServiceLeg`
- `TK_Notruf`
- `TK_GSM_Modems`
- `TK_GSM_Sim`
- `TK_GSM_ModemSimStatus`
- `TK_Properties`

## TK_SIP_User

### Spalten

| Spalte | Definition |
|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` |
| `Username` | `[nvarchar](100) NOT NULL` |
| `Telefonnummer` | `[nvarchar](50) NOT NULL` |
| `Password` | `[nvarchar](255) NOT NULL` |
| `DisplayName` | `[nvarchar](100) NULL` |
| `Email` | `[nvarchar](255) NULL` |
| `Description` | `[nvarchar](255) NULL` |
| `UserType` | `[nvarchar](20) NOT NULL` |
| `Active` | `[bit] NOT NULL` |
| `CanDialInternal` | `[bit] NOT NULL` |
| `CanDialExternal` | `[bit] NOT NULL` |
| `CanReceiveInternal` | `[bit] NOT NULL` |
| `CanReceiveExternal` | `[bit] NOT NULL` |
| `SortOrder` | `[int] NOT NULL` |
| `CreatedAt` | `[datetime2](7) NOT NULL` |
| `UpdatedAt` | `[datetime2](7) NULL` |

### Beschreibung

Zweck heute:
Stammtabelle für alle internen SIP-Benutzer des TK-Servers. Hier liegen Benutzername, interne Rufnummer, Passwort, Anzeigename und Berechtigungen für interne und externe Gespräche.

Wofür die Tabelle verwendet wird:
- Anmeldung interner SIP-Clients.
- Rechteprüfung beim Wählen und Annehmen.
- Auflösung interner Rufnummern und Zielsuche.
- Zuordnung von Gesprächen, Registrierungen und Weiterleitungen zu einem Benutzer.

Zukünftige Nutzung:
- Erweiterte Rollenmodelle, z. B. Admin, User, Service, System.
- Geräteprofile pro Benutzer.
- Präsenzstatus, BLF, Pickup-Gruppen, Chef/Sekretariat.
- Rechte für SMS, GSM, Mailbox, Zeitansage, IVR und Sonderdienste.

## TK_SIP_Registration

### Spalten

| Spalte | Definition |
|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` |
| `UserID` | `[int] NOT NULL` |
| `ContactURI` | `[nvarchar](512) NOT NULL` |
| `RemoteEndPoint` | `[nvarchar](128) NOT NULL` |
| `Transport` | `[nvarchar](16) NULL` |
| `CallID` | `[nvarchar](255) NOT NULL` |
| `UserAgent` | `[nvarchar](255) NULL` |
| `ExpiresAt` | `[datetime2](7) NOT NULL` |
| `LastSeen` | `[datetime2](7) NOT NULL` |
| `Active` | `[bit] NOT NULL` |

### Beschreibung

Zweck heute:
Alte bzw. klassische Tabelle für interne SIP-Registrierungen. Sie speichert Contact, RemoteEndPoint, Transport, Call-ID, Ablaufzeit und Aktivstatus.

Wofür die Tabelle verwendet wird:
- Historisch bzw. als Altbestand für interne Registrierungen.
- Teilweise noch als einfache Registrierungsbasis in älteren Pfaden denkbar.

Zukünftige Nutzung:
- Wahrscheinlich nur noch als Übergangs- oder Migrationsobjekt.
- Falls komplett auf die neue Laufzeitlogik umgestellt wird, ist diese Tabelle ein Kandidat für vollständige Stilllegung.
- Wenn sie bleibt, dann höchstens als Legacy-Kompatibilität oder Archivsicht.

## TK_InternalRegistrationState

### Spalten

| Spalte | Definition |
|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` |
| `GUID` | `[uniqueidentifier] NOT NULL` |
| `UserID` | `[int] NOT NULL` |
| `Username` | `[nvarchar](100) NOT NULL` |
| `ContactURI` | `[nvarchar](512) NOT NULL` |
| `RemoteEndPoint` | `[nvarchar](255) NOT NULL` |
| `Transport` | `[nvarchar](16) NULL` |
| `CallID` | `[nvarchar](255) NOT NULL` |
| `UserAgent` | `[nvarchar](255) NULL` |
| `ExpiresAt` | `[datetime2](7) NOT NULL` |
| `LastSeen` | `[datetime2](7) NOT NULL` |
| `IsRegistered` | `[bit] NOT NULL` |
| `IsReachable` | `[bit] NOT NULL` |
| `LastError` | `[nvarchar](max) NULL` |
| `CreatedAt` | `[datetime2](7) NOT NULL` |
| `UpdatedAt` | `[datetime2](7) NOT NULL` |

### Beschreibung

Zweck heute:
Neue interne Laufzeittabelle für aktive interne Registrierungen. Sie speichert den tatsächlichen Zustand einer internen SIP-Anmeldung mit Benutzerbezug, ContactURI, RemoteEndPoint, Transport, Call-ID, Reachability und Zeitstempeln.

Wofür die Tabelle verwendet wird:
- Aktuelle Online-/Erreichbarkeitslogik für interne SIP-Teilnehmer.
- Routing zu aktuell erreichbaren internen Endgeräten.
- UI-Anzeige, ob Benutzer grün/online sind.
- Grundlage für Health-Checks, Socket-/Transportbezug und Runtime-Bereinigung.

Zukünftige Nutzung:
- Mehrfachregistrierungen pro Benutzer mit sauberer Priorisierung.
- Heartbeat-/OPTIONS-Status je Kontakt.
- Geräteklassen, NAT-Erkennung, Push-Wakeup oder mobile Clients.
- Bessere Session-Zuordnung für Dialoge, BYE, CANCEL und Re-INVITE.

## TK_RuntimeRegistration

### Spalten

| Spalte | Definition |
|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` |
| `GUID` | `[uniqueidentifier] NOT NULL` |
| `RegistrationType` | `[nvarchar](16) NOT NULL` |
| `SourceType` | `[nvarchar](32) NOT NULL` |
| `SourceID` | `[int] NOT NULL` |
| `UserID` | `[int] NULL` |
| `ProviderAccountID` | `[int] NULL` |
| `InternalStateID` | `[int] NULL` |
| `ProviderStateID` | `[int] NULL` |
| `DisplayName` | `[nvarchar](100) NULL` |
| `AorUri` | `[nvarchar](512) NULL` |
| `ContactURI` | `[nvarchar](512) NULL` |
| `RemoteEndPoint` | `[nvarchar](255) NULL` |
| `Transport` | `[nvarchar](16) NULL` |
| `LocalIPAddress` | `[nvarchar](64) NULL` |
| `LocalPort` | `[int] NULL` |
| `RequestUri` | `[nvarchar](512) NULL` |
| `CallID` | `[nvarchar](255) NULL` |
| `UserAgent` | `[nvarchar](255) NULL` |
| `ServiceRoute` | `[nvarchar](max) NULL` |
| `RouteSet` | `[nvarchar](max) NULL` |
| `AssociatedUri` | `[nvarchar](512) NULL` |
| `LastResolvedIP` | `[nvarchar](64) NULL` |
| `StatusCode` | `[int] NULL` |
| `StatusText` | `[nvarchar](255) NULL` |
| `ExpiresAt` | `[datetime2](7) NULL` |
| `LastSeen` | `[datetime2](7) NOT NULL` |
| `IsRegistered` | `[bit] NOT NULL` |
| `IsReachable` | `[bit] NOT NULL` |
| `LastError` | `[nvarchar](max) NULL` |
| `CreatedAt` | `[datetime2](7) NOT NULL` |
| `UpdatedAt` | `[datetime2](7) NOT NULL` |

### Beschreibung

Zweck heute:
Zentrale vereinheitlichte Laufzeittabelle für Registrierungen. Sie fasst interne und externe Registrierungen in einer gemeinsamen Struktur zusammen und kann auf interne Zustände oder Provider-Zustände verweisen.

Wofür die Tabelle verwendet wird:
- Einheitliche Sicht auf alle aktiven Registrierungen.
- UI, Monitoring und Debugging für intern und extern.
- Zusammenführung von AOR, Contact, RemoteEndPoint, RequestUri, CallID, Status, Reachability und Routeninformationen.
- Grundlage für zentrale Runtime-Cleanup-Logik.

Zukünftige Nutzung:
- Zentrale Live-Ansicht für die gesamte Anlage.
- Erweiterung um GSM-Registrierungs- bzw. Erreichbarkeitskonzepte.
- Globale Diagnose- und Socket-Verwaltung.
- Eventuell Basis für Dashboard, Alarmierung und Cluster-/Redundanzlogik.

## TK_ProviderAccounts

### Spalten

| Spalte | Definition |
|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` |
| `ProviderName` | `[nvarchar](100) NOT NULL` |
| `SipAuthUser` | `[nvarchar](100) NOT NULL` |
| `SIPUser` | `[nvarchar](100) NOT NULL` |
| `SIPPasswort` | `[nvarchar](255) NOT NULL` |
| `SIPServer` | `[nvarchar](255) NOT NULL` |
| `SIPRegistrar` | `[nvarchar](255) NULL` |
| `SIPProxy` | `[nvarchar](255) NULL` |
| `SIPOutboundProxy` | `[nvarchar](255) NULL` |
| `SIPRealm` | `[nvarchar](255) NULL` |
| `SIPSTUNServer` | `[nvarchar](255) NULL` |
| `SIPProtokoll` | `[nvarchar](10) NOT NULL` |
| `SIPRTPPort` | `[int] NULL` |
| `SIPPort` | `[int] NOT NULL` |
| `SRTP` | `[bit] NOT NULL` |
| `SIPNr` | `[nvarchar](50) NULL` |
| `Active` | `[bit] NOT NULL` |
| `CreatedAt` | `[datetime2](7) NOT NULL` |
| `UpdatedAt` | `[datetime2](7) NULL` |
| `ID` | `ASC` |

### Beschreibung

Zweck heute:
Stammdaten externer SIP-Providerkonten. Enthält alle Registrierungs- und Verbindungsdaten wie SIPUser, AuthUser, Passwort, Registrar, Proxy, Realm, Port, Protokoll und SRTP-Option.

Wofür die Tabelle verwendet wird:
- Aufbau externer SIP-Registrierungen.
- Auswahl des passenden Providerkontos für ausgehende Gespräche.
- Providerbezogene Konfiguration für Routing und Authentifizierung.

Zukünftige Nutzung:
- Mehrere Provider pro Standort oder Rufnummernblock.
- Fallback-Routing zwischen Providern.
- Provider-spezifische Codec-, Header- oder Sicherheitsprofile.
- Später auch Trennung nach SIP-Trunk, Mobilfunk-Gateway oder SMS-Provider.

## TK_ProviderRegistrationState

### Spalten

| Spalte | Definition |
|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` |
| `ProviderAccountID` | `[int] NOT NULL` |
| `IsRegistered` | `[bit] NOT NULL` |
| `LastRegisterAt` | `[datetime2](0) NULL` |
| `ExpiresAt` | `[datetime2](0) NULL` |
| `LastRemoteEndPoint` | `[nvarchar](255) NULL` |
| `LastResolvedIP` | `[nvarchar](64) NULL` |
| `LastTransport` | `[nvarchar](16) NULL` |
| `LastRequestUri` | `[nvarchar](512) NULL` |
| `ServiceRoute` | `[nvarchar](max) NULL` |
| `RouteSet` | `[nvarchar](max) NULL` |
| `AssociatedUri` | `[nvarchar](512) NULL` |
| `LastCallID` | `[nvarchar](255) NULL` |
| `LastStatusCode` | `[int] NULL` |
| `LastStatusText` | `[nvarchar](255) NULL` |
| `LastError` | `[nvarchar](max) NULL` |
| `UpdatedAt` | `[datetime2](0) NOT NULL` |

### Beschreibung

Zweck heute:
Laufzeitzustand je Providerkonto. Die Tabelle speichert, ob ein Provider aktuell registriert ist, wann zuletzt registriert wurde, welche Route und Associated URI geliefert wurden und welcher Status zuletzt vorlag.

Wofür die Tabelle verwendet wird:
- Laufende Kontrolle des externen Registrierungszustands.
- Übernahme von Service-Route, RouteSet, AssociatedUri und RemoteEndPoint in die Telefonielogik.
- Routing externer Gespräche über den aktuell gültigen Providerdialog.

Zukünftige Nutzung:
- Reconnect-Strategien, Failover und automatische Gesundheitsbewertung.
- Providerdiagnose in der UI.
- Session-Auffrischung und Testmechanismen via OPTIONS.
- Basis für spätere SMS-/Messaging-Anbindung über denselben Providerkontext.

## TK_ProviderRegistrationLog

### Spalten

| Spalte | Definition |
|---|---|
| `ID` | `[bigint] IDENTITY(1,1) NOT NULL` |
| `ProviderAccountID` | `[int] NOT NULL` |
| `EventType` | `[nvarchar](64) NOT NULL` |
| `StatusCode` | `[int] NULL` |
| `StatusText` | `[nvarchar](255) NULL` |
| `RemoteEndPoint` | `[nvarchar](255) NULL` |
| `RequestUri` | `[nvarchar](512) NULL` |
| `Transport` | `[nvarchar](16) NULL` |
| `CallID` | `[nvarchar](255) NULL` |
| `RawRequest` | `[nvarchar](max) NULL` |
| `RawResponse` | `[nvarchar](max) NULL` |
| `ErrorText` | `[nvarchar](max) NULL` |
| `CreatedAt` | `[datetime2](0) NOT NULL` |

### Beschreibung

Zweck heute:
Protokolltabelle für Ereignisse rund um Provider-Registrierungen. Enthält EventType, Statuscode, SIP-Rohdaten, Fehlertexte und Zeitstempel.

Wofür die Tabelle verwendet wird:
- Nachvollziehen von REGISTER, 401, 200 OK und Fehlerbildern.
- Diagnose bei Auth-, Routing- oder Transportproblemen.
- Historische Analyse je ProviderAccount.

Zukünftige Nutzung:
- Langfristige Betriebsdiagnose.
- Automatische Fehlerklassifikation.
- Alarmierung bei Ausfällen oder Registrierungsflattern.
- Eventuell Reporting, wann welcher Provider wie stabil lief.

## TK_SIP_CallLog

### Spalten

| Spalte | Definition |
|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` |
| `SipCallID` | `[nvarchar](255) NULL` |
| `FromUserID` | `[int] NULL` |
| `ToUserID` | `[int] NULL` |
| `FromNumber` | `[nvarchar](50) NULL` |
| `ToNumber` | `[nvarchar](50) NULL` |
| `Direction` | `[nvarchar](16) NULL` |
| `Status` | `[nvarchar](32) NULL` |
| `StartTime` | `[datetime2](7) NULL` |
| `AnswerTime` | `[datetime2](7) NULL` |
| `EndTime` | `[datetime2](7) NULL` |
| `DisconnectReason` | `[nvarchar](255) NULL` |
| `CreatedAt` | `[datetime2](7) NULL` |
| `UpdatedAt` | `[datetime2](7) NULL` |
| `BridgeID` | `[uniqueidentifier] NULL` |
| `LegACallID` | `[nvarchar](255) NULL` |
| `LegBCallID` | `[nvarchar](255) NULL` |
| `InviteInRaw` | `[nvarchar](max) NULL` |
| `InviteOutRaw` | `[nvarchar](max) NULL` |
| `ByeInRaw` | `[nvarchar](max) NULL` |
| `ByeOutRaw` | `[nvarchar](max) NULL` |
| `ReinviteCount` | `[int] NOT NULL` |
| `ByeCount` | `[int] NOT NULL` |

### Beschreibung

Zweck heute:
Zentrales Gesprächsprotokoll. Speichert Gesprächsrichtung, Status, Start/Ende, Rufnummern, Benutzerbezug, BridgeID sowie Rohdaten der SIP-Nachrichten pro Call.

Wofür die Tabelle verwendet wird:
- Nachvollziehen aller Anrufe.
- Analyse von LegA/LegB-Zuordnung.
- Debugging bei BYE-, CANCEL- und Re-INVITE-Problemen.
- Historie für interne, externe und vermittelte Gespräche.

Zukünftige Nutzung:
- CDR-Funktionalität mit Gebühren- oder Dauerabrechnung.
- Auswertungen zu Gesprächsdauer, Auslastung und Fehlerraten.
- Mailbox-/Service-/GSM-Gesprächsprotokolle.
- Compliance-, Export- oder Journalfunktionen.

## TK_ExternalNumbers

### Spalten

| Spalte | Definition |
|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` |
| `NumberType` | `[nvarchar](20) NOT NULL` |
| `SourceID` | `[int] NOT NULL` |
| `NumberValue` | `[nvarchar](50) NOT NULL` |
| `DisplayName` | `[nvarchar](100) NULL` |
| `IsOnline` | `[bit] NOT NULL` |
| `IsSelectable` | `[bit] NOT NULL` |
| `Active` | `[bit] NOT NULL` |
| `CreatedAt` | `[datetime2](7) NOT NULL` |
| `UpdatedAt` | `[datetime2](7) NOT NULL` |
| `SourceType` | `[nvarchar](20) NOT NULL` |

### Beschreibung

Zweck heute:
Abstraktionsschicht für externe bzw. extern nutzbare Nummern. Eine Nummer wird hier als eigenständiges Objekt mit Typ, Herkunft, Online-Status und Selektierbarkeit geführt.

Wofür die Tabelle verwendet wird:
- Verwaltung der nach außen verfügbaren Nummern.
- UI-Auswahl, welche externe Rufnummer für ausgehende Gespräche verwendet werden kann.
- Online-/Offline-Anzeige externer Accounts.
- Zusammenführung von Providernummern und später GSM-Nummern unter einer gemeinsamen Sicht.

Zukünftige Nutzung:
- Einheitliche Nummernverwaltung für SIP-Provider, GSM, SMS-Absender, Service-DIDs und Sondernummern.
- Nummern-Pools, CLIP no screening, Absenderrichtlinien.
- Priorisierung nach Erreichbarkeit und Medienart.

## TK_ExternalNumberRouting

### Spalten

| Spalte | Definition |
|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` |
| `ExternalNumberID` | `[int] NOT NULL` |
| `ExterneNr` | `[nvarchar](50) NULL` |
| `TargetType` | `[nvarchar](20) NOT NULL` |
| `InterneNr` | `[nvarchar](50) NULL` |
| `InternalNumberID` | `[int] NOT NULL` |
| `Priority` | `[int] NOT NULL` |
| `Active` | `[bit] NOT NULL` |
| `CreatedAt` | `[datetime2](7) NOT NULL` |
| `UpdatedAt` | `[datetime2](7) NOT NULL` |

### Beschreibung

Zweck heute:
Routingtabelle für externe Rufnummern. Sie beschreibt, welches Ziel zu einer externen Nummer gehört und mit welcher Priorität diese Zuordnung gilt.

Wofür die Tabelle verwendet wird:
- Inbound-Routing von externer Nummer auf internes Ziel.
- Abbildung von DIDs auf Benutzer, interne Nummern oder andere Zielarten.
- Priorisierte Zielsuche.

Zukünftige Nutzung:
- Mehrziel-Ringing, Rufgruppen, Hunt-Groups.
- Zeitabhängiges Routing.
- Routing auf Service-Legs, Mailbox, Anrufbeantworter, GSM oder Eskalationsketten.
- Standort- oder Lastverteilung.

## TK_UserForwarding

### Spalten

| Spalte | Definition |
|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` |
| `UserID` | `[int] NOT NULL` |
| `Active` | `[bit] NOT NULL` |
| `ForwardType` | `[nvarchar](20) NOT NULL` |
| `Target` | `[nvarchar](100) NOT NULL` |
| `ProviderAccountID` | `[int] NULL` |
| `Priority` | `[int] NOT NULL` |
| `CreatedAt` | `[datetime2](7) NOT NULL` |
| `UpdatedAt` | `[datetime2](7) NULL` |
| `ID` | `ASC` |

### Beschreibung

Zweck heute:
Weiterleitungsregeln pro internem Benutzer. Enthält ForwardType, Ziel, Providerbezug und Priorität.

Wofür die Tabelle verwendet wird:
- Rufumleitungen intern, extern, per Provider oder später Richtung GSM/Voicemail.
- Regelbasierte Zielumleitung bei Benutzern.

Zukünftige Nutzung:
- Besetzt-, keine Antwort- und Sofort-Weiterleitung.
- Zeitprofile, Kalenderbezug und Benutzerstatus.
- Mailbox, IVR, GSM-Fallback und Eskalationen.
- Kombination mit Service-Legs und späterem Anrufbeantworter.

## TK_SIP_ServiceLeg

### Spalten

| Spalte | Definition |
|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` |
| `GUID` | `AS ([dbo].[TK_GUID](concat(isnull([InternalNumber],N''),N'\|',isnull([ServiceCode],N''),N'\|',isnull([ServiceType],N'')))) PERSISTED` |
| `InternalNumber` | `[nvarchar](50) NOT NULL` |
| `DisplayName` | `[nvarchar](100) NOT NULL` |
| `Description` | `[nvarchar](255) NULL` |
| `ServiceCode` | `[nvarchar](50) NOT NULL` |
| `ServiceType` | `[nvarchar](30) NOT NULL` |
| `PromptFile` | `[nvarchar](260) NULL` |
| `RecordTemplate` | `[nvarchar](260) NULL` |
| `JsonConfig` | `[nvarchar](max) NULL` |
| `AutoAnswer` | `[bit] NOT NULL` |
| `KeepSessionUntilPeerEnded` | `[bit] NOT NULL` |
| `Active` | `[bit] NOT NULL` |
| `SortOrder` | `[int] NOT NULL` |
| `CreatedAt` | `[datetime2](7) NOT NULL` |
| `UpdatedAt` | `[datetime2](7) NULL` |

### Beschreibung

Zweck heute:
Stammtabelle für interne Service-Legs. Sie beschreibt interne Servicenummern wie Zeitansage, spätere Mediadienste, automatische Annahme, Prompt-Dateien und Konfigurationsdaten.

Wofür die Tabelle verwendet wird:
- Abbildung interner Service-Ziele als eigene logische Gesprächspartner.
- Verwaltung von ServiceCode, ServiceType, PromptFile und JSON-Konfiguration.
- Steuerung, ob ein Dienst automatisch annimmt und wie lange die Session offen bleibt.

Zukünftige Nutzung:
- Zeitansage, Ansagen, IVR, Testdienste, Anrufbeantworter, Warteschlangen, Musik, Echo-Test.
- Service-Leg D und weitere Spezial-Legs.
- Konfigurierbare Sprachdienste, Aufzeichnungen und Automationsabläufe.

## TK_Notruf

### Spalten

| Spalte | Definition |
|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` |
| `Notrufnummer` | `[nvarchar](20) NOT NULL` |
| `Bezeichnung` | `[nvarchar](100) NOT NULL` |
| `TargetType` | `[nvarchar](20) NOT NULL` |
| `TargetValue` | `[nvarchar](100) NOT NULL` |
| `ProviderAccountID` | `[int] NULL` |
| `Priority` | `[int] NOT NULL` |
| `Active` | `[bit] NOT NULL` |
| `CreatedAt` | `[datetime2](7) NOT NULL` |
| `UpdatedAt` | `[datetime2](7) NULL` |

### Beschreibung

Zweck heute:
Spezialrouting für Notrufnummern bzw. priorisierte Notfallziele.

Wofür die Tabelle verwendet wird:
- Behandlung bestimmter Notrufnummern mit festem Zieltyp und Zielwert.
- Priorisierte Auswahl eines Providerkontos oder Ziels für Notruf-Szenarien.

Zukünftige Nutzung:
- Standort- oder providerabhängiges Emergency Routing.
- Gesonderte Regeln für GSM-Fallback bei SIP-Ausfall.
- Dokumentations- und Schutzmechanismen für sicherheitskritische Rufe.

## TK_GSM_Modems

### Spalten

| Spalte | Definition |
|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` |
| `GUID` | `AS ([dbo].[TK_GUID](concat(isnull([ModemName],N''),N'\|',isnull([Manufacturer],N''),N'\|',isnull([Model],N''),N'\|',isnull([IMEI],N'')))) PERSISTED` |
| `ModemName` | `[nvarchar](100) NOT NULL` |
| `Manufacturer` | `[nvarchar](100) NULL` |
| `Model` | `[nvarchar](100) NULL` |
| `IMEI` | `[nvarchar](50) NULL` |
| `PNPDeviceID` | `[nvarchar](500) NULL` |
| `Active` | `[bit] NOT NULL` |
| `CreatedAt` | `[datetime2](7) NOT NULL` |
| `UpdatedAt` | `[datetime2](7) NULL` |

### Beschreibung

Zweck heute:
Stammdaten aller erkannten GSM-Modems. Enthält Name, Hersteller, Modell, IMEI und PNPDeviceID. Die GUID wird deterministisch aus den Modemmerkmalen erzeugt.

Wofür die Tabelle verwendet wird:
- Erkennung und Identifizierung physischer GSM-Hardware.
- Stabiler Bezug von Runtime-Statusdaten zu einem bekannten Modem.
- Grundlage für spätere GSM-Leg-Logik.

Zukünftige Nutzung:
- Verwaltung mehrerer GSM-Gateways.
- Port-Mapping, Kanalverwaltung, SMS-Funktionen und Mobilfunktelefonie.
- Health-Monitoring und automatische Gerätezuordnung.

## TK_GSM_Sim

### Spalten

| Spalte | Definition |
|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` |
| `GUID` | `AS ([dbo].[TK_GUID](concat(isnull([Telefonnummer],N''),N'\|',isnull([ProviderName],N''),N'\|',isnull([ICCID],N''),N'\|',isnull([IMSI],N'')))) PERSISTED` |
| `Telefonnummer` | `[nvarchar](50) NULL` |
| `ProviderName` | `[nvarchar](100) NULL` |
| `ICCID` | `[nvarchar](50) NULL` |
| `IMSI` | `[nvarchar](50) NULL` |
| `Active` | `[bit] NOT NULL` |
| `CreatedAt` | `[datetime2](7) NOT NULL` |
| `UpdatedAt` | `[datetime2](7) NULL` |

### Beschreibung

Zweck heute:
Stammdaten von SIM-Karten. Enthält Telefonnummer, ProviderName, ICCID, IMSI und eine deterministische GUID.

Wofür die Tabelle verwendet wird:
- Eindeutige Identifikation und Verwaltung von SIM-Karten.
- Trennung zwischen physischem Modem und eingesetzter SIM.
- Basis für Zuordnung zu externer Nummer und später zu GSM-Legs.

Zukünftige Nutzung:
- SMS-Versand und SMS-Empfang.
- Call-through, Mobilfunk-Routing und fallbackfähige Mobilfunkkanäle.
- Tarif-, Provider- und Nummernmanagement.

## TK_GSM_ModemSimStatus

### Spalten

| Spalte | Definition |
|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` |
| `GUID` | `[uniqueidentifier] NOT NULL` |
| `ModemID` | `[int] NOT NULL` |
| `SimID` | `[int] NULL` |
| `IsPresent` | `[bit] NOT NULL` |
| `IsSimInserted` | `[bit] NOT NULL` |
| `IsRegistered` | `[bit] NOT NULL` |
| `SignalStrength` | `[int] NULL` |
| `ComPortAT` | `[nvarchar](50) NULL` |
| `ComPortModem` | `[nvarchar](50) NULL` |
| `ComPortDiag` | `[nvarchar](50) NULL` |
| `ComPortAux` | `[nvarchar](50) NULL` |
| `LastSeen` | `[datetime2](7) NULL` |
| `LastError` | `[nvarchar](500) NULL` |
| `CreatedAt` | `[datetime2](7) NOT NULL` |
| `UpdatedAt` | `[datetime2](7) NULL` |

### Beschreibung

Zweck heute:
Laufzeit- und Status-Tabelle für die Beziehung zwischen Modem und SIM. Enthält Netzstatus, Signalstärke, COM-Ports, Anwesenheit und Fehlerzustand.

Wofür die Tabelle verwendet wird:
- Live-Status eines GSM-Geräts im System.
- Kontrolle, ob SIM eingelegt und im Netz registriert ist.
- Technische Diagnose über COM-Port-Zuordnung und Fehlertexte.

Zukünftige Nutzung:
- Kanalwahl für GSM-Gespräche.
- SMS-Queue-Routing auf geeignete Modems.
- Automatische Degradierung oder Umschaltung bei schlechtem Empfang.
- Monitoring und Alarme bei Modem-/SIM-Ausfällen.

## TK_Properties

### Spalten

| Spalte | Definition |
|---|---|
| `Id` | `[int] IDENTITY(1,1) NOT NULL` |
| `SipDomain` | `[nvarchar](255) NOT NULL` |
| `SipRegistrar` | `[nvarchar](255) NOT NULL` |
| `SipProxy` | `[nvarchar](255) NULL` |
| `SipPort` | `[int] NOT NULL` |
| `SipTransport` | `[nvarchar](10) NOT NULL` |
| `StunServer` | `[nvarchar](255) NULL` |
| `MediaAdvertisedIp` | `[nvarchar](64) NULL` |
| `RtpPortStart` | `[int] NOT NULL` |
| `RtpPortEnd` | `[int] NOT NULL` |
| `Codecs` | `[nvarchar](255) NULL` |
| `UseSRTP` | `[bit] NOT NULL` |
| `RegisterExpire` | `[int] NOT NULL` |
| `CreatedAt` | `[datetime2](7) NOT NULL` |
| `UpdatedAt` | `[datetime2](7) NULL` |
| `Id` | `ASC` |

### Beschreibung

Zweck heute:
Globale Grundeinstellungen des TK-Servers für SIP, RTP, STUN, Codec-Auswahl und Registrierungsintervalle.

Wofür die Tabelle verwendet wird:
- Zentrale Serverkonfiguration.
- Vorgabewerte für Ports, Transport und Medien.
- Basisparameter für den laufenden Betrieb.

Zukünftige Nutzung:
- Trennung in profilbasierte Konfigurationen.
- Mehrere Umgebungen oder Mandanten.
- Erweiterung um Transportmanager-, Health- und Timeout-Parameter.
- Globale Richtlinien für SRTP, Codec-Strategien, Keepalive und NAT.

## Kurzfazit

Das Schema bildet heute bereits fünf große Bereiche ab:
1. interne SIP-Benutzer und interne Registrierung,
2. externe Provider und externe Registrierung,
3. Gesprächs- und Routinglogik,
4. Service-Legs und Sonderdienste,
5. GSM-Hardware, SIMs und Mobilfunkstatus.

Die technische Entwicklungsrichtung des Schemas ist klar erkennbar:
- weg von einfachen Einzel-Registrierungstabellen,
- hin zu vereinheitlichten Runtime-Zuständen,
- hin zu sauber getrennten Gesprächspartnern bzw. Legs,
- hin zu einer gemeinsamen Plattform für intern, extern, Service und später GSM/SMS.
