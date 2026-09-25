using System;
using System.Globalization;
using System.IO;

namespace Keel
{
    /// <summary>
    /// Keel.log, beside Keel.dll: what the starter did the last time the game
    /// ran. Each line is written as it happens, so a game that stops part way
    /// still leaves it.
    /// </summary>
    internal static class Note
    {
        private static string _path;

        internal static void Open(string path)
        {
            try
            {
                File.WriteAllText(path, string.Empty);
                _path = path;
            }
            catch (Exception)
            {
                _path = null;
            }
        }

        internal static void Line(string text)
        {
            if (_path == null) return;
            try
            {
                File.AppendAllText(_path, DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                                          + "  " + text + Environment.NewLine);
            }
            catch (Exception)
            {
                // A log that can't be written is no reason to stop the game.
            }
        }
    }
}
