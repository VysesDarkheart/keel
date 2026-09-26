using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace Keel
{
    /// <summary>
    /// Starts a mod that BepInEx has loaded, with BepInEx's own settings file
    /// and log, so mod managers and BepInEx's config tools see it as usual.
    /// This is the only part of Keel that uses BepInEx, and it only runs when
    /// BepInEx calls the mod's plugin.
    /// </summary>
    internal static class Bep
    {
        /// <summary>Called from the mod's BepInEx plugin, in Awake.</summary>
        internal static void Start(Mod mod, ConfigFile config, ManualLogSource log)
        {
            BepHost host = new BepHost(mod, config, log);
            if (!Once.Claim(mod, "BepInEx"))
            {
                log.LogInfo(Once.StaysOff(mod));
                return;
            }
            mod.Start(host);
        }

        private sealed class BepHost : Host
        {
            private readonly Mod _mod;
            private readonly ConfigFile _config;
            private readonly Log _log;

            internal BepHost(Mod mod, ConfigFile config, ManualLogSource log)
            {
                _mod = mod;
                _config = config;
                _log = new Log(log.LogInfo, log.LogWarning);
            }

            internal override string Starter { get { return "BepInEx"; } }

            internal override string Folder
            {
                get
                {
                    string folder = Path.Combine(Paths.ConfigPath, _mod.Name);
                    Directory.CreateDirectory(folder);
                    return folder;
                }
            }

            internal override Log Log { get { return _log; } }

            internal override bool Saving
            {
                get { return _config.SaveOnConfigSet; }
                set
                {
                    if (_config.SaveOnConfigSet == value) return;
                    _config.SaveOnConfigSet = value;
                    if (value) _config.Save();
                }
            }

            internal override Setting<T> Bind<T>(string section, string key, T fallback, string about)
            {
                // The same types as Keel's own file, so a mod behaves the same
                // under either starter.
                if (!Types.Supported(typeof(T)))
                    throw new NotSupportedException("Keel can't keep a setting of type " + typeof(T).Name + ".");
                ConfigEntry<T> entry = _config.Bind(section, key, fallback, about);
                return new Setting<T>(delegate { return entry.Value; }, delegate (T v) { entry.Value = v; });
            }

            internal override Setting<string> Bind(string section, string key, string fallback,
                                                   string about, string[] allowed)
            {
                ConfigEntry<string> entry = _config.Bind(section, key, fallback,
                    new ConfigDescription(about, new AcceptableValueList<string>(allowed)));
                return new Setting<string>(delegate { return entry.Value; }, delegate (string v) { entry.Value = v; });
            }

            internal override Setting<float> Bind(string section, string key, float fallback,
                                                  string about, float min, float max)
            {
                ConfigEntry<float> entry = _config.Bind(section, key, fallback,
                    new ConfigDescription(about, new AcceptableValueRange<float>(min, max)));
                return new Setting<float>(delegate { return entry.Value; }, delegate (float v) { entry.Value = v; });
            }
        }
    }
}
