using UnityEngine;
public class PlayerSpawnerHandler : MonoBehaviour, IPlayerSpawner
{
    [SerializeField] private GameObject playerPrefab;

    private GameObject current;

    public GameObject Spawn()
    {
        DespawnAll();
        if (playerPrefab == null) return null;
        
        // Spawn at Y=0.25 to ensure player is on ground level, not embedded in floor
        Vector3 spawnPos = new Vector3(0f, 0.25f, 0f);
        current = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        return current;
    }

    public void DespawnAll()
    {
        if (current != null) { Destroy(current); current = null; }
    }
}