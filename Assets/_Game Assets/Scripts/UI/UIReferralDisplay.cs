using TMPro;
using UnityEngine;

public class UIReferralDisplay : MonoBehaviour
{
    public TextMeshProUGUI usernameTMP;
    public TextMeshProUGUI earnedTMP;

    public void Set(string username, float earned)
    {
        if (usernameTMP) usernameTMP.text = string.IsNullOrWhiteSpace(username) ? "Guest" : username;
        if (earnedTMP)   earnedTMP.text   = earned.ToString("0.##");
    }
}