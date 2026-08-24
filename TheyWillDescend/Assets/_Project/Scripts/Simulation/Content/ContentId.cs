using System.Text;
using Unity.Collections;

namespace TheyWillDescend.Simulation.Content
{
    /// <summary>
        /// Catalog keys: <c>sawmill</c>, <c>wood</c>. Design-time is string;
    /// runtime copies into <see cref="FixedString64Bytes"/>.
    /// </summary>
    public static class ContentId
    {
        public static string Normalize(string value, string fallback = null)
        {
            var raw = string.IsNullOrWhiteSpace(value) ? fallback : value;
            return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim().ToLowerInvariant();
        }

        public static bool TryEncode(string value, out FixedString64Bytes id)
        {
            id = default;
            if (string.IsNullOrEmpty(value))
                return false;
            if (Encoding.UTF8.GetByteCount(value) > FixedString64Bytes.UTF8MaxLengthInBytes)
                return false;
            id = value;
            return true;
        }

        public static FixedString64Bytes EncodeOrEmpty(string value)
        {
            return TryEncode(Normalize(value), out var id) ? id : default;
        }
    }
}
