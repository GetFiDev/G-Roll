using UnityEngine;

public class FinishLine : MonoBehaviour, IPlayerInteractable
{
    public void OnInteract()
    {
        GameManager.Instance.LevelFinish(true);
    }
}
