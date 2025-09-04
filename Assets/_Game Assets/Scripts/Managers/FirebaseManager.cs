using System.Threading.Tasks;
using Firebase;
using Firebase.Analytics;
using Firebase.Auth;
using Firebase.Crashlytics;
using Firebase.Extensions;
using Google;
using UnityEngine;

public abstract class FirebaseManager
{
    public static bool IsFirebaseInitialized = false;
    private static DependencyStatus _dependencyStatus = DependencyStatus.UnavailableOther;

    private static FirebaseApp _firebaseApp;
    private static FirebaseAuth _firebaseAuth;

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