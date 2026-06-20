using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Fat32Formator
{
    /// <summary>
    /// Natywna implementacja formatowania FAT32 w C#.
    /// Pozwala formatować dyski powyżej 32GB bez użycia zewnętrznych programów.
    /// Bezpośrednio zapisuje struktury systemu plików FAT32 na dysk za pomocą Windows API.
    /// </summary>
    public static class Fat32Formatter
    {
        // ==========================================
        // P/Invoke — deklaracje funkcji Windows API
        // ==========================================

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(
            SafeFileHandle hFile,
            byte[] lpBuffer,
            uint nNumberOfBytesToWrite,
            out uint lpNumberOfBytesWritten,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetFilePointerEx(
            SafeFileHandle hFile,
            long liDistanceToMove,
            out long lpNewFilePointer,
            uint dwMoveMethod);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetDiskFreeSpaceEx(
            string lpDirectoryName,
            out long lpFreeBytesAvailable,
            out long lpTotalNumberOfBytes,
            out long lpTotalNumberOfFreeBytes);

        // ==========================================
        // Stałe Windows API
        // ==========================================
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const uint FSCTL_LOCK_VOLUME = 0x00090018;
        private const uint FSCTL_UNLOCK_VOLUME = 0x0009001C;
        private const uint FSCTL_DISMOUNT_VOLUME = 0x00090020;
        private const uint FILE_BEGIN = 0;

        // ==========================================
        // Stałe FAT32
        // ==========================================
        private const int BYTES_PER_SECTOR = 512;
        private const int RESERVED_SECTORS = 32;
        private const int NUM_FATS = 2;
        private const int ROOT_DIR_CLUSTER = 2;
        private const int FSINFO_SECTOR = 1;
        private const int BACKUP_BOOT_SECTOR = 6;

        /// <summary>
        /// Formatuje wolumin na FAT32 — metoda główna.
        /// Obsługuje dyski dowolnego rozmiaru, w tym powyżej 32GB.
        /// </summary>
        /// <param name="driveLetter">Litera dysku, np. "H:"</param>
        /// <param name="volumeLabel">Etykieta woluminu (max 11 znaków, wielkie litery)</param>
        /// <param name="progress">Opcjonalny callback do raportowania postępu (0-100)</param>
        public static void Format(string driveLetter, string volumeLabel, Action<int>? progress = null)
        {
            // Normalizacja etykiety — FAT32 wymaga wielkich liter i max 11 znaków
            volumeLabel = NormalizeVolumeLabel(volumeLabel);

            // Pobranie rozmiaru dysku
            long totalBytes = GetVolumeSizeBytes(driveLetter);
            if (totalBytes <= 0)
                throw new Exception($"Nie można odczytać rozmiaru woluminu {driveLetter}.");

            long totalSectors = totalBytes / BYTES_PER_SECTOR;
            if (totalSectors < 65536)
                throw new Exception("Wolumin jest za mały dla systemu plików FAT32 (minimum ~32MB).");

            // Obliczenie parametrów FAT32
            int sectorsPerCluster = GetSectorsPerCluster(totalBytes);
            uint fatSize = CalculateFatSize((uint)totalSectors, sectorsPerCluster);

            progress?.Invoke(5);

            // Otwarcie woluminu do bezpośredniego zapisu
            string volumePath = @"\\.\" + driveLetter;
            using SafeFileHandle hVolume = CreateFile(
                volumePath,
                GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (hVolume.IsInvalid)
                throw new Exception($"Nie można otworzyć woluminu {driveLetter}. Kod błędu: {Marshal.GetLastWin32Error()}");

            try
            {
                // Zablokowanie woluminu — żaden inny proces nie będzie miał dostępu
                if (!DeviceIoControl(hVolume, FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
                    throw new Exception($"Nie można zablokować woluminu. Kod błędu: {Marshal.GetLastWin32Error()}");

                // Odmontowanie systemu plików
                if (!DeviceIoControl(hVolume, FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
                    throw new Exception($"Nie można odmontować woluminu. Kod błędu: {Marshal.GetLastWin32Error()}");

                progress?.Invoke(10);

                // 1. Zapis sektora rozruchowego (Boot Sector) na sektor 0
                byte[] bootSector = BuildBootSector((uint)totalSectors, sectorsPerCluster, fatSize, volumeLabel);
                WriteSector(hVolume, 0, bootSector);

                progress?.Invoke(15);

                // 2. Zapis sektora FSInfo na sektor 1
                uint totalDataSectors = (uint)totalSectors - RESERVED_SECTORS - (NUM_FATS * fatSize);
                uint totalClusters = totalDataSectors / (uint)sectorsPerCluster;
                byte[] fsInfo = BuildFSInfoSector(totalClusters - 1); // -1 bo klaster 2 jest zajęty przez katalog główny
                WriteSector(hVolume, FSINFO_SECTOR, fsInfo);

                progress?.Invoke(20);

                // 3. Zapis znacznika końca na sektorze 2 (wymagany przez specyfikację)
                byte[] endMarker = new byte[BYTES_PER_SECTOR];
                endMarker[BYTES_PER_SECTOR - 2] = 0x55;
                endMarker[BYTES_PER_SECTOR - 1] = 0xAA;
                WriteSector(hVolume, 2, endMarker);

                // 4. Kopia zapasowa boot sectora na sektorze 6, FSInfo na 7, znacznik na 8
                WriteSector(hVolume, BACKUP_BOOT_SECTOR, bootSector);
                WriteSector(hVolume, BACKUP_BOOT_SECTOR + 1, fsInfo);
                WriteSector(hVolume, BACKUP_BOOT_SECTOR + 2, endMarker);

                progress?.Invoke(30);

                // 5. Wyzerowanie obu tablic FAT (może trwać chwilę dla dużych dysków)
                uint fat1Start = RESERVED_SECTORS;
                uint fat2Start = RESERVED_SECTORS + fatSize;

                ClearFat(hVolume, fat1Start, fatSize, progress, 30, 55);
                ClearFat(hVolume, fat2Start, fatSize, progress, 55, 80);

                // 6. Zapis pierwszych wpisów FAT (identyfikator nośnika + katalog główny)
                byte[] fatEntries = BuildFatFirstEntries();
                WriteSector(hVolume, fat1Start, fatEntries);
                WriteSector(hVolume, fat2Start, fatEntries);

                progress?.Invoke(85);

                // 7. Wyzerowanie klastra katalogu głównego
                uint dataStart = RESERVED_SECTORS + (NUM_FATS * fatSize);
                byte[] emptyCluster = new byte[sectorsPerCluster * BYTES_PER_SECTOR];

                // Jeżeli podano etykietę, zapisz ją jako wpis katalogowy
                if (!string.IsNullOrWhiteSpace(volumeLabel))
                {
                    WriteVolumeLabelEntry(emptyCluster, volumeLabel);
                }

                for (int i = 0; i < sectorsPerCluster; i++)
                {
                    byte[] sectorData = new byte[BYTES_PER_SECTOR];
                    Array.Copy(emptyCluster, i * BYTES_PER_SECTOR, sectorData, 0, BYTES_PER_SECTOR);
                    WriteSector(hVolume, dataStart + (uint)i, sectorData);
                }

                progress?.Invoke(95);

                // 8. Odblokowanie woluminu
                DeviceIoControl(hVolume, FSCTL_UNLOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);

                progress?.Invoke(100);
            }
            catch
            {
                // W razie błędu próbujemy odblokować wolumin
                DeviceIoControl(hVolume, FSCTL_UNLOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
                throw;
            }
        }

        // ==========================================
        // Budowanie sektora rozruchowego (Boot Sector / BPB)
        // ==========================================

        /// <summary>
        /// Tworzy 512-bajtowy sektor rozruchowy FAT32 zgodny ze specyfikacją Microsoft.
        /// </summary>
        private static byte[] BuildBootSector(uint totalSectors, int sectorsPerCluster, uint fatSize, string volumeLabel)
        {
            byte[] bs = new byte[BYTES_PER_SECTOR];

            // BS_JmpBoot — instrukcja skoku (3 bajty)
            bs[0] = 0xEB;
            bs[1] = 0x58;
            bs[2] = 0x90;

            // BS_OEMName — nazwa OEM (8 bajtów)
            WriteString(bs, 3, "MSDOS5.0", 8);

            // BPB_BytsPerSec — bajtów na sektor (offset 0x0B, 2 bajty)
            WriteUInt16(bs, 0x0B, (ushort)BYTES_PER_SECTOR);

            // BPB_SecPerClus — sektorów na klaster (offset 0x0D, 1 bajt)
            bs[0x0D] = (byte)sectorsPerCluster;

            // BPB_RsvdSecCnt — sektory zarezerwowane (offset 0x0E, 2 bajty)
            WriteUInt16(bs, 0x0E, (ushort)RESERVED_SECTORS);

            // BPB_NumFATs — liczba kopii FAT (offset 0x10, 1 bajt)
            bs[0x10] = NUM_FATS;

            // BPB_RootEntCnt — dla FAT32 musi być 0 (offset 0x11, 2 bajty)
            WriteUInt16(bs, 0x11, 0);

            // BPB_TotSec16 — dla FAT32 musi być 0 (offset 0x13, 2 bajty)
            WriteUInt16(bs, 0x13, 0);

            // BPB_Media — typ nośnika: 0xF8 = dysk twardy/pendrive (offset 0x15, 1 bajt)
            bs[0x15] = 0xF8;

            // BPB_FATSz16 — dla FAT32 musi być 0 (offset 0x16, 2 bajty)
            WriteUInt16(bs, 0x16, 0);

            // BPB_SecPerTrk — sektory na ścieżkę (offset 0x18, 2 bajty) — wartość domyślna
            WriteUInt16(bs, 0x18, 63);

            // BPB_NumHeads — liczba głowic (offset 0x1A, 2 bajty) — wartość domyślna
            WriteUInt16(bs, 0x1A, 255);

            // BPB_HiddSec — sektory ukryte (offset 0x1C, 4 bajty)
            WriteUInt32(bs, 0x1C, 0);

            // BPB_TotSec32 — całkowita liczba sektorów (offset 0x20, 4 bajty)
            WriteUInt32(bs, 0x20, totalSectors);

            // === Pola rozszerzone FAT32 ===

            // BPB_FATSz32 — rozmiar jednej tablicy FAT w sektorach (offset 0x24, 4 bajty)
            WriteUInt32(bs, 0x24, fatSize);

            // BPB_ExtFlags — flagi rozszerzone (offset 0x28, 2 bajty) — 0 = lustrzane kopiowanie FAT
            WriteUInt16(bs, 0x28, 0);

            // BPB_FSVer — wersja systemu plików (offset 0x2A, 2 bajty) — 0.0
            WriteUInt16(bs, 0x2A, 0);

            // BPB_RootClus — pierwszy klaster katalogu głównego (offset 0x2C, 4 bajty)
            WriteUInt32(bs, 0x2C, ROOT_DIR_CLUSTER);

            // BPB_FSInfo — numer sektora FSInfo (offset 0x30, 2 bajty)
            WriteUInt16(bs, 0x30, (ushort)FSINFO_SECTOR);

            // BPB_BkBootSec — numer sektora kopii zapasowej boot sectora (offset 0x32, 2 bajty)
            WriteUInt16(bs, 0x32, (ushort)BACKUP_BOOT_SECTOR);

            // BPB_Reserved — zarezerwowane 12 bajtów (offset 0x34) — już wyzerowane

            // BS_DrvNum — numer dysku BIOS (offset 0x40, 1 bajt) — 0x80 = dysk twardy
            bs[0x40] = 0x80;

            // BS_Reserved1 (offset 0x41, 1 bajt) — zarezerwowany
            bs[0x41] = 0;

            // BS_BootSig — sygnatura rozszerzonego boot sectora (offset 0x42, 1 bajt)
            bs[0x42] = 0x29;

            // BS_VolID — numer seryjny woluminu (offset 0x43, 4 bajty) — losowy
            uint volId = GenerateVolumeId();
            WriteUInt32(bs, 0x43, volId);

            // BS_VolLab — etykieta woluminu (offset 0x47, 11 bajtów)
            WriteString(bs, 0x47, volumeLabel, 11);

            // BS_FilSysType — typ systemu plików (offset 0x52, 8 bajtów)
            WriteString(bs, 0x52, "FAT32   ", 8);

            // Sygnatura końca sektora (offset 0x1FE)
            bs[0x1FE] = 0x55;
            bs[0x1FF] = 0xAA;

            return bs;
        }

        // ==========================================
        // Budowanie sektora FSInfo
        // ==========================================

        /// <summary>
        /// Tworzy 512-bajtowy sektor FSInfo z informacjami o wolnych klastrach.
        /// </summary>
        private static byte[] BuildFSInfoSector(uint freeClusters)
        {
            byte[] fsi = new byte[BYTES_PER_SECTOR];

            // FSI_LeadSig (offset 0x00, 4 bajty)
            WriteUInt32(fsi, 0x00, 0x41615252);

            // FSI_Reserved1 (offset 0x04, 480 bajtów) — wyzerowane

            // FSI_StrucSig (offset 0x1E4, 4 bajty)
            WriteUInt32(fsi, 0x1E4, 0x61417272);

            // FSI_Free_Count — liczba wolnych klastrów (offset 0x1E8, 4 bajty)
            WriteUInt32(fsi, 0x1E8, freeClusters);

            // FSI_Nxt_Free — następny wolny klaster (offset 0x1EC, 4 bajty)
            // Klaster 2 = katalog główny, więc następny wolny to 3
            WriteUInt32(fsi, 0x1EC, 3);

            // FSI_Reserved2 (offset 0x1F0, 12 bajtów) — wyzerowane

            // FSI_TrailSig (offset 0x1FC, 4 bajty)
            WriteUInt32(fsi, 0x1FC, 0xAA550000);

            return fsi;
        }

        // ==========================================
        // Budowanie pierwszych wpisów FAT
        // ==========================================

        /// <summary>
        /// Tworzy sektor z pierwszymi 3 wpisami tablicy FAT:
        /// [0] = identyfikator nośnika, [1] = koniec łańcucha, [2] = koniec łańcucha (katalog główny)
        /// </summary>
        private static byte[] BuildFatFirstEntries()
        {
            byte[] fat = new byte[BYTES_PER_SECTOR];

            // Wpis 0: identyfikator nośnika (0xF8) + maska FAT32
            WriteUInt32(fat, 0, 0x0FFFFFF8);

            // Wpis 1: znacznik końca łańcucha (konwencja)
            WriteUInt32(fat, 4, 0x0FFFFFFF);

            // Wpis 2: katalog główny — koniec łańcucha (jeden klaster)
            WriteUInt32(fat, 8, 0x0FFFFFFF);

            return fat;
        }

        // ==========================================
        // Wpis etykiety woluminu w katalogu głównym
        // ==========================================

        /// <summary>
        /// Zapisuje wpis katalogowy z etykietą woluminu w danych klastra katalogu głównego.
        /// </summary>
        private static void WriteVolumeLabelEntry(byte[] clusterData, string volumeLabel)
        {
            // Wpis katalogowy FAT32 ma 32 bajty
            // Offset 0x00 (11 bajtów) — nazwa pliku / etykieta
            byte[] labelBytes = new byte[11];
            for (int i = 0; i < 11; i++) labelBytes[i] = 0x20; // Wypełnienie spacjami

            byte[] nameBytes = System.Text.Encoding.ASCII.GetBytes(volumeLabel);
            Array.Copy(nameBytes, 0, labelBytes, 0, Math.Min(nameBytes.Length, 11));
            Array.Copy(labelBytes, 0, clusterData, 0, 11);

            // Offset 0x0B (1 bajt) — atrybut: 0x08 = etykieta woluminu
            clusterData[0x0B] = 0x08;

            // Reszta wpisu (czas utworzenia itp.) zostawiamy wyzerowaną
        }

        // ==========================================
        // Zerowanie tablicy FAT (może trwać chwilę)
        // ==========================================

        /// <summary>
        /// Zeruje całą tablicę FAT sektor po sektorze.
        /// Dla dużych dysków tablica FAT może mieć setki tysięcy sektorów.
        /// </summary>
        private static void ClearFat(SafeFileHandle hVolume, uint fatStart, uint fatSize,
            Action<int>? progress, int progressStart, int progressEnd)
        {
            byte[] zeroes = new byte[BYTES_PER_SECTOR];
            int progressRange = progressEnd - progressStart;

            // Zapisujemy zerami w blokach po 128 sektorów dla wydajności
            const int BLOCK_SIZE = 128;
            byte[] zeroBlock = new byte[BYTES_PER_SECTOR * BLOCK_SIZE];

            uint sectorsWritten = 0;
            while (sectorsWritten < fatSize)
            {
                uint remaining = fatSize - sectorsWritten;
                uint toWrite = Math.Min(remaining, BLOCK_SIZE);

                // Ustawienie pozycji na dysku
                long offset = (long)(fatStart + sectorsWritten) * BYTES_PER_SECTOR;
                SetFilePointerEx(hVolume, offset, out _, FILE_BEGIN);

                // Zapis bloku zerami
                uint bytesToWrite = toWrite * BYTES_PER_SECTOR;
                byte[] buffer = (toWrite == BLOCK_SIZE) ? zeroBlock : new byte[bytesToWrite];

                if (!WriteFile(hVolume, buffer, bytesToWrite, out uint written, IntPtr.Zero))
                    throw new Exception($"Błąd zerowania tablicy FAT. Kod błędu: {Marshal.GetLastWin32Error()}");

                if (written != bytesToWrite)
                    throw new Exception("Nie udało się zapisać wszystkich bajtów podczas zerowania FAT.");

                sectorsWritten += toWrite;

                // Raportowanie postępu
                if (progress != null && fatSize > 0)
                {
                    int pct = progressStart + (int)((long)sectorsWritten * progressRange / fatSize);
                    progress(Math.Min(pct, progressEnd));
                }
            }
        }

        // ==========================================
        // Obliczenia parametrów FAT32
        // ==========================================

        /// <summary>
        /// Dobiera liczbę sektorów na klaster na podstawie rozmiaru dysku.
        /// Tabela zgodna ze specyfikacją Microsoft.
        /// </summary>
        private static int GetSectorsPerCluster(long totalBytes)
        {
            long totalMB = totalBytes / (1024 * 1024);

            if (totalMB <= 260)      return 1;   // 512 B klaster
            if (totalMB <= 8192)     return 8;   // 4 KB klaster
            if (totalMB <= 16384)    return 16;  // 8 KB klaster
            if (totalMB <= 32768)    return 32;  // 16 KB klaster
            return 64;                            // 32 KB klaster (dla dysków > 32 GB)
        }

        /// <summary>
        /// Oblicza rozmiar jednej tablicy FAT w sektorach.
        /// Wzór z oficjalnej specyfikacji Microsoft FAT32.
        /// </summary>
        private static uint CalculateFatSize(uint totalSectors, int sectorsPerCluster)
        {
            // Wzór ze specyfikacji:
            // FATSz = (TotalSectors - ReservedSectors) / (SecPerCluster * (BytsPerSec / 4) + NumFATs) + 1 (zaokrąglenie w górę)
            // Uproszczony: każdy klaster wymaga 4 bajtów w FAT
            uint dataSectors = totalSectors - RESERVED_SECTORS;
            uint entriesPerFatSector = (uint)(BYTES_PER_SECTOR / 4); // 128 wpisów FAT na sektor

            // Liczba klastrów = (dataSectors - numFATs * fatSize) / secPerCluster
            // fatSize * entriesPerFatSector >= numClusters + 2
            // Rozwiązujemy iteracyjnie aby uniknąć problemów z zaokrągleniem
            uint fatSize = (dataSectors / ((uint)sectorsPerCluster * entriesPerFatSector + NUM_FATS)) + 1;

            return fatSize;
        }

        // ==========================================
        // Narzędzia I/O
        // ==========================================

        /// <summary>
        /// Zapisuje pojedynczy sektor na dysk pod podanym numerem sektora.
        /// </summary>
        private static void WriteSector(SafeFileHandle hVolume, uint sectorNumber, byte[] data)
        {
            if (data.Length != BYTES_PER_SECTOR)
                throw new ArgumentException($"Dane muszą mieć dokładnie {BYTES_PER_SECTOR} bajtów (jeden sektor).");

            long offset = (long)sectorNumber * BYTES_PER_SECTOR;
            if (!SetFilePointerEx(hVolume, offset, out _, FILE_BEGIN))
                throw new Exception($"Nie można ustawić pozycji na sektorze {sectorNumber}. Kod błędu: {Marshal.GetLastWin32Error()}");

            if (!WriteFile(hVolume, data, (uint)BYTES_PER_SECTOR, out uint written, IntPtr.Zero))
                throw new Exception($"Nie można zapisać sektora {sectorNumber}. Kod błędu: {Marshal.GetLastWin32Error()}");

            if (written != BYTES_PER_SECTOR)
                throw new Exception($"Zapisano tylko {written} z {BYTES_PER_SECTOR} bajtów na sektorze {sectorNumber}.");
        }

        /// <summary>
        /// Odczytuje rozmiar woluminu w bajtach.
        /// </summary>
        private static long GetVolumeSizeBytes(string driveLetter)
        {
            string path = driveLetter.EndsWith("\\") ? driveLetter : driveLetter + "\\";
            if (GetDiskFreeSpaceEx(path, out _, out long totalBytes, out _))
                return totalBytes;

            // Metoda zapasowa — DriveInfo
            try
            {
                var driveInfo = new DriveInfo(driveLetter.Substring(0, 1));
                return driveInfo.TotalSize;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Generuje losowy numer seryjny woluminu (Volume ID) na podstawie aktualnej daty i czasu.
        /// Metoda zgodna z konwencją Windows — łączy datę i czas w 32-bitową wartość.
        /// </summary>
        private static uint GenerateVolumeId()
        {
            DateTime now = DateTime.Now;
            uint lo = (uint)((now.Day + (now.Month << 8)) + (now.Millisecond << 16));
            uint hi = (uint)((now.Minute + (now.Hour << 8)) + (now.Second << 16));
            return lo + hi;
        }

        /// <summary>
        /// Normalizuje etykietę woluminu — wielkie litery, max 11 znaków, dopełnienie spacjami.
        /// </summary>
        private static string NormalizeVolumeLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return "NO NAME    ";

            label = label.ToUpperInvariant().Trim();
            if (label.Length > 11) label = label.Substring(0, 11);
            return label.PadRight(11);
        }

        // ==========================================
        // Pomocnicze metody zapisu little-endian
        // ==========================================

        private static void WriteUInt16(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static void WriteUInt32(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteString(byte[] buffer, int offset, string value, int length)
        {
            byte[] strBytes = System.Text.Encoding.ASCII.GetBytes(value.PadRight(length));
            Array.Copy(strBytes, 0, buffer, offset, Math.Min(strBytes.Length, length));
        }
    }
}
