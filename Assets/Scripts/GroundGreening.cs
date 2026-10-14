using System.Collections.Generic;
using UnityEngine;

// Lerps the ground, sky and cloud colour, grows the plants and the tree, and shifts the green
// when the weather takes a bucket back.
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

    // The camera, whose background is the sky. Hazy and warm while the ground is dry.
    [SerializeField] Camera sky;
    [SerializeField] Color skyDry = new Color32(0xC2, 0xBC, 0x9C, 0xFF);
    [SerializeField] Color skyClear = new Color32(0x87, 0xC5, 0xE8, 0xFF);

    // Cloud groups. Only the groups are referenced; the blocks inside carry the colour.
    [SerializeField] Transform[] clouds;
    [SerializeField] Color cloudDry = new Color32(0xD7, 0xD3, 0xBE, 0xFF);
    [SerializeField] Color cloudClear = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

    [Header("Growth")]
    // Buckets of water needed before the ground is completely green.
    [SerializeField] int bucketsToFullGreen = 6;

    // In the order they come up.
    [SerializeField] Transform[] plants;

    // Seconds per bucket soaked in, and per plant grown.
    [SerializeField] float soakSeconds = 1.5f;
    [SerializeField] float growSeconds = 1.2f;

    // Seconds for the plants to travel the whole droop, and how far they lean at the end of it.
    [SerializeField] float moodSeconds = 1.5f;
    [SerializeField] float droopDegrees = 24f;

    // Read by Ending; 1 means fully green.
    public float Green => _green;

    // Read by Ending so it can sway them. They are grown here, so the array lives here.
    public Transform[] Plants => plants;

    Weather _weather;
    GrowingTree _tree;
    float _green;          // 0 = bone dry, 1 = fully green
    float _plantDroop;     // 0 = upright, 1 = fully wilted
    float[] _plantScale;   // current growth of each plant, 0..1
    Quaternion[] _plantRotations;
    SpriteRenderer[] _cloudBlocks;

    void Awake()
    {
        // Same object, and the two only make sense together, so no scene reference to go stale.
        _weather = GetComponent<Weather>();

        CreateTree();
        CacheCloudBlocks();
        SnapToCurrentState();
    }

    void Update()
    {
        if (bucket == null)
            return;

        UpdatePlantMood();
        _green = Mathf.MoveTowards(_green, TargetGreen(), Time.deltaTime / soakSeconds);

        ApplyColour(_green);
        GrowPlants();
        GrowTree();
    }

    // Flattened once, because the groups are containers and the blocks inside hold the colour.
    void CacheCloudBlocks()
    {
        var found = new List<SpriteRenderer>();

        if (clouds != null)
        {
            foreach (Transform group in clouds)
            {
                if (group != null)
                    found.AddRange(group.GetComponentsInChildren<SpriteRenderer>(true));
            }
        }

        _cloudBlocks = found.ToArray();
    }

    void CreateTree()
    {
        if (plants == null || plants.Length == 0 || plants[0] == null)
            return;

        SpriteRenderer source = plants[0].GetComponentInChildren<SpriteRenderer>(true);
        if (source == null || source.sprite == null)
            return;

        var root = new GameObject("Growing Tree");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = new Vector3(4.35f, -2.8f, 0f);
        _tree = root.AddComponent<GrowingTree>();
        _tree.Initialise(source);
    }

    void GrowTree()
    {
        if (_tree != null)
            _tree.Grow(DeliveredGreen(), _plantDroop);
    }

    // Starts from the state the scene was saved in, with no animation.
    void SnapToCurrentState()
    {
        if (_plantScale == null)
            _plantScale = new float[plants == null ? 0 : plants.Length];

        if (_plantRotations == null)
        {
            _plantRotations = new Quaternion[plants == null ? 0 : plants.Length];
            for (int i = 0; i < _plantRotations.Length; i++)
                _plantRotations[i] = plants[i] != null ? plants[i].localRotation : Quaternion.identity;
        }

        _green = TargetGreen();
        ApplyColour(_green);

        for (int i = 0; i < _plantScale.Length; i++)
        {
            _plantScale[i] = IsDue(i) ? 1f : 0f;
            ApplyPlantScale(i, _plantScale[i]);
        }
    }

    // What the ground and sky show. Includes the weather offset.
    float TargetGreen()
    {
        if (bucket == null || bucketsToFullGreen <= 0)
            return 0f;

        int offset = _weather != null ? _weather.GreenOffset : 0;
        return Mathf.Clamp01((bucket.DeliveredCount + offset) / (float)bucketsToFullGreen);
    }

    // What has actually been delivered. The plants and the tree follow this, not the offset one.
    float DeliveredGreen()
    {
        if (bucket == null || bucketsToFullGreen <= 0)
            return 0f;

        return Mathf.Clamp01(bucket.DeliveredCount / (float)bucketsToFullGreen);
    }

    void UpdatePlantMood()
    {
        if (plants == null || _plantRotations == null)
            return;

        float want = _weather != null ? _weather.DroopTarget : 0f;
        float next = Mathf.MoveTowards(_plantDroop, want, Time.deltaTime / moodSeconds);

        if (Mathf.Approximately(next, _plantDroop))
            return;

        _plantDroop = next;

        for (int i = 0; i < plants.Length; i++)
        {
            if (plants[i] == null)
                continue;

            plants[i].localRotation = _plantRotations[i] *
                Quaternion.Euler(0f, 0f, -droopDegrees * _plantDroop);
        }
    }

    void ApplyColour(float green)
    {
        if (grass != null)
            grass.color = Color.Lerp(grassDry, grassGreen, green);
        if (dirt != null)
            dirt.color = Color.Lerp(dirtDry, dirtGreen, green);
        if (sky != null)
            sky.backgroundColor = Tinted(Color.Lerp(skyDry, skyClear, green));

        if (_cloudBlocks == null)
            return;

        Color cloud = Tinted(Color.Lerp(cloudDry, cloudClear, green));
        foreach (SpriteRenderer block in _cloudBlocks)
            block.color = cloud;
    }

    Color Tinted(Color colour)
    {
        return _weather != null ? _weather.Tint(colour) : colour;
    }

    // Evenly spread: with 10 plants the first comes up at _green = 1/11.
    bool IsDue(int index)
    {
        if (plants == null || plants.Length == 0)
            return false;

        float threshold = (index + 1f) / (plants.Length + 1f);
        return DeliveredGreen() >= threshold;
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
