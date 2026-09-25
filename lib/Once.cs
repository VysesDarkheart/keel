using System;

namespace Keel
{
    /// <summary>
    /// Makes sure a mod starts only once, even when both starters are in the
    /// game (BepInEx and Keel's own) or there are two copies of the mod. The
    /// mark is kept in the AppDomain's own data, which every assembly in the
    /// game shares, however many copies of this code there are.
    /// </summary>
    internal static class Once
    {
        private const string Prefix = "Keel.Started.";

        /// <summary>
        /// True the first time a mod's id is claimed, false after that. What
        /// the first claim said is kept, so a second copy can say which copy
        /// is running.
        /// </summary>
        internal static bool Claim(Mod mod, string starter)
        {
            AppDomain domain = AppDomain.CurrentDomain;
            string key = Prefix + mod.Id;
            lock (typeof(AppDomain))
            {
                if (domain.GetData(key) != null) return false;
                domain.SetData(key, starter + " already started " + mod.Name + " " + mod.Version);
                return true;
            }
        }

        /// <summary>
        /// Why a copy of a mod stays off, as a sentence for a log: "Daywheel
        /// 1.0.14 stays off, because BepInEx already started Daywheel 1.0.13."
        /// </summary>
        internal static string StaysOff(Mod mod)
        {
            string who = AppDomain.CurrentDomain.GetData(Prefix + mod.Id) as string;
            return mod.Name + " " + mod.Version + " stays off, because "
                 + (who ?? "another copy is already running") + ".";
        }
    }
}
