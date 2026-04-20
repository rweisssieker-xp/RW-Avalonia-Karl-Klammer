# Distribution (Portable / Zip)

Für eine **tragbare Installation** ohne MSI/MSIX genügt ein Release-Publish in einen Ordner:

```powershell
.\scripts\Package-Portable.ps1
```

Ergebnis: `artifacts/CarolusNexus-portable.zip` (oder der im Skript konfigurierte Pfad).

**Hinweis:** MSI/MSIX-Installer sind optional und können später ergänzt werden (Signatur, Update-Kanal). Die Kern-App-Funktionen sind im Publish-Paket vollständig enthalten.
