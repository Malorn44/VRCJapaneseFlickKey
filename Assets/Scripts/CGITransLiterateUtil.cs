using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Threading.Tasks;
using UnityWebRequestUtil;

namespace CGITransLiterateUtil
{
    public static class TransLiterateSystem
    {
        private const string apiUrl = "https://www.google.com/transliterate";

        public static async Task<Newtonsoft.Json.Linq.JArray> GetJapaneseConversionAsync(string input)
        {
            string url = $"{apiUrl}?langpair=ja-Hira|ja&text={UnityWebRequest.EscapeURL(input)}";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var result =  JsonConvert.DeserializeObject<List<List<object>>>(request.downloadHandler.text);
                    var item = result[0][1] as Newtonsoft.Json.Linq.JArray;
                    item.AddFirst(result[0][0].ToString());
                    return item;
                }
                else
                {
                    Debug.LogError($"Error: {request.error}");
                    return null;
                }
            }
        }

        // Every segment of the reading, with its candidates. Text after a comma comes back as one segment
        // ("きょう,はさむいですね" gives きょう and はさむいですね), so a fixed first group goes alone with
        // a trailing comma and the rest in a request of its own.
        public static async Task<List<(string reading, List<string> candidates)>> GetSegmentsAsync(string input)
        {
            string url = $"{apiUrl}?langpair=ja-Hira|ja&text={UnityWebRequest.EscapeURL(input)}";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Error: {request.error}");
                    return null;
                }
                var result = JsonConvert.DeserializeObject<List<List<object>>>(request.downloadHandler.text);
                var segments = new List<(string reading, List<string> candidates)>();
                foreach (var segment in result)
                {
                    var candidates = (segment[1] as Newtonsoft.Json.Linq.JArray).ToObject<List<string>>();
                    segments.Add((segment[0].ToString(), candidates));
                }
                return segments;
            }
        }
    }
}