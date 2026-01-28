using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using Firebase;
using Firebase.Functions;
using Firebase.Firestore;
using Firebase.Auth;
using UnityEngine;

namespace GRoll.Infrastructure.Firebase.Services
{
    [Serializable]
    public class GalleryItemDTO
    {
        public string id;
        public string pngUrl;
        public string descriptionText;
        public string guidanceKey;
    }

    [Serializable]
    public class SequencedMapsResponse
    {
        public bool ok;
        public int count;
        public List<int> pattern;
        public List<SequencedMapEntry> entries;
    }

    [Serializable]
    public class SequencedMapEntry
    {
        public string mapId;
        public int difficultyTag;
        public string json;
    }

    /// <summary>
    /// Remote service for fetching app data from Firebase Functions and Firestore.
    /// Handles gallery items, map data, and sequenced maps for gameplay.
    /// </summary>
    public class RemoteAppDataService : MonoBehaviour
    {
        [Header("Logging")]
        public bool enableDebugLogs = false;

        [Tooltip("Firebase Functions region")]
        public string region = "us-central1";

        [Header("Firestore (Maps JSON)")]
        [Tooltip("Root collection for app data")] public string appdataCollection = "appdata";
        [Tooltip("Document under appdata that groups maps")] public string mapsDocument = "maps";

        private FirebaseFirestore _db;
        private FirebaseFunctions _funcs;

        private void D(string msg) { if (!enableDebugLogs) return; UnityEngine.Debug.Log($"[RemoteAppDataService] {msg}"); }
        private void W(string msg) { if (!enableDebugLogs) return; UnityEngine.Debug.LogWarning($"[RemoteAppDataService] {msg}"); }
        private void E(string msg) { UnityEngine.Debug.LogError($"[RemoteAppDataService] {msg}"); }

        private static string JoinKeys(IDictionary<string, object> d)
        {
            if (d == null) return "<null>";
            var arr = new List<string>();
            foreach (var k in d.Keys) arr.Add(k ?? "<null>");
            return string.Join(",", arr);
        }

        private static string JoinKeysNG(System.Collections.IDictionary d)
        {
            if (d == null) return "<null>";
            var arr = new List<string>();
            foreach (var k in d.Keys) arr.Add(k?.ToString() ?? "<null>");
            return string.Join(",", arr);
        }

        private static bool TryGetNG(System.Collections.IDictionary d, string key, out object value)
        {
            if (d != null && d.Contains(key))
            {
                value = d[key];
                return true;
            }
            value = null;
            return false;
        }

        private void Awake()
        {
            D($"Awake() region='{region}' – Functions will be lazily initialized on first call.");
        }

        private async Task<bool> EnsureFunctionsReadyAsync()
        {
            if (_funcs != null) return true;

            D("EnsureFunctionsReadyAsync: Checking Firebase dependencies...");
            var status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (status != DependencyStatus.Available)
            {
                E($"Dependencies not available: {status}");
                return false;
            }

            var app = FirebaseApp.DefaultInstance;
            if (app == null)
            {
                E("DefaultInstance is null after dependencies check (Functions).");
                return false;
            }

            _funcs = FirebaseFunctions.GetInstance(app, region);
            D($"Functions bound to DefaultInstance region='{region}'");
            return _funcs != null;
        }

        private async Task EnsureSignedInAsync()
        {
            var auth = FirebaseAuth.DefaultInstance;
            if (auth.CurrentUser != null) return;
            var cred = await auth.SignInAnonymouslyAsync();
            D($"Auth: signed in anonymously uid={cred?.User?.UserId}");
        }

        private async Task<bool> EnsureFirestoreReadyAsync()
        {
            if (_db != null) return true;
            D("EnsureFirestoreReadyAsync: Checking Firebase dependencies...");
            var status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (status != DependencyStatus.Available)
            {
                E($"Dependencies not available (Firestore): {status}");
                return false;
            }
            var app = FirebaseApp.DefaultInstance;
            if (app == null)
            {
                E("DefaultInstance is null after dependencies check (Firestore).");
                return false;
            }
            _db = FirebaseFirestore.GetInstance(FirebaseApp.DefaultInstance, "getfi");
            D($"Firestore instance created? {_db != null}");
            return _db != null;
        }

        public async Task<List<GalleryItemDTO>> FetchGalleryItemsAsync(
            string collectionPath = "appdata/galleryitems/itemdata",
            string[] ids = null)
        {
            var sw = Stopwatch.StartNew();
            D($"FetchGalleryItemsAsync START path='{collectionPath}', ids=[{(ids == null ? "null" : string.Join(",", ids))}]");

            var result = new List<GalleryItemDTO>();

            if (!await EnsureFunctionsReadyAsync())
            {
                E("Functions instance null (init failed).");
                sw.Stop();
                D($"FetchGalleryItemsAsync END (init failed) in {sw.ElapsedMilliseconds} ms");
                return result;
            }

            try
            {
                var payload = new Dictionary<string, object>
                {
                    { "collectionPath", collectionPath }
                };
                if (ids != null && ids.Length > 0) payload["ids"] = ids;

                D($"Callable payload prepared: path='{collectionPath}', ids=[{(ids == null ? "null" : string.Join(",", ids))}]");

                var callable = _funcs.GetHttpsCallable("getGalleryItems");
                D("Invoking callable 'getGalleryItems'...");
                var resp = await callable.CallAsync(payload);

                D($"Callable returned. resp null? {(resp == null)}");
                if (resp == null)
                {
                    W("Callable response is null (unexpected)");
                    sw.Stop();
                    D($"FetchGalleryItemsAsync END (resp null) in {sw.ElapsedMilliseconds} ms");
                    return result;
                }

                D($"resp.Data type: {resp.Data?.GetType().FullName ?? "<null>"}");

                if (resp.Data is IDictionary<string, object> root)
                {
                    D($"Root keys: {JoinKeys(root)}");
                    ParseItemsFromRoot(root, result);
                }
                else if (resp.Data is System.Collections.IDictionary rootNG)
                {
                    D($"Root(NG) keys: {JoinKeysNG(rootNG)}");
                    ParseItemsFromRootNG(rootNG, result);
                }
                else if (resp.Data is IList<object> rootList)
                {
                    D($"Root is a LIST with count={rootList.Count} (no wrapper).");
                    ParseItemsFromList(rootList, result);
                }
                else if (resp.Data is string s)
                {
                    W($"resp.Data is STRING: '{s}'");
                }
                else
                {
                    var typeName = resp.Data?.GetType().FullName ?? "<null>";
                    var str = resp.Data?.ToString();
                    W($"resp.Data is not a dictionary or list – cannot parse root. type={typeName} toString='{(str ?? "<null>")}'");
                }

                D($"Parsed {result.Count} gallery items");
            }
            catch (FirebaseException fx)
            {
                E($"Callable error (FirebaseException): {fx.Message}\n{fx.StackTrace}");
            }
            catch (Exception e)
            {
                E($"FetchGalleryItemsAsync exception: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                sw.Stop();
                D($"FetchGalleryItemsAsync END in {sw.ElapsedMilliseconds} ms");
            }

            return result;
        }

        private void ParseItemsFromRoot(IDictionary<string, object> root, List<GalleryItemDTO> result)
        {
            if (!root.TryGetValue("items", out var itemsObj))
            {
                W("'items' key missing in response root");
                return;
            }

            if (itemsObj is IList<object> list)
            {
                D($"'items' is a list with count={list.Count}");
                for (int idx = 0; idx < list.Count; idx++)
                {
                    if (list[idx] is IDictionary<string, object> d)
                    {
                        result.Add(ParseGalleryItem(d, idx));
                    }
                    else
                    {
                        W($"#{idx} item is not a dictionary.");
                    }
                }
            }
            else
            {
                W($"'items' is not a list. type={itemsObj?.GetType().FullName ?? "<null>"}");
            }
        }

        private void ParseItemsFromRootNG(System.Collections.IDictionary rootNG, List<GalleryItemDTO> result)
        {
            if (!TryGetNG(rootNG, "items", out var itemsObjNG))
            {
                W("'items' key missing in root (non-generic)");
                return;
            }

            if (itemsObjNG is System.Collections.IList listNG)
            {
                D($"'items'(NG) is a list with count={listNG.Count}");
                for (int idx = 0; idx < listNG.Count; idx++)
                {
                    var it = listNG[idx];
                    if (it is IDictionary<string, object> d)
                    {
                        result.Add(ParseGalleryItem(d, idx));
                    }
                    else if (it is System.Collections.IDictionary dng)
                    {
                        result.Add(ParseGalleryItemNG(dng, idx));
                    }
                    else
                    {
                        W($"NG #{idx} item is not a dictionary.");
                    }
                }
            }
            else
            {
                W($"'items'(NG) is not a list. type={itemsObjNG?.GetType().FullName ?? "<null>"}");
            }
        }

        private void ParseItemsFromList(IList<object> rootList, List<GalleryItemDTO> result)
        {
            for (int idx = 0; idx < rootList.Count; idx++)
            {
                if (rootList[idx] is IDictionary<string, object> it)
                {
                    result.Add(ParseGalleryItem(it, idx));
                }
                else
                {
                    W($"Root list item #{idx} not a dict.");
                }
            }
        }

        private GalleryItemDTO ParseGalleryItem(IDictionary<string, object> d, int idx)
        {
            string id = d.TryGetValue("id", out var _id) ? _id as string : "";
            string url = d.TryGetValue("pngUrl", out var _u) ? _u as string : "";
            string desc = d.TryGetValue("descriptionText", out var _dt) ? _dt as string : "";
            string key = d.TryGetValue("guidanceKey", out var _gk) ? _gk as string : "";

            D($"#{idx} values: id='{id}', key='{key}', url='{url}', descLen={(desc?.Length ?? 0)}");

            return new GalleryItemDTO
            {
                id = id ?? "",
                pngUrl = url ?? "",
                descriptionText = desc ?? "",
                guidanceKey = key ?? ""
            };
        }

        private GalleryItemDTO ParseGalleryItemNG(System.Collections.IDictionary dng, int idx)
        {
            string id = (TryGetNG(dng, "id", out var _id) ? _id as string : "") ?? "";
            string url = (TryGetNG(dng, "pngUrl", out var _u) ? _u as string : "") ?? "";
            string desc = (TryGetNG(dng, "descriptionText", out var _dt) ? _dt as string : "") ?? "";
            string key = (TryGetNG(dng, "guidanceKey", out var _gk) ? _gk as string : "") ?? "";

            D($"NG #{idx} values: id='{id}', key='{key}', url='{url}', descLen={(desc?.Length ?? 0)}");

            return new GalleryItemDTO
            {
                id = id,
                pngUrl = url,
                descriptionText = desc,
                guidanceKey = key
            };
        }

        public async Task SaveMapJsonAsync(string mapId, string fieldKey, string jsonString)
        {
            if (string.IsNullOrWhiteSpace(jsonString)) { E("SaveMapJsonAsync: jsonString is empty"); return; }
            if (string.IsNullOrWhiteSpace(mapId)) { E("SaveMapJsonAsync: mapId is empty"); return; }

            mapId = SanitizeId(mapId);
            fieldKey = SanitizeFieldKey(string.IsNullOrWhiteSpace(fieldKey) ? mapId : fieldKey);

            await EnsureSignedInAsync();

            if (!await EnsureFirestoreReadyAsync())
            {
                E("SaveMapJsonAsync: Firestore init failed");
                return;
            }

            try
            {
                var doc = _db.Collection(appdataCollection)
                    .Document(mapsDocument)
                    .Collection(mapId)
                    .Document("raw");

                var payload = new Dictionary<string, object>
                {
                    { fieldKey, jsonString }
                };

                D($"SaveMapJsonAsync → path: {appdataCollection}/{mapsDocument}/{mapId}/raw | field='{fieldKey}' size={jsonString?.Length ?? 0}");
                await doc.SetAsync(payload, SetOptions.MergeAll);
                D("SaveMapJsonAsync: Firestore write OK");
            }
            catch (Exception ex)
            {
                E($"SaveMapJsonAsync exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static string SanitizeId(string s)
        {
            if (string.IsNullOrEmpty(s)) return "untitled";
            return s.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        }

        private static string SanitizeFieldKey(string s)
        {
            if (string.IsNullOrEmpty(s)) return "map";
            return s.Replace('.', '_').Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        }

        public async Task<SequencedMapsResponse> GetSequencedMapsAsync(int count, string seed = null)
        {
            try
            {
                var dep = await FirebaseApp.CheckAndFixDependenciesAsync();
                if (dep != DependencyStatus.Available)
                {
                    UnityEngine.Debug.LogError($"[RemoteAppDataService] Deps not available: {dep}");
                    return new SequencedMapsResponse { ok = false, entries = new List<SequencedMapEntry>() };
                }

                var auth = FirebaseAuth.DefaultInstance;
                if (auth.CurrentUser == null)
                {
                    var res = await auth.SignInAnonymouslyAsync();
                    D($"Auth anon uid={res?.User?.UserId}");
                }

                var app = FirebaseApp.DefaultInstance;
                if (app == null)
                {
                    UnityEngine.Debug.LogError("[RemoteAppDataService] DefaultInstance is null for Functions.");
                    return new SequencedMapsResponse { ok = false, entries = new List<SequencedMapEntry>() };
                }

                var usedRegion = string.IsNullOrWhiteSpace(region) ? "us-central1" : region;
                var functions = FirebaseFunctions.GetInstance(app, usedRegion);
                var callable = functions.GetHttpsCallable("getSequencedMaps");

                var payload = new Dictionary<string, object>
                {
                    { "count", Mathf.Clamp(count, 1, 50) }
                };
                if (!string.IsNullOrWhiteSpace(seed))
                    payload["seed"] = seed.Trim();

                D($"Calling getSequencedMaps count={payload["count"]} seed='{(payload.ContainsKey("seed") ? payload["seed"] : "")}'");
                var resp = await callable.CallAsync(payload);
                var raw = resp?.Data;
                D($"getSequencedMaps raw type: {raw?.GetType().FullName ?? "null"}");

                var dict = ToStringDictFromAny(raw);
                if (dict != null)
                    return ParseResponseFromDictionary(dict);

                if (raw is string jsonText)
                {
                    try
                    {
                        var dto = JsonUtility.FromJson<SequencedMapsResponse>(jsonText);
                        return dto ?? new SequencedMapsResponse { ok = false, entries = new List<SequencedMapEntry>() };
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"[RemoteAppDataService] JSON parse fail: {ex.Message}");
                    }
                }

                UnityEngine.Debug.LogError("[RemoteAppDataService] Unexpected response payload.");
                return new SequencedMapsResponse { ok = false, entries = new List<SequencedMapEntry>() };
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[RemoteAppDataService] GetSequencedMapsAsync error: {e.Message}\n{e}");
                return new SequencedMapsResponse { ok = false, entries = new List<SequencedMapEntry>() };
            }
        }

        private static IDictionary<string, object> ToStringDictFromAny(object obj)
        {
            if (obj is IDictionary<string, object> ds) return ds;
            if (obj is Dictionary<string, object> dso) return dso;
            if (obj is IDictionary<object, object> doo)
            {
                var conv = new Dictionary<string, object>();
                foreach (var kv in doo)
                {
                    var key = kv.Key?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(key)) conv[key] = kv.Value;
                }
                return conv;
            }
            return null;
        }

        private static SequencedMapsResponse ParseResponseFromDictionary(IDictionary<string, object> data)
        {
            var resp = new SequencedMapsResponse
            {
                ok = data.ContainsKey("ok") && data["ok"] is bool b && b,
                count = data.ContainsKey("count") ? Convert.ToInt32(data["count"]) : 0,
                pattern = new List<int>(),
                entries = new List<SequencedMapEntry>()
            };

            if (data.TryGetValue("pattern", out var pObj) && pObj is IList<object> pList)
            {
                foreach (var v in pList)
                    resp.pattern.Add(Convert.ToInt32(v));
            }

            if (data.TryGetValue("entries", out var eObj) && eObj is IList<object> eList)
            {
                foreach (var itemAny in eList)
                {
                    var item = ToStringDictFromAny(itemAny);
                    if (item == null) continue;

                    var entry = new SequencedMapEntry
                    {
                        mapId = item.TryGetValue("mapId", out var _id) ? _id?.ToString() : "",
                        difficultyTag = item.TryGetValue("difficultyTag", out var _d) ? Convert.ToInt32(_d) : 0,
                        json = item.TryGetValue("json", out var _j) && _j is string js ? js : ""
                    };
                    resp.entries.Add(entry);
                }
            }

            return resp;
        }
    }

    internal static class DictExtensions
    {
        public static bool TryGetValue(this IDictionary<string, object> d, string key, out object value)
        {
            if (d != null && d.ContainsKey(key))
            {
                value = d[key];
                return true;
            }
            value = null;
            return false;
        }
    }
}
