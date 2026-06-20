using System.Text;
using Fat32Formator;
using Xunit;

namespace Fat32Formator.Tests
{
    /// <summary>
    /// Testy metod pomocniczych zapisu little-endian.
    /// </summary>
    public class WriteHelperTests
    {
        [Fact]
        public void WriteUInt16_ZapisujeWartoscLittleEndian()
        {
            byte[] buf = new byte[4];
            Fat32Formatter.WriteUInt16(buf, 0, 0x0200); // 512

            Assert.Equal(0x00, buf[0]); // młodszy bajt
            Assert.Equal(0x02, buf[1]); // starszy bajt
        }

        [Fact]
        public void WriteUInt16_ZapisujeNaPodanymOffsecie()
        {
            byte[] buf = new byte[4];
            Fat32Formatter.WriteUInt16(buf, 2, 0xAABB);

            Assert.Equal(0x00, buf[0]); // nie tknięty
            Assert.Equal(0xBB, buf[2]);
            Assert.Equal(0xAA, buf[3]);
        }

        [Fact]
        public void WriteUInt32_ZapisujeWartoscLittleEndian()
        {
            byte[] buf = new byte[4];
            Fat32Formatter.WriteUInt32(buf, 0, 0x0FFFFFF8);

            Assert.Equal(0xF8, buf[0]);
            Assert.Equal(0xFF, buf[1]);
            Assert.Equal(0xFF, buf[2]);
            Assert.Equal(0x0F, buf[3]);
        }

        [Fact]
        public void WriteString_ZapisujeASCIIIDopelniaSpasjami()
        {
            byte[] buf = new byte[8];
            Fat32Formatter.WriteString(buf, 0, "FAT32", 8);

            string result = Encoding.ASCII.GetString(buf, 0, 8);
            Assert.Equal("FAT32   ", result);
        }

        [Fact]
        public void WriteString_ObcinaZaDlugiString()
        {
            byte[] buf = new byte[4];
            Fat32Formatter.WriteString(buf, 0, "ABCDEF", 4);

            string result = Encoding.ASCII.GetString(buf, 0, 4);
            Assert.Equal("ABCD", result);
        }
    }

    /// <summary>
    /// Testy normalizacji etykiety woluminu.
    /// </summary>
    public class NormalizeVolumeLabelTests
    {
        [Fact]
        public void PustaEtykieta_ZwracaNoName()
        {
            string result = Fat32Formatter.NormalizeVolumeLabel("");
            Assert.Equal("NO NAME    ", result);
        }

        [Fact]
        public void NullEtykieta_ZwracaNoName()
        {
            string result = Fat32Formatter.NormalizeVolumeLabel(null!);
            Assert.Equal("NO NAME    ", result);
        }

        [Fact]
        public void KrotkaNazwa_DopelnionaSpasjami()
        {
            string result = Fat32Formatter.NormalizeVolumeLabel("USB");
            Assert.Equal(11, result.Length);
            Assert.Equal("USB        ", result);
        }

        [Fact]
        public void MaleLitery_ZamienioneNaWielkie()
        {
            string result = Fat32Formatter.NormalizeVolumeLabel("pendrive");
            Assert.StartsWith("PENDRIVE", result);
        }

        [Fact]
        public void ZaDlugaNazwa_ObcietaDo11Znakow()
        {
            string result = Fat32Formatter.NormalizeVolumeLabel("BARDZO_DLUGA_NAZWA_ETYKIETY");
            Assert.Equal(11, result.Length);
        }

        [Fact]
        public void NazwaDokladnie11Znakow_BezZmian()
        {
            string result = Fat32Formatter.NormalizeVolumeLabel("ABCDEFGHIJK");
            Assert.Equal("ABCDEFGHIJK", result);
        }
    }

    /// <summary>
    /// Testy doboru sektorów na klaster wg specyfikacji Microsoft.
    /// </summary>
    public class SectorsPerClusterTests
    {
        [Theory]
        [InlineData(100L * 1024 * 1024, 1)]         // 100 MB → 1 spc
        [InlineData(260L * 1024 * 1024, 1)]          // 260 MB → 1 spc
        [InlineData(512L * 1024 * 1024, 8)]          // 512 MB → 8 spc
        [InlineData(4L * 1024 * 1024 * 1024, 8)]     // 4 GB → 8 spc
        [InlineData(8L * 1024 * 1024 * 1024, 8)]     // 8 GB → 8 spc
        [InlineData(10L * 1024 * 1024 * 1024, 16)]   // 10 GB → 16 spc
        [InlineData(16L * 1024 * 1024 * 1024, 16)]   // 16 GB → 16 spc
        [InlineData(20L * 1024 * 1024 * 1024, 32)]   // 20 GB → 32 spc
        [InlineData(32L * 1024 * 1024 * 1024, 32)]   // 32 GB → 32 spc
        [InlineData(64L * 1024 * 1024 * 1024, 64)]   // 64 GB → 64 spc
        [InlineData(128L * 1024 * 1024 * 1024, 64)]  // 128 GB → 64 spc
        [InlineData(256L * 1024 * 1024 * 1024, 64)]  // 256 GB → 64 spc
        public void ZwracaPoprawnySectorsPerCluster(long totalBytes, int oczekiwany)
        {
            int wynik = Fat32Formatter.GetSectorsPerCluster(totalBytes);
            Assert.Equal(oczekiwany, wynik);
        }
    }

    /// <summary>
    /// Testy obliczania rozmiaru tablicy FAT.
    /// </summary>
    public class CalculateFatSizeTests
    {
        [Fact]
        public void ZwracaWartoscWiekszaOdZera()
        {
            // 64 GB = 134217728 sektorów (512 bajtów/sektor)
            uint totalSectors = 134217728;
            int spc = 64;

            uint fatSize = Fat32Formatter.CalculateFatSize(totalSectors, spc);

            Assert.True(fatSize > 0, "Rozmiar FAT musi być większy od zera");
        }

        [Fact]
        public void WiekszyDysk_WiekszyFat()
        {
            uint small = Fat32Formatter.CalculateFatSize(2097152, 8);   // 1 GB
            uint large = Fat32Formatter.CalculateFatSize(134217728, 64); // 64 GB

            Assert.True(large > small, "Większy dysk powinien mieć większą tablicę FAT");
        }

        [Fact]
        public void FatPokrywaWszystkieKlastry()
        {
            uint totalSectors = 134217728; // 64 GB
            int spc = 64;
            uint fatSize = Fat32Formatter.CalculateFatSize(totalSectors, spc);

            // FAT ma pokryć wszystkie klastry danych
            // Każdy sektor FAT mieści 128 wpisów (512 / 4)
            uint maxClusters = fatSize * 128;
            uint dataSectors = totalSectors - 32 - (2 * fatSize);
            uint actualClusters = dataSectors / (uint)spc;

            Assert.True(maxClusters >= actualClusters,
                $"FAT ({maxClusters} wpisów) musi pokryć wszystkie klastry ({actualClusters})");
        }
    }

    /// <summary>
    /// Testy budowania sektora rozruchowego (Boot Sector / BPB).
    /// </summary>
    public class BootSectorTests
    {
        private byte[] UtworzBootSector()
        {
            return Fat32Formatter.BuildBootSector(
                totalSectors: 134217728, // 64 GB
                sectorsPerCluster: 64,
                fatSize: 16384,
                volumeLabel: "PENDRIVE   ");
        }

        [Fact]
        public void Ma512Bajtow()
        {
            byte[] bs = UtworzBootSector();
            Assert.Equal(512, bs.Length);
        }

        [Fact]
        public void ZawieraInstrukcjeSkoku()
        {
            byte[] bs = UtworzBootSector();
            Assert.Equal(0xEB, bs[0]);
            Assert.Equal(0x58, bs[1]);
            Assert.Equal(0x90, bs[2]);
        }

        [Fact]
        public void ZawieraNazweOEM()
        {
            byte[] bs = UtworzBootSector();
            string oem = Encoding.ASCII.GetString(bs, 3, 8);
            Assert.Equal("MSDOS5.0", oem);
        }

        [Fact]
        public void BytesPerSectorJest512()
        {
            byte[] bs = UtworzBootSector();
            ushort bps = BitConverter.ToUInt16(bs, 0x0B);
            Assert.Equal(512, bps);
        }

        [Fact]
        public void SectorsPerClusterJest64()
        {
            byte[] bs = UtworzBootSector();
            Assert.Equal(64, bs[0x0D]);
        }

        [Fact]
        public void ReservedSectorsJest32()
        {
            byte[] bs = UtworzBootSector();
            ushort rsvd = BitConverter.ToUInt16(bs, 0x0E);
            Assert.Equal(32, rsvd);
        }

        [Fact]
        public void LiczbaFATJest2()
        {
            byte[] bs = UtworzBootSector();
            Assert.Equal(2, bs[0x10]);
        }

        [Fact]
        public void MediaTypeJestF8()
        {
            byte[] bs = UtworzBootSector();
            Assert.Equal(0xF8, bs[0x15]);
        }

        [Fact]
        public void TotalSectors32JestPoprawny()
        {
            byte[] bs = UtworzBootSector();
            uint total = BitConverter.ToUInt32(bs, 0x20);
            Assert.Equal(134217728u, total);
        }

        [Fact]
        public void FatSize32JestPoprawny()
        {
            byte[] bs = UtworzBootSector();
            uint fatSz = BitConverter.ToUInt32(bs, 0x24);
            Assert.Equal(16384u, fatSz);
        }

        [Fact]
        public void RootClusterJest2()
        {
            byte[] bs = UtworzBootSector();
            uint rootClus = BitConverter.ToUInt32(bs, 0x2C);
            Assert.Equal(2u, rootClus);
        }

        [Fact]
        public void FSInfoSektorJest1()
        {
            byte[] bs = UtworzBootSector();
            ushort fsi = BitConverter.ToUInt16(bs, 0x30);
            Assert.Equal(1, fsi);
        }

        [Fact]
        public void BackupBootSectorJest6()
        {
            byte[] bs = UtworzBootSector();
            ushort bk = BitConverter.ToUInt16(bs, 0x32);
            Assert.Equal(6, bk);
        }

        [Fact]
        public void SygnaturaBoot0x29()
        {
            byte[] bs = UtworzBootSector();
            Assert.Equal(0x29, bs[0x42]);
        }

        [Fact]
        public void EtykietaWoluminu()
        {
            byte[] bs = UtworzBootSector();
            string label = Encoding.ASCII.GetString(bs, 0x47, 11);
            Assert.Equal("PENDRIVE   ", label);
        }

        [Fact]
        public void TypSystemuPlikowFAT32()
        {
            byte[] bs = UtworzBootSector();
            string fsType = Encoding.ASCII.GetString(bs, 0x52, 8);
            Assert.Equal("FAT32   ", fsType);
        }

        [Fact]
        public void SygnaturaKoncaSektora()
        {
            byte[] bs = UtworzBootSector();
            Assert.Equal(0x55, bs[0x1FE]);
            Assert.Equal(0xAA, bs[0x1FF]);
        }
    }

    /// <summary>
    /// Testy budowania sektora FSInfo.
    /// </summary>
    public class FSInfoSectorTests
    {
        [Fact]
        public void Ma512Bajtow()
        {
            byte[] fsi = Fat32Formatter.BuildFSInfoSector(1000);
            Assert.Equal(512, fsi.Length);
        }

        [Fact]
        public void LeadSigJestPoprawny()
        {
            byte[] fsi = Fat32Formatter.BuildFSInfoSector(1000);
            uint sig = BitConverter.ToUInt32(fsi, 0x00);
            Assert.Equal(0x41615252u, sig);
        }

        [Fact]
        public void StrucSigJestPoprawny()
        {
            byte[] fsi = Fat32Formatter.BuildFSInfoSector(1000);
            uint sig = BitConverter.ToUInt32(fsi, 0x1E4);
            Assert.Equal(0x61417272u, sig);
        }

        [Fact]
        public void FreeCountJestPoprawny()
        {
            byte[] fsi = Fat32Formatter.BuildFSInfoSector(12345);
            uint free = BitConverter.ToUInt32(fsi, 0x1E8);
            Assert.Equal(12345u, free);
        }

        [Fact]
        public void NextFreeClusterJest3()
        {
            byte[] fsi = Fat32Formatter.BuildFSInfoSector(1000);
            uint nxt = BitConverter.ToUInt32(fsi, 0x1EC);
            Assert.Equal(3u, nxt);
        }

        [Fact]
        public void TrailSigJestPoprawny()
        {
            byte[] fsi = Fat32Formatter.BuildFSInfoSector(1000);
            uint sig = BitConverter.ToUInt32(fsi, 0x1FC);
            Assert.Equal(0xAA550000u, sig);
        }
    }

    /// <summary>
    /// Testy pierwszych wpisów tablicy FAT.
    /// </summary>
    public class FatEntriesTests
    {
        [Fact]
        public void Ma512Bajtow()
        {
            byte[] fat = Fat32Formatter.BuildFatFirstEntries();
            Assert.Equal(512, fat.Length);
        }

        [Fact]
        public void Wpis0_IdentyfikatorNosnika()
        {
            byte[] fat = Fat32Formatter.BuildFatFirstEntries();
            uint entry0 = BitConverter.ToUInt32(fat, 0);
            Assert.Equal(0x0FFFFFF8u, entry0);
        }

        [Fact]
        public void Wpis1_KoniecLancucha()
        {
            byte[] fat = Fat32Formatter.BuildFatFirstEntries();
            uint entry1 = BitConverter.ToUInt32(fat, 4);
            Assert.Equal(0x0FFFFFFFu, entry1);
        }

        [Fact]
        public void Wpis2_KatalogGlowny()
        {
            byte[] fat = Fat32Formatter.BuildFatFirstEntries();
            uint entry2 = BitConverter.ToUInt32(fat, 8);
            Assert.Equal(0x0FFFFFFFu, entry2);
        }

        [Fact]
        public void PozostaleWpisyWyzerowane()
        {
            byte[] fat = Fat32Formatter.BuildFatFirstEntries();
            for (int i = 12; i < 512; i++)
            {
                Assert.Equal(0, fat[i]);
            }
        }
    }

    /// <summary>
    /// Testy wpisu etykiety woluminu w katalogu głównym.
    /// </summary>
    public class VolumeLabelEntryTests
    {
        [Fact]
        public void ZapisujeNazweNaPierwszych11Bajtach()
        {
            byte[] cluster = new byte[512];
            Fat32Formatter.WriteVolumeLabelEntry(cluster, "PENDRIVE   ");

            string name = Encoding.ASCII.GetString(cluster, 0, 11);
            Assert.Equal("PENDRIVE   ", name);
        }

        [Fact]
        public void UstawiaAtrybutEtykiety()
        {
            byte[] cluster = new byte[512];
            Fat32Formatter.WriteVolumeLabelEntry(cluster, "TEST       ");

            Assert.Equal(0x08, cluster[0x0B]);
        }
    }
}
