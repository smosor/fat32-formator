# Fat32Formator dla CDJ 2000

Fat32Formator to prosta aplikacja okienkowa dla systemu Windows (napisana w C# i WinForms), która pozwala w szybki i bezproblemowy sposób przygotować pendrive lub dysk zewnętrzny do pracy z odtwarzaczami Pioneer CDJ 2000 (i podobnymi).

## Dlaczego powstał ten program?
Odtwarzacze CDJ mają specyficzne wymagania:
1. Wymagają systemu plików **FAT32**.
2. Wymagają schematu partycjonowania **MBR** (Master Boot Record).

Tymczasem system Windows natywnie **blokuje** możliwość formatowania dysków większych niż 32 GB do systemu FAT32, proponując jedynie exFAT lub NTFS (z którymi sprzęt muzyczny często sobie nie radzi). 

## Jak to działa?
Aplikacja jest nakładką graficzną wzorowaną na domyślnym narzędziu Windows. Zmienia jednak całą logikę pod spodem:
1. Wyszukuje tylko dyski wymienne podłączone do komputera (aby uniknąć pomyłkowego formatowania dysków systemowych).
2. Przy formatowaniu, generuje skrypt dla ukrytego w systemie narzędzia `diskpart`. Czyści on dysk i ustawia prawidłowy schemat partycji **MBR**.
3. Przy pierwszym uruchomieniu aplikacja pobiera z oficjalnego źródła mały program `fat32format.exe`, przy pomocy którego natychmiast formatuje dysk do FAT32, sprawnie **obchodząc wbudowany limit 32 GB systemu Windows**.

## Wymagania
* System Windows (10, 11)
* [Zainstalowane środowisko uruchomieniowe .NET 9.0](https://dotnet.microsoft.com/download/dotnet/9.0) (jeśli kompilujesz ze źródeł)
* **Uprawnienia Administratora** - narzędzie ingeruje w niskopoziomowe zarządzanie dyskami poprzez `diskpart`, co wymaga podwyższonych uprawnień.

## Kompilacja i uruchomienie

Aby skompilować aplikację samodzielnie z kodu źródłowego, użyj wiersza poleceń w folderze z projektem:

Uruchomienie bezpośrednie:
```bash
dotnet run
```

Publikacja do pojedynczego, niezależnego pliku `.exe` (który nie będzie wymagał instalacji .NET u użytkownika końcowego):
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
Wygenerowany program znajdziesz wtedy w ścieżce `bin/Release/net9.0-windows/win-x64/publish/Fat32Formator.exe`. Możesz go umieścić na Pulpicie.

## Zrzut ekranu
*(Interfejs programu do złudzenia przypomina domyślny konfigurator formatowania z systemu Windows, lecz opcje są zablokowane na najlepsze dla CDJ 2000, co zapobiega pomyłkom).*

## Technologie
* C# .NET 9.0 (Windows Forms)
* System.Management (WMI) do precyzyjnej identyfikacji dysków i przypisywania liter.
* Diskpart (CLI)
* [fat32format](http://ridgecrop.co.uk/index.htm?guiformat.htm) (Narzędzie zewnętrzne pobierane w tle).
