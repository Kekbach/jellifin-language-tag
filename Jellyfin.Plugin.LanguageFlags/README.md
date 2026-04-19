# Jellyfin Language Flags Overlay Plugin

Dieses Plugin ist ein **Startpunkt** für dein gewünschtes Feature:

- nimmt das vorhandene Poster eines Films oder einer Serie
- liest Audio-/Untertitelsprachen aus den MediaStreams
- rendert oben rechts eine kleine Flaggenleiste
- speichert ein generiertes Poster als `*.langflags.png`
- stellt eine geplante Aufgabe zur Stapelverarbeitung bereit

## Wichtige Einschränkung

Dieser Stand ist ein **funktionsnahes Gerüst**, aber **nicht gegen eine konkrete Jellyfin-10.11.x-Instanz kompiliert und getestet**. Je nach genauer Server-Version können Namespaces, Package-Referenzen oder Bildspeicher-APIs leicht abweichen.

## Warum dieser Ansatz?

Die referenzierte `language-overlay`-Vorlage ist nur ein externes Shell-Skript mit `ffprobe` und `imagemagick`. Ein robustes Jellyfin-Plugin sollte stattdessen direkt auf Jellyfins MediaStreams und geplante Tasks zugreifen.

## Nächste sinnvolle Ausbaustufen

1. Generiertes Poster automatisch als Primary Image am Item registrieren.
2. Serien-Sprachen aus Episoden aggregieren, nicht nur aus dem Series-Objekt.
3. Mehr echte Flaggen statt Platzhalter für komplexe Länder.
4. Optional nur Originalsprache statt aller Audiospuren anzeigen.
5. Manuelle Ausführung pro Item im Admin-Menü.

## Build

```bash
dotnet restore
dotnet build -c Release
```

## Verzeichnis

- `Plugin.cs` – Hauptplugin
- `Configuration/` – Konfiguration + Admin-Seite
- `Services/FlagOverlayRenderer.cs` – Bildbearbeitung und Sprachlogik
- `Tasks/GenerateLanguageFlagsTask.cs` – geplanter Task
