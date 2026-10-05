using UnityEngine;

// Creak follows how fast the wheel turns; splash volume and low-pass cutoff follow how
// deep the water was. Reads TurnSpeed from Crank and FillCount from Bucket.
public class WellAudio : MonoBehaviour
{
    [SerializeField] Crank crank;
    [SerializeField] Bucket bucket;

    [Header("Wheel creak")]
    [SerializeField] AudioSource creak;

    // Degrees per second at which the creak reaches full volume.
    [SerializeField] float creakFullVolumeAt = 220f;

    [SerializeField] float creakMaxVolume = 0.55f;

    [Header("Splash")]
    // The splash is the only feedback that the bucket reached water, so it carries the
    // distance: quiet and dull when deep.
    [SerializeField] AudioSource splash;

    [SerializeField] float splashVolumeNear = 1f;
    [SerializeField] float splashVolumeFar = 0.2f;

    // Low-pass cutoff in Hz. Lower is duller.
    [SerializeField] float splashCutoffNear = 1600f;
    [SerializeField] float splashCutoffFar = 400f;

    AudioLowPassFilter _splashFilter;
    int _lastFillCount;

    void Start()
    {
        if (creak == null)
            return;

        // Left playing at zero volume rather than started and stopped, so there is no
        // click when the player grabs the wheel.
        creak.loop = true;
        creak.volume = 0f;
        creak.Play();
    }

    void Update()
    {
        UpdateCreak();
        UpdateSplash();
    }

    void UpdateCreak()
    {
        if (creak == null || crank == null)
            return;

        // While the pour has the crank locked the wheel is driven by the animation, not
        // by the player, so the creak stays out of the way.
        float effort = crank.Locked
            ? 0f
            : Mathf.Clamp01(Mathf.Abs(crank.TurnSpeed) / creakFullVolumeAt);

        creak.volume = Mathf.MoveTowards(creak.volume, effort * creakMaxVolume, Time.deltaTime * 1.5f);
        creak.pitch = Mathf.Lerp(0.85f, 1.15f, effort);
    }

    // FillCount is a running total, so watch it for a change rather than for a value.
    void UpdateSplash()
    {
        if (splash == null || bucket == null)
            return;

        if (bucket.FillCount == _lastFillCount)
            return;

        _lastFillCount = bucket.FillCount;

        // Measured from the surface rather than from the shallowest the table ever gets,
        // so the whole scale slides down together: 0 at the surface, 1 at the deepest.
        float distance = Mathf.Clamp01(bucket.WaterDepth / bucket.MaxWaterDepth);

        splash.volume = Mathf.Lerp(splashVolumeNear, splashVolumeFar, distance);

        if (SplashFilter != null)
            SplashFilter.cutoffFrequency = Mathf.Lerp(splashCutoffNear, splashCutoffFar, distance);

        splash.Play();
    }

    // Looked up once and kept.
    AudioLowPassFilter SplashFilter
    {
        get
        {
            if (_splashFilter == null && splash != null)
                _splashFilter = splash.GetComponent<AudioLowPassFilter>();
            return _splashFilter;
        }
    }
}
