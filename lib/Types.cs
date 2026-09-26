using System;
using UnityEngine;

namespace Keel
{
    /// <summary>
    /// The kinds of setting Keel keeps: true or false, a whole number, a
    /// number, text, a colour, or one of an enum's values. The core keeps the
    /// same kinds, so a mod behaves the same under either starter.
    /// </summary>
    internal static class Types
    {
        internal static bool Supported(Type type)
        {
            return type == typeof(bool) || type == typeof(int) || type == typeof(float)
                || type == typeof(string) || type == typeof(Color) || type.IsEnum;
        }
    }
}
