using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using Firebase.Functions;
using UnityEngine;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using NetworkingData;
using System.Linq;
using System.Collections.Generic;
using System.Collections;


public class UserDatabaseManager : MonoBehaviour
{
    public static UserDatabaseManager Instance { get; private set; }
    // --- Log & Durum Event'leri ---
    public event Action<string> OnLog;
    public event Action OnRegisterSucceeded;
    public event Action<string> OnRegisterFailed;
    public event Action OnLoginSucceeded;
    public event Action<string> OnLoginFailed;
    public event Action<UserData> OnUserDataSaved;

    // Capability flag for leaderboard all-rows fetch
    public bool HasMethod_FetchLeaderboardAll => true;


    // Main-thread güvenli dispatch için
    private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
    private void EnqueueMain(Action a) { if (a != null) _mainThreadQueue.Enqueue(a); }
    private void Update() { while (_mainThreadQueue.TryDequeue(out var a)) a?.Invoke(); }

    // --- Firebase alanlar ---
    private FirebaseAuth auth;
    public FirebaseUser currentUser;
    private FirebaseFirestore db;
    private FirebaseFunctions _funcs;

    // Server-only alanlar: client bu alanları asla yazmamalı (rules ile de yasak)
    private static readonly string[] SERVER_ONLY_KEYS = new[] {
        "hasElitePass", "elitePassExpiresAt",
        "score", "lastLogin", "createdAt", "updatedAt",
        "referralKey", "referredByKey", "referredByUid", "referralAppliedAt"
    };

    private static void StripServerOnlyKeys(System.Collections.Generic.Dictionary<string, object> dict)
    {
        if (dict == null) return;
        foreach (var k in SERVER_ONLY_KEYS)
        {
            if (dict.ContainsKey(k)) dict.Remove(k);
        }
    }

    [Sirenix.OdinInspector.ReadOnly] public string currentLoggedUserID;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // İstersen kaldır
    }
    void Start() => InitializeFirebase();

    private void InitializeFirebase()
    {
        Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == Firebase.DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                db = FirebaseFirestore.GetInstance(Firebase.FirebaseApp.DefaultInstance, "getfi");

                if (auth.CurrentUser == null)
                {
                    auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(t =>
                    {
                        if (t.IsCompletedSuccessfully)
                        {
                            currentUser = auth.CurrentUser;
                            EmitLog("✅ Anonymous signed in: " + currentUser?.UserId);
                        }
                        else
                        {
                            EmitLog("❌ Anonymous sign-in failed: " + t.Exception?.Message);
                        }
                    });
                }

                EmitLog("✅ Firebase ready");
                _funcs = FirebaseFunctions.GetInstance("us-central1");
                EmitLog("✅ Functions bound to us-central1");
            }
            else
            {
                EmitLog($"❌ Firebase deps not available: {task.Result}");
            }
        });
    }

    /// <summary>
    /// Computes local timezone offset in minutes (e.g., Istanbul +180) for daily streak calculation.
    /// </summary>
    private static int GetLocalTzOffsetMinutes()
    {
        var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
        return (int)Math.Round(offset.TotalMinutes);
    }

    /// <summary>
    /// Calls the `updateDailyStreak` callable on the server. Safe to call multiple times per day (no-op if already updated).
    /// </summary>
    private async Task<bool> UpdateDailyStreakAsync()
    {
        if (_funcs == null)
        {
            EmitLog("⚠️ updateDailyStreak: Functions not initialized");
            return false;
        }
        if (currentUser == null)
        {
            EmitLog("⚠️ updateDailyStreak: No Login");
            return false;
        }
        try
        {
            var callable = _funcs.GetHttpsCallable("updateDailyStreak");
            var payload = new System.Collections.Generic.Dictionary<string, object>
            {
                { "tzOffsetMinutes", GetLocalTzOffsetMinutes() }
            };
            var resp = await callable.CallAsync(payload);
            EmitLog("✅ updateDailyStreak ok");
            return true;
        }
        catch (Exception e)
        {
            EmitLog("⚠️ updateDailyStreak error: " + e.Message);
            return false;
        }
    }

    // --- REGISTER ---
    public async void Register(string email, string password, string referralCode)
    {
        if (auth == null) { EmitLog("❌ Auth null"); return; }

        try
        {
            var r = await auth.CreateUserWithEmailAndPasswordAsync(email, password);
            currentUser = r.User;
            currentLoggedUserID = currentUser?.UserId;
            EmitLog($"✅ Register Successful, UID: {currentLoggedUserID}");

            // Sunucuda profil oluşturulduğundan (Auth trigger) emin olmak için callable:
            await EnsureUserProfileAsync(referralCode);
            // Daily streak: update once on sign-up/login
            await UpdateDailyStreakAsync();

            // Taze veriyi çek ve UI’a yayınla
            var data = await LoadUserData();

            EnqueueMain(() =>
            {
                OnRegisterSucceeded?.Invoke();
                OnLoginSucceeded?.Invoke();        // UI kapanması için
                if (data != null) OnUserDataSaved?.Invoke(data); // HUD/istatistikler yenilensin
            });
        }
        catch (Exception e)
        {
            EmitLog("❌ Register error: " + e.Message);
            EnqueueMain(() => OnRegisterFailed?.Invoke(e.Message));
        }
    }

    // --- LOGIN ---
    public async void Login(string email, string password)
    {
        if (auth == null) { EmitLog("❌ Auth null"); return; }

        try
        {
            var r = await auth.SignInWithEmailAndPasswordAsync(email, password);
            currentUser = r.User;
            currentLoggedUserID = currentUser?.UserId;
            EmitLog($"✅ Login succeed, UID: {currentLoggedUserID}");

            // Profil var/yok normalize et (lastLogin güncellemesini artık server yapıyor)
            await EnsureUserProfileAsync();
            // Daily streak: update once on sign-in
            await UpdateDailyStreakAsync();

            // En güncel user data
            var data = await LoadUserData();

            EnqueueMain(() =>
            {
                OnLoginSucceeded?.Invoke();
                if (data != null) OnUserDataSaved?.Invoke(data);  // HUD tazele
            });
        }
        catch (Exception e)
        {
            EmitLog("❌ Login error: " + e.Message);
            EnqueueMain(() => OnLoginFailed?.Invoke(e.Message));
        }
    }


    // --- LOAD ---
    public async Task<UserData> LoadUserData()
    {
        if (currentUser == null) { EmitLog("❌ No Login"); return null; }

        try
        {
            var snap = await UserDoc().GetSnapshotAsync();
            if (!snap.Exists)
            {
                EmitLog("⚠️ No user doc found");
                return null;
            }

            var data = snap.ConvertTo<UserData>(); // Eksikler property defaultlarına düşer
            EmitLog("✅ User data loaded");
            return data;
        }
        catch (Exception e)
        {
            EmitLog("❌ Data load error: " + e.Message);
            return null;
        }
    }

    // --- SAVE (merge: true ise sadece verilen alanları günceller) ---
    public async void SaveUserData(UserData data, bool merge = true)
    {
        if (currentUser == null) { EmitLog("❌ No Login"); return; }
        if (data == null) { EmitLog("❌ SaveUserData: data null"); return; }

        try
        {
            // Yalnızca client'ın yazmasına izin verilen alanları patch olarak gönder
            var patch = new System.Collections.Generic.Dictionary<string, object>
            {
                { "mail",      data.mail ?? string.Empty },
                { "username",  data.username ?? string.Empty },
                { "currency",  data.currency },
                { "streak",    data.streak },
                { "referrals", data.referrals }
                // Do NOT write rank from client
            };

            // Güvenlik: yanlışlıkla server-only key girilirse ayıkla
            StripServerOnlyKeys(patch);

            await UserDoc().SetAsync(patch, SetOptions.MergeAll);

            EmitLog("✅ User data saved (patch)");
            EnqueueMain(() => OnUserDataSaved?.Invoke(data)); // HUD otomatik yenilensin
        }
        catch (Exception e)
        {
            EmitLog("❌ Data save error: " + e.Message);
        }
    }

    // --- Safe field updaters (client-allowed) ---
    public async Task<bool> SetUsernameAsync(string newUsername)
    {
        if (currentUser == null) { EmitLog("❌ No Login"); return false; }
        newUsername = (newUsername ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(newUsername)) { EmitLog("❌ Username empty"); return false; }

        try
        {
            var patch = new System.Collections.Generic.Dictionary<string, object> { { "username", newUsername } };
            StripServerOnlyKeys(patch);
            await UserDoc().SetAsync(patch, SetOptions.MergeAll);
            EmitLog("✅ Username updated");
            // Lokal event’i tetikle (LoadUserData ile güncel nesneyi elde etmek istersen ayrıca çağırabilirsin)
            EnqueueMain(async () =>
            {
                var fresh = await LoadUserData();
                if (fresh != null) OnUserDataSaved?.Invoke(fresh);
            });
            return true;
        }
        catch (Exception e)
        {
            EmitLog("❌ SetUsernameAsync error: " + e.Message);
            return false;
        }
    }

    public async Task<bool> SetCurrencyAsync(float value)
    {
        if (currentUser == null) { EmitLog("❌ No Login"); return false; }
        try
        {
            var patch = new System.Collections.Generic.Dictionary<string, object> { { "currency", value } };
            StripServerOnlyKeys(patch);
            await UserDoc().SetAsync(patch, SetOptions.MergeAll);
            EmitLog("✅ Currency updated");
            EnqueueMain(async () =>
            {
                var fresh = await LoadUserData();
                if (fresh != null) OnUserDataSaved?.Invoke(fresh);
            });
            return true;
        }
        catch (Exception e)
        {
            EmitLog("❌ SetCurrencyAsync error: " + e.Message);
            return false;
        }
    }

    public async Task<System.Collections.Generic.List<NetworkingData.ReferredUser>>
        ListMyReferredUsersAsync(int limit = 100, bool includeEarnings = true)
    {
        var result = new System.Collections.Generic.List<NetworkingData.ReferredUser>();

        if (_funcs == null)
        {
            EmitLog("❌ Functions null");
            return result;
        }

        try
        {
            var payload = new System.Collections.Generic.Dictionary<string, object>
            {
                { "limit", limit },
                { "includeEarnings", includeEarnings }
            };

            var callable = _funcs.GetHttpsCallable("listReferredUsers");
            var resp = await callable.CallAsync(payload);

            if (resp?.Data is System.Collections.IDictionary root &&
                root.Contains("items") &&
                root["items"] is System.Collections.IList arr)
            {
                foreach (var it in arr)
                {
                    if (it is System.Collections.IDictionary d)
                    {
                        string uid = d.Contains("uid") ? d["uid"] as string : "";
                        string username = d.Contains("username") ? d["username"] as string : "Guest";

                        float currency = 0f;
                        if (d.Contains("currency"))
                        {
                            var _c = d["currency"];
                            if (_c is double dd) currency = (float)dd;
                            else if (_c is long ll) currency = ll;
                            else if (_c is int ii) currency = ii;
                            else if (_c is float ff) currency = ff;
                            else if (_c is string ss && float.TryParse(ss, out var parsed)) currency = parsed;
                        }

                        float earned = 0f;
                        if (d.Contains("earnedTotal"))
                        {
                            var _e = d["earnedTotal"];
                            if (_e is double d2) earned = (float)d2;
                            else if (_e is long l2) earned = l2;
                            else if (_e is int i2) earned = i2;
                            else if (_e is float f2) earned = f2;
                            else if (_e is string s2 && float.TryParse(s2, out var p2)) earned = p2;
                        }

                        string createdAtIso = d.Contains("createdAt") ? d["createdAt"] as string : null;
                        string referralAppliedIso = d.Contains("referralAppliedAt") ? d["referralAppliedAt"] as string : null;

                        result.Add(new NetworkingData.ReferredUser
                        {
                            uid = uid,
                            username = string.IsNullOrWhiteSpace(username) ? "Guest" : username,
                            currency = currency,
                            earnedTotal = earned,
                            createdAtIso = createdAtIso,
                            referralAppliedAtIso = referralAppliedIso
                        });
                    }
                }
            }

            EmitLog($"✅ listReferredUsers: {result.Count} items");
        }
        catch (Exception e)
        {
            EmitLog("❌ listReferredUsers error: " + e.Message);
        }

        return result;
    }

    // ===== Leaderboard DTOs for callable response =====
    [Serializable]
    public class LBEntry
    {
        public string uid;
        public string username;
        public int score;
        public int rank; // server returns no rank; UI will use index+1
    }

    [Serializable]
    public class LeaderboardPage
    {
        public List<LBEntry> items = new();
        public bool hasMore;
        public double? nextStartAfterScore;
        public LBEntry me; // includeSelf=true ise dolar
    }

    /// <summary>
    /// Calls getLeaderboardsSnapshot callable and maps the result.
    /// Prefer this over direct Firestore queries.
    /// </summary>
    public async Task<LeaderboardPage> GetLeaderboardsSnapshotAsync(int limit = 100, double? startAfterScore = null, bool includeSelf = true)
    {
        var page = new LeaderboardPage();

        if (_funcs == null)
        {
            EmitLog("❌ Functions not initialized");
            return page;
        }

        try
        {
            var fn = _funcs.GetHttpsCallable("getLeaderboardsSnapshot");
            var payload = new Dictionary<string, object>
            {
                { "limit", Mathf.Clamp(limit, 1, 500) },
                { "includeSelf", includeSelf }
            };
            if (startAfterScore.HasValue) payload["startAfterScore"] = startAfterScore.Value;

            var res = await fn.CallAsync(payload);
            if (res?.Data is IDictionary root)
            {
                // items
                if (root["items"] is IList arr)
                {
                    foreach (var it in arr)
                    {
                        if (it is IDictionary m)
                        {
                            var e = new LBEntry
                            {
                                uid = m.TryGetString("uid"),
                                username = string.IsNullOrWhiteSpace(m.TryGetString("username")) ? "Guest" : m.TryGetString("username"),
                                score = m.TryGetInt("score"),
                                rank = 0
                            };
                            page.items.Add(e);
                        }
                    }
                }

                // hasMore
                page.hasMore = root.TryGetBool("hasMore");

                // next.startAfterScore
                if (root["next"] is IDictionary nextD)
                {
                    if (nextD.Contains("startAfterScore"))
                    {
                        var sas = nextD["startAfterScore"];
                        try { page.nextStartAfterScore = Convert.ToDouble(sas); } catch { page.nextStartAfterScore = null; }
                    }
                }

                // me
                if (includeSelf && root["me"] is IDictionary meD)
                {
                    page.me = new LBEntry
                    {
                        uid = meD.TryGetString("uid"),
                        username = string.IsNullOrWhiteSpace(meD.TryGetString("username")) ? "You" : meD.TryGetString("username"),
                        score = meD.TryGetInt("score"),
                        rank = 0
                    };
                }
            }

            EmitLog($"✅ getLeaderboardsSnapshot: {page.items.Count} items");
        }
        catch (Exception e)
        {
            EmitLog("❌ getLeaderboardsSnapshot error: " + e.Message);
        }

        return page;
    }

    // --- Helpers ---
    private DocumentReference UserDoc()
    {
        return db.Collection("users").Document(currentUser.UserId);
    }

    private void EmitLog(string msg)
    {
        Debug.Log(msg);
        EnqueueMain(() => OnLog?.Invoke(msg)); // UI güvenli
    }

    public async Task<float> GetCurrencyAsync()
    {
        if (currentUser == null) { EmitLog("❌ No Login"); return 0f; }

        try
        {
            var snap = await UserDoc().GetSnapshotAsync();
            if (!snap.Exists)
            {
                EmitLog("⚠️ User doc not found");
                return 0f;
            }

            var data = snap.ConvertTo<UserData>();
            if (data != null) return (float)data.currency;

            // İhtiyaten sözlükten okuma (eski kayıtlar int/double olabilir)
            var dict = snap.ToDictionary();
            if (dict != null && dict.TryGetValue("currency", out var v))
            {
                if (v is float f) return f;
                if (v is double d) return (float)d;
                if (v is long l) return (float)l;
                if (v is int i) return i;
                if (v is string s && float.TryParse(s,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
            }

            EmitLog("ℹ️ 'currency' not set, returning 0");
            return 0f;
        }
        catch (Exception e)
        {
            EmitLog("❌ GetCurrency error: " + e.Message);
            return 0f;
        }
    }
    public async Task<System.Collections.Generic.List<LBEntry>> FetchLeaderboardTopAsync(int topN = 50)
    {
        var list = new System.Collections.Generic.List<LBEntry>();
        try
        {
            var docRef = db.Collection("leaderboards").Document("current")
                        .Collection("meta").Document($"top{topN}");

            var snap = await docRef.GetSnapshotAsync();
            if (!snap.Exists) return list;

            if (snap.TryGetValue("entries", out System.Collections.Generic.List<object> raw))
            {
                foreach (var o in raw)
                {
                    if (o is System.Collections.Generic.Dictionary<string, object> d)
                    {
                        var uid = d.TryGetValue("uid", out var _uid) ? _uid as string : "";
                        var username = d.TryGetValue("username", out var _un) ? _un as string : "Guest";
                        var scoreObj = d.TryGetValue("score", out var _sc) ? _sc : 0;
                        int score = System.Convert.ToInt32(scoreObj);

                        int rank = 0;
                        if (d.TryGetValue("rank", out var _rk))
                        {
                            if (_rk is int ir) rank = ir;
                            else if (_rk is long lr) rank = (int)lr;
                            else if (_rk is double dr) rank = (int)dr;
                        }

                        if (!string.IsNullOrEmpty(uid))
                            list.Add(new LBEntry { uid = uid, username = username ?? "Guest", score = score, rank = rank });
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            EmitLog("❌ FetchLeaderboardTopAsync error: " + e.Message);
        }
        return list;
    }

    /// <summary>
    /// Fetches the entire leaderboard by scanning users ordered by rank asc.
    /// Requires server to maintain users/{uid}.rank for all players.
    /// Skips users without a valid (>=1) rank.
    /// </summary>
    public async Task<List<LBEntry>> FetchLeaderboardAllAsync(int pageSize = 500, int hardLimit = 100000)
    {
        var result = new List<LBEntry>();
        try
        {
            int fetched = 0;
            int? lastRank = null;
            while (fetched < hardLimit)
            {
                Query q = db.Collection("users").OrderBy("rank").Limit(pageSize);
                if (lastRank.HasValue)
                {
                    q = q.StartAfter(lastRank.Value);
                }

                var snap = await q.GetSnapshotAsync();
                if (snap == null || snap.Count == 0) break;

                foreach (var doc in snap.Documents)
                {
                    // read rank first; skip missing/invalid
                    int rank = 0;
                    if (doc.TryGetValue("rank", out object rObj))
                    {
                        if (rObj is int ri) rank = ri;
                        else if (rObj is long rl) rank = (int)rl;
                        else if (rObj is double rd) rank = (int)rd;
                    }
                    if (rank <= 0) continue; // only ranked users

                    string uid = doc.Id;
                    string username = "Guest";
                    if (doc.TryGetValue("username", out string un) && !string.IsNullOrWhiteSpace(un))
                        username = un;
                    else if (doc.TryGetValue("username", out object unAny) && unAny is string un2 && !string.IsNullOrWhiteSpace(un2))
                        username = un2;

                    // prefer maxScore; fall back to score
                    int score = 0;
                    if (doc.TryGetValue("maxScore", out object sc))
                    {
                        if (sc is int i) score = i;
                        else if (sc is long l) score = (int)l;
                        else if (sc is double d) score = (int)d;
                    }
                    else if (doc.TryGetValue("score", out object sc2))
                    {
                        if (sc2 is int i2) score = i2;
                        else if (sc2 is long l2) score = (int)l2;
                        else if (sc2 is double d2) score = (int)d2;
                    }

                    result.Add(new LBEntry
                    {
                        uid = uid,
                        username = string.IsNullOrWhiteSpace(username) ? "Guest" : username,
                        score = score,
                        rank = rank
                    });

                    lastRank = rank; // pagination anchor
                    fetched++;
                    if (fetched >= hardLimit) break;
                }

                if (snap.Count < pageSize) break; // reached end
            }

            EmitLog($"✅ FetchLeaderboardAllAsync: {result.Count} items");
        }
        catch (System.Exception e)
        {
            EmitLog("❌ FetchLeaderboardAllAsync error: " + e.Message);
        }

        return result;
    }

    private async Task EnsureUserProfileAsync(string referralCode = null)
    {
        if (_funcs == null) return;
        try
        {
            var callable = _funcs.GetHttpsCallable("ensureUserProfile");
            var payload = new System.Collections.Generic.Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(referralCode))
                payload["referralCode"] = referralCode;

            await callable.CallAsync(payload);
            EmitLog("✅ ensureUserProfile ok");
        }
        catch (Exception e)
        {
            EmitLog("⚠️ ensureUserProfile error: " + e.Message);
        }
    }

    public async Task<float> FetchMyReferralEarningsAsync()
    {
        if (currentUser == null) { EmitLog("❌ No Login"); return 0f; }
        try
        {
            var s = await UserDoc().GetSnapshotAsync();
            if (!s.Exists) return 0f;

            if (s.TryGetValue("referralEarnings", out object v))
            {
                if (v is double d) return (float)d;
                if (v is long l) return (float)l;
                if (v is int i) return i;
                if (v is float f) return f;
                if (v is string s2 && float.TryParse(s2, out var p)) return p;
            }
        }
        catch (Exception e) { EmitLog("❌ FetchMyReferralEarningsAsync error: " + e.Message); }
        return 0f;
    }

    public async Task<string> GetReferralKeyAsync()
    {
        if (currentUser == null) return "-";
        try
        {
            var s = await UserDoc().GetSnapshotAsync();
            if (!s.Exists) return "-";

            if (s.TryGetValue("referralKey", out string key) && !string.IsNullOrEmpty(key)) return key;
            if (s.TryGetValue("referralKey", out object v) && v is string k && !string.IsNullOrEmpty(k)) return k;
        }
        catch (Exception e) { EmitLog("❌ GetReferralKeyAsync error: " + e.Message); }
        return "-";
    }

    // Örnek: users/{userId}/profile/stats  veya users/{userId} içinde "statsJson" alanı gibi
    public async Task<string> FetchPlayerStatsJsonAsync(string userId)
    {
        if (db == null)
        {
            EmitLog("❌ Firestore db is null");
            return string.Empty;
        }
        if (string.IsNullOrWhiteSpace(userId))
        {
            EmitLog("❌ FetchPlayerStatsJsonAsync: userId empty");
            return string.Empty;
        }
        try
        {
            var docRef = db.Collection("users").Document(userId);
            var snap = await docRef.GetSnapshotAsync();
            if (!snap.Exists)
            {
                EmitLog($"⚠️ user doc not found: {userId}");
                return string.Empty;
            }

            // Field stored as plain string JSON (see console screenshot)
            if (snap.TryGetValue("statsJson", out string json) && !string.IsNullOrWhiteSpace(json))
                return json;

            // Defensive: handle heterogeneous types if needed
            if (snap.TryGetValue("statsJson", out object any) && any is string s && !string.IsNullOrWhiteSpace(s))
                return s;

            EmitLog("ℹ️ statsJson empty for user");
            return string.Empty;
        }
        catch (Exception e)
        {
            EmitLog("❌ FetchPlayerStatsJsonAsync error: " + e.Message);
            return string.Empty;
        }
    }
    /// <summary>
    /// Fetches the current user's referral count from Firestore.
    /// Returns 0 if not logged in, on error, or if the field is missing.
    /// </summary>
    public async Task<int> GetReferralCountAsync()
    {
        if (currentUser == null)
        {
            EmitLog("❌ GetReferralCountAsync: No Login");
            return 0;
        }
        try
        {
            var snap = await UserDoc().GetSnapshotAsync();
            if (!snap.Exists)
            {
                EmitLog("⚠️ GetReferralCountAsync: user doc not found");
                return 0;
            }

            if (snap.TryGetValue("referrals", out object val))
            {
                // Try to convert to int
                if (val is int i) return i;
                if (val is long l) return (int)l;
                if (val is double d) return (int)d;
                if (val is float f) return (int)f;
                if (val is string s && int.TryParse(s, out var parsed)) return parsed;
                EmitLog($"ℹ️ GetReferralCountAsync: 'referrals' field type unexpected ({val?.GetType()})");
            }
            else
            {
                EmitLog("ℹ️ GetReferralCountAsync: 'referrals' field not set, returning 0");
            }
        }
        catch (Exception e)
        {
            EmitLog("❌ GetReferralCountAsync error: " + e.Message);
        }
        return 0;
    }
}

// ===== Helpers to read IDictionary safely (top-level extension class) =====
public static class DictReadHelpers
{
    public static string TryGetString(this IDictionary d, string key, string def = "")
    {
        if (d != null && d.Contains(key) && d[key] != null) return d[key].ToString();
        return def;
    }
    public static int TryGetInt(this IDictionary d, string key, int def = 0)
    {
        if (d != null && d.Contains(key) && d[key] != null)
        {
            var v = d[key];
            if (v is int i) return i;
            if (v is long l) return (int)l;
            if (v is double db) return (int)db;
            if (int.TryParse(v.ToString(), out var p)) return p;
        }
        return def;
    }
    public static bool TryGetBool(this IDictionary d, string key, bool def = false)
    {
        if (d != null && d.Contains(key) && d[key] != null)
        {
            var v = d[key];
            if (v is bool b) return b;
            if (bool.TryParse(v.ToString(), out var p)) return p;
        }
        return def;
    }
}

