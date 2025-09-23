using UnityEngine;

public class MapDesignerControlsHUD : MonoBehaviour
{
    public GridPlacer placer;     // sahnedeki GridPlacer
    public OrbitCamera orbitCam;  // opsiyonel (şimdilik sadece referans)

    public Vector2 margin = new Vector2(14, 14);
    public float width = 360f;
    public int line = 19;

    void OnGUI()
    {
        if (placer == null) return;

        // --- Dynamic height calc (adds a line when portal pairing is active) ---
        int baseLines = 9; // Title + Pan + Orbit + Zoom + Mode + ESC + X + P + Click
        int lines = baseLines + (placer.PortalPairMode ? 1 : 0);
        var h = lines * line + 16;

        var rect = new Rect(Screen.width - width - margin.x, margin.y, width, h);
        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var pad = 10f;
        var y = rect.y + pad;
        var x = rect.x + pad;

        LabelBold(x, y, "LEVEL EDITOR — Controls"); y += line + 6;

        Label(x, y, "Pan:         Left Mouse drag"); y += line;
        Label(x, y, "Orbit:       Right Mouse drag"); y += line;
        Label(x, y, "Zoom:        Mouse Scroll"); y += line;

        // Mode line — show Portal Pairing as a distinct mode
        string displayMode = placer.PortalPairMode ? "Portal Pairing" : placer.currentMode.ToString();
        string item = (!placer.PortalPairMode && placer.currentMode == BuildMode.Place && placer.currentItem != null)
                        ? $" ({placer.currentItem.displayName})" : string.Empty;
        LabelBold(x, y, $"Mode: {displayMode}{item}"); y += line;

        // Universal ESC exit
        Label(x, y, "ESC:         Exit to Navigate"); y += line;

        // One-way entries (no toggles)
        Label(x, y, "X:           Enter Demolish mode"); y += line;
        Label(x, y, "P:           Enter Portal Pairing mode"); y += line;

        // Pairing state hint
        if (placer.PortalPairMode)
        {
            string pairState = placer.HasPortalFirstSelection ? "Select SECOND portal" : "Select FIRST portal";
            LabelBold(x, y, $"Portal Pairing: {pairState}"); y += line;
        }

        // Click behavior hint (depends on mode/pairing)
        string clickHint = placer.PortalPairMode ? "Select portals to pair" :
                           (placer.currentMode == BuildMode.Demolish ? "Remove object" : "Place / Select");
        Label(x, y, $"Click:       {clickHint}"); y += line;

        GUI.color = Color.white;
    }

    void Label(float x, float y, string text) => GUI.Label(new Rect(x, y, 1000, line + 4), text);

    void LabelBold(float x, float y, string text)
    {
        var prev = GUI.skin.label.fontStyle;
        GUI.skin.label.fontStyle = FontStyle.Bold;
        Label(x, y, text);
        GUI.skin.label.fontStyle = prev;
    }
}
