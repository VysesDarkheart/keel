namespace Doorstop
{
    /// <summary>
    /// Doorstop calls this before the game starts, because
    /// doorstop_config.ini names Keel.dll, or for the game's own Linux and
    /// Mac builds, Keel/run.sh does. See Keel.Starter.
    /// </summary>
    internal static class Entrypoint
    {
        public static void Start()
        {
            Keel.Starter.Begin();
        }
    }
}
