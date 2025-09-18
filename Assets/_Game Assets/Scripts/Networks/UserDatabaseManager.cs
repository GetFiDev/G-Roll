using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

// --- Kullanıcı verisi (tek konteyner) ---
[FirestoreData]
public class UserData
{
    [FirestoreProperty] public string    mail       { get; set; } = "";
    [FirestoreProperty] public string    username   { get; set; } = "";
    [FirestoreProperty] public long      currency   { get; set; } = 0;
    [FirestoreProperty] public Timestamp lastLogin  { get; set; } = Timestamp.GetCurrentTimestamp();

    // Yarın ekleyeceğin alanlar sadece buraya property olarak gelsin:
    // [FirestoreProperty] public bool hasElitePass { get; set; } = false;
}

public class UserDatabaseManager : MonoBehaviour
{
    // --- Basit Log Event ---
    public event Action<string> OnLog;

    // Main-thread güvenli event dispatch için kuyruk
    private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
    private void EnqueueMain(Action a) { if (a != null) _mainThreadQueue.Enqueue(a); }
    private void Update() { while (_mainThreadQueue.TryDequeue(out var a)) a?.Invoke(); }

    // --- Firebase alanlar ---
    private FirebaseAuth auth;
    private FirebaseUser currentUser;
    private FirebaseFirestore db;

    void Start() => InitializeFirebase();

    private void InitializeFirebase()
    {
        Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == Firebase.DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                db = FirebaseFirestore.GetInstance(Firebase.FirebaseApp.DefaultInstance, "getfi");
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
            EmitLog($"✅ Register Successful, UID: {currentUser.UserId}");

            var data = new UserData
            {
                mail      = currentUser.Email ?? "",
                username  = "",
                currency  = 0,
                lastLogin = Timestamp.GetCurrentTimestamp()
            };

            await UserDoc().SetAsync(data); // tam doküman yaz
            EmitLog("✅ Created user doc with defaults");
        }
        catch (Exception e)
        {
            EmitLog("❌ Register error: " + e.Message);
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
            EmitLog($"✅ Login succeed, UID: {currentUser.UserId}");

            var doc = UserDoc();
            var snap = await doc.GetSnapshotAsync();

            if (!snap.Exists)
            {
                var data = new UserData
                {
                    mail      = currentUser.Email ?? "",
                    username  = "",
                    currency  = 0,
                    lastLogin = Timestamp.GetCurrentTimestamp()
                };
                await doc.SetAsync(data);
                EmitLog("ℹ️ User doc created after login (didn't exist)");
            }
            else
            {
                await doc.SetAsync(new { lastLogin = Timestamp.GetCurrentTimestamp() }, SetOptions.MergeAll);
                EmitLog("✅ lastLogin updated");
            }
        }
        catch (Exception e)
        {
            EmitLog("❌ Login error: " + e.Message);
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

            var data = snap.ConvertTo<UserData>(); // Eksik alanlar property defaultlarına düşer
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
            else       await UserDoc().SetAsync(data);

            EmitLog("✅ User data saved");
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
        EnqueueMain(() => OnLog?.Invoke(msg)); // UI güvenli: main thread’e taşır
    }
}
