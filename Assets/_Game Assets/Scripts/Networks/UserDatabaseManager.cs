using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using Firebase.Functions;
using UnityEngine;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using NetworkingData;

public class UserDatabaseManager : MonoBehaviour
{
    // --- Log & Durum Event'leri ---
    public event Action<string> OnLog;
    public event Action OnRegisterSucceeded;
    public event Action<string> OnRegisterFailed;
    public event Action OnLoginSucceeded;
    public event Action<string> OnLoginFailed;
    public event Action<UserData> OnUserDataSaved;


    // Main-thread güvenli dispatch için
    private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
    private void EnqueueMain(Action a) { if (a != null) _mainThreadQueue.Enqueue(a); }
    private void Update() { while (_mainThreadQueue.TryDequeue(out var a)) a?.Invoke(); }

    // --- Firebase alanlar ---
    private FirebaseAuth auth;
    private FirebaseUser currentUser;
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
                { "referrals", data.referrals },
                { "rank",      data.rank }
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
            if (data != null) return data.currency;

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

                        if (!string.IsNullOrEmpty(uid))
                            list.Add(new LBEntry { uid = uid, username = username ?? "Guest", score = score });
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

}
