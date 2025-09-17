using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;

public class FirebaseDebug : MonoBehaviour
{
    void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                var app = FirebaseApp.DefaultInstance;
                var auth = FirebaseAuth.DefaultInstance;
                var db = FirebaseFirestore.DefaultInstance;

                Debug.Log("🔥 Firebase Debug Info -----------------");
                Debug.Log("Project ID: " + app.Options.ProjectId);
                Debug.Log("App Name: " + app.Name);
                Debug.Log("Database URL: " + app.Options.DatabaseUrl);
                Debug.Log("Storage Bucket: " + app.Options.StorageBucket);

                // Firestore test path
                var docRef = db.Collection("users").Document("DEBUG_TEST");
                Debug.Log("Firestore Path: " + docRef.Path);

                // Test veri yaz
                docRef.SetAsync(new { test = "hello", time = Timestamp.GetCurrentTimestamp() })
                    .ContinueWithOnMainThread(writeTask =>
                    {
                        if (writeTask.IsCompletedSuccessfully)
                        {
                            Debug.Log("✅ Test dokümanı Firestore’a yazıldı: " + docRef.Path);
                        }
                        else
                        {
                            Debug.LogError("❌ Firestore yazma hatası: " + writeTask.Exception);
                        }
                    });
            }
            else
            {
                Debug.LogError("❌ Firebase bağımlılıkları eksik: " + task.Result);
            }
        });
    }
}
