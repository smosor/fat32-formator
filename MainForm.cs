using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;

namespace Fat32Formator
{
    public partial class MainForm : Form
    {
        private const string Fat32FormatUrl = "http://ridgecrop.co.uk/fat32format.exe";
        private readonly string Fat32FormatPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fat32format.exe");

        public MainForm()
        {
            InitializeComponent();
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            if (!IsAdministrator())
            {
                MessageBox.Show("Aplikacja wymaga uprawnień administratora do formatowania dysków. Uruchom ponownie jako Administrator.", "Błąd uprawnień", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }

            LoadDrives();

            if (!File.Exists(Fat32FormatPath))
            {
                await DownloadFat32FormatAsync();
            }
        }

        private bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private void LoadDrives()
        {
            cmbDrives.Items.Clear();
            var drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Removable && d.IsReady).ToList();
            
            foreach (var drive in drives)
            {
                double sizeGb = Math.Round(drive.TotalSize / (1024.0 * 1024.0 * 1024.0), 1);
                string label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Dysk Wymienny" : drive.VolumeLabel;
                cmbDrives.Items.Add($"{drive.Name} [{label}] - {sizeGb} GB");
            }

            if (cmbDrives.Items.Count > 0)
            {
                cmbDrives.SelectedIndex = 0;
                btnStart.Enabled = true;
            }
            else
            {
                cmbDrives.Items.Add("Brak dysków wymiennych");
                cmbDrives.SelectedIndex = 0;
                btnStart.Enabled = false;
            }
            if (cmbFileSystem.Items.Count > 0) cmbFileSystem.SelectedIndex = 0;
        }

        private async Task DownloadFat32FormatAsync()
        {
            try
            {
                lblDrives.Text = "Pobieranie narzędzia...";
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(Fat32FormatUrl);
                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsByteArrayAsync();
                    File.WriteAllBytes(Fat32FormatPath, content);
                }
                lblDrives.Text = "Urządzenie:";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się pobrać fat32format.exe.\n{ex.Message}\nUpewnij się, że masz internet przy pierwszym uruchomieniu.", "Błąd pobierania", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblDrives.Text = "Urządzenie:";
            }
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private async void BtnStart_Click(object sender, EventArgs e)
        {
            if (cmbDrives.SelectedIndex < 0 || !btnStart.Enabled) return;

            string selectedText = cmbDrives.SelectedItem.ToString();
            if (selectedText == "Brak dysków wymiennych") return;

            string driveLetter = selectedText.Substring(0, 2); // np. "H:"
            
            var result = MessageBox.Show($"UWAGA: Formatowanie wykasuje wszystkie dane na dysku {driveLetter}\nCzy na pewno chcesz kontynuować?", "Potwierdzenie formatowania", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            
            if (result == DialogResult.No) return;

            btnStart.Enabled = false;
            btnClose.Enabled = false;
            pbProgress.Style = ProgressBarStyle.Marquee;

            try
            {
                // 1. Diskpart clean i MBR
                await RunDiskpartAsync(driveLetter);

                // 2. Format na FAT32
                // Poczekaj chwilkę po diskparcie, żeby system zdążył zidentyfikować nową partycję
                await Task.Delay(2000);
                
                await RunFat32FormatAsync(driveLetter, txtVolumeLabel.Text);

                MessageBox.Show($"Dysk {driveLetter} został pomyślnie sformatowany do FAT32 (MBR) i jest gotowy dla CDJ 2000!", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadDrives(); // odśwież widok
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wystąpił błąd podczas formatowania:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                pbProgress.Style = ProgressBarStyle.Continuous;
                pbProgress.Value = 0;
                btnStart.Enabled = true;
                btnClose.Enabled = true;
            }
        }

        private Task RunDiskpartAsync(string driveLetter)
        {
            return Task.Run(() =>
            {
                string scriptPath = Path.Combine(Path.GetTempPath(), "diskpart_script.txt");
                string volumeId = driveLetter.Replace(":", "");
                
                int diskNumber = GetDiskNumberFromDriveLetter(driveLetter);
                if (diskNumber == -1) throw new Exception($"Nie można odnaleźć fizycznego numeru dysku dla litery {driveLetter}.");

                string[] script = {
                    $"select disk {diskNumber}",
                    "clean",
                    "convert mbr",
                    "create partition primary",
                    "select partition 1",
                    "active",
                    $"assign letter={volumeId}"
                };

                File.WriteAllLines(scriptPath, script);

                var psi = new ProcessStartInfo("diskpart.exe", $"/s \"{scriptPath}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit();
                    if (proc.ExitCode != 0)
                    {
                        string err = proc.StandardError.ReadToEnd();
                        string outpt = proc.StandardOutput.ReadToEnd();
                        throw new Exception($"Błąd Diskpart: {err} {outpt}");
                    }
                }
                
                File.Delete(scriptPath);
            });
        }

        private int GetDiskNumberFromDriveLetter(string driveLetter)
        {
            int diskNumber = -1;
            try
            {
                string query = $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter}'}} WHERE AssocClass=Win32_LogicalDiskToPartition";
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject partition in searcher.Get())
                    {
                        string partitionQuery = $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition";
                        using (var diskSearcher = new ManagementObjectSearcher(partitionQuery))
                        {
                            foreach (ManagementObject disk in diskSearcher.Get())
                            {
                                diskNumber = Convert.ToInt32(disk["Index"]);
                                return diskNumber;
                            }
                        }
                    }
                }
            }
            catch { }
            return diskNumber;
        }

        private Task RunFat32FormatAsync(string driveLetter, string volumeLabel)
        {
            return Task.Run(() =>
            {
                if (!File.Exists(Fat32FormatPath))
                {
                    throw new FileNotFoundException("Brak narzędzia fat32format.exe. Zamknij program, połącz z internetem i spróbuj ponownie.");
                }

                // fat32format nie lubi spacji po -v
                string labelArg = string.IsNullOrWhiteSpace(volumeLabel) ? "" : $"-v{volumeLabel}";
                
                var psi = new ProcessStartInfo(Fat32FormatPath, $"{driveLetter} {labelArg}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var proc = Process.Start(psi))
                {
                    proc.StandardInput.WriteLine("y"); // Confirm
                    proc.WaitForExit();
                    if (proc.ExitCode != 0)
                    {
                        string output = proc.StandardOutput.ReadToEnd();
                        string err = proc.StandardError.ReadToEnd();
                        throw new Exception($"Błąd fat32format:\n{output}\n{err}");
                    }
                }
            });
        }
    }
}
