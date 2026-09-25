namespace Doorstop
{
    /// <summary>
    /// Doorstop calls this before the game starts, because
    /// doorstop_config.ini names Keel.dll. See Keel.Loader.
    /// </summary>
    internal static class Entrypoint
    {
        public static void Start()
        {
            Keel.Loader.Begin();
        }
    }
}
