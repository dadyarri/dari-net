using System.Text;

namespace Dari.Archiver.Extra;

/// <summary>
/// Parsed representation of the Dari extra field: a semicolon-delimited list of
/// <c>key=value</c> pairs with <c>%3B</c> escaping for literal semicolons (§7).
/// </summary>
/// <remarks>
/// <para>
/// Stored internally as a small <c>(string Key, string Value)[]</c> array.
/// Most entries carry 0–5 pairs, so a dictionary would add unnecessary overhead.
/// </para>
/// <para>
/// All mutation methods return a <strong>new</strong> <see cref="ExtraField"/>; the
/// struct is effectively immutable after construction.
/// </para>
/// </remarks>
public readonly struct ExtraField
{
    private static readonly (string Key, string Value)[] EmptyPairs = [];

    private readonly (string Key, string Value)[] _pairs;

    /// <summary>An empty extra field (serialises to an empty string).</summary>
    public static ExtraField Empty { get; } = new(EmptyPairs);

    private ExtraField((string Key, string Value)[] pairs) => _pairs = pairs;

    /// <summary>Number of key/value pairs.</summary>
    public int Count => (_pairs ?? EmptyPairs).Length;

    // -----------------------------------------------------------------------
    // Parsing
    // -----------------------------------------------------------------------

    /// <summary>
    /// Parses an extra field from a UTF-8 encoded byte span (zero-copy path).
    /// </summary>
    public static ExtraField Parse(ReadOnlySpan<byte> utf8) =>
        Parse(Encoding.UTF8.GetString(utf8));

    /// <summary>Parses an extra field from a string.</summary>
    public static ExtraField Parse(ReadOnlySpan<char> raw)
    {
        if (raw.IsEmpty)
            return Empty;

        // Count segments to pre-allocate (avoids List<T> in common cases).
        int segCount = CountChar(raw, ';') + 1;
        var pairs = new (string Key, string Value)[segCount];
        int written = 0;

        // We must split on ';' FIRST, then unescape '%3B' in each token (§7).
        int start = 0;
        while (start <= raw.Length)
        {
            int sep = raw[start..].IndexOf(';');
            ReadOnlySpan<char> segment = sep < 0
                ? raw[start..]
                : raw.Slice(start, sep);

            int eq = segment.IndexOf('=');
            if (eq > 0)                              // key must be non-empty
            {
                string key = Unescape(segment[..eq]);
                string value = Unescape(segment[(eq + 1)..]);
                if (value.Length > 0)                // skip empty values (§7)
                    pairs[written++] = (key, value);
            }

            if (sep < 0) break;
            start += sep + 1;
        }

        if (written == 0)
            return Empty;

        // Last-write-wins deduplication: walk backwards and keep only the
        // last occurrence of each key.
        var seen = new HashSet<string>(written, StringComparer.Ordinal);
        var result = new (string Key, string Value)[written];
        int ri = written - 1;
        for (int i = written - 1; i >= 0; i--)
        {
            if (seen.Add(pairs[i].Key))
                result[ri--] = pairs[i];
        }

        // Trim leading unset slots
        int firstUsed = ri + 1;
        return firstUsed == 0
            ? new ExtraField(result)
            : new ExtraField(result[firstUsed..]);
    }

    // -----------------------------------------------------------------------
    // Lookup
    // -----------------------------------------------------------------------

    /// <summary>Returns the value for <paramref name="key"/>, or <see langword="null"/>.</summary>
    public string? GetValueOrDefault(string key)
    {
        foreach (var (k, v) in (_pairs ?? EmptyPairs))
            if (string.Equals(k, key, StringComparison.Ordinal))
                return v;
        return null;
    }

    /// <summary>Returns <see langword="true"/> and sets <paramref name="value"/> when <paramref name="key"/> exists.</summary>
    public bool TryGetValue(string key, out string value)
    {
        string? v = GetValueOrDefault(key);
        value = v ?? string.Empty;
        return v is not null;
    }

    // -----------------------------------------------------------------------
    // Immutable mutation
    // -----------------------------------------------------------------------

    /// <summary>Returns a new <see cref="ExtraField"/> with <paramref name="key"/> set to <paramref name="value"/>.</summary>
    public ExtraField With(string key, string value)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key must not be empty.", nameof(key));
        if (string.IsNullOrEmpty(value)) throw new ArgumentException("Value must not be empty.", nameof(value));

        var source = _pairs ?? EmptyPairs;

        // Replace in-place if key already exists.
        for (int i = 0; i < source.Length; i++)
        {
            if (string.Equals(source[i].Key, key, StringComparison.Ordinal))
            {
                var updated = new (string Key, string Value)[source.Length];
                source.CopyTo(updated, 0);
                updated[i] = (key, value);
                return new ExtraField(updated);
            }
        }

        // Append new pair.
        var appended = new (string Key, string Value)[source.Length + 1];
        source.CopyTo(appended, 0);
        appended[^1] = (key, value);
        return new ExtraField(appended);
    }

    /// <summary>Returns a new <see cref="ExtraField"/> without <paramref name="key"/> (no-op if absent).</summary>
    public ExtraField Without(string key)
    {
        var source = _pairs ?? EmptyPairs;
        int idx = -1;
        for (int i = 0; i < source.Length; i++)
            if (string.Equals(source[i].Key, key, StringComparison.Ordinal))
            { idx = i; break; }

        if (idx < 0) return this;

        var result = new (string Key, string Value)[source.Length - 1];
        source[..idx].CopyTo(result, 0);
        source[(idx + 1)..].CopyTo(result, idx);
        return new ExtraField(result);
    }

    // -----------------------------------------------------------------------
    // Serialisation
    // -----------------------------------------------------------------------

    /// <summary>
    /// Serialises to the canonical Dari extra-field wire format:
    /// <c>key1=value1;key2=value2</c> with <c>;</c> escaped as <c>%3B</c>.
    /// Returns an empty string when there are no pairs.
    /// </summary>
    public string Serialize()
    {
        var pairs = _pairs ?? EmptyPairs;
        if (pairs.Length == 0)
            return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < pairs.Length; i++)
        {
            if (i > 0) sb.Append(';');
            sb.Append(Escape(pairs[i].Key));
            sb.Append('=');
            sb.Append(Escape(pairs[i].Value));
        }
        return sb.ToString();
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private static string Escape(ReadOnlySpan<char> s) =>
        s.IndexOf(';') < 0 ? s.ToString() : s.ToString().Replace(";", "%3B", StringComparison.Ordinal);

    private static string Unescape(ReadOnlySpan<char> s) =>
        s.IndexOf("%3B", StringComparison.Ordinal) < 0
            ? s.ToString()
            : s.ToString().Replace("%3B", ";", StringComparison.Ordinal);

    private static int CountChar(ReadOnlySpan<char> s, char c)
    {
        int count = 0;
        foreach (char ch in s)
            if (ch == c) count++;
        return count;
    }

    public override string ToString() => Serialize();
}
