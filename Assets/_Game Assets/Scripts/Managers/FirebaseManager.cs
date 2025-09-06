using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Analytics;
using Firebase.Auth;
using Firebase.Crashlytics;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;
using Newtonsoft.Json;

public abstract class FirebaseManager
{
    public static bool IsFirebaseInitialized = false;
    private static DependencyStatus _dependencyStatus = DependencyStatus.UnavailableOther;

    private static FirebaseApp _firebaseApp;
    private static FirebaseAuth _firebaseAuth;
    private static FirebaseFirestore _firebaseFirestore;
    
    // Cache
    private static List<GalleryElementInfo> _cachedGallery = null;

    public static void Initialize()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            _dependencyStatus = task.Result;
            if (_dependencyStatus == DependencyStatus.Available)
            {
                OnInitializationComplete();
            }
            else
            {
                Debug.LogError("Could not resolve all Firebase dependencies: " + _dependencyStatus);
            }
        });
    }

    private static void OnInitializationComplete()
    {
        _firebaseApp = FirebaseApp.DefaultInstance;
        _firebaseAuth = FirebaseAuth.DefaultInstance;
        _firebaseFirestore = FirebaseFirestore.DefaultInstance;
        
        FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);

        Crashlytics.ReportUncaughtExceptionsAsFatal = true;

        IsFirebaseInitialized = true;

        // DebugLog("Set user properties.");
        // // Set the user's sign up method.
        // FirebaseAnalytics.SetUserProperty(
        //     FirebaseAnalytics.UserPropertySignUpMethod,
        //     "Google");
        // // Set the user ID.
        // FirebaseAnalytics.SetUserId("uber_user_510");
        // // Set default session duration values.
        // FirebaseAnalytics.SetSessionTimeoutDuration(new TimeSpan(0, 30, 0));
    }
    
    public static async UniTask<List<GalleryElementInfo>> GetGalleryElementsAsync(bool forceRefresh = false)
    {
        if (!IsFirebaseInitialized)
            return new List<GalleryElementInfo>();

        if (_cachedGallery != null && !forceRefresh)
        {
            Debug.Log("Returning cached gallery elements (Newtonsoft).");
            return _cachedGallery;
        }

        try
        {
            var docRef = _firebaseFirestore.Document("appdata/gallery-view");
            var snapshot = await docRef.GetSnapshotAsync().AsUniTask();

            if (!snapshot.Exists)
            {
                Debug.LogWarning("Firestore document does not exist!");
                return new List<GalleryElementInfo>();
            }

            // Firestore dictionary -> JSON string
            string json = JsonConvert.SerializeObject(snapshot.ToDictionary());

            // JSON string -> Wrapper class
            var wrapper = JsonConvert.DeserializeObject<GalleryElementsWrapper>(json);

            if (wrapper?.elements == null)
                return new List<GalleryElementInfo>();

            _cachedGallery = new List<GalleryElementInfo>(wrapper.elements.Values);

            return _cachedGallery;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error fetching gallery elements: {e}");
            return _cachedGallery ?? new List<GalleryElementInfo>();
        }
    }

    public static void SignInWithGoogle()
    {
        // // Google Sign-In yapılandırması
        // GoogleSignIn.Configuration = new GoogleSignInConfiguration
        // {
        //     WebClientId = "926358794548-l5g8nno4kep2vt3hkhts6h3cika2nlts.apps.googleusercontent.com", // Firebase Console → Web client ID
        //     RequestIdToken = true
        // };
        //
        // GoogleSignIn.DefaultInstance.SignIn().ContinueWith(OnGoogleSignIn);
    }

    // private static void OnGoogleSignIn(Task<GoogleSignInUser> task)
    // {
    //     if (task.IsFaulted)
    //     {
    //         Debug.LogError("Google Sign-In failed: " + task.Exception);
    //         return;
    //     }
    //
    //     if (task.IsCanceled)
    //     {
    //         Debug.LogWarning("Google Sign-In canceled.");
    //         return;
    //     }
    //
    //     var idToken = task.Result.IdToken;
    //     var credential = GoogleAuthProvider.GetCredential(idToken, null);
    //
    //     _firebaseAuth.SignInWithCredentialAsync(credential).ContinueWith(authTask =>
    //     {
    //         if (authTask.IsFaulted)
    //         {
    //             Debug.LogError("Firebase Sign-In failed: " + authTask.Exception);
    //             return;
    //         }
    //
    //         var newUser = authTask.Result;
    //         Debug.Log("Firebase user signed in: " + newUser.DisplayName);
    //     });
    // }
}