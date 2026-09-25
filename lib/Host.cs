using System;

namespace Keel
{
    /// <summary>
    /// What a mod gets from whichever starter ran it. Under BepInEx, settings
    /// live in BepInEx's config folder and the log is BepInEx's. Started by
    /// Keel, settings live beside the mod and the log is the game's own.
    /// </summary>
    internal abstract class Host
    {
        /// <summary>Who started the mod: "BepInEx" or "Keel".</summary>
        internal abstract string Starter { get; }

        /// <summary>A folder for the mod's own files. It's made the first time it's asked for.</summary>
        internal abstract string Folder { get; }

        internal abstract Log Log { get; }

        /// <summary>
        /// A setting: true or false, a whole number, a number, text, a colour,
        /// or one of an enum's values.
        /// </summary>
        internal abstract Setting<T> Bind<T>(string section, string key, T fallback, string about);

        /// <summary>A text setting that must be one of these. Anything else reads as the first.</summary>
        internal abstract Setting<string> Bind(string section, string key, string fallback,
                                               string about, string[] allowed);

        /// <summary>A number kept between min and max.</summary>
        internal abstract Setting<float> Bind(string section, string key, float fallback,
                                              string about, float min, float max);

        /// <summary>
        /// Whether a change is written to disk at once. Anything that changes
        /// a setting every frame, like dragging, turns this off, and turning
        /// it back on writes the file once.
        /// </summary>
        internal abstract bool Saving { get; set; }
    }

    /// <summary>A mod's log. Info and Warn go wherever its starter logs.</summary>
    internal sealed class Log
    {
        private readonly Action<string> _info;
        private readonly Action<string> _warn;

        internal Log(Action<string> info, Action<string> warn)
        {
            _info = info;
            _warn = warn;
        }

        internal void Info(string message)
        {
            if (_info != null) _info(message);
        }

        internal void Warn(string message)
        {
            if (_warn != null) _warn(message);
        }
    }
}
