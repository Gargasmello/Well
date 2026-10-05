using UnityEngine;

// Lerps the ground colour and grows the plants as buckets are delivered.
// Reads DeliveredCount from Bucket.
public class GroundGreening : MonoBehaviour
{
    [SerializeField] Bucket bucket;

    [Header("Colour")]
    // Top layer of soil.
    [SerializeField] SpriteRenderer grass;
    [SerializeField] Color grassDry = new Color32(0xC9, 0xB3, 0x5E, 0xFF);
    [SerializeField] Color grassGreen = new Color32(0x5F, 0xA8, 0x4E, 0xFF);

    // Soil underneath.
    [SerializeField] SpriteRenderer dirt;
    [SerializeField] Color dirtDry = new Color32(0xB3, 0x90, 0x62, 0xFF);
    [SerializeField] Color dirtGreen = new Color32(0x8B, 0x6B, 0x4A, 0xFF);

    [Header("Growth")]
    // Buckets of water needed before the ground is completely green.
    [SerializeField] int bucketsToFullGreen = 6;

    // In the order they come up. Each one waits its turn.
    [SerializeField] Transform[] plants;

    // Seconds. The ground soaks one bucket in over soakSeconds, and a single plant
    // grows to full size over growSeconds.
    [SerializeField] float soakSeconds = 1.5f;
    [SerializeField] float growSeconds = 1.2f;

    float _green;          // 0 = bone dry, 1 = fully green
    float[] _plantScale;   // current growth of each plant, 0..1

    void Awake()
    {
        SnapToCurrentState();
    }

    void Update()
    {
        if (bucket == null)
            return;

        _green = Mathf.MoveTowards(_green, TargetGreen(), Time.deltaTime / soakSeconds);

        ApplyColour(_green);
        GrowPlants();
    }

    // Starts from the state the scene was saved in, with no animation.
    void SnapToCurrentState()
    {
        if (_plantScale == null)
            _plantScale = new float[plants == null ? 0 : plants.Length];

        _green = TargetGreen();
        ApplyColour(_green);

        for (int i = 0; i < _plantScale.Length; i++)
        {
            _plantScale[i] = IsDue(i) ? 1f : 0f;
            ApplyPlantScale(i, _plantScale[i]);
        }
    }

    float TargetGreen()
    {
        if (bucket == null || bucketsToFullGreen <= 0)
            return 0f;

        return Mathf.Clamp01(bucket.DeliveredCount / (float)bucketsToFullGreen);
    }

    void ApplyColour(float green)
    {
        if (grass != null)
            grass.color = Color.Lerp(grassDry, grassGreen, green);
        if (dirt != null)
            dirt.color = Color.Lerp(dirtDry, dirtGreen, green);
    }

    // Plants are spread evenly across the greening, so with 10 plants the first comes
    // up at _green = 1/11 and the last at 10/11.
    bool IsDue(int index)
    {
        if (plants == null || plants.Length == 0)
            return false;

        float threshold = (index + 1f) / (plants.Length + 1f);
        return _green >= threshold;
    }

    void GrowPlants()
    {
        if (plants == null || _plantScale == null)
            return;

        for (int i = 0; i < plants.Length; i++)
        {
            float want = IsDue(i) ? 1f : 0f;
            _plantScale[i] = Mathf.MoveTowards(_plantScale[i], want, Time.deltaTime / growSeconds);
            ApplyPlantScale(i, _plantScale[i]);
        }
    }

    void ApplyPlantScale(int index, float scale)
    {
        if (plants[index] == null)
            return;

        // The plant's origin is at ground level, so scaling the root grows it upwards.
        plants[index].localScale = new Vector3(scale, scale, 1f);
        plants[index].gameObject.SetActive(scale > 0.001f);
    }
}
