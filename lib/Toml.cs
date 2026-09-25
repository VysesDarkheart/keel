using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Keel
{
    /// <summary>
    /// Setting values as text, written the way BepInEx writes them, so a
    /// settings file reads the same whichever of the two wrote it.
    /// </summary>
    internal static class Toml
    {
        internal static bool Supports(Type type)
        {
            return type == typeof(bool) || type == typeof(int) || type == typeof(float)
                || type == typeof(string) || type == typeof(Color) || type.IsEnum;
        }

        internal static string Write(object value, Type type)
        {
            if (type == typeof(bool)) return (bool)value ? "true" : "false";
            if (type == typeof(int)) return ((int)value).ToString(NumberFormatInfo.InvariantInfo);
            if (type == typeof(float)) return ((float)value).ToString(NumberFormatInfo.InvariantInfo);
            if (type == typeof(string)) return Escape((string)value);
            if (type == typeof(Color)) return Hex((Color)value);
            if (type.IsEnum) return value.ToString();
            throw new NotSupportedException("Keel can't keep a setting of type " + type.Name + ".");
        }

        /// <summary>Reads a value written by Write, or by BepInEx. False if the text isn't one.</summary>
        internal static bool TryRead(string text, Type type, out object value)
        {
            value = null;
            if (text == null) return false;
            try
            {
                if (type == typeof(bool))
                {
                    value = bool.Parse(text);
                }
                else if (type == typeof(int))
                {
                    value = int.Parse(text, NumberStyles.Integer, NumberFormatInfo.InvariantInfo);
                }
                else if (type == typeof(float))
                {
                    value = float.Parse(text, NumberStyles.Float | NumberStyles.AllowThousands,
                                        NumberFormatInfo.InvariantInfo);
                }
                else if (type == typeof(string))
                {
                    value = LooksLikePath(text) ? text : Unescape(text);
                }
                else if (type == typeof(Color))
                {
                    Color colour;
                    if (!TryHex(text, out colour)) return false;
                    value = colour;
                }
                else if (type.IsEnum)
                {
                    value = Enum.Parse(type, text, true);
                }
                else
                {
                    return false;
                }
                return true;
            }
            catch (FormatException) { }
            catch (OverflowException) { }
            catch (ArgumentException) { }
            return false;
        }

        /// <summary>
        /// BepInEx writes a backslash in text as it is, so a Windows path in
        /// one of its files has single backslashes, and reading them as
        /// escapes would spoil it. Text that starts like a drive path, with
        /// no doubled backslash anywhere in it, is taken as it stands.
        /// </summary>
        private static bool LooksLikePath(string text)
        {
            int at = text.StartsWith("\"", StringComparison.Ordinal) ? 1 : 0;
            if (text.Length < at + 3) return false;
            char drive = text[at];
            if (!(char.IsLetterOrDigit(drive) || drive == '_')) return false;
            if (text[at + 1] != ':' || text[at + 2] != '\\') return false;
            return text.IndexOf("\\\\", at + 2, StringComparison.Ordinal) < 0;
        }

        /// <summary>RRGGBBAA, as Unity's ColorUtility.ToHtmlStringRGBA writes it.</summary>
        private static string Hex(Color c)
        {
            return Byte(c.r).ToString("X2", CultureInfo.InvariantCulture)
                 + Byte(c.g).ToString("X2", CultureInfo.InvariantCulture)
                 + Byte(c.b).ToString("X2", CultureInfo.InvariantCulture)
                 + Byte(c.a).ToString("X2", CultureInfo.InvariantCulture);
        }

        private static int Byte(float channel)
        {
            int n = (int)Math.Round(channel * 255f);
            return n < 0 ? 0 : (n > 255 ? 255 : n);
        }

        /// <summary>
        /// RGB, RGBA, RRGGBB or RRGGBBAA, with or without a #. Missing alpha
        /// is solid.
        /// </summary>
        private static bool TryHex(string text, out Color colour)
        {
            colour = default(Color);
            string s = text.Trim().Trim('#', ' ');
            if (s.Length == 3 || s.Length == 4)
            {
                StringBuilder wide = new StringBuilder(8);
                foreach (char ch in s) wide.Append(ch).Append(ch);
                s = wide.ToString();
            }
            if (s.Length == 6) s += "FF";
            if (s.Length != 8) return false;
            uint all;
            if (!uint.TryParse(s, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out all))
                return false;
            colour = new Color32((byte)(all >> 24), (byte)(all >> 16), (byte)(all >> 8), (byte)all);
            return true;
        }

        // The backslash escapes a settings file can hold: the character on
        // one line, and the letter written after the backslash for it on the
        // other, in the same place.
        private const string Escaped = "\\\"\n\r\t\0\a\b\v\f'";
        private const string Letters = "\\\"nrt0abvf'";
        /// <summary>How many of those Keel writes: the first six. The rest it only reads.</summary>
        private const int Written = 6;

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            StringBuilder b = new StringBuilder(text.Length + 8);
            foreach (char c in text)
            {
                int at = Escaped.IndexOf(c);
                if (at >= 0 && at < Written) b.Append('\\').Append(Letters[at]);
                else b.Append(c);
            }
            return b.ToString();
        }

        /// <summary>
        /// Undoes Escape, and reads every escape BepInEx writes. A backslash
        /// before any other letter, or at the very end, is kept as it is.
        /// </summary>
        private static string Unescape(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            StringBuilder b = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != '\\' || i + 1 == text.Length)
                {
                    b.Append(c);
                    continue;
                }
                char letter = text[++i];
                int at = Letters.IndexOf(letter);
                if (at >= 0) b.Append(Escaped[at]);
                else b.Append('\\').Append(letter);
            }
            return b.ToString();
        }
    }
}
