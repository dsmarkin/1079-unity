using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Height1079.Core
{
    public enum JsonKind : byte { Null, Bool, Number, String, Array, Object }

    /// <summary>A very small JSON tree, written here rather than taken from a package for one reason: the save file of
    /// <see cref="SaveGame"/> is a format this repository owns, and a format nobody can test is a format that breaks
    /// silently. This class has no engine and no dependencies, so the schema, the round trip and every "an old save
    /// must not crash the game" case run in <c>dotnet</c> along with the rest of Core.
    ///
    /// What it does: objects (keys keep the order they were set in, so two saves of the same state are the same file),
    /// arrays, strings with the standard escapes, numbers as doubles, true/false/null. What it does not: comments,
    /// trailing commas, big integers, or anything else no file we write will ever contain.</summary>
    public sealed class JsonValue
    {
        public readonly JsonKind Kind;
        readonly bool boolean;
        readonly double number;
        readonly string text;
        readonly List<JsonValue> items;
        readonly Dictionary<string, JsonValue> members;
        readonly List<string> order;

        JsonValue(JsonKind kind, bool b = false, double n = 0, string s = null,
            List<JsonValue> list = null, Dictionary<string, JsonValue> map = null, List<string> keys = null)
        { Kind = kind; boolean = b; number = n; text = s; items = list; members = map; order = keys; }

        public static readonly JsonValue Null = new JsonValue(JsonKind.Null);
        public static JsonValue Of(bool value) => new JsonValue(JsonKind.Bool, value);
        public static JsonValue Of(double value) => new JsonValue(JsonKind.Number, false, value);
        public static JsonValue Of(string value) => value == null ? Null : new JsonValue(JsonKind.String, false, 0, value);
        public static JsonValue Array() => new JsonValue(JsonKind.Array, false, 0, null, new List<JsonValue>());
        public static JsonValue Object() => new JsonValue(JsonKind.Object, false, 0, null, null,
            new Dictionary<string, JsonValue>(StringComparer.Ordinal), new List<string>());

        public bool IsNull => Kind == JsonKind.Null;
        public int Count => Kind == JsonKind.Array ? items.Count : Kind == JsonKind.Object ? order.Count : 0;

        /// <summary>Keys in the order they were written, so a file reads back the way it was laid out.</summary>
        public IReadOnlyList<string> Keys => Kind == JsonKind.Object ? (IReadOnlyList<string>)order : new string[0];

        public JsonValue this[int index]
            => Kind == JsonKind.Array && index >= 0 && index < items.Count ? items[index] : Null;

        public JsonValue this[string key]
            => Kind == JsonKind.Object && key != null && members.TryGetValue(key, out var v) ? v : Null;

        public JsonValue Add(JsonValue value)
        {
            if (Kind != JsonKind.Array) throw new InvalidOperationException("not an array");
            items.Add(value ?? Null);
            return this;
        }

        public JsonValue Set(string key, JsonValue value)
        {
            if (Kind != JsonKind.Object) throw new InvalidOperationException("not an object");
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (!members.ContainsKey(key)) order.Add(key);
            members[key] = value ?? Null;
            return this;
        }

        public JsonValue Set(string key, string value) => Set(key, Of(value));
        public JsonValue Set(string key, double value) => Set(key, Of(value));
        public JsonValue Set(string key, bool value) => Set(key, Of(value));

        // ── reading, always with a fallback: a missing or wrong-typed field is never a crash ──────────────

        public bool AsBool(bool fallback = false) => Kind == JsonKind.Bool ? boolean : fallback;
        public double AsNumber(double fallback = 0) => Kind == JsonKind.Number ? number : fallback;
        public string AsString(string fallback = "") => Kind == JsonKind.String ? text : fallback;

        public bool Flag(string key, bool fallback = false) => this[key].AsBool(fallback);
        public double Num(string key, double fallback = 0) => this[key].AsNumber(fallback);
        public float Float(string key, float fallback = 0f) => (float)this[key].AsNumber(fallback);
        public int Int(string key, int fallback = 0)
        {
            double v = this[key].AsNumber(fallback);
            return v >= int.MaxValue ? int.MaxValue : v <= int.MinValue ? int.MinValue : (int)Math.Round(v);
        }
        public string Str(string key, string fallback = "") => this[key].AsString(fallback);

        // ── writing ───────────────────────────────────────────────────────────────────────────────────────

        public string ToJson(bool pretty = false)
        {
            var sb = new StringBuilder();
            Write(sb, pretty, 0);
            return sb.ToString();
        }

        /// <summary>A node with nothing but scalars in it, short enough to read in one glance, is written on one line
        /// even when the file is laid out: a rucksack of forty items is unreadable at four lines an item.</summary>
        const int InlineWidth = 96;

        bool Inline
        {
            get
            {
                if (Kind == JsonKind.Array)
                {
                    foreach (var v in items) if (v.Kind == JsonKind.Array || v.Kind == JsonKind.Object) return false;
                }
                else if (Kind == JsonKind.Object)
                {
                    foreach (var k in order)
                    {
                        var v = members[k];
                        if (v.Kind == JsonKind.Array || v.Kind == JsonKind.Object) return false;
                    }
                }
                else return false;
                return Count > 0 && ToJson().Length <= InlineWidth;
            }
        }

        void Write(StringBuilder sb, bool pretty, int depth)
        {
            // a short node of scalars keeps the spacing of the laid-out file but stays on its own line
            bool flat = pretty && Inline;
            bool wrap = pretty && !flat;
            switch (Kind)
            {
                case JsonKind.Null: sb.Append("null"); return;
                case JsonKind.Bool: sb.Append(boolean ? "true" : "false"); return;
                case JsonKind.Number: sb.Append(Number(number)); return;
                case JsonKind.String: Quote(sb, text); return;
                case JsonKind.Array:
                    if (items.Count == 0) { sb.Append("[]"); return; }
                    sb.Append('[');
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (i > 0) { sb.Append(','); if (flat) sb.Append(' '); }
                        Break(sb, wrap, depth + 1);
                        items[i].Write(sb, pretty, depth + 1);
                    }
                    Break(sb, wrap, depth);
                    sb.Append(']');
                    return;
                default:
                    if (order.Count == 0) { sb.Append("{}"); return; }
                    sb.Append('{');
                    for (int i = 0; i < order.Count; i++)
                    {
                        if (i > 0) { sb.Append(','); if (flat) sb.Append(' '); }
                        Break(sb, wrap, depth + 1);
                        Quote(sb, order[i]);
                        sb.Append(':');
                        if (pretty) sb.Append(' ');
                        members[order[i]].Write(sb, pretty, depth + 1);
                    }
                    Break(sb, wrap, depth);
                    sb.Append('}');
                    return;
            }
        }

        static void Break(StringBuilder sb, bool pretty, int depth)
        {
            if (!pretty) return;
            sb.Append('\n');
            sb.Append(' ', depth * 2);
        }

        /// <summary>Whole numbers come out whole (a save full of "5290.0" reads badly); everything else round-trips.</summary>
        static string Number(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
            if (Math.Abs(v) < 1e15 && Math.Abs(v - Math.Round(v)) < 1e-9)
                return ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture);
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        static void Quote(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        // Cyrillic stays Cyrillic: the file is UTF-8 and a save is meant to be readable
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ── reading ───────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Parses a document. Throws <see cref="FormatException"/> on anything malformed — callers that read
        /// a file off disk are expected to catch it and treat the save as unreadable rather than as empty.</summary>
        public static JsonValue Parse(string json)
        {
            if (json == null) throw new FormatException("empty document");
            int i = 0;
            var v = ParseValue(json, ref i);
            SkipSpace(json, ref i);
            if (i != json.Length) throw new FormatException("trailing characters at " + i);
            return v;
        }

        /// <summary>Parses, or returns null when the text is not JSON at all. For files, where broken is normal.</summary>
        public static JsonValue TryParse(string json)
        {
            try { return Parse(json); }
            catch (FormatException) { return null; }
        }

        static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        static JsonValue ParseValue(string s, ref int i)
        {
            SkipSpace(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end");
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return Of(ParseString(s, ref i));
                case 't': Literal(s, ref i, "true"); return Of(true);
                case 'f': Literal(s, ref i, "false"); return Of(false);
                case 'n': Literal(s, ref i, "null"); return Null;
                default: return Of(ParseNumber(s, ref i));
            }
        }

        static void Literal(string s, ref int i, string word)
        {
            if (i + word.Length > s.Length || string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
                throw new FormatException("bad literal at " + i);
            i += word.Length;
        }

        static JsonValue ParseObject(string s, ref int i)
        {
            var o = Object();
            i++;                                            // {
            SkipSpace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return o; }
            while (true)
            {
                SkipSpace(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new FormatException("key expected at " + i);
                string key = ParseString(s, ref i);
                SkipSpace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("':' expected at " + i);
                i++;
                o.Set(key, ParseValue(s, ref i));
                SkipSpace(s, ref i);
                if (i >= s.Length) throw new FormatException("unexpected end in object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return o; }
                throw new FormatException("',' or '}' expected at " + i);
            }
        }

        static JsonValue ParseArray(string s, ref int i)
        {
            var a = Array();
            i++;                                            // [
            SkipSpace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return a; }
            while (true)
            {
                a.Add(ParseValue(s, ref i));
                SkipSpace(s, ref i);
                if (i >= s.Length) throw new FormatException("unexpected end in array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return a; }
                throw new FormatException("',' or ']' expected at " + i);
            }
        }

        static string ParseString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++;                                            // opening quote
            while (true)
            {
                if (i >= s.Length) throw new FormatException("unterminated string");
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) throw new FormatException("unterminated escape");
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("short \\u escape");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw new FormatException("bad escape \\" + e);
                }
            }
        }

        static double ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E'
                || ((s[i] == '-' || s[i] == '+') && (s[i - 1] == 'e' || s[i - 1] == 'E')))) i++;
            if (i == start) throw new FormatException("number expected at " + start);
            if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException("bad number at " + start);
            return v;
        }
    }
}
