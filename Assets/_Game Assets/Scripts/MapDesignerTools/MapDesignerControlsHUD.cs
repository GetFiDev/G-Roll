using UnityEngine;

public class MapDesignerControlsHUD : MonoBehaviour
{
    public GridPlacer placer;
    public OrbitCamera orbitCam;

    public Vector2 margin = new Vector2(14, 14);
    public float width = 360f;
    public int line = 19;

    void OnGUI()
    {
        // Legacy HUD disabled by user request.
        // To re-enable, revert this file or uncomment original logic.
    }
}
