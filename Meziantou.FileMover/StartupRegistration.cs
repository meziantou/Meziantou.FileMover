using System.Security;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Meziantou.FileMover;

internal static class StartupRegistration
{
    private const string WindowsRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string WindowsRunValueName = "Meziantou_FileMover";
    private const string MacOSLaunchAgentLabel = "com.meziantou.filemover";

    internal static void RegisterAsStartup()
    {
        if (Environment.ProcessPath is not { } processPath)
            return;

        if (OperatingSystem.IsWindows())
        {
            RegisterWindows(processPath);
        }
        else if (OperatingSystem.IsMacOS())
        {
            RegisterMacOS(processPath);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterWindows(string processPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(WindowsRunKeyPath, writable: true);
        key?.SetValue(WindowsRunValueName, processPath);
    }

    [SupportedOSPlatform("macos")]
    private static void RegisterMacOS(string processPath)
    {
        var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(userProfilePath))
            return;

        var launchAgentPath = GetMacOSLaunchAgentPath(userProfilePath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(launchAgentPath)!);

        var content = CreateMacOSLaunchAgentPlistContent(processPath);
        if (File.Exists(launchAgentPath))
        {
            var currentContent = File.ReadAllText(launchAgentPath);
            if (string.Equals(currentContent, content, StringComparison.Ordinal))
                return;
        }

        File.WriteAllText(launchAgentPath, content);
    }

    internal static string GetMacOSLaunchAgentPath(string userProfilePath)
    {
        return Path.Combine(userProfilePath, "Library", "LaunchAgents", $"{MacOSLaunchAgentLabel}.plist");
    }

    internal static string CreateMacOSLaunchAgentPlistContent(string processPath)
    {
        var escapedProcessPath = SecurityElement.Escape(processPath) ?? processPath;
        return $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
              <key>Label</key>
              <string>{{MacOSLaunchAgentLabel}}</string>
              <key>ProgramArguments</key>
              <array>
                <string>{{escapedProcessPath}}</string>
              </array>
              <key>RunAtLoad</key>
              <true/>
              <key>ProcessType</key>
              <string>Background</string>
            </dict>
            </plist>
            """;
    }
}
