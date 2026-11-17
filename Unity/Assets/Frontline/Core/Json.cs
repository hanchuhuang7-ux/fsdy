using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Frontline.Core
{
    // Shared strict JSON boundary; no runtime JavaScript, dynamic code or third-party JSON runtime.
    public static class Json
    {
        public static object Parse(string text, int maxBytes = 64 * 1024 * 1024)
        {
            if (text == null || Encoding.UTF8.GetByteCount(text) > maxBytes) throw new FormatException("JSON 文件超出大小限制");
            return new Reader(text).Read();
        }
        public static T Read<T>(string text, int maxBytes = 64 * 1024 * 1024) { return (T)ConvertValue(Parse(text, maxBytes), typeof(T)); }
        public static T Clone<T>(T value) { return Read<T>(Write(value)); }
        public static string Write(object value) { var b = new StringBuilder(); Encode(value, b, 0); return b.ToString(); }
        static bool Dangerous(string s) { return s == "__proto__" || s == "prototype" || s == "constructor"; }
        static object ConvertValue(object value, Type target)
        {
            if (value == null) return target.IsValueType ? Activator.CreateInstance(target) : null;
            if (target == typeof(object)) return value;
            if (target == typeof(string)) { if (!(value is string)) throw new FormatException("需要字符串"); return value; }
            if (target == typeof(bool)) { if (!(value is bool)) throw new FormatException("需要布尔值"); return value; }
            if (target.IsEnum) return Enum.Parse(target, value.ToString());
            if (target.IsPrimitive || target == typeof(decimal))
            {
                if (!(value is double)) throw new FormatException("需要数值");
                var number = (double)value;
                if (target != typeof(float) && target != typeof(double) && Math.Truncate(number) != number) throw new FormatException("需要整数");
                return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
            }
            var array = value as List<object>;
            if (target.IsArray)
            {
                if (array == null) throw new FormatException("需要数组");
                var element = target.GetElementType(); var result = Array.CreateInstance(element, array.Count);
                for (int i = 0; i < array.Count; i++) result.SetValue(ConvertValue(array[i], element), i);
                return result;
            }
            if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(List<>))
            {
                if (array == null) throw new FormatException("需要列表");
                var list = (IList)Activator.CreateInstance(target); var element = target.GetGenericArguments()[0];
                foreach (var item in array) list.Add(ConvertValue(item, element)); return list;
            }
            var map = value as Dictionary<string, object>;
            if (map == null) throw new FormatException("需要对象");
            if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var dictionary = (IDictionary)Activator.CreateInstance(target); var element = target.GetGenericArguments()[1];
                foreach (var row in map) dictionary.Add(row.Key, ConvertValue(row.Value, element)); return dictionary;
            }
            var instance = Activator.CreateInstance(target);
            foreach (var field in target.GetFields(BindingFlags.Instance | BindingFlags.Public))
                if (!Attribute.IsDefined(field,typeof(NonSerializedAttribute)) && map.TryGetValue(field.Name, out var item)) field.SetValue(instance, ConvertValue(item, field.FieldType));
            return instance;
        }
        static void Encode(object value, StringBuilder b, int depth)
        {
            if (depth > 32) throw new FormatException("对象递归过深");
            if (value == null) { b.Append("null"); return; }
            if (value is string str) { Quote(str, b); return; }
            if (value is bool flag) { b.Append(flag ? "true" : "false"); return; }
            if (value is float || value is double || value is decimal || value is int || value is uint || value is long || value is short || value is byte)
            {
                var n = Convert.ToDouble(value, CultureInfo.InvariantCulture); if (double.IsNaN(n) || double.IsInfinity(n)) throw new FormatException("不能保存非有限数值");
                b.Append(value is float ? ((float)value).ToString("R", CultureInfo.InvariantCulture) : value is double ? n.ToString("R", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture)); return;
            }
            if (value is IDictionary map)
            {
                b.Append('{'); bool first = true;
                foreach (DictionaryEntry row in map) { if (!first) b.Append(','); first = false; var key = row.Key.ToString(); if (Dangerous(key)) throw new FormatException("非法 JSON 键"); Quote(key, b); b.Append(':'); Encode(row.Value, b, depth + 1); }
                b.Append('}'); return;
            }
            if (value is IEnumerable list)
            {
                b.Append('['); bool first = true; foreach (var item in list) { if (!first) b.Append(','); first = false; Encode(item, b, depth + 1); } b.Append(']'); return;
            }
            b.Append('{'); bool leading = true;
            if(value.GetType().Name.Contains("AnonymousType"))
            {
                foreach(var property in value.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance)){if(!leading)b.Append(',');leading=false;Quote(property.Name,b);b.Append(':');Encode(property.GetValue(value,null),b,depth+1);}
                b.Append('}');return;
            }
            foreach (var field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (Attribute.IsDefined(field,typeof(NonSerializedAttribute))) continue;
                if (!leading) b.Append(','); leading = false; Quote(field.Name, b); b.Append(':'); Encode(field.GetValue(value), b, depth + 1);
            }
            b.Append('}');
        }
        static void Quote(string text, StringBuilder b)
        {
            b.Append('"'); foreach (var c in text) { switch (c) { case '"': b.Append("\\\""); break; case '\\': b.Append("\\\\"); break; case '\n': b.Append("\\n"); break; case '\r': b.Append("\\r"); break; case '\t': b.Append("\\t"); break; default: if (c < 32) b.Append("\\u" + ((int)c).ToString("x4")); else b.Append(c); break; } } b.Append('"');
        }
        sealed class Reader
        {
            readonly string s; int p, nodes;
            public Reader(string text) { s = text; }
            void Space() { while (p < s.Length && char.IsWhiteSpace(s[p])) p++; }
            public object Read() { var value = Value(0); Space(); if (p != s.Length) throw new FormatException("JSON 尾部有多余内容"); return value; }
            object Value(int depth)
            {
                if (depth > 32 || ++nodes > 4000000) throw new FormatException("JSON 结构过大"); Space(); if (p >= s.Length) throw new FormatException("JSON 不完整");
                char c = s[p];
                if (c == '"') return String();
                if (c == '{')
                {
                    p++; var map = new Dictionary<string, object>(StringComparer.Ordinal); Space(); if (Take('}')) return map;
                    do { Space(); if (p >= s.Length || s[p] != '"') throw new FormatException("JSON 键无效"); var key = String(); if (Dangerous(key) || map.ContainsKey(key)) throw new FormatException("危险或重复 JSON 键"); Space(); Need(':'); map.Add(key, Value(depth + 1)); Space(); if (Take('}')) return map; Need(','); } while (true);
                }
                if (c == '[')
                {
                    p++; var list = new List<object>(); Space(); if (Take(']')) return list;
                    do { list.Add(Value(depth + 1)); Space(); if (Take(']')) return list; Need(','); } while (true);
                }
                foreach (var literal in new[] { "true", "false", "null" }) if (p + literal.Length <= s.Length && s.Substring(p, literal.Length) == literal) { p += literal.Length; return literal == "null" ? null : (object)(literal == "true"); }
                int start = p; if (Take('-')) { } if (p >= s.Length || s[p] < '0' || s[p] > '9') throw new FormatException("JSON 数字无效");
                if (s[p++] != '0') while (p < s.Length && char.IsDigit(s[p])) p++;
                if (Take('.')) { int k = p; while (p < s.Length && char.IsDigit(s[p])) p++; if (p == k) throw new FormatException("JSON 小数无效"); }
                if (p < s.Length && (s[p] == 'e' || s[p] == 'E')) { p++; if (p < s.Length && (s[p] == '+' || s[p] == '-')) p++; int k = p; while (p < s.Length && char.IsDigit(s[p])) p++; if (p == k) throw new FormatException("JSON 指数无效"); }
                if (!double.TryParse(s.Substring(start, p - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || double.IsNaN(n) || double.IsInfinity(n)) throw new FormatException("JSON 数值必须有限"); return n;
            }
            bool Take(char c) { if (p < s.Length && s[p] == c) { p++; return true; } return false; }
            void Need(char c) { if (!Take(c)) throw new FormatException("JSON 缺少 " + c); }
            string String()
            {
                Need('"'); var b = new StringBuilder(); while (p < s.Length) { char c = s[p++]; if (c == '"') return b.ToString(); if (c < 32) throw new FormatException("JSON 字符串控制字符无效"); if (c != '\\') { b.Append(c); continue; } if (p >= s.Length) break; c = s[p++]; switch (c) { case '"': case '\\': case '/': b.Append(c); break; case 'b': b.Append('\b'); break; case 'f': b.Append('\f'); break; case 'n': b.Append('\n'); break; case 'r': b.Append('\r'); break; case 't': b.Append('\t'); break; case 'u': if (p + 4 > s.Length || !ushort.TryParse(s.Substring(p, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var u)) throw new FormatException("JSON Unicode 转义无效"); b.Append((char)u); p += 4; break; default: throw new FormatException("JSON 转义无效"); } } throw new FormatException("JSON 字符串不完整");
            }
        }
    }
}

