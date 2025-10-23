using System.Collections;
using TMPro;
using UnityEngine;

public class UISessionGate : MonoBehaviour
{
    [SerializeField] private CanvasGroup root;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private float toastDuration = 0.5f;

    void Awake() { if (root != null) root.alpha = 0; gameObject.SetActive(false); }

    public void ShowRequesting()
    {
        EnsureHierarchyActive();

        gameObject.SetActive(true);
        if (root)
        {
            root.alpha = 1f;
            root.blocksRaycasts = true;
            root.interactable = false;
        }
        // Bring to top to ensure visibility
        transform.SetAsLastSibling();
        if (statusText)
        {
            statusText.color = Color.white;
            statusText.text = "Please wait while session is granting…";
        }
    }

    public IEnumerator ShowGrantedToast()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (root)
        {
            root.alpha = 1f;
            root.blocksRaycasts = true; // still block during toast
            root.interactable = false;
        }
        if (statusText)
        {
            statusText.color = new Color(0.2f, 1f, 0.4f);
            statusText.text = "Session granted";
        }
        yield return new WaitForSeconds(toastDuration);
        Close();
    }

    public IEnumerator ShowInsufficientToast()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (root)
        {
            root.alpha = 1f;
            root.blocksRaycasts = true;
            root.interactable = false;
        }
        if (statusText)
        {
            statusText.color = new Color(1f, 0.35f, 0.35f);
            statusText.text = "Not enough energy";
        }
        yield return new WaitForSeconds(toastDuration);
        Close();
    }

    public void Close()
    {
        if (root)
        {
            root.alpha = 0f;
            root.blocksRaycasts = false;
            root.interactable = false;
        }
        gameObject.SetActive(false);
    }
    private void EnsureHierarchyActive()
    {
        // Parent zincirini etkinleştir
        var t = transform;
        while (t != null)
        {
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            t = t.parent;
        }

        // En yakın Canvas devre dışıysa aç
        var c = GetComponentInParent<Canvas>(true);
        if (c != null && !c.enabled) c.enabled = true;

        Canvas.ForceUpdateCanvases();
    }
}