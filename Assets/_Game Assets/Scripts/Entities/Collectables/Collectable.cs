using DG.Tweening;
using UnityEngine;

public abstract class Collectable : MonoBehaviour, IPlayerInteractable
{
    public virtual void OnInteract()
    {
        var activeCollider = gameObject.GetComponent<Collider>();
        activeCollider.enabled = false;

        transform.DOScale(Vector3.zero, .2f)
            .SetEase(Ease.InBack)
            .OnComplete(() => Destroy(gameObject));
    }
}
