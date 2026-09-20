using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PuzzleOnline.Network
{
    public sealed class NetMessage
    {
        public readonly string Type;
        public readonly Dictionary<string, string> Data;

        public NetMessage(string type, Dictionary<string, string> data = null)
        {
            Type = type ?? string.Empty;
            Data = data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string Get(string key, string fallback = "")
        {
            return Data.TryGetValue(key, out var value) ? value : fallback;
        }

        public int GetInt(string key, int fallback = 0)
        {
            return int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;
        }

        public long GetLong(string key, long fallback = 0)
        {
            return long.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;
        }

        public float GetFloat(string key, float fallback = 0f)
        {
            return float.TryParse(Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;
        }

        public bool GetBool(string key, bool fallback = false)
        {
            var value = Get(key);
            if (bool.TryParse(value, out var parsed)) return parsed;
            if (value == "1") return true;
            if (value == "0") return false;
            return fallback;
        }
    }

    public static class WireProtocol
    {
        public const char RecordSeparator = '\u001e';
        public const char FieldSeparator = '\u001f';

        public static string Encode(string type, params (string Key, object Value)[] fields)
        {
            var builder = new StringBuilder(type ?? string.Empty);
            if (fields == null) return builder.ToString();

            foreach (var field in fields)
            {
                if (string.IsNullOrWhiteSpace(field.Key)) continue;
                builder.Append('|').Append(field.Key).Append('=').Append(ToBase64(Format(field.Value)));
            }

            return builder.ToString();
        }

        public static NetMessage Decode(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return new NetMessage(string.Empty);
            var parts = line.Split('|');
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 1; i < parts.Length; i++)
            {
                var separator = parts[i].IndexOf('=');
                if (separator <= 0) continue;
                var key = parts[i].Substring(0, separator);
                var encoded = parts[i].Substring(separator + 1);
                try { fields[key] = FromBase64(encoded); }
                catch (FormatException) { fields[key] = string.Empty; }
            }
            return new NetMessage(parts[0], fields);
        }

        public static string PackRecords(IEnumerable<IEnumerable<object>> records)
        {
            if (records == null) return string.Empty;
            var packed = new List<string>();
            foreach (var record in records)
            {
                var fields = new List<string>();
                foreach (var value in record) fields.Add(Format(value));
                packed.Add(string.Join(FieldSeparator.ToString(), fields));
            }
            return string.Join(RecordSeparator.ToString(), packed);
        }

        public static List<string[]> UnpackRecords(string packed)
        {
            var output = new List<string[]>();
            if (string.IsNullOrEmpty(packed)) return output;
            foreach (var row in packed.Split(RecordSeparator))
                output.Add(row.Split(FieldSeparator));
            return output;
        }

        public static string Format(object value)
        {
            if (value == null) return string.Empty;
            if (value is bool boolean) return boolean ? "true" : "false";
            if (value is IFormattable formattable) return formattable.ToString(null, CultureInfo.InvariantCulture);
            return value.ToString();
        }

        private static string ToBase64(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string FromBase64(string encoded)
        {
            var normalized = (encoded ?? string.Empty).Replace('-', '+').Replace('_', '/');
            switch (normalized.Length % 4)
            {
                case 2: normalized += "=="; break;
                case 3: normalized += "="; break;
            }
            return Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
        }
    }
}
