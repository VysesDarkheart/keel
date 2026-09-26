using System;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using UnityEngine;

namespace Keel
{
    /// <summary>
    /// What the core keeps for a mod it started: its settings in [Name].cfg
    /// in the mod's own folder, read before the mod starts, and the game's
    /// own log. Only made once Unity is running, when the mod starts.
    /// </summary>
    internal sealed class ModState
    {
        private static readonly MethodInfo BindAsT =
            typeof(ModState).GetMethod("BindAs", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly string _name;
        private readonly string _seed;
        private readonly SettingsFile _settings;

        internal ModState(string id, string name, string version, string folder)
        {
            _name = name;
            string file = Path.Combine(folder, Safe(name) + ".cfg");

            // While the mod has no settings file of its own, the settings
            // BepInEx kept for it in the same game folder are the starting
            // point, so switching over keeps them. BepInEx's file is only
            // read, never changed.
            if (!File.Exists(file))
            {
                string bepinex = Path.Combine(Path.Combine(Path.Combine(GameFolder(folder), "BepInEx"), "config"),
                                              id + ".cfg");
                if (File.Exists(bepinex)) _seed = bepinex;
            }

            _settings = new SettingsFile(
                file,
                new[] { "Settings for " + name + " " + version, "Mod ID: " + id },
                delegate (string message) { Debug.LogWarning(name + ": " + message); },
                _seed);
        }

        /// <summary>Called once the mod is sure to start.</summary>
        internal void Begun()
        {
            if (_seed != null)
                Info(_name + " starts from the settings BepInEx kept in " + _seed
                     + ", and keeps its own from now on, in " + _settings.FilePath + ".");
        }

        internal void Info(string message)
        {
            Debug.Log(message);
        }

        internal void Warn(string message)
        {
            Debug.LogWarning(message);
        }

        internal bool GetSaving()
        {
            return _settings.Saving;
        }

        internal void SetSaving(bool on)
        {
            _settings.Saving = on;
        }

        /// <summary>
        /// The mod's Start has returned, so every setting is bound, and the
        /// file is written with all of them, whether or not any changed.
        /// </summary>
        internal void Done()
        {
            _settings.Save();
        }

        /// <summary>
        /// A setting of the given type. Limits are null, a string[] of the
        /// values a text setting may take, or a float[] of a number's low and
        /// high ends. Returns a Func of that type and an Action of it, so a
        /// setting read every frame never boxes.
        /// </summary>
        internal Delegate[] Bind(string section, string key, Type type, object fallback, string about, object limits)
        {
            if (type == null) throw new ArgumentNullException("type");
            try
            {
                return (Delegate[])BindAsT.MakeGenericMethod(type)
                    .Invoke(this, new[] { section, key, fallback, about, limits });
            }
            catch (TargetInvocationException e)
            {
                // The mod sees the fault itself, not reflection's wrapper.
                if (e.InnerException == null) throw;
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }

        private Delegate[] BindAs<T>(string section, string key, object fallback, string about, object limits)
        {
            Func<T, T> clamp = null;
            string lines = null;
            if (limits is string[] allowed)
            {
                if (typeof(T) != typeof(string))
                    throw new ArgumentException("[" + section + "] " + key + " can only have allowed values if it's text.");
                if (allowed.Length == 0)
                    throw new ArgumentException("[" + section + "] " + key + " needs at least one allowed value.");
                string[] list = (string[])allowed.Clone();
                Func<string, string> pick = delegate (string v) { return Array.IndexOf(list, v) >= 0 ? v : list[0]; };
                clamp = (Func<T, T>)(object)pick;
                lines = SettingsFile.ListLimits(list);
            }
            else if (limits is float[] range)
            {
                if (typeof(T) != typeof(float) || range.Length != 2)
                    throw new ArgumentException("[" + section + "] " + key
                                                + " can only have a range if it's a number, from a low end to a high end.");
                float min = range[0];
                float max = range[1];
                // BepInEx refuses such a range, so Keel does too.
                if (min.CompareTo(max) >= 0)
                    throw new ArgumentException("[" + section + "] " + key + "'s low end has to be lower than its high end.");
                Func<float, float> keep = delegate (float v)
                {
                    if (min.CompareTo(v) > 0) return min;
                    if (max.CompareTo(v) < 0) return max;
                    return v;
                };
                clamp = (Func<T, T>)(object)keep;
                lines = SettingsFile.RangeLimits(min, max);
            }
            else if (limits != null)
            {
                throw new ArgumentException("[" + section + "] " + key + " has limits of a kind Keel doesn't know.");
            }
            return _settings.Add(section, key, (T)fallback, about, clamp, lines);
        }

        /// <summary>
        /// A mod's name as a file name: a character the system won't take in
        /// one becomes _. Any other name is used as it is.
        /// </summary>
        private static string Safe(string name)
        {
            char[] bad = Path.GetInvalidFileNameChars();
            StringBuilder b = new StringBuilder(name.Length);
            foreach (char c in name) b.Append(Array.IndexOf(bad, c) >= 0 ? '_' : c);
            return b.ToString();
        }

        /// <summary>
        /// The game's folder: two folders up from the mod, which is where Keel's
        /// own layout puts it (Keel\Name, in the game folder), on every system.
        /// The game's program isn't always in it: on a Mac it sits deep inside
        /// valheim.app.
        /// </summary>
        private static string GameFolder(string folder)
        {
            string mod = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetDirectoryName(Path.GetDirectoryName(mod) ?? string.Empty) ?? string.Empty;
        }
    }
}
