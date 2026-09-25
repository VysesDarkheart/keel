using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Keel
{
    /// <summary>
    /// Keel's starter. Doorstop runs it before the game starts. In order: hand
    /// the game to BepInEx first if BepInEx is installed in the same folder,
    /// wait for Unity's first scene, then start every mod kept in a folder
    /// beside this file, each at most once.
    ///
    /// Nothing in this class names a Unity or game type. It runs before Unity
    /// has loaded its own assemblies, and BepInEx swaps one of those for a
    /// changed copy while it starts. Asking for one early would load the
    /// unchanged copy first and leave BepInEx unable to start. Everything
    /// that needs Unity is in Scenes, which is only reached once Unity has
    /// loaded.
    /// </summary>
    internal static class Loader
    {
        internal const string Version = "1.0.0";

        /// <summary>What Doorstop starts BepInEx 5 and BepInEx 6 from.</summary>
        private static readonly string[] BepInExStarters =
        {
            "BepInEx.Preloader.dll",
            "BepInEx.Unity.Mono.Preloader.dll",
        };

        private static string _home;
        private static string _game;
        private static string _exe;
        private static bool _bepinex;
        private static int _listening;
        private static bool _resolving;
        private static readonly List<string> _mods = new List<string>();

        internal static void Begin()
        {
            try
            {
                // Doorstop's own record of where this file is. Assembly.Location
                // can garble a path with letters outside English in it.
                string self = Environment.GetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH");
                if (string.IsNullOrEmpty(self)) self = typeof(Loader).Assembly.Location;
                _home = Path.GetDirectoryName(Path.GetFullPath(self));
                _exe = Environment.GetEnvironmentVariable("DOORSTOP_PROCESS_PATH");
                _game = string.IsNullOrEmpty(_exe)
                    ? Directory.GetCurrentDirectory()
                    : Path.GetDirectoryName(Path.GetFullPath(_exe));

                Note.Open(Path.Combine(_home, "Keel.log"));
                Note.Line("Keel " + Version + " is starting in " + _game + ".");
                HandBack();
                Wait();
            }
            catch (Exception e)
            {
                Note.Line("Keel couldn't start: " + e);
            }
        }

        /// <summary>
        /// A game folder holds one Doorstop, which starts one file. When
        /// BepInEx is installed here too, Keel's files have taken its place,
        /// so Keel starts BepInEx the way Doorstop does, and BepInEx and its
        /// mods run as before. BepInEx 5.4.22 and later start from
        /// Doorstop.Entrypoint.Start(); older ones, made for Doorstop 3, from
        /// Main(string[]).
        /// </summary>
        private static void HandBack()
        {
            string core = Path.Combine(Path.Combine(_game, "BepInEx"), "core");
            foreach (string name in BepInExStarters)
            {
                string path = Path.Combine(core, name);
                if (!File.Exists(path)) continue;

                if (!StartBepInEx())
                {
                    Note.Line("BepInEx is installed here too, but Keel.cfg says not to start it, so it stays off.");
                    return;
                }
                Note.Line("BepInEx is installed here too, so Keel starts it first.");
                DateTime before = DateTime.UtcNow.AddSeconds(-2);
                try
                {
                    // BepInEx finds its own folder from this, the way it does
                    // when Doorstop starts it.
                    Environment.SetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH", path);
                    Assembly bepinex = Assembly.LoadFrom(path);
                    Type entry = bepinex.GetType("Doorstop.Entrypoint");
                    const BindingFlags any = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                    MethodInfo start = entry == null ? null : entry.GetMethod("Start", any, null, Type.EmptyTypes, null);
                    MethodInfo main = entry == null ? null : entry.GetMethod("Main", any, null, new[] { typeof(string[]) }, null);
                    if (start != null)
                    {
                        start.Invoke(null, null);
                    }
                    else if (main != null)
                    {
                        main.Invoke(null, new object[] { new[] { _exe ?? string.Empty } });
                    }
                    else
                    {
                        Note.Line("BepInEx's starter, " + path + ", has no entry point Keel knows, so BepInEx didn't start.");
                        return;
                    }

                    // BepInEx catches its own failures and writes them to a
                    // preloader_*.log beside the game. If one has just been
                    // written, BepInEx won't be running its plugins.
                    string trouble = NewPreloaderLog(before);
                    if (trouble != null)
                    {
                        Note.Line("BepInEx ran into a problem while starting, which it wrote to " + trouble
                                  + ", so Keel won't count on it running any mods.");
                        return;
                    }
                    _bepinex = true;
                    Note.Line("BepInEx started.");
                }
                catch (Exception e)
                {
                    Note.Line("BepInEx failed to start: " + Inner(e));
                }
                return;
            }
        }

        /// <summary>A preloader_*.log BepInEx wrote beside the game since the given time, or null.</summary>
        private static string NewPreloaderLog(DateTime since)
        {
            try
            {
                foreach (string file in Directory.GetFiles(_game, "preloader_*.log"))
                {
                    if (File.GetLastWriteTimeUtc(file) >= since) return file;
                }
            }
            catch (Exception)
            {
                // Can't look, so assume BepInEx is fine, as it usually is.
            }
            return null;
        }

        /// <summary>
        /// Keel's own settings, Keel.cfg beside Keel.dll, with one switch:
        /// whether to start BepInEx when it's installed in the game folder.
        /// A player who switched BepInEx off can keep it off. The file is
        /// written the first time BepInEx is found, in the same format as
        /// every mod's settings.
        /// </summary>
        private static bool StartBepInEx()
        {
            string path = Path.Combine(_home, "Keel.cfg");
            try
            {
                if (!File.Exists(path))
                {
                    string nl = Environment.NewLine;
                    File.WriteAllText(path,
                        "## Settings for Keel " + Version + nl
                        + nl
                        + "[BepInEx]" + nl
                        + nl
                        + "## Whether Keel starts BepInEx too, when BepInEx is installed in this game" + nl
                        + "## folder. If you've switched BepInEx off, set this to false and it stays off." + nl
                        + "# Setting type: Boolean" + nl
                        + "# Default value: true" + nl
                        + "StartBepInEx = true" + nl
                        + nl);
                    return true;
                }
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("#", StringComparison.Ordinal)) continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    if (!string.Equals(line.Substring(0, eq).Trim(), "StartBepInEx", StringComparison.OrdinalIgnoreCase))
                        continue;
                    string value = line.Substring(eq + 1).Trim().Trim('"').ToLowerInvariant();
                    return value != "false" && value != "no" && value != "off" && value != "0";
                }
            }
            catch (Exception e)
            {
                Note.Line("Keel couldn't read or write its settings in " + path + ", so it goes by the default: "
                          + e.Message);
            }
            return true;
        }

        /// <summary>
        /// Mods can only be started once Unity is running. Unity loads
        /// UnityEngine.CoreModule first, then its first scene, so Keel waits
        /// for the one and then listens for the other. When BepInEx started
        /// first, it has already loaded its copy of CoreModule.
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
                    Assembly mod = Assembly.LoadFrom(Path.Combine(folder, name + ".dll"));
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
        /// BepInEx sets it too. It's looked up by name so that Keel.dll never
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

        private static Assembly FromModFolders(object sender, ResolveEventArgs args)
        {
            try
            {
                string name = new AssemblyName(args.Name).Name;
                foreach (string folder in _mods)
                {
                    string dll = Path.Combine(folder, name + ".dll");
                    if (File.Exists(dll)) return Assembly.LoadFrom(dll);
                }
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
