using UnityEngine;
using UnityEngine.InputSystem;

// Winch crank. Owns the rope length; Rope and Pouring read it, Pouring writes Locked.
public class Crank : MonoBehaviour
{
    [Header("Drum")]
    // Metres of rope per radian. 0.3 is about 1.9 m per full turn.
    [Tooltip("Metres of rope released per radian of wheel rotation.")]
    [SerializeField] float drumRadius = 0.3f;

    // Metres. No maximum of its own, but StrainFrom caps it.
    [SerializeField] float minRopeLength = 0.8f;

    [Header("Feel")]
    // How close the mouse has to be to the wheel centre to grab it.
    [SerializeField] float grabRadius = 0.8f;

    // Degrees per scroll-wheel notch.
    [SerializeField] float scrollDegreesPerNotch = 8f;

    [Header("Strain")]
    // Turns of rope between the bucket touching water or bottom and the crank locking
    // solid. The gearing falls off across this span, so it grinds rather than stopping dead.
    [Tooltip("Turns of slack before the crank locks up.")]
    [SerializeField] float strainTurns = 2f;

    // Gearing at the lock. Lower is heavier.
    [SerializeField] float strainGear = 0.15f;

    // Negative until first read; the getter replaces it with minRopeLength.
    [SerializeField] float ropeLength = -1f;

    // Rope length at which the crank starts to resist. Bucket sets this each trip; beyond
    // it the gearing falls away until the rope locks at StrainFrom + strainTurns.
    public float StrainFrom { get; set; } = float.PositiveInfinity;

    public float RopeLimit => StrainFrom + strainTurns * 2f * Mathf.PI * drumRadius;

    // Metres of rope currently out.
    public float RopeLength
    {
        get
        {
            if (ropeLength < 0f)
                ropeLength = minRopeLength;
            return ropeLength;
        }
    }

    // Used by Bucket to tell "all the way up".
    public float MinRopeLength => minRopeLength;

    // Signed degrees per second, smoothed. WellAudio maps its magnitude to creak volume.
    public float TurnSpeed { get; private set; }

    // Set by Pouring while it animates.
    public bool Locked { get; set; }

    Camera _camera;
    bool _dragging;
    float _lastMouseAngle;
    float _lastWheelAngle;

    void Update()
    {
        // Keep measuring while locked, so the creak fades out instead of sticking.
        TrackTurnSpeed();

        var mouse = Mouse.current;
        if (mouse == null || !EnsureCamera())
            return;

        if (Locked)
        {
            _dragging = false;
            return;
        }

        // Driven by the angle around the wheel, not by how far the mouse travels.
        Vector2 offset = MouseWorldPosition(mouse) - (Vector2)transform.position;

        if (mouse.leftButton.wasPressedThisFrame && offset.magnitude <= grabRadius)
        {
            _dragging = true;
            _lastMouseAngle = AngleOf(offset);
        }

        if (_dragging && mouse.leftButton.isPressed)
        {
            float angle = AngleOf(offset);
            Turn(Mathf.DeltaAngle(_lastMouseAngle, angle));
            _lastMouseAngle = angle;
        }

        if (mouse.leftButton.wasReleasedThisFrame)
            _dragging = false;

        float notches = mouse.scroll.ReadValue().y / 120f;
        if (Mathf.Abs(notches) > 0.01f)
            Turn(notches * scrollDegreesPerNotch);
    }

    // Counter-clockwise is positive; clockwise lets rope out.
    void Turn(float deltaDegrees)
    {
        float gear = GearAt(RopeLength);
        SetRopeLength(RopeLength - deltaDegrees * Mathf.Deg2Rad * drumRadius * gear);
    }

    // 1 outside the strain zone, falling to strainGear at the lock. Scaling the rope
    // movement also scales the wheel, because SetRopeLength derives the angle from it.
    float GearAt(float length)
    {
        if (length <= StrainFrom)
            return 1f;

        return Mathf.Lerp(1f, strainGear, Mathf.InverseLerp(StrainFrom, RopeLimit, length));
    }

    // Wheel angle comes from how far the rope moved, so the wheel stops when the rope
    // clamps at either end. Pouring drives this directly while it animates.
    public void SetRopeLength(float length)
    {
        float before = RopeLength;
        float after = Mathf.Clamp(length, minRopeLength, RopeLimit);

        float degreesTurned = (before - after) / (drumRadius * Mathf.Deg2Rad);

        transform.localRotation *= Quaternion.Euler(0f, 0f, degreesTurned);
        ropeLength = after;
    }

    // 0.35 smooths; raw per-frame deltas are too noisy to drive audio.
    void TrackTurnSpeed()
    {
        float angle = transform.localEulerAngles.z;
        float raw = Mathf.DeltaAngle(_lastWheelAngle, angle) / Mathf.Max(Time.deltaTime, 0.0001f);

        _lastWheelAngle = angle;
        TurnSpeed = Mathf.Lerp(TurnSpeed, raw, 0.35f);
    }

    static float AngleOf(Vector2 offset)
    {
        return Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
    }

    // Orthographic: the z is the camera's distance to the z = 0 plane.
    Vector2 MouseWorldPosition(Mouse mouse)
    {
        Vector2 screen = mouse.position.ReadValue();
        return _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
    }

    bool EnsureCamera()
    {
        if (_camera == null)
            _camera = Camera.main;
        return _camera != null;
    }
}
