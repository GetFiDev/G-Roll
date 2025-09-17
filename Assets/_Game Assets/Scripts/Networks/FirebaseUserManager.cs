using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;
using System;
using System.Collections.Generic;
public class FirebaseUserManager : MonoBehaviour
{
    private FirebaseAuth auth;
    private FirebaseUser currentUser;
    private FirebaseFirestore db;

    void Start()
    {
        InitializeFirebase();
    }

    private void InitializeFirebase()
    {
        Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == Firebase.DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                db = FirebaseFirestore.GetInstance(Firebase.FirebaseApp.DefaultInstance, "getfi");
                Debug.Log("✅ Firebase hazır!");
            }
            else
            {
                Debug.LogError("❌ Firebase bağımlılıkları eksik: " + task.Result);
            }
        });
    }

    // --- REGISTER ---
    public async void Register(string email, string password)
    {
        if (auth == null) { Debug.LogError("❌ Auth null — Firebase hazır değil."); return; }

        try
        {
            var authResult = await auth.CreateUserWithEmailAndPasswordAsync(email, password);
            currentUser = authResult.User;

            Debug.Log("✅ Kayıt başarılı, UID: " + currentUser.UserId);

            CreateUserDocument(currentUser);
        }
        catch (Exception e)
        {
            Debug.LogError("❌ Register hata: " + e.Message);
        }
    }

    // --- LOGIN ---
    public async void Login(string email, string password)
    {
        if (auth == null) { Debug.LogError("❌ Auth null — Firebase hazır değil."); return; }

        try
        {
            var authResult = await auth.SignInWithEmailAndPasswordAsync(email, password);
            currentUser = authResult.User;

            Debug.Log("✅ Giriş başarılı, UID: " + currentUser.UserId);
        }
        catch (Exception e)
        {
            Debug.LogError("❌ Login hata: " + e.Message);
        }
    }

    // --- FIRESTORE ---
    private void CreateUserDocument(Firebase.Auth.FirebaseUser user)
    {
        DocumentReference docRef = db.Collection("users").Document(user.UserId);
        Debug.Log("Firestore path being written: users/" + user.UserId);  // 🔥 burası önemli

        Dictionary<string, object> newUser = new Dictionary<string, object>
        {
            { "email", user.Email },
            { "score", 0 },
            { "currency", 0 },
            { "lastUpdated", Timestamp.GetCurrentTimestamp() }
        };

        docRef.SetAsync(newUser).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted) {
                Debug.LogError("Firestore write error: " + task.Exception);
            }
            else if (task.IsCompletedSuccessfully) {
                Debug.Log("✅ Firestore write completed to users/" + user.UserId);
            }
        });
    }


    public async void SaveUserData(int score, int currency)
    {
        if (currentUser == null) { Debug.LogError("❌ Kullanıcı giriş yapmamış!"); return; }

        try
        {
            DocumentReference docRef = db.Collection("users").Document(currentUser.UserId);

            Dictionary<string, object> data = new Dictionary<string, object>
            {
                { "score", score },
                { "currency", currency },
                { "lastUpdated", Timestamp.GetCurrentTimestamp() }
            };

            await docRef.UpdateAsync(data);
            Debug.Log("✅ Kullanıcı verisi güncellendi.");
        }
        catch (Exception e)
        {
            Debug.LogError("❌ Veri kaydetme hatası: " + e.Message);
        }
    }

    public async void LoadUserData()
    {
        if (currentUser == null) { Debug.LogError("❌ Kullanıcı giriş yapmamış!"); return; }

        try
        {
            DocumentReference docRef = db.Collection("users").Document(currentUser.UserId);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            if (snapshot.Exists)
            {
                var data = snapshot.ToDictionary();
                Debug.Log("✅ Kullanıcı verisi yüklendi:");
                Debug.Log("Score: " + data["score"]);
                Debug.Log("Currency: " + data["currency"]);
            }
            else
            {
                Debug.LogWarning("⚠️ Kullanıcı verisi bulunamadı!");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("❌ Veri yükleme hatası: " + e.Message);
        }
    }
}
