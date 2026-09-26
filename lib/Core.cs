using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace Keel
{
    /// <summary>
    /// Finds Keel's core for a mod that Keel is starting. Usually the starter
    /// has already run the newest core, which published itself. When Keel
    /// 1.0's Keel.dll started the game instead, this loads the newest core
    /// itself, by the starter's own rule: a Keel.Core.X.Y.Z.dll in the Keel
    /// folder, named exactly, that really is the version its name says and
    /// isn't in SkipCores in Keel.cfg, the highest version first.
    /// </summary>
    internal static class Core
    {
        /// <summary>The Keel this copy of the plug comes from, which it tells the core.</summary>
        internal const string Version = "2.0.0";

        private const string Key = "Keel.Core";
        private const string CoreName = "Keel.Core.";

        /// <summary>What this copy of Keel needs from the core: contract 1, as in Keel 2.0.</summary>
        private const int Needs = 1;

        /// <summary>
        /// The published core, or null, with why in a few words that finish a
        /// sentence. When this copy had to load the core itself, loaded says
        /// so, as a sentence for Keel's log; otherwise it's null.
        /// </summary>
        internal static IDictionary<string, object> Find(string folder, out string why, out string loaded)
        {
            why = null;
            loaded = null;
            IDictionary<string, object> core = AppDomain.CurrentDomain.GetData(Key) as IDictionary<string, object>;
            if (core == null)
            {
                lock (typeof(AppDomain))
                {
                    core = AppDomain.CurrentDomain.GetData(Key) as IDictionary<string, object>;
                    if (core == null) core = Load(folder, out why, out loaded);
                }
            }
            if (core == null) return null;

            object contract, version, start;
            core.TryGetValue("contract", out contract);
            core.TryGetValue("version", out version);
            core.TryGetValue("start", out start);
            if (!(contract is int) || (int)contract < Needs
                || !(start is Func<string, string, string, string, IDictionary<string, object>, IDictionary<string, object>>))
            {
                why = "it needs a newer Keel core than " + ((version as string) ?? "the one that's running") + ".";
                return null;
            }
            return core;
        }

        private static IDictionary<string, object> Load(string folder, out string why, out string loaded)
        {
            why = null;
            loaded = null;
            string mod = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string home = Path.GetDirectoryName(mod) ?? string.Empty;
            List<string> passed = new List<string>();
            List<KeyValuePair<System.Version, string>> cores;
            try
            {
                cores = Newest(home, Skipped(Path.Combine(home, "Keel.cfg")), passed);
            }
            catch (Exception e)
            {
                why = "Keel couldn't look for its core in " + home + ": " + e.Message;
                return null;
            }

            foreach (KeyValuePair<System.Version, string> found in cores)
            {
                string file = Path.GetFileName(found.Value);
                bool isLoaded = false;
                string wrong;
                try
                {
                    AssemblyName inside = AssemblyName.GetAssemblyName(found.Value);
                    if (inside.Name != "Keel.Core" || !Same(inside.Version, found.Key))
                    {
                        wrong = "it isn't the core its name says";
                    }
                    else
                    {
                        Assembly assembly = Assembly.LoadFrom(found.Value);
                        isLoaded = true;
                        // The game hands back a core it already has, whatever
                        // the file asked for, once one with the same name is
                        // loaded.
                        System.Version got = assembly.GetName().Version;
                        Type services = Same(got, found.Key) ? assembly.GetType("Keel.Services") : null;
                        MethodInfo publish = services == null
                            ? null
                            : services.GetMethod("Publish", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                                                 null, Type.EmptyTypes, null);
                        IDictionary<string, object> core = publish == null
                            ? null
                            : publish.Invoke(null, null) as IDictionary<string, object>;
                        if (core != null)
                        {
                            loaded = "It loaded Keel's core, " + file + ", itself"
                                   + (passed.Count == 0 ? "." : ", after passing over " + string.Join(", ", passed.ToArray()) + ".");
                            return core;
                        }
                        wrong = Same(got, found.Key)
                            ? "it has no way in that this copy of Keel knows"
                            : "the game already has core " + Three(got) + " loaded";
                    }
                }
                catch (Exception)
                {
                    wrong = isLoaded ? "it failed as it loaded" : "it couldn't be loaded";
                }
                passed.Add(file + " (" + wrong + ")");

                // The game won't load a second core in the same start.
                if (isLoaded) break;
            }
            why = passed.Count == 0
                ? "Keel's core isn't in " + home + "."
                : "none of the cores in " + home + " could be used: " + string.Join(", ", passed.ToArray()) + ".";
            return null;
        }

        /// <summary>
        /// Every core in the Keel folder, newest first. Any other name, and any
        /// version SkipCores names, is passed over, with why.
        /// </summary>
        private static List<KeyValuePair<System.Version, string>> Newest(string home, List<string> skip, List<string> passed)
        {
            List<KeyValuePair<System.Version, string>> found = new List<KeyValuePair<System.Version, string>>();
            if (!Directory.Exists(home)) return found;
            foreach (string path in Directory.GetFiles(home, CoreName + "*.dll"))
            {
                string file = Path.GetFileName(path);
                System.Version v = Named(file);
                if (v == null) passed.Add(file + " (it isn't named like a core)");
                else if (skip.Contains(Three(v))) passed.Add(file + " (SkipCores names it)");
                else found.Add(new KeyValuePair<System.Version, string>(v, path));
            }
            found.Sort(delegate (KeyValuePair<System.Version, string> a, KeyValuePair<System.Version, string> b)
            {
                int newer = b.Key.CompareTo(a.Key);
                return newer != 0 ? newer : string.CompareOrdinal(a.Value, b.Value);
            });
            return found;
        }

        /// <summary>The versions SkipCores names in Keel.cfg, read the way the starter reads them.</summary>
        private static List<string> Skipped(string path)
        {
            List<string> skip = new List<string>();
            try
            {
                if (!File.Exists(path)) return skip;
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("#", StringComparison.Ordinal)) continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0 || !string.Equals(line.Substring(0, eq).Trim(), "SkipCores", StringComparison.OrdinalIgnoreCase))
                        continue;
                    foreach (string item in line.Substring(eq + 1).Split(new[] { ',' }))
                    {
                        string entry = item.Trim().Trim(new[] { '"' }).Trim();
                        if (entry.StartsWith(CoreName, StringComparison.OrdinalIgnoreCase)) entry = entry.Substring(CoreName.Length);
                        if (entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) entry = entry.Substring(0, entry.Length - 4);
                        System.Version v = Named(CoreName + entry + ".dll");
                        if (v != null) skip.Add(Three(v));
                    }
                }
            }
            catch (Exception)
            {
                // Can't read it, so nothing is passed over, as with the starter.
            }
            return skip;
        }

        /// <summary>
        /// The version in a core's file name, or null when the name isn't a
        /// core's: Keel.Core., three whole numbers with no leading zero, .dll.
        /// </summary>
        private static System.Version Named(string file)
        {
            if (file.Length <= CoreName.Length + 4
                || !file.StartsWith(CoreName, StringComparison.OrdinalIgnoreCase)
                || !file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                return null;
            string[] parts = file.Substring(CoreName.Length, file.Length - CoreName.Length - 4).Split(new[] { '.' });
            if (parts.Length != 3) return null;
            int[] numbers = new int[3];
            for (int i = 0; i < 3; i++)
            {
                string part = parts[i];
                if (part.Length == 0 || part.Length > 9 || (part.Length > 1 && part[0] == '0')) return null;
                int n = 0;
                foreach (char c in part)
                {
                    if (c < '0' || c > '9') return null;
                    n = n * 10 + (c - '0');
                }
                numbers[i] = n;
            }
            return new System.Version(numbers[0], numbers[1], numbers[2]);
        }

        private static bool Same(System.Version v, System.Version named)
        {
            return v != null && v.Major == named.Major && v.Minor == named.Minor && v.Build == named.Build;
        }

        private static string Three(System.Version v)
        {
            if (v == null) return "no version";
            return v.Major.ToString(CultureInfo.InvariantCulture) + "."
                 + v.Minor.ToString(CultureInfo.InvariantCulture) + "."
                 + Math.Max(v.Build, 0).ToString(CultureInfo.InvariantCulture);
        }
    }
}
