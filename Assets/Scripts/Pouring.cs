using UnityEngine;

/// <summary>
/// Emptying the bucket.
///
/// A full bucket cannot simply be tipped where it hangs: it comes up directly over
/// the well, so anything poured out would fall straight back down the shaft. So the
/// bucket first swings out clear of the well wall, then tips, then swings back.
///
/// The crank is locked for the whole thing, so you cannot start heading back down
/// with the bucket half emptied.
/// </summary>
public class Pouring : MonoBehaviour
{
    [Tooltip("The crank. It gets locked while the pour plays.")]
    [SerializeField] Crank crank;

    [Tooltip("The bucket's state. The pour is triggered by the bucket coming back up full.")]
    [SerializeField] Bucket bucket;

    [Tooltip("The rope. The bucket swings on the end of it.")]
    [SerializeField] Rope rope;

    [Tooltip("The bucket itself, so it can be tipped over.")]
    [SerializeField] Transform bucketTransform;

    [Tooltip("The watering sound.")]
    [SerializeField] AudioSource pour;

    [Tooltip("The falling water, shown while the bucket is tipped. Starts hidden.")]
    [SerializeField] Transform pourStream;

    [Header("Swing")]
    [Tooltip("How far to the side the bucket swings before it tips, in metres. Has to be at " +
             "least the well's half width plus the bucket's own length, or the bucket ends " +
             "up half hidden behind the well wall.")]
    [SerializeField] float swingOutTo = 2.55f;

    [Tooltip("Rope paid out during the swing. Leave it equal to the length the bucket comes " +
             "up at to swing the bucket sideways at the same height; lower values make it " +
             "rise, higher values drop it towards the ground.")]
    [SerializeField] float payOutTo = 1.2f;

    [Tooltip("How far the bucket tips, in degrees. Enough that the low side of the rim goes " +
             "under the water, without turning the bucket into an unreadable box.")]
    [SerializeField] float tipDegrees = -55f;

    [Tooltip("Height of the ground the water lands on.")]
    [SerializeField] float groundY = -2.8f;

    [Tooltip("Where the low side of the rim sits in the bucket's own space. Used to work out " +
             "where the water actually comes over the edge once the bucket is tipped.")]
    [SerializeField] float rimHalfWidth = 0.46f;
    [SerializeField] float rimOffsetY = -0.36f;

    [Tooltip("How thick the falling water is.")]
    [SerializeField] float streamWidth = 0.16f;

    [Header("Timing")]
    [Tooltip("Seconds the whole pour takes. The crank is locked for this long, which is " +
             "deliberately shorter than the sound so the tail plays while you crank again.")]
    [SerializeField] float duration = 1.5f;

    // Where each stage of the pour sits on the 0..1 timeline.
    const float OutEnd  = 0.30f;
    const float TipEnd  = 0.50f;
    const float HoldEnd = 0.67f;
    const float EmptyAt = 0.55f;

    int _lastPourCount;
    float _elapsed = -1f;
    float _ropeAtStart;
    bool _emptied;

    /// <summary>How long the whole pour takes.</summary>
    public float Duration => duration;

    void Update()
    {
        if (crank == null || bucket == null)
            return;

        if (_elapsed < 0f && bucket.PourCount != _lastPourCount)
        {
            _lastPourCount = bucket.PourCount;
            Begin();
        }

        if (_elapsed < 0f)
            return;

        _elapsed += Time.deltaTime;
        ApplyPose(_elapsed);

        if (_elapsed >= duration)
            Finish();
    }

    void Begin()
    {
        _elapsed = 0f;
        _emptied = false;
        _ropeAtStart = crank.RopeLength;
        crank.Locked = true;
    }

    void Finish()
    {
        _elapsed = -1f;
        crank.Locked = false;

        ApplyPose(0f);
        crank.SetRopeLength(_ropeAtStart);

        if (rope != null)
            rope.Refresh();
    }

    /// <summary>
    /// Pose the pour at a given moment without advancing it. Used by the previews,
    /// which cannot run the animation. Everything else - including the bucket actually
    /// emptying - happens exactly as it would in play.
    /// </summary>
    public void PoseAt(float seconds)
    {
        if (_ropeAtStart <= 0f && crank != null)
            _ropeAtStart = crank.RopeLength;

        ApplyPose(seconds);
    }

    /// <summary>Put the bucket back where it simply hangs: no swing, no tip, no stream.</summary>
    public void ResetPose()
    {
        if (rope != null)
        {
            rope.SideSwing = 0f;
            rope.Refresh();
        }

        if (bucketTransform != null)
            bucketTransform.localRotation = Quaternion.identity;

        if (pourStream != null)
            pourStream.gameObject.SetActive(false);
    }

    void ApplyPose(float seconds)
    {
        float p = Mathf.Clamp01(seconds / duration);

        float swing, tip, length;

        if (p < OutEnd)
        {
            float k = Smooth(p / OutEnd);
            swing  = Mathf.Lerp(0f, swingOutTo, k);
            length = Mathf.Lerp(_ropeAtStart, payOutTo, k);
            tip    = 0f;
        }
        else if (p < TipEnd)
        {
            float k = Smooth((p - OutEnd) / (TipEnd - OutEnd));
            swing  = swingOutTo;
            length = payOutTo;
            tip    = Mathf.Lerp(0f, tipDegrees, k);
        }
        else if (p < HoldEnd)
        {
            swing  = swingOutTo;
            length = payOutTo;
            tip    = tipDegrees;
        }
        else
        {
            float k = Smooth((p - HoldEnd) / (1f - HoldEnd));
            swing  = Mathf.Lerp(swingOutTo, 0f, k);
            length = Mathf.Lerp(payOutTo, _ropeAtStart, k);
            tip    = Mathf.Lerp(tipDegrees, 0f, k);
        }

        if (rope != null)
        {
            rope.SideSwing = swing;
            crank.SetRopeLength(length);
            rope.Refresh();
        }

        if (bucketTransform != null)
            bucketTransform.localRotation = Quaternion.Euler(0f, 0f, tip);

        // The water leaves the bucket part way through the hold, once it is tipped over.
        if (!_emptied && p >= EmptyAt)
        {
            _emptied = true;
            bucket.FinishPour();

            // Only actually make a sound in play mode; the editor poses this too.
            if (pour != null && Application.isPlaying)
                pour.Play();
        }

        UpdateStream(p, swing, length, tip);
    }

    /// <summary>
    /// Show the water falling from the tipped bucket down to the ground. This is the
    /// whole point of swinging the bucket out: poured where it hangs, the water would
    /// drop straight back down the well.
    /// </summary>
    void UpdateStream(float p, float swing, float ropeLength, float tip)
    {
        if (pourStream == null)
            return;

        bool falling = p >= EmptyAt && p < HoldEnd;
        pourStream.gameObject.SetActive(falling);

        if (!falling)
            return;

        // Where the low side of the rim ends up once the bucket is tipped. That is the
        // edge the water comes over, so it is where the stream has to hang from.
        float rad = tip * Mathf.Deg2Rad;
        float lipX = rimHalfWidth * Mathf.Cos(rad) - rimOffsetY * Mathf.Sin(rad);
        float lipY = rimHalfWidth * Mathf.Sin(rad) + rimOffsetY * Mathf.Cos(rad);

        float apexY = transform.InverseTransformPoint(crank.transform.position).y - ropeLength;
        float topY = apexY + lipY;
        float height = Mathf.Max(topY - groundY, 0.05f);

        pourStream.localPosition = new Vector3(swing + lipX, (topY + groundY) * 0.5f, 0f);
        pourStream.localScale = new Vector3(streamWidth, height, 1f);
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);

    /// <summary>Used by the scene builder to hook the references up.</summary>
    public void Bind(Crank crank, Bucket bucket, Rope rope, Transform bucketTransform,
                     AudioSource pour, Transform pourStream)
    {
        this.crank = crank;
        this.bucket = bucket;
        this.rope = rope;
        this.bucketTransform = bucketTransform;
        this.pour = pour;
        this.pourStream = pourStream;
    }
}
