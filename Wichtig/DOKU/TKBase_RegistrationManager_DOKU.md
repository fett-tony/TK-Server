# TKBase_RegistrationManager.cs – Aufgabe, Rolle und Zusammenspiel

## Zweck

`TKBase_RegistrationManager.cs` ist die zentrale Registrierungsinstanz des Servers.

Er bündelt die drei Registrierungsbereiche:

- `TKSipExt_Registra.cs`
- `TKSipInt_Registra.cs`
- `TKGSM_Registra.cs`

Der RegistrationManager ist damit der gemeinsame Einstiegspunkt für alles, was mit Registrierung zu tun hat.

---

## Rolle des TKBase_RegistrationManager

Der `TKBase_RegistrationManager` übernimmt die zentrale Verwaltung und Steuerung der Registrierungsbereiche.

Seine Rolle ist:

- Bereichsobjekte halten
- Bereichsobjekte kontrolliert bereitstellen
- Bereichsobjekte starten, stoppen, zurücksetzen und neu laden
- den Gesamtstatus der Registrierung zusammenfassen
- letzte Aktionen und Fehler pro Bereich verwalten
- gemeinsame Runtime-Registrierungszustände serverweit speichern und pflegen

Er ist damit die übergeordnete Registrierungszentrale des Servers.

---

## Was der Manager konkret tut

### 1. Bereichsinstanzen verwalten

Der Manager hält die drei Bereichsinstanzen im Speicher:

- `_sipExt`
- `_sipInt`
- `_gsm`

Dafür stellt er kontrollierte Zugriffsmethoden bereit:

- `GetSipExtRegistra()`
- `GetSipIntRegistra()`
- `GetGsmRegistra()`

---

### 2. Lebenszyklus der Bereiche steuern

Der Manager steuert den Betriebszustand der Bereiche.

#### SIP extern

- `StartSipExt()`
- `ReloadSipExt()`
- `StopSipExt()`
- `ResetSipExt()`

#### SIP intern

- `StartSipInt()`
- `StopSipInt()`
- `ResetSipInt()`

#### GSM

- `StartGsm()`
- `ExecuteGsmScan()`
- `StopGsm()`
- `ResetGsm()`

#### Gesamtsteuerung

- `StartAll()`
- `StartArea(...)`
- `StopArea(...)`
- `ResetArea(...)`
- `StopAll()`
- `ResetAll()`

Damit ist der RegistrationManager der Betriebsführer für alle Registrierungsbereiche.

---

### 3. Status und Verwaltungszustand zusammenführen

Der Manager sammelt und liefert den Status der einzelnen Bereiche.

Dafür existieren unter anderem:

- `GetSipExtStatus()`
- `GetSipIntStatus()`
- `GetGsmStatus()`
- `GetStatusSnapshot()`

Zusätzlich verwaltet er pro Bereich den Verwaltungszustand, also zum Beispiel:

- letzte Aktion
- Zeitpunkt der letzten Aktion
- letzter Fehler

Hilfsmethoden dafür sind:

- `MarkAction(...)`
- `LogManagerInfo(...)`
- `LogManagerWarn(...)`

Der Manager liefert damit die zentrale Sicht auf den Zustand der Registrierung im laufenden Server.

---

### 4. Gemeinsame Laufzeitdaten für Registrierung pflegen

Der Manager führt außerdem die gemeinsame Runtime-Sicht für Registrierungen.

Dazu gehören unter anderem:

- `SaveInternalRegistration(...)`
- `SaveExternalRegistration(...)`
- `SaveGsmRegistrationState(...)`
- `DeactivateInternalRegistration(...)`
- `CleanupExpiredInternalRegistrations()`

Zusätzlich gibt es die GUID-Bildung für interne Registrierungseinträge:

- `BuildInternalRegistrationGuidCore(...)`
- `BuildInternalRegistrationGuid(...)`

Der Manager ist damit auch die gemeinsame Schreib- und Pflegeinstanz für Registrierungszustände zur Laufzeit.

---

## Rolle der Bereichsdateien

Die Bereichsdateien sind die fachlichen Bearbeiter unterhalb des Managers.

Der `TKBase_RegistrationManager` führt und steuert diese Bereiche. Die Bereichsdateien erledigen die konkrete Arbeit innerhalb ihres Fachbereichs.

---

## TKSipExt_Registra.cs

### Rolle

`TKSipExt_Registra.cs` ist der Registrierungsbereich für externe SIP-Provider.

### Aufgabe

Diese Datei bearbeitet die Registrierung nach außen zu Providern.

Dazu gehören insbesondere:

- Providerkonten laden
- Registrierung für Provider starten
- Registrierung neu laden
- Registrierung stoppen oder zurücksetzen
- Registrierungszustände führen
- Rohdaten und Antworten merken
- Registrierungslogs schreiben
- öffentliche Netzsicht beobachten
- externe Nummern und Routing synchronisieren

### Wichtige Methoden

- `Start()`
- `Stop()`
- `Reset()`
- `Reload()`
- `LoadProviderAccounts()`
- `LoadAndStartAllAccounts()`
- `GetRegistrationStates()`
- `SaveRegistrationState(...)`
- `WriteRegistrationLog(...)`
- `SyncExternalNumberAndRouting(...)`
- `TrySetExternalNumberOffline(...)`

Damit ist `TKSipExt_Registra` der Bearbeiter für alles, was mit Provider-Registrierung und deren Laufzeitstatus zu tun hat.

---

## TKSipInt_Registra.cs

### Rolle

`TKSipInt_Registra.cs` ist der Registrierungsbereich für interne SIP-Endgeräte und interne Benutzer.

### Aufgabe

Diese Datei bearbeitet die interne Registrierung am eigenen Server.

Dazu gehören insbesondere:

- interne Registrierung annehmen
- authentifizierte Registrierung verarbeiten
- Trusted-Registrierung verarbeiten
- Abmeldung verarbeiten
- abgelaufene Registrierungen bereinigen
- Registrierung nach Nummer nachschlagen
- Nonces und Authentifizierungsdaten verwalten

### Wichtige Methoden

- `Start()`
- `Stop()`
- `Reset()`
- `Register(...)`
- `RegisterTrusted(...)`
- `Unregister(...)`
- `CleanupExpired()`
- `GetRegistrationByNumber(...)`
- `HandleRegisterReceived(...)`
- `HandleRegisterAuthenticated(...)`
- `HandleRegisterAsync(...)`
- `CleanupExpiredRegistrations()`
- `ArmNonce(...)`
- `IsNonceValid(...)`
- `CleanupExpiredNonces()`

Damit ist `TKSipInt_Registra` der Bearbeiter für die interne SIP-Registrierung der Teilnehmer am eigenen System.

---

## TKGSM_Registra.cs

### Rolle

`TKGSM_Registra.cs` ist der Registrierungsbereich für den GSM-Zustand innerhalb des Servers.

### Aufgabe

Diese Datei bearbeitet den GSM-bezogenen Laufzeitzustand.

Dazu gehören insbesondere:

- GSM-Erfassung starten
- GSM-Erfassung stoppen
- GSM-Zustand zurücksetzen
- einzelne oder laufende Scans ausführen
- Modem-/SIM-/Registrierungszustände in die zentrale Registrierungssicht einbringen

### Wichtige Methoden

- `Execute()`
- `RunOnce()`
- `Start()`
- `Stop()`
- `Reset()`

Damit ist `TKGSM_Registra` der Bearbeiter für GSM-Verfügbarkeit, GSM-Laufzeitstatus und deren Rückmeldung an die zentrale Registrierung.

---

## Zusammenspiel

Das Zusammenspiel ist einfach:

1. Der Server arbeitet mit dem `TKBase_RegistrationManager`.
2. Der Manager hält die drei Bereiche `SipExt`, `SipInt` und `Gsm`.
3. Der Manager startet, stoppt, überwacht und bündelt diese Bereiche.
4. Jeder Bereich bearbeitet seine eigene fachliche Registrierungsarbeit.
5. Der Manager sammelt Status, Aktionen, Fehler und Laufzeitinformationen an einer zentralen Stelle.

---

## Kurzfassung

### TKBase_RegistrationManager.cs

Zentrale Registrierungszentrale des Servers.

### TKSipExt_Registra.cs

Bearbeiter für externe SIP-Providerregistrierung.

### TKSipInt_Registra.cs

Bearbeiter für interne SIP-Registrierung von Endgeräten und Benutzern.

### TKGSM_Registra.cs

Bearbeiter für GSM-Laufzeit- und Registrierungszustand.

---

## Fazit

`TKBase_RegistrationManager.cs` ist die gemeinsame Führungs- und Verwaltungsinstanz für den gesamten Registrierungsbereich des Servers.

Die Bereichsdateien sind die fachlichen Ausführer innerhalb ihrer jeweiligen Domäne.

So entsteht eine klare Gesamtstruktur:

- oben der zentrale RegistrationManager
- darunter die drei fachlichen Registrierungsbereiche
- alles zusammen unter einer einheitlichen Server-Registrierungslogik
