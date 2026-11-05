# TK-Server Baustellen und Prioritaeten

Stand: 2026-05-11

Diese Notiz fasst die aktuell offenen grossen Themen zusammen und ordnet sie nach sinnvoller Bearbeitungsreihenfolge. Ziel ist, die vorhandene TK-Server-Struktur gezielt weiterzuentwickeln und keine neue Parallelstruktur aufzubauen.

## 1. Media-Struktur

Die Media-Struktur ist die Grundlage fuer mehrere Folgefunktionen:

- Gespraechsaufzeichnung
- Anrufbeantworter
- Zeitansage und weitere Services
- Fax, mindestens G.711-Passthrough und spaeter eventuell T.38/UDPTL

Die bestehende Struktur mit `TKBase_MediaManager` und den getrennten Media-Dateien fuer SipInt, SipExt, GSM und SVC bleibt erhalten. Es soll kein kreativer Umbau entstehen. Die naechsten Erweiterungen sollen an der vorhandenen RTP-/Media-Verarbeitung ansetzen.

Wichtig ist zuerst eine klare lokale Media-Struktur ohne ADS-Abhaengigkeit. ADS kommt spaeter und darf die Media-Arbeit nicht blockieren.

## 2. Media-Aufzeichnung

Die Gespraechsaufzeichnung bekommt jetzt den Hauptfokus.

Fachliche Vorgabe:

- `*21#` startet die Aufnahme fuer das aktuelle laufende Gespraech.
- erneutes `*21#` stoppt die aktuelle Aufnahme und finalisiert die Datei.
- ein weiteres `*21#` startet eine neue Datei.
- jede gestartete Aufnahme erfasst beide Gespraechsrichtungen.
- beim Call-Ende wird eine aktive Aufnahme automatisch sauber beendet.
- geschlossene WAV-Dateien werden nicht einfach appended.

Technische Einordnung:

- Aufnahme gehoert in den bestehenden Media-/Session-Pfad.
- RTP-Pakete duerfen nicht nur roh in eine WAV-Datei geschrieben werden.
- Fuer eine echte WAV-Datei muessen Payload/Codec, Sampleformat und Header sauber behandelt werden.
- Dateiname und Pfad kommen aus DB/Service-Setting, z.B. `RecordTemplate`.
- Logging nur ueber `TKBase_LogManager`.

## 3. Anrufbeantworter

Ansaetze sind vorhanden, aber es fehlt noch Struktur.

Der Anrufbeantworter sollte nach der Media-Aufzeichnung angegangen werden, weil er dieselben Grundlagen braucht:

- Ansage abspielen
- danach Aufnahme starten
- DTMF/Timeout auswerten
- Aufnahme speichern
- Service-Logik sauber von SIP-, GSM- und allgemeinen Call-Commands trennen

Die Umsetzung soll auf der bestehenden Service- und Media-Struktur aufbauen.

## 4. Fax

Fax ist sehr wichtig, weil ein echtes Faxgeraet vorhanden ist. Gleichzeitig ist die Integration nicht klein.

Aktuelle Einordnung:

- G.711-Fax-Passthrough ist der erste pragmatische Schritt.
- T.38 ist deutlich groesser, weil es SDP `m=image`, UDPTL und Re-INVITE-Handling braucht.
- SIPSorcery kann SIP/SDP/RTP gut und kennt T.38 im SDP-Kontext, liefert aber keinen fertigen T.38/UDPTL-Faxstack fuer den TK-Server.
- SpanDSP ist als freie Library interessant, aber nur als native C-Library mit Windows-Build und C#-Interop realistisch.
- Kommerzielle SDKs wie VOCAL, Commetrex oder GAO waeren Alternativen, aber keine einfache NuGet-Loesung.

Fax wird deshalb nicht als naechster Hauptblock begonnen. Zuerst sollen Media-Struktur und Aufzeichnung stabil werden.

## 5. ADS-Integration

ADS ist praktisch noch nicht vorhanden und wird voraussichtlich grosse Auswirkungen haben.

Einschaetzung:

- ADS betrifft Benutzer, Felder, Identitaeten, Sync, Datenmodell und eventuell GUI/DB-Struktur.
- Es ist noch unklar, welche Felder reichen, welche gestrichen werden und welche spaeter gebraucht werden.
- ADS sollte nicht mehrfach angefangen, zurueckgedreht und wieder veraendert werden.
- Deshalb wird ADS bewusst spaet eingeordnet.

Vorgehen:

- Erst lokale Struktur stabilisieren.
- Erst Media, Aufnahme, Services und Fax-Grundsatz klaeren.
- ADS danach einmal sauber und bewusst integrieren.

## Prioritaet

Empfohlene Reihenfolge:

1. Media-Struktur lokal sauber halten und gezielt erweitern.
2. Media-Aufzeichnung mit `*21#` Start/Stop umsetzen.
3. Anrufbeantworter auf Basis der Aufnahme-/Playback-Struktur bauen.
4. Fax zunaechst ueber G.711-Passthrough betrachten, T.38 spaeter separat planen.
5. ADS am Ende angehen, wenn Datenmodell und lokale Funktionen stabiler sind.

Kernaussage:

Jetzt nicht ADS und nicht T.38-Fax als Hauptbaustelle. Der naechste Schwerpunkt ist Media-Aufzeichnung innerhalb der bestehenden TK-Server-Struktur.

## T.38 / FoIP Suchbegriffe und Links

- SpanDSP: https://www.soft-switch.org/spandsp-doc/
- SpanDSP T.38 Core: https://www.soft-switch.org/spandsp-doc/t38__core_8h.html
- SpanDSP T.38 Gateway: https://www.soft-switch.org/spandsp-doc/t38__gateway_8h.html
- SpanDSP T.38 Terminal: https://www.soft-switch.org/spandsp-doc/t38__terminal_8h.html
- FreeSWITCH mod_spandsp: https://developer.signalwire.com/freeswitch/FreeSWITCH-Explained/Modules/mod_spandsp_6587021/
- VOCAL FXSRV Fax over IP Server Library: https://vocal.com/fax-over-ip-server-library-fxsrv/
- VOCAL T.38 Fax over IP: https://vocal.com/voip/t-38-fax/
- VOCAL Fax Modem Software Library: https://vocal.com/software-modules/fax-modem-software/
- Commetrex TerminatingT38: https://commetrex.com/products/terminatingt38/
- Commetrex Fax Portal / Produktuebersicht: https://commetrex.com/products/fax-portal/
- GAO Research T.38 Fax Relay Software: https://www.gaoresearch.com/products/faxsoftware/t38pkg/
- GAO Research Fax Software Products: https://www.gaoresearch.com/products/faxsoftware/

Suchwoerter:

- `spandsp t38 gateway`
- `spandsp t38 terminal`
- `spandsp windows build`
- `spandsp C# PInvoke`
- `T.38 UDPTL library`
- `T.38 fax server library`
- `FoIP server library`
- `VOCAL FXSRV`
- `Commetrex PortableT30 PowerRelay`
- `Commetrex PowerFax SDK`
- `GAO T.38 Fax Relay Software`
