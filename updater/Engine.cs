using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Vow.Updater;
internal sealed class Payload
{
    public string version { get; set; }
    public Module[] modules { get; set; }
}
internal sealed class Module
{
    public string name { get; set; }
    public string sha256 { get; set; }
}
internal sealed class Journal
{
    public string folder { get; set; }
    public Module[] modules { get; set; }
}
internal static class Engine
{
    internal const string Api = "https://api.github.com/repos/dpo393366-tech/HeroesVowParry/releases/latest";
    internal const string DownloadPrefix = "https://github.com/dpo393366-tech/HeroesVowParry/releases/download/";
    internal static readonly string[] Names = { "DuelCounters", "DodgeLimit" };
    internal static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data));
    static string FileHash(string file) => Hash(File.ReadAllBytes(file));
    static string Destination(string root, string name) => Path.Combine(root, "BepInEx", "plugins", name, name + ".dll");
    static string StateRoot(string root) => Path.Combine(root, "BepInEx", "cache", "HeroesVowParryUpdate");
    static string JournalPath(string root) => Path.Combine(StateRoot(root), "pending.json");
    static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    internal static void SafePath(string root, string path)
    {
        root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        path = Path.GetFullPath(path);
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Path escaped game directory.");
        for (var p = path; p.Length >= root.Length; p = Path.GetDirectoryName(p))
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked update path preserved: " + p);
    }
    internal static void ValidateModules(Module[] modules)
    {
        if (modules == null || modules.Length != 2 || modules.Select(m => m.name).Distinct().Count() != 2 ||
            modules.Any(m => !Names.Contains(m.name) || m.sha256 == null || m.sha256.Length != 64 || !m.sha256.All(Uri.IsHexDigit)))
            throw new InvalidDataException("Unexpected update module list.");
    }
    internal static async Task<byte[]> Get(HttpClient client, string url, int limit, CancellationToken token)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Update response is too large.");
        using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[32768]; int count;
        while ((count = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
        {
            if (output.Length + count > limit) throw new InvalidDataException("Update response exceeds size limit.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
    internal static (string url, string hash) ReadRelease(byte[] data)
    {
        using var doc = JsonDocument.Parse(data);
        var release = doc.RootElement;
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) throw new InvalidDataException("Not a stable release.");
        var assets = release.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() == "HeroesVowParry-update.zip").ToArray();
        if (assets.Length != 1) throw new InvalidDataException("No startup update asset in this release.");
        var asset = assets[0];
        var url = asset.GetProperty("browser_download_url").GetString();
        var digest = asset.GetProperty("digest").GetString();
        if (url == null || !url.StartsWith(DownloadPrefix, StringComparison.Ordinal) ||
            digest == null || !digest.StartsWith("sha256:", StringComparison.Ordinal) || digest.Length != 71 || !digest.Substring(7).All(Uri.IsHexDigit))
            throw new InvalidDataException("Update source or SHA256 missing.");
        return (url, digest.Substring(7));
    }
    internal static bool InstalledMatches(string root, Payload p)
    {
        try { ValidateModules(p.modules); return p.modules.All(m => File.Exists(Destination(root, m.name)) && Same(FileHash(Destination(root, m.name)), m.sha256)); }
        catch { return false; }
    }
    internal static bool OwnedInstallation(string root)
    {
        // A later all-in-one/custom install must never be overwritten.
        try {
            var marker=Path.Combine(root,"BepInEx","patchers","HeroesVowParry","owner.json");
            SafePath(root,marker);
            if(File.Exists(marker)&&InstalledMatches(root,JsonSerializer.Deserialize<Payload>(File.ReadAllBytes(marker))))return true;
            var receipt=Path.Combine(StateRoot(root),"installed.json");SafePath(root,receipt);
            if(File.Exists(receipt)){
                using var doc=JsonDocument.Parse(File.ReadAllBytes(receipt));
                return InstalledMatches(root,JsonSerializer.Deserialize<Payload>(doc.RootElement.GetProperty("payload").GetRawText()));
            }
        }catch{}
        return false;
    }
    internal static void Run(string root, bool enabled, Action<string> log)
    {
        using var gate = new Mutex(false, "Local\\HeroesVowParryUpdate-" + Hash(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(root).ToUpperInvariant())).Substring(0, 20));
        bool held = false;
        try
        {
            try { held = gate.WaitOne(0); } catch (AbandonedMutexException) { held = true; }
            if (!held) { log("Update already running; skipped."); return; }
            Recover(root, log);
            if (!enabled) { log("Auto update disabled."); return; }
            // An explicitly removed module must never be resurrected by the updater.
            if (Names.Any(n => !File.Exists(Destination(root, n)))) { log("A module is absent; automatic reinstall skipped."); return; }
            if (!OwnedInstallation(root)) { log("Local modules changed outside the standalone installer; preserving them."); return; }
            foreach (var n in Names) SafePath(root, Destination(root, n));
            using var client = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("HeroesVowParry-StartupUpdater/1.0");
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            using var metadataCancel = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);
            metadataCancel.CancelAfter(TimeSpan.FromSeconds(8));
            var remote = ReadRelease(Get(client, Api, 1024 * 1024, metadataCancel.Token).GetAwaiter().GetResult());
            var receipt = Path.Combine(StateRoot(root), "installed.json");
            SafePath(root, receipt);
            if (File.Exists(receipt))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllBytes(receipt));
                    var cached = doc.RootElement;
                    if (Same(cached.GetProperty("hash").GetString(), remote.hash) && InstalledMatches(root, JsonSerializer.Deserialize<Payload>(cached.GetProperty("payload").GetRawText())))
                    { log("Already up to date."); return; }
                }
                catch { /* A damaged cache is not authority for installed files. */ }
            }
            var archive = Get(client, remote.url, 16 * 1024 * 1024, cancel.Token).GetAwaiter().GetResult();
            var payload = Apply(root, archive, remote.hash, log);
            Directory.CreateDirectory(StateRoot(root));
            File.WriteAllText(receipt, JsonSerializer.Serialize(new { hash = remote.hash, payload }));
            log("Startup update verified: " + payload.version + "; settings preserved.");
        }
        catch (Exception ex) { log("Update skipped/rolled back; continuing with installed files: " + ex.Message); }
        finally { if (held) gate.ReleaseMutex(); }
    }
    internal static Payload Apply(string root, byte[] archive, string expected, Action<string> log, Action<int> afterReplace = null)
    {
        if (!Same(Hash(archive), expected)) throw new InvalidDataException("Downloaded ZIP SHA256 mismatch.");
        using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
        if (zip.Entries.Any(e => e.Length > 12 * 1024 * 1024) || zip.Entries.Count != 3) throw new InvalidDataException("Unexpected update ZIP layout.");
        var entry = zip.GetEntry("update.json") ?? throw new InvalidDataException("Update manifest missing.");
        Payload payload;
        using (var s = entry.Open()) payload = JsonSerializer.Deserialize<Payload>(s);
        ValidateModules(payload.modules);
        if (!Version.TryParse(payload.version, out var version)) throw new InvalidDataException("Invalid version.");
        var content = new Dictionary<string, byte[]>();
        foreach (var m in payload.modules)
        {
            if (zip.Entries.Count(e => e.FullName == m.name + ".dll") != 1) throw new InvalidDataException("Duplicate or missing DLL.");
            using var s = zip.GetEntry(m.name + ".dll").Open(); using var memory = new MemoryStream(); s.CopyTo(memory);
            var data = memory.ToArray(); if (!Same(Hash(data), m.sha256)) throw new InvalidDataException("Module hash mismatch.");
            content.Add(m.name, data);
            var dest = Destination(root, m.name); SafePath(root, dest);
            if (!File.Exists(dest)) throw new IOException("Removed module preserved.");
            var identity = AssemblyName.GetAssemblyName(dest);
            if (identity.Name != m.name) throw new InvalidDataException("Existing module identity mismatch.");
            if (identity.Version > version) throw new InvalidDataException("Refusing to downgrade a newer local module.");
        }
        if (InstalledMatches(root, payload)) return payload;
        var dir = Path.Combine(root, "BepInEx", "disabled-mods", "ParryAuto-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        SafePath(root, dir); SafePath(root, JournalPath(root));
        Directory.CreateDirectory(dir);
        var old = new List<Module>();
        foreach (var m in payload.modules)
        {
            var temp = Path.Combine(dir, m.name + ".new"); File.WriteAllBytes(temp, content[m.name]);
            if (AssemblyName.GetAssemblyName(temp).Name != m.name) throw new InvalidDataException("Downloaded module identity mismatch.");
            var dest = Destination(root, m.name);
            File.Copy(dest, Path.Combine(dir, m.name + ".backup"));
            old.Add(new Module { name = m.name, sha256 = FileHash(dest) });
            if (!Same(FileHash(Path.Combine(dir, m.name + ".backup")), old.Last().sha256)) throw new IOException("Backup verification failed.");
        }
        Directory.CreateDirectory(StateRoot(root));
        File.WriteAllText(JournalPath(root) + ".new", JsonSerializer.Serialize(new Journal { folder = Path.GetFileName(dir), modules = old.ToArray() }));
        File.Move(JournalPath(root) + ".new", JournalPath(root));
        try
        {
            int i = 0;
            foreach (var m in payload.modules)
            {
                File.Replace(Path.Combine(dir, m.name + ".new"), Destination(root, m.name), null);
                afterReplace?.Invoke(++i);
                if (!Same(FileHash(Destination(root, m.name)), m.sha256)) throw new IOException("Installed file verification failed.");
            }
            File.Delete(JournalPath(root));
            log("Updated only DuelCounters and DodgeLimit. Backup: " + dir);
            return payload;
        }
        catch { Recover(root, log); throw; }
    }
    internal static void Recover(string root, Action<string> log)
    {
        var journal = JournalPath(root); SafePath(root, journal);
        if (!File.Exists(journal)) return;
        var j = JsonSerializer.Deserialize<Journal>(File.ReadAllText(journal)); ValidateModules(j.modules);
        if (j.folder == null || !j.folder.StartsWith("ParryAuto-", StringComparison.Ordinal) || Path.GetFileName(j.folder) != j.folder) throw new InvalidDataException("Invalid recovery folder.");
        var dir = Path.Combine(root, "BepInEx", "disabled-mods", j.folder); SafePath(root, dir);
        // Validate every backup before restoring any module.
        foreach (var m in j.modules)
        {
            var backup = Path.Combine(dir, m.name + ".backup"); SafePath(root, backup); SafePath(root, Destination(root, m.name));
            if (!Same(FileHash(backup), m.sha256)) throw new IOException("Recovery backup damaged; preserved for manual recovery.");
        }
        foreach (var m in j.modules)
        {
            var dest = Destination(root, m.name);
            if (File.Exists(dest) && Same(FileHash(dest), m.sha256)) continue;
            var tmp = Path.Combine(dir, m.name + ".restore"); File.Copy(Path.Combine(dir, m.name + ".backup"), tmp, true);
            if (File.Exists(dest)) File.Replace(tmp, dest, null); else File.Move(tmp, dest);
        }
        File.Delete(journal); log("Recovered interrupted update from verified backup.");
    }
}
