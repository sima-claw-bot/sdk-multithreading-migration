#nullable enable
using System;
using System.IO;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Represents an absolute file system path.
    /// </summary>
    public readonly struct AbsolutePath : IEquatable<AbsolutePath>
    {
        private readonly string _value;

        public AbsolutePath(string path)
        {
            _value = path ?? throw new ArgumentNullException(nameof(path));
        }

        /// <summary>
        /// The string representation of this path.
        /// </summary>
        public string Value => _value;

        /// <summary>
        /// Implicitly converts an AbsolutePath to a string.
        /// </summary>
        public static implicit operator string(AbsolutePath path) => path._value;

        /// <summary>
        /// Returns the canonical form of this path with resolved relative segments
        /// and normalized directory separators.
        /// </summary>
        public string GetCanonicalForm()
        {
            if (string.IsNullOrEmpty(_value))
            {
                return _value;
            }

            return Path.GetFullPath(_value);
        }

        public static bool operator ==(AbsolutePath left, AbsolutePath right) => left.Equals(right);
        public static bool operator !=(AbsolutePath left, AbsolutePath right) => !left.Equals(right);

        public bool Equals(AbsolutePath other) =>
            StringComparer.OrdinalIgnoreCase.Equals(_value, other._value);

        public override bool Equals(object? obj) => obj is AbsolutePath other && Equals(other);

        public override int GetHashCode() =>
            _value is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(_value);

        public override string ToString() => _value;
    }
}
