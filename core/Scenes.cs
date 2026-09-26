using System;
using System.Runtime.CompilerServices;
using UnityEngine.SceneManagement;

namespace Keel
{
    /// <summary>
    /// The part of Loader that touches Unity. It's only reached once Unity
    /// has loaded UnityEngine.CoreModule; see Loader.
    /// </summary>
    internal static class Scenes
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Listen()
        {
            SceneManager.sceneLoaded += First;
        }

        /// <summary>
        /// Unity's first scene has loaded: the engine is up, and this runs on
        /// the game's main thread, where mods can make their objects. Nothing
        /// may escape from here, because a fault in one sceneLoaded listener
        /// stops Unity calling the ones after it, which include the game's own.
        /// </summary>
        private static void First(Scene scene, LoadSceneMode mode)
        {
            try
            {
                SceneManager.sceneLoaded -= First;
                Loader.StartMods(scene.name);
            }
            catch (Exception e)
            {
                Note.Line("Starting the mods failed: " + e);
            }
        }
    }
}
