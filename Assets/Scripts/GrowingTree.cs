using UnityEngine;

public class GrowingTree : MonoBehaviour
{
    class Branch
    {
        public float side;
        public SpriteRenderer wood, leaves;
    }

    class SeedLeaf
    {
        public float attach, start;
        public Vector2 direction;
        public SpriteRenderer stem, leaf;
    }

    static readonly float[] TrunkHeights = { 0f, 0.5f, 0.85f, 1.3f, 2.1f, 2.85f, 3.55f };
    static readonly float[] TrunkWidths = { 0f, 0.035f, 0.06f, 0.13f, 0.26f, 0.46f, 0.7f };
    static readonly float[] BranchReaches = { 0f, 0f, 0f, 0.18f, 0.27f, 0.38f, 0.45f };
    static readonly float[] BranchRises = { 0f, 0f, 0f, 0.3f, 0.58f, 0.9f, 1.15f };
    static readonly float[] BranchWidths = { 0f, 0f, 0f, 0.06f, 0.12f, 0.22f, 0.34f };
    static readonly float[] CrownSizes = { 0f, 0f, 0f, 0.45f, 0.9f, 1.55f, 2.2f };
    static readonly Color Bark = new Color32(115, 83, 54, 255);
    static readonly Color Leaves = new Color32(85, 146, 70, 255);
    static readonly Color YoungLeaves = new Color32(112, 169, 82, 255);
    static readonly Color DryLeaves = new Color32(149, 146, 83, 255);

    // Below the grass strip (17) and the plants (18, 19), so the tree never covers a blade.
    const int OrderWood = 14;
    const int OrderCrown = 15;
    const int OrderYoungLeaf = 16;

    Sprite _trunkSprite, _branchSprite, _crownSprite, _leafSprite;
    readonly Texture2D[] _textures = new Texture2D[4];
    int _textureCount;
    SpriteRenderer _trunk;
    Branch[] _branches;
    SeedLeaf[] _seedLeaves;
    Material _material;
    int _sortingLayer;
    float _growth;

    public void Initialise(SpriteRenderer source)
    {
        _material = source.sharedMaterial;
        _sortingLayer = source.sortingLayerID;

        _trunkSprite = MakeSprite("Tree trunk", new[] {
            new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f),
            new Vector2(0.43f, -0.3f), new Vector2(0.35f, 0.07f),
            new Vector2(0.3f, 0.5f), new Vector2(-0.3f, 0.5f),
            new Vector2(-0.35f, 0.07f), new Vector2(-0.43f, -0.3f)
        });

        _branchSprite = MakeSprite("Tree branch", new[] {
            new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f),
            new Vector2(0.3f, 0.5f), new Vector2(-0.3f, 0.5f)
        });
        _crownSprite = MakeSprite("Tree foliage", new[] {
            new Vector2(-0.48f, -0.12f), new Vector2(-0.39f, -0.34f),
            new Vector2(-0.15f, -0.48f), new Vector2(0.13f, -0.46f),
            new Vector2(0.38f, -0.3f), new Vector2(0.49f, -0.03f),
            new Vector2(0.42f, 0.23f), new Vector2(0.21f, 0.42f),
            new Vector2(-0.04f, 0.5f), new Vector2(-0.3f, 0.37f),
            new Vector2(-0.47f, 0.15f)
        });
        _leafSprite = MakeSprite("Sapling leaf", new[] {
            new Vector2(0f, -0.5f), new Vector2(0.4f, -0.12f),
            new Vector2(0.36f, 0.2f), new Vector2(0f, 0.5f),
            new Vector2(-0.36f, 0.2f), new Vector2(-0.4f, -0.12f)
        });

        _trunk = AddPart("Trunk", _trunkSprite, Bark, OrderWood);
        _branches = new[] {
            AddBranch(-1f),
            AddBranch(1f)
        };
        _seedLeaves = new[] {
            AddSeedLeaf(0.55f, new Vector2(-0.85f, 0.5f), 0.02f),
            AddSeedLeaf(0.85f, new Vector2(0.8f, 0.6f), 0.025f),
            AddSeedLeaf(0.4f, new Vector2(0.9f, 0.4f), 0.19f),
            AddSeedLeaf(0.72f, new Vector2(-0.75f, 0.65f), 0.22f)
        };
        Draw(0f);
    }

    public void Grow(float progress, float dryness)
    {
        // Each bucket takes about a second to grow in.
        _growth = Mathf.MoveTowards(_growth, progress, Time.deltaTime / 6f);
        Draw(dryness);
    }

    void Draw(float dryness)
    {
        if (_trunk == null)
            return;

        float height = StageValue(TrunkHeights, _growth);
        float width = StageValue(TrunkWidths, _growth);
        float reach = StageValue(BranchReaches, _growth);
        float rise = StageValue(BranchRises, _growth);
        float branchWidth = StageValue(BranchWidths, _growth);
        float leafSize = StageValue(CrownSizes, _growth);
        Place(_trunk, new Vector2(0f, height * 0.5f), new Vector2(width, height));

        foreach (Branch branch in _branches)
        {
            Vector2 anchor = new Vector2(0f, height - width * 0.1f);
            Vector2 tip = anchor + new Vector2(branch.side * reach, rise);
            DrawStem(branch.wood, anchor, tip, branchWidth);
            float leafScale = branch.side < 0f ? 1.2f : 1f;
            Place(branch.leaves, tip, Vector2.one * leafSize * leafScale);
            branch.leaves.color = Color.Lerp(Leaves, DryLeaves, dryness * 0.65f);
        }

        float leafFade = 1f - Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(2f / 6f, 3f / 6f, _growth));
        foreach (SeedLeaf leaf in _seedLeaves)
        {
            float growth = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(leaf.start, leaf.start + 0.12f, _growth));
            float length = 0.36f * growth * leafFade;
            Vector2 anchor = new Vector2(0f, height * leaf.attach);
            Vector2 tip = anchor + leaf.direction * length * 0.2f;
            DrawStem(leaf.stem, anchor, tip, 0.015f * leafFade);
            Place(leaf.leaf, tip + leaf.direction * length * 0.5f, new Vector2(length * 0.55f, length));
            leaf.leaf.transform.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(leaf.direction.y, leaf.direction.x) * Mathf.Rad2Deg - 90f);
            leaf.leaf.color = Color.Lerp(YoungLeaves, DryLeaves, dryness * 0.65f);
        }
    }

    Branch AddBranch(float side)
    {
        return new Branch {
            side = side,
            wood = AddPart("Branch", _branchSprite, Bark, OrderWood),
            leaves = AddPart("Crown", _crownSprite, Leaves, OrderCrown)
        };
    }

    SeedLeaf AddSeedLeaf(float attach, Vector2 direction, float start)
    {
        return new SeedLeaf {
            attach = attach, direction = direction.normalized, start = start,
            stem = AddPart("Leaf stem", _branchSprite, Bark, OrderWood),
            leaf = AddPart("Young leaf", _leafSprite, YoungLeaves, OrderYoungLeaf)
        };
    }

    SpriteRenderer AddPart(string name, Sprite sprite, Color colour, int order)
    {
        var part = new GameObject(name);
        part.transform.SetParent(transform, false);
        var renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = _material;
        renderer.sortingLayerID = _sortingLayer;
        renderer.sortingOrder = order;
        renderer.color = colour;
        return renderer;
    }

    static void Place(SpriteRenderer renderer, Vector2 position, Vector2 size)
    {
        renderer.enabled = size.x > 0.001f && size.y > 0.001f;
        renderer.transform.localPosition = position;
        renderer.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    static void DrawStem(SpriteRenderer renderer, Vector2 from, Vector2 to, float width)
    {
        Vector2 direction = to - from;
        Place(renderer, (from + to) * 0.5f, new Vector2(width, direction.magnitude));
        renderer.transform.localRotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
    }

    static float StageValue(float[] stages, float progress)
    {
        float step = Mathf.Clamp01(progress) * (stages.Length - 1);
        int index = Mathf.Min(Mathf.FloorToInt(step), stages.Length - 2);
        return Mathf.Lerp(stages[index], stages[index + 1], step - index);
    }

    Sprite MakeSprite(string name, Vector2[] points)
    {
        const int resolution = 128;
        var pixels = new Color32[resolution * resolution];

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                Vector2 point = new Vector2((x + 0.5f) / resolution - 0.5f,
                    (y + 0.5f) / resolution - 0.5f);
                bool inside = false;

                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                {
                    if ((points[i].y > point.y) != (points[j].y > point.y) &&
                        point.x < (points[j].x - points[i].x) * (point.y - points[i].y) /
                        (points[j].y - points[i].y) + points[i].x)
                        inside = !inside;
                }

                if (inside)
                    pixels[y * resolution + x] = Color.white;
            }
        }

        var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        texture.name = name;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        _textures[_textureCount++] = texture;

        var sprite = Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution),
            new Vector2(0.5f, 0.5f), resolution, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        return sprite;
    }

    void OnDestroy()
    {
        if (_trunkSprite != null) Destroy(_trunkSprite);
        if (_branchSprite != null) Destroy(_branchSprite);
        if (_crownSprite != null) Destroy(_crownSprite);
        if (_leafSprite != null) Destroy(_leafSprite);
        for (int i = 0; i < _textureCount; i++)
            Destroy(_textures[i]);
    }
}
