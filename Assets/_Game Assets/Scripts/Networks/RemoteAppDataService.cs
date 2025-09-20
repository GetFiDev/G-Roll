using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;                     // Stopwatch
using Firebase;                               // FirebaseException
using Firebase.Functions;
using Firebase.Extensions;                    // ContinueWithOnMainThread (gerekirse)
using UnityEngine;

namespace RemoteApp
{
    [Serializable]
    public class GalleryItemDTO
    {
        public string id;
        public string pngUrl;
        public string descriptionText;
        public string guidanceKey;
    }

    public class RemoteAppDataService : MonoBehaviour
    {
        [Header("Logging")]
        public bool enableDebugLogs = false;

        // unified log helpers (instance-based)
        private void D(string msg) { if (!enableDebugLogs) return; UnityEngine.Debug.Log($"[RemoteAppDataService] {msg}"); }
        private void W(string msg) { if (!enableDebugLogs) return; UnityEngine.Debug.LogWarning($"[RemoteAppDataService] {msg}"); }
        private void E(string msg) { UnityEngine.Debug.LogError($"[RemoteAppDataService] {msg}"); }

        [Tooltip("Firebase Functions region")]
        public string region = "us-central1";

        private FirebaseFunctions _funcs;

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
            // Firebase hazır olmadan Functions yaratmaya çalışma — sadece bilgi logu at.
            D($"Awake() region='{region}' – Functions will be lazily initialized on first call.");
        }

        /// Ensure Functions is ready (lazy init, safe to call many times)
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
                E("DefaultInstance is null after dependencies check.");
                return false;
            }

            _funcs = FirebaseFunctions.GetInstance(app, region);
            D($"Functions instance created? {_funcs != null}");
            return _funcs != null;
        }

        public async Task<List<GalleryItemDTO>> FetchGalleryItemsAsync(
            string collectionPath = "appdata/galleryitems/itemdata",
            string[] ids = null)
        {
            var sw = Stopwatch.StartNew();
            D($"FetchGalleryItemsAsync START path='{collectionPath}', ids=[{(ids==null?"null":string.Join(",", ids))}]");

            var result = new List<GalleryItemDTO>();

            // 🔧 LAZY INIT
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

                D($"Callable payload prepared: path='{collectionPath}', ids=[{(ids==null?"null":string.Join(",", ids))}]");

                var callable = _funcs.GetHttpsCallable("getGalleryItems");
                D("Invoking callable 'getGalleryItems'...");
                var resp = await callable.CallAsync(payload);

                D($"Callable returned. resp null? { (resp==null) }");
                if (resp == null)
                {
                    W("Callable response is null (unexpected)");
                    sw.Stop();
                    D($"FetchGalleryItemsAsync END (resp null) in {sw.ElapsedMilliseconds} ms");
                    return result;
                }

                D($"resp.Data type: {resp.Data?.GetType().FullName ?? "<null>"}");

                // ---- Robust parse ----
                if (resp.Data is IDictionary<string, object> root)
                {
                    D($"Root keys: {JoinKeys(root)}");

                    if (!root.TryGetValue("items", out var itemsObj))
                    {
                        W("'items' key missing in response root");
                    }
                    else if (itemsObj is IList<object> list)
                    {
                        D($"'items' is a list with count={list.Count}");
                        for (int idx = 0; idx < list.Count; idx++)
                        {
                            var it = list[idx];
                            if (it is IDictionary<string, object> d)
                            {
                                D($"#{idx} keys: {JoinKeys(d)}");

                                string id   = d.TryGetValue("id", out var _id) ? _id as string : "";
                                string url  = d.TryGetValue("pngUrl", out var _u) ? _u as string : "";
                                string desc = d.TryGetValue("descriptionText", out var _dt) ? _dt as string : "";
                                string key  = d.TryGetValue("guidanceKey", out var _gk) ? _gk as string : "";

                                D($"#{idx} values: id='{id}', key='{key}', url='{url}', descLen={(desc?.Length ?? 0)}");

                                result.Add(new GalleryItemDTO
                                {
                                    id = id ?? "",
                                    pngUrl = url ?? "",
                                    descriptionText = desc ?? "",
                                    guidanceKey = key ?? ""
                                });
                            }
                            else
                            {
                                W($"#{idx} item is not a dictionary. type={it?.GetType().FullName ?? "<null>"}");
                            }
                        }
                    }
                    else
                    {
                        W($"'items' is not a list. type={itemsObj?.GetType().FullName ?? "<null>"}");
                    }
                }
                else if (resp.Data is System.Collections.IDictionary rootNG)
                {
                    D($"Root(NG) keys: {JoinKeysNG(rootNG)}");

                    if (!TryGetNG(rootNG, "items", out var itemsObjNG))
                    {
                        W("'items' key missing in root (non-generic)");
                    }
                    else if (itemsObjNG is System.Collections.IList listNG)
                    {
                        D($"'items'(NG) is a list with count={listNG.Count}");
                        for (int idx = 0; idx < listNG.Count; idx++)
                        {
                            var it = listNG[idx];
                            if (it is IDictionary<string, object> d)
                            {
                                D($"NG #{idx} keys(generic): {JoinKeys(d)}");

                                string id   = d.TryGetValue("id", out var _id) ? _id as string : "";
                                string url  = d.TryGetValue("pngUrl", out var _u) ? _u as string : "";
                                string desc = d.TryGetValue("descriptionText", out var _dt) ? _dt as string : "";
                                string key  = d.TryGetValue("guidanceKey", out var _gk) ? _gk as string : "";

                                D($"NG #{idx} values(generic): id='{id}', key='{key}', url='{url}', descLen={(desc?.Length ?? 0)}");

                                result.Add(new GalleryItemDTO
                                {
                                    id = id ?? "",
                                    pngUrl = url ?? "",
                                    descriptionText = desc ?? "",
                                    guidanceKey = key ?? ""
                                });
                            }
                            else if (it is System.Collections.IDictionary dng)
                            {
                                D($"NG #{idx} keys(non-generic): {JoinKeysNG(dng)}");

                                string id   = (TryGetNG(dng, "id", out var _id) ? _id as string : "") ?? "";
                                string url  = (TryGetNG(dng, "pngUrl", out var _u) ? _u as string : "") ?? "";
                                string desc = (TryGetNG(dng, "descriptionText", out var _dt) ? _dt as string : "") ?? "";
                                string key  = (TryGetNG(dng, "guidanceKey", out var _gk) ? _gk as string : "") ?? "";

                                D($"NG #{idx} values(non-generic): id='{id}', key='{key}', url='{url}', descLen={(desc?.Length ?? 0)}");

                                result.Add(new GalleryItemDTO
                                {
                                    id = id,
                                    pngUrl = url,
                                    descriptionText = desc,
                                    guidanceKey = key
                                });
                            }
                            else
                            {
                                W($"NG #{idx} item is not a dictionary. type={it?.GetType().FullName ?? "<null>"}");
                            }
                        }
                    }
                    else
                    {
                        W($"'items'(NG) is not a list. type={itemsObjNG?.GetType().FullName ?? "<null>"}");
                    }
                }
                else if (resp.Data is IList<object> rootList)
                {
                    // Bazı backend’ler direkt array döndürebilir — destek verelim
                    D($"Root is a LIST with count={rootList.Count} (no wrapper).");
                    for (int idx = 0; idx < rootList.Count; idx++)
                    {
                        var it = rootList[idx] as IDictionary<string, object>;
                        if (it == null)
                        {
                            W($"Root list item #{idx} not a dict. type={rootList[idx]?.GetType().FullName ?? "<null>"}");
                            continue;
                        }

                        string id   = it.TryGetValue("id", out var _id) ? _id as string : "";
                        string url  = it.TryGetValue("pngUrl", out var _u) ? _u as string : "";
                        string desc = it.TryGetValue("descriptionText", out var _dt) ? _dt as string : "";
                        string key  = it.TryGetValue("guidanceKey", out var _gk) ? _gk as string : "";

                        D($"LIST #{idx} values: id='{id}', key='{key}', url='{url}', descLen={(desc?.Length ?? 0)}");

                        result.Add(new GalleryItemDTO
                        {
                            id = id ?? "",
                            pngUrl = url ?? "",
                            descriptionText = desc ?? "",
                            guidanceKey = key ?? ""
                        });
                    }
                }
                else if (resp.Data is string s)
                {
                    // Hata mesajı vs. döndüyse görebilelim
                    W($"resp.Data is STRING: '{s}'");
                }
                else
                {
                    var typeName = resp.Data?.GetType().FullName ?? "<null>";
                    var str = resp.Data?.ToString();
                    W($"resp.Data is not a dictionary or list – cannot parse root. type={typeName} toString='{(str ?? "<null>")}'");
                }

                D($"Parsed {result.Count} gallery items");
                for (int i = 0; i < result.Count; i++)
                {
                    var it = result[i];
                    D($"Parsed #{i}: id='{it.id}' key='{it.guidanceKey}' url='{it.pngUrl}' descLen={(it.descriptionText?.Length ?? 0)}");
                }
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
    }

    internal static class DictExt
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