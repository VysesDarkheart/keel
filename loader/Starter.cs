using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Keel
{
    /// <summary>
    /// Keel.dll, the one file Doorstop runs. It never changes after 2.0.0, so
    /// copying any Keel 2 mod into a game leaves it byte for byte as it was,
    /// and everything that may ever need fixing lives in the core instead.
    ///
    /// In order, it:
    ///
    /// 1. Begins Keel.log afresh, keeping the last one as Keel.previous.log,
    ///    and reads Keel.cfg.
    /// 2. Starts BepInEx, when BepInEx is installed in the same game folder,
    ///    the folder that holds the Keel folder. A game folder holds one
    ///    Doorstop, and Keel's took the place of BepInEx's, so Keel has to.
    ///    It's done here rather than in a core, so that a core that's missing
    ///    or broken can never stop BepInEx and the mods it runs.
    /// 3. Hands over to the newest core beside it, Keel.Core.X.Y.Z.dll, by
    ///    calling its Keel.Boot.Start, where the core sets itself up before
    ///    the game loads. A core that can't be read or loaded is passed over
    ///    for the next one down. A core that fails once it's loaded, or while
    ///    it's setting itself up, gets no second try that time: the game
    ///    won't load a second core in the same start, and a core that has
    ///    begun may have started things of its own. It's added to SkipCores
    ///    in Keel.cfg instead, so the next start passes over it. So is a core
    ///    the game stopped in twice in a row during the hand-over, which Keel
    ///    learns from Keel.starting, a file it writes just before the
    ///    hand-over and deletes once it's done. Only the hand-over is watched
    ///    here; a later core may watch its own later steps with a file of its
    ///    own.
    /// </summary>
    internal static class Starter
    {
        /// <summary>What Doorstop starts BepInEx 5 from, and BepInEx 6 from 6.0.0-pre.2 on.</summary>
        private static readonly string[] BepInExStarters =
        {
            "BepInEx.Preloader.dll",
            "BepInEx.Unity.Mono.Preloader.dll",
        };

        /// <summary>A core's file name is this, then its version, then .dll.</summary>
        private const string CoreName = "Keel.Core.";

        private static string _log;

        internal static void Begin()
        {
            try
            {
                // Doorstop's own record of where this file is. Assembly.Location
                // can garble a path with letters outside English in it.
                string self = Environment.GetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH");
                if (string.IsNullOrEmpty(self)) self = typeof(Starter).Assembly.Location;
                string home = Path.GetDirectoryName(Path.GetFullPath(self));
                string exe = Environment.GetEnvironmentVariable("DOORSTOP_PROCESS_PATH");
                // The game folder is the one that holds the Keel folder, on
                // every system. The game's program isn't always in it: on a
                // Mac it sits deep inside Valheim.app.
                string game = Path.GetDirectoryName(home);
                if (string.IsNullOrEmpty(game)) game = Directory.GetCurrentDirectory();

                Open(home);
                Line("Keel is starting in " + game + ".");

                string settings = Path.Combine(home, "Keel.cfg");
                bool startBepInEx;
                List<string> skip;
                Read(settings, out startBepInEx, out skip);

                // The core reads how this went, to know whether BepInEx is
                // running mods of its own.
                AppDomain.CurrentDomain.SetData("Keel.BepInEx", BepInEx(game, exe, startBepInEx));

                Run(home, settings, skip);
            }
            catch (Exception e)
            {
                // Nothing may escape into Doorstop, which would drop it without a word.
                Line("Keel couldn't start: " + Inner(e));
            }
        }

        /// <summary>
        /// Starts BepInEx the way Doorstop does, when it's installed in the game
        /// folder. BepInEx 5.4.23 and later, and BepInEx 6 from 6.0.0-pre.2 on,
        /// start from Doorstop.Entrypoint.Start(); BepInEx 5.1 to 5.4.22, made for
        /// Doorstop 3, from BepInEx.Preloader.Entrypoint.Main(); 5.0 from
        /// Main(string[]). BepInEx 6.0.0-pre.1 starts from
        /// BepInEx.Preloader.Unity.dll, which isn't looked for, so it stays off.
        /// Returns how it went, for the core: none, off, started, failed, or
        /// unknown when BepInEx is there but has no way in that this Keel.dll
        /// knows.
        /// </summary>
        private static string BepInEx(string game, string exe, bool start)
        {
            string core = Path.Combine(Path.Combine(game, "BepInEx"), "core");
            foreach (string name in BepInExStarters)
            {
                string path = Path.Combine(core, name);
                if (!File.Exists(path)) continue;

                if (!start)
                {
                    Line("BepInEx is installed here too, but Keel.cfg says not to start it, so it stays off.");
                    return "off";
                }
                Line("BepInEx is installed here too, so Keel starts it first.");
                DateTime before = DateTime.UtcNow.AddSeconds(-2);
                try
                {
                    // BepInEx finds its own folder from this, the way it does
                    // when Doorstop starts it.
                    Environment.SetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH", path);
                    Assembly bep = Assembly.LoadFrom(path);
                    Type entry = bep.GetType("Doorstop.Entrypoint") ?? bep.GetType("BepInEx.Preloader.Entrypoint");
                    const BindingFlags any = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                    MethodInfo begin = entry == null ? null : entry.GetMethod("Start", any, null, Type.EmptyTypes, null);
                    MethodInfo plain = entry == null ? null : entry.GetMethod("Main", any, null, Type.EmptyTypes, null);
                    MethodInfo main = entry == null ? null : entry.GetMethod("Main", any, null, new[] { typeof(string[]) }, null);
                    if (begin != null)
                    {
                        begin.Invoke(null, null);
                    }
                    else if (plain != null)
                    {
                        plain.Invoke(null, null);
                    }
                    else if (main != null)
                    {
                        main.Invoke(null, new object[] { new[] { exe ?? string.Empty } });
                    }
                    else
                    {
                        Line("BepInEx's starter, " + path + ", has no way in that Keel knows, so BepInEx didn't start.");
                        return "unknown";
                    }
                }
                catch (Exception e)
                {
                    Line("BepInEx failed to start: " + Inner(e));
                    return "failed";
                }

                // BepInEx catches its own failures and writes them to a
                // preloader_*.log: beside its program when its entry fails, and
                // in the game folder when its preloader does. On a Mac those
                // differ. If one has just been written, BepInEx won't be
                // running its plugins.
                string trouble = NewPreloaderLog(game, before) ?? NewPreloaderLog(ProgramFolder(exe), before);
                if (trouble != null)
                {
                    Line("BepInEx ran into a problem while starting, which it wrote to " + trouble
                         + ", so Keel won't count on it running any mods.");
                    return "failed";
                }
                Line("BepInEx started.");
                return "started";
            }
            return "none";
        }

        /// <summary>A preloader_*.log BepInEx wrote in the given folder since the given time, or null.</summary>
        private static string NewPreloaderLog(string folder, DateTime since)
        {
            try
            {
                foreach (string file in Directory.GetFiles(folder, "preloader_*.log"))
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

        /// <summary>The folder that holds the game's program, or null when that isn't known.</summary>
        private static string ProgramFolder(string exe)
        {
            try
            {
                return Path.GetDirectoryName(exe);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Hands over to the newest core that can run here.</summary>
        private static void Run(string home, string settings, List<string> skip)
        {
            int skipped;
            List<KeyValuePair<System.Version, string>> cores = Cores(home, skip, out skipped);
            string marker = Path.Combine(home, "Keel.starting");
            string stoppedIn;
            int stops = Stopped(marker, out stoppedIn);

            foreach (KeyValuePair<System.Version, string> core in cores)
            {
                string file = Path.GetFileName(core.Value);
                string version = Three(core.Key);

                // A hand-over that never finished leaves Keel.starting behind,
                // naming the core and how many starts in a row the game has
                // stopped in while Keel was handing over to it.
                int tries = string.Equals(file, stoppedIn, StringComparison.OrdinalIgnoreCase) ? stops : 0;
                if (tries >= 2)
                {
                    Line("Keel passed over " + file + ", because the game stopped twice in a row while Keel was handing over to it. "
                         + Added(settings, version, "the game stopped twice in a row while Keel was handing over to " + file));
                    Remove(marker);
                    continue;
                }
                Mark(marker, file, tries + 1);

                MethodInfo boot;
                bool loaded, handedBack;
                string why = Check(core.Value, core.Key, out boot, out loaded, out handedBack);
                if (why != null && !loaded)
                {
                    Remove(marker);
                    Line("Keel passed over " + file + ", because " + why + ".");
                    continue;
                }
                if (why != null)
                {
                    // The game won't load a second core in the same start, so
                    // this one goes without. A core the game already had came
                    // from somewhere else, so the fault isn't this file's, and
                    // it isn't passed over next time.
                    Remove(marker);
                    string ends = file + " can't run, because " + why + ", and no other core can load in the same start, "
                                + "so no Keel mods start this time.";
                    Line(handedBack ? ends : ends + " " + Added(settings, version, file + " couldn't run: " + why));
                    return;
                }

                Line("Keel hands over to " + file + ".");
                try
                {
                    boot.Invoke(null, new object[] { home });
                }
                catch (Exception e)
                {
                    Line(file + " failed while starting, so no Keel mods start this time. "
                         + Added(settings, version, file + " failed while starting") + " What went wrong: " + Inner(e));
                }
                finally
                {
                    Remove(marker);
                }
                return;
            }

            Line("Keel found no core it can run in " + home + ", so no Keel mods start."
                 + (skipped == 0
                     ? string.Empty
                     : " SkipCores in Keel.cfg passes over " + skipped.ToString(CultureInfo.InvariantCulture)
                       + (skipped == 1 ? " core here: take it out to try it again." : " cores here: take one out to try it again.")));
        }

        /// <summary>
        /// The cores beside this file, newest first. A core's name is Keel.Core.,
        /// then its version as three whole numbers, then .dll, such as
        /// Keel.Core.2.0.0.dll. Any other name is passed over, and so is any
        /// version SkipCores names; skipped counts those.
        /// </summary>
        private static List<KeyValuePair<System.Version, string>> Cores(string home, List<string> skip, out int skipped)
        {
            skipped = 0;
            List<KeyValuePair<System.Version, string>> found = new List<KeyValuePair<System.Version, string>>();
            string[] files;
            try
            {
                files = Directory.GetFiles(home, CoreName + "*.dll");
            }
            catch (Exception e)
            {
                Line("Keel couldn't look for cores in " + home + ": " + e.Message);
                return found;
            }
            foreach (string path in files)
            {
                string file = Path.GetFileName(path);
                System.Version v = Named(file);
                if (v == null)
                {
                    Line("Keel passed over " + file + ", because a core's name is Keel.Core., then a version such as 2.0.0, then .dll.");
                }
                else if (skip.Contains(Three(v)))
                {
                    skipped++;
                    Line("Keel passed over " + file + ", because SkipCores in Keel.cfg names it.");
                }
                else
                {
                    found.Add(new KeyValuePair<System.Version, string>(v, path));
                }
            }
            found.Sort(delegate (KeyValuePair<System.Version, string> a, KeyValuePair<System.Version, string> b)
            {
                int newer = b.Key.CompareTo(a.Key);
                return newer != 0 ? newer : string.CompareOrdinal(a.Value, b.Value);
            });
            return found;
        }

        /// <summary>
        /// The version in a core's file name, or null when the name isn't a
        /// core's. Plain digits only, with no leading zero, so each version has
        /// exactly one name.
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

        /// <summary>
        /// Why a core can't run here, or null, along with its way in. The name
        /// and version inside the file are read before it's loaded, so a file
        /// that isn't the core its name says is never run. loaded says whether
        /// the file got as far as being loaded, after which the game won't
        /// load another core in the same start. handedBack says the game handed
        /// back a core something else had loaded before Keel looked.
        /// </summary>
        private static string Check(string path, System.Version named, out MethodInfo boot, out bool loaded,
                                    out bool handedBack)
        {
            boot = null;
            loaded = false;
            handedBack = false;
            try
            {
                AssemblyName inside = AssemblyName.GetAssemblyName(path);
                if (inside.Name != "Keel.Core") return "it isn't one of Keel's cores";
                if (!Same(inside.Version, named))
                    return "its name says " + Three(named) + ", but the file itself is " + Three(inside.Version);
                Assembly core = Assembly.LoadFrom(path);
                loaded = true;
                // The game hands back a core it already has, whatever the file
                // asked for, once one with the same name is loaded. Keel loads
                // one core a start, so that one came from somewhere else.
                System.Version got = core.GetName().Version;
                if (!Same(got, named))
                {
                    handedBack = true;
                    return "the game already has core " + Three(got) + " loaded" + From(core);
                }
                Type type = core.GetType("Keel.Boot");
                if (type != null)
                    boot = type.GetMethod("Start", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                                          null, new[] { typeof(string) }, null);
                return boot == null ? "it has no way in that this Keel.dll knows" : null;
            }
            catch (Exception e)
            {
                return (loaded ? "it failed as it loaded: " : "it couldn't be loaded: ")
                     + Inner(e).Message.TrimEnd(new[] { '.' });
            }
        }

        private static bool Same(System.Version v, System.Version named)
        {
            return v != null && v.Major == named.Major && v.Minor == named.Minor && v.Build == named.Build;
        }

        /// <summary>Where a loaded assembly came from, as the end of a sentence, or nothing when that isn't known.</summary>
        private static string From(Assembly a)
        {
            try
            {
                string at = a.Location;
                return string.IsNullOrEmpty(at) ? string.Empty : ", from " + at;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// How many starts in a row Keel.starting says the game stopped in while
        /// Keel was handing over to a core, and which core; 0 when there's no
        /// such file or it can't be read. Its first line is the core's file name
        /// and that count; the rest explains the file to anyone who opens it and
        /// names the game that wrote it. When that game is another one started
        /// from this folder and still running, it may be handing over right now,
        /// so its own start isn't counted, only the ones before it.
        /// </summary>
        private static int Stopped(string marker, out string file)
        {
            file = null;
            try
            {
                if (!File.Exists(marker)) return 0;
                string[] lines = File.ReadAllLines(marker);
                if (lines.Length == 0) return 0;
                string[] parts = lines[0].Trim().Split(new[] { ' ' });
                int n;
                if (parts.Length != 2 || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out n))
                    return 0;
                // Another game started from this folder may be handing over
                // right now. Its own start may yet finish, so it isn't counted,
                // but the ones before it still are.
                for (int i = 1; i < lines.Length; i++)
                {
                    int pid;
                    string line = lines[i].Trim();
                    if (line.StartsWith("Process ", StringComparison.Ordinal)
                        && int.TryParse(line.Substring(8), NumberStyles.None, CultureInfo.InvariantCulture, out pid)
                        && Running(pid))
                    {
                        n = Math.Max(n - 1, 0);
                        break;
                    }
                }
                file = parts[0];
                return n;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>Writes Keel.starting just before Keel hands over to a core.</summary>
        private static void Mark(string marker, string file, int tries)
        {
            try
            {
                string nl = Environment.NewLine;
                string text = file + " " + tries.ToString(CultureInfo.InvariantCulture) + nl
                    + "Keel writes this file just before it hands over to a core, and deletes it once the core has" + nl
                    + "taken over, before the game loads. If it's still here and the process named below isn't running," + nl
                    + "the game stopped during that hand-over. After two such starts in a row, Keel passes over that" + nl
                    + "core and adds it to SkipCores in Keel.cfg." + nl;
                try
                {
                    text += ThisGame() + nl;
                }
                catch (Exception)
                {
                    // Without that line, a later start counts the file as a stop.
                }
                File.WriteAllText(marker, text);
            }
            catch (Exception)
            {
                // Without the file, this start just isn't counted.
            }
        }

        /// <summary>
        /// This game's line in Keel.starting, naming its process. It's a method
        /// of its own and never inlined, so if the game's libraries ever lacked
        /// Process, only the call to it would fail, inside Mark's try, and the
        /// file would still be written.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ThisGame()
        {
            using (Process self = Process.GetCurrentProcess())
                return "Process " + self.Id.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Whether another game with this process id is running now. False
        /// when that can't be told, so the file counts as a stop.
        /// </summary>
        private static bool Running(int pid)
        {
            try
            {
                return SameProgram(pid);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Whether the process with this id is another copy of this program,
        /// so an id used again by something else isn't taken for a game. Kept
        /// apart and never inlined, as ThisGame is, so Running catches anything
        /// it throws. Only the names' last parts are compared: on Linux the
        /// game's Mono names this process by its full path and another by its
        /// file name alone.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool SameProgram(int pid)
        {
            using (Process self = Process.GetCurrentProcess())
            {
                if (pid == self.Id || !Leader(pid)) return false;
                using (Process other = Process.GetProcessById(pid))
                    return string.Equals(Path.GetFileName(other.ProcessName), Path.GetFileName(self.ProcessName), StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// False when an id is one of a process's threads rather than the
        /// process itself: Linux numbers both from the same ids, so a thread,
        /// even one of this game's own, may have the id of a game that ended.
        /// True where there's no /proc to say.
        /// </summary>
        private static bool Leader(int pid)
        {
            string id = pid.ToString(CultureInfo.InvariantCulture);
            string status = "/proc/" + id + "/status";
            if (!File.Exists(status)) return true;
            foreach (string line in File.ReadAllLines(status))
                if (line.StartsWith("Tgid:", StringComparison.Ordinal))
                    return string.Equals(line.Substring(5).Trim(), id, StringComparison.Ordinal);
            return true;
        }

        /// <summary>
        /// Deletes Keel.starting once the hand-over is done, or at least empties
        /// it, which counts the same, so that it doesn't count against a core
        /// that took over. Something else may hold the file for a while, such
        /// as an antivirus checking it, so Keel tries both again, a little
        /// longer each time, for up to about a second. If both still fail, the
        /// next start may count this one, and Keel.log says so.
        /// </summary>
        private static void Remove(string marker)
        {
            int wait = 25;
            for (int i = 0; i < 6; i++)
            {
                if (i > 0)
                {
                    Thread.Sleep(wait);
                    wait *= 2;
                }
                try
                {
                    if (File.Exists(marker)) File.Delete(marker);
                    return;
                }
                catch (Exception)
                {
                    // Emptied instead, below.
                }
                try
                {
                    File.WriteAllText(marker, string.Empty);
                    return;
                }
                catch (Exception)
                {
                    // Both again in a moment, a little longer each time.
                }
            }
            Line("Keel couldn't delete or empty " + marker + ", so the next start may take this one for a start where the game stopped.");
        }

        /// <summary>
        /// Keel.cfg beside this file. The core writes it and players edit it,
        /// so it's read leniently: any section, any case, and quotes around a
        /// value are fine. StartBepInEx is off with false, no, off or 0, and
        /// the last line for it wins, as in any settings file. SkipCores lists
        /// versions, such as 2.1.0, or core file names, with commas between
        /// them, and every SkipCores line counts.
        /// </summary>
        private static void Read(string path, out bool startBepInEx, out List<string> skip)
        {
            startBepInEx = true;
            skip = new List<string>();
            try
            {
                if (!File.Exists(path)) return;
                foreach (string raw in File.ReadAllLines(path))
                {
                    string key, value;
                    if (!Setting(raw, out key, out value)) continue;
                    if (string.Equals(key, "StartBepInEx", StringComparison.OrdinalIgnoreCase))
                    {
                        string on = value.ToLowerInvariant();
                        startBepInEx = on != "false" && on != "no" && on != "off" && on != "0";
                    }
                    else if (string.Equals(key, "SkipCores", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (string item in value.Split(new[] { ',' }))
                        {
                            string entry = item.Trim().Trim(new[] { '"' }).Trim();
                            if (entry.Length == 0) continue;
                            System.Version v = Listed(entry);
                            if (v == null)
                                Line("SkipCores in Keel.cfg names " + entry + ", which isn't a version such as 2.1.0, so Keel ignores it.");
                            else if (!skip.Contains(Three(v)))
                                skip.Add(Three(v));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Line("Keel couldn't read " + path + ", so it goes by the defaults: " + e.Message);
            }
        }

        /// <summary>A key and its value from one line of Keel.cfg, or false for any other line.</summary>
        private static bool Setting(string raw, out string key, out string value)
        {
            key = null;
            value = null;
            string line = raw.Trim();
            if (line.StartsWith("#", StringComparison.Ordinal)) return false;
            int eq = line.IndexOf('=');
            if (eq < 0) return false;
            key = line.Substring(0, eq).Trim();
            value = line.Substring(eq + 1).Trim().Trim(new[] { '"' });
            return true;
        }

        /// <summary>A version from SkipCores, written as 2.1.0 or as the core's own file name.</summary>
        private static System.Version Listed(string entry)
        {
            if (entry.StartsWith(CoreName, StringComparison.OrdinalIgnoreCase)) entry = entry.Substring(CoreName.Length);
            if (entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) entry = entry.Substring(0, entry.Length - 4);
            return Named(CoreName + entry + ".dll");
        }

        /// <summary>Adds a version to SkipCores and says so, or says how to.</summary>
        private static string Added(string settings, string version, string reason)
        {
            return Skip(settings, version, reason)
                ? "Keel added " + version + " to SkipCores in Keel.cfg, so the next start passes over it."
                : "To pass over it, add " + version + " to SkipCores in Keel.cfg.";
        }

        /// <summary>
        /// Adds a SkipCores line to the end of Keel.cfg, with a note of when and
        /// why, in the file's own text encoding, and leaves every line already
        /// there as it was. True once it's there.
        /// </summary>
        private static bool Skip(string path, string version, string reason)
        {
            try
            {
                string nl = Environment.NewLine;
                bool exists = File.Exists(path);
                string text = (exists ? nl : "## Settings for Keel" + nl + nl)
                    + "## Keel added the line below on " + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    + ", because " + reason.Replace('\r', ' ').Replace('\n', ' ') + "." + nl
                    + "## Take that line out to try the core again." + nl
                    + "SkipCores = " + version + nl;
                File.AppendAllText(path, text, exists ? EncodingOf(path) : new UTF8Encoding(false));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// A file's text encoding, from the mark at its start, read the way the
        /// game's own readers take those marks, or UTF-8 when it has none.
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

        /// <summary>A version as Keel writes it: 2.0.0.</summary>
        private static string Three(System.Version v)
        {
            if (v == null) return "no version";
            return v.Major.ToString(CultureInfo.InvariantCulture) + "."
                 + v.Minor.ToString(CultureInfo.InvariantCulture) + "."
                 + Math.Max(v.Build, 0).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Begins Keel.log afresh, keeping the last one as Keel.previous.log, so
        /// the log of a start that went wrong survives the next try. The core
        /// carries the new one on.
        /// </summary>
        private static void Open(string home)
        {
            string path = Path.Combine(home, "Keel.log");
            try
            {
                if (File.Exists(path)) File.Copy(path, Path.Combine(home, "Keel.previous.log"), true);
            }
            catch (Exception)
            {
                // Losing the last log is no reason to stop.
            }
            try
            {
                File.WriteAllText(path, string.Empty);
                _log = path;
            }
            catch (Exception)
            {
                _log = null;
            }
        }

        /// <summary>A line in Keel.log, written at once, so a game that stops part way still leaves it.</summary>
        private static void Line(string text)
        {
            if (_log == null) return;
            try
            {
                File.AppendAllText(_log, DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + text
                                         + Environment.NewLine);
            }
            catch (Exception)
            {
                // A log that can't be written is no reason to stop the game.
            }
        }

        private static Exception Inner(Exception e)
        {
            return e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
        }
    }
}
