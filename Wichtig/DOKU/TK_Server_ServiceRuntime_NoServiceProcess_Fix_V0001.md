# TK-Server ServiceRuntime NoServiceProcess Fix V0001

## Ziel

Der Buildfehler CS1069 wurde entfernt.

`System.ServiceProcess.ServiceBase` und `System.ServiceProcess.ServiceController` werden nicht mehr verwendet.

## Umsetzung

Die GUI startet die TK-Runtime als separaten Prozess mit dem Parameter `--runtime`.

Dadurch bleibt die Runtime aktiv, wenn die GUI geschlossen wird.

Der Runtime-Prozess schreibt seine PID nach:

`C:\ProgramData\TK-Server\runtime.pid`

Der Stop erfolgt ueber:

`C:\ProgramData\TK-Server\runtime.stop`

## Hinweis

Das ist noch kein echter Windows-Service ueber den Service Control Manager.

Es ist der sichere Zwischenstand, damit die GUI wieder kompiliert und die Runtime trotzdem getrennt von der GUI laufen kann.
