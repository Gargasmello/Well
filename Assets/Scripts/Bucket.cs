using UnityEngine;

// Water table depth, fill on contact, and the ready-to-pour flag.
//
//   HasWater        true from the moment it passes the water table until the pour
//   PourPending     true once it is back up and waiting for Pouring
//   SplashCount     ++ crossing the surface either way -> WellAudio plays the splash
//   HitGroundCount  ++ on the first dry bottom         -> Weather counts dry trips
//   PourCount       ++ on ready                        -> Pouring starts the animation
//   DeliveredCount  ++ on tipped                       -> GroundGreening greens the ground
public class Bucket : MonoBehaviour
{
    [SerializeField] Crank crank;

    [Header("Where the water is")]
    // Metres of rope. Re-picked each descent from the top.
    [SerializeField] float minWaterDepth = 15f;
    [SerializeField] float maxWaterDepth = 30f;

    // Base chance a trip comes up dry. The ramps scale with it, so the limits hold either way.
    [Range(0f, 1f)]
    [SerializeField] float dryChance = 0.5f;

    // Trips of dry before water is certain, and of wet before dry is certain.
    [SerializeField] int dryRamp = 3;
    [SerializeField] int wetRamp = 3;

    [Header("Bucket")]
    // Shown while HasWater.
    [SerializeField] Transform water;

    // A descent starting this close to the top counts as a new trip.
    [SerializeField] float newTripWithin = 1f;

    // Metres between the two stops the crank snaps between once a dry trip reaches the bottom.
    // Zero means it just locks there, with no detent to rock.
    [SerializeField] float bottomDetentTravel = 0.15f;

    // Metres the bucket sinks below the surface before the crank locks on a wet trip.
    [SerializeField] float sinkDepth = 2f;

    // Must be above Crank's minimum rope length, or the water never comes up.
    [Tooltip("Rope length at which a full bucket is ready to pour.")]
    [SerializeField] float pourAtRopeLength = 1.2f;

    public int PourCount { get; private set; }
    public int DeliveredCount { get; private set; }
    public int HitGroundCount { get; private set; }

    // ++ every time the bucket crosses the water surface, either way. WellAudio watches this.
    public int SplashCount { get; private set; }

    public int TripsStarted => _trips;
    public int LastDeliveredTrip { get; private set; }

    public bool HasWater { get; private set; }
    public bool PourPending { get; private set; }
    public bool WaterExist { get; private set; }

    public void ForceDryTrips(int count)
    {
        _forcedDryTripsRemaining = Mathf.Max(_forcedDryTripsRemaining, count);

        if (_trips > 0 && !_hitGround && !HasWater)
        {
            _currentTripForcedDry = true;
            WaterExist = false;
        }
    }

    // Picked lazily so the scene does not have to store one.
    public float WaterDepth
    {
        get
        {
            if (_waterDepth <= 0f)
                PickWaterDepth();
            return _waterDepth;
        }
    }

    // Read by WellAudio to scale the splash and the thud.
    public float MaxWaterDepth => maxWaterDepth;

    float _waterDepth;
    float _lastLength;
    bool _wasGoingDown;
    bool _hitGround;
    bool _underWater;
    bool _firstDryComplete;
    bool _openingWetDone;
    bool _currentTripForcedDry;
    int _trips;
    int _dryStreak;
    int _wetStreak;
    int _forcedDryTripsRemaining;

    // Chance this trip has water. The step follows dryChance, so it is never fixed.
    float WaterChance
    {
        get
        {
            float baseChance = 1f - dryChance;

            return Mathf.Clamp01(
                baseChance
                + dryChance  * _dryStreak / dryRamp
                - baseChance * _wetStreak / wetRamp);
        }
    }

    void Update()
    {
        if (crank == null)
            return;

        float length = crank.RopeLength;
        bool goingDown = length > _lastLength + 0.0001f;
        bool startingDown = goingDown && !_wasGoingDown;
        bool fromTheTop = length <= crank.MinRopeLength + newTripWithin;

        if (startingDown && fromTheTop && !HasWater)
            StartNewTrip();

        _wasGoingDown = goingDown;
        _lastLength = length;

        Refresh(length);
    }

    void Refresh(float ropeLength)
    {
        if (!HasWater && WaterExist && ropeLength >= WaterDepth)
        {
            HasWater = true;
            SplashCount++;
            _wetStreak++;
            _dryStreak = 0;
            _underWater = true;
        }

        // Winding back up breaks the surface again, which splashes just like going in did.
        if (_underWater && ropeLength < WaterDepth)
        {
            _underWater = false;
            SplashCount++;
        }

        // The crank is locked at the water table on a dry trip and has a detent to rock in, so
        // reaching the top of that travel is what counts as the bottom. Once per trip; every
        // hit after that is the detent's business, not this.
        if (!HasWater && !WaterExist && !_hitGround &&
            ropeLength >= WaterDepth - bottomDetentTravel)
        {
            _hitGround = true;
            HitGroundCount++;
            _dryStreak++;
            _wetStreak = 0;
            _firstDryComplete = true;

            if (_currentTripForcedDry)
            {
                _forcedDryTripsRemaining--;
                _currentTripForcedDry = false;
            }
        }

        // Ready to pour, but not emptied - the pour takes the water.
        if (HasWater && !PourPending && ropeLength <= pourAtRopeLength)
        {
            PourPending = true;
            PourCount++;
        }

        if (water != null)
            water.gameObject.SetActive(HasWater);
    }

    void StartNewTrip()
    {
        HasWater = false;
        PourPending = false;
        _hitGround = false;
        _underWater = false;
        _trips++;
        _currentTripForcedDry = _forcedDryTripsRemaining > 0;

        PickWaterDepth();
        PickWaterExist();

        // A wet trip sinks a little way below the surface before the crank locks; a dry one
        // stops at the bottom and gets a detent to rock in.
        crank.RopeLimit = WaterDepth + (WaterExist ? sinkDepth : 0f);
        crank.DetentTravel = WaterExist ? 0f : bottomDetentTravel;

        if (water != null)
            water.gameObject.SetActive(false);
    }

    // The opening two descents are scripted: the first finds nothing, the one after it always
    // finds water. That puts the discovery on the second trip instead of leaving it to the
    // odds. Everything after that goes through the ramps.
    void PickWaterExist()
    {
        if (!_firstDryComplete || _currentTripForcedDry)
            WaterExist = false;
        else if (!_openingWetDone)
        {
            _openingWetDone = true;
            WaterExist = true;
        }
        else if (_dryStreak >= dryRamp)
            WaterExist = true;
        else if (_wetStreak >= wetRamp)
            WaterExist = false;
        else
            WaterExist = Random.value < WaterChance;
    }

    // Called by Pouring part way through the animation.
    public void FinishPour()
    {
        if (!PourPending)
            return;

        HasWater = false;
        PourPending = false;
        DeliveredCount++;
        LastDeliveredTrip = _trips;

        if (water != null)
            water.gameObject.SetActive(false);
    }

    void PickWaterDepth()
    {
        float previous = _waterDepth;
        float span = maxWaterDepth - minWaterDepth;

        // Re-roll if it lands near the last one; two similar trips read as "not random".
        for (int attempt = 0; attempt < 8; attempt++)
        {
            _waterDepth = Random.Range(minWaterDepth, maxWaterDepth);

            if (previous <= 0f || Mathf.Abs(_waterDepth - previous) >= span * 0.25f)
                return;
        }
    }
}
