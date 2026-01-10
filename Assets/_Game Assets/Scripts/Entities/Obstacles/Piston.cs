using UnityEngine;
using DG.Tweening;
using MapDesignerTool; // for IMapConfigurable
using System.Collections.Generic;
using System.Globalization;

public class Piston : Wall, IMapConfigurable
{
    [Header("Piston Settings")]
    [Tooltip("The actual object that will move.")]
    public Transform movingPart;
    [Tooltip("Transform defining the start position.")]
    public Transform startTransform;
    [Tooltip("Transform defining the target position.")]
    public Transform targetTransform;
    [Tooltip("Time in seconds between strikes.")]
    public float interval = 2f;
    [Tooltip("Start time offset (0-10s) to create wave effects.")]
    public float startOffset = 0f;

    [Header("Animation Settings")]
    public Ease strikeEase = Ease.OutCubic;
    public Ease returnEase = Ease.OutFlash;

    [Header("Visual Effects")]
    public ParticleSystem pistonParticle;
    public bool enableParticle = true;

    private const float StrikeDuration = 0.3f;
    private const float ReturnDuration = 0.1f;

    private void Start()
    {
        if (movingPart == null || startTransform == null || targetTransform == null)
        {
            Debug.LogError("Piston: Missing references!", this);
            return;
        }

        // Piston starts at startTransform. 
        // We will move it in Update based on time.
        movingPart.position = startTransform.position;

        // Setup Collision Proxy on ALL child colliders recursively (from Root)
        var allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in allColliders)
        {
            var proxy = col.GetComponent<PistonCollisionProxy>();
            if (proxy == null) proxy = col.gameObject.AddComponent<PistonCollisionProxy>();
            proxy.pistonOwner = this;
        }
    }

    private void Update()
    {
        if (movingPart == null || startTransform == null || targetTransform == null) return;

        // GLOBAL TIME SYNC
        // Cycle: Strike (0.3) -> Callback(Particle) -> Return (0.1) -> Wait (interval)
        // Total Cycle Time = StrikeDuration + ReturnDuration + interval
        float totalCycle = StrikeDuration + ReturnDuration + interval;
        if (totalCycle < 0.1f) return;

        // Where are we in the cycle (0..totalCycle)
        float t = (Time.time + startOffset) % totalCycle;

        if (t < StrikeDuration)
        {
            // STRIKE PHASE (Start -> Target)
            float progress = t / StrikeDuration; // 0..1
            // Use Tween Ease logic manually? Or standard linear?
            // User likes easing. Helper function?
            // Usually simpler to use Lerp if we want simple robustness.
            // But lets try to respect 'strikeEase' if possible via a helper or simple easing.
            // For now, simple Slerp or similar is decent, but let's use DOVirtual.EasedValue ideally.
            // But we don't want to spam it.
            // Let's settle for simple Lerp for robustness in Sync Mode.
            // Or cubic manual lerp: p*p*p
            
            float eased = DOVirtual.EasedValue(0, 1, progress, strikeEase);
            movingPart.position = Vector3.Lerp(startTransform.position, targetTransform.position, eased);
        }
        else if (t < StrikeDuration + ReturnDuration)
        {
            // RETURN PHASE (Target -> Start)
            // But first, did we just transition from Strike? (Particle trigger)
            // Triggering particles in Update is tricky. 
            // We can check if `t` just passed the threshold delta but that's unreliable with frames.
            // For Sync Mode, maybe we skip Particle trigger OR we just loop an independeny particle system?
            // Let's focus on Movement Sync.
            
            float tRel = t - StrikeDuration;
            float progress = tRel / ReturnDuration; // 0..1
            float eased = DOVirtual.EasedValue(0, 1, progress, returnEase);
            movingPart.position = Vector3.Lerp(targetTransform.position, startTransform.position, eased);
            
            // Hacky Particle: if progress < 0.2f (start of return), try play?
            // Better to ignore particles or use a separate loop.
            if (enableParticle && pistonParticle != null && !pistonParticle.isPlaying && progress < 0.2f)
            {
               // This might spam play if frame rate is high?
               // ParticleSystem.isPlaying check helps.
               pistonParticle.Play();
            }
        }
        else
        {
            // WAIT PHASE (Stay at Start)
            movingPart.position = startTransform.position;
        }
    }

    // Public methods for the proxy to call
    public void OnProxyTriggerEnter(Collider other)
    {
        if (expectTrigger) NotifyPlayer(other);
    }

    public void OnProxyCollisionEnter(Collision collision)
    {
        if (!expectTrigger) NotifyPlayer(collision.collider);
    }
    
    // --- IMapConfigurable Implementation ---

    public List<ConfigDefinition> GetConfigDefinitions()
    {
        return new List<ConfigDefinition>
        {
            new ConfigDefinition
            {
                key = "interval",
                displayName = "Interval (Rate)",
                type = ConfigType.Float,
                min = 0.5f,
                max = 5.0f,
                defaultValue = this.interval
            },
            new ConfigDefinition
            {
                key = "offset",
                displayName = "Start Offset (s)",
                type = ConfigType.Float,
                min = 0f,
                max = 10f,
                defaultValue = this.startOffset
            },
            new ConfigDefinition
            {
                key = "particle",
                displayName = "Enable Effect",
                type = ConfigType.Bool,
                defaultBool = this.enableParticle
            }
        };
    }

    public void ApplyConfig(Dictionary<string, string> config)
    {
        if (config.TryGetValue("interval", out var intVal))
        {
            if (float.TryParse(intVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            {
                interval = Mathf.Clamp(f, 0.2f, 10f); // Allow wide range
            }
        }
        
        if (config.TryGetValue("offset", out var oVal))
        {
            if (float.TryParse(oVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                startOffset = Mathf.Clamp(f, 0f, 10f);
        }

        if (config.TryGetValue("particle", out var pVal))
        {
            enableParticle = bool.Parse(pVal);
        }
    }
}

// Helper component added automatically to the moving part (unchanged)
public class PistonCollisionProxy : MonoBehaviour
{
    public Piston pistonOwner;

    private void OnTriggerEnter(Collider other)
    {
        if (pistonOwner != null) pistonOwner.OnProxyTriggerEnter(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (pistonOwner != null) pistonOwner.OnProxyCollisionEnter(collision);
    }
}
// Force Recompile Fri Jan  9 23:15:37 +03 2026
