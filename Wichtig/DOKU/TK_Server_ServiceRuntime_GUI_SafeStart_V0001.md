# TK-Server ServiceRuntime GUI SafeStart V0001

Ziel: GUI muss wieder normal starten.

Umsetzung:
- TKSTART.cs wurde aus dem funktionierenden Basisstand beibehalten.
- Keine Windows-Service-Automatik im GUI-Startpfad.
- Keine `System.ServiceProcess`-/`ServiceController`-Abhängigkeit in der GUI.
- Die neuen Buttons `Service Start` und `Service Stop` bleiben vorhanden.
- Die Buttons steuern einen installierten Windows-Service über `sc.exe`.
- Beim Schließen der GUI wird kein Service gestoppt.

Hinweis:
- Diese Version behebt vorrangig den GUI-Start.
- Der eigentliche dauerhafte Windows-Service-Prozess muss danach sauber separat aufgebaut werden, ohne den GUI-Startpfad zu belasten.
