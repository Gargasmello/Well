using UnityEngine;

// Opening hint: an arc arrow on the wheel showing which way to crank. Its parts are ordinary
// children of this object, so they can be moved, rotated and recoloured by hand in the Scene
// view; they are only generated when there are none, and replaced from the fields above through
// the Rebuild menu. Fades out once the rope starts moving.
[ExecuteAlways]
public class CrankHint : MonoBehaviour
{
    [Header("Shape")]
    [Tooltip("Sprite the parts are drawn with. The project's 1x1 white block, 1 pixel per metre.")]
    [SerializeField] Sprite block;

    [Tooltip("Metres from the hub. The crank reaches 0.79 m.")]
    [SerializeField] float radius = 0.95f;

    [Tooltip("Degrees swept clockwise from the crank head. Clockwise pays rope out.")]
    [SerializeField] float sweep = 150f;

    [Tooltip("Metres. Thickness of the continuous arc band.")]
    [SerializeField] float arcThickness = 0.10f;

    [Tooltip("Metres.")]
    [SerializeField] float headLength = 0.30f;
    [SerializeField] float headThickness = 0.09f;

    [Tooltip("Degrees between the two head bars.")]
    [SerializeField] float headFan = 28f;

    [Header("Look")]
    [SerializeField] Color colour = new Color(1f, 0.85f, 0.45f, 1f);

    [Tooltip("Degrees. 0 is the crank head at rest.")]
    [SerializeField] float startDegrees = 0f;

    [Tooltip("Seconds to fade once the rope moves.")]
    [SerializeField] float fadeSeconds = 0.4f;

    [Tooltip("Breathing while it waits: radians per second, and depth 0 to 1.")]
    [SerializeField] float pulseSpeed = 3f;
    [SerializeField] float pulseDepth = 0.3f;

    const int HintOrder = 34;

    static Sprite _block;

    Crank _crank;
    SpriteRenderer[] _parts;
    Color[] _baseColours;
    float _alpha = 1f;
    bool _done;

    void OnEnable()
    {
        if (_crank == null && transform.parent != null)
            _crank = transform.parent.GetComponentInChildren<Crank>();

        // Parts are only built when they are missing, so hand edits are never thrown away.
        if (NeedsRebuild())
            Rebuild();

        Gather();
        Apply(1f);
    }

    // True when there is nothing to show: no parts at all, or parts left over from an earlier
    // version of this script. Those had their renderers marked DontSave, so the scene kept the
    // empty objects and dropped the renderers, and a part without a sprite can never be drawn.
    bool NeedsRebuild()
    {
        if (transform.childCount == 0)
            return true;

        foreach (Transform child in transform)
        {
            if ((child.gameObject.hideFlags & HideFlags.DontSave) != 0)
                return true;

            var renderer = child.GetComponent<SpriteRenderer>();

            if (renderer == null || renderer.sprite == null)
                return true;
        }

        return false;
    }

    void Gather()
    {
        _parts = GetComponentsInChildren<SpriteRenderer>(true);
        _baseColours = new Color[_parts.Length];

        for (int i = 0; i < _parts.Length; i++)
            _baseColours[i] = _parts[i].color;
    }

    // Replaces the parts with ones built from the fields above. Right-click the component.
    [ContextMenu("Rebuild")]
    void Rebuild()
    {
        if (this == null)
            return;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;

            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        // Overlapping blocks laid along the tangent make one continuous band.
        int count = Mathf.Max(6, Mathf.CeilToInt(sweep * Mathf.Deg2Rad * radius / (arcThickness * 0.75f)));
        float step = sweep / count;
        float length = 2f * radius * Mathf.Sin(step * 0.5f * Mathf.Deg2Rad) * 1.15f;

        Sprite sprite = block != null ? block : MakeBlock();

        for (int i = 0; i < count; i++)
        {
            float angle = startDegrees - (i + 0.5f) * step;
            MakePart(sprite, "Arc", Offset(angle, radius), new Vector2(length, arcThickness), angle + 90f);
        }

        // Two bars angled back from the end of the sweep make the head.
        float end = startDegrees - sweep;
        Vector2 tip = Offset(end, radius);

        for (int i = 0; i < 2; i++)
        {
            float armAngle = end + 90f + (i == 0 ? -headFan : headFan);
            MakePart(sprite, "Head", tip + Offset(armAngle, headLength * 0.5f),
                new Vector2(headLength, headThickness), armAngle);
        }

        Gather();
    }

    void Update()
    {
        if (!Application.isPlaying || _done || _crank == null || _parts == null)
            return;

        // Stays up until the player has actually paid rope out.
        if (_crank.RopeLength <= _crank.MinRopeLength + 0.001f)
        {
            Apply(1f - pulseDepth * 0.5f * (1f + Mathf.Sin(Time.time * pulseSpeed)));
            return;
        }

        _alpha -= Time.deltaTime / Mathf.Max(fadeSeconds, 0.0001f);

        if (_alpha <= 0f)
        {
            _done = true;
            gameObject.SetActive(false);
            return;
        }

        Apply(_alpha);
    }

    void Apply(float alpha)
    {
        if (_parts == null)
            return;

        alpha = Mathf.Clamp01(alpha);

        for (int i = 0; i < _parts.Length; i++)
        {
            SpriteRenderer part = _parts[i];

            if (part == null)
                continue;

            // Only the alpha is touched, so colours set by hand stay put.
            Color baseColour = _baseColours[i];
            part.color = new Color(baseColour.r, baseColour.g, baseColour.b, baseColour.a * alpha);
        }
    }

    // Part offsets are local to this object, which sits on the hub.
    static Vector2 Offset(float degrees, float metres)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * metres;
    }

    SpriteRenderer MakePart(Sprite block, string name, Vector2 position, Vector2 size, float angle)
    {
        var part = new GameObject(name).AddComponent<SpriteRenderer>();
        part.transform.SetParent(transform, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        part.transform.localScale = new Vector3(size.x, size.y, 1f);
        part.sprite = block;
        part.color = colour;
        part.sortingOrder = HintOrder;
        return part;
    }

    // 1x1 white sprite at 1 pixel per metre, so localScale is the size in metres. Kept out of the
    // scene, since it is rebuilt from the fields above.
    static Sprite MakeBlock()
    {
        if (_block != null)
            return _block;

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, true);
        texture.hideFlags = HideFlags.HideAndDontSave;

        _block = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        _block.hideFlags = HideFlags.HideAndDontSave;
        return _block;
    }
}
