using UnityEngine;

public class UIOverlay : MonoBehaviour
{
    [SerializeField] private GameObject noInternetPopUp;

    private void Start()
    {
        //InvokeRepeating(nameof(CheckForInternetConnection),1f, 3f);
    }

    private void CheckForInternetConnection()
    {
        var isReachable = Application.internetReachability != NetworkReachability.NotReachable;
        noInternetPopUp.SetActive(!isReachable);
    }
}
