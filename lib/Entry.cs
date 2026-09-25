using System;
using System.IO;
using UnityEngine;

namespace Keel
{
    /// <summary>
    /// Where Keel's starter comes in, when the mod was installed without
    /// BepInEx. The starter finds this class and method by name, so neither
    /// the name nor Start's signature may ever change.
    /// </summary>
    internal static class Entry
    {
        /// <summary>
        /// Starts the mod once, with its settings in its own folder. Returns
        /// a sentence for Keel's log.
        /// </summary>
        public static string Start(string folder)
        {
            MainAttribute main = (MainAttribute)Attribute.GetCustomAttribute(
                typeof(Entry).Assembly, typeof(MainAttribute));
            if (main == null || main.Mod == null)
                return typeof(Entry).Assembly.GetName().Name + " doesn't name its Mod class with "
                     + "[assembly: Keel.Main(...)], so it wasn't started.";

            Mod mod = (Mod)Activator.CreateInstance(main.Mod, true);
            // The host is made before the mod is marked as started, so a
            // host that fails leaves the way open for another copy.
            Alone host = new Alone(mod, folder);
            if (!Once.Claim(mod, "Keel")) return Once.StaysOff(mod);

            if (host.Seed != null)
                host.Log.Info(mod.Name + " starts from the settings BepInEx kept in " + host.Seed
                              + ", and keeps its own from now on, in " + host.Settings.FilePath + ".");
            mod.Start(host);
            // Every setting is bound now, so the file is written with all of
            // them, whether or not any changed.
            host.Settings.Save();
            return mod.Name + " " + mod.Version + " started.";
        }
    }

    /// <summary>
    /// The host when Keel started the mod: settings in [Name].cfg in the
    /// mod's own folder, and the game's own log.
    /// </summary>
    internal sealed class Alone : Host
    {
        private readonly string _folder;
        private readonly Log _log;

        internal Alone(Mod mod, string folder)
        {
            _folder = folder;
            _log = new Log(Debug.Log, Debug.LogWarning);
            string file = Path.Combine(folder, mod.Name + ".cfg");

            // While the mod has no settings file of its own, the settings
            // BepInEx kept for it in the same game folder are the starting
            // point, so switching over keeps them. BepInEx's file is only
            // read, never changed.
            string seed = null;
            if (!File.Exists(file))
            {
                string bepinex = Path.Combine(Path.Combine(Path.Combine(GameFolder(folder), "BepInEx"), "config"),
                                              mod.Id + ".cfg");
                if (File.Exists(bepinex)) seed = bepinex;
            }

            Seed = seed;
            Settings = new SettingsFile(
                file,
                new[] { "Settings for " + mod.Name + " " + mod.Version, "Mod ID: " + mod.Id },
                delegate (string message) { _log.Warn(mod.Name + ": " + message); },
                seed);
        }

        internal SettingsFile Settings { get; }

        /// <summary>BepInEx's settings file the settings were first read from, or null.</summary>
        internal string Seed { get; }

        /// <summary>
        /// The game's folder: where Doorstop says the game is, or else two
        /// folders up from the mod, which is where Keel's own layout puts it.
        /// </summary>
        private static string GameFolder(string folder)
        {
            string exe = Environment.GetEnvironmentVariable("DOORSTOP_PROCESS_PATH");
            if (!string.IsNullOrEmpty(exe)) return Path.GetDirectoryName(Path.GetFullPath(exe)) ?? string.Empty;
            string mod = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetDirectoryName(Path.GetDirectoryName(mod) ?? string.Empty) ?? string.Empty;
        }

        internal override string Starter { get { return "Keel"; } }

        internal override string Folder
        {
            get
            {
                Directory.CreateDirectory(_folder);
                return _folder;
            }
        }

        internal override Log Log { get { return _log; } }

        internal override bool Saving
        {
            get { return Settings.Saving; }
            set { Settings.Saving = value; }
        }

        internal override Setting<T> Bind<T>(string section, string key, T fallback, string about)
        {
            return Settings.Add(section, key, fallback, about, null, null);
        }

        internal override Setting<string> Bind(string section, string key, string fallback,
                                               string about, string[] allowed)
        {
            if (allowed == null || allowed.Length == 0)
                throw new ArgumentException("[" + section + "] " + key + " needs at least one allowed value.");
            string[] list = (string[])allowed.Clone();
            return Settings.Add(section, key, fallback, about,
                                delegate (string v) { return Array.IndexOf(list, v) >= 0 ? v : list[0]; },
                                SettingsFile.ListLimits(list));
        }

        internal override Setting<float> Bind(string section, string key, float fallback,
                                              string about, float min, float max)
        {
            return Settings.Add(section, key, fallback, about,
                                delegate (float v)
                                {
                                    if (min.CompareTo(v) > 0) return min;
                                    if (max.CompareTo(v) < 0) return max;
                                    return v;
                                },
                                SettingsFile.RangeLimits(min, max));
        }
    }
}
