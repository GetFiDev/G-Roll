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

        Debug.Log($"Review NOT Requested @{Time.realtimeSinceStartup:N}");
    }

}