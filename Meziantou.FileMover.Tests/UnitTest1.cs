using System.Text.Json;
using Meziantou.Framework;
using Meziantou.Framework.InlineSnapshotTesting;

namespace Meziantou.FileMover.Tests;

public class UnitTest1
{
    [Fact]
    public async Task Test1()
    {
        await using var dir = TemporaryDirectory.Create();
        var path = await dir.CreateTextFileAsync("config.json", $$"""
            {
              "Rules": [
                {
                  "Action": "Move",
                  "Source": {{JsonSerializer.Serialize(dir.FullPath)}},
                  "Destination": {{JsonSerializer.Serialize(dir.FullPath / "dst")}},
                  "Pattern": "*.test"
                },
                {
                  "Action": "Delete",
                  "Source": {{JsonSerializer.Serialize(dir.FullPath)}},
                  "Pattern": "a"
                },
                {
                  "Action": "Delete",
                  "Source": {{JsonSerializer.Serialize(dir.FullPath)}},
                  "Pattern": "b"
                }
              ]
            }
            """);

        using var cts = new CancellationTokenSource();
        var task = Task.Run(() => Program.MainCore([path], cts.Token));

        dir.CreateEmptyFile("a");
        dir.CreateEmptyFile("b");
        dir.CreateEmptyFile("other");
        dir.CreateEmptyFile("test.test");

        await Task.Delay(3000);
        await cts.CancelAsync();
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }

        var files = Directory.GetFiles(dir.FullPath, "*", SearchOption.AllDirectories).Select(FullPath.FromPath).Select(path => path.MakePathRelativeTo(dir.FullPath)).Order(StringComparer.Ordinal);
        InlineSnapshot.Validate(files, """
            - config.json
            - dst\test.test
            - other
            """);
    }
}
