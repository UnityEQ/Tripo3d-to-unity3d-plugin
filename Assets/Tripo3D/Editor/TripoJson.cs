using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Tripo3D.Editor
{
    internal static class TripoJson
    {
        public static string Object(params (string key, object value)[] fields)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            var first = true;
            for (var i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                if (field.value == null)
                    continue;

                if (!first)
                    sb.Append(',');
                first = false;
                sb.Append('"').Append(Escape(field.key)).Append("\":");
                Write(sb, field.value);
            }

            sb.Append('}');
            return sb.ToString();
        }

        public static TripoResponse Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new TripoException("Empty response from Tripo API.");

            try
            {
                return JsonUtility.FromJson<TripoResponse>(json);
            }
            catch (Exception ex)
            {
                throw new TripoException("Could not parse Tripo API response: " + ex.Message);
            }
        }

        public static TripoResponse RequireSuccess(string json)
        {
            var response = Parse(json);
            if (response == null)
                throw new TripoException("Could not parse Tripo API response.");

            if (response.code != 0)
                throw ToException(response.code, response.message, response.suggestion);

            if (response.data == null)
                throw new TripoException("Tripo API returned no data.");

            return response;
        }

        public static TripoException ToException(int code, string message, string suggestion)
        {
            if (IsCreditFailure(code, message))
            {
                var text = "Out of Tripo credits. Top up at platform.tripo3d.ai.";
                if (!string.IsNullOrEmpty(message) && message.IndexOf("credit", StringComparison.OrdinalIgnoreCase) < 0)
                    text = message + " " + text;
                if (!string.IsNullOrEmpty(suggestion))
                    return new TripoException(text, code, suggestion);
                return new TripoException(text, code, "Add credits, then click Generate again.");
            }

            var fallback = string.IsNullOrEmpty(message) ? "Tripo API error " + code : message;
            return new TripoException(fallback, code, suggestion);
        }

        public static bool IsCreditFailure(int code, string message)
        {
            if (code == 2010)
                return true;
            if (string.IsNullOrEmpty(message))
                return false;
            var m = message.ToLowerInvariant();
            return m.Contains("insufficient") && (m.Contains("credit") || m.Contains("token") || m.Contains("balance"))
                   || m.Contains("out of credit")
                   || m.Contains("no credit")
                   || m.Contains("not enough credit");
        }

        static void Write(StringBuilder sb, object value)
        {
            switch (value)
            {
                case string s:
                    sb.Append('"').Append(Escape(s)).Append('"');
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case float f:
                    sb.Append(f.ToString(CultureInfo.InvariantCulture));
                    break;
                case double d:
                    sb.Append(d.ToString(CultureInfo.InvariantCulture));
                    break;
                case string[] arr:
                    WriteStringArray(sb, arr);
                    break;
                default:
                    sb.Append('"').Append(Escape(Convert.ToString(value, CultureInfo.InvariantCulture))).Append('"');
                    break;
            }
        }

        public static string ExtractString(string json, string field)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(field))
                return null;
            var token = "\"" + field + "\"";
            var index = json.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return null;
            var colon = json.IndexOf(':', index + token.Length);
            if (colon < 0)
                return null;
            var q1 = json.IndexOf('"', colon + 1);
            if (q1 < 0)
                return null;
            var q2 = q1 + 1;
            while (q2 < json.Length)
            {
                if (json[q2] == '"' && json[q2 - 1] != '\\')
                    break;
                q2++;
            }
            if (q2 >= json.Length)
                return null;
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }

        public static void MergeOutputUrls(TripoOutput output, string json)
        {
            if (output == null || string.IsNullOrEmpty(json))
                return;
            if (string.IsNullOrEmpty(output.model_url))
                output.model_url = First(ExtractString(json, "model_url"), ExtractString(json, "model"));
            if (string.IsNullOrEmpty(output.pbr_model_url))
                output.pbr_model_url = First(ExtractString(json, "pbr_model_url"), ExtractString(json, "pbr_model"));
            if (string.IsNullOrEmpty(output.base_model_url))
                output.base_model_url = First(ExtractString(json, "base_model_url"), ExtractString(json, "base_model"));
            if (string.IsNullOrEmpty(output.rendered_image_url))
                output.rendered_image_url = First(ExtractString(json, "rendered_image_url"), ExtractString(json, "rendered_image"));
        }

        static string First(string a, string b)
        {
            return !string.IsNullOrEmpty(a) ? a : b;
        }

        public static string[] ExtractPartNames(string json)
        {
            var names = new System.Collections.Generic.List<string>();
            if (string.IsNullOrEmpty(json))
                return names.ToArray();

            CollectStringArray(json, "part_names", names);
            CollectStringArray(json, "partNames", names);

            var unique = new System.Collections.Generic.List<string>();
            for (var i = 0; i < names.Count; i++)
            {
                var n = names[i];
                if (string.IsNullOrEmpty(n) || n.StartsWith("http", StringComparison.OrdinalIgnoreCase) || n.StartsWith("task_", StringComparison.OrdinalIgnoreCase) || n.IndexOf('/') >= 0)
                    continue;
                var seen = false;
                for (var j = 0; j < unique.Count; j++)
                {
                    if (string.Equals(unique[j], n, StringComparison.OrdinalIgnoreCase))
                    {
                        seen = true;
                        break;
                    }
                }

                if (!seen)
                    unique.Add(n);
            }

            return unique.ToArray();
        }

        static void CollectStringArray(string json, string field, System.Collections.Generic.List<string> names)
        {
            var token = "\"" + field + "\"";
            var index = json.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return;
            var bracket = json.IndexOf('[', index + token.Length);
            if (bracket < 0)
                return;
            var end = json.IndexOf(']', bracket + 1);
            if (end < 0)
                return;
            var inner = json.Substring(bracket + 1, end - bracket - 1);
            var start = 0;
            while (start < inner.Length)
            {
                var q1 = inner.IndexOf('"', start);
                if (q1 < 0)
                    break;
                var q2 = inner.IndexOf('"', q1 + 1);
                if (q2 < 0)
                    break;
                names.Add(inner.Substring(q1 + 1, q2 - q1 - 1));
                start = q2 + 1;
            }
        }

        static void WriteStringArray(StringBuilder sb, string[] arr)
        {
            sb.Append('[');
            var first = true;
            if (arr != null)
            {
                for (var i = 0; i < arr.Length; i++)
                {
                    if (string.IsNullOrEmpty(arr[i]))
                        continue;
                    if (!first)
                        sb.Append(',');
                    first = false;
                    sb.Append('"').Append(Escape(arr[i])).Append('"');
                }
            }

            sb.Append(']');
        }

        static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
