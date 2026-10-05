using UnityEngine;

/// <summary>
/// The two sounds of the well.
///
/// The creak is the sound of you working. It loops continuously and its volume
/// and pitch follow how fast the wheel is turning, so it swells when you grab the
/// wheel and dies away when you let go. While the bucket is underground this is
/// the only sign that anything is happening at all.
///
/// The splash is the only sign that the bucket has reached water - you cannot see
/// down there - so it carries the distance for you. The deeper the water was, the
/// quieter and duller it arrives, the way a splash at the bottom of a well would.
/// </summary>
public class WellAudio : MonoBehaviour
{
    [Tooltip("The crank. Wheel speed is read from it.")]
    [SerializeField] Crank crank;

    [Tooltip("The bucket. Reaching water, and how deep it was, are read from it.")]
    [SerializeField] Bucket bucket;

    [Header("Wheel creak")]
    [SerializeField] AudioSource creak;

    [Tooltip("Wheel speed, in degrees per second, at which the creak reaches full volume.")]
    [SerializeField] float creakFullVolumeAt = 220f;

    [SerializeField] float creakMaxVolume = 0.55f;

    [Header("Splash")]
    [SerializeField] AudioSource splash;

    [Tooltip("Splash volume at the surface, i.e. at zero depth.")]
    [SerializeField] float splashVolumeNear = 1f;

    [Tooltip("Splash volume at the deepest the water table ever gets.")]
    [SerializeField] float splashVolumeFar = 0.2f;

    [Tooltip("Low-pass cutoff, in Hz, at the surface.")]
    [SerializeField] float splashCutoffNear = 1600f;

    [Tooltip("Low-pass cutoff, in Hz, at the deepest the water table ever gets. Lower is duller.")]
    [SerializeField] float splashCutoffFar = 400f;

    AudioLowPassFilter _splashFilter;
    int _lastFillCount;

    void Start()
    {
        if (creak == null)
            return;

        // Always playing, with the volume doing the work, so there is no click on start-up.
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

        // While the pour has the crank locked the wheel is being driven by the
        // animation, not by the player, so the creak stays out of the way.
        float effort = crank.Locked
            ? 0f
            : Mathf.Clamp01(Mathf.Abs(crank.TurnSpeed) / creakFullVolumeAt);

        creak.volume = Mathf.MoveTowards(creak.volume, effort * creakMaxVolume, Time.deltaTime * 1.5f);
        creak.pitch = Mathf.Lerp(0.85f, 1.15f, effort);
    }

    void UpdateSplash()
    {
        if (splash == null || bucket == null)
            return;

        if (bucket.FillCount == _lastFillCount)
            return;

        _lastFillCount = bucket.FillCount;

        // How far down the water was, measured from the surface rather than from the
        // shallowest the table ever gets, so the whole scale slides down together:
        // 0 at the surface, 1 at the deepest it can be.
        float distance = Mathf.Clamp01(bucket.WaterDepth / bucket.MaxWaterDepth);

        splash.volume = Mathf.Lerp(splashVolumeNear, splashVolumeFar, distance);

        if (SplashFilter != null)
            SplashFilter.cutoffFrequency = Mathf.Lerp(splashCutoffNear, splashCutoffFar, distance);

        splash.Play();
    }

    /// <summary>Looked up once and kept, rather than on every splash.</summary>
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
