using UnityEngine;

// Creak follows wheel speed; splash and thud play at a fixed level.
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
    [SerializeField] AudioSource splash;

    [Header("Ground hit")]
    [SerializeField] AudioSource ground;

    int _lastSplashCount;
    int _lastThrowCount;

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

    // SplashCount is a running total, so watch it for a change rather than for a value. It
    // counts both directions, so breaking back out of the surface splashes too.
    void UpdateSplash()
    {
        if (splash == null || bucket == null)
            return;

        if (bucket.SplashCount == _lastSplashCount)
            return;

        _lastSplashCount = bucket.SplashCount;

        if (splash.clip != null)
            splash.PlayOneShot(splash.clip);
    }

    // ThrowCount is a running total, so watch it for a change rather than for a value.
    void UpdateGround()
    {
        if (ground == null || crank == null)
            return;

        if (crank.ThrowCount == _lastThrowCount)
            return;

        _lastThrowCount = crank.ThrowCount;

        if (ground.clip != null)
            ground.PlayOneShot(ground.clip);
    }
}
