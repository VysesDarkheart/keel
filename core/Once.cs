using System;

namespace Keel
{
    /// <summary>
    /// Makes sure a mod starts only once, even when both starters are in the
    /// game (BepInEx and Keel's own) or there are two copies of the mod. The
    /// mark is kept in the AppDomain's own data, which every assembly in the
    /// game shares. Every version of Keel writes and reads it the same way,
    /// so the key and the sentence in it never change.
    /// </summary>
    internal static class Once
    {
        private const string Prefix = "Keel.Started.";

        /// <summary>
        /// True the first time a mod's id is claimed, false after that. What
        /// the first claim said is kept, so a second copy can say which copy
        /// is running.
        /// </summary>
        internal static bool Claim(string id, string name, string version, string starter)
        {
            AppDomain domain = AppDomain.CurrentDomain;
            string key = Prefix + id;
            lock (typeof(AppDomain))
            {
                if (domain.GetData(key) != null) return false;
                domain.SetData(key, starter + " already started " + name + " " + version);
                return true;
            }
        }

        /// <summary>
        /// Why a copy of a mod stays off, as a sentence for a log: "Daywheel
        /// 1.0.14 stays off, because BepInEx already started Daywheel 1.0.13."
        /// </summary>
        internal static string StaysOff(string id, string name, string version)
        {
            string who = AppDomain.CurrentDomain.GetData(Prefix + id) as string;
            return name + " " + version + " stays off, because "
                 + (who ?? "another copy is already running") + ".";
        }
    }
}
