using System.Text.Json;
await Task.Delay(250);
await File.WriteAllTextAsync(Path.Combine(Environment.CurrentDirectory, "probe-result.json"), JsonSerializer.Serialize(new
{
    cwd = Environment.CurrentDirectory, args, pid = Environment.ProcessId,
    baseDirectory = AppContext.BaseDirectory
}));
