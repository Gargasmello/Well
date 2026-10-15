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
    // Metres around the crank arm and grip that still counts as being on the crank.
    [SerializeField] float crankMargin = 0.12f;

    // Degrees per scroll-wheel notch.
    [SerializeField] float scrollDegreesPerNotch = 8f;

    // Metres from the hub. Pointer movement is measured at this radius, so the turn rate is the
    // same everywhere: a drag over the hub cannot spin the wheel, and one far from it still turns.
    [SerializeField] float minTurnRadius = 0.35f;
    [SerializeField] float maxTurnRadius = 0.75f;

    // The fist rides the pointer instead of staying on the wheel where it was grabbed.
    [SerializeField] bool handFollowsPointer = true;

    // Hides the OS cursor while the hand covers it.
    [SerializeField] bool hideCursorWhileHeld = true;

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

    // Degrees of wheel rotation one frame of pointer travel can buy. Only a frame that stalled
    // or a drag that jumped can reach it; normal circling is well under.
    const float MaxTurnPerFrame = 90f;

    Camera _camera;
    bool _dragging;
    bool _inDetent;
    bool _onLowStop;
    float _throwFrom;
    float _throwTo;
    float _throwSecondsLeft = -1f;
    float _lastWheelAngle;

    bool Throwing => _throwSecondsLeft >= 0f;

    Hand _hand;
    Vector2 _cursor;
    Vector2 _lastCursor;
    bool _hovering;

    // Where on the wheel the hand was put down when the mouse went down: metres from the hub,
    // and the wheel angle it sat at. Only used when the hand does not follow the pointer.
    float _handRadius;
    float _handAngle;

    // The crank's box in the wheel's own space, measured from CrankArm and CrankGrip.
    float _crankInner;
    float _crankOuter;
    float _crankHalfThickness;

    void Awake()
    {
        // Built at runtime, so the scene keeps its own wheel hierarchy. The opening hint is a
        // scene object under Well, so it can be tuned in the Inspector.
        _hand = new GameObject("Hand").AddComponent<Hand>();
        _hand.transform.SetParent(transform, false);
        MeasureCrank();
    }

    // Both parts are direct children, so their local x is the handle's reach.
    void MeasureCrank()
    {
        Transform arm = transform.Find("CrankArm");
        Transform grip = transform.Find("CrankGrip");

        if (arm == null || grip == null)
        {
            // Matches the scene's CrankArm and CrankGrip, used only if they were renamed.
            _crankInner = 0.36f;
            _crankOuter = 0.79f;
            _crankHalfThickness = 0.07f;
            return;
        }

        _crankInner = arm.localPosition.x - arm.localScale.x * 0.5f;
        _crankOuter = grip.localPosition.x + grip.localScale.x * 0.5f;
        _crankHalfThickness = grip.localScale.y * 0.5f;
    }

    // Pointer in the wheel's own frame, so the crank's box can be tested while the wheel turns.
    bool OnCrank(Vector2 cursor)
    {
        Vector2 local = transform.InverseTransformPoint(cursor);

        return Mathf.Abs(local.y) <= _crankHalfThickness + crankMargin
            && local.x >= _crankInner - crankMargin
            && local.x <= _crankOuter + crankMargin;
    }

    void Update()
    {
        // Keep measuring while locked, so the creak fades out instead of sticking.
        TrackTurnSpeed();

        var mouse = Mouse.current;
        bool tracking = mouse != null && EnsureCamera();

        // Read before the early returns: a throw would otherwise swallow the release and leave
        // the wheel held.
        if (tracking && mouse.leftButton.wasReleasedThisFrame)
            _dragging = false;

        if (Throwing)
        {
            _hovering = false;
            AdvanceThrow();

            // Keep the pointer reference moving through a throw, or its whole travel would be
            // paid back in one frame when the throw ends.
            if (tracking)
                _lastCursor = MouseWorldPosition(mouse);

            return;
        }

        if (!tracking)
        {
            _hovering = false;
            return;
        }

        if (Locked)
        {
            _dragging = false;
            _hovering = false;
            return;
        }

        Vector2 cursor = MouseWorldPosition(mouse);

        // Driven by how far the pointer travels across the wheel, not by where it sits: the turn
        // is that travel along the tangent, at a clamped radius.
        Vector2 from = cursor - (Vector2)transform.position;

        _cursor = cursor;
        _hovering = OnCrank(cursor);

        if (mouse.leftButton.wasPressedThisFrame && _hovering)
        {
            _dragging = true;
            _lastCursor = cursor;
            _handRadius = from.magnitude;
            _handAngle = AngleOf(from) - transform.eulerAngles.z;
        }

        if (_dragging && mouse.leftButton.isPressed)
        {
            float radius = Mathf.Clamp(from.magnitude, minTurnRadius, maxTurnRadius);
            Vector2 tangent = new Vector2(-from.y, from.x).normalized;
            float degrees = Vector2.Dot(cursor - _lastCursor, tangent) / radius * Mathf.Rad2Deg;
            Turn(Mathf.Clamp(degrees, -MaxTurnPerFrame, MaxTurnPerFrame));
        }

        _lastCursor = cursor;

        float notches = mouse.scroll.ReadValue().y / 120f;
        if (Mathf.Abs(notches) > 0.01f)
            Turn(notches * scrollDegreesPerNotch);
    }

    void LateUpdate()
    {
        // Late, so the hand rides a throw instead of stepping ahead of it.
        if (_hand == null)
            return;

        if (_dragging)
        {
            if (handFollowsPointer)
            {
                _hand.Grab(_cursor);
            }
            else
            {
                // Stuck on the wheel: same spot it was put down at, carried round by the turn.
                float angle = (transform.eulerAngles.z + _handAngle) * Mathf.Deg2Rad;
                _hand.Grab((Vector2)transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _handRadius);
            }
        }
        else if (_hovering)
            _hand.Follow(_cursor);
        else
            _hand.Hide();

        // Only hidden while the hand is sitting on the pointer, so the pointer never goes missing.
        bool handOnPointer = _hand.Shown && (!_dragging || handFollowsPointer);
        Cursor.visible = !(hideCursorWhileHeld && handOnPointer);
    }

    void OnDisable()
    {
        if (_hand != null)
            _hand.Hide();

        Cursor.visible = true;
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
