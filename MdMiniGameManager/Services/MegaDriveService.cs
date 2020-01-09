using Renci.SshNet;
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

namespace ProjectLunarUI.Services
{
    public class MegaDriveService : IDisposable
    {
        private string IpAddress { get; set; }
        private string Username { get; set; }
        private string Password { get; set; }

        private SshClient SshClient { get; set; }
        private ScpClient ScpClient { get; set; }

        public event EventHandler UpdateStatus;

        public MegaDriveService()
        {
            IpAddress = ConfigurationManager.AppSettings["SystemIpAddress"];
            Username = ConfigurationManager.AppSettings["SystemRootUsername"];
            Password = ConfigurationManager.AppSettings["SystemRootPassword"];

            SshClient = new SshClient(IpAddress, Username, Password);
            ScpClient = new ScpClient(IpAddress, Username, Password);
        }

        protected virtual void OnUpdateStatus(EventArgs e)
        {
            EventHandler handler = UpdateStatus;
            handler?.Invoke(this, e);
        }

        private string MD5Hash(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            {
                return MD5Hash(stream);
            }
        }

        private string MD5Hash(Stream input)
        {
            MD5 md5Hash = MD5.Create();
            byte[] data = md5Hash.ComputeHash(input);

            StringBuilder sBuilder = new StringBuilder();

            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            return sBuilder.ToString();
        }

        private void Connect()
        {
            if (!ScpClient.IsConnected) ScpClient.Connect();
            if (!SshClient.IsConnected) SshClient.Connect();
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
            foreach (string filePath in fileNames)
            {
                try
                {
                    var md5 = MD5Hash(filePath);
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
                        // UpdateStatus("Installing " + fileName);

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

        public void Dispose()
        {
            if (SshClient.IsConnected) SshClient.Disconnect();
            if (ScpClient.IsConnected) ScpClient.Disconnect();

            SshClient.Dispose();
            ScpClient.Dispose();
        }
    }
}
