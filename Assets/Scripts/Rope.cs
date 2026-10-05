using UnityEngine;

// Stretches the rope block between the axle and the bucket, and hangs the bucket off
// the end of it. Reads the rope length from Crank, plus SideSwing, which Pouring
// writes while it swings the bucket out.
public class Rope : MonoBehaviour
{
    [SerializeField] Crank crank;

    // Metres.
    [SerializeField] float ropeWidth = 0.08f;

    [SerializeField] Transform ropeBlock;

    // The bucket. Its origin is the end of the rope, at the top of the handle.
    [SerializeField] Transform bucket;

    // Metres sideways. Normally zero; the pour animates it.
    public float SideSwing { get; set; }

    void Update()
    {
        Refresh();
    }

    // Draws the rope as a line from the axle to the bucket, so a swung bucket gets a
    // taut diagonal rope instead of a vertical one that misses it. The block's +Y has
    // to point at the axle, hence the -90 on the angle.
    public void Refresh()
    {
        if (crank == null || ropeBlock == null || bucket == null)
            return;

        Vector3 axle = transform.InverseTransformPoint(crank.transform.position);
        Vector3 end = new Vector3(SideSwing, axle.y - crank.RopeLength, 0f);

        Vector3 up = axle - end;
        float angle = Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg - 90f;

        ropeBlock.localPosition = (axle + end) * 0.5f;
        ropeBlock.localScale = new Vector3(ropeWidth, up.magnitude, 1f);
        ropeBlock.localRotation = Quaternion.Euler(0f, 0f, angle);

        bucket.localPosition = end;
    }
}
