using System;
using System.Collections;
using System.Globalization;
using System.Text;

using ReW9x;
using ReW9x.Models;
namespace ReW9x.Utils
{
    public sealed class JsonParser
    {
        private readonly string _json;
        private int _i;

        private JsonParser(string json)
        {
            _json = json ?? "";
            _i = 0;
        }

        public static object Parse(string json)
        {
            JsonParser p = new JsonParser(json);
            object v = p.ParseValue();
            p.SkipWs();
            return v;
        }

        public static Hashtable AsObject(object value)
        {
            return value as Hashtable;
        }

        public static ArrayList AsArray(object value)
        {
            return value as ArrayList;
        }

        public static string AsString(object value)
        {
            if (value == null) return null;
            if (value is string) return (string)value;
            if (value is bool) return ((bool)value) ? "true" : "false";
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static double AsDouble(object value)
        {
            if (value == null) return 0.0;
            if (value is double) return (double)value;
            if (value is int) return (int)value;
            if (value is long) return (long)value;
            if (value is decimal) return (double)(decimal)value;
            double d;
            if (double.TryParse(AsString(value), NumberStyles.Any, CultureInfo.InvariantCulture, out d))
                return d;
            return 0.0;
        }

        public static int AsInt(object value)
        {
            return (int)Math.Round(AsDouble(value));
        }

        public static bool AsBool(object value)
        {
            if (value == null) return false;
            if (value is bool) return (bool)value;
            string s = AsString(value);
            return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetString(Hashtable obj, string key)
        {
            if (obj == null || !obj.ContainsKey(key)) return null;
            return AsString(obj[key]);
        }

        public static string GetString(IDictionary obj, string key)
        {
            if (obj == null || !obj.Contains(key)) return null;
            return AsString(obj[key]);
        }

        public static double GetDouble(Hashtable obj, string key)
        {
            if (obj == null || !obj.ContainsKey(key)) return 0.0;
            return AsDouble(obj[key]);
        }

        public static int GetInt(Hashtable obj, string key)
        {
            if (obj == null || !obj.ContainsKey(key)) return 0;
            return AsInt(obj[key]);
        }

        public static bool GetBool(Hashtable obj, string key)
        {
            if (obj == null || !obj.ContainsKey(key)) return false;
            return AsBool(obj[key]);
        }

        public static Hashtable GetObject(Hashtable obj, string key)
        {
            if (obj == null || !obj.ContainsKey(key)) return null;
            return AsObject(obj[key]);
        }

        public static ArrayList GetArray(Hashtable obj, string key)
        {
            if (obj == null || !obj.ContainsKey(key)) return null;
            return AsArray(obj[key]);
        }

        private void SkipWs()
        {
            while (_i < _json.Length)
            {
                char c = _json[_i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                    _i++;
                else
                    break;
            }
        }

        private char Peek()
        {
            if (_i >= _json.Length) return '\0';
            return _json[_i];
        }

        private char Next()
        {
            if (_i >= _json.Length) return '\0';
            return _json[_i++];
        }

        private object ParseValue()
        {
            SkipWs();
            char c = Peek();

            if (c == '{') return ParseObject();
            if (c == '[') return ParseArray();
            if (c == '"') return ParseString();
            if (c == 't') { Consume("true"); return true; }
            if (c == 'f') { Consume("false"); return false; }
            if (c == 'n') { Consume("null"); return null; }

            return ParseNumber();
        }

        private void Consume(string s)
        {
            int len = s.Length;
            int j;
            for (j = 0; j < len; j++)
            {
                if (_i + j >= _json.Length || _json[_i + j] != s[j])
                    throw new FormatException("Invalid JSON");
            }
            _i += len;
        }

        private Hashtable ParseObject()
        {
            Hashtable obj = new Hashtable();
            Expect('{');
            SkipWs();
            if (Peek() == '}')
            {
                _i++;
                return obj;
            }

            while (true)
            {
                SkipWs();
                string key = ParseString();
                SkipWs();
                Expect(':');
                object value = ParseValue();
                obj[key] = value;
                SkipWs();
                char c = Next();
                if (c == '}') break;
                if (c != ',') throw new FormatException("Invalid JSON object");
            }

            return obj;
        }

        private ArrayList ParseArray()
        {
            ArrayList arr = new ArrayList();
            Expect('[');
            SkipWs();
            if (Peek() == ']')
            {
                _i++;
                return arr;
            }

            while (true)
            {
                object value = ParseValue();
                arr.Add(value);
                SkipWs();
                char c = Next();
                if (c == ']') break;
                if (c != ',') throw new FormatException("Invalid JSON array");
            }

            return arr;
        }

        private string ParseString()
        {
            Expect('"');
            StringBuilder sb = new StringBuilder();

            while (_i < _json.Length)
            {
                char c = Next();
                if (c == '"') break;
                if (c == '\\')
                {
                    c = Next();
                    switch (c)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            sb.Append(ParseUnicode());
                            break;
                        default:
                            throw new FormatException("Invalid escape");
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private char ParseUnicode()
        {
            if (_i + 4 > _json.Length)
                throw new FormatException("Invalid unicode escape");

            string hex = _json.Substring(_i, 4);
            _i += 4;
            int code = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return (char)code;
        }

        private object ParseNumber()
        {
            int start = _i;
            char c = Peek();
            if (c == '-') _i++;

            while (char.IsDigit(Peek())) _i++;

            if (Peek() == '.')
            {
                _i++;
                while (char.IsDigit(Peek())) _i++;
            }

            if (Peek() == 'e' || Peek() == 'E')
            {
                _i++;
                if (Peek() == '+' || Peek() == '-') _i++;
                while (char.IsDigit(Peek())) _i++;
            }

            string num = _json.Substring(start, _i - start);
            double d = double.Parse(num, CultureInfo.InvariantCulture);
            return d;
        }

        private void Expect(char expected)
        {
            SkipWs();
            char c = Next();
            if (c != expected)
                throw new FormatException("Invalid JSON");
        }
    }
}
