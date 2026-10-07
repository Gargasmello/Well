using UnityEngine;

// Water table depth, fill on contact, and the ready-to-pour flag.
//
//   HasWater        true from the moment it passes the water table until the pour
//   PourPending     true once it is back up and waiting for Pouring
//   FillCount       ++ on fill         -> WellAudio plays the splash
//   HitGroundCount  ++ on dry bottom   -> WellAudio plays the thud
//   PourCount       ++ on ready        -> Pouring starts the animation
//   DeliveredCount  ++ on tipped       -> GroundGreening greens the ground
public class Bucket : MonoBehaviour
{
    [SerializeField] Crank crank;

    [Header("Where the water is")]
    // Metres of rope. Re-picked each descent from the top.
    [SerializeField] float minWaterDepth = 15f;
    [SerializeField] float maxWaterDepth = 30f;

    // 0 = always water, 1 = never.
    [Range(0f, 1f)]
    [SerializeField] float dryChance = 0.5f;

    [Header("Bucket")]
    // Shown while HasWater.
    [SerializeField] Transform water;

    // A descent starting this close to the top counts as a new trip.
    [SerializeField] float newTripWithin = 1f;

    // Must be above Crank's minimum rope length, or the water never comes up.
    [Tooltip("Rope length at which a full bucket is ready to pour.")]
    [SerializeField] float pourAtRopeLength = 1.2f;

    public int FillCount { get; private set; }
    public int PourCount { get; private set; }
    public int DeliveredCount { get; private set; }
    public int HitGroundCount { get; private set; }

    public bool HasWater { get; private set; }
    public bool PourPending { get; private set; }
    public bool WaterExist { get; private set; }

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
            FillCount++;
        }

        // Latched: the well has no bottom, so without this it would fire every frame.
        if (!HasWater && !WaterExist && !_hitGround && ropeLength >= WaterDepth)
        {
            _hitGround = true;
            HitGroundCount++;
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

        PickWaterDepth();
        WaterExist = Random.value >= dryChance;

        // Water or bottom, the crank starts to fight back from here.
        crank.StrainFrom = WaterDepth;

        if (water != null)
            water.gameObject.SetActive(false);
    }

    // Called by Pouring part way through the animation.
    public void FinishPour()
    {
        if (!PourPending)
            return;

        HasWater = false;
        PourPending = false;
        DeliveredCount++;

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
