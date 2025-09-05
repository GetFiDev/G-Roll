using DG.Tweening;
using UnityEngine;

public class UIReferralPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup referralCodeModal;
    
    public void OnCopyCodeButtonClick()
    {
        referralCodeModal.DOKill();
        
        referralCodeModal.alpha = 1;
        referralCodeModal.DOFade(0, .5f).SetDelay(1f).SetEase(Ease.OutCubic);
    }
}
