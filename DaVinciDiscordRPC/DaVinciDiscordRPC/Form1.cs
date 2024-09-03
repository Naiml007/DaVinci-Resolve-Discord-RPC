using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using DiscordRPC;
using DiscordRPC.Logging;

namespace DaVinciDiscordRPC
{
    public partial class Form1 : Form
    {
        private DiscordRpcClient client;
        private const string CONFIG_FILE = "config.json";
        private DateTime lastUpdateTime = DateTime.UtcNow;
        private TimeSpan presenceDuration = TimeSpan.Zero;
        private Thread rpcThread = null;
        private bool isRunning = false;

        public Form1()
        {
            InitializeComponent();
            LoadDiscordAppId();
        }

        private void LoadDiscordAppId()
        {
            if (File.Exists(CONFIG_FILE))
            {
                string json = File.ReadAllText(CONFIG_FILE);
                var config = JsonSerializer.Deserialize<Config>(json);
                if (config != null && !string.IsNullOrEmpty(config.DiscordAppId))
                {
                    txtDiscordAppId.Text = config.DiscordAppId;
                }
            }
        }

        private void btnSaveAppId_Click(object sender, EventArgs e)
        {
            SaveDiscordAppId();
        }

        private void SaveDiscordAppId()
        {
            var config = new Config { DiscordAppId = txtDiscordAppId.Text };
            string json = JsonSerializer.Serialize(config);
            File.WriteAllText(CONFIG_FILE, json);
            MessageBox.Show("Discord Application ID saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (isRunning) return;

            if (string.IsNullOrEmpty(txtDiscordAppId.Text))
            {
                MessageBox.Show("Please enter a Discord Application ID first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SaveDiscordAppId();
            rpcThread = new Thread(new ThreadStart(StartRPC));
            rpcThread.IsBackground = true;
            rpcThread.Start();
            isRunning = true;
            UpdateButtonStates();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (!isRunning) return;

            isRunning = false;
            rpcThread?.Join();
            client?.ClearPresence();
            UpdateButtonStates();
        }

        private void btnMinimize_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void StartRPC()
        {
            string discordAppId = txtDiscordAppId.Text;
            if (string.IsNullOrEmpty(discordAppId))
            {
                MessageBox.Show("Discord Application ID is not set.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            InitializeDiscordClient(discordAppId);

            while (isRunning)
            {
                if (IsDaVinciResolveRunning())
                {
                    string projectName = GetDaVinciResolveProjectName();
                    UpdatePresenceDuration();
                    UpdatePresence(projectName);
                }
                else
                {
                    client?.ClearPresence();
                    presenceDuration = TimeSpan.Zero;
                }

                Thread.Sleep(15000);
            }
        }

        private void InitializeDiscordClient(string discordAppId)
        {
            client = new DiscordRpcClient(discordAppId);
            client.Logger = new ConsoleLogger() { Level = LogLevel.Warning };

            client.OnReady += (sender, e) =>
            {
                SetStatus("Discord RPC Client ready");
            };

            client.OnPresenceUpdate += (sender, e) =>
            {
                SetStatus("Presence has been updated!");
            };

            client.Initialize();
        }

        private bool IsDaVinciResolveRunning()
        {
            Process[] processes = Process.GetProcessesByName("Resolve");
            return processes.Length > 0;
        }

        private string GetDaVinciResolveProjectName()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("Resolve");
                foreach (Process process in processes)
                {
                    string windowTitle = process.MainWindowTitle;
                    if (windowTitle.Contains("DaVinci Resolve"))
                    {
                        int dashIndex = windowTitle.IndexOf(" - ");
                        if (dashIndex >= 0)
                        {
                            return windowTitle.Substring(dashIndex + 3);
                        }
                    }
                }
                return "Unknown Project";
            }
            catch (Exception ex)
            {
                SetStatus($"Error getting project name: {ex.Message}");
                return "Unknown Project";
            }
        }

        private void UpdatePresenceDuration()
        {
            if (IsDaVinciResolveRunning())
            {
                presenceDuration = DateTime.UtcNow - lastUpdateTime;
            }
            else
            {
                lastUpdateTime = DateTime.UtcNow;
            }
        }

        private void UpdatePresence(string projectName)
        {
            client.SetPresence(new RichPresence()
            {
                Details = $"Editing: {projectName}",
                State = "Using DaVinci Resolve",
                Assets = new Assets()
                {
                    LargeImageKey = "davinci_resolve_logo",
                    LargeImageText = "DaVinci Resolve"
                },
                Timestamps = new Timestamps()
                {
                    Start = DateTime.UtcNow - presenceDuration
                }
            });
        }

        private void UpdateButtonStates()
        {
            btnStart.Enabled = !isRunning;
            btnStop.Enabled = isRunning;
        }

        private void SetStatus(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(SetStatus), message);
                return;
            }
            txtStatus.AppendText(message + Environment.NewLine);
        }
    }

    class Config
    {
        public string DiscordAppId { get; set; }
    }
}