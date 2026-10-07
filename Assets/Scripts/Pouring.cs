using UnityEngine;

// Swings the bucket clear of the well, tips it, then puts it back. Locks the crank.
// Started by Bucket.PourCount; drives Rope.SideSwing and Crank.SetRopeLength.
public class Pouring : MonoBehaviour
{
    [SerializeField] Crank crank;
    [SerializeField] Bucket bucket;
    [SerializeField] Rope rope;

    // Tipped over during the pour.
    [SerializeField] Transform bucketTransform;

    [SerializeField] AudioSource pour;

    // Shown while the bucket is tipped. Starts hidden.
    [SerializeField] Transform pourStream;

    [Header("Swing")]
    // Must clear the well's half width plus the bucket's length, or the bucket clips.
    [Tooltip("How far to the side the bucket swings before it tips, in metres.")]
    [SerializeField] float swingOutTo = 2.55f;

    // Equal to the pour height swings level; higher drops the bucket.
    [SerializeField] float payOutTo = 1.2f;

    // Degrees. Past 90 and the bucket reads as an unreadable box.
    [SerializeField] float tipDegrees = -55f;

    // Height of the ground the water lands on.
    [SerializeField] float groundY = -2.8f;

    // Duplicate the bucket's rim in the scene: resize the bucket and these must follow.
    [SerializeField] float rimHalfWidth = 0.46f;
    [SerializeField] float rimOffsetY = -0.36f;

    [SerializeField] float streamWidth = 0.16f;

    [Header("Timing")]
    // Seconds. Shorter than the watering sound, so its tail plays while you crank again.
    [SerializeField] float duration = 1.5f;

    // Fractions of duration. Order matters: OutEnd < TipEnd < EmptyAt < HoldEnd.
    const float OutEnd  = 0.30f;
    const float TipEnd  = 0.50f;
    const float HoldEnd = 0.67f;
    const float EmptyAt = 0.55f;

    int _lastPourCount;
    float _elapsed = -1f;
    float _ropeAtStart;
    bool _emptied;

    void Update()
    {
        if (crank == null || bucket == null)
            return;

        // PourCount is a running total; a change means the bucket is back up and full.
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

    // Out, tip, hold, back. Pose is a pure function of elapsed time, so it cannot drift.
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

        // The water leaves part way through the hold, once the bucket is tipped over.
        if (!_emptied && p >= EmptyAt)
        {
            _emptied = true;
            bucket.FinishPour();

            // Guarded so posing the animation in the editor stays silent.
            if (pour != null && Application.isPlaying)
                pour.Play();
        }

        UpdateStream(p, swing, length, tip);
    }

    // Falls from the low side of the tipped rim to the ground.
    void UpdateStream(float p, float swing, float ropeLength, float tip)
    {
        if (pourStream == null)
            return;

        bool falling = p >= EmptyAt && p < HoldEnd;
        pourStream.gameObject.SetActive(falling);

        if (!falling)
            return;

        // The rim edge the water comes over.
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
}
