# TK-Server – strukturelle Funktion von Server, Managern und Bereichen

Stand: 2026-04-26  
Basis: `2026.04.26_02.28_TK-Server.zip`  
Umfang: 76 `.cs` Dateien, inklusive Forms und Designer-Dateien.

## Lesart

Diese Ansicht ist keine physische Ordnerstruktur. Sie zeigt die strukturelle Rolle innerhalb der Anwendung:

- `TKBase_Server.cs` ist der zentrale Laufzeitknoten.
- Die `TKBase_*Manager` halten jeweils eine Basiskompetenz.
- Die Bereichsdateien `TKSipInt_*`, `TKSipExt_*`, `TKGSM_*`, `TKSVC_*` sind fachliche Ausprägungen unterhalb dieser Basiskompetenzen.
- UI-Forms liegen seitlich daneben und steuern bzw. zeigen Zustände an, sie sollen die Kernlogik nicht besitzen.

## Strukturbaum nach Funktion

```text
TK-Server/
├── Einstieg
│   └── Program.cs
│       └── Startpunkt der Anwendung; initialisiert WinForms und öffnet die Startoberfläche.
│
├── Base / zentrale Server-Orchestrierung
│   └── TKBase_Server.cs
│       ├── strukturelle Funktion
│       │   ├── zentraler Runtime-Knoten des TK-Servers
│       │   ├── hält die Manager zusammen
│       │   ├── startet und stoppt Server-Runtime, SIP-Transport, GSM-Anbindung und Registrierungen
│       │   ├── nimmt technische Eingänge entgegen und verteilt sie an den zuständigen Bereich
│       │   └── soll keine fachliche Bereichslogik dauerhaft selbst besitzen
│       │
│       ├── TKBase_SessionManager.cs
│       │   ├── strukturelle Funktion
│       │   │   ├── erzeugt, hält und beendet Sessions
│       │   │   ├── verwaltet SessionID, Leg-Zuordnung und Laufzeit-Snapshots
│       │   │   ├── kennt die Session-Bereiche, entscheidet aber nicht fachlich die Route
│       │   │   └── trennt SIP intern, SIP extern, GSM und Service-Leg strukturell voneinander
│       │   │
│       │   ├── SIP Intern Session-Struktur
│       │   │   ├── TKSipInt_Session.cs
│       │   │   │   └── hält interne SIP-Leg-A/Leg-B-Sessiondaten.
│       │   │   └── TKSipInt_CallController.cs
│       │   │       ├── koordiniert aktive interne SIP-Call-Aktionen
│       │   │       ├── TKSipInt_Command.cs
│       │   │       │   └── ausgehende/ausführende Kommandos für interne SIP-Legs.
│       │   │       ├── TKSipInt_Event.cs
│       │   │       │   └── eingehende Ereignisse/Rückmeldungen interner SIP-Legs.
│       │   │       └── TKSipInt_Forking.cs
│       │   │           └── Mehrfachklingeln/Branching für interne SIP-Ziele.
│       │   │
│       │   ├── SIP Extern / Provider Session-Struktur
│       │   │   ├── TKSipExt_Session.cs
│       │   │   │   └── hält externe SIP-/Provider-Leg-A/Leg-B-Sessiondaten.
│       │   │   └── TKSipExt_CallController.cs
│       │   │       ├── koordiniert aktive externe Provider-Call-Aktionen
│       │   │       ├── TKSipExt_Command.cs
│       │   │       │   └── ausgehende/ausführende Kommandos für externe SIP-Legs.
│       │   │       ├── TKSipExt_Event.cs
│       │   │       │   └── eingehende Ereignisse/Rückmeldungen externer SIP-Legs.
│       │   │       └── TKSipExt_Forking.cs
│       │   │           └── Mehrfachverbindungen/Branching für Provider- oder externe Ziele.
│       │   │
│       │   ├── GSM Session-Struktur
│       │   │   ├── TKGSM_Session.cs
│       │   │   │   └── hält GSM-Leg-A/Leg-B-Sessiondaten.
│       │   │   └── TKGSM_CallController.cs
│       │   │       ├── koordiniert aktive GSM-Call-Aktionen über Modem/SIM
│       │   │       ├── TKGSM_Command.cs
│       │   │       │   └── ausführende GSM-Kommandos, insbesondere Richtung AT/Call-Control.
│       │   │       ├── TKGSM_Event.cs
│       │   │       │   └── Ereignisse vom GSM-Bereich zurück in die Runtime.
│       │   │       └── TKGSM_Forking.cs
│       │   │           └── GSM-Branching bei mehreren möglichen GSM-Zielen.
│       │   │
│       │   └── Service-Leg Session-Struktur
│       │       ├── TKSVC_Session.cs
│       │       │   └── hält Service-Leg-D-Sessiondaten.
│       │       └── TKSVC_CallController.cs
│       │           ├── koordiniert aktive Service-Leg-Aktionen
│       │           ├── TKSVC_Commands.cs
│       │           │   └── ausführende Kommandos für Service-Leg-D.
│       │           ├── TKSVC_Events.cs
│       │           │   └── Ereignisse/Rückmeldungen aus Service-Legs.
│       │           └── TKSVC_Forking.cs
│       │               └── Forking-/Branching-Struktur für Service-Ziele, falls mehrere Servicepfade möglich sind.
│       │
│       ├── TKBase_RoutingManager.cs
│       │   ├── strukturelle Funktion
│       │   │   ├── zentrale Routing-Schaltstelle
│       │   │   ├── entscheidet Zielbereich: SIP intern, SIP extern, GSM oder SVC
│       │   │   ├── verwendet bereichsspezifische Routing-Dateien für Detailentscheidungen
│       │   │   └── darf keine Session-Lifecycle-Logik ersetzen
│       │   ├── TKSipInt_Routing.cs
│       │   │   └── Routing interner SIP-Teilnehmer und interner Zielnummern.
│       │   ├── TKSipExt_Routing.cs
│       │   │   └── Provider-/externes SIP-Routing inklusive Auswahl externer Wege.
│       │   ├── TKGSM_Routing.cs
│       │   │   └── GSM-Zielauswahl, GSM-Leitungswahl und GSM-Erreichbarkeit.
│       │   └── TKSVC_Routing.cs
│       │       └── Routing auf Service-Legs, Ansagen und Dienstnummern.
│       │
│       ├── TKBase_RegistrationManager.cs
│       │   ├── strukturelle Funktion
│       │   │   ├── zentrale Registrierungsübersicht
│       │   │   ├── trennt interne SIP-Registrierung, Provider-Registrierung und GSM-Registrierung
│       │   │   ├── hält Runtime-Registration-Zustände zusammen
│       │   │   └── darf Registrierungen starten/stoppen, aber keine Call-Session führen
│       │   ├── TKSipInt_Registration.cs
│       │   │   ├── interne SIP-Teilnehmerregistrierung
│       │   │   └── TKSipInt_Authentication.cs
│       │   │       └── Authentifizierung interner SIP-Teilnehmer.
│       │   ├── TKSipExt_Registration.cs
│       │   │   ├── externe SIP-/Provider-Registrierung
│       │   │   └── TKSipExt_TransportProfile.cs
│       │   │       └── Transport-/Profilinformationen für Providerregistrierung.
│       │   └── TKGSM_Registration.cs
│       │       ├── GSM-Geräte-/SIM-Registrierungszustand
│       │       ├── TKGSM_DeviceRegister.cs
│       │       │   └── Erfassung und Verwaltung gefundener GSM-Geräte.
│       │       └── TKGSM_DeviceMapping.cs
│       │           └── Zuordnung von Modem, AT-Port, Audio-Port, SIM und externer Nummer.
│       │
│       ├── TKBase_MediaManager.cs
│       │   ├── strukturelle Funktion
│       │   │   ├── zentrale Medienbeschreibung und Medienkopplung
│       │   │   ├── hält AudioDescription/Codec-/SDP-nahe Laufzeitinformationen
│       │   │   ├── verbindet nicht fachlich die Bereiche, sondern stellt Medienfähigkeit bereit
│       │   │   └── Bereichsmedien bleiben in den jeweiligen TKSip/TKGSM/TKSVC-Dateien
│       │   ├── TKSipInt_Media.cs
│       │   │   └── Medienlogik für interne SIP-Legs.
│       │   ├── TKSipExt_Media.cs
│       │   │   └── Medienlogik für externe SIP-/Provider-Legs.
│       │   ├── TKGSM_Media.cs
│       │   │   └── GSM-Audio-/PCM-Brücke und GSM-Medienzustand.
│       │   └── TKSVC_Media.cs
│       │       └── Medienausgabe/-annahme für Service-Legs, Ansagen und interne Dienste.
│       │
│       ├── TKBase_RuntimeCallManager.cs
│       │   └── strukturelle Funktion: hält Live-Call-Zustände, schreibt/aktualisiert RuntimeCall-Daten und stellt aktive Gespräche für UI/Logik bereit.
│       │
│       ├── TKBase_RuntimeCallEvents.cs
│       │   └── strukturelle Funktion: Ereignisverteiler für RuntimeCall-Änderungen Richtung UI und weitere Beobachter.
│       │
│       ├── TKBase_AudioCodecConfig.cs
│       │   └── strukturelle Funktion: zentrale Codec-Konfiguration, Codec-Auswahl und Codec-Fähigkeiten aus Konfiguration/SQL.
│       │
│       ├── TKBase_SipGateway.cs
│       │   └── strukturelle Funktion: gemeinsamer SIP-TransportHost für interne SIP-Seite und Providerregistrierung; stellt Transport bereit, besitzt aber nicht die Fachlogik.
│       │
│       ├── TKBase_GsmGateway.cs
│       │   └── strukturelle Funktion: Gateway zwischen Base-Runtime und GSM-ServiceHost; überwacht GSM-Geräte, Status und GSM-Call-Zustände.
│       │
│       └── TKBase_LogManager.cs
│           └── strukturelle Funktion: zentrales Logging mit LogLevel, Zielsteuerung und Rotation.
│
├── Bereichssicht / SIP Intern
│   ├── Aufgabe
│   │   ├── interne SIP-Teilnehmer verwalten
│   │   ├── interne REGISTER verarbeiten
│   │   ├── interne INVITE/BYE/CANCEL/Events behandeln
│   │   ├── interne Ziele routen
│   │   └── interne Medienfähigkeit bereitstellen
│   └── Dateien sind funktional unter SessionManager, RoutingManager, RegistrationManager und MediaManager einsortiert.
│
├── Bereichssicht / SIP Extern / Provider
│   ├── Aufgabe
│   │   ├── Providerkonten registrieren
│   │   ├── externe SIP-Wege bereitstellen
│   │   ├── externe eingehende/ausgehende Calls führen
│   │   ├── Providertransport und Providerprofile behandeln
│   │   └── externe Medienfähigkeit bereitstellen
│   └── Dateien sind funktional unter SessionManager, RoutingManager, RegistrationManager und MediaManager einsortiert.
│
├── Bereichssicht / GSM
│   ├── Aufgabe
│   │   ├── GSM-Modems/SIMs erkennen und zuordnen
│   │   ├── GSM-Registrierungs- und Onlinezustände halten
│   │   ├── GSM-Calls über AT/Modem steuern
│   │   ├── GSM-Audio an Medienlogik koppeln
│   │   └── GSM-Ziele routen und ggf. forken
│   └── Dateien sind funktional unter SessionManager, RoutingManager, RegistrationManager und MediaManager einsortiert.
│
├── Bereichssicht / SVC / Service-Legs
│   ├── Aufgabe
│   │   ├── Service-Leg-D bereitstellen
│   │   ├── Dienstnummern und Servicecodes verwalten
│   │   ├── Ansagen/Zeitausgabe und weitere Dienste anbinden
│   │   └── Service-Medien bereitstellen
│   ├── TKSVC_ZeitAnsage.cs
│   │   └── konkrete Service-Funktion für Zeitansage/Systemansage.
│   ├── TKSVC_ServiceRegistry.cs
│   │   └── Registry bekannter Service-Legs und aktiver Serviceangebote.
│   └── TKSVC_ServiceTypes.cs
│       └── Service-Typen, Servicecodes und Konstanten für Dienstlogik.
│
├── SQL / Persistenz
│   ├── TKSQL_ConnectionClass.cs
│   │   └── SQL-Verbindung, Verbindungsaufbau und gemeinsame SQL-Hilfsstrukturen.
│   ├── TKSQL_Commands.cs
│   │   └── zentrale SQL-Kommandos und Datenzugriffe.
│   └── TKSQL_DB.Designer.cs
│       └── generierte Settings-/Datenbank-Designerstruktur.
│
└── UI / Forms + Designer
    ├── strukturelle Funktion
    │   ├── Anzeige und Bedienung der Runtime
    │   ├── Start/Stop/Reload-Befehle an Base/Manager weitergeben
    │   ├── SQL-Stammdaten pflegen
    │   ├── Livezustände anzeigen
    │   └── keine dauerhafte Serverfachlogik besitzen
    │
    ├── TK_Start.cs
    │   └── Start-/Hauptbedienung für Serverstart, Serverstop, Reload und Providerregistrierung.
    ├── TK_Start.Designer.cs
    │   └── Designerstruktur zu TK_Start.
    ├── TK_Server.cs
    │   └── Serverübersicht/Formular für Serverstatus und zentrale Bedienung.
    ├── TK_Server.Designer.cs
    │   └── Designerstruktur zu TK_Server.
    ├── TK_ServerEinstellung.cs
    │   └── Servereinstellungen, Ports, Firewall-/Konfigurationsbedienung.
    ├── TK_ServerEinstellung.Designer.cs
    │   └── Designerstruktur zu TK_ServerEinstellung.
    ├── TK_ProviderAccounts.cs
    │   └── Providerkonten, Providerdaten und Providerregistrierungsbedienung.
    ├── TK_ProviderAccounts.Designer.cs
    │   └── Designerstruktur zu TK_ProviderAccounts.
    ├── TK_GSMDevice.cs
    │   └── GSM-Geräte-/Modem-/SIM-Verwaltung und Geräteprüfung.
    ├── TK_GSMDevice.Designer.cs
    │   └── Designerstruktur zu TK_GSMDevice.
    ├── TK_VoIPUser.cs
    │   └── interne VoIP-/SIP-Benutzerverwaltung.
    ├── TK_VoIPUser.Designer.cs
    │   └── Designerstruktur zu TK_VoIPUser.
    ├── TK_NumberRouting.cs
    │   └── Nummernrouting, externe Nummern und Zielzuordnung.
    ├── TK_NumberRouting.Designer.cs
    │   └── Designerstruktur zu TK_NumberRouting.
    ├── TK_EmergencyCallRule.cs
    │   └── Notruf-/Sonderrufnummernregeln.
    ├── TK_EmergencyCallRule.Designer.cs
    │   └── Designerstruktur zu TK_EmergencyCallRule.
    ├── TK_ServiceLegs.cs
    │   └── Verwaltung von Service-Legs und Dienstnummern.
    ├── TK_ServiceLegs.Designer.cs
    │   └── Designerstruktur zu TK_ServiceLegs.
    ├── TK_LiveCallState.cs
    │   └── Live-Call-Anzeige und RuntimeCall-Statusübersicht.
    ├── TK_LiveCallState.Designer.cs
    │   └── Designerstruktur zu TK_LiveCallState.
    ├── TK_CallLog.cs
    │   └── Gesprächsprotokoll-/CallLog-Anzeige.
    ├── TK_CallLog.Designer.cs
    │   └── Designerstruktur zu TK_CallLog.
    └── TK_GuiCommand.cs
        └── UI-Hilfsbefehle/Dispatcher für nicht-blockierende GUI-Aktionen.
```

## Kurzlogik der Ebenen

```text
Program.cs
└── startet UI

TK_Start / UI
└── fordert Aktionen an

TKBase_Server
└── hält Runtime und Manager zusammen

Manager
├── SessionManager      → Wer existiert gerade als Session / Leg?
├── RoutingManager      → Wohin muss der Ruf?
├── RegistrationManager → Wer ist registriert / erreichbar?
├── MediaManager        → Welche Medien-/Audiofähigkeit gilt?
├── RuntimeCallManager  → Was läuft live und was steht in RuntimeCall?
├── SipGateway          → Welcher SIP-Transport wird gemeinsam genutzt?
├── GsmGateway          → Welche GSM-Geräte/States stehen bereit?
└── LogManager          → Was wird protokolliert?

Bereiche
├── SIP Intern          → interne Teilnehmer
├── SIP Extern          → Provider/externe SIP-Seite
├── GSM                 → Modem/SIM/AT/Audio
└── SVC                 → Dienste/Service-Leg-D
```

## Wichtige strukturelle Trennung

```text
SessionManager
└── Session- und Leg-Lifecycle, aber keine fachliche Zielentscheidung.

RoutingManager
└── Zielentscheidung, aber keine Session-Lifecycle-Verwaltung.

RegistrationManager
└── Erreichbarkeits- und Registrierungszustand, aber keine Call-Führung.

MediaManager
└── Medienfähigkeit und Medienkopplung, aber keine Routingentscheidung.

CallController je Bereich
└── konkrete Call-Aktionen im jeweiligen Bereich.

Command/Event je Bereich
├── Command → Aktion nach unten ausführen
└── Event   → Rückmeldung nach oben melden

Forking je Bereich
└── mehrere mögliche Ziele/Branches sauber verwalten.
```

## Dateizählung

```text
Einstieg                         1
Base / zentrale Manager          11
SIP Intern                       9
SIP Extern / Provider            9
GSM                              10
SVC / Service-Legs               10
SQL / Persistenz                 3
UI / Forms + Designer            23
-----------------------------------
Gesamt                           76
```
