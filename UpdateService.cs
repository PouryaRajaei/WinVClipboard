using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;

namespace WinVClipboard;

public static class UpdateService
{
    private const string ReleasesApi = "https://api.github.com/repos/PouryaRajaei/WinVClipboard/releases/latest";
    private static readonly string StateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinVClipboard");
    private static readonly string LastCheckPath = Path.Combine(StateFolder, "last-update-check.txt");
    private static bool _checking;

    public static async Task CheckAtStartupAsync(MainWindow owner)
    {
        try
        {
            if (File.Exists(LastCheckPath) && DateTime.TryParse(File.ReadAllText(LastCheckPath), out var last) && DateTime.UtcNow - last.ToUniversalTime() < TimeSpan.FromHours(6)) return;
            Directory.CreateDirectory(StateFolder); File.WriteAllText(LastCheckPath, DateTime.UtcNow.ToString("O"));
            await Task.Delay(3500); await CheckAsync(owner, false);
        }
        catch { }
    }

    public static async Task CheckAsync(MainWindow owner, bool showUpToDate)
    {
        if (_checking) return; _checking = true;
        try
        {
            using var client = new HttpClient(); client.DefaultRequestHeaders.UserAgent.ParseAdd("WinVClipboard/1.5.2");
            var json = await client.GetStringAsync(ReleasesApi);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var tag = root.GetProperty("tag_name").GetString()?.Trim().TrimStart('v') ?? "0.0.0";
            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version();
            if (!Version.TryParse(tag, out var latest) || latest <= current)
            {
                if (showUpToDate) MessageBox.Show(Localizer.T("UpToDate"), "WinVClipboard");
                return;
            }
            var architecture = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";
            string? downloadUrl = null;
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.Contains(architecture, StringComparison.OrdinalIgnoreCase) && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                { downloadUrl = asset.GetProperty("browser_download_url").GetString(); break; }
            }
            if (downloadUrl == null) throw new InvalidOperationException(Localizer.T("UpdateAssetMissing"));
            var answer = MessageBox.Show($"{Localizer.T("UpdateAvailable")}: {tag}\n{Localizer.T("InstallUpdateQuestion")}", "WinVClipboard", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (answer != MessageBoxResult.Yes) return;
            await DownloadAndInstallAsync(owner, client, downloadUrl, tag);
        }
        catch (Exception ex)
        {
            if (showUpToDate) MessageBox.Show($"{Localizer.T("UpdateFailed")}\n{ex.Message}", "WinVClipboard", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { _checking = false; }
    }

    private static async Task DownloadAndInstallAsync(MainWindow owner, HttpClient client, string url, string version)
    {
        var updateRoot = Path.Combine(Path.GetTempPath(), "WinVClipboard-update-" + Guid.NewGuid().ToString("N"));
        var zipPath = Path.Combine(updateRoot, "update.zip"); var extracted = Path.Combine(updateRoot, "payload");
        Directory.CreateDirectory(updateRoot);
        using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync();
            await using var destination = File.Create(zipPath); await source.CopyToAsync(destination);
        }
        ZipFile.ExtractToDirectory(zipPath, extracted);
        var newExe = Directory.GetFiles(extracted, "WinVClipboard.exe", SearchOption.AllDirectories).FirstOrDefault();
        if (newExe == null) throw new InvalidDataException(Localizer.T("UpdateAssetMissing"));
        var payload = Path.GetDirectoryName(newExe)!; var currentExe = Environment.ProcessPath ?? throw new InvalidOperationException();
        var installFolder = Path.GetDirectoryName(currentExe)!; var scriptPath = Path.Combine(StateFolder, "apply-update.ps1");
        static string Q(string value) => value.Replace("'", "''");
        var script = $$"""
$ErrorActionPreference = 'Stop'
$processId = {{Environment.ProcessId}}
$source = '{{Q(payload)}}'
$destination = '{{Q(installFolder)}}'
$executable = '{{Q(Path.GetFileName(currentExe))}}'
try { Wait-Process -Id $processId -Timeout 30 -ErrorAction SilentlyContinue } catch { }
for ($attempt = 0; $attempt -lt 20; $attempt++) {
  try { Copy-Item -Path (Join-Path $source '*') -Destination $destination -Recurse -Force -ErrorAction Stop; break }
  catch { Start-Sleep -Milliseconds 400; if ($attempt -eq 19) { throw } }
}
Start-Process -FilePath (Join-Path $destination $executable)
Remove-Item -LiteralPath '{{Q(updateRoot)}}' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue
""";
        Directory.CreateDirectory(StateFolder); File.WriteAllText(scriptPath, script);
        Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"") { UseShellExecute = false, CreateNoWindow = true });
        owner.ExitForUpdate();
    }
}
