using System.Diagnostics;
using System.IO;

namespace MeowTools;

public record ToolEntry(string FullPath, string RelativePath, string Name, string Version, string Kind);
public record CatalogResult(IReadOnlyList<ToolEntry> Tools, IReadOnlyList<string> Warnings, bool Missing);

public static class ToolCatalog
{
    public static string FolderAt(string applicationDirectory) => Path.Combine(Path.GetFullPath(applicationDirectory), "tools");
    public static bool Supported(string path) => Path.GetExtension(path).ToLowerInvariant() is ".exe" or ".lnk";
    public static bool IsWithin(string path, string directory) => Path.GetFullPath(path).StartsWith(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    public static CatalogResult Read(string toolsDirectory, CancellationToken token = default)
    {
        string root = Path.GetFullPath(toolsDirectory);
        var entries = new List<ToolEntry>(); var warnings = new List<string>();
        if (!Directory.Exists(root)) return new(entries, warnings, true);
        var directories = new Stack<string>(); directories.Push(root); int visited = 0;
        while (directories.TryPop(out string? dir))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                // Do not traverse directory links or junctions outside the tool collection.
                if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) { warnings.Add("已跳过目录链接：" + Path.GetRelativePath(root, dir)); continue; }
                foreach (string path in Directory.EnumerateFileSystemEntries(dir))
                {
                    token.ThrowIfCancellationRequested();
                    if (++visited > 10000) { warnings.Add("文件数量过多，本次最多检查 10000 项。"); return Finish(); }
                    try
                    {
                        var attributes = File.GetAttributes(path);
                        if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                        if ((attributes & FileAttributes.Directory) != 0) { directories.Push(path); continue; }
                        if (!Supported(path)) continue;
                        string name = Path.GetFileNameWithoutExtension(path), version = "";
                        bool shortcut = Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase);
                        if (!shortcut)
                        {
                            try
                            {
                                var info = FileVersionInfo.GetVersionInfo(path);
                                if (!string.IsNullOrWhiteSpace(info.ProductName)) name = info.ProductName.Trim();
                                version = info.ProductVersion?.Split('+')[0].Trim() ?? "";
                            }
                            catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException) { }
                        }
                        entries.Add(new(path, Path.GetRelativePath(root, path), name, version, shortcut ? "快捷方式" : "应用程序"));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { warnings.Add("无法读取：" + Path.GetRelativePath(root, path)); }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { warnings.Add("无法读取目录：" + Path.GetRelativePath(root, dir)); }
        }
        return Finish();
        CatalogResult Finish() => new(entries.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray(), warnings, false);
    }

    public static ProcessStartInfo LaunchInfo(ToolEntry entry, string toolsDirectory)
    {
        string full = Path.GetFullPath(entry.FullPath);
        if (!IsWithin(full, toolsDirectory) || !Supported(full) || !File.Exists(full)) throw new IOException("工具已移动或删除，请刷新列表。");
        // Revalidate the path at click time, including directories replaced by links after scanning.
        string root = Path.GetFullPath(toolsDirectory);
        string? part = full;
        while (part != null)
        {
            if ((File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0) throw new IOException("工具路径已变为文件或目录链接，请放入 EXE 或普通快捷方式后刷新。");
            if (Path.TrimEndingDirectorySeparator(part).Equals(Path.TrimEndingDirectorySeparator(root), StringComparison.OrdinalIgnoreCase)) break;
            part = Path.GetDirectoryName(part);
        }
        return new ProcessStartInfo(full) { WorkingDirectory = Path.GetDirectoryName(full)!, UseShellExecute = true };
    }
}
