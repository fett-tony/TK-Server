# TK-Server Arbeitsregeln TEST

Diese Regeln sind für alle Änderungen am TK-Server-Projekt verbindlich.

## 0. Grundschutz fuer Struktur, Hierarchie und Betreiberkontrolle

Der TK-Server ist eine 24/7-Serveranwendung und muss unbeaufsichtigt ohne UI-Eingriffe laufen koennen.

Der Betreiber darf nicht durch stille Architekturentscheidungen, feste Vorgaben, versteckte Parallelstrukturen oder unbesprochene Umbauten ueberrumpelt werden.

Die bestehende Struktur, Bereichstrennung, Hierarchie und Manager-Logik sind zu schuetzen.

Keine Strukturänderungen, keine neuen Zuständigkeiten, keine neuen `.cs`-Dateien, keine Wrapper, keine Delegationshuellen und keine fest im Code verankerten Optionen ohne vorherige Absprache.

Wenn eine Delegation technisch nicht vermeidbar ist, muss sie vorher ausdruecklich angekuendigt und begruendet werden.

Optionen, Nummern, Ports, IPs, Codecs, Routing, Services und sonstige veraenderbare Betriebswerte muessen ueber Datenbank, Settings oder vorhandene Strukturen steuerbar bleiben, wenn sie dort sinnvoll abbildbar sind.

Keine MessageBoxen oder UI-Blockaden im Serverbetrieb.

Aenderungen muessen fokussiert bleiben und duerfen keine Nebenbaustellen oder versteckte Parallelstrukturen erzeugen.

## 1. Regeldatei lesen und Projektzusammenhang nachvollziehen

Vor Planung, Diskussion oder Aenderung muss zuerst der aktuelle Projektzustand nachvollzogen werden.

Verbindlich zu lesen ist die produktive Regeldatei:

`\\homeland.net\dfs\PROFILES\aktony\Documents\source\repos\TK-Server\Wichtig\DOKU\TK-Server_Arbeitsregeln.md`

oder

`H:\Documents\source\repos\TK-Server\Wichtig\DOKU\TK-Server_Arbeitsregeln.md`

Dazu gehoeren insbesondere:

- bestehende Struktur und Zustaendigkeiten pruefen
- relevante Dateien lesen, bevor Aenderungen vorgeschlagen oder umgesetzt werden
- vorhandene DOKU-Dateien und Bereichskommentare beruecksichtigen
- bereits erarbeitete Architekturentscheidungen nicht ignorieren
- keine Planung oder Umsetzung auf Annahmen aufbauen, wenn der Projektkontext lokal nachlesbar ist

Wenn vorher viel Planung, Diskussion oder Architekturarbeit geleistet wurde, darf dieser Kontext nicht verloren gehen.

Bei Unsicherheit wird zuerst analysiert und nachgelesen, bevor etwas geaendert oder vorgeschlagen wird.

## 2. Vor jeder Aenderung Projekt archivieren

diese regel muss vom anwender durchgefürt werden oder ist nur für codex relevant!

Bevor Aenderungen an Projektdateien, SQL-Dateien, Ressourcen, Skripten oder sonstigen Dateien im TK-Server-Projekt vorgenommen werden, muss zuerst das Archiv-Skript ausgefuehrt werden.

Zu verwenden ist ausschliesslich:

`.archive\TK-Server.Archivieren.ps1`

Das Skript legt die ZIP-Archive im Projektordner unter `.archive\TK-Server` ab.

Erst nach erfolgreicher Archivierung duerfen Aenderungen am Projekt vorgenommen werden.

## 3. Datenbank-, SQL- und Settings-Grundsatz

Der TK-Server bezieht seine veraenderbaren Einstellungen aus der Datenbank.

SQL-Befehle, SQL-Abfragen und SQL-Commandtexte gehoeren ausschliesslich in `TKSQL_Commands.cs`.

Keine SQL-Statements in Forms, Controller, Manager, Leg-Dateien oder sonstigen Klassen.

Der Datenbankzugriff und der Verbindungsaufbau laufen ausschliesslich ueber `TKSQL_ConnectionClass.cs`.

Es werden keine separaten Datenbankverbindungen in anderen `.cs`-Dateien erstellt.

Keine eigene `SqlConnection`, kein eigener `ConnectionString` und kein direkter DB-Verbindungsaufbau ausserhalb der zentralen Verbindungsklasse.

Settings werden aus der Datenbank geladen, in den vorgesehenen Structs bzw. Konfigurationsklassen gehalten und im gesamten Programm aus diesen Strukturen verwendet.

Keine IP-Adressen, Ports, COM-Ports, RTP-Bereiche, SIP-Ports, Codecs, Nummern, Servicewerte, Routingregeln oder sonstige Betriebsoptionen duerfen fest im Code verankert werden, wenn sie ueber Datenbank, Settings oder vorhandene Strukturen abbildbar sind.

Dieser Aufbau schuetzt die Dynamik des Servers und haelt den Datenbankbereich austauschbar. Wenn spaeter z.B. MariaDB, PostgreSQL oder ein anderer Datenbanktyp verwendet werden soll, darf diese Aenderung nicht das gesamte Programm betreffen, sondern muss sich auf den zentralen Datenbank-/Commandbereich konzentrieren koennen.

## 4. Kein neuer Logger

Es wird kein neuer Logger angelegt.

Fuer Logging wird ausschliesslich `TKBase_LogManager.cs` verwendet.

## 5. Bereiche, LEG-A, LEG-B und LEG-D strikt getrennt halten

Der TK-Server ist in die Bereiche `GSM`, `SIPInt`, `SIPExt` und `SVC` getrennt.

`GSM`, `SIPInt` und `SIPExt` koennen je nach Callrichtung ein `Leg-A` oder ein `Leg-B` fuehren.

`SVC` fuehrt ausschliesslich `Leg-D`.

`SVC` ruft keine Teilnehmer an und baut keine externen oder internen Zielverbindungen auf. `SVC` stellt nur Servicefunktionen bereit, die einem bestehenden Call oder einem Serviceaufruf zugeordnet sind.

Bereiche duerfen sich nicht gegenseitig mit fremder Logik, fremden Zustandsautomaten oder fremden Leg-Zustaendigkeiten kontaminieren.

Ein Leg darf nicht durch ein anderes Leg gesteuert oder mit dessen Logik vermischt werden.

Wenn ein Call bereichsuebergreifend laeuft, bleiben die Legs trotzdem in ihrem jeweiligen Bereich getrennt.

Beispiel:

`GSM.LegA=(016090446464 via--> 015161460912) --> SIPInt.LegB=(1005)`

Dabei bleibt der eingehende GSM-Teil `GSM.LegA` und der interne Zielteil `SIPInt.LegB`.

Nur bei reinen internen Calls koennen beide Legs im selben Bereich liegen, z.B.:

`SIPInt.LegA=(1004) --> SIPInt.LegB=(1005)`

Auch dann bleiben `Leg-A` und `Leg-B` logisch und technisch getrennt.

Service- bzw. Dienst-Commands bleiben in `SVC.LegD` und duerfen nicht mit `GSM`, `SIPInt`, `SIPExt`, `Leg-A` oder `Leg-B` vermischt werden.

## 6. Wann bekommt wer welchen leg.

wenn SipInt ruft --> int ist der Anrufer leg-A in SipInt und der angerufene ist Leg-B in SipInt.

Wenn SipInt ruft --> via 033714023129 --> nach außen ist der Anrufer Leg-A in SipInt und der angerufene Leg-B in SipExt.

Wenn SipInt ruft --> via GSM --> nach außen ist der Anrufer Leg-A in SipInt und der angerufene Leg-B in GSM.

Wenn call von außen kommt 016090446464 --> via 033714023129 --> 1001,1002,1004,1005 hier bekommt der Anrufer Leg-A in SipExt und die angerufenen bekommen Leg-B in SipInt

Wenn call von außen kommt 016090446464 --> via 015161460912 --> 1001,1002,1004,1005 hier bekommt der Anrufer Leg-A in GSM und die angerufenen bekommen Leg-B in SipInt

Eingehende und Ausgehende, die ein Anrufer haben sind immer leg-A, entgegen nehmende Empfänger oder angerufene sind immer Leg-B.

#Alle möglichen Verbindungen
Inbound         GSM.LegA=(016090446464 via--> 015161460912) --> SipInt.LegB=(1005)
Inbound      SipExt.LegA=(016090446464 via--> 033714023129) --> SipInt.LegB=(1004)
Outbound     SipInt.LegA=(1005         via--> 015161460912) -->    GSM.LegB=(016090446464)
Outbound     SipInt.LegA=(1004         via--> 033714023129) --> SipExt.LegB=(016090446464)
Localbound   SipInt.LegA=(1004)           -->                   SipInt.LegB=(1005)
Servicebound SipInt.LegA=(1004)           -->                      SVC.LegD=(*100)
#FAX 
Inbound         Fax.LegA=(033719988776 via--> 033716899955) -->    Fax.LegB=(1003)
Outbound        Fax.LegA=(1003         via--> 033716899955) -->    Fax.LegB=(033719988776)
Inbound         Fax.LegA=(1006)           -->                      Fax.LegB=(1003)

Das Fax unterliegt einer sonder regel darurch das es von vorherein eine festgelegt nummer und einen festgelegten User gibt.
Hinzu kommt das ein Fax immer mit einem Fax verbunden werden muss oder mit einem software dienst der ein fax simuliert.

Call kommt rein
→ LegA bestimmen
→ LegA-CallID bekommen/erstellen          --> A.CALLID
→ Routing/Forking auswerten
→ LegB/LegD-CallID erstellen              --> ZielLeg(B/D).CALLID
→ SessionID aus LegA + LegB/LegD bilden   --> SessionID (A.CALLID,ZielLeg.CALLID)
→ Session speichern
→ INVITE / ATA / ServiceStart ausführen

## 7. ID-Bildung

keine Präfixe an den IDs nicht bei callID und auch nicht bei sessionID.

Die SessionID wird immer aus der CallID LegA und der CallID LegB bzw. LegD gebildet!

zu den CallIDs selbst diese werden automatisch erstellt ohne bezug auf irgendeinen bestimmten wert.

## 8. Service-, Menue- und interne Nummernregeln

Interne Benutzer-/Nebenstellennummern sind 4-stellig und bestehen nur aus Ziffern, z.B. `1004`.

Servicefunktionen laufen ueber eine zentrale Service-Menue-Rootnummer. Diese Rootnummer wird in `TK_SIP_ServiceLeg` festgelegt und ist aktuell `*100`.

Alle Service-Menueoptionen werden unterhalb dieser Rootnummer gebildet. Ohne die festgelegte Service-Menue-Rootnummer wird keine Servicefunktion direkt ausgefuehrt.

Grundformen:

`*100` = Service-Hauptmenue starten
`*100*8#` = Menueoption 8 direkt ausfuehren
`*100*2*1005#` = Menueoption 2 mit Parameter `1005` ausfuehren

Innerhalb eines laufenden Service-Menues wird nur die Option gewaehlt:

`*8#` = Menueoption 8 im aktuellen Menue ausfuehren
`*0#` = abbrechen oder zurueck ins Hauptmenue
`*1#` = bestaetigen, wenn die aktuelle Menue-Logik eine Bestaetigung erwartet

Menueoptionen und Parameterregeln duerfen nicht fest im Code verankert werden, wenn sie ueber `TK_SIP_ServiceLeg`, Routing, Settings oder andere DB-Regeln abbildbar sind.

Ob ein Parameter zulaessig ist, entscheidet die zustaendige Service-/Bereichslogik. Das betrifft insbesondere interne Ziele, externe Rufnummern, eigene Server-/Provider-/GSM-/Faxnummern und kosten- oder schleifenrelevante Weiterleitungen.

## 9. Zusammenarbeit zwischen GUI und Service

Der TK-Service ist die produktive Laufzeitinstanz des TK-Servers.

Die GUI dient ausschließlich der Verwaltung, Steuerung, Konfiguration und Anzeige des laufenden Systems.

### Aufgabenverteilung

Der Service führt aus:

- SIP-Server
- SIP-Registrierungen
- Provider-Registrierungen
- GSM-Monitoring
- Routing
- Sessionverwaltung
- Callsteuerung
- Servicefunktionen
- Mediahandling
- Runtime-Datenpflege

Die GUI führt nicht aus, sondern:

- startet und stoppt den Service
- zeigt Registrierungen an
- zeigt Live-Calls an
- zeigt Logs an
- verwaltet Einstellungen, Benutzer, Provider, GSM und Services

## 10. Der server ist keine endpoint sondern nur vermittlungsstelle

Der server ist immer nur Vermittler er dolmetscht und erstellt alle nötigen antworten um ein
zustande kommen eines gespräches zu ermöglichen.

Es gibt nur einen bereich in dem der server selber ein endpoint ist der SVC bereich aber selbt dort tritt 
der Server als LegD auf. er ist vermittler + endpoint beim Service bereich SVC.

### Gemeinsamer Zustand

Service und GUI arbeiten immer auf derselben Datenbasis.

Runtime-Daten werden ausschließlich vom Service erzeugt und gepflegt.

Die GUI liest und visualisiert diese Daten.

Es dürfen keine GUI-internen Schattenlisten, Parallelzustände oder eigenen Registrierungs-, Call- oder Sessionobjekte entstehen.

### Live-Daten

Registriert sich ein Client, aktualisiert der Service die Runtime-Daten.

Entsteht, ändert oder endet ein Gespräch, aktualisiert der Service die Runtime-Daten.

Diese Änderungen müssen unmittelbar in der GUI sichtbar werden.

### Keine doppelte Serverlogik

SIP, GSM, Routing, Registrierungen, Sessions, Calls und Media laufen ausschließlich im Service.

Die GUI darf diese Funktionen weder selbst ausführen noch eigene Zustände dafür verwalten.

### Startverhalten

Produktive Komponenten werden nicht selbstständig gestartet.

Die GUI fordert Aktionen an.

Der Service führt sie aus.

Zwischen GUI und Service muss jederzeit derselbe Systemzustand sichtbar sein.

## Kernaussage

Es wird nicht kreativ umgebaut.

Änderungen erfolgen gezielt innerhalb der bestehenden TK-Server-Struktur.

Jede Änderung muss zur vorhandenen Architektur passen und darf keine neue Parallelstruktur erzeugen.
