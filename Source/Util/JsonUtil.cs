using System;
using System.Collections;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace RimTalk.TTS.Util
{
    public static class JsonUtil
    {
        public static string SerializeToJson<T>(T obj)
        {
            using var stream = new MemoryStream();
            var serializer = new DataContractJsonSerializer(typeof(T));
            serializer.WriteObject(stream, obj);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        public static T DeserializeFromJson<T>(string json)
        {
            string sanitizedJson = Sanitize(json, typeof(T));
            
            try
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sanitizedJson));
                var serializer = new DataContractJsonSerializer(typeof(T));
                return (T)serializer.ReadObject(stream);
            }
            catch (Exception ex)
            {
                Verse.Log.Error($"[RimTalk.TTS] Json deserialization failed for {typeof(T).Name}: {ex.Message}\n{json}");
                throw;
            }
        }

        public static string Sanitize(string text, Type targetType)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string sanitized = text.Replace("```json", "").Replace("```", "").Trim();

            int startIndex = sanitized.IndexOfAny(new char[] { '{', '[' });
            int endIndex = sanitized.LastIndexOfAny(new char[] { '}', ']' });

            if (startIndex >= 0 && endIndex > startIndex)
            {
                sanitized = sanitized.Substring(startIndex, endIndex - startIndex + 1).Trim();
            }
            else
            {
                return string.Empty;
            }

            if (sanitized.Contains("]["))
            {
                 sanitized = sanitized.Replace("][", ",");
            }
            if (sanitized.Contains("}{"))
            {
                sanitized = sanitized.Replace("}{", "},{");
            }
            
            if (sanitized.StartsWith("{") && sanitized.EndsWith("}"))
            {
                string innerContent = sanitized.Substring(1, sanitized.Length - 2).Trim();
                if (innerContent.StartsWith("[") && innerContent.EndsWith("]"))
                {
                    sanitized = innerContent;
                }
            }

            bool isEnumerable = typeof(IEnumerable).IsAssignableFrom(targetType) && targetType != typeof(string);
            if (isEnumerable && sanitized.StartsWith("{"))
            {
                sanitized = $"[{sanitized}]";
            }

            return sanitized;
        }
    }
}
