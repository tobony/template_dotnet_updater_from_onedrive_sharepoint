using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;

namespace MyApp;

public class UpdateService
{
    private const string BaseUrl = "https://zb5rg-my.sharepoint.com/personal/dev_zb5rg_onmicrosoft_com/_layouts/15/download.aspx?share=";
    private const string UpdateJsonShareId = "IQBo2mgEO12oTpxVCCBwF3jcAa50fA9CiomljfHfsnHWMHg";

    private static readonly CookieContainer Cookies = new();
    private static readonly HttpClientHandler Handler = new()
    {
        AllowAutoRedirect = false,
        CookieContainer = Cookies,
        UseCookies = true
    };
    private static readonly HttpClient Http = new(Handler) { Timeout = TimeSpan.FromMinutes(5) };

    public static string CurrentVersion
    {
        get
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            return asm.GetName().Version?.ToString(3) ?? "0.0.0";
        }
    }

    public static string TempDir => Path.Combine(Path.GetTempPath(), "MyApp_Update");

    private static string BuildUrl(string shareId) => BaseUrl + shareId;

    /// <summary>
    /// SharePoint download.aspx의 리다이렉트 체인을 수동으로 따라가서 최종 응답을 반환
    /// </summary>
    private static async Task<HttpResponseMessage> GetWithRedirectsAsync(string url, HttpCompletionOption option = HttpCompletionOption.ResponseContentRead)
    {
        const int maxRedirects = 10;
        var currentUrl = url;

        for (int i = 0; i < maxRedirects; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
            request.Headers.Add("User-Agent", "MyApp-Updater/1.0");
            var response = await Http.SendAsync(request, option);

            if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Moved
                or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect
                or (HttpStatusCode)308)
            {
                var location = response.Headers.Location;
                if (location == null) break;
                currentUrl = location.IsAbsoluteUri ? location.AbsoluteUri : new Uri(new Uri(currentUrl), location).AbsoluteUri;
                response.Dispose();
                continue;
            }

            response.EnsureSuccessStatusCode();
            return response;
        }

        throw new HttpRequestException($"Too many redirects or failed to download from {url}");
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        var url = BuildUrl(UpdateJsonShareId);
        using var response = await GetWithRedirectsAsync(url);
        var json = await response.Content.ReadAsStringAsync();
        var info = JsonSerializer.Deserialize<UpdateInfo>(json);
        if (info == null) return null;

        var remote = new Version(info.Version);
        var local = new Version(CurrentVersion);
        return remote > local ? info : null;
    }

    public async Task DownloadUpdateAsync(UpdateInfo info, IProgress<double>? progress = null)
    {
        var url = BuildUrl(info.ShareId);
        if (Directory.Exists(TempDir)) Directory.Delete(TempDir, true);
        Directory.CreateDirectory(TempDir);

        var destPath = Path.Combine(TempDir, info.FileName);

        using var response = await GetWithRedirectsAsync(url, HttpCompletionOption.ResponseHeadersRead);
        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var file = File.Create(destPath);

        var buffer = new byte[81920];
        long downloaded = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read));
            downloaded += read;
            if (totalBytes > 0)
                progress?.Report((double)downloaded / totalBytes * 100);
        }
    }

    public static bool VerifyHash(UpdateInfo info)
    {
        if (string.IsNullOrEmpty(info.Hash)) return true;

        var filePath = Path.Combine(TempDir, info.FileName);
        if (!File.Exists(filePath)) return false;

        var parts = info.Hash.Split(':', 2);
        if (parts.Length != 2 || parts[0] != "sha256") return false;

        using var sha = SHA256.Create();
        using var fs = File.OpenRead(filePath);
        var hash = Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
        return hash == parts[1].ToLowerInvariant();
    }

    public static void ApplyUpdate(UpdateInfo info)
    {
        var downloadedFile = Path.Combine(TempDir, info.FileName);
        var appExe = Environment.ProcessPath!;
        var appDir = Path.GetDirectoryName(appExe)!;
        var extractDir = TempDir;

        if (info.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var zipExtract = Path.Combine(TempDir, "extracted");
            if (Directory.Exists(zipExtract)) Directory.Delete(zipExtract, true);
            ZipFile.ExtractToDirectory(downloadedFile, zipExtract);
            extractDir = zipExtract;
        }

        var batPath = Path.Combine(TempDir, "update.bat");
        var isZip = info.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        var sourceArg = isZip ? extractDir : downloadedFile;

        var script = $"""
            @echo off
            timeout /t 2 /nobreak >nul
            :waitloop
            tasklist /FI "PID eq %1" 2>nul | find "%1" >nul
            if not errorlevel 1 (
                timeout /t 1 /nobreak >nul
                goto waitloop
            )
            if exist "{appExe}.bak" del /f "{appExe}.bak"
            copy /y "{appExe}" "{appExe}.bak"
            if "{(isZip ? "zip" : "exe")}"=="zip" (
                xcopy /s /y /q "{sourceArg}\*" "{appDir}\"
            ) else (
                copy /y "{sourceArg}" "{appExe}"
            )
            start "" "{appExe}"
            timeout /t 3 /nobreak >nul
            rd /s /q "{TempDir}" 2>nul
            """;

        File.WriteAllText(batPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{batPath}\" {Environment.ProcessId}",
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        Application.Current.Shutdown();
    }
}
