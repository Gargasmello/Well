using UnityEngine;
using UnityEngine.InputSystem;

// Winch crank. Drag the wheel to pay rope out or wind it in.
// Owns the rope length: Rope and Pouring read it. Pouring writes Locked, and
// WellAudio reads TurnSpeed.
public class Crank : MonoBehaviour
{
    [Header("Drum")]
    // The feel knob: 0.3 gives about 1.9 m per full turn, 0.9 gives 5.7 m.
    [Tooltip("Metres of rope released per radian of wheel rotation.")]
    [SerializeField] float drumRadius = 0.3f;

    // Shortest the rope ever gets, in metres. There is no maximum.
    [SerializeField] float minRopeLength = 0.8f;

    [Header("Feel")]
    // How close the mouse has to be to the wheel centre to grab it.
    [SerializeField] float grabRadius = 0.8f;

    // Degrees the wheel turns per notch of the scroll wheel.
    [SerializeField] float scrollDegreesPerNotch = 8f;

    // Negative until the getter runs, which replaces it with minRopeLength. That
    // saves the scene from having to store a starting length.
    [SerializeField] float ropeLength = -1f;

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

    // Set by Pouring for the length of the pour, so you cannot start heading back
    // down with the bucket half emptied.
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

        // Angle from the wheel centre to the mouse. The drag is driven by how far
        // that angle swings, not by how far the mouse travels.
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

    // Counter-clockwise is positive. Cranking clockwise lets rope out, so the bucket
    // goes down.
    void Turn(float deltaDegrees)
    {
        SetRopeLength(RopeLength - deltaDegrees * Mathf.Deg2Rad * drumRadius);
    }

    // Sets the rope length and turns the wheel by the matching angle. The wheel
    // rotation is derived from how far the rope actually moved rather than from the
    // input, so once the rope is clamped at its minimum the wheel stops instead of
    // spinning on the spot. Pouring drives this directly while it animates.
    public void SetRopeLength(float length)
    {
        float before = RopeLength;
        float after = Mathf.Max(length, minRopeLength);

        float degreesTurned = (before - after) / (drumRadius * Mathf.Deg2Rad);

        transform.localRotation *= Quaternion.Euler(0f, 0f, degreesTurned);
        ropeLength = after;
    }

    // 0.35 is the smoothing factor; raw per-frame deltas are too noisy to drive audio.
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

    // ScreenToWorldPoint wants a z, and for an orthographic camera that z is the
    // distance from the camera to the z = 0 plane.
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
