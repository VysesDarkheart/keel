using System;

namespace Keel
{
    /// <summary>One setting. Read or set Value; its host keeps the file up to date.</summary>
    internal sealed class Setting<T>
    {
        private readonly Func<T> _get;
        private readonly Action<T> _set;

        internal Setting(Func<T> get, Action<T> set)
        {
            _get = get;
            _set = set;
        }

        internal T Value
        {
            get { return _get(); }
            set { _set(value); }
        }
    }
}
