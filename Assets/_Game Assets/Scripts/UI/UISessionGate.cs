using System.Collections;
using TMPro;
using UnityEngine;

public class UISessionGate : MonoBehaviour
{
    [SerializeField] private CanvasGroup root;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private float toastDuration = 0.6f;

    void Awake() { if (root != null) root.alpha = 0; gameObject.SetActive(false); }

    public void ShowRequesting()
    {
        gameObject.SetActive(true);
        if (root) root.alpha = 1f;
        if (statusText) { statusText.color = Color.white; statusText.text = "Requesting session…"; }
    }

    public IEnumerator ShowGrantedToast()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (root) root.alpha = 1f;
        if (statusText) { statusText.color = new Color(0.2f,1f,0.4f); statusText.text = "Session granted"; }
        yield return new WaitForSeconds(toastDuration);
        Close();
    }

    public IEnumerator ShowInsufficientToast()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (root) root.alpha = 1f;
        if (statusText) { statusText.color = new Color(1f,0.35f,0.35f); statusText.text = "Not enough energy"; }
        yield return new WaitForSeconds(toastDuration);
        Close();
    }

    public void Close()
    {
        if (root) root.alpha = 0f;
        gameObject.SetActive(false);
    }
}