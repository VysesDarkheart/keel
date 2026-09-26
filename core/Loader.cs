using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Keel
{
    /// <summary>
    /// What the core does when the starter hands over, before the game
    /// starts. In order: learn whether the starter started BepInEx, make sure
    /// Keel.cfg holds Keel's settings, offer the core to mods, then wait for
    /// Unity's first scene and start every mod kept in a folder of its own in
    /// the Keel folder, each at most once.
    ///
    /// Nothing in this class names a Unity or game type. It runs before Unity
    /// has loaded its own assemblies, and BepInEx swaps one of those for a
    /// changed copy while it starts. Asking for one early would load the
    /// unchanged copy first and leave BepInEx unable to start. Everything
    /// that needs Unity is in Scenes and ModState, which are only reached once
    /// Unity has loaded.
    /// </summary>
    internal static class Loader
    {
        /// <summary>Keel's version, which is this core's: 2.0.0, from the project file.</summary>
        internal static readonly string Version = Three(typeof(Loader).Assembly.GetName().Version);

        private static string _home;
        private static string _game;
        private static bool _bepinex;
        private static int _listening;
        private static bool _resolving;
        private static bool _sharedLoaded;
        private static readonly List<string> _mods = new List<string>();

        /// <summary>Called once, from Boot, with the Keel folder the starter is in.</summary>
        internal static void Begin(string home)
        {
            try
            {
                _home = home;
                // The game folder is the one that holds the Keel folder, on
                // every system. On a Mac the game's program sits deep inside
                // valheim.app, so its folder isn't the game folder.
                _game = Path.GetDirectoryName(Path.GetFullPath(home));
                if (string.IsNullOrEmpty(_game)) _game = Directory.GetCurrentDirectory();

                Note.Open(Path.Combine(_home, "Keel.log"));
                Note.Line("Keel " + Version + " is running" + Fingerprint() + ".");

                // The starter starts BepInEx, when it's here, before any core
                // runs, and leaves word of how that went.
                _bepinex = (AppDomain.CurrentDomain.GetData("Keel.BepInEx") as string) == "started";

                Settings();
                try
                {
                    Services.Publish();
                }
                catch (Exception e)
                {
                    // Only Keel's own mods go without, and each says so when
                    // it tries.
                    Note.Line("Keel couldn't offer its core to mods: " + e.Message);
                }
                Wait();
            }
            catch (Exception e)
            {
                Note.Line("Keel couldn't start: " + e);
            }
        }

        /// <summary>A version as Keel writes it: 2.0.0.</summary>
        internal static string Three(System.Version v)
        {
            return v == null ? "?" : v.Major + "." + v.Minor + "." + Math.Max(v.Build, 0);
        }

        /// <summary>
        /// Which file this core is, and its SHA-256, so Keel.log shows exactly
        /// which core ran. The starter only runs a core whose file is named
        /// after its version, but the game can hand back a core of the same
        /// version that something else loaded from another file first, so the
        /// file this code really came from is the one named and hashed.
        /// </summary>
        private static string Fingerprint()
        {
            string file = "Keel.Core." + Version + ".dll";
            string own = Path.Combine(_home, file);
            string at = null;
            try
            {
                at = typeof(Loader).Assembly.Location;
            }
            catch (Exception)
            {
                // Not known, so the Keel folder's file is named, as usual.
            }
            string named = file;
            string path = own;
            if (at != null && at.Length == 0)
            {
                return ", which wasn't loaded from a file";
            }
            if (!string.IsNullOrEmpty(at))
            {
                try
                {
                    if (!string.Equals(Path.GetFullPath(at), Path.GetFullPath(own), StringComparison.OrdinalIgnoreCase))
                    {
                        named = at;
                        path = at;
                    }
                }
                catch (Exception)
                {
                    // A path that can't be compared: the Keel folder's file is named.
                }
            }
            try
            {
                byte[] hash;
                using (SHA256 sha = SHA256.Create()) hash = sha.ComputeHash(File.ReadAllBytes(path));
                StringBuilder b = new StringBuilder(hash.Length * 2);
                foreach (byte x in hash) b.Append(x.ToString("x2", CultureInfo.InvariantCulture));
                return ", from " + named + " (SHA-256 " + b + ")";
            }
            catch (Exception)
            {
                return ", from " + named;
            }
        }

        /// <summary>
        /// Keel's own settings, Keel.cfg beside Keel.dll, which the starter
        /// reads before any core runs: whether to start BepInEx, and which
        /// cores to pass over. Written the first time; a setting missing from
        /// it is added at the end, and nothing a player wrote is changed.
        /// </summary>
        private static void Settings()
        {
            string path = Path.Combine(_home, "Keel.cfg");
            try
            {
                string text = File.Exists(path) ? File.ReadAllText(path) : null;
                bool bepinex = false;
                bool skip = false;
                if (text != null)
                {
                    // Split the way the starter reads it: a line may end in
                    // \r\n, \n or a lone \r.
                    foreach (string raw in text.Split(new[] { '\r', '\n' }))
                    {
                        string line = raw.Trim();
                        if (line.StartsWith("#", StringComparison.Ordinal)) continue;
                        int eq = line.IndexOf('=');
                        if (eq < 0) continue;
                        string key = line.Substring(0, eq).Trim();
                        if (string.Equals(key, "StartBepInEx", StringComparison.OrdinalIgnoreCase)) bepinex = true;
                        else if (string.Equals(key, "SkipCores", StringComparison.OrdinalIgnoreCase)) skip = true;
                    }
                }
                if (bepinex && skip) return;

                string nl = Environment.NewLine;
                StringBuilder b = new StringBuilder(1024);
                if (text == null)
                {
                    b.Append("## Settings for Keel").Append(nl).Append(nl);
                }
                else
                {
                    // One blank line between what's there and what's added.
                    int breaks = 0;
                    for (int i = text.TrimEnd(new[] { '\r', '\n', ' ', '\t' }).Length; i < text.Length; i++)
                    {
                        if (text[i] == '\n' || (text[i] == '\r' && (i + 1 == text.Length || text[i + 1] != '\n'))) breaks++;
                    }
                    if (text.Trim().Length > 0 && breaks < 2) b.Append(breaks == 0 ? nl + nl : nl);
                }
                if (!bepinex)
                {
                    b.Append("[BepInEx]").Append(nl).Append(nl)
                     .Append("## Whether Keel starts BepInEx too, when BepInEx is installed in this game").Append(nl)
                     .Append("## folder. If you've switched BepInEx off, set this to false and it stays off.").Append(nl)
                     .Append("# Setting type: Boolean").Append(nl)
                     .Append("# Default value: true").Append(nl)
                     .Append("StartBepInEx = true").Append(nl);
                    if (!skip) b.Append(nl);
                }
                if (!skip)
                {
                    b.Append("[Cores]").Append(nl).Append(nl)
                     .Append("## Versions of Keel's core to pass over, with commas between them, such as").Append(nl)
                     .Append("## 2.1.0. Keel runs the newest core that isn't listed. It adds a core here").Append(nl)
                     .Append("## itself, with a note saying when and why, when the core fails as Keel hands").Append(nl)
                     .Append("## over to it, or the game stops twice in a row during that hand-over.").Append(nl)
                     .Append("# Setting type: String").Append(nl)
                     .Append("SkipCores =").Append(nl);
                }
                File.AppendAllText(path, b.ToString(), text == null ? new UTF8Encoding(false) : EncodingOf(path));
            }
            catch (Exception e)
            {
                Note.Line("Keel couldn't write its settings in " + path + ": " + e.Message);
            }
        }

        /// <summary>
        /// A file's text encoding, from the mark at its start, or UTF-8 when it
        /// has none, so what's added to a file a player saved in another
        /// encoding still reads.
        /// </summary>
        private static Encoding EncodingOf(string path)
        {
            byte[] head = new byte[4];
            int n;
            using (FileStream f = File.OpenRead(path)) n = f.Read(head, 0, 4);
            if (n >= 4 && head[0] == 0xFF && head[1] == 0xFE && head[2] == 0 && head[3] == 0) return new UTF32Encoding(false, false);
            if (n >= 4 && head[0] == 0 && head[1] == 0 && head[2] == 0xFE && head[3] == 0xFF) return new UTF32Encoding(true, false);
            if (n >= 2 && head[0] == 0xFF && head[1] == 0xFE) return new UnicodeEncoding(false, false);
            if (n >= 2 && head[0] == 0xFE && head[1] == 0xFF) return new UnicodeEncoding(true, false);
            return new UTF8Encoding(false);
        }

        /// <summary>
        /// Mods can only be started once Unity is running. Unity loads
        /// UnityEngine.CoreModule first, then its first scene, so Keel waits
        /// for the one and then listens for the other. When the starter started
        /// BepInEx, BepInEx has already loaded its copy of CoreModule.
        /// </summary>
        private static void Wait()
        {
            AppDomain.CurrentDomain.AssemblyLoad += Loaded;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (IsUnity(a))
                {
                    Listen();
                    return;
                }
            }
            Note.Line("Waiting for the game to load.");
        }

        private static void Loaded(object sender, AssemblyLoadEventArgs args)
        {
            if (IsUnity(args.LoadedAssembly)) Listen();
        }

        private static bool IsUnity(Assembly a)
        {
            try
            {
                return a.GetName().Name == "UnityEngine.CoreModule";
            }
            catch (Exception)
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Listen()
        {
            if (Interlocked.Exchange(ref _listening, 1) != 0) return;
            AppDomain.CurrentDomain.AssemblyLoad -= Loaded;
            try
            {
                Scenes.Listen();
                Note.Line("The game is loading. Mods will start with its first scene.");
            }
            catch (Exception e)
            {
                Note.Line("Keel couldn't watch for the game's first scene, so no mods will start: " + e);
            }
        }

        /// <summary>
        /// Starts every mod in a folder beside Keel.dll. A mod lives in a
        /// folder of its own, named after its DLL: Keel\Name\Name.dll.
        /// Called on the game's main thread, from Scenes.
        /// </summary>
        internal static void StartMods(string scene)
        {
            Note.Line("The first scene, " + scene + ", has loaded.");
            string[] folders;
            try
            {
                folders = Directory.GetDirectories(_home);
            }
            catch (Exception e)
            {
                Note.Line("Keel couldn't look in " + _home + ": " + e.Message);
                return;
            }
            Array.Sort(folders, StringComparer.OrdinalIgnoreCase);

            _mods.Clear();
            foreach (string folder in folders)
            {
                string dll = Path.Combine(folder, Path.GetFileName(folder) + ".dll");
                if (File.Exists(dll)) _mods.Add(folder);
            }
            if (_mods.Count == 0)
            {
                Note.Line("There are no mods in " + _home + ".");
                return;
            }

            // A mod may bring DLLs of its own, kept in its folder.
            if (!_resolving)
            {
                _resolving = true;
                AppDomain.CurrentDomain.AssemblyResolve += FromModFolders;
            }
            LoadShared();

            // A mod that BepInEx has its own copy of is left to BepInEx, even
            // an old copy from before Keel, which wouldn't know to step aside.
            HashSet<string> bepinexHas = _bepinex ? BepInExPlugins() : new HashSet<string>();

            List<KeyValuePair<string, MethodInfo>> starts = new List<KeyValuePair<string, MethodInfo>>();
            foreach (string folder in _mods)
            {
                string name = Path.GetFileName(folder);
                if (bepinexHas.Contains(name + ".dll"))
                {
                    Note.Line(name + " is in BepInEx's plugins folder too, so BepInEx runs that copy and this one stays off.");
                    continue;
                }
                try
                {
                    string dll = Path.Combine(folder, name + ".dll");
                    Assembly mod = Assembly.LoadFrom(dll);
                    // The game hands back an assembly it already has, from
                    // wherever it came, when one with the same name is
                    // loaded: an old copy BepInEx started under another file
                    // name, say.
                    string at = mod.Location;
                    if (!string.IsNullOrEmpty(at)
                        && !string.Equals(Path.GetFullPath(at), Path.GetFullPath(dll), StringComparison.OrdinalIgnoreCase))
                    {
                        Note.Line(name + " is already loaded from " + at + ", so the copy in " + folder + " stays off.");
                        continue;
                    }
                    Type entry = mod.GetType("Keel.Entry");
                    MethodInfo start = entry == null
                        ? null
                        : entry.GetMethod("Start", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                                          null, new[] { typeof(string) }, null);
                    if (start == null)
                    {
                        Note.Line(name + ".dll isn't built on Keel, so it was left alone.");
                        continue;
                    }
                    starts.Add(new KeyValuePair<string, MethodInfo>(folder, start));
                }
                catch (Exception e)
                {
                    Note.Line(name + " couldn't be loaded: " + Inner(e));
                }
            }

            if (starts.Count > 0) Modded();
            foreach (KeyValuePair<string, MethodInfo> start in starts)
            {
                string name = Path.GetFileName(start.Key);
                try
                {
                    Note.Line((string)start.Value.Invoke(null, new object[] { start.Key }));
                }
                catch (Exception e)
                {
                    Note.Line(name + " failed to start: " + Inner(e));
                }
            }
        }

        /// <summary>The names of every DLL in BepInEx's plugins folder and the folders below it.</summary>
        private static HashSet<string> BepInExPlugins()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string plugins = Path.Combine(Path.Combine(_game, "BepInEx"), "plugins");
            if (Directory.Exists(plugins)) Collect(plugins, names, 0);
            return names;
        }

        /// <summary>
        /// One folder at a time, so a folder that can't be read is skipped on
        /// its own instead of hiding every other plugin.
        /// </summary>
        private static void Collect(string folder, HashSet<string> names, int depth)
        {
            try
            {
                foreach (string file in Directory.GetFiles(folder, "*.dll"))
                    names.Add(Path.GetFileName(file));
            }
            catch (Exception e)
            {
                Note.Line("Keel couldn't look in " + folder + ": " + e.Message);
            }
            if (depth >= 16) return;
            string[] below;
            try
            {
                below = Directory.GetDirectories(folder);
            }
            catch (Exception)
            {
                return;
            }
            foreach (string sub in below) Collect(sub, names, depth + 1);
        }

        /// <summary>
        /// The game asks every mod to set Game.isModded, which shows "modded"
        /// in its main menu and helps its makers with support requests.
        /// BepInEx sets it too. It's looked up by name so that the core never
        /// depends on the game's code.
        /// </summary>
        private static void Modded()
        {
            try
            {
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (a.GetName().Name != "assembly_valheim") continue;
                    Type game = a.GetType("Game");
                    FieldInfo flag = game == null
                        ? null
                        : game.GetField("isModded", BindingFlags.Static | BindingFlags.Public);
                    if (flag == null || flag.FieldType != typeof(bool))
                    {
                        Note.Line("The game has no isModded flag, so its menu won't say it's modded.");
                        return;
                    }
                    flag.SetValue(null, true);
                    Note.Line("Told the game that it's modded.");
                    return;
                }
                Note.Line("The game's code wasn't loaded, so Keel couldn't tell it that it's modded.");
            }
            catch (Exception e)
            {
                Note.Line("Keel couldn't tell the game that it's modded: " + e.Message);
            }
        }

        /// <summary>
        /// A DLL that two or more mods keep in their own folders loads once, at
        /// its highest version, before any mod loads. The game hands the first
        /// copy of a DLL it has to every mod that asks for that name, whatever
        /// version it asks for, so loading the highest first is what gives every
        /// mod that one. A mod's own DLL, another mod's, and Keel's own files
        /// are never loaded this way.
        /// </summary>
        private static void LoadShared()
        {
            if (_sharedLoaded) return;
            _sharedLoaded = true;
            HashSet<string> own = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in _mods) own.Add(Path.GetFileName(folder) + ".dll");
            SortedDictionary<string, List<string>> kept =
                new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in _mods)
            {
                string[] dlls;
                try
                {
                    dlls = Directory.GetFiles(folder, "*.dll");
                }
                catch (Exception)
                {
                    continue;
                }
                foreach (string dll in dlls)
                {
                    string name = Path.GetFileName(dll);
                    if (own.Contains(name) || string.Equals(name, "Keel.dll", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("Keel.Core.", StringComparison.OrdinalIgnoreCase))
                        continue;
                    List<string> copies;
                    if (!kept.TryGetValue(name, out copies))
                    {
                        copies = new List<string>();
                        kept[name] = copies;
                    }
                    copies.Add(dll);
                }
            }
            foreach (KeyValuePair<string, List<string>> shared in kept)
            {
                if (shared.Value.Count < 2) continue;
                string best = null;
                System.Version bestVersion = null;
                foreach (string dll in shared.Value)
                {
                    System.Version v;
                    try
                    {
                        v = AssemblyName.GetAssemblyName(dll).Version;
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    if (best == null || (v != null && (bestVersion == null || v.CompareTo(bestVersion) > 0)))
                    {
                        best = dll;
                        bestVersion = v;
                    }
                }
                if (best == null) continue;
                try
                {
                    Assembly.LoadFrom(best);
                    Note.Line(shared.Key + " is in more than one mod's folder, so Keel loads its highest version, "
                              + (bestVersion == null ? "which has none" : bestVersion.ToString()) + ", from "
                              + Path.GetDirectoryName(best) + ", for all of them.");
                }
                catch (Exception e)
                {
                    Note.Line("Keel couldn't load " + best + ", which more than one mod keeps: " + Inner(e).Message);
                }
            }
        }

        /// <summary>
        /// A DLL a mod asks for that the game couldn't find: one the asking mod
        /// doesn't keep in its own folder, and that isn't loaded yet. The game
        /// looks in the asking mod's folder and at what's loaded first, and asks
        /// here only after that. The highest version any mod keeps loads.
        /// </summary>
        private static Assembly FromModFolders(object sender, ResolveEventArgs args)
        {
            try
            {
                string name = new AssemblyName(args.Name).Name;
                string best = null;
                System.Version bestVersion = null;
                foreach (string folder in _mods)
                {
                    string dll = Path.Combine(folder, name + ".dll");
                    if (!File.Exists(dll)) continue;
                    System.Version v;
                    try
                    {
                        v = AssemblyName.GetAssemblyName(dll).Version;
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    if (best == null || (v != null && (bestVersion == null || v.CompareTo(bestVersion) > 0)))
                    {
                        best = dll;
                        bestVersion = v;
                    }
                }
                if (best != null) return Assembly.LoadFrom(best);
            }
            catch (Exception)
            {
                // Not found here; whoever asked tries elsewhere.
            }
            return null;
        }

        private static Exception Inner(Exception e)
        {
            return e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
        }
    }
}
