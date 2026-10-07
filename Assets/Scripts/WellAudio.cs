using UnityEngine;

// Creak follows wheel speed; splash and thud follow how deep the bucket went.
public class WellAudio : MonoBehaviour
{
    [SerializeField] Crank crank;
    [SerializeField] Bucket bucket;

    [Header("Wheel creak")]
    [SerializeField] AudioSource creak;

    // Degrees per second for full volume.
    [SerializeField] float creakFullVolumeAt = 220f;

    [SerializeField] float creakMaxVolume = 0.55f;

    [Header("Splash")]
    // The only sign the bucket reached water, so it carries the distance.
    [SerializeField] AudioSource splash;

    [SerializeField] float splashVolumeNear = 1f;
    [SerializeField] float splashVolumeFar = 0.2f;

    // Low-pass cutoff in Hz. Lower is duller.
    [SerializeField] float splashCutoffNear = 1600f;
    [SerializeField] float splashCutoffFar = 400f;

    [Header("Ground hit")]
    // A dry trip. Softer than the splash: a disappointment, not a reward.
    [SerializeField] AudioSource ground;

    [SerializeField] float groundVolumeNear = 0.8f;
    [SerializeField] float groundVolumeFar = 0.15f;

    [SerializeField] float groundCutoffNear = 900f;
    [SerializeField] float groundCutoffFar = 250f;

    AudioLowPassFilter _splashFilter;
    AudioLowPassFilter _groundFilter;
    int _lastFillCount;
    int _lastHitGroundCount;

    void Start()
    {
        if (creak == null)
            return;

        // Left running at zero volume, so grabbing the wheel makes no click.
        creak.loop = true;
        creak.volume = 0f;
        creak.Play();
    }

    void Update()
    {
        UpdateCreak();
        UpdateSplash();
        UpdateGround();
    }

    void UpdateCreak()
    {
        if (creak == null || crank == null)
            return;

        // Locked means the pour is driving the wheel, not the player.
        float effort = crank.Locked
            ? 0f
            : Mathf.Clamp01(Mathf.Abs(crank.TurnSpeed) / creakFullVolumeAt);

        creak.volume = Mathf.MoveTowards(creak.volume, effort * creakMaxVolume, Time.deltaTime * 1.5f);
        creak.pitch = Mathf.Lerp(0.85f, 1.15f, effort);
    }

    // The counts are running totals, so watch for a change rather than a value.
    void UpdateSplash()
    {
        if (splash == null || bucket == null)
            return;

        if (bucket.FillCount == _lastFillCount)
            return;

        _lastFillCount = bucket.FillCount;

        // 0 at the surface, 1 at the deepest the table ever gets.
        float distance = Mathf.Clamp01(bucket.WaterDepth / bucket.MaxWaterDepth);

        splash.volume = Mathf.Lerp(splashVolumeNear, splashVolumeFar, distance);

        if (SplashFilter != null)
            SplashFilter.cutoffFrequency = Mathf.Lerp(splashCutoffNear, splashCutoffFar, distance);

        splash.Play();
    }

    void UpdateGround()
    {
        if (ground == null || bucket == null)
            return;

        if (bucket.HitGroundCount == _lastHitGroundCount)
            return;

        _lastHitGroundCount = bucket.HitGroundCount;

        float distance = Mathf.Clamp01(bucket.WaterDepth / bucket.MaxWaterDepth);

        ground.volume = Mathf.Lerp(groundVolumeNear, groundVolumeFar, distance);

        if (GroundFilter != null)
            GroundFilter.cutoffFrequency = Mathf.Lerp(groundCutoffNear, groundCutoffFar, distance);

        ground.Play();
    }

    AudioLowPassFilter SplashFilter
    {
        get
        {
            if (_splashFilter == null && splash != null)
                _splashFilter = splash.GetComponent<AudioLowPassFilter>();
            return _splashFilter;
        }
    }

    AudioLowPassFilter GroundFilter
    {
        get
        {
            if (_groundFilter == null && ground != null)
                _groundFilter = ground.GetComponent<AudioLowPassFilter>();
            return _groundFilter;
        }
    }
}
