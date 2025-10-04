using UnityEngine;

public class Coin : MonoBehaviour, IPlayerInteractable
{
    public float value = 0.1f;
    public GameObject vfxOnCollect;

    private bool _collected;

    public void OnPlayerEnter(PlayerController player, Collider other)
    {
        if (_collected) return;
        _collected = true;

        GameplayManager.Instance.AddCoins(value);
        if (vfxOnCollect) Instantiate(vfxOnCollect, transform.position, Quaternion.identity);

        gameObject.SetActive(false);
    }

    public void OnPlayerStay(PlayerController p, Collider o, float dt) { }
    public void OnPlayerExit(PlayerController p, Collider o) { }
}