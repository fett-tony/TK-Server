# TKBase_RoutingManager.cs – Soll-Aufgabe und Rolle der Bereichs-Routing-Dateien

## Ziel dieses Dokuments
Dieses Dokument beschreibt **nicht den Ist-Zustand**, sondern die **Soll-Aufgabe** von `TKBase_RoutingManager.cs` sowie die **Rolle der Routing-Dateien in den einzelnen Bereichen**.

---

## 1. Was `TKBase_RoutingManager.cs` tun soll

`TKBase_RoutingManager.cs` soll die **zentrale fachliche Routingentscheidung** für einen Ruf treffen.

Das bedeutet konkret:

- Er soll eingehende Wählziele **fachlich einordnen**.
- Er soll entscheiden, **welcher Bereich zuständig ist**.
- Er soll die Routing-Anfrage an den **passenden Bereichsrouter delegieren**.
- Er soll **bereichsübergreifende Regeln** anwenden.
- Er soll ein **neutrales Routing-Ergebnis** zurückgeben.
- Er soll verhindern, dass Routingwissen wieder in `Server`, `SessionManager`, `Commands` oder `CallController` verteilt wird.

---

## 2. Die eigentliche Kernaufgabe

Die Kernaufgabe von `TKBase_RoutingManager.cs` ist:

> Er soll ausschließlich entscheiden, **wohin ein Ruf fachlich gehört** und **welcher Bereich das Ziel auflösen muss**.

Er ist damit **kein technischer Ausführer**, sondern der **fachliche Dirigent** für das Routing.

---

## 3. Welche Entscheidungen er treffen soll

`TKBase_RoutingManager.cs` soll prüfen, in welche Routing-Kategorie ein Ziel fällt.

Er soll insbesondere unterscheiden zwischen:

- **Internem Ziel**
- **Service-Ziel**
- **Externem SIP-Ziel**
- **GSM-Ziel**
- **Notruf / Sonderregel**
- **Fallback / kein Treffer**

Er soll also nicht selber das Ziel technisch herstellen, sondern nur sauber feststellen:

- Was ist das für ein Ziel?
- Welcher Bereich ist zuständig?
- Gibt es eine Priorität oder einen Fallback?
- Welches Routing-Ergebnis wird an die nächste Schicht zurückgegeben?

---

## 4. Was er ausdrücklich nicht tun soll

`TKBase_RoutingManager.cs` soll **nicht**:

- Sessions erzeugen oder verwalten
- Legs aufbauen oder koppeln
- SIP-Requests erzeugen oder senden
- GSM-Befehle ausführen
- RTP / Audio / Medien koppeln
- Provider registrieren oder SIP-Dialoge führen
- bereichsspezifische Detailprüfung selbst enthalten
- Gesprächssteuerung übernehmen

Kurz:

> Er soll **Routing entscheiden**, aber **nichts technisch ausführen**.

---

## 5. Welche Stellung er in der Architektur haben soll

`TKBase_RoutingManager.cs` soll die **zentrale Routing-Orchestrierung** bilden.

Die saubere Soll-Kette ist:

`DialPlan / Call-Entscheidung` → `TKBase_RoutingManager` → `Bereichsrouter` → `RoutingResult` → `Commands / CallController führen aus`

Damit bleibt die Trennung sauber:

- `RoutingManager` entscheidet fachlich
- `Routing-Dateien` lösen bereichsintern auf
- `Commands` führen technisch aus
- `CallController` steuert den Gesprächsablauf
- `SessionManager` verwaltet nur Sessions

---

## 6. Welche Art Ergebnis er liefern soll

`TKBase_RoutingManager.cs` soll am Ende ein **neutrales gemeinsames Routing-Ergebnis** zurückgeben.

Dieses Ergebnis soll mindestens enthalten:

- `Success`
- `Reason`
- `TargetKind`
- `CalledNumber`
- bereichsspezifische Zielparameter

Wichtig ist dabei:

Das Ergebnisobjekt soll **bereichsneutral** sein und nicht logisch einem einzelnen Bereich „gehören“.

---

## 7. Bereichsübergreifende Regeln, die in den RoutingManager gehören

Folgende Regeln gehören in `TKBase_RoutingManager.cs`, weil sie **bereichsübergreifend** sind:

- Auswahl des zuständigen Bereichs
- Prioritätsauswahl
- Fallback auf nächste Route
- Überspringen belegter oder nicht nutzbarer Kandidaten
- Berechtigungsprüfung auf oberer Ebene
- Sonderbehandlung für Notruf oder globale Ausnahmen
- zentrale Protokollierung der Routingentscheidung

Diese Logik soll **nicht** wieder verstreut in mehreren `Commands`- oder `Server`-Dateien landen.

---

## 8. Rolle der Routing-Dateien in den Bereichen

Die Routing-Dateien in den Bereichen sollen **nicht global koordinieren**, sondern nur die **fachliche Zielauflösung innerhalb ihres eigenen Bereichs** übernehmen.

Sie sind also **Spezialisten für ihren Bereich**, aber **nicht der Gesamtkoordinator**.

---

## 9. Rolle von `TKSipInt_Routing.cs`

`TKSipInt_Routing.cs` soll nur für **interne SIP-Ziele** zuständig sein.

Diese Datei soll:

- interne Rufnummern / Nebenstellen auflösen
- interne Benutzer finden
- interne Registrierung und Erreichbarkeit prüfen
- internes Ziel für SIP intern zurückgeben

Diese Datei soll **nicht**:

- externe Provider auswählen
- GSM-Routing machen
- Service-Ziele behandeln
- globale Prioritätslogik übernehmen
- Session- oder Leg-Steuerung ausführen

Kurz:

> `TKSipInt_Routing.cs` soll nur beantworten: **Wie wird ein internes SIP-Ziel innerhalb des internen Bereichs aufgelöst?**

---

## 10. Rolle von `TKSVC_Routing.cs`

`TKSVC_Routing.cs` soll nur für **Service-Ziele** zuständig sein.

Diese Datei soll:

- Service-Nummern erkennen
- Service-Codes auflösen
- passendes `ServiceLeg` oder Service-Ziel liefern
- interne Service-Funktionen wie Ansage, Voicemail, IVR oder Sonderdienste adressieren

Diese Datei soll **nicht**:

- interne Benutzerziele auflösen
- externe Provider auswählen
- GSM-Routen bilden
- globale Routingsteuerung übernehmen

Kurz:

> `TKSVC_Routing.cs` soll nur beantworten: **Welcher interne Service ist für diese Service-Rufnummer zuständig?**

---

## 11. Rolle von `TKSipExt_Routing.cs`

`TKSipExt_Routing.cs` soll nur für den **externen SIP-Bereich** zuständig sein.

Diese Datei soll:

- externe Rufnummern normalisieren
- eingehende DID-Ziele auflösen
- ausgehende Provider-Ziele vorbereiten
- Provider-Routen und Zielparameter bestimmen
- Provider-Erreichbarkeit und nutzbare Zustände bewerten

Diese Datei soll **nicht**:

- globale Routingentscheidung treffen
- interne SIP-Ziele lösen
- Service-Ziele steuern
- GSM-Ressourcen auswählen
- Sessions oder Gespräche führen

Kurz:

> `TKSipExt_Routing.cs` soll nur beantworten: **Wie wird ein externes SIP-Ziel oder eine DID innerhalb des externen SIP-Bereichs aufgelöst?**

---

## 12. Rolle von `TKGSM_Routing.cs`

`TKGSM_Routing.cs` soll nur für den **GSM-Bereich** zuständig sein.

Diese Datei soll:

- GSM-fähige Routen auflösen
- nutzbare Modems / SIMs / Statusobjekte bestimmen
- prüfen, ob eine GSM-Leitung online, selektierbar und registriert ist
- die Zielparameter für den GSM-Bereich liefern

Diese Datei soll **nicht**:

- SIP-Provider-Routing übernehmen
- interne SIP-Ziele steuern
- Service-Ziele behandeln
- globale Priorität oder Gesamtkoordination übernehmen
- technische Gesprächssteuerung enthalten

Kurz:

> `TKGSM_Routing.cs` soll nur beantworten: **Welche konkrete GSM-Ressource kann diesen Ruf innerhalb des GSM-Bereichs bedienen?**

---

## 13. Wichtige Architekturregel

Es muss eine klare Trennung gelten:

- `TKBase_RoutingManager` = **entscheidet den zuständigen Bereich**
- `..._Routing.cs` = **löst das Ziel innerhalb des Bereichs auf**
- `..._Commands.cs` = **führt die technische Aktion aus**
- `..._CallController.cs` = **steuert den Gesprächsablauf**
- `TKBase_SessionManager` = **verwaltet Session-Lebenszyklus**

Sobald Routingwissen wieder in `Server`, `Commands` oder `CallController` landet, wird die Trennung wieder unsauber.

---

## 14. Was der RoutingManager architektonisch sichern soll

`TKBase_RoutingManager.cs` soll dafür sorgen, dass:

- Routing **zentral** bleibt
- Routing **fachlich** bleibt
- Bereichslogik **nicht vermischt** wird
- technische Ausführung **nicht in den RoutingManager zurückläuft**
- die Bereiche **klar getrennt** bleiben
- spätere Erweiterungen wie Notruf, Fax, Sonderdienste oder neue Trunks kontrolliert ergänzt werden können

---

## 15. Empfohlene Kurzbeschreibung für die Datei

Die fachlich richtige Kurzbeschreibung lautet:

> `TKBase_RoutingManager.cs` soll ausschließlich die zentrale fachliche Routingentscheidung treffen, den zuständigen Bereich bestimmen, die Routing-Anfrage an den passenden Bereichsrouter delegieren und ein neutrales Routing-Ergebnis zurückgeben. Er soll keine Sessionsteuerung, keine Leg-Steuerung und keine technische Signalisierung ausführen.

---

## 16. Empfohlener Kopfkommentar für `TKBase_RoutingManager.cs`

```csharp
/*
 * TKBase_RoutingManager
 * Aufgabe:
 * - Trifft ausschließlich die zentrale fachliche Routingentscheidung.
 * - Ordnet ein Ziel den Bereichen SipInt, SipExt, GSM oder SVC zu.
 * - Delegiert die konkrete Zielauflösung an den zuständigen Bereichsrouter.
 * - Führt nur bereichsübergreifende Routingregeln wie Priorität, Fallback und globale Prüfungen aus.
 * - Gibt ein neutrales Routing-Ergebnis zurück.
 *
 * Nicht Aufgabe:
 * - Keine Session-Erzeugung oder Session-Verwaltung.
 * - Keine Leg-Steuerung.
 * - Keine technische SIP-, GSM- oder Medienausführung.
 * - Keine bereichsspezifische Detail-Fachlogik direkt in dieser Datei.
 *
 * Diese Information darf nicht gelöscht werden!
 */
```

---

## 17. Fazit

`TKBase_RoutingManager.cs` soll **nicht selber routen wie ein Fachmodul**, sondern **fachlich steuern, welcher Routing-Bereich zuständig ist**.

Die Bereichsdateien sollen **nur ihren eigenen Bereich auflösen**.

Genau dadurch bleibt die Architektur sauber:

- zentrale Routingentscheidung oben
- konkrete Bereichsauflösung unten
- technische Ausführung erst danach

