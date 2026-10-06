using UnityEngine;

// Water table depth, fill on contact, and the ready-to-pour flag.
//
//   HasWater        true from the moment it passes the water table until the pour
//   PourPending     true once it is back up and waiting for Pouring
//   FillCount       ++ on fill      -> WellAudio plays the splash
//   PourCount       ++ on ready     -> Pouring starts the animation
//   DeliveredCount  ++ on tipped    -> GroundGreening greens the ground
//
// There is no failure case: going past the water table always fills the bucket.
public class Bucket : MonoBehaviour
{
    [SerializeField] Crank crank;

    [Header("Where the water is")]
    // Both in metres of rope paid out. Re-picked on every descent from the top.
    [SerializeField] float minWaterDepth = 15f;
    [SerializeField] float maxWaterDepth = 30f;

    [Header("Bucket")]
    // Shown while HasWater.
    [SerializeField] Transform water;

    // A descent that starts with the rope less than this far above its shortest
    // length counts as a new trip, and gets a fresh water table.
    [SerializeField] float newTripWithin = 1f;

    // Has to be above Crank's minimum rope length, or the water never comes up.
    [Tooltip("Rope length at which a full bucket is ready to pour.")]
    [SerializeField] float pourAtRopeLength = 1.2f;

    public int FillCount { get; private set; }
    public int PourCount { get; private set; }
    public int DeliveredCount { get; private set; }

    public int HitGroundCount { get; private set; }

    public bool HasWater { get; private set; }
    public bool PourPending { get; private set; }

    public bool WaterExist = false;

    // Metres of rope. Picked lazily so the scene does not have to store one.
    public float WaterDepth
    {
        get
        {
            if (_waterDepth <= 0f)
                PickWaterDepth();
            return _waterDepth;
        }
    }

    // Read by WellAudio to scale the splash.
    public float MaxWaterDepth => maxWaterDepth;

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

        // A fresh descent from the top is a fresh trip. Rolling the depth on delivery
        // instead meant it only changed if the player wound the rope all the way in,
        // which they never do, so every descent hit water at the same distance.
        if (startingDown && fromTheTop && !HasWater)
            StartNewTrip();

        _wasGoingDown = goingDown;
        _lastLength = length;

        Refresh(length);
    }

    void Refresh(float ropeLength)
    {
        if (!HasWater && ropeLength >= WaterDepth && WaterExist)
        {
            HasWater = true;
            FillCount++;
        }

        if (!HasWater && ropeLength >= WaterDepth && !WaterExist)
        {
            
            HitGroundCount++;
        }

        // Ready to pour, but not emptied: the pour takes the water, not this.
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
        PickWaterDepth();
        RandomWater();

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

        // Uniform random clumps, and two trips landing within a few metres of each
        // other read as "not random at all", so re-roll until the new depth is clearly
        // different from the last one.
        for (int attempt = 0; attempt < 8; attempt++)
        {
            _waterDepth = Random.Range(minWaterDepth, maxWaterDepth);

            if (previous <= 0f || Mathf.Abs(_waterDepth - previous) >= span * 0.25f)
                return;
        }
    }

    private void RandomWater()
    {
        int rand = Random.Range(0, 8);
        WaterExist = false;
        switch (rand)
        {
            case 0:
                WaterExist = false; 
                
                break;
            case 1:
                WaterExist = false;

                break;
            case 2:
                WaterExist = false;

                break;
            case 3:
                WaterExist = false;

                break;
            case 4:
                WaterExist = false;

                break;
            case 5:
                WaterExist = false;

                break;
            case 6:
                WaterExist = true;

                break;
            case 7:
                WaterExist = true;

                break;
            case 8:
                WaterExist = true;

                break;
        }
    }
}
