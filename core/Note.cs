using System;
using System.Globalization;
using System.IO;

namespace Keel
{
    /// <summary>
    /// Keel.log, beside Keel.dll: what Keel did the last time the game ran.
    /// The starter begins it afresh, and the core carries it on. Each line is
    /// written as it happens, so a game that stops part way still leaves it.
    /// </summary>
    internal static class Note
    {
        private static string _path;

        /// <summary>Carries on the Keel.log the starter began.</summary>
        internal static void Open(string path)
        {
            _path = path;
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
