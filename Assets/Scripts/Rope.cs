using UnityEngine;

/// <summary>
/// The rope and the bucket's hanging point.
///
/// Does one thing: given how much rope the crank has paid out, and how far the
/// bucket has swung out to one side, stretch the rope block between the axle and
/// the bucket and hang the bucket off the end of it.
///
/// The bucket drops out of sight behind the well wall once it goes below the rim.
/// That is deliberate - the well is deep and you are not meant to see the bottom.
/// </summary>
public class Rope : MonoBehaviour
{
    [Tooltip("The crank. Rope length is read from it, and the top of the rope is " +
             "pinned to wherever the crank sits.")]
    [SerializeField] Crank crank;

    [Tooltip("How thick the rope is.")]
    [SerializeField] float ropeWidth = 0.08f;

    [Tooltip("The rope block.")]
    [SerializeField] Transform ropeBlock;

    [Tooltip("The bucket. Its origin is the end of the rope, at the top of the handle.")]
    [SerializeField] Transform bucket;

    /// <summary>
    /// How far the bucket has swung out sideways, in metres. Normally zero; the pour
    /// animates it so the bucket can be swung clear of the well before it tips.
    /// </summary>
    public float SideSwing { get; set; }

    void Update()
    {
        Refresh();
    }

    /// <summary>Put the rope and the bucket where the current length and swing say they go.</summary>
    public void Refresh()
    {
        if (crank == null || ropeBlock == null || bucket == null)
            return;

        // The top of the rope is pinned to the axle, wherever the crank is.
        Vector3 axle = transform.InverseTransformPoint(crank.transform.position);
        Vector3 end = new Vector3(SideSwing, axle.y - crank.RopeLength, 0f);

        // The rope stretches from the axle down to the bucket, so a swung bucket
        // gets a taut diagonal rope rather than a vertical one that misses it.
        Vector3 up = axle - end;
        float angle = Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg - 90f;

        ropeBlock.localPosition = (axle + end) * 0.5f;
        ropeBlock.localScale = new Vector3(ropeWidth, up.magnitude, 1f);
        ropeBlock.localRotation = Quaternion.Euler(0f, 0f, angle);

        bucket.localPosition = end;
    }

    /// <summary>Used by the scene builder to hook the references up.</summary>
    public void Bind(Crank crank, Transform ropeBlock, Transform bucket)
    {
        this.crank = crank;
        this.ropeBlock = ropeBlock;
        this.bucket = bucket;
    }
}
