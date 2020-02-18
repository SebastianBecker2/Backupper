using Backupper.Properties;
using Ionic.Zip;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Backupper
{
    public partial class frmMain : Form
    {
        private volatile bool Canceled = false;

        public frmMain()
        {
            InitializeComponent();

            if (Settings.Default.Folders == null)
            {
                Settings.Default.Folders = new List<string>();
            }

            if (Settings.Default.Files == null)
            {
                Settings.Default.Files = new List<string>();
            }

            UpdateBackupContentList();
        }

        private void UpdateBackupContentList()
        {
            lsbBackupContent.Items.Clear();
            foreach (var folder in Settings.Default.Folders)
            {
                lsbBackupContent.Items.Add(folder);
            }
            foreach (var file in Settings.Default.Files)
            {
                lsbBackupContent.Items.Add(file);
            }
        }

        private void BtnAddFolder_Click(object sender, EventArgs e)
        {
            using (var dlg = new CommonOpenFileDialog())
            {
                dlg.IsFolderPicker = true;
                dlg.Multiselect = true;
                if (dlg.ShowDialog() != CommonFileDialogResult.Ok)
                {
                    return;
                }

                Settings.Default.Folders.AddRange(dlg.FileNames);
                Settings.Default.Save();
                UpdateBackupContentList();
            }
        }

        private void btnFile_Click(object sender, EventArgs e)
        {
            using (var dlg = new CommonOpenFileDialog())
            {
                dlg.IsFolderPicker = false;
                dlg.Multiselect = true;
                if (dlg.ShowDialog() != CommonFileDialogResult.Ok)
                {
                    return;
                }

                Settings.Default.Files.AddRange(dlg.FileNames);
                Settings.Default.Save();
                UpdateBackupContentList();
            }
        }

        private void btnRemoveFolder_Click(object sender, EventArgs e)
        {
            if (lsbBackupContent.SelectedIndex < 0)
            {
                return;
            }
            Settings.Default.Folders.Remove(lsbBackupContent.SelectedItem.ToString());
            Settings.Default.Files.Remove(lsbBackupContent.SelectedItem.ToString());
            Settings.Default.Save();
            UpdateBackupContentList();
        }

        private string SelectOutputFile()
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.FileName = "Backup";
                dlg.Filter = "Zip File (*.zip)|*.zip";
                if (dlg.ShowDialog() != DialogResult.OK)
                {
                    return null;
                }
                return dlg.FileName;
            }
        }

        private void btnZipFiles_Click(object sender, EventArgs e)
        {
            var file_name = SelectOutputFile();
            if (string.IsNullOrWhiteSpace(file_name))
            {
                return;
            }

            Canceled = false;
            btnCancel.Visible = true;
            btnZipFiles.Visible = false;

            MessageBox.Show("Make sure Thunderbird is closed before you continue.");

            Task.Run(() =>
            {
                using (var zip_file = new ZipFile())
                {
                    zip_file.UseZip64WhenSaving = Zip64Option.Always;
                    zip_file.CompressionLevel = Ionic.Zlib.CompressionLevel.BestCompression;
                    zip_file.CompressionMethod = CompressionMethod.BZip2;
                    zip_file.Password = txtPassword.Text;
                    zip_file.MaxOutputSegmentSize = 1024 * 1024 * 200;

                    foreach (var folder in Settings.Default.Folders)
                    {
                        zip_file.AddDirectory(folder, Path.GetFileName(folder));
                    }

                    zip_file.AddFiles(Settings.Default.Files);

                    zip_file.SaveProgress += (s, args) =>
                    {
                        args.Cancel = Canceled;
                        if (args.EventType != ZipProgressEventType.Saving_AfterWriteEntry)
                        {
                            return;
                        }
                        FileProgress.Invoke(new Action(() =>
                        {
                            FileProgress.Maximum = args.EntriesTotal;
                            FileProgress.Value = args.EntriesSaved;
                        }));
                    };

                    zip_file.Save(file_name);
                }
            }).ContinueWith((t) =>
            {
                this.Invoke(new Action(() =>
                {
                    MessageBox.Show("Done!");
                }));
            });
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Canceled = true;
            btnCancel.Visible = false;
            btnZipFiles.Visible = true;
        }
    }
}
