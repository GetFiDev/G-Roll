using UnityEngine;
using UnityEngine.UI;

public class UILoading : MonoBehaviour
{
    [SerializeField] private Image loadingBarFill;
    
    public void SetProgress(float progress)
    {
        loadingBarFill.fillAmount = progress;
    }
}
