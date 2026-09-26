using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Keel
{
    /// <summary>
    /// What the core offers mods, published in the AppDomain's data where
    /// every mod can find it. Mods and the core share no Keel types: they
    /// talk only through types every .NET library has, so a mod built on any
    /// Keel 2 works with any later core, and there's never a second copy of
    /// a Keel type to clash with.
    ///
    /// Newer cores only add. A key, once published, keeps its name, its type
    /// and its meaning, and a new ability is a new key under a higher
    /// contract number.
    /// </summary>
    internal static class Services
    {
        /// <summary>The AppDomain data key the core is published under.</summary>
        internal const string Key = "Keel.Core";

        /// <summary>What this core offers: 1, as in Keel 2.0.</summary>
        internal const int Contract = 1;

        /// <summary>
        /// Publishes this core, unless one already is, and returns the one
        /// that's published. A mod that finds no core loads the newest itself
        /// and calls this by name, so neither the name nor the signature may
        /// ever change.
        /// </summary>
        internal static IDictionary<string, object> Publish()
        {
            AppDomain domain = AppDomain.CurrentDomain;
            lock (typeof(AppDomain))
            {
                IDictionary<string, object> published = domain.GetData(Key) as IDictionary<string, object>;
                if (published != null) return published;

                Dictionary<string, object> offer = new Dictionary<string, object>(StringComparer.Ordinal);
                offer["version"] = Loader.Version;
                offer["contract"] = Contract;
                offer["start"] = (Func<string, string, string, string, IDictionary<string, object>, IDictionary<string, object>>)Start;
                published = new ReadOnlyDictionary<string, object>(offer);
                domain.SetData(Key, published);
                return published;
            }
        }

        /// <summary>
        /// Makes a mod's state in the core: its settings file, read now, and
        /// what the mod needs to use it. The state is made before the mod is
        /// marked as started, so a state that fails leaves the way open for
        /// another copy. When the mod mustn't start, "run" is false and
        /// "reason" says why, for Keel's log; today that's only when another
        /// copy already started it.
        ///
        /// "about" is more about the mod, from the part of Keel inside it:
        /// "keel" is the Keel version it was built on. A core reads what it
        /// knows and leaves the rest, so later mods can say more.
        /// </summary>
        private static IDictionary<string, object> Start(string id, string name, string version, string folder,
                                                         IDictionary<string, object> about)
        {
            ModState state = new ModState(id, name, version, folder);
            Dictionary<string, object> s = new Dictionary<string, object>(StringComparer.Ordinal);
            if (!Once.Claim(id, name, version, "Keel"))
            {
                s["run"] = false;
                s["reason"] = Once.StaysOff(id, name, version);
                return new ReadOnlyDictionary<string, object>(s);
            }

            s["run"] = true;
            s["folder"] = folder;
            s["starter"] = "Keel";
            s["info"] = (Action<string>)state.Info;
            s["warn"] = (Action<string>)state.Warn;
            s["bind"] = (Func<string, string, Type, object, string, object, Delegate[]>)state.Bind;
            s["getSaving"] = (Func<bool>)state.GetSaving;
            s["setSaving"] = (Action<bool>)state.SetSaving;
            s["done"] = (Action)state.Done;
            state.Begun();
            return new ReadOnlyDictionary<string, object>(s);
        }
    }
}
