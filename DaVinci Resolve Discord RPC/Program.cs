using System;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Text.Json;
using DiscordRPC;
using DiscordRPC.Logging;

class Program
{
    private static DiscordRpcClient client = null!;
    private const string CONFIG_FILE = "config.json";
    private static DateTime lastUpdateTime = DateTime.UtcNow;
    private static TimeSpan presenceDuration = TimeSpan.Zero;

    static void Main(string[] args)
    {
        string discordAppId = GetDiscordAppId() ?? throw new InvalidOperationException("DiscordAppId cannot be null.");
        InitializeDiscordClient(discordAppId);

        while (true)
        {
            if (IsDaVinciResolveRunning())
            {
                string projectName = GetDaVinciResolveProjectName();

                // Update presence duration
                UpdatePresenceDuration();

                // Debugging: Log the project name and duration
                Console.WriteLine($"Current project name: {projectName}");
                Console.WriteLine($"Presence duration: {presenceDuration}");

                UpdatePresence(projectName);
            }
            else
            {
                Console.WriteLine("DaVinci Resolve is not running.");
                client.ClearPresence();
                presenceDuration = TimeSpan.Zero; // Reset duration when not running
            }

            Thread.Sleep(15000); // Check every 15 seconds
        }
    }

    static string? GetDiscordAppId()
    {
        if (File.Exists(CONFIG_FILE))
        {
            string json = File.ReadAllText(CONFIG_FILE);
            var config = JsonSerializer.Deserialize<Config>(json);

            // Ensure the config and DiscordAppId are not null
            return config?.DiscordAppId ?? throw new InvalidOperationException("DiscordAppId cannot be null.");
        }
        else
        {
            Console.Write("Enter your Discord Application ID: ");
            return Console.ReadLine() ?? throw new InvalidOperationException("DiscordAppId cannot be null.");
        }
    }

    static void InitializeDiscordClient(string discordAppId)
    {
        client = new DiscordRpcClient(discordAppId);

        client.Logger = new ConsoleLogger() { Level = LogLevel.Warning };

        client.OnReady += (sender, e) =>
        {
            Console.WriteLine("Discord RPC Client ready");
        };

        client.OnPresenceUpdate += (sender, e) =>
        {
            Console.WriteLine("Presence has been updated!");
        };

        client.Initialize();
    }

    static bool IsDaVinciResolveRunning()
    {
        Process[] processes = Process.GetProcessesByName("Resolve");
        return processes.Length > 0;
    }

    static string GetDaVinciResolveProjectName()
    {
        try
        {
            // Get all processes with the name "Resolve"
            Process[] processes = Process.GetProcessesByName("Resolve");

            foreach (Process process in processes)
            {
                // Get the main window title
                string windowTitle = process.MainWindowTitle;

                // Check if the window title contains "DaVinci Resolve"
                if (windowTitle.Contains("DaVinci Resolve"))
                {
                    // Extract the project name from the title
                    // Assuming format: "DaVinci Resolve - [Project Name]"
                    int dashIndex = windowTitle.IndexOf(" - ");
                    if (dashIndex >= 0)
                    {
                        return windowTitle.Substring(dashIndex + 3); // +3 to skip " - "
                    }
                }
            }

            return "Unknown Project";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting project name: {ex.Message}");
            return "Unknown Project";
        }
    }

    static void UpdatePresenceDuration()
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

    static void UpdatePresence(string projectName)
    {
        client.SetPresence(new RichPresence()
        {
            Details = $"Editing: {projectName}",
            State = "Using DaVinci Resolve",
            Assets = new Assets()
            {
                LargeImageKey = "davinci_resolve_logo", // Ensure this matches exactly with your uploaded image key
                LargeImageText = "DaVinci Resolve"
            },
            Timestamps = new Timestamps()
            {
                Start = DateTime.UtcNow - presenceDuration // Correctly set start time based on duration
            }
        });
    }
}

class Config
{
    public string? DiscordAppId { get; set; } // Updated to be nullable
}
