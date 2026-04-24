# Meziantou.FileMover

Watches directories and automatically moves or deletes files based on rules from `Meziantou.FileMover.json`.

## Configuration

The configuration file contains a `Rules` array:

```json
{
  "Rules": [
    {
      "Action": "Move",
      "Source": "%USERPROFILE%\\Downloads",
      "Destination": "Z:\\zips",
      "Pattern": "*.zip",
      "Delay": "00:00:10"
    },
    {
      "Action": "Delete",
      "Source": "%USERPROFILE%\\Downloads",
      "Pattern": "Sample"
    }
  ]
}
```

For macOS, use POSIX paths in `Source` and `Destination` (for example `/Users/your-user/Downloads`).

## Startup registration

The application registers itself at user login on startup:

- **Windows:** `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- **macOS:** `~/Library/LaunchAgents/com.meziantou.filemover.plist`

Windows release builds use `WinExe` when published for Windows runtimes, so no console window is shown at startup.  
On macOS, the LaunchAgent is configured as a background process (`ProcessType=Background`) for equivalent non-interactive startup behavior.

## macOS notes

- Keep the binary in a stable path because the LaunchAgent points to the executable location.
- Startup is user-scoped (LaunchAgent), not system-wide (LaunchDaemon).
- If you distribute binaries, make sure signing/notarization expectations are handled for Gatekeeper.
