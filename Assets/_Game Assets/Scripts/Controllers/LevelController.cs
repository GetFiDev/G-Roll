using System.Collections.Generic;
using UnityEngine;

public class LevelController : MonoBehaviour
{
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
        
        return this;
    }
}