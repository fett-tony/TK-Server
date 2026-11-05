# SQL_SCHEMA – vollständige Tabellen- und Beziehungsbeschreibung

Stand: 05.11.2026. Neue vollständige Fassung nach dem Aufbau von `SQL_SCHEMA.MD`; die bisherige Datei bleibt unverändert.

Grundlage sind `TK-Server_DB-INSTALL.sql` (aktuelles Installationsschema), die Struktur aus `TK-Server_DB-ORIGINAL.sql` und die relevanten C#-Zugriffe unter `SRC/`. Die Dokumentation enthält **23 Tabellen, 408 tatsächliche Spalten und alle 24 im SQL definierten Fremdschlüssel**. Datenwerte wie persönliche Zugangsdaten werden nicht wiedergegeben.

Die Bedeutungen beschreiben den geprüften Iststand. Zukünftige Erweiterungen aus der alten Vorlage werden nicht als bereits vorhandene Funktionen ausgegeben. Eine Live-Datenbankabfrage wurde nicht durchgeführt.

## Leseschlüssel

- **FK:** Beziehung wird im SQL-Schema durch einen FOREIGN KEY abgesichert.
- **Codebezug/fachlicher Bezug:** Die Anwendung oder die Konfiguration verknüpft die Werte; im aktuellen Schema existiert dafür kein FOREIGN KEY.
- **Bedingter Bezug:** Die Zieltabelle einer ID ergibt sich erst aus einem zusätzlichen Typfeld, beispielsweise SourceType.
- `NULL` bedeutet nicht belegt; `NOT NULL` ist eine Pflichtvorgabe des Schemas. `IDENTITY` vergibt laufende IDs. `AS … PERSISTED` kennzeichnet gespeicherte berechnete Werte, die nicht als normale Spalte eingefügt werden.
- Bei Beziehungen Kind → Eltern referenzieren mehrere Kindzeilen grundsätzlich einen Elterndatensatz; zusätzliche UNIQUE-Indizes können die Anzahl begrenzen. Eine ID ist nicht automatisch eine Rufnummer.
- Ohne ausdrückliche ON DELETE/ON UPDATE-Aktion gilt bei den hier vorhandenen FKs das SQL-Standardverhalten NO ACTION; diese Dokumentation erfindet keine Löschkaskaden.

## Funktionen

### `dbo.TK_GUID(@input nvarchar(4000))`

Zweck heute: deterministische technische GUID aus Text. Die Funktion ersetzt NULL durch die leere Zeichenfolge, berechnet SHA2_256, nimmt die ersten 16 Bytes und konvertiert sie nach uniqueidentifier. Sie ist WITH SCHEMABINDING definiert. Gleicher Eingabetext liefert denselben Wert; die Funktion erzeugt keinen zufälligen oder auf einer fremden Tabellen-ID beruhenden Schlüssel.

Aktuell berechnete Spalten:

- [`TK_GSM_Sim`](#tk_gsm_sim).`GUID`: `AS ([dbo].[TK_GUID](concat(isnull([Telefonnummer],N''),N'\|',isnull([ProviderName],N''),N'\|',isnull([ICCID],N''),N'\|',isnull([IMSI],N'')))) PERSISTED`
- [`TK_GSM_Modems`](#tk_gsm_modems).`GUID`: `AS ([dbo].[TK_GUID](concat(isnull([ModemName],N''),N'\|',isnull([Manufacturer],N''),N'\|',isnull([Model],N''),N'\|',isnull([IMEI],N'')))) PERSISTED`
- [`TK_SIP_ServiceLeg`](#tk_sip_serviceleg).`GUID`: `AS ([dbo].[TK_GUID](concat(isnull([InternalNumber],N''),N'\|',isnull([ServiceCode],N''),N'\|',isnull([ServiceType],N'')))) PERSISTED`
- [`TK_CountryPrefix`](#tk_countryprefix).`GUID`: `AS ([dbo].[TK_GUID](concat(isnull([ISO2],N''),N'\|',isnull([CountryName],N''),N'\|',isnull([PrefixDigits],N'')))) PERSISTED`
- [`TK_DEAreaCode`](#tk_deareacode).`GUID`: `AS ([dbo].[TK_GUID](concat(isnull([AreaCodeDigits],N''),N'\|',isnull([AreaName],N'')))) PERSISTED`

Zusätzlich berechnet TKSQL_Commands bei Bedarf die regulär gespeicherte `TK_GSM_ModemSimStatus.GUID` aus Modem-GUID und optionaler SIM-GUID über dieselbe SQL-Funktion.

### ID-Bildung und Gesprächszuordnung

Die SQL-Funktion ist von der C#-Sessionbildung zu unterscheiden: `TKBase_SessionManager.CreateSessionId` bildet aktuell eine MD5-basierte GUID aus `LegACallID + "|" + LegBOrDCallID` und gibt sie ohne Bindestriche als Text aus. Die Bridge-GUID wird dort getrennt aus dem Text `BRIDGE|<SessionID>` gebildet. Dies dokumentiert den vorhandenen Code; es ändert nicht die IDs.

Leg A ist der Anrufer, Leg B das Ziel und Leg D das Service-Leg im SVC-Bereich. Die Call-IDs bleiben Textkennungen ihrer Legs und sind keine Benutzer- oder Service-Datensatz-IDs. `TK_RuntimeCall` besitzt keine numerische `ServiceLegID`-Spalte. Bei der Historienanlage durch den RuntimeCallManager erhält `TK_SIP_CallLog.SipCallID` die SessionID; die tatsächlichen Leg-Call-IDs stehen in ihren eigenen Spalten.

## Tabellenübersicht

| Tabelle | Spalten | Zweck |
|---|---:|---|
| [`TK_SIP_User`](#tk_sip_user) | 18 | Interne SIP- und Faxbenutzer mit Nebenstellen, verschlüsselten Passwörtern und Berechtigungen. |
| [`TK_SIP_Registration`](#tk_sip_registration) | 10 | Klassische Registrierungstabelle mit Benutzer, Kontakt und Ablaufzeit. |
| [`TK_InternalRegistrationState`](#tk_internalregistrationstate) | 16 | Detailzustand interner SIP-Registrierungen. |
| [`TK_RuntimeRegistration`](#tk_runtimeregistration) | 32 | Vereinheitlichte Live-Sicht auf interne und externe SIP-Registrierungen. |
| [`TK_ProviderAccounts`](#tk_provideraccounts) | 19 | Externe SIP-Providerkonten mit Authentifizierungs-, Transport- und Rufnummerneinstellungen. |
| [`TK_ProviderRegistrationState`](#tk_providerregistrationstate) | 17 | Zuletzt gespeicherter Registrierungszustand eines Providerkontos. |
| [`TK_ProviderRegistrationLog`](#tk_providerregistrationlog) | 13 | Ereignisprotokoll der Providerregistrierung einschließlich SIP-Rohtexten. |
| [`TK_SIP_CallLog`](#tk_sip_calllog) | 23 | Gesprächshistorie für interne, externe, GSM-, Fax- und Servicegespräche. |
| [`TK_RuntimeCall`](#tk_runtimecall) | 37 | Live-Sicht auf laufende Gespräche mit Session, Legs und Bedienmöglichkeiten. |
| [`TK_ExternalNumbers`](#tk_externalnumbers) | 11 | Einheitlicher Katalog externer Leitungs-/Absenderrufnummern aus Providern und GSM. |
| [`TK_ExternalNumberRouting`](#tk_externalnumberrouting) | 11 | Zuordnung externer Rufnummern zu internen Benutzern mit Aktivierung und Priorität. |
| [`TK_UserForwarding`](#tk_userforwarding) | 9 | Benutzerbezogene Rufumleitungen. |
| [`TK_SIP_ServiceLeg`](#tk_sip_serviceleg) | 22 | Service-Leg-D-Konfiguration mit Service-Hauptmenü, Untermenüs, DTMF-Optionen und Medieneinstellungen. |
| [`TK_Notruf`](#tk_notruf) | 10 | Spezielle Routingregeln für Notruf-/Sondernummern. |
| [`TK_GSM_Modems`](#tk_gsm_modems) | 10 | Identität physischer GSM-Modems. |
| [`TK_GSM_Sim`](#tk_gsm_sim) | 9 | Identität von SIM-Karten und deren Mobilfunkrufnummern. |
| [`TK_GSM_ModemSimStatus`](#tk_gsm_modemsimstatus) | 16 | Aktuelle Zuordnung und Status eines Modems mit optional eingesetzter SIM. |
| [`TK_Properties`](#tk_properties) | 34 | Zentrale Einstellungen für Standort, SIP, RTP, Codecs, Medienablage, Logging und öffentliche IP. |
| [`TK_CountryPrefix`](#tk_countryprefix) | 13 | Katalog internationaler Länder- und Telefonvorwahlen. |
| [`TK_DEAreaCode`](#tk_deareacode) | 12 | Katalog deutscher Ortsvorwahlen mit berechneten nationalen/internationalen Darstellungen. |
| [`TK_AudioCodec`](#tk_audiocodec) | 31 | Katalog der Audio-/RTP-Codecs mit Formaten, Parametern und Einsatzmöglichkeiten. |
| [`TK_MailBoxMessage`](#tk_mailboxmessage) | 24 | Datenbankbasierter Audiospeicher mit Benutzerbezug und Formatmetadaten. |
| [`TK_RuntimeAreaControl`](#tk_runtimeareacontrol) | 11 | Gemeinsame Bereichssteuerung mit Sollzustand der GUI und Istzustand des Services. |

## Verbindungen der gesamten Datenbank

### Durch SQL Server abgesicherte Fremdschlüssel

| Quelltabelle und Spalte | Zieltabelle und Spalte | SQL-Constraint |
|---|---|---|
| [`TK_GSM_ModemSimStatus`](#tk_gsm_modemsimstatus).`ModemID` | [`TK_GSM_Modems`](#tk_gsm_modems).`ID` | `FK_TK_GSM_ModemSimStatus_Modem` |
| [`TK_GSM_ModemSimStatus`](#tk_gsm_modemsimstatus).`SimID` | [`TK_GSM_Sim`](#tk_gsm_sim).`ID` | `FK_TK_GSM_ModemSimStatus_Sim` |
| [`TK_InternalRegistrationState`](#tk_internalregistrationstate).`UserID` | [`TK_SIP_User`](#tk_sip_user).`ID` | `FK_TK_InternalRegistrationState_User` |
| [`TK_MailBoxMessage`](#tk_mailboxmessage).`CodecID` | [`TK_AudioCodec`](#tk_audiocodec).`ID` | `FK_TK_MailBoxMessage_CodecID` |
| [`TK_MailBoxMessage`](#tk_mailboxmessage).`UserID` | [`TK_SIP_User`](#tk_sip_user).`ID` | `FK_TK_MailBoxMessage_UserID` |
| [`TK_Properties`](#tk_properties).`AreaID` | [`TK_DEAreaCode`](#tk_deareacode).`ID` | `FK_TK_Properties_AreaID` |
| [`TK_Properties`](#tk_properties).`CountryID` | [`TK_CountryPrefix`](#tk_countryprefix).`ID` | `FK_TK_Properties_CountryID` |
| [`TK_ProviderRegistrationLog`](#tk_providerregistrationlog).`ProviderAccountID` | [`TK_ProviderAccounts`](#tk_provideraccounts).`ID` | `FK_TK_ProviderRegistrationLog_ProviderAccountID` |
| [`TK_ProviderRegistrationState`](#tk_providerregistrationstate).`ProviderAccountID` | [`TK_ProviderAccounts`](#tk_provideraccounts).`ID` | `FK_TK_ProviderRegistrationState_ProviderAccountID` |
| [`TK_RuntimeCall`](#tk_runtimecall).`FromUserID` | [`TK_SIP_User`](#tk_sip_user).`ID` | `FK_TK_RuntimeCall_FromUser` |
| [`TK_RuntimeCall`](#tk_runtimecall).`GsmModemID` | [`TK_GSM_Modems`](#tk_gsm_modems).`ID` | `FK_TK_RuntimeCall_GsmModem` |
| [`TK_RuntimeCall`](#tk_runtimecall).`GsmSimID` | [`TK_GSM_Sim`](#tk_gsm_sim).`ID` | `FK_TK_RuntimeCall_GsmSim` |
| [`TK_RuntimeCall`](#tk_runtimecall).`GsmStatusID` | [`TK_GSM_ModemSimStatus`](#tk_gsm_modemsimstatus).`ID` | `FK_TK_RuntimeCall_GsmStatus` |
| [`TK_RuntimeCall`](#tk_runtimecall).`ProviderAccountID` | [`TK_ProviderAccounts`](#tk_provideraccounts).`ID` | `FK_TK_RuntimeCall_ProviderAccount` |
| [`TK_RuntimeCall`](#tk_runtimecall).`ToUserID` | [`TK_SIP_User`](#tk_sip_user).`ID` | `FK_TK_RuntimeCall_ToUser` |
| [`TK_RuntimeRegistration`](#tk_runtimeregistration).`InternalStateID` | [`TK_InternalRegistrationState`](#tk_internalregistrationstate).`ID` | `FK_TK_RuntimeRegistration_InternalState` |
| [`TK_RuntimeRegistration`](#tk_runtimeregistration).`ProviderAccountID` | [`TK_ProviderAccounts`](#tk_provideraccounts).`ID` | `FK_TK_RuntimeRegistration_ProviderAccount` |
| [`TK_RuntimeRegistration`](#tk_runtimeregistration).`ProviderStateID` | [`TK_ProviderRegistrationState`](#tk_providerregistrationstate).`ID` | `FK_TK_RuntimeRegistration_ProviderState` |
| [`TK_RuntimeRegistration`](#tk_runtimeregistration).`UserID` | [`TK_SIP_User`](#tk_sip_user).`ID` | `FK_TK_RuntimeRegistration_User` |
| [`TK_SIP_CallLog`](#tk_sip_calllog).`FromUserID` | [`TK_SIP_User`](#tk_sip_user).`ID` | `FK_TK_SIP_CallLog_FromUser` |
| [`TK_SIP_CallLog`](#tk_sip_calllog).`ToUserID` | [`TK_SIP_User`](#tk_sip_user).`ID` | `FK_TK_SIP_CallLog_ToUser` |
| [`TK_SIP_Registration`](#tk_sip_registration).`UserID` | [`TK_SIP_User`](#tk_sip_user).`ID` | `FK_TK_SIP_Registration_User` |
| [`TK_UserForwarding`](#tk_userforwarding).`ProviderAccountID` | [`TK_ProviderAccounts`](#tk_provideraccounts).`ID` | `FK_TK_UserForwarding_ProviderAccountID` |
| [`TK_UserForwarding`](#tk_userforwarding).`UserID` | [`TK_SIP_User`](#tk_sip_user).`ID` | `FK_TK_UserForwarding_UserID` |

### Zusätzliche Beziehungen über den Code und die Konfiguration

| Quelle | Ziel | Bedeutung / Bedingung |
|---|---|---|
| [`TK_Properties`](#tk_properties).`SIPCodec` | [`TK_AudioCodec`](#tk_audiocodec).`ID` | Codebezug; kein SQL-Fremdschlüssel. LoadServerSettings/Medienauswahl verwenden die Codec-ID. |
| [`TK_Properties`](#tk_properties).`GSMCodec` | [`TK_AudioCodec`](#tk_audiocodec).`ID` | Codebezug; kein SQL-Fremdschlüssel. Zusätzlich prüft die Anwendung die GSM-Bridge-Eignung. |
| [`TK_ExternalNumberRouting`](#tk_externalnumberrouting).`ExternalNumberID` | [`TK_ExternalNumbers`](#tk_externalnumbers).`ID` | Codebezug durch JOIN; kein SQL-Fremdschlüssel. |
| [`TK_ExternalNumberRouting`](#tk_externalnumberrouting).`InternalNumberID` | [`TK_SIP_User`](#tk_sip_user).`ID` | Codebezug durch JOIN; kein SQL-Fremdschlüssel. Gilt auch für FAXUser-Datensätze dieser Benutzertabelle. |
| [`TK_ExternalNumbers`](#tk_externalnumbers).`SourceID` | [`TK_ProviderAccounts`](#tk_provideraccounts).`ID` | Bedingter Codebezug: SourceType='ProviderAccount'; kein SQL-Fremdschlüssel. |
| [`TK_ExternalNumbers`](#tk_externalnumbers).`SourceID` | [`TK_GSM_ModemSimStatus`](#tk_gsm_modemsimstatus).`ID` | Bedingter Codebezug: SourceType='GSMStatus'; NICHT direkt TK_GSM_Modems.ID oder TK_GSM_Sim.ID. |
| [`TK_RuntimeRegistration`](#tk_runtimeregistration).`SourceID` | [`TK_SIP_User`](#tk_sip_user).`ID` | Bedingter Codebezug: RegistrationType='Internal', SourceType='InternalUser'; entspricht dem übergebenen userId. |
| [`TK_RuntimeRegistration`](#tk_runtimeregistration).`SourceID` | [`TK_ProviderAccounts`](#tk_provideraccounts).`ID` | Bedingter Codebezug: RegistrationType='External', SourceType='ProviderAccount'; entspricht providerAccountId. |
| [`TK_Notruf`](#tk_notruf).`ProviderAccountID` | [`TK_ProviderAccounts`](#tk_provideraccounts).`ID` | Optionaler fachlicher Providerbezug; kein SQL-Fremdschlüssel im aktuellen Schema. |
| [`TK_DEAreaCode`](#tk_deareacode).`CountryISO2` | [`TK_CountryPrefix`](#tk_countryprefix).`ISO2` | Fachliche Länderzuordnung über ISO2, kein FK. ISO2 ist wegen geteilter Vorwahlpläne kein garantierter eindeutiger Zielschlüssel. |
| [`TK_SIP_ServiceLeg`](#tk_sip_serviceleg).`MenuParentCode` | [`TK_SIP_ServiceLeg`](#tk_sip_serviceleg).`ServiceCode` | Fachliche Menü-Elternzuordnung per Schlüsseltext; kein FK. Ein vorhandenes Feld ist kein Nachweis, dass jeder aktuelle Pfad es auswertet. |
| [`TK_SIP_ServiceLeg`](#tk_sip_serviceleg).`MenuRootNumber` | [`TK_SIP_ServiceLeg`](#tk_sip_serviceleg).`InternalNumber` | Fachliche Menüwurzel-Zuordnung per Servicenummer, aktuell *100; kein FK. |
| [`TK_RuntimeCall`](#tk_runtimecall).`SessionID` | [`TK_SIP_CallLog`](#tk_sip_calllog).`SipCallID` | Logische Zuordnung im aktuellen TKBase_RuntimeCallManager: CreateCallLog erhält sessionId als sipCallId. Kein FK und keine erzwungene 1:1-Kardinalität. |
| [`TK_RuntimeCall`](#tk_runtimecall).`BridgeID` | [`TK_SIP_CallLog`](#tk_sip_calllog).`BridgeID` | Logische Gesprächskorrelation über dieselbe Bridge-GUID; kein FK und keine eigenständige Bridge-Tabelle. |
| [`TK_RuntimeCall`](#tk_runtimecall).`LegACallID` | [`TK_SIP_CallLog`](#tk_sip_calllog).`LegACallID` | Logische Korrelation des Anrufer-Legs zwischen Live-Sicht und Historie; kein FK. |
| [`TK_RuntimeCall`](#tk_runtimecall).`LegBCallID` | [`TK_SIP_CallLog`](#tk_sip_calllog).`LegBCallID` | Logische Korrelation des Ziel-Legs zwischen Live-Sicht und Historie; kein FK. |

### Besonderheiten bei IDs und Quellen

`TK_ExternalNumbers.SourceID` verweist bei `ProviderAccount` auf das Providerkonto, bei `GSMStatus` auf die Modem-/SIM-Statuszeile. Um von einer GSM-Nummer zum physischen Modem oder zur SIM zu gelangen, folgt man erst `SourceID → TK_GSM_ModemSimStatus.ID` und danach `ModemID` bzw. `SimID`.

`TK_RuntimeRegistration.SourceID` verweist bei `InternalUser` auf den Benutzer und bei `ProviderAccount` auf das Providerkonto. Die Spalten `UserID`, `ProviderAccountID`, `InternalStateID` und `ProviderStateID` sind zusätzliche, jeweils eigenständige FK-Bezüge. Aus der bloßen Existenz einer FK-Spalte folgt nicht, dass jeder Schreibpfad sie befüllt.

`TK_ExternalNumberRouting.InternalNumberID` ist die Benutzer-ID aus `TK_SIP_User`, nicht der Text der Nebenstelle und keine ServiceLeg-ID. Die Textspalten `InterneNr`/`ExterneNr` ersetzen die eigentlichen ID-Bezüge nicht.

`TK_UserForwarding.Target`, `TK_Notruf.TargetValue`, Rufnummernfelder, SIP-URIs und Werte innerhalb von `JsonConfig` sind fachliche Texte. Ihre Interpretation hängt vom Typ und der Bereichslogik ab; sie dürfen nicht pauschal als IDs einer bestimmten Tabelle dokumentiert werden.

## Tabellen im Einzelnen

## TK_SIP_User

### Beschreibung

**Zweck heute:** Interne SIP- und Faxbenutzer mit Nebenstellen, verschlüsselten Passwörtern und Berechtigungen.

**Verwendung:** Stammdaten; GUI verwaltet, Service liest für Anmeldung, Zielauflösung und Rechteprüfung.

**Erstinstallation:** Fünf lokale SIP-Benutzer als Installationsvorgabe; für automatisches internes Starten wird mindestens ein aktiver Benutzer benötigt.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `AdsSID` | `[nvarchar](128) NULL` | Optionale Windows-/Active-Directory-SID des zugeordneten Benutzers; externer Verzeichnisbezug, keine ID einer SQL-Tabelle. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `Username` | `[nvarchar](100) NOT NULL` | Anmeldename des internen SIP-Benutzers; nicht zwangsläufig mit seiner Nebenstellennummer identisch. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Telefonnummer` | `[nvarchar](50) NOT NULL` | Rufnummer des betreffenden Benutzers bzw. der SIM; kein numerischer Datenbankschlüssel. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Password` | `[nvarchar](255) NOT NULL` | Mit sipcryptlib.SipCryptor.EncryptFixed verschlüsseltes SIP-Passwort. Die Authentifizierung entschlüsselt mit DecryptFixed; Klartext ist kein gültiger Installationswert. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `DisplayName` | `[nvarchar](100) NULL` | Lesbarer Anzeigename für Oberfläche oder fachliche Darstellung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Email` | `[nvarchar](255) NULL` | Optionale E-Mail-Adresse des Benutzers. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Description` | `[nvarchar](255) NULL` | Zusätzliche textliche Beschreibung des Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UserType` | `[nvarchar](20) NOT NULL` | Benutzerart, insbesondere SIPUser oder FAXUser; die Anwendung verwendet den Typ bei Zielklassifikation und Rechteverwaltung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `AdsProfilePath` | `[nvarchar](260) NULL` | Optionaler ADS-/Windows-Profilpfad des Benutzers, insbesondere für den benutzerbezogenen Medienpfad. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Active` | `[bit] NOT NULL` | Aktivierungskennzeichen des Datensatzes: 1 aktiv, 0 deaktiviert. Kein Ersatz für Registrierungs- oder Erreichbarkeitsstatus. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CanDialInternal` | `[bit] NOT NULL` | Erlaubt dem Benutzer interne ausgehende Gespräche. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CanDialExternal` | `[bit] NOT NULL` | Erlaubt dem Benutzer externe ausgehende Gespräche. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CanReceiveInternal` | `[bit] NOT NULL` | Erlaubt dem Benutzer das Annehmen interner Gespräche. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CanReceiveExternal` | `[bit] NOT NULL` | Erlaubt dem Benutzer das Annehmen externer Gespräche. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SortOrder` | `[int] NOT NULL` | Gespeicherte Sortierreihenfolge für Listen bzw. Auswahl; Bedeutung der Reihenfolge wird vom jeweiligen Ladepfad bestimmt. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](7) NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK__TK.SIP.U__3214EC2711D05FB9`: PRIMARY KEY auf `[ID] ASC`.
- `UQ_TK_SIP_User_Telefonnummer`: UNIQUE auf `[Telefonnummer] ASC` (separat im SQL angelegt).
- `UQ_TK_SIP_User_Username`: UNIQUE auf `[Username] ASC` (separat im SQL angelegt).
- Standardwert `UserType`: `('User')` (`DF_TK_SIP_User_UserType`).
- Standardwert `Active`: `((1))` (`DF__TK.SIP.Us__Activ__5BE2A6F2`).
- Standardwert `CanDialInternal`: `((1))` (`DF_TK_SIP_User_CanDialInternal`).
- Standardwert `CanDialExternal`: `((1))` (`DF_TK_SIP_User_CanDialExternal`).
- Standardwert `CanReceiveInternal`: `((1))` (`DF_TK_SIP_User_CanReceiveInternal`).
- Standardwert `CanReceiveExternal`: `((1))` (`DF_TK_SIP_User_CanReceiveExternal`).
- Standardwert `SortOrder`: `((0))` (`DF_TK_SIP_User_SortOrder`).
- Standardwert `CreatedAt`: `(sysdatetime())` (`DF__TK.SIP.Us__Creat__5CD6CB2B`).

### Verbindungen zu anderen Tabellen

- Eingehend, **FK**: [`TK_InternalRegistrationState`](#tk_internalregistrationstate).`UserID` → `ID` (`FK_TK_InternalRegistrationState_User`).
- Eingehend, **FK**: [`TK_MailBoxMessage`](#tk_mailboxmessage).`UserID` → `ID` (`FK_TK_MailBoxMessage_UserID`).
- Eingehend, **FK**: [`TK_RuntimeCall`](#tk_runtimecall).`FromUserID` → `ID` (`FK_TK_RuntimeCall_FromUser`).
- Eingehend, **FK**: [`TK_RuntimeCall`](#tk_runtimecall).`ToUserID` → `ID` (`FK_TK_RuntimeCall_ToUser`).
- Eingehend, **FK**: [`TK_RuntimeRegistration`](#tk_runtimeregistration).`UserID` → `ID` (`FK_TK_RuntimeRegistration_User`).
- Eingehend, **FK**: [`TK_SIP_CallLog`](#tk_sip_calllog).`FromUserID` → `ID` (`FK_TK_SIP_CallLog_FromUser`).
- Eingehend, **FK**: [`TK_SIP_CallLog`](#tk_sip_calllog).`ToUserID` → `ID` (`FK_TK_SIP_CallLog_ToUser`).
- Eingehend, **FK**: [`TK_SIP_Registration`](#tk_sip_registration).`UserID` → `ID` (`FK_TK_SIP_Registration_User`).
- Eingehend, **FK**: [`TK_UserForwarding`](#tk_userforwarding).`UserID` → `ID` (`FK_TK_UserForwarding_UserID`).
- Eingehend, fachlich/Code: [`TK_ExternalNumberRouting`](#tk_externalnumberrouting).`InternalNumberID` → `ID`. Codebezug durch JOIN; kein SQL-Fremdschlüssel. Gilt auch für FAXUser-Datensätze dieser Benutzertabelle.
- Eingehend, fachlich/Code: [`TK_RuntimeRegistration`](#tk_runtimeregistration).`SourceID` → `ID`. Bedingter Codebezug: RegistrationType='Internal', SourceType='InternalUser'; entspricht dem übergebenen userId.

## TK_SIP_Registration

### Beschreibung

**Zweck heute:** Klassische Registrierungstabelle mit Benutzer, Kontakt und Ablaufzeit.

**Verwendung:** Im geprüften aktuellen SRC kein regulärer Lese-/Schreibpfad auf diese Tabelle gefunden; der Name steht noch in einer Tabellen-Allowlist. Aktive Routingabfragen verwenden TK_RuntimeRegistration.

**Erstinstallation:** Leer. Tabelle bleibt als vorhandenes Schemaobjekt erhalten; keine Stilllegung oder Migration durch diese Dokumentation.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `UserID` | `[int] NOT NULL` | ID des zugeordneten internen Benutzers aus TK_SIP_User.ID; die Beziehung ist in der letzten Spalte ausgewiesen. | **FK:** [`TK_SIP_User`](#tk_sip_user).`ID`; `FK_TK_SIP_Registration_User`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `ContactURI` | `[nvarchar](512) NOT NULL` | SIP-Contact-Adresse des registrierten Endgeräts oder Providerkontakts; Ziel für die weitere Signalisierung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RemoteEndPoint` | `[nvarchar](128) NOT NULL` | Gespeicherter entfernter Netzwerkendpunkt mit IP/Port bzw. SIP-Endpunktdarstellung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Transport` | `[nvarchar](16) NULL` | Transportprotokoll der betreffenden SIP-Verbindung, beispielsweise UDP, TCP oder TLS. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CallID` | `[nvarchar](255) NOT NULL` | SIP-Call-ID des Registrierungsvorgangs; kein Fremdschlüssel auf die numerische ID einer Gesprächstabelle. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `UserAgent` | `[nvarchar](255) NULL` | Vom SIP-Gegenüber gemeldeter User-Agent bzw. Geräte-/Softwarekennung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ExpiresAt` | `[datetime2](7) NOT NULL` | Ablaufzeit der Registrierung; NULL ist nur zulässig, wenn die Spaltendefinition dies erlaubt. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastSeen` | `[datetime2](7) NOT NULL` | Zeitpunkt der letzten Beobachtung/Aktualisierung des Geräts oder der Registrierung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Active` | `[bit] NOT NULL` | Aktivkennzeichen dieser klassischen Registrierung; nicht der administrative Active-Schalter des Benutzers. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK_TK_SIP_Registration`: PRIMARY KEY auf `[ID] ASC`.
- `IX_TK_SIP_Registration_CallID`: INDEX auf `[CallID] ASC`.
- `IX_TK_SIP_Registration_ExpiresAt`: INDEX auf `[ExpiresAt] ASC`.
- `IX_TK_SIP_Registration_User_Active`: INDEX auf `[UserID] ASC, [Active] ASC`.
- `IX_TK_SIP_Registration_UserID`: INDEX auf `[UserID] ASC`.
- Standardwert `LastSeen`: `(sysdatetime())` (`DF_TK_SIP_Registration_LastSeen`).
- Standardwert `Active`: `((1))` (`DF_TK_SIP_Registration_Active`).

### Verbindungen zu anderen Tabellen

- Ausgehend, **FK**: `UserID` → [`TK_SIP_User`](#tk_sip_user).`ID` (`FK_TK_SIP_Registration_User`).

## TK_InternalRegistrationState

### Beschreibung

**Zweck heute:** Detailzustand interner SIP-Registrierungen.

**Verwendung:** Service pflegt pro Registrierung die Kontakt-, Ablauf- und Erreichbarkeitsdaten; TK_RuntimeRegistration kann auf diese Datensätze verweisen.

**Erstinstallation:** Leer; entsteht im Betrieb.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `GUID` | `[uniqueidentifier] NOT NULL` | Vom Anwendungscode übergebene Registrierungs-GUID. Der aktuelle interne Pfad bildet sie deterministisch aus Benutzer-, Transport-, Endpunkt- und Kontaktdaten. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `UserID` | `[int] NOT NULL` | ID des zugeordneten internen Benutzers aus TK_SIP_User.ID; die Beziehung ist in der letzten Spalte ausgewiesen. | **FK:** [`TK_SIP_User`](#tk_sip_user).`ID`; `FK_TK_InternalRegistrationState_User`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `Username` | `[nvarchar](100) NOT NULL` | Anmeldename des internen SIP-Benutzers; nicht zwangsläufig mit seiner Nebenstellennummer identisch. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ContactURI` | `[nvarchar](512) NOT NULL` | SIP-Contact-Adresse des registrierten Endgeräts oder Providerkontakts; Ziel für die weitere Signalisierung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RemoteEndPoint` | `[nvarchar](255) NOT NULL` | Gespeicherter entfernter Netzwerkendpunkt mit IP/Port bzw. SIP-Endpunktdarstellung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Transport` | `[nvarchar](16) NULL` | Transportprotokoll der betreffenden SIP-Verbindung, beispielsweise UDP, TCP oder TLS. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CallID` | `[nvarchar](255) NOT NULL` | SIP-Call-ID des Registrierungsvorgangs; kein Fremdschlüssel auf die numerische ID einer Gesprächstabelle. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `UserAgent` | `[nvarchar](255) NULL` | Vom SIP-Gegenüber gemeldeter User-Agent bzw. Geräte-/Softwarekennung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ExpiresAt` | `[datetime2](7) NOT NULL` | Ablaufzeit der Registrierung; NULL ist nur zulässig, wenn die Spaltendefinition dies erlaubt. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastSeen` | `[datetime2](7) NOT NULL` | Zeitpunkt der letzten Beobachtung/Aktualisierung des Geräts oder der Registrierung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `IsRegistered` | `[bit] NOT NULL` | Gespeicherter Registrierungsstatus: 1 registriert, 0 nicht registriert. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `IsReachable` | `[bit] NOT NULL` | Gespeicherte Erreichbarkeit: 1 erreichbar, 0 nicht erreichbar; unabhängig vom administrativen Active-Schalter. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastError` | `[nvarchar](max) NULL` | Zuletzt gespeicherter Fehlertext für das betreffende Objekt; NULL bedeutet kein eingetragener Fehler. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](7) NOT NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK_TK_InternalRegistrationState`: PRIMARY KEY auf `[ID] ASC`.
- `UQ_TK_InternalRegistrationState_GUID`: UNIQUE auf `[GUID] ASC` (separat im SQL angelegt).
- `IX_TK_InternalRegistrationState_CallID`: INDEX auf `[CallID] ASC`.
- `IX_TK_InternalRegistrationState_UserID_Registered`: INDEX auf `[UserID] ASC, [IsRegistered] ASC, [ExpiresAt] ASC`.
- Standardwert `GUID`: `(newid())` (`DF_TK_InternalRegistrationState_GUID`).
- Standardwert `IsRegistered`: `((1))` (`DF_TK_InternalRegistrationState_IsRegistered`).
- Standardwert `IsReachable`: `((1))` (`DF_TK_InternalRegistrationState_IsReachable`).
- Standardwert `CreatedAt`: `(sysdatetime())` (`DF_TK_InternalRegistrationState_CreatedAt`).
- Standardwert `UpdatedAt`: `(sysdatetime())` (`DF_TK_InternalRegistrationState_UpdatedAt`).

### Verbindungen zu anderen Tabellen

- Ausgehend, **FK**: `UserID` → [`TK_SIP_User`](#tk_sip_user).`ID` (`FK_TK_InternalRegistrationState_User`).
- Eingehend, **FK**: [`TK_RuntimeRegistration`](#tk_runtimeregistration).`InternalStateID` → `ID` (`FK_TK_RuntimeRegistration_InternalState`).

## TK_RuntimeRegistration

### Beschreibung

**Zweck heute:** Vereinheitlichte Live-Sicht auf interne und externe SIP-Registrierungen.

**Verwendung:** Service schreibt, GUI und Routing lesen. Quelltyp/Quell-ID sowie optionale konkrete Zustands-IDs ordnen die Live-Sicht zu.

**Erstinstallation:** Leer; entsteht durch Registrierungen.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `GUID` | `[uniqueidentifier] NOT NULL` | Interne Registrierung: übernommene Registrierungs-GUID; externe Registrierung: beim Anlegen NEWID(). Nicht pauschal ein Fremdschlüssel. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `RegistrationType` | `[nvarchar](16) NOT NULL` | Art der Laufzeitregistrierung; laut Schema Internal oder External. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SourceType` | `[nvarchar](32) NOT NULL` | Typ des Quellobjekts; bestimmt zusammen mit SourceID die tatsächliche Herkunft. Zuordnung siehe Beziehungsspalte und Übersicht. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SourceID` | `[int] NOT NULL` | Numerische ID des Quellobjekts; die Zieltabelle hängt von SourceType ab. Keine universelle Benutzer-ID. | [`TK_SIP_User`](#tk_sip_user).`ID`: Bedingter Codebezug: RegistrationType='Internal', SourceType='InternalUser'; entspricht dem übergebenen userId. [`TK_ProviderAccounts`](#tk_provideraccounts).`ID`: Bedingter Codebezug: RegistrationType='External', SourceType='ProviderAccount'; entspricht providerAccountId. |
| `UserID` | `[int] NULL` | ID des zugeordneten internen Benutzers aus TK_SIP_User.ID; die Beziehung ist in der letzten Spalte ausgewiesen. | **FK:** [`TK_SIP_User`](#tk_sip_user).`ID`; `FK_TK_RuntimeRegistration_User`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `ProviderAccountID` | `[int] NULL` | Zuordnung zu TK_ProviderAccounts.ID, nicht zur Provider-Rufnummer oder SIP-Benutzerkennung. | **FK:** [`TK_ProviderAccounts`](#tk_provideraccounts).`ID`; `FK_TK_RuntimeRegistration_ProviderAccount`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `InternalStateID` | `[int] NULL` | Optionaler Bezug auf TK_InternalRegistrationState.ID. | **FK:** [`TK_InternalRegistrationState`](#tk_internalregistrationstate).`ID`; `FK_TK_RuntimeRegistration_InternalState`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `ProviderStateID` | `[int] NULL` | Optionaler Bezug auf TK_ProviderRegistrationState.ID; der FK ist vorhanden, der aktuelle Provider-Upsert befüllt die Spalte nicht ausdrücklich. | **FK:** [`TK_ProviderRegistrationState`](#tk_providerregistrationstate).`ID`; `FK_TK_RuntimeRegistration_ProviderState`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `DisplayName` | `[nvarchar](100) NULL` | Lesbarer Anzeigename für Oberfläche oder fachliche Darstellung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `AorUri` | `[nvarchar](512) NULL` | Address-of-Record des SIP-Teilnehmers/Providerkontos. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ContactURI` | `[nvarchar](512) NULL` | SIP-Contact-Adresse des registrierten Endgeräts oder Providerkontakts; Ziel für die weitere Signalisierung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RemoteEndPoint` | `[nvarchar](255) NULL` | Gespeicherter entfernter Netzwerkendpunkt mit IP/Port bzw. SIP-Endpunktdarstellung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Transport` | `[nvarchar](16) NULL` | Transportprotokoll der betreffenden SIP-Verbindung, beispielsweise UDP, TCP oder TLS. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LocalIPAddress` | `[nvarchar](64) NULL` | Lokale IP-Adresse des verwendeten SIP-Transports. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LocalPort` | `[int] NULL` | Lokaler Port des verwendeten SIP-Transports. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RequestUri` | `[nvarchar](512) NULL` | SIP-Ziel-URI des gespeicherten Requests bzw. Registrierungskontexts. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CallID` | `[nvarchar](255) NULL` | SIP-Call-ID des Registrierungsvorgangs; kein Fremdschlüssel auf die numerische ID einer Gesprächstabelle. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `UserAgent` | `[nvarchar](255) NULL` | Vom SIP-Gegenüber gemeldeter User-Agent bzw. Geräte-/Softwarekennung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ServiceRoute` | `[nvarchar](max) NULL` | Gespeicherte SIP-Service-Route aus der Registrierung bzw. deren Laufzeitsicht. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RouteSet` | `[nvarchar](max) NULL` | Gespeicherte SIP-Routeninformationen; kein Verweis auf TK_ExternalNumberRouting.ID. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `AssociatedUri` | `[nvarchar](512) NULL` | Vom Provider gelieferte zugeordnete SIP-Identität/URI. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastResolvedIP` | `[nvarchar](64) NULL` | Zuletzt aufgelöste IP-Adresse des Provider-/SIP-Ziels. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `StatusCode` | `[int] NULL` | Numerischer SIP-Statuscode des Ereignisses bzw. der Laufzeitregistrierung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `StatusText` | `[nvarchar](255) NULL` | Text zum SIP-Statuscode. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ExpiresAt` | `[datetime2](7) NULL` | Ablaufzeit der Registrierung; NULL ist nur zulässig, wenn die Spaltendefinition dies erlaubt. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastSeen` | `[datetime2](7) NOT NULL` | Zeitpunkt der letzten Beobachtung/Aktualisierung des Geräts oder der Registrierung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `IsRegistered` | `[bit] NOT NULL` | Gespeicherter Registrierungsstatus: 1 registriert, 0 nicht registriert. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `IsReachable` | `[bit] NOT NULL` | Gespeicherte Erreichbarkeit: 1 erreichbar, 0 nicht erreichbar; unabhängig vom administrativen Active-Schalter. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastError` | `[nvarchar](max) NULL` | Zuletzt gespeicherter Fehlertext für das betreffende Objekt; NULL bedeutet kein eingetragener Fehler. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](7) NOT NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK_TK_RuntimeRegistration`: PRIMARY KEY auf `[ID] ASC`.
- `UQ_TK_RuntimeRegistration_GUID`: UNIQUE auf `[GUID] ASC` (separat im SQL angelegt).
- `IX_TK_RuntimeRegistration_CallID`: INDEX auf `[CallID] ASC`.
- `IX_TK_RuntimeRegistration_Type_Source`: INDEX auf `[RegistrationType] ASC, [SourceID] ASC, [IsRegistered] ASC`.
- Standardwert `GUID`: `(newid())` (`DF_TK_RuntimeRegistration_GUID`).
- Standardwert `IsRegistered`: `((0))` (`DF_TK_RuntimeRegistration_IsRegistered`).
- Standardwert `IsReachable`: `((0))` (`DF_TK_RuntimeRegistration_IsReachable`).
- Standardwert `CreatedAt`: `(sysdatetime())` (`DF_TK_RuntimeRegistration_CreatedAt`).
- Standardwert `UpdatedAt`: `(sysdatetime())` (`DF_TK_RuntimeRegistration_UpdatedAt`).
- Prüfung `CHK_TK_RuntimeRegistration_Type`: `(([RegistrationType]='External' OR [RegistrationType]='Internal'))`.

### Verbindungen zu anderen Tabellen

- Ausgehend, **FK**: `InternalStateID` → [`TK_InternalRegistrationState`](#tk_internalregistrationstate).`ID` (`FK_TK_RuntimeRegistration_InternalState`).
- Ausgehend, **FK**: `ProviderAccountID` → [`TK_ProviderAccounts`](#tk_provideraccounts).`ID` (`FK_TK_RuntimeRegistration_ProviderAccount`).
- Ausgehend, **FK**: `ProviderStateID` → [`TK_ProviderRegistrationState`](#tk_providerregistrationstate).`ID` (`FK_TK_RuntimeRegistration_ProviderState`).
- Ausgehend, **FK**: `UserID` → [`TK_SIP_User`](#tk_sip_user).`ID` (`FK_TK_RuntimeRegistration_User`).
- Ausgehend, fachlich/Code: `SourceID` → [`TK_SIP_User`](#tk_sip_user).`ID`. Bedingter Codebezug: RegistrationType='Internal', SourceType='InternalUser'; entspricht dem übergebenen userId.
- Ausgehend, fachlich/Code: `SourceID` → [`TK_ProviderAccounts`](#tk_provideraccounts).`ID`. Bedingter Codebezug: RegistrationType='External', SourceType='ProviderAccount'; entspricht providerAccountId.

## TK_ProviderAccounts

### Beschreibung

**Zweck heute:** Externe SIP-Providerkonten mit Authentifizierungs-, Transport- und Rufnummerneinstellungen.

**Verwendung:** Stammdaten für SIPExt/Providerregistrierung und externe Gesprächswege.

**Erstinstallation:** Bewusst leer; jeder Betreiber legt seine eigenen Providerkonten an.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `ProviderName` | `[nvarchar](100) NOT NULL` | Name des SIP-Providers bzw. Mobilfunkanbieters. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SipAuthUser` | `[nvarchar](100) NOT NULL` | Authentifizierungsname für SIP-Digest beim Provider; kann vom SIPUser abweichen. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SIPUser` | `[nvarchar](100) NOT NULL` | SIP-Benutzerkennung des Providerkontos. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SIPPasswort` | `[nvarchar](255) NOT NULL` | Verschlüsseltes Providerpasswort; die Providerlogik verwendet sipcryptlib.SipCryptor.DecryptFixed zum Lesen. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SIPServer` | `[nvarchar](255) NOT NULL` | Konfigurierter SIP-Server des Providerkontos. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SIPRegistrar` | `[nvarchar](255) NULL` | Provider-Registrar für REGISTER-Anmeldungen. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SIPProxy` | `[nvarchar](255) NULL` | Optionaler Proxy des Providerkontos. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SIPOutboundProxy` | `[nvarchar](255) NULL` | Optionaler Outbound-Proxy des Providerkontos. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SIPRealm` | `[nvarchar](255) NULL` | SIP-Digest-Realm des Providerkontos, soweit vorkonfiguriert. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SIPSTUNServer` | `[nvarchar](255) NULL` | Optionaler STUN-Server des Providerkontos für NAT-bezogene Konfiguration. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SIPProtokoll` | `[nvarchar](10) NOT NULL` | Für das Providerkonto konfiguriertes SIP-Transportprotokoll. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SIPRTPPort` | `[int] NULL` | Optionaler providerbezogener RTP-Port-Konfigurationswert; nicht mit dem globalen RTP-Portbereich gleichzusetzen. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SIPPort` | `[int] NOT NULL` | SIP-Port des Providerkontos. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SRTP` | `[bit] NOT NULL` | SRTP-Einstellung des Providerkontos: aktiviert/deaktiviert. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SIPNr` | `[nvarchar](50) NULL` | Dem Providerkonto zugeordnete externe Rufnummer; wird für Nummernauflösung und externe Nummernsynchronisation verwendet. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Active` | `[bit] NOT NULL` | Aktivierungskennzeichen des Datensatzes: 1 aktiv, 0 deaktiviert. Kein Ersatz für Registrierungs- oder Erreichbarkeitsstatus. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](7) NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:


### Verbindungen zu anderen Tabellen

- Eingehend, **FK**: [`TK_ProviderRegistrationLog`](#tk_providerregistrationlog).`ProviderAccountID` → `ID` (`FK_TK_ProviderRegistrationLog_ProviderAccountID`).
- Eingehend, **FK**: [`TK_ProviderRegistrationState`](#tk_providerregistrationstate).`ProviderAccountID` → `ID` (`FK_TK_ProviderRegistrationState_ProviderAccountID`).
- Eingehend, **FK**: [`TK_RuntimeCall`](#tk_runtimecall).`ProviderAccountID` → `ID` (`FK_TK_RuntimeCall_ProviderAccount`).
- Eingehend, **FK**: [`TK_RuntimeRegistration`](#tk_runtimeregistration).`ProviderAccountID` → `ID` (`FK_TK_RuntimeRegistration_ProviderAccount`).
- Eingehend, **FK**: [`TK_UserForwarding`](#tk_userforwarding).`ProviderAccountID` → `ID` (`FK_TK_UserForwarding_ProviderAccountID`).
- Eingehend, fachlich/Code: [`TK_ExternalNumbers`](#tk_externalnumbers).`SourceID` → `ID`. Bedingter Codebezug: SourceType='ProviderAccount'; kein SQL-Fremdschlüssel.
- Eingehend, fachlich/Code: [`TK_RuntimeRegistration`](#tk_runtimeregistration).`SourceID` → `ID`. Bedingter Codebezug: RegistrationType='External', SourceType='ProviderAccount'; entspricht providerAccountId.
- Eingehend, fachlich/Code: [`TK_Notruf`](#tk_notruf).`ProviderAccountID` → `ID`. Optionaler fachlicher Providerbezug; kein SQL-Fremdschlüssel im aktuellen Schema.

## TK_ProviderRegistrationState

### Beschreibung

**Zweck heute:** Zuletzt gespeicherter Registrierungszustand eines Providerkontos.

**Verwendung:** Service schreibt Registrierungsstatus, Ablaufzeiten und vom Provider gelieferte Routeninformationen.

**Erstinstallation:** Leer; wird im Betrieb gefüllt.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `ProviderAccountID` | `[int] NOT NULL` | Zuordnung zu TK_ProviderAccounts.ID, nicht zur Provider-Rufnummer oder SIP-Benutzerkennung. | **FK:** [`TK_ProviderAccounts`](#tk_provideraccounts).`ID`; `FK_TK_ProviderRegistrationState_ProviderAccountID`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `IsRegistered` | `[bit] NOT NULL` | Gespeicherter Registrierungsstatus: 1 registriert, 0 nicht registriert. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastRegisterAt` | `[datetime2](0) NULL` | Zeitpunkt des zuletzt gespeicherten REGISTER-Vorgangs dieses Providerkontos. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ExpiresAt` | `[datetime2](0) NULL` | Ablaufzeit der Registrierung; NULL ist nur zulässig, wenn die Spaltendefinition dies erlaubt. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastRemoteEndPoint` | `[nvarchar](255) NULL` | Zuletzt verwendeter/gemeldeter entfernter SIP-Netzwerkendpunkt. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastResolvedIP` | `[nvarchar](64) NULL` | Zuletzt aufgelöste IP-Adresse des Provider-/SIP-Ziels. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastTransport` | `[nvarchar](16) NULL` | Zuletzt verwendetes SIP-Transportprotokoll. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastRequestUri` | `[nvarchar](512) NULL` | Zuletzt verwendete Request-URI der Providerregistrierung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ServiceRoute` | `[nvarchar](max) NULL` | Gespeicherte SIP-Service-Route aus der Registrierung bzw. deren Laufzeitsicht. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RouteSet` | `[nvarchar](max) NULL` | Gespeicherte SIP-Routeninformationen; kein Verweis auf TK_ExternalNumberRouting.ID. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `AssociatedUri` | `[nvarchar](512) NULL` | Vom Provider gelieferte zugeordnete SIP-Identität/URI. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastCallID` | `[nvarchar](255) NULL` | Zuletzt gespeicherte SIP-Call-ID der Providerregistrierung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastStatusCode` | `[int] NULL` | Zuletzt gespeicherter numerischer SIP-Statuscode. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastStatusText` | `[nvarchar](255) NULL` | Text zum zuletzt gespeicherten SIP-Status. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastError` | `[nvarchar](max) NULL` | Zuletzt gespeicherter Fehlertext für das betreffende Objekt; NULL bedeutet kein eingetragener Fehler. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](0) NOT NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK_TK_ProviderRegistrationState`: PRIMARY KEY auf `[ID] ASC`.
- `UQ_TK_ProviderRegistrationState_ProviderAccountID`: UNIQUE auf `[ProviderAccountID] ASC` (separat im SQL angelegt).
- `IX_TK_ProviderRegistrationState_IsRegistered`: INDEX auf `[IsRegistered] ASC, [ProviderAccountID] ASC`.
- Standardwert `IsRegistered`: `((0))` (`DF_TK_ProviderRegistrationState_IsRegistered`).
- Standardwert `UpdatedAt`: `(sysutcdatetime())` (`DF_TK_ProviderRegistrationState_UpdatedAt`).

### Verbindungen zu anderen Tabellen

- Ausgehend, **FK**: `ProviderAccountID` → [`TK_ProviderAccounts`](#tk_provideraccounts).`ID` (`FK_TK_ProviderRegistrationState_ProviderAccountID`).
- Eingehend, **FK**: [`TK_RuntimeRegistration`](#tk_runtimeregistration).`ProviderStateID` → `ID` (`FK_TK_RuntimeRegistration_ProviderState`).

## TK_ProviderRegistrationLog

### Beschreibung

**Zweck heute:** Ereignisprotokoll der Providerregistrierung einschließlich SIP-Rohtexten.

**Verwendung:** Diagnosehistorie; nicht mit TK_RuntimeRegistration oder dem administrativen Providerkonto gleichzusetzen.

**Erstinstallation:** Leer; keine Betriebsprotokolle in der Installation.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[bigint] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `ProviderAccountID` | `[int] NOT NULL` | Zuordnung zu TK_ProviderAccounts.ID, nicht zur Provider-Rufnummer oder SIP-Benutzerkennung. | **FK:** [`TK_ProviderAccounts`](#tk_provideraccounts).`ID`; `FK_TK_ProviderRegistrationLog_ProviderAccountID`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `EventType` | `[nvarchar](64) NOT NULL` | Art des protokollierten Providerereignisses, beispielsweise Registrierung, Antwort oder Fehler. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `StatusCode` | `[int] NULL` | Numerischer SIP-Statuscode des Ereignisses bzw. der Laufzeitregistrierung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `StatusText` | `[nvarchar](255) NULL` | Text zum SIP-Statuscode. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RemoteEndPoint` | `[nvarchar](255) NULL` | Gespeicherter entfernter Netzwerkendpunkt mit IP/Port bzw. SIP-Endpunktdarstellung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RequestUri` | `[nvarchar](512) NULL` | SIP-Ziel-URI des gespeicherten Requests bzw. Registrierungskontexts. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Transport` | `[nvarchar](16) NULL` | Transportprotokoll der betreffenden SIP-Verbindung, beispielsweise UDP, TCP oder TLS. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CallID` | `[nvarchar](255) NULL` | SIP-Call-ID des Registrierungsvorgangs; kein Fremdschlüssel auf die numerische ID einer Gesprächstabelle. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `RawRequest` | `[nvarchar](max) NULL` | Gespeicherter SIP-Request im Rohtext; Diagnoseinhalt, keine Fremdschlüsselstruktur. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RawResponse` | `[nvarchar](max) NULL` | Gespeicherte SIP-Antwort im Rohtext. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ErrorText` | `[nvarchar](max) NULL` | Fehlerbeschreibung des protokollierten Ereignisses. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](0) NOT NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK_TK_ProviderRegistrationLog`: PRIMARY KEY auf `[ID] ASC`.
- `IX_TK_ProviderRegistrationLog_ProviderAccountID_CreatedAt`: INDEX auf `[ProviderAccountID] ASC, [CreatedAt] DESC`.
- Standardwert `CreatedAt`: `(sysutcdatetime())` (`DF_TK_ProviderRegistrationLog_CreatedAt`).

### Verbindungen zu anderen Tabellen

- Ausgehend, **FK**: `ProviderAccountID` → [`TK_ProviderAccounts`](#tk_provideraccounts).`ID` (`FK_TK_ProviderRegistrationLog_ProviderAccountID`).

## TK_SIP_CallLog

### Beschreibung

**Zweck heute:** Gesprächshistorie für interne, externe, GSM-, Fax- und Servicegespräche.

**Verwendung:** Service schreibt Beginn, Status, Annahme, Ende und SIP-Diagnosedaten; GUI liest die Historie.

**Erstinstallation:** Leer; entsteht während des Betriebs.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `SipCallID` | `[nvarchar](255) NULL` | Fachlicher Such-/Zuordnungsschlüssel der Gesprächshistorie. Der aktuelle RuntimeCallManager übergibt beim Anlegen die SessionID; daher nicht pauschal mit der reinen SIP-Leg-A-Call-ID gleichzusetzen. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `FromUserID` | `[int] NULL` | Optionaler interner Benutzer des Anrufers: TK_SIP_User.ID; externe Anrufer müssen keinen internen Benutzer besitzen. | **FK:** [`TK_SIP_User`](#tk_sip_user).`ID`; `FK_TK_SIP_CallLog_FromUser`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `ToUserID` | `[int] NULL` | Optionaler interner Benutzer des Angerufenen: TK_SIP_User.ID; Service-/externe Ziele müssen keinen internen Benutzer besitzen. | **FK:** [`TK_SIP_User`](#tk_sip_user).`ID`; `FK_TK_SIP_CallLog_ToUser`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `FromNumber` | `[nvarchar](50) NULL` | Rufnummer/Identität des Anrufers als Text. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ToNumber` | `[nvarchar](50) NULL` | Rufnummer/Identität des angerufenen Ziels als Text. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Direction` | `[nvarchar](16) NULL` | Gespeicherte Gesprächsrichtung der Historie als Text. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Status` | `[nvarchar](32) NULL` | Gespeicherter Status des betreffenden Gesprächs/Datensatzes; die zulässigen Werte unterscheiden sich je Tabelle. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `StartTime` | `[datetime2](7) NULL` | Beginn des protokollierten Gesprächs. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `AnswerTime` | `[datetime2](7) NULL` | Zeitpunkt der Gesprächsannahme in der Historie. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `EndTime` | `[datetime2](7) NULL` | Endzeitpunkt des protokollierten Gesprächs. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `DisconnectReason` | `[nvarchar](255) NULL` | Grund der Gesprächsbeendigung bzw. Fehlerbeschreibung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](7) NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](7) NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `BridgeID` | `[uniqueidentifier] NULL` | Zusätzliche technische GUID zur Zusammengehörigkeit des Gesprächs/der Bridge; logische Verknüpfung zur Gesprächshistorie, kein FK auf eine Bridge-Tabelle. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `LegACallID` | `[nvarchar](255) NULL` | Call-ID des Anrufer-Legs A; Bezug zu SIP-/Sessiondaten als Text, keine numerische FK-Spalte. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `LegBCallID` | `[nvarchar](255) NULL` | Call-ID des Ziel-Legs B; Bezug zu SIP-/Sessiondaten als Text. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `InviteInRaw` | `[nvarchar](max) NULL` | Eingehender INVITE als SIP-Rohtext zur Gesprächsdiagnose. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `InviteOutRaw` | `[nvarchar](max) NULL` | Ausgehender INVITE als SIP-Rohtext zur Gesprächsdiagnose. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ByeInRaw` | `[nvarchar](max) NULL` | Eingehendes BYE als SIP-Rohtext. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ByeOutRaw` | `[nvarchar](max) NULL` | Ausgehendes BYE als SIP-Rohtext. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ReinviteCount` | `[int] NOT NULL` | Gespeicherte Anzahl der Re-INVITE-Ereignisse für dieses Gespräch. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ByeCount` | `[int] NOT NULL` | Gespeicherte Anzahl der BYE-Ereignisse für dieses Gespräch. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK_TK_SIP_CallLog`: PRIMARY KEY auf `[ID] ASC`.
- `IX_TK_SIP_CallLog_BridgeID`: INDEX auf `[BridgeID] ASC`.
- `IX_TK_SIP_CallLog_FromUser`: INDEX auf `[FromUserID] ASC`.
- `IX_TK_SIP_CallLog_LegACallID`: INDEX auf `[LegACallID] ASC`.
- `IX_TK_SIP_CallLog_LegBCallID`: INDEX auf `[LegBCallID] ASC`.
- `IX_TK_SIP_CallLog_SipCallID`: INDEX auf `[SipCallID] ASC`.
- `IX_TK_SIP_CallLog_StartTime`: INDEX auf `[StartTime] ASC`.
- `IX_TK_SIP_CallLog_ToUser`: INDEX auf `[ToUserID] ASC`.
- Standardwert `ReinviteCount`: `((0))` (`DF_TK_SIP_CallLog_ReinviteCount`).
- Standardwert `ByeCount`: `((0))` (`DF_TK_SIP_CallLog_ByeCount`).

### Verbindungen zu anderen Tabellen

- Ausgehend, **FK**: `FromUserID` → [`TK_SIP_User`](#tk_sip_user).`ID` (`FK_TK_SIP_CallLog_FromUser`).
- Ausgehend, **FK**: `ToUserID` → [`TK_SIP_User`](#tk_sip_user).`ID` (`FK_TK_SIP_CallLog_ToUser`).
- Eingehend, fachlich/Code: [`TK_RuntimeCall`](#tk_runtimecall).`SessionID` → `SipCallID`. Logische Zuordnung im aktuellen TKBase_RuntimeCallManager: CreateCallLog erhält sessionId als sipCallId. Kein FK und keine erzwungene 1:1-Kardinalität.
- Eingehend, fachlich/Code: [`TK_RuntimeCall`](#tk_runtimecall).`BridgeID` → `BridgeID`. Logische Gesprächskorrelation über dieselbe Bridge-GUID; kein FK und keine eigenständige Bridge-Tabelle.
- Eingehend, fachlich/Code: [`TK_RuntimeCall`](#tk_runtimecall).`LegACallID` → `LegACallID`. Logische Korrelation des Anrufer-Legs zwischen Live-Sicht und Historie; kein FK.
- Eingehend, fachlich/Code: [`TK_RuntimeCall`](#tk_runtimecall).`LegBCallID` → `LegBCallID`. Logische Korrelation des Ziel-Legs zwischen Live-Sicht und Historie; kein FK.

## TK_RuntimeCall

### Beschreibung

**Zweck heute:** Live-Sicht auf laufende Gespräche mit Session, Legs und Bedienmöglichkeiten.

**Verwendung:** TKBase_RuntimeCallManager/TKSQL_Commands pflegen die Tabelle. Die GUI verwendet dieselbe Datenbasis; beendete Gespräche werden aus dieser Live-Sicht entfernt.

**Erstinstallation:** Leer; entsteht bei Gesprächen. Keine Wiederherstellung alter Live-Calls als Startdaten.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `GUID` | `[uniqueidentifier] NOT NULL` | Technische GUID des Live-Gesprächsdatensatzes; Schema-Default NEWID(). Fachlicher Upsert-Schlüssel ist SessionID. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `SessionID` | `[nvarchar](100) NOT NULL` | Technische Sessionkennung aus Leg-A-Call-ID und Leg-B-/Leg-D-Call-ID; aktuelle C#-Bildung siehe Abschnitt ID-Bildung. Keine TK_SIP_User-ID. | [`TK_SIP_CallLog`](#tk_sip_calllog).`SipCallID`: Logische Zuordnung im aktuellen TKBase_RuntimeCallManager: CreateCallLog erhält sessionId als sipCallId. Kein FK und keine erzwungene 1:1-Kardinalität. |
| `BridgeID` | `[uniqueidentifier] NULL` | Zusätzliche technische GUID zur Zusammengehörigkeit des Gesprächs/der Bridge; logische Verknüpfung zur Gesprächshistorie, kein FK auf eine Bridge-Tabelle. | [`TK_SIP_CallLog`](#tk_sip_calllog).`BridgeID`: Logische Gesprächskorrelation über dieselbe Bridge-GUID; kein FK und keine eigenständige Bridge-Tabelle. |
| `CallDirection` | `[nvarchar](16) NOT NULL` | Gesprächsrichtung; Runtime-Schema erlaubt Internal, Outbound und Inbound. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CallType` | `[nvarchar](20) NOT NULL` | Gesprächsart; Runtime-Schema erlaubt Internal, External, Service, GSM oder Fax. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RouteKind` | `[nvarchar](20) NULL` | Art des aufgelösten Routingziels, beispielsweise User, Provider, GSM, Service, FaxUser oder Unknown. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Status` | `[nvarchar](32) NOT NULL` | Aktueller Live-Gesprächszustand; SQL-Check erlaubt Trying, Ringing, Answered, Holding oder Transferring. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `FromUserID` | `[int] NULL` | Optionaler interner Benutzer des Anrufers: TK_SIP_User.ID; externe Anrufer müssen keinen internen Benutzer besitzen. | **FK:** [`TK_SIP_User`](#tk_sip_user).`ID`; `FK_TK_RuntimeCall_FromUser`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `ToUserID` | `[int] NULL` | Optionaler interner Benutzer des Angerufenen: TK_SIP_User.ID; Service-/externe Ziele müssen keinen internen Benutzer besitzen. | **FK:** [`TK_SIP_User`](#tk_sip_user).`ID`; `FK_TK_RuntimeCall_ToUser`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `FromNumber` | `[nvarchar](50) NULL` | Rufnummer/Identität des Anrufers als Text. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ToNumber` | `[nvarchar](50) NULL` | Rufnummer/Identität des angerufenen Ziels als Text. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `DisplayFrom` | `[nvarchar](100) NULL` | Für die Live-Anzeige aufbereitete Anruferdarstellung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `DisplayTo` | `[nvarchar](100) NULL` | Für die Live-Anzeige aufbereitete Zieldarstellung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `InternalNumber` | `[nvarchar](50) NULL` | Für das Gespräch gespeicherte interne Nebenstellennummer als Text; keine TK_SIP_User.ID. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ExternalNumber` | `[nvarchar](50) NULL` | Zum Gespräch gespeicherte externe Rufnummer als Text; keine TK_ExternalNumbers.ID. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `OutboundExternalNumber` | `[nvarchar](50) NULL` | Für ausgehende Gespräche ausgewählte externe Leitungs-/Absenderrufnummer als Text; keine numerische Tabellen-ID. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ProviderAccountID` | `[int] NULL` | Zuordnung zu TK_ProviderAccounts.ID, nicht zur Provider-Rufnummer oder SIP-Benutzerkennung. | **FK:** [`TK_ProviderAccounts`](#tk_provideraccounts).`ID`; `FK_TK_RuntimeCall_ProviderAccount`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `GsmStatusID` | `[int] NULL` | Optionaler Bezug auf die benutzte Modem-/SIM-Laufzeitzuordnung: TK_GSM_ModemSimStatus.ID. | **FK:** [`TK_GSM_ModemSimStatus`](#tk_gsm_modemsimstatus).`ID`; `FK_TK_RuntimeCall_GsmStatus`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `GsmModemID` | `[int] NULL` | Optionaler Bezug auf das am Gespräch beteiligte Modem: TK_GSM_Modems.ID. | **FK:** [`TK_GSM_Modems`](#tk_gsm_modems).`ID`; `FK_TK_RuntimeCall_GsmModem`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `GsmSimID` | `[int] NULL` | Optionaler Bezug auf die am Gespräch beteiligte SIM: TK_GSM_Sim.ID. | **FK:** [`TK_GSM_Sim`](#tk_gsm_sim).`ID`; `FK_TK_RuntimeCall_GsmSim`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `LegACallID` | `[nvarchar](255) NULL` | Call-ID des Anrufer-Legs A; Bezug zu SIP-/Sessiondaten als Text, keine numerische FK-Spalte. | [`TK_SIP_CallLog`](#tk_sip_calllog).`LegACallID`: Logische Korrelation des Anrufer-Legs zwischen Live-Sicht und Historie; kein FK. |
| `LegBCallID` | `[nvarchar](255) NULL` | Call-ID des Ziel-Legs B; Bezug zu SIP-/Sessiondaten als Text. | [`TK_SIP_CallLog`](#tk_sip_calllog).`LegBCallID`: Logische Korrelation des Ziel-Legs zwischen Live-Sicht und Historie; kein FK. |
| `LegDCallID` | `[nvarchar](255) NULL` | Call-ID des Service-Legs D; Servicebezug erfolgt über Laufzeitkontext, nicht über eine ServiceLeg-ID-Spalte. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `LegAStatus` | `[nvarchar](32) NULL` | Gespeicherter Status des Anrufer-Legs A. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LegBStatus` | `[nvarchar](32) NULL` | Gespeicherter Status des Ziel-Legs B. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LegDStatus` | `[nvarchar](32) NULL` | Gespeicherter Status des Service-Legs D. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ContactUriA` | `[nvarchar](512) NULL` | Contact-URI des Legs A in der Live-Gesprächssicht. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ContactUriB` | `[nvarchar](512) NULL` | Contact-URI des Legs B in der Live-Gesprächssicht. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RemoteEndPointA` | `[nvarchar](255) NULL` | Gespeicherter entfernter Netzwerkendpunkt des Legs A. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RemoteEndPointB` | `[nvarchar](255) NULL` | Gespeicherter entfernter Netzwerkendpunkt des Legs B. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CanHangup` | `[bit] NOT NULL` | Vom Service bereitgestelltes Kennzeichen, ob die GUI das Auflegen anbieten darf. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CanTransfer` | `[bit] NOT NULL` | Vom Service bereitgestelltes Kennzeichen, ob die GUI einen Transfer anbieten darf. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `TransferTarget` | `[nvarchar](100) NULL` | Gespeichertes Transferziel als Text, keine allgemeine numerische Benutzer-ID. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `StartedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt des Gesprächsbeginns in der Live-Gesprächssicht. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `AnsweredAt` | `[datetime2](7) NULL` | Zeitpunkt der Gesprächsannahme in der Live-Gesprächssicht. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastUpdatedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt der letzten Aktualisierung der Live-Gesprächssicht. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK_TK_RuntimeCall`: PRIMARY KEY auf `[ID] ASC`.
- `UQ_TK_RuntimeCall_GUID`: UNIQUE auf `[GUID] ASC` (separat im SQL angelegt).
- `UQ_TK_RuntimeCall_SessionID`: UNIQUE auf `[SessionID] ASC` (separat im SQL angelegt).
- `IX_TK_RuntimeCall_BridgeID`: INDEX auf `[BridgeID] ASC`.
- `IX_TK_RuntimeCall_FromUserID`: INDEX auf `[FromUserID] ASC`.
- `IX_TK_RuntimeCall_GsmStatusID`: INDEX auf `[GsmStatusID] ASC`.
- `IX_TK_RuntimeCall_LegACallID`: INDEX auf `[LegACallID] ASC`.
- `IX_TK_RuntimeCall_LegBCallID`: INDEX auf `[LegBCallID] ASC`.
- `IX_TK_RuntimeCall_OutboundExternalNumber`: INDEX auf `[OutboundExternalNumber] ASC`.
- `IX_TK_RuntimeCall_ProviderAccountID`: INDEX auf `[ProviderAccountID] ASC`.
- `IX_TK_RuntimeCall_StartedAt`: INDEX auf `[StartedAt] ASC`.
- `IX_TK_RuntimeCall_Status`: INDEX auf `[Status] ASC`.
- `IX_TK_RuntimeCall_ToUserID`: INDEX auf `[ToUserID] ASC`.
- Standardwert `GUID`: `(newid())` (`DF_TK_RuntimeCall_GUID`).
- Standardwert `CanHangup`: `((1))` (`DF_TK_RuntimeCall_CanHangup`).
- Standardwert `CanTransfer`: `((0))` (`DF_TK_RuntimeCall_CanTransfer`).
- Standardwert `StartedAt`: `(sysdatetime())` (`DF_TK_RuntimeCall_StartedAt`).
- Standardwert `LastUpdatedAt`: `(sysdatetime())` (`DF_TK_RuntimeCall_LastUpdatedAt`).
- Prüfung `CHK_TK_RuntimeCall_CallDirection`: `(([CallDirection]='Internal' OR [CallDirection]='Outbound' OR [CallDirection]='Inbound'))`.
- Prüfung `CHK_TK_RuntimeCall_CallType`: `(([CallType]=N'Fax' OR [CallType]=N'GSM' OR [CallType]=N'Service' OR [CallType]=N'External' OR [CallType]=N'Internal'))`.
- Prüfung `CHK_TK_RuntimeCall_RouteKind`: `(([RouteKind] IS NULL OR ([RouteKind]='Unknown' OR [RouteKind]='FaxUser' OR [RouteKind]='Service' OR [RouteKind]='GSM' OR [RouteKind]='Provider' OR [RouteKind]='User')))`.
- Prüfung `CHK_TK_RuntimeCall_Status`: `(([Status]='Transferring' OR [Status]='Holding' OR [Status]='Answered' OR [Status]='Ringing' OR [Status]='Trying'))`.

### Verbindungen zu anderen Tabellen

- Ausgehend, **FK**: `FromUserID` → [`TK_SIP_User`](#tk_sip_user).`ID` (`FK_TK_RuntimeCall_FromUser`).
- Ausgehend, **FK**: `GsmModemID` → [`TK_GSM_Modems`](#tk_gsm_modems).`ID` (`FK_TK_RuntimeCall_GsmModem`).
- Ausgehend, **FK**: `GsmSimID` → [`TK_GSM_Sim`](#tk_gsm_sim).`ID` (`FK_TK_RuntimeCall_GsmSim`).
- Ausgehend, **FK**: `GsmStatusID` → [`TK_GSM_ModemSimStatus`](#tk_gsm_modemsimstatus).`ID` (`FK_TK_RuntimeCall_GsmStatus`).
- Ausgehend, **FK**: `ProviderAccountID` → [`TK_ProviderAccounts`](#tk_provideraccounts).`ID` (`FK_TK_RuntimeCall_ProviderAccount`).
- Ausgehend, **FK**: `ToUserID` → [`TK_SIP_User`](#tk_sip_user).`ID` (`FK_TK_RuntimeCall_ToUser`).
- Ausgehend, fachlich/Code: `SessionID` → [`TK_SIP_CallLog`](#tk_sip_calllog).`SipCallID`. Logische Zuordnung im aktuellen TKBase_RuntimeCallManager: CreateCallLog erhält sessionId als sipCallId. Kein FK und keine erzwungene 1:1-Kardinalität.
- Ausgehend, fachlich/Code: `BridgeID` → [`TK_SIP_CallLog`](#tk_sip_calllog).`BridgeID`. Logische Gesprächskorrelation über dieselbe Bridge-GUID; kein FK und keine eigenständige Bridge-Tabelle.
- Ausgehend, fachlich/Code: `LegACallID` → [`TK_SIP_CallLog`](#tk_sip_calllog).`LegACallID`. Logische Korrelation des Anrufer-Legs zwischen Live-Sicht und Historie; kein FK.
- Ausgehend, fachlich/Code: `LegBCallID` → [`TK_SIP_CallLog`](#tk_sip_calllog).`LegBCallID`. Logische Korrelation des Ziel-Legs zwischen Live-Sicht und Historie; kein FK.

## TK_ExternalNumbers

### Beschreibung

**Zweck heute:** Einheitlicher Katalog externer Leitungs-/Absenderrufnummern aus Providern und GSM.

**Verwendung:** Provider- und GSM-Pfade synchronisieren die Nummern. SourceType + SourceID bestimmen die Herkunft, nicht NumberType allein.

**Erstinstallation:** Leer; externe Leitungen müssen erst eingerichtet bzw. erkannt werden.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `NumberType` | `[nvarchar](20) NOT NULL` | Fachlicher Typ der externen Nummer, beispielsweise Provider oder GSM; unterscheidet sich vom technischen SourceType. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SourceID` | `[int] NOT NULL` | Numerische ID des Quellobjekts; die Zieltabelle hängt von SourceType ab. Keine universelle Benutzer-ID. | [`TK_ProviderAccounts`](#tk_provideraccounts).`ID`: Bedingter Codebezug: SourceType='ProviderAccount'; kein SQL-Fremdschlüssel. [`TK_GSM_ModemSimStatus`](#tk_gsm_modemsimstatus).`ID`: Bedingter Codebezug: SourceType='GSMStatus'; NICHT direkt TK_GSM_Modems.ID oder TK_GSM_Sim.ID. |
| `NumberValue` | `[nvarchar](50) NOT NULL` | Externe Rufnummer als Text; kein Fremdschlüssel auf eine andere numerische ID. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `DisplayName` | `[nvarchar](100) NULL` | Lesbarer Anzeigename für Oberfläche oder fachliche Darstellung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `IsOnline` | `[bit] NOT NULL` | Gespeicherter Onlinezustand der externen Leitung/Nummer. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `IsSelectable` | `[bit] NOT NULL` | Kennzeichnet, ob die externe Nummer für Auswahl/Routing angeboten werden darf. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Active` | `[bit] NOT NULL` | Aktivierungskennzeichen des Datensatzes: 1 aktiv, 0 deaktiviert. Kein Ersatz für Registrierungs- oder Erreichbarkeitsstatus. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](7) NOT NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SourceType` | `[nvarchar](20) NOT NULL` | Typ des Quellobjekts; bestimmt zusammen mit SourceID die tatsächliche Herkunft. Zuordnung siehe Beziehungsspalte und Übersicht. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK_TK_ExternalNumbers`: PRIMARY KEY auf `[ID] ASC`.
- `IX_TK_ExternalNumbers_IsSelectable`: INDEX auf `[IsSelectable] ASC`.
- `IX_TK_ExternalNumbers_NumberType`: INDEX auf `[NumberType] ASC`.
- `IX_TK_ExternalNumbers_NumberValue`: INDEX auf `[NumberValue] ASC`.
- `IX_TK_ExternalNumbers_SourceType`: INDEX auf `[SourceType] ASC`.
- Standardwert `IsOnline`: `((0))` (`DF_TK_ExternalNumbers_IsOnline`).
- Standardwert `IsSelectable`: `((0))` (`DF_TK_ExternalNumbers_IsSelectable`).
- Standardwert `Active`: `((1))` (`DF_TK_ExternalNumbers_Active`).
- Standardwert `CreatedAt`: `(sysdatetime())` (`DF_TK_ExternalNumbers_CreatedAt`).
- Standardwert `UpdatedAt`: `(sysdatetime())` (`DF_TK_ExternalNumbers_UpdatedAt`).
- Standardwert `SourceType`: `('Unknown')` (`DF_TK_ExternalNumbers_SourceType`).

### Verbindungen zu anderen Tabellen

- Eingehend, fachlich/Code: [`TK_ExternalNumberRouting`](#tk_externalnumberrouting).`ExternalNumberID` → `ID`. Codebezug durch JOIN; kein SQL-Fremdschlüssel.
- Ausgehend, fachlich/Code: `SourceID` → [`TK_ProviderAccounts`](#tk_provideraccounts).`ID`. Bedingter Codebezug: SourceType='ProviderAccount'; kein SQL-Fremdschlüssel.
- Ausgehend, fachlich/Code: `SourceID` → [`TK_GSM_ModemSimStatus`](#tk_gsm_modemsimstatus).`ID`. Bedingter Codebezug: SourceType='GSMStatus'; NICHT direkt TK_GSM_Modems.ID oder TK_GSM_Sim.ID.

## TK_ExternalNumberRouting

### Beschreibung

**Zweck heute:** Zuordnung externer Rufnummern zu internen Benutzern mit Aktivierung und Priorität.

**Verwendung:** Inbound-Zielsuche sowie Auswahl/Priorisierung externer Leitungen für interne Teilnehmer; fachliche Zuordnung über ExternalNumberID und InternalNumberID.

**Erstinstallation:** Leer; ohne eigene externe Leitungen sind keine persönlichen Routingzuordnungen vorgegeben.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `ExternalNumberID` | `[int] NOT NULL` | ID des zugehörigen Datensatzes in TK_ExternalNumbers; nicht die externe Rufnummer selbst. | [`TK_ExternalNumbers`](#tk_externalnumbers).`ID`: Codebezug durch JOIN; kein SQL-Fremdschlüssel. |
| `ExterneNr` | `[nvarchar](50) NULL` | Zusätzlich gespeicherter Rufnummerntext der externen Nummer; maßgeblicher ID-Bezug ist ExternalNumberID. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `TargetType` | `[nvarchar](20) NOT NULL` | Fachlicher Zieltyp; zulässige bzw. ausgewertete Werte hängen von der betreffenden Tabelle und dem Routingpfad ab. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `InterneNr` | `[nvarchar](50) NULL` | Zusätzlich gespeicherter Text der internen Nebenstelle; maßgeblicher Benutzerbezug ist InternalNumberID. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `InternalNumberID` | `[int] NOT NULL` | ID des internen Ziel-/Zuordnungsbenutzers aus TK_SIP_User.ID; trotz des Namens keine Nebenstellennummer. | [`TK_SIP_User`](#tk_sip_user).`ID`: Codebezug durch JOIN; kein SQL-Fremdschlüssel. Gilt auch für FAXUser-Datensätze dieser Benutzertabelle. |
| `Priority` | `[int] NOT NULL` | Priorität der Regel; die jeweiligen Abfragen werten kleinere Zahlen zuerst aus. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Active` | `[bit] NOT NULL` | Aktivierungskennzeichen des Datensatzes: 1 aktiv, 0 deaktiviert. Kein Ersatz für Registrierungs- oder Erreichbarkeitsstatus. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](7) NOT NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RestorePriority` | `[int] NOT NULL` | Gesicherte Priorität einer Route für spätere Wiederaktivierung; keine ID-Verknüpfung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK_TK_ExternalNumberRouting`: PRIMARY KEY auf `[ID] ASC`.
- Standardwert `RestorePriority`: `((1))` (`DF_TK_ExternalNumberRouting_RestorePriority`).

### Verbindungen zu anderen Tabellen

- Ausgehend, fachlich/Code: `ExternalNumberID` → [`TK_ExternalNumbers`](#tk_externalnumbers).`ID`. Codebezug durch JOIN; kein SQL-Fremdschlüssel.
- Ausgehend, fachlich/Code: `InternalNumberID` → [`TK_SIP_User`](#tk_sip_user).`ID`. Codebezug durch JOIN; kein SQL-Fremdschlüssel. Gilt auch für FAXUser-Datensätze dieser Benutzertabelle.

## TK_UserForwarding

### Beschreibung

**Zweck heute:** Benutzerbezogene Rufumleitungen.

**Verwendung:** Service/Routing werten ForwardType, Ziel, Providerbezug und Priorität aus.

**Erstinstallation:** Leer; Umleitungen werden bei Bedarf eingerichtet.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `UserID` | `[int] NOT NULL` | ID des zugeordneten internen Benutzers aus TK_SIP_User.ID; die Beziehung ist in der letzten Spalte ausgewiesen. | **FK:** [`TK_SIP_User`](#tk_sip_user).`ID`; `FK_TK_UserForwarding_UserID`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `Active` | `[bit] NOT NULL` | Aktivierungskennzeichen des Datensatzes: 1 aktiv, 0 deaktiviert. Kein Ersatz für Registrierungs- oder Erreichbarkeitsstatus. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ForwardType` | `[nvarchar](20) NOT NULL` | Art der Rufumleitung; bestimmt zusammen mit Target die Zielauflösung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Target` | `[nvarchar](100) NOT NULL` | Umleitungsziel als Text, je nach ForwardType interne/externe Rufnummer oder Dienstziel; kein allgemeiner numerischer Fremdschlüssel. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ProviderAccountID` | `[int] NULL` | Zuordnung zu TK_ProviderAccounts.ID, nicht zur Provider-Rufnummer oder SIP-Benutzerkennung. | **FK:** [`TK_ProviderAccounts`](#tk_provideraccounts).`ID`; `FK_TK_UserForwarding_ProviderAccountID`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `Priority` | `[int] NOT NULL` | Priorität der Regel; die jeweiligen Abfragen werten kleinere Zahlen zuerst aus. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](7) NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- Prüfung `CHK_TK_UserForwarding_ForwardType`: `(([ForwardType]='Voicemail' OR [ForwardType]='GSM' OR [ForwardType]='Provider' OR [ForwardType]='Extern' OR [ForwardType]='Intern'))`.

### Verbindungen zu anderen Tabellen

- Ausgehend, **FK**: `ProviderAccountID` → [`TK_ProviderAccounts`](#tk_provideraccounts).`ID` (`FK_TK_UserForwarding_ProviderAccountID`).
- Ausgehend, **FK**: `UserID` → [`TK_SIP_User`](#tk_sip_user).`ID` (`FK_TK_UserForwarding_UserID`).

## TK_SIP_ServiceLeg

### Beschreibung

**Zweck heute:** Service-Leg-D-Konfiguration mit Service-Hauptmenü, Untermenüs, DTMF-Optionen und Medieneinstellungen.

**Verwendung:** SVC führt die in ServiceType/JsonConfig beschriebene Funktion aus. Leg D ist vom Anrufer-Leg A und einem möglichen Ziel-Leg B getrennt.

**Erstinstallation:** Alle 19 exportierten Dienste einschließlich Aktivierungszuständen und Menükonfigurationen sind enthalten.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `GUID` | `AS ([dbo].[TK_GUID](concat(isnull([InternalNumber],N''),N'\|',isnull([ServiceCode],N''),N'\|',isnull([ServiceType],N'')))) PERSISTED` | Persistierte, berechnete technische GUID aus den in der SQL-Formel genannten Merkmalen über dbo.TK_GUID. Ändern sich diese Merkmale, kann sich auch diese GUID ändern; kein Bezug auf eine GUID-Tabelle. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `InternalNumber` | `[nvarchar](50) NOT NULL` | Eindeutige vollständige Servicenummer. SQL-Check erlaubt *100 oder eine unter *100 liegende Form *100*…#. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `DisplayName` | `[nvarchar](100) NOT NULL` | Lesbarer Anzeigename für Oberfläche oder fachliche Darstellung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Description` | `[nvarchar](255) NULL` | Zusätzliche textliche Beschreibung des Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ServiceCode` | `[nvarchar](50) NOT NULL` | Eindeutiger fachlicher Schlüssel eines Dienstes, beispielsweise MAIN_MENU. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ServiceType` | `[nvarchar](30) NOT NULL` | Ausführbarer Diensttyp; Auflösung durch die SVC-Logik, nicht über eine separate SQL-Typentabelle. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `PromptFile` | `[nvarchar](260) NULL` | Optionaler Pfad einer Ansagedatei; Dateisystembezug, keine AudioBlob-ID. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RecordTemplate` | `[nvarchar](260) NULL` | Vorlage für Dateiname/Pfad von Aufzeichnungen, gegebenenfalls mit Session-/Zeitplatzhaltern. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `JsonConfig` | `[nvarchar](max) NULL` | Zusätzliche objektbezogene JSON-Konfiguration. Enthaltene Ziel-/Menüwerte werden von der jeweiligen Bereichslogik ausgewertet, nicht durch SQL-Fremdschlüssel. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `AutoAnswer` | `[bit] NOT NULL` | Kennzeichnet die automatische Annahme des Service-Legs. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `KeepSessionUntilPeerEnded` | `[bit] NOT NULL` | Konfiguriert, ob die Service-Session bis zum Beenden durch die Gegenseite gehalten werden soll. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Active` | `[bit] NOT NULL` | Aktivierungskennzeichen des Datensatzes: 1 aktiv, 0 deaktiviert. Kein Ersatz für Registrierungs- oder Erreichbarkeitsstatus. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SortOrder` | `[int] NOT NULL` | Gespeicherte Sortierreihenfolge für Listen bzw. Auswahl; Bedeutung der Reihenfolge wird vom jeweiligen Ladepfad bestimmt. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `MenuRootNumber` | `[nvarchar](50) NULL` | Zugehörige Service-Menüwurzel, aktuell *100; fachlicher Bezug auf die InternalNumber des Wurzeldienstes. | [`TK_SIP_ServiceLeg`](#tk_sip_serviceleg).`InternalNumber`: Fachliche Menüwurzel-Zuordnung per Servicenummer, aktuell *100; kein FK. |
| `MenuOption` | `[nvarchar](20) NULL` | DTMF-/Menüoption innerhalb der Menüstruktur, beispielsweise *8; keine Datenbank-ID. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `MenuParentCode` | `[nvarchar](50) NULL` | Gespeicherter ServiceCode des übergeordneten Menüs; fachlicher Selbstbezug innerhalb TK_SIP_ServiceLeg, ohne FK. Nicht in jedem aktuellen Ladepfad ausgewertet. | [`TK_SIP_ServiceLeg`](#tk_sip_serviceleg).`ServiceCode`: Fachliche Menü-Elternzuordnung per Schlüsseltext; kein FK. Ein vorhandenes Feld ist kein Nachweis, dass jeder aktuelle Pfad es auswertet. |
| `MenuSortOrder` | `[int] NULL` | Reihenfolge des Eintrags innerhalb des Service-Menüs. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `IsMenuEntry` | `[bit] NOT NULL` | Kennzeichnet einen als Menüeintrag vorgesehenen Dienst. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `IsDirectDialAllowed` | `[bit] NOT NULL` | Kennzeichnet, ob der Dienst über seine vollständige Servicenummer direkt anwählbar sein soll. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](7) NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK_TK_SIP_ServiceLeg`: PRIMARY KEY auf `[ID] ASC`.
- `UQ_TK_SIP_ServiceLeg_InternalNumber`: UNIQUE auf `[InternalNumber] ASC` (separat im SQL angelegt).
- `UQ_TK_SIP_ServiceLeg_ServiceCode`: UNIQUE auf `[ServiceCode] ASC` (separat im SQL angelegt).
- `IX_TK_SIP_ServiceLeg_Active_SortOrder`: INDEX auf `[Active] ASC, [SortOrder] ASC, [InternalNumber] ASC`.
- `IX_TK_SIP_ServiceLeg_MenuRoot_MenuSort`: INDEX auf `[MenuRootNumber] ASC, [MenuParentCode] ASC, [MenuSortOrder] ASC, [MenuOption] ASC`.
- Standardwert `AutoAnswer`: `((1))` (`DF_TK_SIP_ServiceLeg_AutoAnswer`).
- Standardwert `KeepSessionUntilPeerEnded`: `((1))` (`DF_TK_SIP_ServiceLeg_KeepSessionUntilPeerEnded`).
- Standardwert `Active`: `((1))` (`DF_TK_SIP_ServiceLeg_Active`).
- Standardwert `SortOrder`: `((0))` (`DF_TK_SIP_ServiceLeg_SortOrder`).
- Standardwert `IsMenuEntry`: `((1))` (`DF_TK_SIP_ServiceLeg_IsMenuEntry`).
- Standardwert `IsDirectDialAllowed`: `((1))` (`DF_TK_SIP_ServiceLeg_IsDirectDialAllowed`).
- Standardwert `CreatedAt`: `(sysdatetime())` (`DF_TK_SIP_ServiceLeg_CreatedAt`).
- Prüfung `CK_TK_SIP_ServiceLeg_InternalNumber_ServiceMenu100`: `(([InternalNumber]=N'*100' OR [InternalNumber] like N'*100*%#'))`.

### Verbindungen zu anderen Tabellen

- Ausgehend, fachlich/Code: `MenuParentCode` → [`TK_SIP_ServiceLeg`](#tk_sip_serviceleg).`ServiceCode`. Fachliche Menü-Elternzuordnung per Schlüsseltext; kein FK. Ein vorhandenes Feld ist kein Nachweis, dass jeder aktuelle Pfad es auswertet.
- Ausgehend, fachlich/Code: `MenuRootNumber` → [`TK_SIP_ServiceLeg`](#tk_sip_serviceleg).`InternalNumber`. Fachliche Menüwurzel-Zuordnung per Servicenummer, aktuell *100; kein FK.

## TK_Notruf

### Beschreibung

**Zweck heute:** Spezielle Routingregeln für Notruf-/Sondernummern.

**Verwendung:** Die Zielklassifikation sucht aktive Regeln vor weiteren Zielarten. ProviderAccountID ist optional; Nutzbarkeit hängt von tatsächlich eingerichteten externen Wegen ab.

**Erstinstallation:** Drei Vorlagen für 110, 112 und 116117 sind enthalten, ohne Providerzuordnung und deaktiviert. Vor produktiver Nutzung passend konfigurieren.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `Notrufnummer` | `[nvarchar](20) NOT NULL` | Als Notruf-/Sonderregel zu erkennende gewählte Nummer. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Bezeichnung` | `[nvarchar](100) NOT NULL` | Lesbarer Name der Notruf-/Sonderregel. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `TargetType` | `[nvarchar](20) NOT NULL` | Fachlicher Zieltyp; zulässige bzw. ausgewertete Werte hängen von der betreffenden Tabelle und dem Routingpfad ab. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `TargetValue` | `[nvarchar](100) NOT NULL` | Zielwert der Notrufregel, beispielsweise eine externe Zielnummer; Interpretation über TargetType, kein genereller ID-Fremdschlüssel. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ProviderAccountID` | `[int] NULL` | Zuordnung zu TK_ProviderAccounts.ID, nicht zur Provider-Rufnummer oder SIP-Benutzerkennung. | [`TK_ProviderAccounts`](#tk_provideraccounts).`ID`: Optionaler fachlicher Providerbezug; kein SQL-Fremdschlüssel im aktuellen Schema. |
| `Priority` | `[int] NOT NULL` | Priorität der Regel; die jeweiligen Abfragen werten kleinere Zahlen zuerst aus. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Active` | `[bit] NOT NULL` | Aktivierungskennzeichen des Datensatzes: 1 aktiv, 0 deaktiviert. Kein Ersatz für Registrierungs- oder Erreichbarkeitsstatus. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](7) NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK_TK_Notruf`: PRIMARY KEY auf `[ID] ASC`.
- `UQ_TK_Notruf_Notrufnummer_TargetValue`: UNIQUE auf `[Notrufnummer] ASC, [TargetValue] ASC` (separat im SQL angelegt).
- Standardwert `Priority`: `((1))` (`DF_TK_Notruf_Priority`).
- Standardwert `Active`: `((1))` (`DF_TK_Notruf_Active`).
- Standardwert `CreatedAt`: `(sysutcdatetime())` (`DF_TK_Notruf_CreatedAt`).

### Verbindungen zu anderen Tabellen

- Ausgehend, fachlich/Code: `ProviderAccountID` → [`TK_ProviderAccounts`](#tk_provideraccounts).`ID`. Optionaler fachlicher Providerbezug; kein SQL-Fremdschlüssel im aktuellen Schema.

## TK_GSM_Modems

### Beschreibung

**Zweck heute:** Identität physischer GSM-Modems.

**Verwendung:** Geräteinformationen sind von SIM-Identität und aktuellem Modem-/SIM-Status getrennt.

**Erstinstallation:** Leer; konkrete Hardware ist keine allgemeine Installationsvorgabe.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `GUID` | `AS ([dbo].[TK_GUID](concat(isnull([ModemName],N''),N'\|',isnull([Manufacturer],N''),N'\|',isnull([Model],N''),N'\|',isnull([IMEI],N'')))) PERSISTED` | Persistierte, berechnete technische GUID aus den in der SQL-Formel genannten Merkmalen über dbo.TK_GUID. Ändern sich diese Merkmale, kann sich auch diese GUID ändern; kein Bezug auf eine GUID-Tabelle. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `ModemName` | `[nvarchar](100) NOT NULL` | Lesbarer Name des GSM-Modems. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Manufacturer` | `[nvarchar](100) NULL` | Hersteller des GSM-Modems. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Model` | `[nvarchar](100) NULL` | Modellbezeichnung des GSM-Modems. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `IMEI` | `[nvarchar](50) NULL` | Mobilfunk-Gerätekennung des Modems; keine SQL-ID. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `PNPDeviceID` | `[nvarchar](500) NULL` | Windows-Plug-and-Play-Gerätekennung zur Hardwarezuordnung. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `Active` | `[bit] NOT NULL` | Aktivierungskennzeichen des Datensatzes: 1 aktiv, 0 deaktiviert. Kein Ersatz für Registrierungs- oder Erreichbarkeitsstatus. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](7) NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK_TK_GSM_Modems`: PRIMARY KEY auf `[ID] ASC`.
- `UX_TK_GSM_Modems_GUID`: UNIQUE INDEX auf `[GUID] ASC`.

### Verbindungen zu anderen Tabellen

- Eingehend, **FK**: [`TK_GSM_ModemSimStatus`](#tk_gsm_modemsimstatus).`ModemID` → `ID` (`FK_TK_GSM_ModemSimStatus_Modem`).
- Eingehend, **FK**: [`TK_RuntimeCall`](#tk_runtimecall).`GsmModemID` → `ID` (`FK_TK_RuntimeCall_GsmModem`).

## TK_GSM_Sim

### Beschreibung

**Zweck heute:** Identität von SIM-Karten und deren Mobilfunkrufnummern.

**Verwendung:** Getrennte SIM-Stammdaten ermöglichen eine Zuordnung zu unterschiedlichen Modems über TK_GSM_ModemSimStatus.

**Erstinstallation:** Leer; konkrete SIM-Karten werden separat eingerichtet/erfasst.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `GUID` | `AS ([dbo].[TK_GUID](concat(isnull([Telefonnummer],N''),N'\|',isnull([ProviderName],N''),N'\|',isnull([ICCID],N''),N'\|',isnull([IMSI],N'')))) PERSISTED` | Persistierte, berechnete technische GUID aus den in der SQL-Formel genannten Merkmalen über dbo.TK_GUID. Ändern sich diese Merkmale, kann sich auch diese GUID ändern; kein Bezug auf eine GUID-Tabelle. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `Telefonnummer` | `[nvarchar](50) NULL` | Rufnummer des betreffenden Benutzers bzw. der SIM; kein numerischer Datenbankschlüssel. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ProviderName` | `[nvarchar](100) NULL` | Name des SIP-Providers bzw. Mobilfunkanbieters. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ICCID` | `[nvarchar](50) NULL` | Kennung der SIM-Karte. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `IMSI` | `[nvarchar](50) NULL` | Mobilfunk-Teilnehmerkennung der SIM. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `Active` | `[bit] NOT NULL` | Aktivierungskennzeichen des Datensatzes: 1 aktiv, 0 deaktiviert. Kein Ersatz für Registrierungs- oder Erreichbarkeitsstatus. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](7) NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK_TK_GSM_Sims`: PRIMARY KEY auf `[ID] ASC`.
- `UX_TK_GSM_Sim_GUID`: UNIQUE INDEX auf `[GUID] ASC`.

### Verbindungen zu anderen Tabellen

- Eingehend, **FK**: [`TK_GSM_ModemSimStatus`](#tk_gsm_modemsimstatus).`SimID` → `ID` (`FK_TK_GSM_ModemSimStatus_Sim`).
- Eingehend, **FK**: [`TK_RuntimeCall`](#tk_runtimecall).`GsmSimID` → `ID` (`FK_TK_RuntimeCall_GsmSim`).

## TK_GSM_ModemSimStatus

### Beschreibung

**Zweck heute:** Aktuelle Zuordnung und Status eines Modems mit optional eingesetzter SIM.

**Verwendung:** Service/GSM-Gateway verwenden diese Datensätze für Monitoring und Gesprächswege. Externe GSM-Nummern referenzieren diesen Statusdatensatz.

**Erstinstallation:** Leer; passende Geräte-/SIM-Daten sind erst für GSM-Betrieb erforderlich.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `GUID` | `[uniqueidentifier] NOT NULL` | Technische GUID der Modem-/SIM-Zuordnung. TKSQL_Commands kann sie über dbo.TK_GUID aus Modem-GUID und optionaler SIM-GUID bilden; regulär gespeicherte Spalte. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `ModemID` | `[int] NOT NULL` | ID des physischen Modems aus TK_GSM_Modems.ID. | **FK:** [`TK_GSM_Modems`](#tk_gsm_modems).`ID`; `FK_TK_GSM_ModemSimStatus_Modem`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `SimID` | `[int] NULL` | Optionale ID der eingesetzten SIM aus TK_GSM_Sim.ID. | **FK:** [`TK_GSM_Sim`](#tk_gsm_sim).`ID`; `FK_TK_GSM_ModemSimStatus_Sim`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `IsPresent` | `[bit] NOT NULL` | Gibt an, ob das GSM-Modem aktuell als vorhanden erkannt wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `IsSimInserted` | `[bit] NOT NULL` | Gibt an, ob eine SIM im Modem erkannt wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `IsRegistered` | `[bit] NOT NULL` | Gespeicherter Registrierungsstatus: 1 registriert, 0 nicht registriert. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SignalStrength` | `[int] NULL` | Vom Modem erfasster Signalstärkewert; gespeicherter Roh-/Statuswert, nicht pauschal als dBm zu interpretieren. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ComPortAT` | `[nvarchar](50) NULL` | COM-Port für AT-Steuerbefehle des Modems. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ComPortAudio` | `[nvarchar](50) NULL` | COM-Port für den seriellen Audiopfad des Modems; ersetzt die veraltete Bezeichnung ComPortModem in der bisherigen Dokumentation. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ComPortDiag` | `[nvarchar](50) NULL` | COM-Port für Diagnosefunktionen des Modems. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ComPortNMEA` | `[nvarchar](50) NULL` | COM-Port des NMEA-/GNSS-Kanals; ersetzt die veraltete Bezeichnung ComPortAux in der bisherigen Dokumentation. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastSeen` | `[datetime2](7) NULL` | Zeitpunkt der letzten Beobachtung/Aktualisierung des Geräts oder der Registrierung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastError` | `[nvarchar](500) NULL` | Zuletzt gespeicherter Fehlertext für das betreffende Objekt; NULL bedeutet kein eingetragener Fehler. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](7) NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK_TK_GSM_ModemSimStatus`: PRIMARY KEY auf `[ID] ASC`.

### Verbindungen zu anderen Tabellen

- Ausgehend, **FK**: `ModemID` → [`TK_GSM_Modems`](#tk_gsm_modems).`ID` (`FK_TK_GSM_ModemSimStatus_Modem`).
- Ausgehend, **FK**: `SimID` → [`TK_GSM_Sim`](#tk_gsm_sim).`ID` (`FK_TK_GSM_ModemSimStatus_Sim`).
- Eingehend, **FK**: [`TK_RuntimeCall`](#tk_runtimecall).`GsmStatusID` → `ID` (`FK_TK_RuntimeCall_GsmStatus`).
- Eingehend, fachlich/Code: [`TK_ExternalNumbers`](#tk_externalnumbers).`SourceID` → `ID`. Bedingter Codebezug: SourceType='GSMStatus'; NICHT direkt TK_GSM_Modems.ID oder TK_GSM_Sim.ID.

## TK_Properties

### Beschreibung

**Zweck heute:** Zentrale Einstellungen für Standort, SIP, RTP, Codecs, Medienablage, Logging und öffentliche IP.

**Verwendung:** LoadServerSettings liest den Datensatz mit der höchsten Id. Pflichtfelder und Werte werden in TKBase_ServerSettings geprüft; Einstellungen stammen aus der DB.

**Erstinstallation:** Ein vollständiger Datensatz aus dem aktuellen Export, mit den vereinbarten IP-/DNS- und Berliner Standortanpassungen. Die Tabelle ist für den SIP-Start erforderlich.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `Id` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Einstellungsdatensatzes. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `PLZ` | `[int] NULL` | Postleitzahl des Anlagenstandorts; als Integer gespeichert und deshalb ohne führende Nullen. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `City` | `[nvarchar](100) NULL` | Ortsname des Anlagenstandorts; sollte zum referenzierten AreaID-Datensatz passen. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Country` | `[nvarchar](100) NULL` | Landesbezeichnung des Anlagenstandorts; fachlich passend zu CountryID halten. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CountryID` | `[int] NULL` | ID des ausgewählten Ländervorwahl-Datensatzes aus TK_CountryPrefix.ID. | **FK:** [`TK_CountryPrefix`](#tk_countryprefix).`ID`; `FK_TK_Properties_CountryID`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `AreaID` | `[int] NULL` | ID des ausgewählten Ortsvorwahl-Datensatzes aus TK_DEAreaCode.ID; keine Vorwahlziffern. | **FK:** [`TK_DEAreaCode`](#tk_deareacode).`ID`; `FK_TK_Properties_AreaID`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `ServerIP` | `[nvarchar](255) NULL` | Lokale SIP-Bind-IP oder durch Semikolon getrennte Liste von IP-Adressen. Die SIP-Gateway-Logik prüft lokal bindbare Adressen. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SipProxy` | `[nvarchar](255) NULL` | Optionale globale SIP-Proxy-Konfiguration. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Registrar` | `[nvarchar](255) NULL` | Globaler Registrar-/Servername für interne SIP-Konfiguration. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Realm` | `[nvarchar](255) NULL` | Globaler SIP-Digest-Realm für interne Authentifizierung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `StunServer` | `[nvarchar](255) NULL` | Optionaler globaler STUN-Server. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `OutboundProxy` | `[nvarchar](255) NULL` | Optionaler globaler Outbound-Proxy. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SipTransport` | `[nvarchar](10) NULL` | Globales SIP-Transportprofil; die Settings-Validierung akzeptiert UDP, TCP, TLS oder UDP+TCP. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SipPort` | `[int] NULL` | Lokaler SIP-Listenport des Servers. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UseSRTP` | `[bit] NULL` | Globale SRTP-Einstellung: 1 aktiviert, 0 deaktiviert. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RtpPortStart` | `[int] NULL` | Beginn des globalen RTP-Portbereichs; der Code verlangt eine gerade positive Portnummer. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RtpPortEnd` | `[int] NULL` | Ende des globalen RTP-Portbereichs; muss ausreichend oberhalb des Startports liegen. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `GSMAudioPort` | `[int] NULL` | Separater konfigurierter GSM-Audioport; optional laut Settings-Modell. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SIPCodec` | `[int] NULL` | Ausgewählter SIP-Codec: ID aus TK_AudioCodec.ID, kein RTP-Payload-Type. | [`TK_AudioCodec`](#tk_audiocodec).`ID`: Codebezug; kein SQL-Fremdschlüssel. LoadServerSettings/Medienauswahl verwenden die Codec-ID. |
| `GSMCodec` | `[int] NULL` | Ausgewählter GSM-Bridge-Codec: ID aus TK_AudioCodec.ID; der Code prüft zusätzliche Bridge-Eignung. | [`TK_AudioCodec`](#tk_audiocodec).`ID`: Codebezug; kein SQL-Fremdschlüssel. Zusätzlich prüft die Anwendung die GSM-Bridge-Eignung. |
| `RegisterExpire` | `[int] NULL` | Standardgültigkeitsdauer interner SIP-Registrierungen in Sekunden. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UseAdsProfileMediaPath` | `[bit] NOT NULL` | Schalter für benutzerbezogene Medienablage unter ADS-Profilpfaden. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UserMediaRootPath` | `[nvarchar](260) NULL` | Optionales Stammverzeichnis der Medienablage. Ohne Wert verwendet die Anwendung ihren vorhandenen Media-Fallback am Programmverzeichnis. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UserMediaFolderName` | `[nvarchar](100) NOT NULL` | Name des Benutzer-Unterordners der Medienablage, beispielsweise User. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](7) NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LogLevel` | `[nvarchar](10) NOT NULL` | Globales Protokollierungsniveau; Schema erlaubt Info, Warn oder Error. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `DebugLoggingEnabled` | `[bit] NOT NULL` | Zusätzlicher Schalter für Debug-Protokollierung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `PublicInternetIPv4` | `[nvarchar](64) NULL` | Zuletzt gespeicherte bzw. konfigurierte öffentliche IPv4-Adresse; kein lokaler SIP-Bind-Endpunkt. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `PublicInternetIPv6` | `[nvarchar](128) NULL` | Zuletzt gespeicherte bzw. konfigurierte öffentliche IPv6-Adresse. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `PublicInternetIPSource` | `[nvarchar](128) NULL` | Quelle/Methode der Ermittlung der öffentlichen Internet-IP. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `PublicInternetIPLastCheckedAt` | `[datetime2](7) NULL` | Zeitpunkt der letzten Prüfung der öffentlichen Internet-IP. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `PublicInternetIPLastChangedAt` | `[datetime2](7) NULL` | Zeitpunkt der zuletzt festgestellten Änderung der öffentlichen Internet-IP. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `PublicInternetIPLastError` | `[nvarchar](1000) NULL` | Zuletzt gespeicherter Fehler bei der Ermittlung der öffentlichen Internet-IP. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK__TK.Prope__3214EC07E28444BB`: PRIMARY KEY auf `[Id] ASC`.
- Standardwert `SipTransport`: `('UDP')` (`DF__TK.Proper__SipTr__4BAC3F29`).
- Standardwert `SipPort`: `((5060))` (`DF__TK.Proper__SipPo__4AB81AF0`).
- Standardwert `UseSRTP`: `((0))` (`DF__TK.Proper__UseSR__4E88ABD4`).
- Standardwert `RtpPortStart`: `((10000))` (`DF__TK.Proper__RtpPo__4CA06362`).
- Standardwert `RtpPortEnd`: `((20000))` (`DF__TK.Proper__RtpPo__4D94879B`).
- Standardwert `RegisterExpire`: `((300))` (`DF__TK.Proper__Regis__4F7CD00D`).
- Standardwert `UseAdsProfileMediaPath`: `((0))` (`DF_TK_Properties_UseAdsProfileMediaPath`).
- Standardwert `UserMediaFolderName`: `(N'User')` (`DF_TK_Properties_UserMediaFolderName`).
- Standardwert `CreatedAt`: `(sysdatetime())` (`DF__TK.Proper__Creat__5070F446`).
- Standardwert `LogLevel`: `(N'Info')` (`DF_TK_Properties_LogLevel`).
- Standardwert `DebugLoggingEnabled`: `((0))` (`DF_TK_Properties_DebugLoggingEnabled`).
- Prüfung `CHK_TK_Properties_DebugLoggingEnabled`: `(([DebugLoggingEnabled]=(1) OR [DebugLoggingEnabled]=(0)))`.
- Prüfung `CHK_TK_Properties_LogLevel`: `(([LogLevel]=N'Info' OR [LogLevel]=N'Warn' OR [LogLevel]=N'Error'))`.

### Verbindungen zu anderen Tabellen

- Ausgehend, **FK**: `AreaID` → [`TK_DEAreaCode`](#tk_deareacode).`ID` (`FK_TK_Properties_AreaID`).
- Ausgehend, **FK**: `CountryID` → [`TK_CountryPrefix`](#tk_countryprefix).`ID` (`FK_TK_Properties_CountryID`).
- Ausgehend, fachlich/Code: `SIPCodec` → [`TK_AudioCodec`](#tk_audiocodec).`ID`. Codebezug; kein SQL-Fremdschlüssel. LoadServerSettings/Medienauswahl verwenden die Codec-ID.
- Ausgehend, fachlich/Code: `GSMCodec` → [`TK_AudioCodec`](#tk_audiocodec).`ID`. Codebezug; kein SQL-Fremdschlüssel. Zusätzlich prüft die Anwendung die GSM-Bridge-Eignung.

## TK_CountryPrefix

### Beschreibung

**Zweck heute:** Katalog internationaler Länder- und Telefonvorwahlen.

**Verwendung:** Länderauswahl/-auflösung in den Servereinstellungen; TK_Properties.CountryID referenziert einen konkreten Katalogdatensatz.

**Erstinstallation:** Vollständig: 250 Datensätze aus dem aktuellen Export.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `GUID` | `AS ([dbo].[TK_GUID](concat(isnull([ISO2],N''),N'\|',isnull([CountryName],N''),N'\|',isnull([PrefixDigits],N'')))) PERSISTED` | Persistierte, berechnete technische GUID aus den in der SQL-Formel genannten Merkmalen über dbo.TK_GUID. Ändern sich diese Merkmale, kann sich auch diese GUID ändern; kein Bezug auf eine GUID-Tabelle. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `ISO2` | `[nvarchar](2) NULL` | Zweistelliger ISO-Ländercode, beispielsweise DE; wegen geteilter Vorwahlpläne nicht automatisch eine eindeutige Tabellen-ID. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ISO3` | `[nvarchar](3) NULL` | Dreistelliger ISO-Ländercode, beispielsweise DEU. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CountryName` | `[nvarchar](150) NOT NULL` | Landesname im Vorwahlkatalog; im aktuellen Deutschland-Datensatz Germany. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `PrefixDisplay` | `[nvarchar](32) NOT NULL` | Darstellungsform der internationalen Vorwahl, beispielsweise +49. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `PrefixDigits` | `[nvarchar](16) NOT NULL` | Internationale Vorwahl nur als Ziffern, beispielsweise 49. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CountryCodeDigits` | `[nvarchar](8) NULL` | Gespeicherte Ländercode-Ziffern für die Nummernnormalisierung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `IsSharedPlan` | `[bit] NOT NULL` | Kennzeichnet die Zugehörigkeit zu einem geteilten internationalen Nummerierungsplan. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SortOrder` | `[int] NOT NULL` | Gespeicherte Sortierreihenfolge für Listen bzw. Auswahl; Bedeutung der Reihenfolge wird vom jeweiligen Ladepfad bestimmt. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Active` | `[bit] NOT NULL` | Aktivierungskennzeichen des Datensatzes: 1 aktiv, 0 deaktiviert. Kein Ersatz für Registrierungs- oder Erreichbarkeitsstatus. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](7) NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK_TK_CountryPrefix`: PRIMARY KEY auf `[ID] ASC`.
- `IX_TK_CountryPrefix_CountryCodeDigits`: INDEX auf `[CountryCodeDigits] ASC, [PrefixDigits] ASC`.
- `IX_TK_CountryPrefix_PrefixDigits`: INDEX auf `[PrefixDigits] ASC, [Active] ASC`.
- `UX_TK_CountryPrefix_GUID`: UNIQUE INDEX auf `[GUID] ASC`.
- `UX_TK_CountryPrefix_ISO2_PrefixDigits`: UNIQUE INDEX auf `[ISO2] ASC, [PrefixDigits] ASC`.
- Standardwert `CreatedAt`: `(sysdatetime())` (`DF_TK_CountryPrefix_CreatedAt`).

### Verbindungen zu anderen Tabellen

- Eingehend, **FK**: [`TK_Properties`](#tk_properties).`CountryID` → `ID` (`FK_TK_Properties_CountryID`).
- Eingehend, fachlich/Code: [`TK_DEAreaCode`](#tk_deareacode).`CountryISO2` → `ISO2`. Fachliche Länderzuordnung über ISO2, kein FK. ISO2 ist wegen geteilter Vorwahlpläne kein garantierter eindeutiger Zielschlüssel.

## TK_DEAreaCode

### Beschreibung

**Zweck heute:** Katalog deutscher Ortsvorwahlen mit berechneten nationalen/internationalen Darstellungen.

**Verwendung:** Ortsauflösung und Anzeige der lokalen Vorwahl; TK_Properties.AreaID referenziert den konkreten Datensatz.

**Erstinstallation:** Vollständig: 5.202 Datensätze aus dem aktuellen Export.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `GUID` | `AS ([dbo].[TK_GUID](concat(isnull([AreaCodeDigits],N''),N'\|',isnull([AreaName],N'')))) PERSISTED` | Persistierte, berechnete technische GUID aus den in der SQL-Formel genannten Merkmalen über dbo.TK_GUID. Ändern sich diese Merkmale, kann sich auch diese GUID ändern; kein Bezug auf eine GUID-Tabelle. | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `CountryISO2` | `[nvarchar](2) NOT NULL` | ISO2-Länderkennung der Ortsvorwahl; fachlicher Bezug auf TK_CountryPrefix.ISO2, ohne FK und ohne garantierte Eindeutigkeit des Zielwerts. | [`TK_CountryPrefix`](#tk_countryprefix).`ISO2`: Fachliche Länderzuordnung über ISO2, kein FK. ISO2 ist wegen geteilter Vorwahlpläne kein garantierter eindeutiger Zielschlüssel. |
| `AreaName` | `[nvarchar](150) NOT NULL` | Orts-/Vorwahlgebietsname, beispielsweise Berlin. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `AreaCodeDigits` | `[nvarchar](10) NOT NULL` | Ortsvorwahl ohne nationale Verkehrsausscheidungsziffer, beispielsweise 30 für Berlin. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `NationalPrefixDisplay` | `AS (concat(N'0',[AreaCodeDigits])) PERSISTED NOT NULL` | Berechnete nationale Darstellung aus 0 + AreaCodeDigits, beispielsweise 030. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `InternationalPrefixDigits` | `AS (concat(N'49',[AreaCodeDigits])) PERSISTED NOT NULL` | Berechnete internationale Ziffernfolge aus 49 + AreaCodeDigits; im aktuellen Schema ausdrücklich auf Deutschland ausgelegt. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `InternationalPrefixDisplay` | `AS (concat(N'+49',[AreaCodeDigits])) PERSISTED NOT NULL` | Berechnete internationale Darstellung aus +49 + AreaCodeDigits, beispielsweise +4930. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Active` | `[bit] NOT NULL` | Aktivierungskennzeichen des Datensatzes: 1 aktiv, 0 deaktiviert. Kein Ersatz für Registrierungs- oder Erreichbarkeitsstatus. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SortOrder` | `[int] NOT NULL` | Gespeicherte Sortierreihenfolge für Listen bzw. Auswahl; Bedeutung der Reihenfolge wird vom jeweiligen Ladepfad bestimmt. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](7) NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK_TK_DEAreaCode`: PRIMARY KEY auf `[ID] ASC`.
- `IX_TK_DEAreaCode_AreaCodeDigits`: INDEX auf `[AreaCodeDigits] ASC, [Active] ASC`.
- `IX_TK_DEAreaCode_AreaName`: INDEX auf `[AreaName] ASC, [Active] ASC`.
- `UX_TK_DEAreaCode_AreaCodeDigits_AreaName`: UNIQUE INDEX auf `[AreaCodeDigits] ASC, [AreaName] ASC`.
- `UX_TK_DEAreaCode_GUID`: UNIQUE INDEX auf `[GUID] ASC`.
- Standardwert `CreatedAt`: `(sysdatetime())` (`DF_TK_DEAreaCode_CreatedAt`).

### Verbindungen zu anderen Tabellen

- Eingehend, **FK**: [`TK_Properties`](#tk_properties).`AreaID` → `ID` (`FK_TK_Properties_AreaID`).
- Ausgehend, fachlich/Code: `CountryISO2` → [`TK_CountryPrefix`](#tk_countryprefix).`ISO2`. Fachliche Länderzuordnung über ISO2, kein FK. ISO2 ist wegen geteilter Vorwahlpläne kein garantierter eindeutiger Zielschlüssel.

## TK_AudioCodec

### Beschreibung

**Zweck heute:** Katalog der Audio-/RTP-Codecs mit Formaten, Parametern und Einsatzmöglichkeiten.

**Verwendung:** Service lädt aktive Audioeinträge. TK_Properties wählt SIP-/GSM-Codecs über deren Tabellen-IDs; Medienpfade prüfen weitere Eignungsmerkmale.

**Erstinstallation:** Vollständig: 10 Datensätze. Für den SIP-Start muss mindestens ein gültiger aktiver Audioeintrag vorhanden sein.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `CodecKey` | `[nvarchar](50) NOT NULL` | Eindeutiger fachlicher Codec-Schlüssel, beispielsweise PCMA, G722 oder PCM16_16K. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `DisplayName` | `[nvarchar](100) NOT NULL` | Lesbarer Anzeigename für Oberfläche oder fachliche Darstellung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Description` | `[nvarchar](255) NULL` | Zusätzliche textliche Beschreibung des Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `MediaType` | `[nvarchar](20) NOT NULL` | Medienkategorie des Codec-Datensatzes; der aktuelle aktive Codec-Lader filtert auf Audio. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RtpPayloadType` | `[int] NULL` | RTP-Payload-Type von 0 bis 127, falls zugeordnet; kein Fremdschlüssel und nicht identisch mit TK_AudioCodec.ID. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ClockRate` | `[int] NOT NULL` | Audio-Abtastrate des Codecs in Hz. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RtpClockRate` | `[int] NULL` | RTP-Zeitstempel-Taktrate in Hz; kann von ClockRate abweichen, insbesondere bei G722. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Channels` | `[int] NOT NULL` | Anzahl der Audiokanäle. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Bitrate` | `[int] NULL` | Codec-/Audiobitrate in Bit pro Sekunde, soweit eingetragen. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RtpEncodingName` | `[nvarchar](50) NOT NULL` | Codecname für RTP/SDP, beispielsweise PCMA, G722 oder L16. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `RtpFormatParams` | `[nvarchar](255) NULL` | Zusätzliche SDP-fmtp-Parameter des Codecs. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SampleFormat` | `[nvarchar](30) NULL` | Internes Audio-Sampleformat, beispielsweise PCM16 oder ALaw. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `FrameDurationMs` | `[int] NULL` | Dauer eines Codec-Frames in Millisekunden. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `PtimeMs` | `[int] NULL` | Bevorzugte RTP-Paketierungsdauer in Millisekunden. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `MaxPtimeMs` | `[int] NULL` | Maximale konfigurierte RTP-Paketierungsdauer in Millisekunden. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CanDecode` | `[bit] NOT NULL` | Kennzeichnet die konfigurierte Decodierfähigkeit des Codecs. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CanEncode` | `[bit] NOT NULL` | Kennzeichnet die konfigurierte Encodierfähigkeit des Codecs. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `IsPassThrough` | `[bit] NOT NULL` | Kennzeichnet die vorgesehene Weitergabe ohne Transcodierung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `IsDefault` | `[bit] NOT NULL` | Kennzeichnung eines Standardcodecs im Katalog. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `IsPreferred` | `[bit] NOT NULL` | Kennzeichnung eines bevorzugten Codecs im Katalog. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Active` | `[bit] NOT NULL` | Aktivierungskennzeichen des Datensatzes: 1 aktiv, 0 deaktiviert. Kein Ersatz für Registrierungs- oder Erreichbarkeitsstatus. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SupportsSip` | `[bit] NOT NULL` | Codec ist für SIP-Verwendung vorgesehen. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SupportsRtp` | `[bit] NOT NULL` | Codec ist für RTP-Verwendung vorgesehen. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SupportsGsmBridge` | `[bit] NOT NULL` | Codec ist für die GSM-Bridge vorgesehen; zusätzlich gelten die Auswahlprüfungen im Code. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SupportsInternal` | `[bit] NOT NULL` | Codec ist für interne Verbindungen vorgesehen. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SupportsExternal` | `[bit] NOT NULL` | Codec ist für externe Verbindungen vorgesehen. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SortOrder` | `[int] NOT NULL` | Gespeicherte Sortierreihenfolge für Listen bzw. Auswahl; Bedeutung der Reihenfolge wird vom jeweiligen Ladepfad bestimmt. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `JsonConfig` | `[nvarchar](max) NULL` | Zusätzliche objektbezogene JSON-Konfiguration. Enthaltene Ziel-/Menüwerte werden von der jeweiligen Bereichslogik ausgewertet, nicht durch SQL-Fremdschlüssel. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](7) NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK_TK_AudioCodec`: PRIMARY KEY auf `[ID] ASC`.
- `UQ_TK_AudioCodec_CodecKey`: UNIQUE auf `[CodecKey] ASC` (separat im SQL angelegt).
- `IX_TK_AudioCodec_Active_SortOrder`: INDEX auf `[Active] ASC, [SortOrder] ASC, [CodecKey] ASC`.
- `IX_TK_AudioCodec_RtpEncodingName_ClockRate`: INDEX auf `[RtpEncodingName] ASC, [ClockRate] ASC`.
- `IX_TK_AudioCodec_SupportsGsmBridge`: INDEX auf `[SupportsGsmBridge] ASC, [Active] ASC, [SortOrder] ASC`.
- Standardwert `MediaType`: `('Audio')` (`DF_TK_AudioCodec_MediaType`).
- Standardwert `Channels`: `((1))` (`DF_TK_AudioCodec_Channels`).
- Standardwert `CanDecode`: `((1))` (`DF_TK_AudioCodec_CanDecode`).
- Standardwert `CanEncode`: `((1))` (`DF_TK_AudioCodec_CanEncode`).
- Standardwert `IsPassThrough`: `((0))` (`DF_TK_AudioCodec_IsPassThrough`).
- Standardwert `IsDefault`: `((0))` (`DF_TK_AudioCodec_IsDefault`).
- Standardwert `IsPreferred`: `((0))` (`DF_TK_AudioCodec_IsPreferred`).
- Standardwert `Active`: `((1))` (`DF_TK_AudioCodec_Active`).
- Standardwert `SupportsSip`: `((1))` (`DF_TK_AudioCodec_SupportsSip`).
- Standardwert `SupportsRtp`: `((1))` (`DF_TK_AudioCodec_SupportsRtp`).
- Standardwert `SupportsGsmBridge`: `((0))` (`DF_TK_AudioCodec_SupportsGsmBridge`).
- Standardwert `SupportsInternal`: `((1))` (`DF_TK_AudioCodec_SupportsInternal`).
- Standardwert `SupportsExternal`: `((1))` (`DF_TK_AudioCodec_SupportsExternal`).
- Standardwert `SortOrder`: `((0))` (`DF_TK_AudioCodec_SortOrder`).
- Standardwert `CreatedAt`: `(sysdatetime())` (`DF_TK_AudioCodec_CreatedAt`).
- Prüfung `CHK_TK_AudioCodec_Bitrate`: `(([Bitrate] IS NULL OR [Bitrate]>(0)))`.
- Prüfung `CHK_TK_AudioCodec_Channels`: `(([Channels]>(0)))`.
- Prüfung `CHK_TK_AudioCodec_ClockRate`: `(([ClockRate]>(0)))`.
- Prüfung `CHK_TK_AudioCodec_FrameDurationMs`: `(([FrameDurationMs] IS NULL OR [FrameDurationMs]>(0)))`.
- Prüfung `CHK_TK_AudioCodec_MaxPtimeMs`: `(([MaxPtimeMs] IS NULL OR [MaxPtimeMs]>(0)))`.
- Prüfung `CHK_TK_AudioCodec_MediaType`: `(([MediaType]='Text' OR [MediaType]='Video' OR [MediaType]='Audio'))`.
- Prüfung `CHK_TK_AudioCodec_PtimeMs`: `(([PtimeMs] IS NULL OR [PtimeMs]>(0)))`.
- Prüfung `CHK_TK_AudioCodec_RtpClockRate`: `(([RtpClockRate] IS NULL OR [RtpClockRate]>(0)))`.
- Prüfung `CHK_TK_AudioCodec_RtpPayloadType`: `(([RtpPayloadType] IS NULL OR [RtpPayloadType]>=(0) AND [RtpPayloadType]<=(127)))`.

### Verbindungen zu anderen Tabellen

- Eingehend, **FK**: [`TK_MailBoxMessage`](#tk_mailboxmessage).`CodecID` → `ID` (`FK_TK_MailBoxMessage_CodecID`).
- Eingehend, fachlich/Code: [`TK_Properties`](#tk_properties).`SIPCodec` → `ID`. Codebezug; kein SQL-Fremdschlüssel. LoadServerSettings/Medienauswahl verwenden die Codec-ID.
- Eingehend, fachlich/Code: [`TK_Properties`](#tk_properties).`GSMCodec` → `ID`. Codebezug; kein SQL-Fremdschlüssel. Zusätzlich prüft die Anwendung die GSM-Bridge-Eignung.

## TK_MailBoxMessage

### Beschreibung

**Zweck heute:** Datenbankbasierter Audiospeicher mit Benutzerbezug und Formatmetadaten.

**Verwendung:** Trotz des Tabellennamens speichert der aktuelle konkrete Pfad die aktive persönliche Mailboxansage als WAV-Blob und AudioRole=Prompt. Keine ausschließlich für eingegangene Sprachnachrichten bestimmte Tabelle.

**Erstinstallation:** Leer; persönliche Ansagen/Audioaufnahmen werden im Betrieb erzeugt.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `GUID` | `[uniqueidentifier] NOT NULL` | Zusätzliche technische GUID des Audioobjekts; der aktuelle Mailboxansage-Upsert erzeugt sie mit NEWID(). | Technische/fachliche Kennung, kein zusätzlicher SQL-Fremdschlüssel. Details siehe Bedeutung und ID-Bildung. |
| `UserID` | `[int] NOT NULL` | ID des zugeordneten internen Benutzers aus TK_SIP_User.ID; die Beziehung ist in der letzten Spalte ausgewiesen. | **FK:** [`TK_SIP_User`](#tk_sip_user).`ID`; `FK_TK_MailBoxMessage_UserID`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `AudioKey` | `[nvarchar](100) NOT NULL` | Fachlicher Audioschlüssel; der aktuelle Mailboxansage-Pfad verwendet mailbox_greeting_<UserID>. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `DisplayName` | `[nvarchar](150) NOT NULL` | Lesbarer Anzeigename für Oberfläche oder fachliche Darstellung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Description` | `[nvarchar](500) NULL` | Zusätzliche textliche Beschreibung des Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `AudioRole` | `[nvarchar](30) NOT NULL` | Rolle des Audioobjekts; Schema erlaubt Prompt, Recording, Voicemail, MusicOnHold, SystemTone oder Other. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `FileNameOriginal` | `[nvarchar](260) NULL` | Ursprünglicher Dateiname des gespeicherten Audioobjekts. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `FileExtension` | `[nvarchar](20) NULL` | Dateiendung des Audioobjekts, beispielsweise .wav. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ContentType` | `[nvarchar](100) NULL` | MIME-Typ der Audiodaten, beispielsweise audio/wav. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `AudioFormat` | `[nvarchar](30) NOT NULL` | Gespeichertes Audioformat; Schema erlaubt WAV, MP3, PCM, GSM, ALaw, ULaw, Opus oder Other. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CodecID` | `[int] NULL` | Optionaler Bezug auf TK_AudioCodec.ID; beim aktuellen Mailboxansage-Upsert wird NULL gesetzt. | **FK:** [`TK_AudioCodec`](#tk_audiocodec).`ID`; `FK_TK_MailBoxMessage_CodecID`. SQL Server prüft vorhandene Ziel-ID (bei nullable Spalten außer NULL). |
| `SampleRateHz` | `[int] NULL` | Abtastrate des gespeicherten Audioobjekts in Hz. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Channels` | `[int] NULL` | Anzahl der Audiokanäle. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Bitrate` | `[int] NULL` | Codec-/Audiobitrate in Bit pro Sekunde, soweit eingetragen. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `DurationMs` | `[int] NULL` | Dauer des Audioobjekts in Millisekunden. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `FileSizeBytes` | `[bigint] NOT NULL` | Größe des AudioBlob-Inhalts in Bytes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ContentHashSha256` | `[varbinary](32) NULL` | Binärer SHA-256-Inhaltshash des AudioBlob, insbesondere für Vergleich/Identifikation; keine fremde Datensatz-ID. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `AudioBlob` | `[varbinary](max) NOT NULL` | Binärer Audioinhalt direkt in der Datenbank. Die aktuelle Mailboxansage wird als WAV-Blob gespeichert. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `JsonConfig` | `[nvarchar](max) NULL` | Zusätzliche objektbezogene JSON-Konfiguration. Enthaltene Ziel-/Menüwerte werden von der jeweiligen Bereichslogik ausgewertet, nicht durch SQL-Fremdschlüssel. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `Active` | `[bit] NOT NULL` | Aktivierungskennzeichen des Datensatzes: 1 aktiv, 0 deaktiviert. Kein Ersatz für Registrierungs- oder Erreichbarkeitsstatus. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `SortOrder` | `[int] NOT NULL` | Gespeicherte Sortierreihenfolge für Listen bzw. Auswahl; Bedeutung der Reihenfolge wird vom jeweiligen Ladepfad bestimmt. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](7) NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK_TK_MailBoxMessage`: PRIMARY KEY auf `[ID] ASC`.
- `UQ_TK_MailBoxMessage_AudioKey`: UNIQUE auf `[AudioKey] ASC` (separat im SQL angelegt).
- `UQ_TK_MailBoxMessage_GUID`: UNIQUE auf `[GUID] ASC` (separat im SQL angelegt).
- `UQ_TK_MailBoxMessage_UserID`: UNIQUE auf `[UserID] ASC` (separat im SQL angelegt).
- `IX_TK_MailBoxMessage_Active_Role_SortOrder`: INDEX auf `[Active] ASC, [AudioRole] ASC, [SortOrder] ASC, [AudioKey] ASC`.
- `IX_TK_MailBoxMessage_ContentHashSha256`: INDEX auf `[ContentHashSha256] ASC`; Filter `([ContentHashSha256] IS NOT NULL)`.
- Standardwert `GUID`: `(newid())` (`DF_TK_MailBoxMessage_GUID`).
- Standardwert `AudioRole`: `('Prompt')` (`DF_TK_MailBoxMessage_AudioRole`).
- Standardwert `AudioFormat`: `('WAV')` (`DF_TK_MailBoxMessage_AudioFormat`).
- Standardwert `Active`: `((1))` (`DF_TK_MailBoxMessage_Active`).
- Standardwert `SortOrder`: `((0))` (`DF_TK_MailBoxMessage_SortOrder`).
- Standardwert `CreatedAt`: `(sysdatetime())` (`DF_TK_MailBoxMessage_CreatedAt`).
- Prüfung `CHK_TK_MailBoxMessage_AudioBlob`: `((datalength([AudioBlob])>(0)))`.
- Prüfung `CHK_TK_MailBoxMessage_AudioFormat`: `(([AudioFormat]='WAV' OR [AudioFormat]='MP3' OR [AudioFormat]='PCM' OR [AudioFormat]='GSM' OR [AudioFormat]='ALaw' OR [AudioFormat]='ULaw' OR [AudioFormat]='Opus' OR [AudioFormat]='Other'))`.
- Prüfung `CHK_TK_MailBoxMessage_AudioRole`: `(([AudioRole]='Prompt' OR [AudioRole]='Recording' OR [AudioRole]='Voicemail' OR [AudioRole]='MusicOnHold' OR [AudioRole]='SystemTone' OR [AudioRole]='Other'))`.
- Prüfung `CHK_TK_MailBoxMessage_Bitrate`: `(([Bitrate] IS NULL OR [Bitrate]>(0)))`.
- Prüfung `CHK_TK_MailBoxMessage_Channels`: `(([Channels] IS NULL OR [Channels]>(0)))`.
- Prüfung `CHK_TK_MailBoxMessage_DurationMs`: `(([DurationMs] IS NULL OR [DurationMs]>(0)))`.
- Prüfung `CHK_TK_MailBoxMessage_FileSizeBytes`: `(([FileSizeBytes]>(0)))`.
- Prüfung `CHK_TK_MailBoxMessage_SampleRateHz`: `(([SampleRateHz] IS NULL OR [SampleRateHz]>(0)))`.

### Verbindungen zu anderen Tabellen

- Ausgehend, **FK**: `CodecID` → [`TK_AudioCodec`](#tk_audiocodec).`ID` (`FK_TK_MailBoxMessage_CodecID`).
- Ausgehend, **FK**: `UserID` → [`TK_SIP_User`](#tk_sip_user).`ID` (`FK_TK_MailBoxMessage_UserID`).

## TK_RuntimeAreaControl

### Beschreibung

**Zweck heute:** Gemeinsame Bereichssteuerung mit Sollzustand der GUI und Istzustand des Services.

**Verwendung:** Der aktuelle automatische Startresolver wertet SIPInt, Provider und GSM aus. SVC ist als zusätzlicher AreaKey im Schema/Installationsdatensatz vorhanden, aber kein unabhängig ausgewertetes Startflag dieses Resolvers.

**Erstinstallation:** Vier Datensätze: SIPInt, Provider, GSM, SVC. DesiredEnabled=0, ActualState=Stopped; frühere Statuszeiten und Fehler NULL.

### Spalten

| Spalte | Definition | Bedeutung | Verbindung / ID-Herkunft |
|---|---|---|---|
| `ID` | `[int] IDENTITY(1,1) NOT NULL` | Automatisch hochgezählter Primärschlüssel dieses Datensatzes; nicht mit einer Rufnummer oder Call-ID gleichzusetzen. | Primärschlüssel; eingehende Beziehungen siehe unten. |
| `AreaKey` | `[nvarchar](30) NOT NULL` | Name des gesteuerten Runtime-Bereichs; Schema erlaubt SIPInt, Provider, GSM und SVC. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `DesiredEnabled` | `[bit] NOT NULL` | Vom Betreiber/der GUI gewünschter Aktivierungszustand; der Service setzt den Sollzustand um. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `ActualState` | `[nvarchar](30) NOT NULL` | Vom Service gemeldeter Istzustand: Stopped, Starting, Running, Stopping oder Error. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastCommandAt` | `[datetime2](7) NULL` | Zeitpunkt der letzten gespeicherten Sollzustandsänderung. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastAppliedAt` | `[datetime2](7) NULL` | Zeitpunkt der letzten Rückmeldung/Anwendung eines Istzustands durch den Service. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastStartAt` | `[datetime2](7) NULL` | Zeitpunkt des zuletzt gespeicherten Übergangs nach Starting. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastStopAt` | `[datetime2](7) NULL` | Zeitpunkt des zuletzt gespeicherten Übergangs nach Stopped. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `LastError` | `[nvarchar](500) NULL` | Zuletzt gespeicherter Fehlertext für das betreffende Objekt; NULL bedeutet kein eingetragener Fehler. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `CreatedAt` | `[datetime2](7) NOT NULL` | Zeitpunkt der Anlage dieses Datensatzes. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |
| `UpdatedAt` | `[datetime2](7) NOT NULL` | Gespeicherter Zeitpunkt der letzten Änderung; NULL, sofern noch kein Änderungszeitpunkt eingetragen wurde. | Kein tabellenübergreifender ID-Fremdschlüssel im Schema; Bedeutung siehe Spaltenbeschreibung. |

### Schlüssel, Standardwerte und Prüfungen

Die Datentypen und berechneten Formeln stehen oben. Schlüssel und Indizes des aktuellen SQL-Schemas:

- `PK_TK_RuntimeAreaControl`: PRIMARY KEY auf `[ID] ASC`.
- `UQ_TK_RuntimeAreaControl_AreaKey`: UNIQUE auf `[AreaKey] ASC` (separat im SQL angelegt).
- Standardwert `DesiredEnabled`: `((0))` (`DF_TK_RuntimeAreaControl_DesiredEnabled`).
- Standardwert `ActualState`: `(N'Stopped')` (`DF_TK_RuntimeAreaControl_ActualState`).
- Standardwert `CreatedAt`: `(sysdatetime())` (`DF_TK_RuntimeAreaControl_CreatedAt`).
- Standardwert `UpdatedAt`: `(sysdatetime())` (`DF_TK_RuntimeAreaControl_UpdatedAt`).
- Prüfung `CHK_TK_RuntimeAreaControl_ActualState`: `(([ActualState]=N'Error' OR [ActualState]=N'Stopping' OR [ActualState]=N'Running' OR [ActualState]=N'Starting' OR [ActualState]=N'Stopped'))`.
- Prüfung `CHK_TK_RuntimeAreaControl_AreaKey`: `(([AreaKey]=N'SVC' OR [AreaKey]=N'GSM' OR [AreaKey]=N'Provider' OR [AreaKey]=N'SIPInt'))`.

### Verbindungen zu anderen Tabellen

Keine ausgehenden oder eingehenden tabellenübergreifenden ID-Beziehungen im geprüften SQL-/Codebestand nachgewiesen.

## Abgleich und bekannte Grenzen

- Alle tatsächlichen Spalten der 23 CREATE-TABLE-Definitionen sind enthalten. Wiederholungen einer ID innerhalb von PRIMARY-KEY- oder Indexdefinitionen wurden nicht als zusätzliche Spalten gezählt.
- Alle 24 FOREIGN-KEY-Definitionen sind mit Quell- und Zielspalte erfasst. Zusätzlich dokumentierte Beziehungen sind ausdrücklich nicht als SQL-Fremdschlüssel ausgegeben.
- Einige ältere Methoden in TKSQL_Commands verwenden noch `TK_ExternalNumberRouting.TargetID`; im aktuellen Schema existiert diese Spalte nicht. Dokumentiert wird daher die tatsächliche Spalte `InternalNumberID`. Eine Codekorrektur gehört nicht zu dieser Dokumentation.
- Die vorhandene Spalte `TK_RuntimeRegistration.ProviderStateID` hat einen FK. Der geprüfte Provider-Laufzeit-Upsert setzt sie jedoch nicht ausdrücklich. Eine automatisch immer bestehende Verbindung wird deshalb nicht behauptet.
- Für `TK_SIP_Registration` wurde im aktuellen SRC außer einer Tabellen-Allowlist kein regulärer Zugriff gefunden; die aktive Registrierungsauflösung liest TK_RuntimeRegistration. Die klassische Tabelle bleibt trotzdem vollständig dokumentiert.
- `MenuParentCode`, `IsMenuEntry`, `IsDirectDialAllowed` und weitere Menümetadaten beschreiben die gespeicherte Konfiguration. Nicht jeder aktuelle C#-Ladepfad liest sämtliche Menüspalten; vorhandene Konfiguration und nachgewiesene Auswertung sind zu unterscheiden.
- Das aktuelle Schema setzt keine FKs für `TK_Properties.SIPCodec/GSMCodec` oder für die beiden ID-Verbindungen der externen Routingtabelle. Diese Beziehungen sind durch Codeabfragen belegt und müssen fachlich konsistent bleiben.
- Die Tabellenstruktur aus Installations- und Originalexport wurde abgeglichen. Die Dokumentation trifft keine Aussage über nach dem Export vorgenommene Änderungen an einer laufenden Datenbank.

## Quellen im Repository

- [TK-Server_DB-INSTALL.sql](./TK-Server_DB-INSTALL.sql): verbindliche Spalten, Typen, PK/UNIQUE, Indizes, Defaults, CHECKs und Fremdschlüssel dieser Fassung.
- [TK-Server_DB-ORIGINAL.sql](./TK-Server_DB-ORIGINAL.sql): Strukturvergleich mit dem aktuellen Export; Datensätze werden hier nicht veröffentlicht.
- [SQL_SCHEMA.MD](./SQL_SCHEMA.MD): bisheriger Aufbau der Beschreibung als Vorlage.
- [TKSQL_Commands.cs](../../SRC/SQL/TKSQL_Commands.cs): zentrale Abfragen, JOINs, Upserts, Quellen-IDs und Codecbezüge.
- [TKBase_ServerSettings.cs](../../SRC/BASE/TKBase_ServerSettings.cs): Pflichtwerte, Ports, Medienpfade und Settings-Validierung.
- [TKBase_ServiceRunTime.cs](../../SRC/BASE/TKBase_ServiceRunTime.cs): Bereichssteuerung und Startvoraussetzungen.
- [TKBase_RuntimeCallManager.cs](../../SRC/BASE/TKBase_RuntimeCallManager.cs): Live-Gesprächssicht und Zuordnung zur Historie.
- [TKBase_SessionManager.cs](../../SRC/BASE/TKBase_SessionManager.cs): Session-/Bridge-ID-Bildung und Leg-Zuordnung.
- [TK_ServerEinstellung.cs](../../SRC/FormView/TK_ServerEinstellung.cs): Auflösung von CountryID und AreaID.
- [TKSVC_ServiceRegistry.cs](../../SRC/SVC/TKSVC_ServiceRegistry.cs) und [TKSVC_Session.cs](../../SRC/SVC/TKSVC_Session.cs): Laden und Auflösen der Servicekonfiguration.
- [TKSipInt_Authentication.cs](../../SRC/SipInt/TKSipInt_Authentication.cs): Lesen verschlüsselter interner SIP-Passwörter.
- [TKSipExt_Registration.cs](../../SRC/SipExt/TKSipExt_Registration.cs): Providerpasswörter und externe Nummernsynchronisation.
