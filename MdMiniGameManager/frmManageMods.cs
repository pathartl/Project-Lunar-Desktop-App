﻿using DarkUI.Forms;
using LunarLib.Services;
using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProjectLunarUI
{
    public partial class frmManageMods : DarkForm
    {
        private int selectedNumberOfMods = 0;
        frmLoading loadingForm = new frmLoading("");
        Task checkModTask;
        Task installerTask;
        CancellationTokenSource chkModCts = new CancellationTokenSource();
        CancellationTokenSource installCts = new CancellationTokenSource();

        public frmManageMods()
        {
            InitializeComponent();
        }

        private void frmManageMods_Shown(object sender, EventArgs e)
        {
            toolStripStatusLabel.Alignment = ToolStripItemAlignment.Right;
            UpdateStatus("Checking for installed mods");
            //Replace with spinner
            LockButtons(true);
            this.Cursor = Cursors.WaitCursor;
            checkModTask = Task.Run(() => GetInstalledMods(), chkModCts.Token);
            this.Cursor = Cursors.Default;
            UpdateStatus("Ready");
        }

        private void LockButtons(bool locked)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    cmdAddNew.Enabled = !locked;
                    cmdRemove.Enabled = !locked;
                });
            }
            else
            {
                cmdAddNew.Enabled = !locked;
                cmdRemove.Enabled = !locked;
            }
        }

        private void GetInstalledMods()
        {
            ShowLoadingBox("CHECKING MODS STATUS");

            using (var service = new MegaDriveMiniService())
            {
                var mods = service.GetInstalledMods().ToList();

                if (mods.Count > 0)
                {
                    if (noModsPanel.InvokeRequired)
                    {
                        noModsPanel.Invoke((MethodInvoker)delegate
                        {
                            noModsPanel.Visible = false;
                        });
                    }
                    else
                    {
                        noModsPanel.Visible = false;
                    }
                }
                else
                {
                    noModsPanel.Visible = true;
                }

                if (grdMods.InvokeRequired)
                {
                    grdMods.Invoke((MethodInvoker)delegate
                    {
                        grdMods.DataSource = mods;
                    });
                }
                else
                {
                    grdMods.DataSource = mods;
                }
            }
            
            if (grdMods.Rows.Count.Equals(0))
            {
                if (noModsPanel.InvokeRequired)
                {
                    noModsPanel.Invoke((MethodInvoker)delegate
                    {
                        noModsPanel.Visible = true;
                    });
                }
                else
                {
                    noModsPanel.Visible = true;
                }
            }

            CloseLoadingBox();

            UpdateStatus("Ready");

            LockButtons(false);
        }

        private void cmdAddNew_Click(object sender, EventArgs e)
        {
            openFileDialog.Multiselect = true;
            openFileDialog.Title = "Select mod file(s)...";
            openFileDialog.Filter = "Mod Packages|*.mod";
            openFileDialog.FileName = "*.mod";

            if (openFileDialog.ShowDialog().Equals(DialogResult.Cancel))
            {
                return;
            }

            LockButtons(true);
            installerTask = Task.Run(() => AddMods(openFileDialog.FileNames), installCts.Token);
        }

        private void AddMods(string[] fileNames)
        {
            ShowLoadingBox("INSTALLING MOD(S)");

            using (var service = new MegaDriveMiniService())
            {
                service.UpdateStatus += UpdateStatus;
                service.AddMods(fileNames);
                service.RestartWithUpdateMessage();

                UpdateStatus("Waiting for console");
                ShowLoadingBox("RESTARTING CONSOLE");

                checkModTask = Task.Run(() => GetInstalledMods());
            }
            //SwingMessageBox.Show("Mods installed successfuly.", "Mod Install", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Shell_DataReceived(object sender, ShellDataEventArgs e)
        {
            Debug.Write(Encoding.Default.GetString(e.Data));
        }

        private void cmdRemove_Click(object sender, EventArgs e)
        {
            selectedNumberOfMods = 0;
            List<string> modList = new List<string>();
            foreach (DataGridViewRow gridRow in grdMods.SelectedRows)
            {
                modList.Add(gridRow.Cells[0].Value.ToString());
                selectedNumberOfMods++;
            }

            if (selectedNumberOfMods == 0)
            {
                SwingMessageBox.Show("You haven't selected any mods to uninstall!", "Mod Uninistall", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (SwingMessageBox.Show($"Do you really want to remove {string.Join(", ", modList.ToArray())}?", "Remove Mod",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question).Equals(DialogResult.No))
            {
                return;
            }

            LockButtons(true);

            installerTask = Task.Run(() => RemoveSelectedMods(), installCts.Token);
            // Below message Will be replaced by spinner
        }

        private void RemoveSelectedMods()
        {
            ShowLoadingBox("REMOVING MOD(S)");

            using (var service = new MegaDriveMiniService())
            {
                var names = new List<string>();

                foreach (DataGridViewRow row in grdMods.SelectedRows)
                {
                    names.Add(row.Cells[0].Value.ToString());
                }

                service.RemoveMods(names);
            }

            foreach (DataGridViewRow gridRow in grdMods.SelectedRows)
            {
                string modName = gridRow.Cells[0].Value.ToString();
                try
                {
                    using (SshClient ssh = new SshClient("169.254.215.100", "root", "5A7213"))
                    {
                        ssh.Connect();
                        UpdateStatus("Uninstalling " + modName);
                        string result = ssh.RunCommand($"mod-remove {modName}").Result;
                    }
                }
                catch
                {

                }
            }

            UpdateStatus("Waiting for console");

            ShowLoadingBox("RESTARTING CONSOLE");

            //SwingMessageBox.Show("Mods removed successfuly.", "Mod Uninistall", MessageBoxButtons.OK, MessageBoxIcon.Information);
            checkModTask = Task.Run(() => GetInstalledMods());
        }

        private void UpdateStatus(string message)
        {
            toolStripStatusLabel.Text = $"Status: {message}";
            Application.DoEvents();
        }

        private void UpdateStatus(object sender, UpdateStatusEventArgs e)
        {
            UpdateStatus(e.Message);
        }

        private void btnMMCwebsite_Click(object sender, EventArgs e)
        {
            ProcessStartInfo sInfo = new ProcessStartInfo("https://modmyclassic.com/project-lunar-mods");
            Process.Start(sInfo);
        }

        private void ShowLoadingBox(string message)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    loadingForm.status.Text = message;
                    if (!loadingForm.Visible)
                    {
                        loadingForm.Show(this);
                    }
                });
            }
            else
            {
                loadingForm.status.Text = message;
                if (!loadingForm.Visible)
                {
                    loadingForm.Show(this);
                }
            }
        }

        private void CloseLoadingBox()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    loadingForm.Hide();
                });
            }
            else
            {
                loadingForm.Hide();
            }
        }

        private void frmManageMods_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (checkModTask != null)
            {
                if (checkModTask.Status.Equals(TaskStatus.Running))
                {
                    chkModCts.Cancel();
                }
            }
            if (installerTask != null)
            {
                if (installerTask.Status.Equals(TaskStatus.Running))
                {
                    installCts.Cancel();
                }
            }
        }
    }
}
