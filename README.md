# Fat32Formator

Formatowanie pendrive'ów na **FAT32 (MBR)** pod **CDJ 2000** — bez zewnętrznych programów.

Dyski > 32GB są obsługiwane dzięki natywnej implementacji FAT32 w C#.

## Wymagania

- Windows 10/11
- [.NET 9.0](https://dotnet.microsoft.com/download/dotnet/9.0)
- Uprawnienia administratora

## Uruchomienie

```bash
dotnet run
```

Publikacja do `.exe`:
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
