using UnityEngine;
using UnityEngine.InputSystem;

// Winch crank. Owns the rope length; Rope and Pouring read it, Pouring writes Locked.
public class Crank : MonoBehaviour
{
    [Header("Drum")]
    // Metres of rope per radian. 0.3 is about 1.9 m per full turn.
    [Tooltip("Metres of rope released per radian of wheel rotation.")]
    [SerializeField] float drumRadius = 0.3f;

    // Metres. No maximum of its own, but RopeLimit caps it.
    [SerializeField] float minRopeLength = 0.8f;

    [Header("Feel")]
    // How close the mouse has to be to the wheel centre to grab it.
    [SerializeField] float grabRadius = 0.8f;

    // Degrees per scroll-wheel notch.
    [SerializeField] float scrollDegreesPerNotch = 8f;

    [Header("Detent")]
    // Seconds to travel between the two stops. The throw runs to completion, so the player
    // cannot leave the rope hanging between them.
    [Tooltip("Seconds for the crank to throw between its two stops.")]
    [SerializeField] float throwSeconds = 0.12f;

    // Negative until first read; the getter replaces it with minRopeLength.
    [SerializeField] float ropeLength = -1f;

    float _ropeLimit = float.PositiveInfinity;
    float _wound = -1f;

    // Metres between the two stops the crank snaps between once the rope reaches its limit.
    // Zero means no detent. Bucket sets this at the start of each trip.
    public float DetentTravel { get; set; }

    // Rope length the crank will not go past. Bucket lowers it to the water table each trip.
    public float RopeLimit
    {
        get => _ropeLimit;
        set
        {
            _ropeLimit = value;
            _wound = -1f;
            _inDetent = false;
            _onLowStop = false;
            _throwSecondsLeft = -1f;
        }
    }

    // ++ every time it settles on a stop. WellAudio watches this.
    public int ThrowCount { get; private set; }

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
    bool _inDetent;
    bool _onLowStop;
    float _throwFrom;
    float _throwTo;
    float _throwSecondsLeft = -1f;
    float _lastMouseAngle;
    float _lastWheelAngle;

    bool Throwing => _throwSecondsLeft >= 0f;

    void Update()
    {
        // Keep measuring while locked, so the creak fades out instead of sticking.
        TrackTurnSpeed();

        if (Throwing)
        {
            AdvanceThrow();
            return;
        }

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
        Move(deltaDegrees * Mathf.Deg2Rad * drumRadius);
    }

    public void SetRopeLength(float length)
    {
        if (!Throwing)
            Apply(length);
    }

    // Where the player has wound to. Inside the detent this keeps moving while the rope stays
    // held on a stop, which is what lets the midpoint be crossed and the switch thrown.
    float Wound
    {
        get
        {
            if (_wound < 0f)
                _wound = RopeLength;
            return _wound;
        }
    }

    // Positive ropeDelta pays rope out. The wheel angle comes from how far the rope actually
    // moved, so a clamped rope stops the wheel dead instead of it spinning on the spot.
    void Move(float ropeDelta)
    {
        float after = Mathf.Clamp(Wound - ropeDelta, minRopeLength, RopeLimit);
        _wound = after;

        float low = RopeLimit;
        float high = RopeLimit - DetentTravel;

        if (DetentTravel <= 0f || after < high)
        {
            // Clear of the detent. The next entry lands on the upper stop.
            _inDetent = false;
            _onLowStop = false;
            Apply(after);
            return;
        }

        // Inside the detent the rope only exists at the two stops. Crossing the midpoint
        // commits to the far one, and the travel between them is never left half done.
        bool wantLow = after > (low + high) * 0.5f;

        if (!_inDetent || wantLow != _onLowStop)
        {
            _inDetent = true;
            GoTo(wantLow, RopeLength);
            return;
        }

        Apply(wantLow ? low : high);
    }

    void GoTo(bool lowStop, float from)
    {
        _onLowStop = lowStop;
        ThrowCount++;

        _throwTo = lowStop ? RopeLimit : RopeLimit - DetentTravel;

        // Already sitting on that stop, so there is nothing to travel - just the click.
        if (Mathf.Abs(_throwTo - from) < 0.0001f)
            return;

        _throwFrom = from;
        _throwSecondsLeft = 0f;
    }

    void AdvanceThrow()
    {
        _throwSecondsLeft += Time.deltaTime;

        if (_throwSecondsLeft >= throwSeconds)
        {
            Apply(_throwTo);
            _throwSecondsLeft = -1f;
            return;
        }

        float k = Mathf.SmoothStep(0f, 1f, _throwSecondsLeft / Mathf.Max(throwSeconds, 0.0001f));
        Apply(Mathf.Lerp(_throwFrom, _throwTo, k));
    }

    void Apply(float length)
    {
        float before = RopeLength;
        float after = Mathf.Clamp(length, minRopeLength, RopeLimit);

        ropeLength = after;

        if (!_inDetent)
            _wound = after;

        transform.localRotation *= Quaternion.Euler(0f, 0f, (before - after) / (drumRadius * Mathf.Deg2Rad));
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
