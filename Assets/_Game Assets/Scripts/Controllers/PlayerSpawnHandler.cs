using UnityEngine;
public class PlayerSpawnerHandler : MonoBehaviour, IPlayerSpawner
{
    [SerializeField] private GameObject playerPrefab;

    private GameObject current;

    public GameObject Spawn()
    {
        DespawnAll();
        if (playerPrefab == null) return null;
        current = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        return current;
    }

    public void DespawnAll()
    {
        if (current != null) { Destroy(current); current = null; }
    }
}