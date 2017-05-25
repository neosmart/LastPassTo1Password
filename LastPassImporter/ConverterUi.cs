using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic.FileIO;
using System.Diagnostics;
using NeoSmart.ExtensionMethods;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace LastPassImporter
{
    class ConverterUi : Form
    {
        public ConverterUi()
            : base()
        {
            Visible = false;

            try
            {
                LoadAndConvert();
            }
            catch (ConverterException ex)
            {
                MessageBox.Show(ex.Message, ex.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unhandled exception during conversion!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            Load += (s, e) => Application.Exit();
        }

        private bool Retry(string title, string message, Func<bool> action)
        {
            while (true)
            {
                var actionResult = action();

                if (actionResult)
                {
                    return true;
                }

                var result = MessageBox.Show(message, title, MessageBoxButtons.RetryCancel, MessageBoxIcon.Error);
                if (result == DialogResult.Retry)
                {
                    continue;
                }
                else
                {
                    return false;
                }
            }
        }

        private void LoadAndConvert()
        {
            List<PasswordEntry> lastPassEntries = null;
            string csvPath = null;
            if (!Retry("Invalid/corrupt LastPass export!", 
                "The converter was unable to safely load the selected file as a CSV. Ensure the export was correctly generated as a CSV file in LastPass and try again!",
                () =>
                {
                    var loader = new OpenFileDialog()
                    {
                        AutoUpgradeEnabled = true,
                        CheckFileExists = true,
                        CheckPathExists = true,
                        DefaultExt = "csv",
                        Filter = "LastPass CSV Export (*.csv)|*.csv",
                        InitialDirectory = KnownFolders.GetPath(KnownFolder.Downloads),
                        Title = "Select LastPass CSV export",
                    };
                    loader.ShowDialog();

                    csvPath = loader.FileName;
                    return Validate(csvPath);
                }))
            {
                return;
            }

            if (!GenerateFromExport(csvPath, out lastPassEntries))
            {
                throw new ConverterException("Unable to load LastPass export!",
                    "The converter was unable to import the selected LastPass export. Make sure the selected file is a valid CSV export from the latest version of the LastPass extension and try again!");
            }

            string onePasswordPath = null;
            if (!Retry("Read-only file selected!",
                "A read-only file has been selected and the 1Password Converter cannot proceed. Please select a different path to continue.",
                () =>
                {
                    var saver = new SaveFileDialog()
                    {
                        AddExtension = true,
                        AutoUpgradeEnabled = true,
                        DefaultExt = "1pif",
                        Filter = "1Password Import File (*.1pif)|*.1pif",
                        CheckPathExists = true,
                        FileName = $"LastPass Export {DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd")}.1pif",
                        OverwritePrompt = true,
                        Title = "Select 1Password import file path",
                    };
                    saver.ShowDialog();
                    onePasswordPath = saver.FileName;

                    if (File.Exists(onePasswordPath))
                    {
                        return !File.GetAttributes(onePasswordPath).HasFlag(FileAttributes.ReadOnly);
                    }
                    return true;
                }))
            {
                return;
            }

            string lastPassPath = null;
            if (!SaveAs1Password(onePasswordPath, lastPassEntries, out lastPassPath))
            {
                throw new ConverterException("Unable to create 1Password import file!",
                    "The converter was unable to create a valid 1Password import file from your backup! Please report this error to the developers.");
            }

            CompleteActions(csvPath, lastPassPath);
        }

        private bool Validate(string csvPath)
        {
            if (!File.Exists(csvPath))
            {
                throw new ConverterException("Unable to locate selected LastPass export file!");
            }

            using (var parser = new TextFieldParser(csvPath))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");

                while (!parser.EndOfData)
                {
                    try
                    {
                        var fields = parser.ReadFields();
                        if (fields.Length != 7)
                        {
                            //invalid/unrecognized LastPass format
                            return false;
                        }
                    }
                    catch (MalformedLineException ex)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private bool GenerateFromExport(string csvPath, out List<PasswordEntry> entries)
        {
            entries = new List<PasswordEntry>();

            using (var parser = new TextFieldParser(csvPath))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");

                while (!parser.EndOfData)
                {
                    try
                    {
                        var fields = parser.ReadFields();
                        var entry = new PasswordEntry();

                        if (!Uri.TryCreate(fields[0], UriKind.Absolute, out var uri)
                            || !uri.IsAbsoluteUri || (uri.Scheme.ToLower() != "http" && uri.Scheme.ToLower() != "https") || string.IsNullOrWhiteSpace(uri.Host))
                        {
                            //invalid record
                            continue;
                        }

                        entry.Url = uri;
                        entry.UserName = fields[1];
                        entry.Password = fields[2];
                        if (string.IsNullOrWhiteSpace(entry.Password))
                        {
                            continue;
                        }

                        entry.Note = fields[3];
                        entry.Title = string.IsNullOrWhiteSpace(fields[4]) ? uri.Host : fields[4];
                        entry.Group = fields[5];
                        entry.Favorite = fields[6] == "1";

                        entries.Add(entry);
                    }
                    catch (MalformedLineException ex)
                    {
                        //we should have already validated the content, so this should never happen
                        Debug.Assert(false);
                    }
                }

                return entries.Count > 0;
            }
        }

        private dynamic Generate1PasswordEntry(string keyId, PasswordEntry entry)
        {
            return new
            {
                //keyId,
                locationKey = entry.Url.Host,
                typeName = "webforms.WebForm",
                location = entry.Url.ToString(),
                uuid = Guid.NewGuid().ToString("N"),
                updatedAt = DateTime.UtcNow.ToUnixTimeMilliseconds(),
                createdAt = DateTime.UtcNow.ToUnixTimeMilliseconds(),
                title = entry.Title,
                securityLevel = "SL5",
                secureContents = new
                {
                    fields = new[]
                    {
                        new
                        {
                            type = "T",
                            //name = "username",
                            value = entry.UserName,
                            designation = "username"
                        },
                        new
                        {
                            type = "P",
                            //name = "password",
                            value = entry.Password,
                            designation = "password",
                        }
                    }
                },
                notesPlain = entry.Note
            };
        }

        private bool SaveAs1Password(string path, List<PasswordEntry> entries, out string lastPassPath)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                lastPassPath = null;
                return false;
            }

            var keyId = Guid.NewGuid().ToString("N");

            using (var outFile = File.Create(path))
            using (var writer = new StreamWriter(outFile, Encoding.UTF8))
            {
                foreach (var entry in entries)
                {
                    var onePasswordEntry = Generate1PasswordEntry(keyId, entry);
                    var serialized = Newtonsoft.Json.JsonConvert.SerializeObject(onePasswordEntry);
                    writer.WriteLine(serialized);
                    //this seems to be a separator between each JSON record
                    writer.WriteLine("***5642bee8-a5ff-11dc-8314-0800200c9a66***");
                }
            }

            lastPassPath = path;
            return true;
        }

        private bool CompleteActions(string csvPath, string onePasswordFile)
        {
            var taskDialog = new TaskDialog()
            {
                Text = "A 1Password import file has been successfully generated from the selected LastPass export file. Here are some options to get you on your way:",
                Caption = "Conversion completed successfully!",
                StandardButtons = TaskDialogStandardButtons.Close
            };
            var action1 = new TaskDialogCommandLink("launch1Password", "Launch 1Password\nLaunches 1Password (must be installed locally) for importing the newly-created file.");
            action1.Click += (s, e) => Launch1Password();
            taskDialog.Controls.Add(action1);

            var action2 = new TaskDialogCommandLink("shredCsv", "Securely delete LastPass export\nSecurely delete the original LastPass export, overwriting then deleting the CSV file.");
            action2.Click += (s, e) => Shred(csvPath);
            taskDialog.Controls.Add(action2);

            var action3 = new TaskDialogCommandLink("shred1Password", "Securely delete 1Password import file\nSecurely delete the generated 1Password file, overwriting then deleting the 1PIF file.");
            action3.Click += (s, e) => Shred(onePasswordFile);
            taskDialog.Controls.Add(action3);

            while (true)
            {
                var result = taskDialog.Show();
                if (result == TaskDialogResult.Close)
                {
                    return true;
                }
            }
        }

        private bool Launch1Password()
        {
            var unexpanded = @"%localappdata%\1password\app\";
            var expanded = Environment.ExpandEnvironmentVariables(unexpanded);
            var subdirectories = Directory.GetFileSystemEntries(expanded);

            bool Show1PasswordNotFound()
            {
                MessageBox.Show("Cannot find a local installation of the 1Password app for Windows; 1Password cannot be automatically launched.", "Cannot find 1Password installation!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            string MakeExePath(int version)
            {
                return Path.Combine(expanded, version.ToString(), "AgileBits.OnePassword.Desktop.exe");
            }

            if (!Directory.Exists(expanded))
            {
                return Show1PasswordNotFound();
            }

            var versions = subdirectories
                .Where(s => Directory.Exists(s))
                .Select(s => Path.GetFileName(s))
                .Where(s => int.TryParse(s, out var unused)).Select(s => int.Parse(s));
            var validVersions = versions.Where(v => File.Exists(MakeExePath(v)));
            if (!validVersions.Any())
            {
                return Show1PasswordNotFound();
            }

            var maxVersion = validVersions.Max();
            var exePath = MakeExePath(maxVersion);

            try
            {
                var process = new Process()
                {
                    StartInfo = new ProcessStartInfo(exePath)
                };
                return process.Start();
            }
            catch
            {
                return false;
            }
        }

        private bool Shred(string path)
        {
            bool DeleteError()
            {
                MessageBox.Show($"Unable to securely delete the file {path}!\r\nMake sure to manually delete the file!", "Unable to securely delete file", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (!File.Exists(path))
            {
                return DeleteError();
            }

            //overwrite the existing file w/ the same number of blocks (not bytes)
            //typical filesystem block size is 4kb, so at most we are writing (4kb - 1 byte) extra
            //I just feel this is safer than writing exactly the number of bytes
            var random = new Random((int) DateTime.UtcNow.Ticks);
            var fileLength = new FileInfo(path).Length;
            using (var fileStream = new FileStream(path, FileMode.Open))
            {
                var buffer = new byte[512];               
                for (var remaining = fileLength; remaining > 0; )
                {
                    random.NextBytes(buffer);
                    fileStream.Write(buffer, 0, buffer.Length);
                    remaining -= buffer.Length;
                }
            }

            try
            {
                File.Delete(path);
            }
            catch
            {
                return DeleteError();
            }

            MessageBox.Show($"File \"{Path.GetFullPath(path)}\" has been successfully shredded.", "File shredded successfully!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }
    }
}
