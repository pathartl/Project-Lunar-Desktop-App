using LunarLib.Models;
using LunarLib.Services;
using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LunarLib.Services
{
    public class MegaDriveMiniService : IDisposable
    {
        private bool Disposed = false;

        private string IpAddress { get; set; }
        private string Username { get; set; }
        private string Password { get; set; }

        private SshClient SshClient { get; set; }
        private ScpClient ScpClient { get; set; }

        CancellationTokenSource CheckModCancellationToken = new CancellationTokenSource();
        CancellationTokenSource InstallCancellationToken = new CancellationTokenSource();

        public MegaDriveMiniService()
        {
            IpAddress = ConfigurationManager.AppSettings["MegaDriveMiniIpAddress"];
            Username = ConfigurationManager.AppSettings["MegaDriveMiniRootUsername"];
            Password = ConfigurationManager.AppSettings["MegaDriveMiniRootPassword"];

            SshClient = new SshClient(IpAddress, Username, Password);
            ScpClient = new ScpClient(IpAddress, Username, Password);
        }

        private void OnUpdateStatus(string message)
        {
            var args = new UpdateStatusEventArgs()
            {
                Message = message
            };

            OnUpdateStatus(args);
        }

        public void OnUpdateStatus(UpdateStatusEventArgs e)
        {
            EventHandler<UpdateStatusEventArgs> handler = UpdateStatus;
            handler?.Invoke(this, e);
        }

        public event EventHandler<UpdateStatusEventArgs> UpdateStatus;

        private void Connect()
        {
            if (!ScpClient.IsConnected) ScpClient.Connect();
            if (!SshClient.IsConnected) SshClient.Connect();
        }

        private void Shell_DataReceived(object sender, ShellDataEventArgs e)
        {
            Debug.Write(Encoding.Default.GetString(e.Data));
        }

        public void RestartWithUpdateMessage()
        {
            SshClient.RunCommand("killall sdl_display &> \"/dev/null\"");
            SshClient.RunCommand("sdl_display /opt/project_lunar/etc/project_lunar/IMG/PL_UpdateDone.png &");

            // UpdateStatus("Restarting console");

            Thread.Sleep(5000);

            var shell = SshClient.CreateShellStream("Lunar", 120, 9999, 120, 9999, 65536);
            shell.DataReceived += Shell_DataReceived;
            shell.WriteLine($"cd /");

            Thread.Sleep(500);

            shell.WriteLine($"kill_ui_programs");

            Thread.Sleep(500);

            shell.WriteLine($"restart");

            /* while (ssh.IsConnected)
            {
                if (installCts.IsCancellationRequested)
                {
                    installCts.Dispose();
                    return;
                }
                Thread.Sleep(500);
            } */

            Debug.WriteLine("Finished wait.");

            // UpdateStatus("Waiting for console");
        }

        public void AddMods(IEnumerable<string> fileNames)
        {
            Connect();

            foreach (string filePath in fileNames)
            {
                try
                {
                    var md5 = Utilities.Md5Hash(filePath);
                    var fileName = Path.GetFileName(filePath);
                    var parsedFilename = fileName.Replace("_SEGAMD", "").Replace(".mod", "").ToUpper();

                    ScpClient.Upload(File.OpenRead(filePath), $"/tmp/{fileName}");

                    var md5CommandText = $"cd /tmp;[ \"$(md5sum {fileName})\" = " +
                                  $"\"$(echo $'{md5}  {fileName}')\" ] " +
                                  "&& echo \"File integrity OK\" || " +
                                  "echo \"File intergrity FAIL\"";

                    var result = SshClient.RunCommand(md5CommandText).Result;

                    if (result.Contains("File integrity OK"))
                    {
                        OnUpdateStatus($"Installing {fileName}");

                        result = SshClient.RunCommand($"cd /tmp ; mod-install {fileName}").Result;

                        if (result.Contains("[PROJECT LUNAR](ERROR)"))
                        {
                            result = result.Replace("[PROJECT LUNAR](ERROR)", "");
                            throw new InvalidDataException(parsedFilename + "'\n\rFailed to install!\n\rReason: " + result);
                        }
                    }
                    else
                    {
                        throw new InvalidDataException(parsedFilename + "'\n\rFailed to install!\n\rReason: Mod has corrupted in transit. Please try again.");
                    }
                }
                catch (InvalidDataException ex)
                {
                    throw ex;
                }
            }
        }

        public IEnumerable<Mod> GetInstalledMods()
        {
            List<Mod> mods = new List<Mod>();

            EnsureSshConnected();

            if (!Disposed)
            {
                var result = SshClient.RunCommand("mod-list").Result;

                using (var reader = new StringReader(result))
                {
                    // Skip first two lines
                    reader.ReadLine();
                    reader.ReadLine();

                    var line = String.Empty;

                    do
                    {
                        line = reader.ReadLine();

                        if (line != null && !String.IsNullOrWhiteSpace(line))
                        {
                            line.Replace("  ", " ");

                            var values = line.Split(' ');

                            mods.Add(
                                new Mod()
                                {
                                    Name = values[values.Length - 2],
                                    Version = values[values.Length - 1]
                                }
                            );
                        }
                    }
                    while (line != null);
                }
            }

            return mods;
        }

        public void RemoveMods(IEnumerable<string> modNames)
        {
            Connect();

            foreach (var modName in modNames)
            {
                // Not doing anything with the catch? Ok...
                try
                {
                    OnUpdateStatus($"Uninstalling {modName}");

                    SshClient.RunCommand($"mod-remove {modName}");
                }
                catch { }
            }

            RestartWithUpdateMessage();
        }

        private void EnsureSshConnected()
        {
            while (!SshClient.IsConnected)
            {
                try
                {
                    SshClient.Connect();
                }
                catch
                {
                    if (CheckModCancellationToken.IsCancellationRequested)
                    {
                        CheckModCancellationToken.Dispose();
                        Dispose();
                        return;
                    }

                    Thread.Sleep(500);
                }
            }
        }

        public void Dispose()
        {
            Disposed = true;

            if (SshClient.IsConnected) SshClient.Disconnect();
            if (ScpClient.IsConnected) ScpClient.Disconnect();

            SshClient.Dispose();
            ScpClient.Dispose();
            CheckModCancellationToken.Dispose();
            InstallCancellationToken.Dispose();
        }
    }
}
