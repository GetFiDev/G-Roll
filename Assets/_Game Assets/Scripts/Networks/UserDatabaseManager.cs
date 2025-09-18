using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
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
            }
            else
            {
                EmitLog($"❌ Firebase deps not available: {task.Result}");
            }
        });
    }

    // --- REGISTER ---
    public async void Register(string email, string password)
    {
        if (auth == null) { EmitLog("❌ Auth null"); return; }

        try
        {
            var r = await auth.CreateUserWithEmailAndPasswordAsync(email, password);
            currentUser = r.User;
            currentLoggedUserID = currentUser?.UserId;
            EmitLog($"✅ Register Successful, UID: {currentLoggedUserID}");

            // Default user doc
            var data = new UserData
            {
                mail = currentUser?.Email ?? "",
                username = "",
                currency = 0,
                lastLogin = Timestamp.GetCurrentTimestamp()
            };

            await UserDoc().SetAsync(data); // dokümanı oluştur
            EmitLog("✅ Created user doc with defaults");

            // Firebase CreateUser sonrası zaten login olmuş durumdasın:
            // UI'lar aynı akışı kullansın diye login succeeded event'i yayınlıyoruz.
            EnqueueMain(() =>
            {
                OnRegisterSucceeded?.Invoke();
                OnLoginSucceeded?.Invoke(); // UI kapanması için tetik
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

            var doc  = UserDoc();
            var snap = await doc.GetSnapshotAsync();

            NetworkingData.UserData userData;

            if (!snap.Exists)
            {
                // İlk kez giren kullanıcı için default doküman
                var data = new NetworkingData.UserData
                {
                    mail      = currentUser?.Email ?? "",
                    username  = "",
                    currency  = 0f,
                    lastLogin = Timestamp.GetCurrentTimestamp()
                };

                await doc.SetAsync(data);
                EmitLog("ℹ️ User doc created after login (didn't exist)");
                userData = data; // az önce yazdığımız veri
            }
            else
            {
                // Sadece lastLogin'i güncelle, sonra güncel veriyi tekrar çek
                await doc.SetAsync(new { lastLogin = Timestamp.GetCurrentTimestamp() }, SetOptions.MergeAll);
                EmitLog("✅ lastLogin updated");

                var freshSnap = await doc.GetSnapshotAsync();
                userData = freshSnap.Exists ? freshSnap.ConvertTo<NetworkingData.UserData>()
                                            : new NetworkingData.UserData { mail = currentUser?.Email ?? "" };
            }

            // UI'ler login + stat refresh'i aynı frame'de alabilsin
            EnqueueMain(() =>
            {
                OnLoginSucceeded?.Invoke();
                OnUserDataSaved?.Invoke(userData); // <-- stat view'ler bu event'i dinleyip refresh eder
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

        try
        {
            if (merge) await UserDoc().SetAsync(data, SetOptions.MergeAll);
            else await UserDoc().SetAsync(data);

            EmitLog("✅ User data saved");
            EnqueueMain(() => OnUserDataSaved?.Invoke(data));   // ⬅️ username “Done” sonrası HUD otomatik yenilensin
        }
        catch (Exception e)
        {
            EmitLog("❌ Data save error: " + e.Message);
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
            if (v is float f)   return f;
            if (v is double d)  return (float)d;
            if (v is long l)    return (float)l;
            if (v is int i)     return i;
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

}
