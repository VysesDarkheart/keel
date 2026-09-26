using System;
using System.Collections.Generic;
using System.IO;

namespace Keel
{
    /// <summary>
    /// Where Keel comes in, when the mod was installed without BepInEx.
    /// Every version of Keel finds this class and method by name, so neither
    /// the name nor Start's signature may ever change.
    /// </summary>
    internal static class Entry
    {
        /// <summary>
        /// Starts the mod once, through Keel's core, with its settings in its
        /// own folder. Returns a sentence for Keel's log.
        /// </summary>
        public static string Start(string folder)
        {
            MainAttribute main = (MainAttribute)Attribute.GetCustomAttribute(
                typeof(Entry).Assembly, typeof(MainAttribute));
            if (main == null || main.Mod == null)
                return typeof(Entry).Assembly.GetName().Name + " doesn't name its Mod class with "
                     + "[assembly: Keel.Main(...)], so it wasn't started.";

            Mod mod = (Mod)Activator.CreateInstance(main.Mod, true);
            string why, loaded;
            IDictionary<string, object> core = Core.Find(folder, out why, out loaded);
            if (core == null) return mod.Name + " " + mod.Version + " wasn't started, because " + why;

            // What the core may want to know about this copy of Keel.
            Dictionary<string, object> about = new Dictionary<string, object>(StringComparer.Ordinal);
            about["keel"] = Core.Version;

            Func<string, string, string, string, IDictionary<string, object>, IDictionary<string, object>> start =
                (Func<string, string, string, string, IDictionary<string, object>, IDictionary<string, object>>)core["start"];
            IDictionary<string, object> state = start(mod.Id, mod.Name, mod.Version, folder, about);
            object run, reason, done;
            state.TryGetValue("run", out run);
            if (!(run is bool) || !(bool)run)
            {
                state.TryGetValue("reason", out reason);
                return (reason as string) ?? (mod.Name + " " + mod.Version + " stays off.");
            }

            mod.Start(new CoreHost(state));
            state.TryGetValue("done", out done);
            if (done is Action) ((Action)done)();
            return mod.Name + " " + mod.Version + " started." + (loaded == null ? string.Empty : " " + loaded);
        }
    }

    /// <summary>
    /// The host when Keel started the mod. The settings file and the log are
    /// the core's, so every Keel mod in a game keeps its settings the same
    /// way, the way of the newest core there.
    /// </summary>
    internal sealed class CoreHost : Host
    {
        private readonly string _starter;
        private readonly string _folder;
        private readonly Log _log;
        private readonly Func<string, string, Type, object, string, object, Delegate[]> _bind;
        private readonly Func<bool> _getSaving;
        private readonly Action<bool> _setSaving;

        internal CoreHost(IDictionary<string, object> state)
        {
            _starter = (string)state["starter"];
            _folder = (string)state["folder"];
            _log = new Log((Action<string>)state["info"], (Action<string>)state["warn"]);
            _bind = (Func<string, string, Type, object, string, object, Delegate[]>)state["bind"];
            _getSaving = (Func<bool>)state["getSaving"];
            _setSaving = (Action<bool>)state["setSaving"];
        }

        internal override string Starter { get { return _starter; } }

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
            get { return _getSaving(); }
            set { _setSaving(value); }
        }

        internal override Setting<T> Bind<T>(string section, string key, T fallback, string about)
        {
            return Wrap<T>(_bind(section, key, typeof(T), fallback, about, null));
        }

        internal override Setting<string> Bind(string section, string key, string fallback,
                                               string about, string[] allowed)
        {
            return Wrap<string>(_bind(section, key, typeof(string), fallback, about, allowed ?? new string[0]));
        }

        internal override Setting<float> Bind(string section, string key, float fallback,
                                              string about, float min, float max)
        {
            return Wrap<float>(_bind(section, key, typeof(float), fallback, about, new[] { min, max }));
        }

        private static Setting<T> Wrap<T>(Delegate[] made)
        {
            return new Setting<T>((Func<T>)made[0], (Action<T>)made[1]);
        }
    }
}
