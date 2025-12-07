using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class AchievementItemView : MonoBehaviour
{
    [Header("Wiring")]
    public Button button;
    public Image  backgroundImage;
    public Image  iconImage;
    public TMP_Text titleTMP;
    public TMP_Text progressTMP;

    private Coroutine _iconFadeRoutine;

    public enum AchievementVisualState
    {
        Claimable,    // Green
        InProgress,   // Orange
        Locked,       // Gray (No progress)
        Completed     // Gray (Max level reached)
    }

    [Header("Background Sprites")]
    // Eski sprite'lar yerine state-based sprite'lar
    public Sprite spriteClaimable;   // Green
    public Sprite spriteInProgress;  // Orange
    public Sprite spriteLocked;      // Gray
    public Sprite spriteCompleted;   // Gray (or distinct)

    private AchDef _def;
    private AchState _state;
    private System.Action<AchDef, AchState> _onClick;

    public async void Bind(AchDef def, AchState state, AchievementVisualState visualState, System.Action<AchDef, AchState> onClick)
    {
        _def = def; _state = state; _onClick = onClick;

        titleTMP.text = def.displayName;
        int lvl = Mathf.Clamp(state?.level ?? 0, 0, def.maxLevel);

        // === Strict: NEXT threshold with concrete types (no reflection) ===
        // Assumptions from AchievementService model:
        // def.thresholds : List<double>
        // state.progress : double
        // state.nextThreshold : double? (nullable)
        {
            var thresholds = def.thresholds; // concrete list
            double nextTarget;

            if (lvl >= def.maxLevel)
            {
                nextTarget = thresholds[thresholds.Count - 1];
            }
            else if (state.nextThreshold.HasValue)
            {
                nextTarget = state.nextThreshold.Value;
            }
            else
            {
                int idx = Mathf.Clamp(lvl, 0, thresholds.Count - 1); // level 1 → index 0
                nextTarget = thresholds[idx];
            }

            double current = state.progress; // concrete double

            // Clamp and format
            double shown = System.Math.Max(0.0, System.Math.Min(current, nextTarget));
            bool shownIsInt = Mathf.Approximately((float)shown, Mathf.Round((float)shown));
            bool nextIsInt  = Mathf.Approximately((float)nextTarget, Mathf.Round((float)nextTarget));

            if (shownIsInt && nextIsInt)
                progressTMP.text = $"{(int)System.Math.Round(shown)}/{(int)System.Math.Round(nextTarget)}";
            else
                progressTMP.text = $"{shown:0.#}/{nextTarget:0.#}";
        }
        // === End strict block ===

        // Apply background based on visual state
        switch (visualState)
        {
            case AchievementVisualState.Claimable:
                backgroundImage.sprite = spriteClaimable;
                break;
            case AchievementVisualState.InProgress:
                backgroundImage.sprite = spriteInProgress;
                break;
            case AchievementVisualState.Locked:
                backgroundImage.sprite = spriteLocked;
                break;
            case AchievementVisualState.Completed:
                backgroundImage.sprite = spriteCompleted;
                break;
        }

        if (iconImage)
        {
            iconImage.sprite = null;
            iconImage.gameObject.SetActive(false);
        }

        var sp = await AchievementIconCache.LoadSpriteAsync(def.iconUrl);
        
        // Fix: Check if object is still active before continuing
        if (this == null || !gameObject.activeInHierarchy) return;

        if (sp != null && iconImage != null)
        {
            iconImage.sprite = sp;
            iconImage.gameObject.SetActive(true);
            
            // Set alpha to 0 and fade in
            var c = iconImage.color;
            c.a = 0f;
            iconImage.color = c;

            if (_iconFadeRoutine != null) StopCoroutine(_iconFadeRoutine);
            _iconFadeRoutine = StartCoroutine(FadeInIcon());
        }
        else if (iconImage != null)
        {
            iconImage.gameObject.SetActive(false);
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => _onClick?.Invoke(_def, _state));
    }

    private IEnumerator FadeInIcon()
    {
        if (iconImage == null) yield break;
        
        float duration = 0.25f;
        float t = 0f;
        Color c = iconImage.color;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / duration);
            c.a = a;
            iconImage.color = c;
            yield return null;
        }
        c.a = 1f;
        iconImage.color = c;
    }
}