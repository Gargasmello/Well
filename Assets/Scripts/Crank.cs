using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The winch crank.
///
/// Hold the left mouse button on the wheel and drag: the wheel follows the mouse,
/// and the rope pays out or winds back in. The rope is wound on a drum, so one
/// radian of wheel rotation releases <c>drumRadius</c> metres of rope.
///
/// The rope has a minimum length (you cannot wind the bucket up into the wheel)
/// and no maximum - the well is as deep as you are willing to keep cranking.
///
/// This script answers exactly one question: how much rope is out right now.
/// What the rope looks like and where the bucket hangs is Rope's business.
/// </summary>
public class Crank : MonoBehaviour
{
    [Header("Drum")]
    [Tooltip("Radius of the drum the rope winds on. One radian of wheel rotation " +
             "releases this many metres of rope. This is the main 'how heavy does it feel' knob.")]
    [SerializeField] float drumRadius = 0.3f;

    [Header("Rope length")]
    [Tooltip("Shortest the rope ever gets, in metres. There is no maximum.")]
    [SerializeField] float minRopeLength = 0.8f;

    [Header("Feel")]
    [Tooltip("How close to the wheel centre the mouse has to be to grab the wheel.")]
    [SerializeField] float grabRadius = 0.8f;

    [Tooltip("Degrees the wheel turns per notch of the scroll wheel.")]
    [SerializeField] float scrollDegreesPerNotch = 8f;

    [SerializeField]
    [Tooltip("Metres of rope currently out. Negative means 'not initialised yet', " +
             "which is treated as the minimum.")]
    float ropeLength = -1f;

    /// <summary>Metres of rope currently paid out.</summary>
    public float RopeLength
    {
        get
        {
            if (ropeLength < 0f)
                ropeLength = minRopeLength;
            return ropeLength;
        }
    }

    /// <summary>Shortest the rope can get. Callers use this to tell "all the way up".</summary>
    public float MinRopeLength => minRopeLength;

    /// <summary>How fast the wheel is turning right now, in signed degrees per second.</summary>
    public float TurnSpeed { get; private set; }

    /// <summary>
    /// While true the crank ignores mouse and scroll input. The pour locks it so you
    /// cannot start heading back down in the middle of emptying the bucket.
    /// </summary>
    public bool Locked { get; set; }

    Camera _camera;
    bool _dragging;
    float _lastMouseAngle;
    float _lastWheelAngle;

    void Update()
    {
        // Keep measuring the wheel even while locked, so the creak fades out instead of sticking.
        TrackTurnSpeed();

        var mouse = Mouse.current;
        if (mouse == null || !EnsureCamera())
            return;

        if (Locked)
        {
            _dragging = false;
            return;
        }

        // Where the mouse is, relative to the centre of the wheel.
        Vector2 offset = MouseWorldPosition(mouse) - (Vector2)transform.position;

        // Pressed on the wheel: start dragging.
        if (mouse.leftButton.wasPressedThisFrame && offset.magnitude <= grabRadius)
        {
            _dragging = true;
            _lastMouseAngle = AngleOf(offset);
        }

        // Dragging: however far the mouse swings around the wheel, the wheel turns.
        if (_dragging && mouse.leftButton.isPressed)
        {
            float angle = AngleOf(offset);
            Turn(Mathf.DeltaAngle(_lastMouseAngle, angle));
            _lastMouseAngle = angle;
        }

        if (mouse.leftButton.wasReleasedThisFrame)
            _dragging = false;

        // Fallback: the scroll wheel turns the crank too.
        float notches = mouse.scroll.ReadValue().y / 120f;
        if (Mathf.Abs(notches) > 0.01f)
            Turn(notches * scrollDegreesPerNotch);
    }

    /// <summary>
    /// Turn the wheel by <paramref name="deltaDegrees"/> (counter-clockwise is positive)
    /// and pay out or wind in the matching amount of rope.
    /// Cranking clockwise lets the rope out and sends the bucket down.
    /// </summary>
    void Turn(float deltaDegrees)
    {
        SetRopeLength(RopeLength - deltaDegrees * Mathf.Deg2Rad * drumRadius);
    }

    /// <summary>
    /// Set the rope to a given length, turning the wheel by the matching angle.
    /// Clamped at the short end only. Once the rope is all the way in, the wheel
    /// stops turning rather than spinning on the spot.
    /// </summary>
    public void SetRopeLength(float length)
    {
        float before = RopeLength;
        float after = Mathf.Max(length, minRopeLength);

        float degreesTurned = (before - after) / (drumRadius * Mathf.Deg2Rad);

        transform.localRotation *= Quaternion.Euler(0f, 0f, degreesTurned);
        ropeLength = after;
    }

    /// <summary>Smoothed wheel speed, for anything that reacts to how hard you are cranking.</summary>
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

    Vector2 MouseWorldPosition(Mouse mouse)
    {
        Vector2 screen = mouse.position.ReadValue();

        // Orthographic camera: pass the distance from the camera to the z = 0 plane.
        return _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
    }

    bool EnsureCamera()
    {
        if (_camera == null)
            _camera = Camera.main;
        return _camera != null;
    }
}
