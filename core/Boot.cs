namespace Keel
{
    /// <summary>
    /// Where Keel.dll, the starter, hands over. The starter finds this class
    /// and Start by name in the newest core, so neither the names nor
    /// Start's signature may ever change.
    /// </summary>
    internal static class Boot
    {
        internal static void Start(string home)
        {
            Loader.Begin(home);
        }
    }
}
