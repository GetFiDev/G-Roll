using UnityEngine;

public abstract class BoosterBase : MonoBehaviour, IPlayerInteractable
{
    [Header("Booster Base")]
    public bool oneShot = true;
    public bool destroyOnUse = true;
    public GameObject vfxOnUse;
    public AudioClip sfxOnUse;
    public string requiredTag = "Player"; // istersen boş bırak

    protected bool _used;
    protected virtual void PlayFx()
    {
        if (vfxOnUse) Instantiate(vfxOnUse, transform.position, Quaternion.identity);
        if (sfxOnUse) AudioSource.PlayClipAtPoint(sfxOnUse, transform.position);
    }

    // Booster davranışı buraya yazılacak
    protected abstract void Apply(PlayerController player);

    public void OnPlayerEnter(PlayerController player, Collider other)
    {
        if (_used && oneShot) return;
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;

        _used = true;
        Apply(player);
        PlayFx();

        if (destroyOnUse) gameObject.SetActive(false);
    }

    public void OnPlayerStay(PlayerController p, Collider o, float dt) { }
    public void OnPlayerExit(PlayerController p, Collider o) { }
}