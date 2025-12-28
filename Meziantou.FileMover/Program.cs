#pragma warning disable MA0004 // Use Task.ConfigureAwait
#pragma warning disable MA0047 // Declare types in namespaces
#pragma warning disable MA0048 // File name must match type name

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace Meziantou.FileMover;
public static class Program
{
    public static async Task Main(string[] args)
    {
        RegisterAsStartup();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            cts.Cancel();
            e.Cancel = true;
        };

        await MainCore(args, cts.Token);

        static void RegisterAsStartup()
        {
            if (Environment.ProcessPath is { } processPath)
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
                key.SetValue("Meziantou_FileMover", processPath);
            }
        }
    }

    public static async Task MainCore(string[] args, CancellationToken cancellationToken)
    {
        var configurationFilePath = args.Length > 0 ? args[0] : Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "Meziantou.FileMover.json");
        if (!File.Exists(configurationFilePath))
        {
            Console.WriteLine($"Configuration file '{configurationFilePath}' does not exist.");
            return;
        }

        var configurationFileContent = await File.ReadAllTextAsync(configurationFilePath, cancellationToken);
        var configuration = JsonSerializer.Deserialize(configurationFileContent, SourceGenerationContext.Default.Configuration);
        if (configuration?.Rules is null)
        {
            Console.WriteLine($"Cannot deserialize the configuration file '{configurationFilePath}'.");
            return;
        }

        var watchers = new List<FileSystemWatcher>();
        foreach (var rule in configuration.Rules)
        {
            var source = Environment.ExpandEnvironmentVariables(rule.Source);

            // Move newly created files
            var watcher = new FileSystemWatcher(source, rule.Pattern);
            watchers.Add(watcher);
            watcher.Created += (sender, e) =>
            {
                _ = ProcessRule(rule, e.FullPath, cancellationToken);
            };

            watcher.Renamed += (sender, e) =>
            {
                _ = ProcessRule(rule, e.FullPath, cancellationToken);
            };

            watcher.EnableRaisingEvents = true;

            await MoveExistingFiles();

            // Add a safety-net and move files every hour
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromHours(1), cancellationToken);
                        try
                        {
                            await MoveExistingFiles();
                        }
                        catch
                        {
                            // Skip errors
                        }
                    }
                }
                catch
                {
                }
            }, cancellationToken);

            // Move existing files
            async Task MoveExistingFiles()
            {
                foreach (var file in Directory.GetFiles(source, rule.Pattern))
                {
                    await ProcessRule(rule, file, cancellationToken);
                }
            }

            async static Task ProcessRule(Rule rule, string filePath, CancellationToken cancellationToken)
            {
                if (rule.Action is FileAction.Delete)
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch
                    {
                    }
                }
                else
                {
                    await MoveFile(rule, filePath, skipDelay: false, cancellationToken);
                }
            }

            static async Task MoveFile(Rule rule, string filePath, bool skipDelay = false, CancellationToken cancellationToken = default)
            {
                try
                {
                    if (rule.Delay > TimeSpan.Zero && !skipDelay)
                        await Task.Delay(rule.Delay, cancellationToken);

                    var destination = Path.Combine(Environment.ExpandEnvironmentVariables(rule.Destination), Path.GetFileName(filePath));
                    _ = Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Move(filePath, destination, overwrite: false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Cannot move the file '{filePath}': {ex}");
                }
            }
        }

        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}

internal sealed record Rule(FileAction Action, string Source, string Destination, string Pattern, TimeSpan Delay);

internal sealed record Configuration(Rule[] Rules);

internal enum FileAction
{
    Move,
    Delete,
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(Configuration))]
internal sealed partial class SourceGenerationContext : JsonSerializerContext
{
}