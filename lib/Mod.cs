using System;

namespace Keel
{
    /// <summary>
    /// A mod built on Keel. Keel starts it once, whether BepInEx loads it or
    /// Keel's own starter does, and hands it a Host for its settings and log.
    /// </summary>
    internal abstract class Mod
    {
        /// <summary>
        /// A name no other mod uses, such as com.example.mymod. BepInEx names
        /// the settings file after it, and Keel uses it to start a mod once.
        /// </summary>
        internal abstract string Id { get; }

        /// <summary>The name players see. Keel's settings file is named after it.</summary>
        internal abstract string Name { get; }

        internal abstract string Version { get; }

        /// <summary>
        /// Called once, on the game's main thread. Unity is running, but the
        /// game's own screens and world may not exist yet.
        /// </summary>
        internal abstract void Start(Host host);
    }

    /// <summary>
    /// Names a mod's Mod class, so Keel's starter can find it without
    /// BepInEx: [assembly: Keel.Main(typeof(MyMod))].
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    internal sealed class MainAttribute : Attribute
    {
        internal MainAttribute(Type mod)
        {
            Mod = mod;
        }

        internal Type Mod { get; }
    }
}
