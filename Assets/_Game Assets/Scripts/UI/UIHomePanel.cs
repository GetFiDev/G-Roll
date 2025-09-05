using UnityEngine;

public class UIHomePanel : MonoBehaviour
{
    public void OnClickPlayButton()
    {
        GameManager.Instance.LevelStart();
    }
}
