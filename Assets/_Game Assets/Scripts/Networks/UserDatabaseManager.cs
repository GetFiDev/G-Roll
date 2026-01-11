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
using UnityEngine.SocialPlatforms.Impl;
using Newtonsoft.Json;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif


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
    public bool IsFirebaseReady { get; private set; } = false;


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


    // --- Caching for Optimistic Updates ---
    private UserData _cachedUserData;
    private float _lastOptimisticUpdateTimestamp = -999f;

    private const float OPTIMISTIC_CACHE_DURATION = 15f; // Duration to prefer local cache over server stale data

    public const string PREF_KEY_REFERRAL_CODE = "MyReferralCodeCache";

    private static void StripServerOnlyKeys(System.Collections.Generic.Dictionary<string, object> dict)
    {
        if (dict == null) return;
        foreach (var k in SERVER_ONLY_KEYS)
        {
            if (dict.ContainsKey(k)) dict.Remove(k);
        }
    }

    [Sirenix.OdinInspector.ReadOnly] public string currentLoggedUserID;
    public UserData currentUserData => _cachedUserData;
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
    // Optimized Initialization Flow
    public void Reset()
    {
        if (auth != null)
        {
            try { auth.SignOut(); } catch { }
        }
        currentUser = null;
        currentLoggedUserID = null;
        
        // Critical: Clear cache so we don't serve previous user's data to new user
        _cachedUserData = null;
        _lastOptimisticUpdateTimestamp = -999f;
        
        EmitLog("✅ UserDatabaseManager Reset (Signed Out)");
    }

    public async Task<bool> InitializeFirebaseAsync()
    {
        if (IsFirebaseReady) return true;

        var depStatus = await Firebase.FirebaseApp.CheckAndFixDependenciesAsync();
        if (depStatus == Firebase.DependencyStatus.Available)
        {
            auth = FirebaseAuth.DefaultInstance;
            db = FirebaseFirestore.GetInstance(Firebase.FirebaseApp.DefaultInstance, "getfi");
            _funcs = FirebaseFunctions.GetInstance("us-central1");

            EmitLog("✅ Firebase Core Initialized");
            
            // Disable Auto-Login -> Force fresh flow every time
            if (auth.CurrentUser != null)
            {
                EmitLog("ℹ️ Auto-login disabled. Signing out exiting user...");
                auth.SignOut();
                currentUser = null;
                currentLoggedUserID = null;
            }
            
            IsFirebaseReady = true;
            return true;
        }
        else
        {
            EmitLog($"❌ Firebase Dependencies Missing: {depStatus}");
            return false;
        }
    }
    
    // Check if user is currently authenticated (synchronous check for AppFlow)
    public bool IsAuthenticated() => auth != null && auth.CurrentUser != null;

    void Start() { /* NO OPs - Controlled by AppFlowManager */ }

    // --- Profile Completion (Server-Side) ---
    public async Task<bool> CompleteProfileAsync(string username, string referralCode)
    {
        if (currentUser == null) { EmitLog("❌ No Login (CompleteProfile)"); return false; }
        if (_funcs == null) { EmitLog("❌ Functions null"); return false; }

        try
        {
            var callable = _funcs.GetHttpsCallable("completeUserProfile");
            var data = new Dictionary<string, object>
            {
                { "username", username },
                { "referralCode", referralCode }
            };

            var result = await callable.CallAsync(data);
            EmitLog("✅ Profile Completed Successfully");
            
            // Check for referral result
            if (result.Data is System.Collections.IDictionary dict)
            {
               if (dict.Contains("referralApplied") && dict["referralApplied"] is bool applied && !applied)
               {
                   string err = dict.Contains("referralError") ? dict["referralError"] as string : "Unknown";
                   if (!string.IsNullOrEmpty(referralCode))
                   {
                        EmitLog($"⚠️ Referral not applied: {err}");
                   }
               }
            }

            // Refresh local cache immediately
            await LoadUserData();
            return true;
        }
        catch (Exception e)
        {
            EmitLog("❌ CompleteProfile Failed: " + e.Message);
            return false;
        }
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
    public async Task<bool> UpdateDailyStreakAsync() // Changed to public for AppFlow if needed
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

    // --- SOCIAL LOGIN ---

    /// <summary>
    /// Authenticate with a credential obtained from a social provider (Play Games, Game Center, etc.)
    /// </summary>
    public async void LoginWithCredential(Credential credential, string photoUrlToSave = null, string emailToSave = null)
    {
        Reset();
        if (auth == null) { EmitLog("❌ Auth null"); return; }
        if (credential == null) { EmitLog("❌ Credential null"); return; }

        try
        {
            var r = await auth.SignInWithCredentialAsync(credential);
            currentUser = r;
            currentLoggedUserID = currentUser?.UserId;
            EmitLog($"✅ Social Login succeed, UID: {currentLoggedUserID}");

            // --- FAKE EMAIL LOGIC FOR GPGS ---
            if (emailToSave == "GENERATE_FROM_UID")
            {
                emailToSave = $"{currentLoggedUserID}@groll.com";
            }
            // ---------------------------------

            // --- NEW: Update Photo URL & Email if provided ---
            if (!string.IsNullOrEmpty(photoUrlToSave) || !string.IsNullOrEmpty(emailToSave))
            {
                try
                {
                    var patch = new Dictionary<string, object>();
                    if (!string.IsNullOrEmpty(photoUrlToSave)) 
                    {
                        EmitLog($"📸 Saving PhotoURL to Firestore: {photoUrlToSave}");
                        patch["photoUrl"] = photoUrlToSave;
                    }
                    if (!string.IsNullOrEmpty(emailToSave))
                    {
                        EmitLog($"📧 Saving Email to Firestore: {emailToSave}");
                        patch["email"] = emailToSave;
                        patch["mail"] = emailToSave; // Duplicate for safety as seen in Cloud Functions
                    }

                    // Direct Firestore update to avoid overwriting other fields
                    await UserDoc().SetAsync(patch, SetOptions.MergeAll);
                }
                catch (Exception ex)
                {
                    EmitLog($"⚠️ Failed to save Profile Info: {ex.Message}");
                }
            }
            // -----------------------------------------

            // Daily streak: update once on sign-in
            await UpdateDailyStreakAsync();

            // --- SYNC EMAIL (Fix for missing emails via GPGS) ---
            try
            {
                var syncFunc = FirebaseFunctions.DefaultInstance.GetHttpsCallable("syncUserEmail");
                _ = syncFunc.CallAsync(); // Fire and forget to not block UI
                EmitLog("📧 Requested Email Sync...");
            }
            catch (Exception exSync)
            {
                EmitLog($"⚠️ Email Sync Failed: {exSync.Message}");
            }
            // ----------------------------------------------------

            // En güncel user data
            var data = await LoadUserData();

            EnqueueMain(() =>
            {
                OnLoginSucceeded?.Invoke();
                if (data != null) OnUserDataSaved?.Invoke(data);
                // For social login, "Register" and "Login" are often the same flow from user perspective.
                // If it's a new user, they might just have empty data.
            });
        }
        catch (Exception e)
        {
            EmitLog("❌ Social Login error: " + e.Message);
            EnqueueMain(() => OnLoginFailed?.Invoke(e.Message));
        }
    }

    /// <summary>
    /// Mock login for Editor testing (uses Email/Pass auth).
    /// </summary>
    public async void LoginWithEditorMock()
    {
        Reset();
        if (auth == null) { EmitLog("❌ Auth null"); return; }
        EmitLog("Starting Editor Mock Login (Email: test@gmail.com)...");

        try
        {
            // Make sure you created this user in Firebase Console -> Auth -> Email/Password
            var r = await auth.SignInWithEmailAndPasswordAsync("testuser@gmail.com", "123456");
            currentUser = r.User;
            currentLoggedUserID = currentUser?.UserId;
            EmitLog($"✅ Mock Login succeed, UID: {currentLoggedUserID}");

            await UpdateDailyStreakAsync();
            var data = await LoadUserData();
            
            EnqueueMain(() =>
            {
                OnLoginSucceeded?.Invoke();
                if (data != null) OnUserDataSaved?.Invoke(data);
            });
        }
        catch (Exception e)
        {
            EmitLog("❌ Mock Login error: " + e.Message);
            EnqueueMain(() => OnLoginFailed?.Invoke(e.Message));
        }
    }

#if UNITY_ANDROID
    public void LoginWithGooglePlayGames()
    {
        if (Application.isEditor)
        {
            LoginWithEditorMock();
            return;
        }

        EmitLog("Starting Google Play Games Login...");
        
        // Ensure PlayGamesPlatform is activated
        if (PlayGamesPlatform.Instance == null)
        {
            PlayGamesPlatform.DebugLogEnabled = true;
            PlayGamesPlatform.Activate();
        }

        // Use ManuallyAuthenticate to FORCE the UI to show up if silent signapp failed
        PlayGamesPlatform.Instance.ManuallyAuthenticate((status) =>
        {
            if (status == SignInStatus.Success)
            {
                EmitLog("✅ Play Games Authenticated (Manual). Requesting Server Auth Code...");

                // --- NEW: Fetch Profile Image URL ---
                string photoUrl = string.Empty;
                try
                {
                    // Attempt to get the high-res image URL
                    photoUrl = PlayGamesPlatform.Instance.GetUserImageUrl(); 
                    EmitLog($"📸 GPGS Avatar URL found: {photoUrl}");
                }
                catch (Exception e)
                {
                    EmitLog($"⚠️ Could not fetch GPGS Avatar URL: {e.Message}");
                }
                // ------------------------------------

                // REQUEST EMAIL SCOPE via Server Access
                // This is the correct way for modern GPGS plugins to ask for email
                var scopes = new List<AuthScope> { AuthScope.EMAIL };
                PlayGamesPlatform.Instance.RequestServerSideAccess(true, scopes, (authResponse) =>
                {
                    string authCode = authResponse?.GetAuthCode();
                    if (!string.IsNullOrEmpty(authCode))
                    {
                        EmitLog("✅ Auth Code received using Scopes. Exchanging for Firebase Credential...");
                        Credential credential = PlayGamesAuthProvider.GetCredential(authCode);
                        
                        // We do not have the email string client-side, but the Token has the scope.
                        // Firebase Function createUserProfile will extract it.
                        // USER REQUEST: Fallback to {uid}@groll.com if GPGS.
                        LoginWithCredential(credential, photoUrl, "GENERATE_FROM_UID"); 
                    }
                    else
                    {
                        EmitLog("❌ Failed to get Server Auth Code. (Code is null/empty)");
                        EnqueueMain(() => OnLoginFailed?.Invoke("Failed to get Google Auth Code"));
                    }
                });
            }
            else
            {
                string errorMsg = $"❌ Play Games Manual Auth Failed. Status: {status}";
                EmitLog(errorMsg);
                Debug.LogError($"[GPGS] {errorMsg}"); 
                EnqueueMain(() => OnLoginFailed?.Invoke(errorMsg));
            }
        });
    }
#endif

#if UNITY_IOS
    public void LoginWithGameCenter()
    {
        if (Application.isEditor)
        {
            LoginWithEditorMock();
            return;
        }

        EmitLog("Starting Game Center Login...");
        
        Social.localUser.Authenticate((bool success) =>
        {
            if (success)
            {
                EmitLog("✅ Game Center Authenticated. Generating Firebase Credential...");
                
                // --- Capture Game Center User Info ---
                string photoUrl = string.Empty;
                string displayName = string.Empty;
                
                try
                {
                    // Social.localUser provides basic info from Game Center
                    var localUser = Social.localUser;
                    displayName = localUser.userName; // Game Center alias/display name
                    EmitLog($"📱 Game Center User: {displayName} (ID: {localUser.id})");
                    
                    // Note: Game Center does NOT provide email - never has, never will.
                    // Photo URL is also not directly available via Unity's Social API.
                    // We would need native iOS code to get the player photo.
                }
                catch (System.Exception e)
                {
                    EmitLog($"⚠️ Could not fetch Game Center user info: {e.Message}");
                }
                // ----------------------------------------
                 
                GameCenterAuthProvider.GetCredentialAsync().ContinueWith(task => {
                    if (task.IsCanceled)
                    {
                        EmitLog("❌ GameCenter GetCredentialAsync was canceled.");
                        EnqueueMain(() => OnLoginFailed?.Invoke("Game Center Canceled"));
                        return;
                    }
                    if (task.IsFaulted)
                    {
                        EmitLog("❌ GameCenter GetCredentialAsync encountered an error: " + task.Exception);
                        EnqueueMain(() => OnLoginFailed?.Invoke("Game Center Error"));
                        return;
                    }

                    Credential credential = task.Result;
                    EmitLog("✅ Game Center Credential created. Signing in to Firebase...");
                    
                    // Game Center does NOT provide email, so we use the same fallback as GPGS:
                    // The UID-based fake email will be generated in LoginWithCredential
                    EnqueueMain(() => LoginWithCredential(credential, photoUrl, "GENERATE_FROM_UID"));
                });
            }
            else
            {
                EmitLog("❌ Game Center Authentication Failed.");
                EnqueueMain(() => OnLoginFailed?.Invoke("Game Center Auth Failed"));
            }
        });
    }
#endif


    // --- LOAD ---
    public async Task<UserData> LoadUserData()
    {
        if (currentUser == null) { EmitLog("❌ No Login"); return null; }

        // --- Fast Path: If we just updated optimistically, return cache immediately ---
        // This makes the UI instant after Game Loop -> Menu transition
        if (_cachedUserData != null && (Time.time - _lastOptimisticUpdateTimestamp) < 5f)
        {
            EmitLog("⚡ LoadUserData: Returning fresh optimistic cache (skipping fetch).");
            return _cachedUserData;
        }

        try
        {
            var snap = await UserDoc().GetSnapshotAsync();
            if (!snap.Exists)
            {
                EmitLog("⚠️ No user doc found");
                return null;
            }

            var data = snap.ConvertTo<UserData>(); // Eksikler property defaultlarına düşer
            
            // --- Optimistic Merge Strategy ---
            if (_cachedUserData != null && (Time.time - _lastOptimisticUpdateTimestamp) < OPTIMISTIC_CACHE_DURATION)
            {
                // If local cache has MORE currency than server, assume server is stale
                if (_cachedUserData.currency > data.currency)
                {
                    EmitLog($"⚠️ Optimistic Merge: Override server currency ({data.currency}) with local ({_cachedUserData.currency})");
                    data.currency = _cachedUserData.currency;
                    data.maxScore = Mathf.Max((float)data.maxScore, (float)_cachedUserData.maxScore);
                }
            }
            
            // Update cache with the (potentially merged) fresh data
            _cachedUserData = data; 
            
            // --- NEW: Cache Referral Key immediately ---
            if (!string.IsNullOrEmpty(data.referralKey) && data.referralKey != "-")
            {
                PlayerPrefs.SetString(PREF_KEY_REFERRAL_CODE, data.referralKey);
                PlayerPrefs.Save();
            }

            EmitLog("✅ User data loaded");
            return data;
        }
        catch (Exception e)
        {
            EmitLog("❌ Data load error: " + e.Message);
            return null;
        }
    }

    public async Task RefreshUserData()
    {
        var data = await LoadUserData();
        if (data != null)
        {
            EnqueueMain(() => OnUserDataSaved?.Invoke(data));
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
                { "referralCount", data.referrals }
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
        public string photoUrl;
        public int score;
        public bool hasElitePass;
        public int rank; // server returns no rank; UI will use index+1
    }

    [Serializable]
    public class SeasonConfig
    {
        public string id;
        public string name;
        public string description;
        public bool isActive;
        public DateTime startDate;
        public DateTime endDate;
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
    public async Task<LeaderboardPage> GetLeaderboardsSnapshotAsync(string leaderboardId = "all_time", int limit = 100, double? startAfterScore = null, bool includeSelf = true)
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
                { "leaderboardId", leaderboardId },
                { "limit", Mathf.Clamp(limit, 1, 100) },
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
                                photoUrl = m.TryGetString("photoUrl"),
                                hasElitePass = m.TryGetBool("isElite"),
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
                        photoUrl = meD.TryGetString("photoUrl"),
                        hasElitePass = meD.TryGetBool("isElite"),
                        rank = meD.TryGetInt("rank")
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

    public async Task<List<SeasonConfig>> GetSeasonsAsync()
    {
        var list = new List<SeasonConfig>();
        if (db == null) return list;

        try
        {
            var snap = await db.Collection("seasons").GetSnapshotAsync();
            foreach (var doc in snap.Documents)
            {
                var dict = doc.ToDictionary();
                var s = new SeasonConfig { id = doc.Id };
                
                s.name = dict.TryGetValue("name", out var n) ? n as string : doc.Id;
                s.description = dict.TryGetValue("description", out var d) ? d as string : "";
                s.isActive = dict.TryGetValue("isActive", out var a) && (bool)a;

                if (dict.TryGetValue("startDate", out var sd) && sd is Timestamp tsStart)
                    s.startDate = tsStart.ToDateTime();
                
                if (dict.TryGetValue("endDate", out var ed) && ed is Timestamp tsEnd)
                    s.endDate = tsEnd.ToDateTime();

                list.Add(s);
            }
            EmitLog($"✅ Fetched {list.Count} seasons.");
        }
        catch (Exception e)
        {
            EmitLog($"❌ GetSeasonsAsync failed: {e.Message}");
        }
        return list;
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

    // REMOVED EnsureUserProfileAsync - method deleted.

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
                EmitLog($"⚠️ GetReferralCountAsync: user doc not found for {currentUser.UserId}");
                return 0;
            }

            if (snap.TryGetValue("referralCount", out object val))
            {
                if (val is int i) return i;
                if (val is long l) return (int)l;
                if (val is double d) return (int)d;
                if (val is float f) return (int)f;
                if (val is string s && int.TryParse(s, out var parsed)) return parsed;
                EmitLog($"ℹ️ GetReferralCountAsync: 'referralCount' field type unexpected ({val?.GetType()})");
            }
            return 0;
        }
        catch (Exception e)
        {
            EmitLog("❌ GetReferralCountAsync error: " + e.Message);
            return 0;
        }
    }

    // --- New Referral Methods (v2.0) ---

    public async Task<(bool hasPending, float total, List<(string name, float amount)> items)> GetPendingReferralsAsync()
    {
        if (_funcs == null || currentUser == null) return (false, 0f, null);
        
        try
        {
            var callable = _funcs.GetHttpsCallable("getPendingReferrals");
            var result = await callable.CallAsync();
            
            if (result.Data is IDictionary d)
            {
                bool has = d.TryGetBool("hasPending");
                float total = (float)d.TryGetInt("total"); // TryGetInt handles double/long too via extension helper if robust enough, checking helper...
                // Helper TryGetInt returns int. Total might be float. Let's act carefully.
                // Re-reading 'total' manually to be safe for floats.
                if (d.Contains("total"))
                {
                    var tObj = d["total"];
                    total = Convert.ToSingle(tObj);
                }

                var items = new List<(string, float)>();
                if (d["items"] is IList arr)
                {
                    foreach (var i in arr)
                    {
                        if (i is IDictionary itemDict)
                        {
                            string n = itemDict.TryGetString("childName");
                            float a = 0f;
                            if (itemDict.Contains("amount")) a = Convert.ToSingle(itemDict["amount"]);
                            items.Add((n, a));
                        }
                    }
                }
                return (has, total, items);
            }
        }
        catch (Exception e)
        {
            EmitLog("❌ GetPendingReferralsAsync error: " + e.Message);
        }
        return (false, 0f, null);
    }

    public async Task<float> ClaimReferralEarningsAsync()
    {
        if (_funcs == null || currentUser == null) return 0f;

        try
        {
            var callable = _funcs.GetHttpsCallable("claimReferralEarnings");
            var result = await callable.CallAsync();
            
            if (result.Data is IDictionary d)
            {
                if (d.Contains("claimed"))
                {
                    float claimed = Convert.ToSingle(d["claimed"]);
                    EmitLog($"✅ Claimed Referral Earnings: {claimed}");
                    
                    // Trigger refresh of user data to show new wallet balance
                    await LoadUserData();
                    
                    return claimed;
                }
            }
        }
        catch (Exception e)
        {
            EmitLog("❌ ClaimReferralEarningsAsync error: " + e.Message);
        }
        return 0f;
    }

    // --- CHAPTER MAP FETCHING (via Cloud Function) ---
    
    /// <summary>
    /// Fetch chapter map using getChapterMap Cloud Function.
    /// Pass 0 or negative to use user's chapterProgress from server.
    /// Returns null if chapter doesn't exist (all chapters completed).
    /// </summary>
    public async Task<(string json, int chapterOrder, string mapDisplayName)> FetchChapterMapAsync(int chapterOrder = 0)
    {
        if (_funcs == null)
        {
            EmitLog("❌ FetchChapterMapAsync: Functions not initialized");
            return (null, 0, null);
        }

        try
        {
            var callable = _funcs.GetHttpsCallable("getChapterMap");
            var payload = new Dictionary<string, object>();
            if (chapterOrder > 0)
            {
                payload["chapterOrder"] = chapterOrder;
            }

            var result = await callable.CallAsync(payload);
            if (result?.Data is IDictionary d)
            {
                bool ok = d.TryGetBool("ok");
                if (!ok)
                {
                    string reason = d.TryGetString("reason");
                    if (reason == "no_more_chapters")
                    {
                        EmitLog($"ℹ️ All chapters completed! (requested: {d.TryGetInt("chapterOrder")})");
                        return (null, d.TryGetInt("chapterOrder"), null);
                    }
                    EmitLog($"⚠️ getChapterMap returned ok=false: {d.TryGetString("message")}");
                    return (null, 0, null);
                }

                string json = d.TryGetString("json");
                int order = d.TryGetInt("chapterOrder");
                string displayName = d.TryGetString("mapDisplayName");
                
                EmitLog($"✅ FetchChapterMapAsync: Chapter {order} loaded");
                return (json, order, displayName);
            }
        }
        catch (Exception e)
        {
            EmitLog($"❌ FetchChapterMapAsync failed: {e.Message}");
        }
        return (null, 0, null);
    }

    /// <summary>
    /// Legacy wrapper for backwards compatibility. Uses Cloud Function.
    /// </summary>
    public async Task<string> FetchChapterMapJsonAsync(int chapterIndex)
    {
        var (json, _, _) = await FetchChapterMapAsync(chapterIndex);
        return json;
    }

    public async Task<bool> CheckIfChapterExists(int chapterIndex)
    {
        var (json, _, _) = await FetchChapterMapAsync(chapterIndex);
        return !string.IsNullOrEmpty(json);
    }

    // --- GAMEPLAY SESSION SUBMISSION (Persistent) ---
    public async Task SubmitGameplaySessionAsync(string sessionId, double earnedCurrency, double earnedScore, double maxCombo, int playtimeSec, int powerUpsCollected, int usedRevives, GameMode mode, bool success)
    {
        string modeStr = mode.ToString().ToLower();
        EmitLog($"[UserDatabaseManager] Submitting session: {sessionId} Cur:{earnedCurrency} Score:{earnedScore} Mode:{modeStr} Revives:{usedRevives} Suc:{success}");

        try
        {
            var resp = await SessionRemoteService.SubmitResultAsync(sessionId, earnedCurrency, earnedScore, maxCombo, playtimeSec, powerUpsCollected, usedRevives, modeStr, success);
            
            // Success! Update local cache immediately
            if (_cachedUserData != null)
            {
                // OPTIMISTIC MATH:
                // Server response 'currency' field seems unreliable (returns 0 often).
                // So we trust our local 'earnedCurrency' and add it to the cached total.
                _cachedUserData.currency += earnedCurrency;
                
                // For maxScore, we can trust the response if it's there, or just take the max
                if (resp.maxScore > _cachedUserData.maxScore)
                    _cachedUserData.maxScore = resp.maxScore;
                else if (earnedScore > _cachedUserData.maxScore) // Fallback local check
                    _cachedUserData.maxScore = (float)earnedScore;

                if (mode == GameMode.Chapter && success)
                {
                    _cachedUserData.chapterProgress++;
                    // Refund energy optimistically
                    // We don't track energy in cachedUserData strictly but let's assume UI refreshes from server or uses separate component
                }

                _lastOptimisticUpdateTimestamp = Time.time;
                EmitLog($"✅ Session Submitted! Optimistic Cache Updated: Currency={_cachedUserData.currency}");

                // Trigger UI refresh immediately
                EnqueueMain(() => OnUserDataSaved?.Invoke(_cachedUserData));
            }
            else
            {
                EmitLog("⚠️ SubmitGameplaySessionAsync: _cachedUserData is null, skipping optimistic update.");
            }
        }
        catch (Exception e)
        {
            EmitLog($"❌ SubmitGameplaySessionAsync Failed: {e.Message}");
        }
    }

    // --- AD PRODUCT MANAGEMENT ---

    [Serializable]
    public class AdClaimData
    {
        public string lastClaimDate; // "yyyy-MM-dd"
        public int count;
    }

    public int GetDailyAdClaimCount(string productId)
    {
        if (currentUser == null || _cachedUserData == null) return 0;
        
        try
        {
            var dict = ParseAdClaims();
            if (dict.TryGetValue(productId, out var data))
            {
                // Date Check
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                if (data.lastClaimDate == today)
                {
                    return data.count;
                }
            }
        }
        catch (Exception e)
        {
            EmitLog($"⚠️ GetDailyAdClaimCount exception: {e.Message}");
        }
        return 0;
    }

    public bool CanClaimAdProduct(string productId, int dailyLimit)
    {
        int used = GetDailyAdClaimCount(productId);
        return used < dailyLimit;
    }

    public async Task<bool> RecordAdClaimAsync(string productId, float rewardCurrency = 0)
    {
        if (currentUser == null) return false;

        try
        {
            var dict = ParseAdClaims();
            
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (!dict.ContainsKey(productId))
            {
                dict[productId] = new AdClaimData { lastClaimDate = today, count = 0 };
            }

            var entry = dict[productId];
            // Reset if new day
            if (entry.lastClaimDate != today)
            {
                entry.lastClaimDate = today;
                entry.count = 0;
            }

            entry.count++;
            
            // Serialize
            string json = JsonConvert.SerializeObject(dict);
            
            // Update UserData
            var patch = new Dictionary<string, object>
            {
                { "adClaimsJson", json }
            };

            // If there's a reward, add it to currency securely via cloud function or patch if permissible.
            // Since we trust client for ad view verification in this scope (or assume server verification is hard),
            // we will pessimistically optimistic update? Or just update currency patch.
            // NOTE: 'currency' is allowed in SaveUserData strip list, so we can patch it.
            if (rewardCurrency > 0)
            {
                // We need to fetch latest currency or use cached?
                // Let's use Firestore increment for atomicity if possible.
                // But SaveUserData does a SetAsync with Merge.
                // We can just add to our local optimistic cache, and send the new total.
                
                // Better: Use FieldValue.Increment
                patch["currency"] = FieldValue.Increment(rewardCurrency);
            }

            StripServerOnlyKeys(patch);
            await UserDoc().SetAsync(patch, SetOptions.MergeAll);

            // Update Local Cache
            if (_cachedUserData != null)
            {
                _cachedUserData.adClaimsJson = json;
                if (rewardCurrency > 0) _cachedUserData.currency += rewardCurrency;
                OnUserDataSaved?.Invoke(_cachedUserData);
            }

            EmitLog($"✅ Ad Claim Recorded: {productId} (Count: {entry.count})");
            return true;
        }
        catch (Exception e)
        {
            EmitLog($"❌ RecordAdClaimAsync error: {e.Message}");
            return false;
        }
    }

    private Dictionary<string, AdClaimData> ParseAdClaims()
    {
        var dict = new Dictionary<string, AdClaimData>();
        if (_cachedUserData == null || string.IsNullOrEmpty(_cachedUserData.adClaimsJson)) return dict;

        try
        {
            dict = JsonConvert.DeserializeObject<Dictionary<string, AdClaimData>>(_cachedUserData.adClaimsJson) 
                   ?? new Dictionary<string, AdClaimData>();
        }
        catch
        {
            // Ignore parse errors, return empty
        }
        return dict;
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
