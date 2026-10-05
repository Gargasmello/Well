using UnityEngine;

/// <summary>
/// The bucket.
///
/// Each trip down it picks a fresh depth for the water table. The moment it goes
/// below that depth it fills, and it stays full until it is wound back up out of
/// the well - at which point it is ready to be poured, and Pouring takes it from
/// there. The bucket only empties once the water has actually been tipped out.
///
/// Nothing about success is random: crank far enough and you always reach water.
/// The only uncertainty is how far that is, which is what makes the splash worth
/// listening for.
/// </summary>
public class Bucket : MonoBehaviour
{
    [Tooltip("The crank. Rope length is read from it.")]
    [SerializeField] Crank crank;

    [Header("Where the water is")]
    [Tooltip("Shallowest the water table can be, measured as metres of rope paid out.")]
    [SerializeField] float minWaterDepth = 15f;

    [Tooltip("Deepest the water table can be, measured as metres of rope paid out.")]
    [SerializeField] float maxWaterDepth = 30f;

    [Header("Bucket")]
    [Tooltip("Shown while the bucket is carrying water.")]
    [SerializeField] Transform water;

    [Tooltip("A descent that starts with the rope less than this far above its shortest " +
             "length counts as a new trip, and gets a fresh water table.")]
    [SerializeField] float newTripWithin = 1f;

    [Tooltip("Wind the rope shorter than this and a full bucket is ready to pour. Must be " +
             "above the crank's minimum rope length, or the water never comes up.")]
    [SerializeField] float pourAtRopeLength = 1.2f;

    /// <summary>How many times the bucket has reached water. WellAudio watches this.</summary>
    public int FillCount { get; private set; }

    /// <summary>How many times a full bucket has been brought back up. Pouring watches this.</summary>
    public int PourCount { get; private set; }

    /// <summary>How many buckets have actually been tipped out. GroundGreening watches this.</summary>
    public int DeliveredCount { get; private set; }

    /// <summary>True while the bucket is carrying water.</summary>
    public bool HasWater { get; private set; }

    /// <summary>True once the bucket is back up and waiting to be poured.</summary>
    public bool PourPending { get; private set; }

    /// <summary>Depth of the water table for the current trip, in metres of rope.</summary>
    public float WaterDepth
    {
        get
        {
            if (_waterDepth <= 0f)
                PickWaterDepth();
            return _waterDepth;
        }
    }

    /// <summary>Shallowest the water table can be.</summary>
    public float MinWaterDepth => minWaterDepth;

    /// <summary>Deepest the water table can be.</summary>
    public float MaxWaterDepth => maxWaterDepth;

    /// <summary>Rope length at which a full bucket becomes ready to pour.</summary>
    public float PourAtRopeLength => pourAtRopeLength;

    float _waterDepth;
    float _lastLength;
    bool _wasGoingDown;

    void Update()
    {
        if (crank == null)
            return;

        float length = crank.RopeLength;
        bool goingDown = length > _lastLength + 0.0001f;
        bool startingDown = goingDown && !_wasGoingDown;
        bool fromTheTop = length <= crank.MinRopeLength + newTripWithin;

        // A fresh descent from the top is a fresh trip, so it gets a fresh water table.
        // Tying this to the delivery instead meant the depth only ever changed if the
        // player wound the rope all the way in - which in practice they never did,
        // because the bucket is already fully out of the well before that point. Every
        // descent then hit water at the same distance, which reads as "not random".
        if (startingDown && fromTheTop && !HasWater)
            StartNewTrip();

        _wasGoingDown = goingDown;
        _lastLength = length;

        Refresh(length);
    }

    /// <summary>
    /// Work out the bucket's state for a given rope length.
    /// Called every frame; the scene builder and the previews call it directly too.
    /// </summary>
    public void Refresh(float ropeLength)
    {
        // Down past the water table: the bucket fills.
        if (!HasWater && ropeLength >= WaterDepth)
        {
            HasWater = true;
            FillCount++;
        }

        // Wound back up out of the well: ready to pour, but not emptied yet.
        if (HasWater && !PourPending && ropeLength <= pourAtRopeLength)
        {
            PourPending = true;
            PourCount++;
        }

        if (water != null)
            water.gameObject.SetActive(HasWater);
    }

    /// <summary>Start a fresh trip: a new water table, and an empty bucket.</summary>
    public void StartNewTrip()
    {
        HasWater = false;
        PourPending = false;
        PickWaterDepth();

        if (water != null)
            water.gameObject.SetActive(false);
    }

    /// <summary>The water has been tipped out. Called by Pouring at the end of the pour.</summary>
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

    /// <summary>Force the water table to a fixed depth, for the scene builder and previews.</summary>
    public void SetWaterDepth(float depth)
    {
        _waterDepth = Mathf.Clamp(depth, minWaterDepth, maxWaterDepth);
    }

    /// <summary>Used by the scene builder to hook the references up.</summary>
    public void Bind(Crank crank, Transform water)
    {
        this.crank = crank;
        this.water = water;
    }

    void PickWaterDepth()
    {
        float previous = _waterDepth;
        float span = maxWaterDepth - minWaterDepth;

        // Plain uniform random clumps. Two trips in a row landing within a few metres
        // of each other read as "it isn't random at all", so re-roll until the new
        // depth is clearly different from the last one.
        for (int attempt = 0; attempt < 8; attempt++)
        {
            _waterDepth = Random.Range(minWaterDepth, maxWaterDepth);

            if (previous <= 0f || Mathf.Abs(_waterDepth - previous) >= span * 0.25f)
                return;
        }
    }
}
