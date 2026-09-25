using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Keel
{
    /// <summary>
    /// A mod's settings when Keel started it: one file beside the mod, laid
    /// out the way BepInEx lays out its own. A [section] line, then each
    /// setting with its description as ## lines, its type and default as #
    /// lines, and key = value. A file copied out of BepInEx's config folder
    /// reads as it is.
    /// </summary>
    internal sealed class SettingsFile
    {
        private abstract class Row
        {
            internal string Section;
            internal string Key;
            internal string About;
            internal Type Type;
            internal string Limits;

            internal abstract string Text();
            internal abstract string FallbackText();
        }

        private sealed class Row<T> : Row
        {
            internal T Fallback;
            internal T Value;
            internal Func<T, T> Clamp;

            internal override string Text() { return Toml.Write(Value, Type); }
            internal override string FallbackText() { return Toml.Write(Fallback, Type); }
        }

        private readonly string _path;
        private readonly string[] _header;
        private readonly Action<string> _warn;
        private readonly List<Row> _rows = new List<Row>();
        /// <summary>What the file said, by section and key, in the order it said it.</summary>
        private readonly List<KeyValuePair<string, string>> _readOrder = new List<KeyValuePair<string, string>>();
        private readonly Dictionary<string, string> _read = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _claimed = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>The file was there but couldn't be read, so writing it would lose what's in it.</summary>
        private bool _unreadable;
        private bool _saving = true;
        private bool _toldWrite;

        /// <summary>
        /// Reads the file at path, or, if seed is given, starts from that file
        /// instead: another settings file in the same format, which is only
        /// ever read.
        /// </summary>
        internal SettingsFile(string path, string[] header, Action<string> warn, string seed)
        {
            _path = path;
            _header = header;
            _warn = warn;
            Read(seed ?? path, seed == null);
        }

        internal string FilePath { get { return _path; } }

        /// <summary>
        /// Whether a change is written at once. Turning it back on writes the
        /// file, so a drag that changed a setting every frame is saved once.
        /// </summary>
        internal bool Saving
        {
            get { return _saving; }
            set
            {
                if (_saving == value) return;
                _saving = value;
                if (value) Save();
            }
        }

        internal Setting<T> Add<T>(string section, string key, T fallback, string about,
                                   Func<T, T> clamp, string limits)
        {
            Type type = typeof(T);
            if (!Toml.Supports(type))
                throw new NotSupportedException("Keel can't keep a setting of type " + type.Name + ".");
            string id = Id(section, key);
            foreach (Row existing in _rows)
            {
                if (Id(existing.Section, existing.Key) == id)
                    throw new ArgumentException("[" + section + "] " + key + " is already a setting.");
            }

            Row<T> row = new Row<T>();
            row.Section = section;
            row.Key = key;
            row.About = about;
            row.Type = type;
            row.Limits = limits;
            row.Clamp = clamp;
            row.Fallback = clamp == null ? fallback : clamp(fallback);
            row.Value = row.Fallback;

            string text;
            if (_read.TryGetValue(id, out text))
            {
                _claimed.Add(id);
                object read;
                if (Toml.TryRead(text, type, out read))
                    row.Value = clamp == null ? (T)read : clamp((T)read);
                else if (_warn != null)
                    _warn("[" + section + "] " + key + " = " + text + " can't be read, so it's back to "
                          + "its default, " + row.FallbackText() + ".");
            }
            _rows.Add(row);

            return new Setting<T>(delegate { return row.Value; }, delegate (T v) { Set(row, v); });
        }

        private void Set<T>(Row<T> row, T value)
        {
            if (row.Clamp != null) value = row.Clamp(value);
            if (EqualityComparer<T>.Default.Equals(row.Value, value)) return;
            row.Value = value;
            if (_saving) Save();
        }

        private static string Id(string section, string key)
        {
            return section + "\n" + key;
        }

        private void Read(string from, bool own)
        {
            if (!File.Exists(from)) return;
            string[] lines;
            try
            {
                lines = File.ReadAllLines(from);
            }
            catch (Exception e)
            {
                // Writing over a file that couldn't be read would lose it, so
                // an unreadable file of its own is never saved over.
                _unreadable = own;
                if (_warn != null)
                    _warn("Couldn't read " + from + " (" + e.Message + "), so the settings are at their "
                          + "defaults for now" + (own ? ", and that file is left as it is." : "."));
                return;
            }

            string section = string.Empty;
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.StartsWith("#", StringComparison.Ordinal)) continue;
                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    section = line.Substring(1, line.Length - 2);
                    continue;
                }
                string[] parts = line.Split(new[] { '=' }, 2);
                if (parts.Length != 2) continue;
                string key = parts[0].Trim();
                string id = Id(section, key);
                if (!_read.ContainsKey(id)) _readOrder.Add(new KeyValuePair<string, string>(section, key));
                _read[id] = parts[1].Trim();
            }
        }

        /// <summary>
        /// Writes every setting, and anything else the file held, sections in
        /// name order. Written to a second file first and then swapped in, so
        /// a crash part way through can't leave half a file.
        /// </summary>
        internal void Save()
        {
            if (_unreadable) return;
            string nl = Environment.NewLine;
            StringBuilder b = new StringBuilder(2048);
            foreach (string line in _header) b.Append("## ").Append(line).Append(nl);
            b.Append(nl);

            SortedDictionary<string, List<KeyValuePair<Row, string>>> sections =
                new SortedDictionary<string, List<KeyValuePair<Row, string>>>(StringComparer.Ordinal);
            foreach (Row row in _rows)
                Into(sections, row.Section).Add(new KeyValuePair<Row, string>(row, row.Key + " = " + row.Text()));
            foreach (KeyValuePair<string, string> loose in _readOrder)
            {
                string id = Id(loose.Key, loose.Value);
                if (_claimed.Contains(id)) continue;
                Into(sections, loose.Key).Add(new KeyValuePair<Row, string>(null, loose.Value + " = " + _read[id]));
            }

            foreach (KeyValuePair<string, List<KeyValuePair<Row, string>>> section in sections)
            {
                b.Append('[').Append(section.Key).Append(']').Append(nl);
                foreach (KeyValuePair<Row, string> item in section.Value)
                {
                    b.Append(nl);
                    if (item.Key != null) Describe(b, item.Key, nl);
                    b.Append(item.Value).Append(nl);
                }
                b.Append(nl);
            }

            try
            {
                string dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                string temp = _path + ".tmp";
                File.WriteAllText(temp, b.ToString(), new UTF8Encoding(false));
                if (File.Exists(_path))
                {
                    try
                    {
                        File.Replace(temp, _path, null);
                    }
                    catch (Exception)
                    {
                        File.Copy(temp, _path, true);
                        File.Delete(temp);
                    }
                }
                else
                {
                    File.Move(temp, _path);
                }
            }
            catch (Exception e)
            {
                if (_toldWrite || _warn == null) return;
                _toldWrite = true;
                _warn("Couldn't save " + _path + ": " + e.Message);
            }
        }

        private static List<KeyValuePair<Row, string>> Into(
            SortedDictionary<string, List<KeyValuePair<Row, string>>> sections, string name)
        {
            List<KeyValuePair<Row, string>> list;
            if (!sections.TryGetValue(name, out list))
            {
                list = new List<KeyValuePair<Row, string>>();
                sections[name] = list;
            }
            return list;
        }

        /// <summary>The lines above a setting, as BepInEx writes them.</summary>
        private static void Describe(StringBuilder b, Row row, string nl)
        {
            if (!string.IsNullOrEmpty(row.About))
            {
                foreach (string line in row.About.Split('\n'))
                    b.Append("## ").Append(line.TrimEnd('\r')).Append(nl);
            }
            b.Append("# Setting type: ").Append(row.Type.Name).Append(nl);
            b.Append("# Default value: ").Append(row.FallbackText()).Append(nl);
            if (row.Limits != null)
            {
                b.Append(row.Limits).Append(nl);
            }
            else if (row.Type.IsEnum)
            {
                b.Append("# Acceptable values: ").Append(string.Join(", ", Enum.GetNames(row.Type))).Append(nl);
                if (row.Type.GetCustomAttributes(typeof(FlagsAttribute), true).Length > 0)
                    b.Append("# More than one can be set at once, with commas between them.").Append(nl);
            }
        }

        /// <summary>The line BepInEx writes under a text setting that must be one of a list.</summary>
        internal static string ListLimits(string[] allowed)
        {
            return "# Acceptable values: " + string.Join(", ", allowed);
        }

        /// <summary>The line BepInEx writes under a number kept between two bounds.</summary>
        internal static string RangeLimits(float min, float max)
        {
            return "# Acceptable value range: From " + min.ToString(NumberFormatInfo.InvariantInfo)
                 + " to " + max.ToString(NumberFormatInfo.InvariantInfo);
        }
    }
}
