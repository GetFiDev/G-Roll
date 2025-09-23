using UnityEngine;

public class GroundMaterialSwitcher : MonoBehaviour
{
    [Header("Ground Targets")]
    [Tooltip("Materyali değiştirmek istediğin tüm ground renderer'ları buraya sürükle.")]
    public Renderer[] groundRenderers;

    [Header("Presets (4 renk)")]
    public Material[] presets = new Material[4];

    [Header("Ayarlar")]
    [Tooltip("true: sharedMaterial kullanır (runtime'da kalıcı). false: instance materyal oluşturur.")]
    public bool useSharedMaterial = true;

    int currentIndex = -1;

    public void Apply(int index)
    {
        if (presets == null || index < 0 || index >= presets.Length) return;
        var mat = presets[index];
        if (mat == null) return;

        currentIndex = index;

        foreach (var r in groundRenderers)
        {
            if (r == null) continue;

            // Tek materyal kullanımını hedefliyoruz. Çoklu materyalse 0. slotu değiştiriyoruz.
            if (useSharedMaterial)
            {
                var mats = r.sharedMaterials;
                if (mats != null && mats.Length > 0) { mats[0] = mat; r.sharedMaterials = mats; }
                else r.sharedMaterial = mat;
            }
            else
            {
                var mats = r.materials;
                if (mats != null && mats.Length > 0) { mats[0] = mat; r.materials = mats; }
                else r.material = mat;
            }
        }
    }

    // İsteğe bağlı: 1-4 kısayolu
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) Apply(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Apply(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) Apply(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) Apply(3);
    }
}