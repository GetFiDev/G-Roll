using Firebase;
using Firebase.Analytics;
using Firebase.Auth;
using Firebase.Crashlytics;
using Firebase.Extensions;
using UnityEngine;

public abstract class FirebaseManager
{
    public static bool IsFirebaseInitialized = false;
    private static DependencyStatus _dependencyStatus = DependencyStatus.UnavailableOther;
    private static FirebaseApp _firebaseApp;

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

    public static void Authentication()
    {
        var authInstance = FirebaseAuth.DefaultInstance;

        //var credential = GoogleAuthProvider.GetCredential(googleIdToken, googleAccessToken);

        // authInstance.SignInAndRetrieveDataWithCredentialAsync(credential).ContinueWith(task =>
        // {
        //     if (task.IsCanceled)
        //     {
        //         Debug.LogError("SignInAndRetrieveDataWithCredentialAsync was canceled.");
        //         return;
        //     }
        //
        //     if (task.IsFaulted)
        //     {
        //         Debug.LogError("SignInAndRetrieveDataWithCredentialAsync encountered an error: " + task.Exception);
        //         return;
        //     }
        //
        //     var result = task.Result;
        //
        //     Debug.LogFormat("User signed in successfully: {0} ({1})", result.User.DisplayName, result.User.UserId);
        // });
    }
}