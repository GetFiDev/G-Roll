using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AchievementItemView : MonoBehaviour
{
    [Header("Wiring")]
    public Button button;
    public Image  backgroundImage;
    public Image  iconImage;
    public TMP_Text titleTMP;
    public TMP_Text progressTMP;

    [Header("Background Sprites")]
    public Sprite spriteLevel0;
    public Sprite spriteLevel1to4;
    public Sprite spriteLevelMax;

    private AchDef _def;
    private AchState _state;
    private System.Action<AchDef, AchState> _onClick;

    public async void Bind(AchDef def, AchState state, System.Action<AchDef, AchState> onClick)
    {
        _def = def; _state = state; _onClick = onClick;

        titleTMP.text = def.displayName;
        int lvl = Mathf.Clamp(state?.level ?? 0, 0, def.maxLevel);
        progressTMP.text = $"{lvl}/{def.maxLevel}";

        backgroundImage.sprite = (lvl >= def.maxLevel) ? spriteLevelMax
                             : (lvl >= 1 ? spriteLevel1to4 : spriteLevel0);

        iconImage.sprite = null;
        var sp = await AchievementIconCache.LoadSpriteAsync(def.iconUrl);
        if (sp) iconImage.sprite = sp;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => _onClick?.Invoke(_def, _state));
    }
}