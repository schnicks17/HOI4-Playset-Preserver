// Hearts of Iron IV Playset Preserver
// Reads a playset from the HOI4 launcher and merges all of its enabled mods, in load order,
// into one local mod that Steam will never update.
//
// Based on the design of the Crusader Kings 3 Playset Preserver by escalonn and Kaepbora
// (https://github.com/Ant0nidas/CK3-Playset-Preserver), licensed under the GNU GPL v3.
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY; see LICENSE.
//
// Written in C# 5 so it compiles with the csc.exe that ships with Windows (.NET Framework 4.x).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;

[assembly: AssemblyTitle("HOI4 Playset Preserver")]
[assembly: AssemblyDescription("Merges a Hearts of Iron IV launcher playset into one local mod. Port of the CK3 Playset Preserver by escalonn and Kaepbora.")]
[assembly: AssemblyProduct("HOI4 Playset Preserver")]
[assembly: AssemblyCompany("schnicks17 (github.com/schnicks17/HOI4-Playset-Preserver)")]
[assembly: AssemblyCopyright("GPL-3.0 - based on the CK3 Playset Preserver by escalonn and Kaepbora")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

static class Program
{
    const string Version = "1.1.0";
    static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

    static int Main()
    {
        // allow paths longer than 260 characters (mods with deep folders); must be set before any file access
        AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
        AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "Hearts of Iron IV Playset Preserver v" + Version;
        try { Run(); }
        catch (Exception e)
        {
            Console.WriteLine();
            Color(ConsoleColor.Red, "ERROR: " + e.Message);
            Console.WriteLine(e.StackTrace);
        }
        Console.WriteLine();
        Console.Write("Press Enter to exit...");
        Console.ReadLine();
        return 0;
    }

    // ------------------------------------------------------------------ helpers

    static void Color(ConsoleColor c, string text)
    {
        ConsoleColor old = Console.ForegroundColor;
        Console.ForegroundColor = c;
        Console.WriteLine(text);
        Console.ForegroundColor = old;
    }

    static string Ask(string prompt, string def)
    {
        Console.Write(def != null ? prompt + " [" + def + "]: " : prompt + ": ");
        string a = Console.ReadLine();
        if (a == null) return def;   // input closed
        a = a.Trim();
        return a.Length == 0 ? def : a;
    }

    // yes/no question in the CK3 tool's style: "question - y/[n]: "
    static bool AskYesNo(string question, bool defaultYes)
    {
        Console.Write(question + (defaultYes ? " - [y]/n: " : " - y/[n]: "));
        string a = Console.ReadLine();
        if (a == null) return defaultYes;
        a = a.Trim().ToLowerInvariant();
        if (a.Length == 0) return defaultYes;
        return a == "y" || a == "yes";
    }

    static string FormatSize(long bytes)
    {
        if (bytes >= 1L << 30) return string.Format("{0:N2} GB", bytes / (double)(1L << 30));
        return string.Format("{0:N0} MB", bytes / (double)(1L << 20));
    }

    // \\?\ prefix lets .NET read paths longer than 260 characters
    static string Long(string path)
    {
        string full = Path.GetFullPath(path);
        return full.StartsWith(@"\\?\") ? full : @"\\?\" + full;
    }

    static string Str(Dictionary<string, object> row, string key)
    {
        object v;
        return row.TryGetValue(key, out v) && v != null ? v.ToString() : "";
    }

    // ------------------------------------------------------------------ locating things

    static string FindHoi4Dir()
    {
        // the tool lives in ...\Hearts of Iron IV\mod\<tool folder>; walk up from there first
        DirectoryInfo d = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (d != null)
        {
            if (d.Name == "Hearts of Iron IV" && Directory.Exists(Path.Combine(d.FullName, "mod"))) return d.FullName;
            d = d.Parent;
        }
        string docs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Paradox Interactive\Hearts of Iron IV");
        return Directory.Exists(Path.Combine(docs, "mod")) ? docs : null;
    }

    static string FindDatabase(string hoi4)
    {
        string rel = Path.Combine(hoi4, "launcher-v2.sqlite");
        string beta = Path.Combine(hoi4, "launcher-v2_openbeta.sqlite");
        bool hasRel = File.Exists(rel), hasBeta = File.Exists(beta);
        if (hasRel && !hasBeta) return rel;
        if (hasBeta && !hasRel) return beta;
        if (hasRel && hasBeta)
        {
            if (File.GetLastWriteTime(beta) > File.GetLastWriteTime(rel)) { Console.WriteLine("Using the beta launcher database (most recently modified)."); return beta; }
            Console.WriteLine("Using the release launcher database (most recently modified).");
            return rel;
        }
        // fallback: newest launcher sqlite, ignoring the launcher's own backup copies
        FileInfo f = new DirectoryInfo(hoi4).GetFiles("launcher*.sqlite")
            .Where(x => x.Name.IndexOf("backup", StringComparison.OrdinalIgnoreCase) < 0)
            .OrderByDescending(x => x.LastWriteTime).FirstOrDefault();
        if (f != null) { Color(ConsoleColor.Yellow, "WARNING: expected launcher database not found, using " + f.Name + "."); return f.FullName; }
        return null;
    }

    static bool LauncherRunning()
    {
        foreach (Process p in Process.GetProcesses())
        {
            try
            {
                string n = p.ProcessName;
                if (n.Equals("Paradox Launcher", StringComparison.OrdinalIgnoreCase) || n.Equals("launcher", StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("dowser", StringComparison.OrdinalIgnoreCase) || (p.MainWindowTitle ?? "").Contains("Paradox Launcher")) return true;
            }
            catch { }
        }
        return false;
    }

    // Sort key like the CK3 tool: first the minimum version a range describes (1.* < 1.16.* < 1.16.9), then the maximum
    static string VersionKey(string v)
    {
        string[] parts = Regex.Replace(v, "^[vV]", "").Split('.');
        Func<string, string> pad = s => Regex.IsMatch(s, @"^\d+$") ? s.PadLeft(5, '0') : s;
        string min = string.Join(".", parts.Where(p => p != "*").Select(pad));
        string max = string.Join(".", parts.Select(p => p == "*" ? "99999" : pad(p)));
        return min + " " + max;   // space sorts before '.', so 1 < 1.19
    }

    static IEnumerable<string> ReplacePaths(string dotmod)
    {
        if (string.IsNullOrEmpty(dotmod) || !File.Exists(dotmod)) yield break;
        foreach (string line in File.ReadAllLines(dotmod))
        {
            Match m = Regex.Match(line, "^\\s*replace_path\\s*=\\s*\"([^\"]+)\"");
            if (m.Success) yield return m.Groups[1].Value;
        }
    }

    // every file below root (not the top level, not .git), as relative paths
    static IEnumerable<KeyValuePair<string, long>> ModFiles(string root)
    {
        string lr = Long(root).TrimEnd('\\');
        Stack<string> dirs = new Stack<string>();
        foreach (string d in Directory.GetDirectories(lr)) dirs.Push(d);
        while (dirs.Count > 0)
        {
            string d = dirs.Pop();
            if (Path.GetFileName(d).Equals(".git", StringComparison.OrdinalIgnoreCase)) continue;
            string[] files, sub;
            try { files = Directory.GetFiles(d); sub = Directory.GetDirectories(d); }
            catch (UnauthorizedAccessException) { continue; }
            foreach (string s in sub) dirs.Push(s);
            foreach (string f in files) yield return new KeyValuePair<string, long>(f.Substring(lr.Length + 1), new FileInfo(f).Length);
        }
    }

    // ------------------------------------------------------------------ main flow

    class Source { public Dictionary<string, object> Mod; public string Path; public long Size; }

    static void Run()
    {
        Console.WriteLine();
        Color(ConsoleColor.DarkYellow, "  ==============================================================");
        Color(ConsoleColor.Yellow, "        HEARTS OF IRON IV  -  PLAYSET PRESERVER  v" + Version);
        Color(ConsoleColor.DarkYellow, "  ==============================================================");
        Console.WriteLine();
        Console.WriteLine("  This program reads a playset from your HOI4 launcher and creates a merged");
        Console.WriteLine("  local copy of it: one mod that contains every enabled mod in load order.");
        Console.WriteLine("  Steam updates can no longer break it, so running campaigns stay playable.");
        Console.WriteLine();
        Console.WriteLine("  Based on the CK3 Playset Preserver by escalonn and Kaepbora (Community Mods for Historicity).");
        Console.WriteLine("  https://github.com/schnicks17/HOI4-Playset-Preserver");
        Console.WriteLine();
        Console.WriteLine("  Answer the prompts by typing and pressing Enter. Defaults are shown [in brackets];");
        Console.WriteLine("  press Enter on an empty line to accept them. Close the window to cancel at any time.");
        Console.WriteLine();
        Console.WriteLine("No support is provided for preserved playsets. Do not ask mod authors for help with");
        Console.WriteLine("problems in a preserved playset, and do not distribute it - all content belongs to its authors.");
        if (!AskYesNo("Have you understood?", false)) { Console.WriteLine("Exiting. Run the program again if you agree."); return; }

        string hoi4 = FindHoi4Dir();
        if (hoi4 == null) { Color(ConsoleColor.Red, "ERROR: Hearts of Iron IV documents folder not found."); return; }
        string modDir = Path.Combine(hoi4, "mod");
        string db = FindDatabase(hoi4);
        if (db == null) { Color(ConsoleColor.Red, "ERROR: launcher database not found."); return; }

        // choose a playset
        List<Dictionary<string, object>> playsets = PPSqlite.Query(db,
            "SELECT id, name, isActive FROM playsets WHERE isRemoved = 0 OR isRemoved IS NULL OR isRemoved = 'false' ORDER BY rowid;", null);
        if (playsets.Count == 0) { Console.WriteLine("No playsets found in the launcher."); return; }
        Console.WriteLine();
        for (int i = 0; i < playsets.Count; i++)
            Console.WriteLine("{0}. {1}{2}", i + 1, Str(playsets[i], "name"), Str(playsets[i], "isActive") == "1" ? "  (active)" : "");
        Console.WriteLine();
        int n = 0;
        while (n < 1 || n > playsets.Count)
        {
            Console.Write("Select the playset by typing its number: ");
            string s = Console.ReadLine();
            if (s == null) return;
            int.TryParse(s.Trim(), out n);
        }
        Dictionary<string, object> playset = playsets[n - 1];

        List<Dictionary<string, object>> mods = PPSqlite.Query(db,
            "SELECT m.gameRegistryId, m.displayName, m.version, m.tags, m.requiredVersion, m.dirPath, m.archivePath, m.status, pm.enabled " +
            "FROM mods AS m JOIN playsets_mods AS pm ON m.id = pm.modId WHERE pm.playsetId = ? ORDER BY pm.position;",
            new object[] { Str(playset, "id") });
        if (mods.Count == 0) { Console.WriteLine("This playset contains no mods."); return; }

        // mods the launcher cannot find on disk
        List<Dictionary<string, object>> missing = mods.Where(m =>
            (Str(m, "status") != "ready_to_play" && Str(m, "status") != "initialized") ||
            (Str(m, "archivePath") == "" && !Directory.Exists(Str(m, "dirPath")))).ToList();
        if (missing.Count > 0)
        {
            Console.WriteLine();
            Color(ConsoleColor.Red, "ERROR: the following mods cannot be found:");
            foreach (var m in missing) { Console.WriteLine("  - " + Str(m, "displayName")); mods.Remove(m); }
            Console.WriteLine("Unsubscribing, resubscribing and restarting the launcher may fix this.");
            if (!AskYesNo("Ignore these mods and continue?", false)) return;
        }

        // disabled mods are left out
        List<Dictionary<string, object>> disabled = mods.Where(m => Str(m, "enabled") == "0" || Str(m, "enabled") == "" || Str(m, "enabled") == "false").ToList();
        if (disabled.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("These mods are disabled in the playset and will NOT be included:");
            foreach (var m in disabled) { Console.WriteLine("  - " + Str(m, "displayName")); mods.Remove(m); }
        }
        if (mods.Count == 0) { Console.WriteLine("No enabled mods left."); return; }

        Console.WriteLine();
        Console.WriteLine("Load order (later mods override earlier ones):");
        for (int i = 0; i < mods.Count; i++) Console.WriteLine("  {0,2}. {1}  ({2})", i + 1, Str(mods[i], "displayName"), Str(mods[i], "version"));

        // game version: the highest one any mod requires
        string defVersion = mods.Select(m => Str(m, "requiredVersion")).Where(v => v != "").OrderBy(VersionKey, StringComparer.Ordinal).LastOrDefault() ?? "1.16.*";
        Console.WriteLine();
        string gameVersion;
        while (true)
        {
            gameVersion = Ask("Enter the game version this playset is for (* is allowed)", defVersion);
            if (Regex.IsMatch(gameVersion, "[\\\\\t\"]")) Color(ConsoleColor.Red, "ERROR: the version cannot contain \\, \" or tabs.");
            else break;
        }

        // name of the new mod
        string clean = Regex.Replace(Str(playset, "name"), "[\t\\\\\"]", "");
        string defName = clean + " (" + DateTime.Now.ToString("yyyy-MM-dd") + ")";
        string newName, folderName, newFolder, dotmod;
        while (true)
        {
            newName = Ask("Enter the name of the preserved playset", defName);
            if (Regex.IsMatch(newName, "[\t\\\\\"]")) { Color(ConsoleColor.Red, "ERROR: the name cannot contain \\, \" or tabs."); continue; }
            if (newName.Length < 3) { Color(ConsoleColor.Red, "ERROR: the name must be at least 3 characters long."); continue; }
            folderName = Regex.Replace(newName, "[*\"/:<>?|]", "").TrimEnd('.').Trim();
            newFolder = Path.Combine(modDir, folderName);
            dotmod = newFolder + ".mod";
            if (Directory.Exists(newFolder)) { Color(ConsoleColor.Red, "ERROR: \"" + folderName + "\" already exists."); continue; }
            if (File.Exists(dotmod)) { Color(ConsoleColor.Red, "ERROR: \"" + folderName + ".mod\" already exists."); continue; }
            break;
        }

        // unpack Paradox Mods / zipped mods, measure sizes, check disk space
        Console.WriteLine();
        Console.WriteLine("Measuring mods...");
        List<string> temps = new List<string>();
        List<Source> sources = new List<Source>();
        Dictionary<string, string> fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            long total = 0;
            foreach (var m in mods)
            {
                string src = Str(m, "dirPath");
                if (Str(m, "archivePath") != "")
                {
                    string td = Path.Combine(Path.GetTempPath(), "hoi4pp_" + Guid.NewGuid().ToString("N"));
                    ZipFile.ExtractToDirectory(Str(m, "archivePath"), td);
                    temps.Add(td); src = td;
                }
                long size = 0;
                foreach (var f in ModFiles(src)) size += f.Value;
                // top-level files count too (they are copied, then removed)
                foreach (string f in Directory.GetFiles(Long(src))) size += new FileInfo(f).Length;
                total += size;
                sources.Add(new Source { Mod = m, Path = src, Size = size });
            }
            DriveInfo drive = new DriveInfo(Path.GetPathRoot(modDir));
            Console.WriteLine("Total size: {0}  -  free on drive {1}: {2}", FormatSize(total), drive.Name.TrimEnd('\\'), FormatSize(drive.AvailableFreeSpace));
            if (drive.AvailableFreeSpace < total * 1.05) { Color(ConsoleColor.Red, "ERROR: not enough free disk space."); return; }

            // copy every mod in load order on top of the previous ones (robocopy handles long paths)
            Console.WriteLine();
            Console.WriteLine("Copying...");
            Directory.CreateDirectory(newFolder);
            for (int i = 0; i < sources.Count; i++)
            {
                Source s = sources[i];
                string name = Str(s.Mod, "displayName");
                Console.WriteLine("  [{0}/{1}] {2}  ({3})", i + 1, sources.Count, name, FormatSize(s.Size));
                ProcessStartInfo psi = new ProcessStartInfo("robocopy.exe",
                    "\"" + s.Path.TrimEnd('\\') + "\" \"" + newFolder.TrimEnd('\\') + "\" /E /XD .git /R:2 /W:1 /MT:8 /NFL /NDL /NJH /NJS /NP");
                psi.UseShellExecute = false; psi.CreateNoWindow = true; psi.RedirectStandardOutput = true;
                using (Process p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    if (p.ExitCode >= 8) throw new Exception("robocopy failed for " + name + " (exit code " + p.ExitCode + ")");
                }
                foreach (var f in ModFiles(s.Path)) fileMap[f.Key] = name;
            }
        }
        finally
        {
            foreach (string td in temps) { try { Directory.Delete(td, true); } catch { } }
        }

        // thumbnails, readmes and descriptors of the source mods pile up at the top level - remove them
        foreach (string f in Directory.GetFiles(newFolder)) File.Delete(f);

        // .mod files: tags of all mods, replace_path lines of all mods
        SortedSet<string> tags = new SortedSet<string>(StringComparer.Ordinal);
        SortedSet<string> replace = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Source s in sources)
        {
            foreach (Match m in Regex.Matches(Str(s.Mod, "tags"), "\"([^\"]*)\"")) tags.Add(m.Groups[1].Value);
            List<string> paths = ReplacePaths(Path.Combine(hoi4, Str(s.Mod, "gameRegistryId").Replace('/', '\\'))).ToList();
            if (paths.Count == 0) paths = ReplacePaths(Path.Combine(s.Path, "descriptor.mod")).ToList();
            foreach (string p in paths) replace.Add(p);
        }
        List<string> lines = new List<string> { "version=\"1.0.0\"", "tags={" };
        lines.AddRange(tags.Select(t => "\t\"" + t + "\""));
        lines.Add("}");
        lines.Add("name=\"" + newName + "\"");
        lines.Add("supported_version=\"" + gameVersion + "\"");
        List<string> replaceLines = replace.Select(p => "replace_path=\"" + p + "\"").ToList();
        File.WriteAllText(dotmod, string.Join("\n", lines.Concat(new[] { "path=\"mod/" + folderName + "\"" }).Concat(replaceLines)) + "\n", Utf8NoBom);
        File.WriteAllText(Path.Combine(newFolder, "descriptor.mod"), string.Join("\n", lines.Concat(replaceLines)) + "\n", Utf8NoBom);

        // README with the contents, and a map of which mod each file came from
        List<string> readme = new List<string> {
            "This mod was generated by the Hearts of Iron IV Playset Preserver v" + Version + ".",
            "https://github.com/schnicks17/HOI4-Playset-Preserver", "",
            "Source playset: " + Str(playset, "name"), "Date: " + DateTime.Now.ToString("yyyy-MM-dd"),
            "Game version: " + gameVersion, "Contents (load order):" };
        readme.AddRange(sources.Select(s => "  " + Str(s.Mod, "displayName") + " (" + Str(s.Mod, "version") + ")"));
        File.WriteAllLines(Path.Combine(newFolder, "README.txt"), readme, Utf8NoBom);
        File.WriteAllLines(Path.Combine(newFolder, "file_to_mod_map.txt"),
            fileMap.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).Select(k => k.Key + " <- [" + k.Value + "]"), Utf8NoBom);

        Console.WriteLine();
        Color(ConsoleColor.Green, "Preserved playset \"" + newName + "\" created in " + newFolder);
        Console.WriteLine("{0} files from {1} mods.", fileMap.Count, sources.Count);

        // optionally register a new playset in the launcher that contains only the merged mod
        Console.WriteLine();
        if (AskYesNo("Create a new playset in the launcher containing only this mod?", true))
        {
            while (LauncherRunning())
            {
                Color(ConsoleColor.Yellow, "The Paradox Launcher is running. Close it completely (also from the tray), then press Enter.");
                if (Console.ReadLine() == null) return;
            }
            string backup = db + ".pp_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            File.Copy(db, backup);
            string modId = Guid.NewGuid().ToString(), psId = Guid.NewGuid().ToString();
            long nowMs = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds, nowS = nowMs / 1000;
            string tagsJson = "[" + string.Join(",", tags.Select(t => "\"" + t + "\"")) + "]";
            PPSqlite.ExecAll(db,
                new[] {
                    "INSERT INTO mods (id, gameRegistryId, name, displayName, version, tags, requiredVersion, dirPath, status, source, createdDate) VALUES (?, ?, ?, ?, '1.0.0', ?, ?, ?, 'ready_to_play', 'local', ?);",
                    "INSERT INTO playsets (id, name, isActive, loadOrder, createdOn, syncState) VALUES (?, ?, 0, 'custom', ?, 'NOT_ELIGIBLE');",
                    "INSERT INTO playsets_mods (playsetId, modId, enabled, position) VALUES (?, ?, 1, 0);" },
                new[] {
                    new object[] { modId, "mod/" + folderName + ".mod", newName, newName, tagsJson, gameVersion, newFolder, nowS },
                    new object[] { psId, newName, nowMs },
                    new object[] { psId, modId } });
            Color(ConsoleColor.Green, "Playset \"" + newName + "\" created in the launcher. (Database backup: " + Path.GetFileName(backup) + ")");
        }
        Console.WriteLine();
        Console.WriteLine("If the launcher is open, close and reopen it to see the changes.");
    }
}
