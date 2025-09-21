using UnityEngine;

public class FinishLine : MonoBehaviour, IPlayerInteractable
{
    public void OnInteract(PlayerController player)
    {
        GameManager.Instance.LevelFinish(true);
    }
}
