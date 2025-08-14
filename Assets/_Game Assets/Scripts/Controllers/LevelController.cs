using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

public class LevelController : MonoBehaviour
{
    public CameraController CameraController => cameraController;
    [SerializeField] private CameraController cameraController;

    [ShowInInspector] public List<Coin> Coins => coins;
    private List<Coin> coins;
    
    [SerializeField] private List<GameManager> levelParts;
    [SerializeField] private float spawnDistance;
    
    public LevelController Initialize()
    {
        var spawnPosition = transform.position;
        
        foreach (var levelPart in levelParts)
        {
            var part = Instantiate(levelPart, transform);
            
            part.transform.position = spawnPosition;
            
            spawnPosition += Vector3.forward * spawnDistance;
        }

        coins = transform.GetComponentsInChildren<Coin>().ToList();
        
        return this;
    }
}