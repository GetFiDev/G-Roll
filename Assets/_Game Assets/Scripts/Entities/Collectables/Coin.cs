using DG.Tweening;
using UnityEngine;

public class Coin : Collectable
{
    public override void OnInteract()
    {
        base.OnInteract();
        
        CurrencyEvents.OnCollected?.Invoke(CurrencyType.SoftCurrency, new CurrencyCollectedData(1, transform.position));
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