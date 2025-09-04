using DG.Tweening;
using UnityEngine;

public class Coin : Collectable
{
    public override void OnInteract()
    {
        var activeCollider = gameObject.GetComponent<Collider>();
        activeCollider.enabled = false;
        
        transform.DOScale(Vector3.zero, .2f)
            .SetDelay(.2f)
            .SetEase(Ease.InBack);
        
        _magnetTween = transform.DOMove(PlayerController.Instance.transform.position, .2f)
            .SetEase(Ease.OutQuad)
            .OnUpdate(() =>
            {
                _magnetTween?.ChangeEndValue(PlayerController.Instance.transform.position, true);
            });
        
        CurrencyEvents.OnCollected?.Invoke(CurrencyType.SoftCurrency, new CurrencyCollectedData(1, transform.position));

        GameManager.Instance.levelManager.currentLevel.Coins.Remove(this);
    }

    private Tweener _magnetTween;

    public void CollectByMagnet(Transform targetTransform)
    {
        _magnetTween = transform.DOMove(targetTransform.position, 20f)
            .SetSpeedBased(true)
            .SetEase(Ease.OutQuad)
            .OnUpdate(() =>
            {
                _magnetTween?.ChangeEndValue(targetTransform.position, true);
            });
    }
}