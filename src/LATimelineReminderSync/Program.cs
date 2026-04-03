using System.Diagnostics;
using LATimelineReminderSync;
using LATimelineReminderSync.Models;
using Serilog;
using Serilog.Events;

// Handle CLI verbs before building the host
if (args.Length > 0)
{
    var verb = args[0].ToLowerInvariant();
    switch (verb)
    {
        case "install":
            RunScExe("create", "LATimelineReminderSync",
                $"binPath= \"{Environment.ProcessPath}\"",
                "start= auto");
            return;

        case "uninstall":
            RunScExe("delete", "LATimelineReminderSync");
            return;

        case "start":
            RunScExe("start", "LATimelineReminderSync");
            return;

        case "stop":
            RunScExe("stop", "LATimelineReminderSync");
            return;

        case "run":
            // Fall through to host builder in console/foreground mode
            break;

        case "merge":
            await RunMergeCommand(args);
            return;

        default:
            Console.Error.WriteLine(
                $"Unknown verb '{verb}'. Valid verbs: install, uninstall, start, stop, run, merge");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Usage for merge:");
            Console.Error.WriteLine("  LATimelineReminderSync merge --file <SavedVariables.lua> --profile <profile.lua> --encounter <id> --difficulty <index>");
            Environment.Exit(1);
            return;
    }
}

// Build the host
var builder = Host.CreateApplicationBuilder(args);

// Bind SyncServiceConfig from the "SyncService" section
var syncConfig = new SyncServiceConfig();
builder.Configuration.GetSection("SyncService").Bind(syncConfig);

// Validate configuration
var configValidator = new ConfigValidator();
var (isValid, errors) = configValidator.Validate(syncConfig);

if (!isValid)
{
    var tempLogger = LoggerFactory.Create(logging => logging.AddConsole())
        .CreateLogger("LATimelineReminderSync");

    foreach (var error in errors)
    {
        tempLogger.LogError("Configuration error: {Error}", error);
    }

    tempLogger.LogError("Exiting due to invalid configuration.");
    Environment.Exit(1);
}

// Configure Serilog
var logLevel = ParseLogLevel(syncConfig.LogLevel);
var logPath = Path.Combine(AppContext.BaseDirectory, "Logs", "trsync-.log");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(logLevel)
    .WriteTo.File(
        logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 31,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Console(
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Services.AddSerilog();

// Configure graceful shutdown within 5 seconds
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(5);
});

// Register config as a singleton for DI
builder.Services.AddSingleton(syncConfig);

// Register core services
builder.Services.AddSingleton<IContentValidator, ContentValidator>();
builder.Services.AddSingleton<IContentHashStore, ContentHashStore>();
builder.Services.AddSingleton<ISavedVariablesWriter>(sp =>
    new SavedVariablesWriter(
        syncConfig.AddonDataFolder,
        syncConfig.ProfileName,
        sp.GetRequiredService<ILogger<SavedVariablesWriter>>()));

// Register HTTP client with timeout
builder.Services.AddHttpClient("RemoteSource", client => {
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register GitHub source directly
builder.Services.AddSingleton<IRemoteSource>(sp =>
    new GitHubSource(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("RemoteSource"),
        syncConfig,
        sp.GetRequiredService<ILogger<GitHubSource>>()));

// Register orchestrator
builder.Services.AddSingleton<ISyncOrchestrator, SyncOrchestrator>();

// Register hosted services
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<WmiProcessWatcher>();

// Use Windows Service hosting unless running in console mode ("run" verb)
var isRunVerb = args.Length > 0
    && string.Equals(args[0], "run", StringComparison.OrdinalIgnoreCase);

if (!isRunVerb)
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "LATimelineReminderSync";
    });
}

var host = builder.Build();

try
{
    Log.Information("LATimelineReminderSync starting up");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "LATimelineReminderSync terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

return;

// --- Helper methods ---

static LogEventLevel ParseLogLevel(string level)
{
    return level?.ToLowerInvariant() switch
    {
        "debug" or "verbose" => LogEventLevel.Debug,
        "information" or "info" => LogEventLevel.Information,
        "warning" or "warn" => LogEventLevel.Warning,
        "error" => LogEventLevel.Error,
        "fatal" => LogEventLevel.Fatal,
        _ => LogEventLevel.Information
    };
}


static async Task RunMergeCommand(string[] args)
{
    // Parse args: merge --file <path> --profile <path> --encounter <id> --difficulty <index>
    string? filePath = null, profilePath = null;
    int encounterId = 0, difficultyIndex = 0;

    for (var i = 1; i < args.Length - 1; i++)
    {
        switch (args[i].ToLowerInvariant())
        {
            case "--file":
            case "-f":
                filePath = args[++i];
                break;
            case "--profile":
            case "-p":
                profilePath = args[++i];
                break;
            case "--encounter":
            case "-e":
                encounterId = int.Parse(args[++i]);
                break;
            case "--difficulty":
            case "-d":
                difficultyIndex = int.Parse(args[++i]);
                break;
        }
    }

    if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(profilePath) || encounterId == 0 || difficultyIndex == 0)
    {
        Console.Error.WriteLine("Missing required arguments.");
        Console.Error.WriteLine("Usage: LATimelineReminderSync merge --file <SavedVariables.lua> --profile <profile.lua> --encounter <id> --difficulty <index>");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Example:");
        Console.Error.WriteLine("  LATimelineReminderSync merge --file LiquidRemindersSaved.lua --profile imperator-mythic.lua --encounter 3176 --difficulty 2");
        Environment.Exit(1);
        return;
    }

    if (!File.Exists(filePath))
    {
        Console.Error.WriteLine($"SavedVariables file not found: {filePath}");
        Environment.Exit(1);
        return;
    }

    if (!File.Exists(profilePath))
    {
        Console.Error.WriteLine($"Profile file not found: {profilePath}");
        Environment.Exit(1);
        return;
    }

    var fileContent = await File.ReadAllTextAsync(filePath);
    var profileContent = await File.ReadAllTextAsync(profilePath);

    var entry = new EncounterEntry
    {
        EncounterId = encounterId,
        EncounterName = $"Encounter {encounterId}",
        DifficultyIndex = difficultyIndex,
        FileName = Path.GetFileName(profilePath)
    };

    Console.WriteLine($"Merging profile into encounter {encounterId}, difficulty {difficultyIndex}...");
    Console.WriteLine($"  SavedVariables: {filePath}");
    Console.WriteLine($"  Profile:        {profilePath}");

    var merged = SavedVariablesWriter.MergeEncounterProfile(fileContent, entry, profileContent);

    // Create backup
    var backupPath = filePath + ".bak";
    File.Copy(filePath, backupPath, overwrite: true);
    Console.WriteLine($"  Backup:         {backupPath}");

    await File.WriteAllTextAsync(filePath, merged);
    Console.WriteLine($"Done! Profile merged successfully.");
    Console.WriteLine($"Reload WoW UI (/reload) to pick up the changes.");
}

static void RunScExe(params string[] scArgs)
{
    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = string.Join(" ", scArgs),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            Console.Error.WriteLine("Failed to start sc.exe");
            Environment.Exit(1);
            return;
        }

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrWhiteSpace(stdout))
            Console.WriteLine(stdout);
        if (!string.IsNullOrWhiteSpace(stderr))
            Console.Error.WriteLine(stderr);

        Environment.Exit(process.ExitCode);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error running sc.exe: {ex.Message}");
        Environment.Exit(1);
    }
}
