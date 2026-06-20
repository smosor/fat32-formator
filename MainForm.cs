using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;

namespace Fat32Formator
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            if (!IsAdministrator())
            {
                try
                {
                    var processInfo = new ProcessStartInfo(Environment.ProcessPath)
                    {
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(processInfo);
                }
                catch
                {
                    MessageBox.Show("Aplikacja wymaga uprawnień administratora do formatowania dysków. Odmówiono dostępu.", "Błąd uprawnień", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                Application.Exit();
                return;
            }

            LoadDrives();
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

        private void BtnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private async void BtnStart_Click(object sender, EventArgs e)
        {
            if (cmbDrives.SelectedIndex < 0 || !btnStart.Enabled) return;

            string? selectedText = cmbDrives.SelectedItem?.ToString();
            if (selectedText == null || selectedText == "Brak dysków wymiennych") return;

            string driveLetter = selectedText.Substring(0, 2); // np. "H:"
            
            // Sprawdzenie rozmiaru dysku
            long driveSize = 0;
            try
            {
                var driveInfo = new DriveInfo(driveLetter.Substring(0, 1));
                driveSize = driveInfo.TotalSize;
            }
            catch { }

            bool usaNatywneFormatowanie = driveSize > 32L * 1024 * 1024 * 1024; // > 32 GB
            string metodaInfo = usaNatywneFormatowanie ? " (natywne formatowanie — dysk > 32GB)" : "";
            
            var result = MessageBox.Show(
                $"UWAGA: Formatowanie wykasuje wszystkie dane na dysku {driveLetter}{metodaInfo}\nCzy na pewno chcesz kontynuować?",
                "Potwierdzenie formatowania",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            
            if (result == DialogResult.No) return;

            btnStart.Enabled = false;
            btnClose.Enabled = false;
            pbProgress.Style = ProgressBarStyle.Continuous;
            pbProgress.Value = 0;
            pbProgress.Maximum = 100;

            try
            {
                // 1. Diskpart — czyszczenie dysku i przygotowanie struktury MBR
                pbProgress.Style = ProgressBarStyle.Marquee;
                await RunDiskpartAsync(driveLetter);

                // 2. Odczekanie aż Windows rozpozna nową partycję
                await Task.Delay(2000);
                
                // 3. Formatowanie na FAT32
                pbProgress.Style = ProgressBarStyle.Continuous;
                pbProgress.Value = 0;

                if (usaNatywneFormatowanie)
                {
                    // Dysk > 32GB — użycie natywnego formattera (bez zewnętrznych programów)
                    await Task.Run(() =>
                    {
                        Fat32Formatter.Format(driveLetter, txtVolumeLabel.Text, (postep) =>
                        {
                            // Aktualizacja progressbar z wątku UI
                            this.Invoke(() => pbProgress.Value = Math.Min(postep, 100));
                        });
                    });
                }
                else
                {
                    // Dysk ≤ 32GB — systemowe polecenie format (szybkie i niezawodne)
                    await RunSystemFormatAsync(driveLetter, txtVolumeLabel.Text);
                }

                pbProgress.Value = 100;
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

        private Task RunSystemFormatAsync(string driveLetter, string volumeLabel)
        {
            return Task.Run(() =>
            {
                string labelArg = string.IsNullOrWhiteSpace(volumeLabel) ? "" : $"/V:{volumeLabel}";
                
                var psi = new ProcessStartInfo("cmd.exe", $"/c echo Y | format {driveLetter} /FS:FAT32 /Q {labelArg}")
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
                        string output = proc.StandardOutput.ReadToEnd();
                        string err = proc.StandardError.ReadToEnd();
                        throw new Exception($"Błąd formatowania systemowego:\n{output}\n{err}\nUwaga: Windows natywnie odrzuca formatowanie FAT32 dla dysków większych niż 32 GB.");
                    }
                }
            });
        }
    }
}
