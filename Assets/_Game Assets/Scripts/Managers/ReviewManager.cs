using System.Collections;
using UnityEngine;

public abstract class ReviewManager
{
    private const string ReviewKey = "Review";

    private static bool ReviewRequested
    {
        get => PlayerPrefs.GetInt(ReviewKey, 0) == 1;
        set => PlayerPrefs.SetInt(ReviewKey, value ? 1 : 0);
    }

    public static void Initialize()
    {
        AppManager.Instance.StartCoroutine(ReviewCountDown());
    }

    private static IEnumerator ReviewCountDown()
    {
        if (ReviewRequested)
            yield break;

        if (DataManager.CurrentLevelIndex == 0)
        {
            yield return new WaitForSeconds(250);

            RequestReview();
        }
        else
        {
            yield return new WaitForSeconds(50);

            RequestReview();
        }
    }

    public static void RequestReview()
    {
        ReviewRequested = true;

#if UNITY_IOS
        UnityEngine.iOS.Device.RequestStoreReview();
#elif UNITY_ANDROID
        GameManager.Instance.StartCoroutine(GooglePlayReview());
#endif

        Debug.Log($"Review Requested @{Time.realtimeSinceStartup:N}");
    }

#if UNITY_ANDROID
    private static IEnumerator GooglePlayReview()
    {
        var reviewManager = new Google.Play.Review.ReviewManager();

        var requestFlowOperation = reviewManager.RequestReviewFlow();
        yield return requestFlowOperation;
        if (requestFlowOperation.Error != Google.Play.Review.ReviewErrorCode.NoError)
        {
            Debug.LogError("Review Request Error" + requestFlowOperation.Error);
            yield break;
        }

        var _playReviewInfo = requestFlowOperation.GetResult();

        var launchFlowOperation = reviewManager.LaunchReviewFlow(_playReviewInfo);
        yield return launchFlowOperation;
        _playReviewInfo = null; // Reset the object

        if (launchFlowOperation.Error != Google.Play.Review.ReviewErrorCode.NoError)
        {
            Debug.LogError("Review Launch Error : " + launchFlowOperation.Error);
            yield break;
        }
    }
#endif
}