using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    [ReadOnly] public LevelController currentLevel;

    [SerializeField, AssetsOnly] private List<LevelController> levels;

    public LevelManager Initialize()
    {
        if (FindAnyObjectByType<LevelController>())
        {
            currentLevel = FindAnyObjectByType<LevelController>()?.Initialize();
            
            Debug.LogWarning("Level Initialized From Scene !".LogColor(Color.yellow), this);
            return this;
        }

        if (levels is null || levels.Count <= 0)
        {
            Debug.LogError("Levels Missing !", this);
            return this;
        }

        var levelToInitialize = levels[DataManager.CurrentLevelIndex % levels.Count];

        currentLevel = Instantiate(levelToInitialize).Initialize();

        return this;
    }
    
    public static void ReloadScene()
    {
        DOTween.Clear();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}