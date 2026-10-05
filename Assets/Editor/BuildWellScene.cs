using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the whole well scene in one go. Editor-only tooling; none of this ships.
///
/// Menu:    Tools / Well / Rebuild Scene
/// Command: unity run D:\Git\Well -- -executeMethod BuildWellScene.Build
///
/// Every shape in the scene is a colour block: one 1x1 white texture is generated
/// and used as a shared sprite, tinted through SpriteRenderer.color and sized with
/// transform.localScale. The texture is 1 pixel per metre, so a block's localScale
/// literally is its size in metres. That is why the project needs no art assets.
/// </summary>
public static class BuildWellScene
{
    const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    const string WellScenePath   = "Assets/Scenes/Well.unity";
    const string BlockSpritePath = "Assets/Art/WhiteBlock.png";

    const string SplashClipPath = "Assets/Audio/110393__soundscalpelcom__water_splash.wav";
    const string CreakClipPath  = "Assets/Audio/106992__robinhood76__02158-spinning-creaking-wheel.wav";
    const string PourClipPath   = "Assets/Audio/421184__inspectorj__water-pouring-a.wav";

    const string PreviewDryPath     = "Logs/well-1-dry.png";
    const string PreviewHalfPath    = "Logs/well-2-half-green.png";
    const string PreviewGreenPath   = "Logs/well-3-green.png";
    const string PreviewDescendPath = "Logs/well-4-descending.png";
    const string PreviewPourPath    = "Logs/well-5-pouring.png";
    const string PreviewMouthPath   = "Logs/well-6-in-the-mouth.png";

    // Sorting order: higher numbers draw in front.
    // Anything that goes "down" is hidden by something drawn in front of it:
    // the well wall hides everything below the rim, the ground hides everything
    // below the horizon.
    //
    // The one rule to keep in mind: the ground has to sit in front of *every*
    // layer of the bucket and the rope, not just the bottom one. The bucket is
    // split into five layers of its own, and if any of them ends up above
    // OrderGround the bucket shows through the dirt as it goes down.
    const int OrderCloud       = -90;
    const int OrderBucket      =  10;   // bucket body
    const int OrderBucketBack  =  11;   // back rim
    const int OrderBucketOpen  =  12;   // dark inside of the bucket
    const int OrderBucketWater =  13;   // water in the bucket
    const int OrderBucketFront =  14;   // front rim, in front of the water
    const int OrderRope        =  15;
    const int OrderGround      =  16;   // in front of the whole bucket and the rope
    const int OrderGrass       =  17;
    const int OrderPlant       =  18;
    const int OrderWellBack    =   8;   // far rim and shaft dark. BEHIND the bucket.
    const int OrderWellFront   =  20;   // masonry below the mouth
    const int OrderWellNearRim =  21;   // the near lip the bucket disappears behind
    const int OrderWellEdge    =  23;
    const int OrderPourStream  =  24;   // the water falling out of the tipped bucket
    // The posts sit behind the bucket, so that when the bucket swings out past one to
    // pour, the post does not slice a stripe out of it. Nothing overlaps in normal play.
    const int OrderPost        =   9;
    const int OrderAxle        =  28;
    const int OrderWheel       =  29;
    const int OrderHub         =  30;
    const int OrderHandle      =  31;
    const int OrderBeam        =  32;   // In front of the wheel, so the crank grip tucks behind it.

    // Key dimensions, in metres.
    const float GroundTopY  = -2.8f;    // Height of the ground surface
    const float WellWidth   =  2.8f;
    const float WellTopY    = -0.9f;    // top of the stone ring
    // The mouth is split into three horizontal bands. The far rim and the dark go behind
    // the bucket; the near lip is what the bucket actually vanishes behind.
    const float FarRimHeight  = 0.07f;
    const float MouthHeight   = 0.18f;
    const float NearRimHeight = 0.14f;
    const float AxleY       =  1.15f;   // Height of the axle, where the top of the rope is pinned
    const float WheelRadius =  0.42f;
    const float PostX       =  1.75f;   // Distance from the centre line to each post
    const float BeamTopY    =  2.30f;

    // Scattered along the grass, in the order they come up: nearest the well first,
    // so the green spreads outwards. Kept clear of the well walls and the two posts.
    static readonly float[] PlantX = { -2.1f, 2.2f, 2.5f, -2.6f, 3.4f, -3.7f, 4.4f, -4.8f, 5.3f, -5.6f };

    static readonly Color Sky       = Rgb(0x87, 0xC5, 0xE8);
    static readonly Color Cloud     = Rgb(0xFF, 0xFF, 0xFF);

    // The ground starts dry and greens up as water is delivered.
    static readonly Color DirtDry    = Rgb(0xB3, 0x90, 0x62);
    static readonly Color GrassDry   = Rgb(0xC9, 0xB3, 0x5E);

    static readonly Color Stone     = Rgb(0x9A, 0x9A, 0x93);
    static readonly Color StoneDark = Rgb(0x8C, 0x8C, 0x85);
    static readonly Color StoneLit  = Rgb(0xB8, 0xB8, 0xB0);
    static readonly Color PitDark   = Rgb(0x1A, 0x1A, 0x22);
    static readonly Color Wood      = Rgb(0x7A, 0x52, 0x30);
    static readonly Color WoodLit   = Rgb(0x9A, 0x6C, 0x42);
    static readonly Color Bucket    = Rgb(0x8A, 0x6A, 0x45);
    static readonly Color BucketLip = Rgb(0xA8, 0x84, 0x5A);
    static readonly Color BucketDark= Rgb(0x3A, 0x2C, 0x1E);
    static readonly Color Water     = Rgb(0x4F, 0xA8, 0xD8);
    static readonly Color Iron      = Rgb(0x5A, 0x4A, 0x38);
    static readonly Color Hemp      = Rgb(0xD9, 0xC0, 0x8A);
    static readonly Color Sprout    = Rgb(0x7E, 0xD9, 0x57);   // Brighter than mature grass, so new growth pops.
    static readonly Color[] Blooms  = { Rgb(0xF2, 0xE0, 0x5A), Rgb(0xF2, 0xF2, 0xF2), Rgb(0xE8, 0x8B, 0xB0) };

    static Color Rgb(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

    // ================================================================
    //  Build
    // ================================================================

    [MenuItem("Tools/Well/Rebuild Scene")]
    public static void Build() => BuildScene(force: false);

    [MenuItem("Tools/Well/Force Rebuild Scene")]
    public static void ForceBuild() => BuildScene(force: true);

    /// <summary>
    /// Generates the scene from scratch.
    ///
    /// Refuses to touch an existing scene unless <paramref name="force"/> is set. Unity
    /// hands every object it creates a fresh local file id, so regenerating an unchanged
    /// scene still rewrites the entire file - thousands of lines of git churn for no
    /// actual difference. The scene is committed, so leaving it alone is the safe default.
    /// </summary>
    public static void BuildScene(bool force)
    {
        if (!force && File.Exists(Path.GetFullPath(WellScenePath)))
        {
            Debug.LogError(
                $"[BuildWellScene] {WellScenePath} already exists, so nothing was rebuilt. " +
                "Regenerating rewrites every local file id and churns the whole file even when " +
                "the scene has not actually changed. If you really mean it, use " +
                "Tools/Well/Force Rebuild Scene, or -executeMethod BuildWellScene.ForceBuild.");
            return;
        }

        Sprite block = EnsureBlockSprite();

        // Start from a copy of SampleScene so we inherit a working URP 2D camera.
        // saveAsCopy is true, so SampleScene itself is never modified.
        var sample = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        EditorSceneManager.SaveScene(sample, WellScenePath, true);
        var scene = EditorSceneManager.OpenScene(WellScenePath, OpenSceneMode.Single);

        // Keep only the camera. The template's 2D global light goes: colour blocks are unlit.
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<Camera>() == null)
                Object.DestroyImmediate(root);
        }

        Camera camera = ConfigureCamera();

        var well = new GameObject("Well").transform;
        well.position = Vector3.zero;

        BuildClouds(well, block);
        BuildGround(well, block, out SpriteRenderer grass, out SpriteRenderer dirt);
        BuildWell(well, block);
        BuildFrame(well, block);

        Transform wheel = BuildWheel(well, block);

        Transform ropeBlock = Block("Rope", well, block, Hemp,
            new Vector2(0.08f, 1f), new Vector2(0f, AxleY - 0.5f), OrderRope);

        Transform bucket = BuildBucket(well, block, out Transform water);
        Transform[] plants = BuildPlants(well, block);

        // The water that falls out of the tipped bucket. It is a child of the well rather
        // than of the bucket, so it does not spin around when the bucket tips.
        Transform pourStream = Block("PourStream", well, block, Water,
            new Vector2(0.16f, 0.5f), Vector2.zero, OrderPourStream);
        pourStream.gameObject.SetActive(false);

        // Wire it up: crank -> rope length -> rope and bucket -> ground and audio.
        var crank = wheel.gameObject.AddComponent<Crank>();

        var rope = well.gameObject.AddComponent<Rope>();
        rope.Bind(crank, ropeBlock, bucket);
        rope.Refresh();

        var bucketState = bucket.gameObject.AddComponent<Bucket>();
        bucketState.Bind(crank, water);

        var greening = well.gameObject.AddComponent<GroundGreening>();
        greening.Bind(bucketState, grass, dirt, plants);
        greening.SnapToCurrentState();

        AudioSource pour = BuildAudio(well, crank, bucketState);

        var pouring = well.gameObject.AddComponent<Pouring>();
        pouring.Bind(crank, bucketState, rope, bucket, pour, pourStream);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[BuildWellScene] Built {WellScenePath}. Rope is {crank.RopeLength:0.##} m, " +
                  $"{plants.Length} plants waiting, water between " +
                  $"{bucketState.MinWaterDepth:0.#} and {bucketState.MaxWaterDepth:0.#} m.");
    }

    static Camera ConfigureCamera()
    {
        Camera camera = Object.FindFirstObjectByType<Camera>();
        if (camera == null)
        {
            camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.gameObject.tag = "MainCamera";
        }

        camera.orthographic = true;
        camera.orthographicSize = 3.4f;                 // 6.8 metres of view, top to bottom
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Sky;
        camera.transform.position = new Vector3(0f, -0.3f, -10f);
        camera.transform.rotation = Quaternion.identity;

        return camera;
    }

    // ================================================================
    //  Pieces of the scene
    // ================================================================

    static void BuildClouds(Transform parent, Sprite block)
    {
        var clouds = Child("Clouds", parent);

        BuildCloud(clouds, block, new Vector2( 3.0f, 2.55f), 1.00f);
        // Moved by hand in the editor and then baked back in here: the scene is generated,
        // so a position only survives a rebuild if it lives in this file.
        BuildCloud(clouds, block, new Vector2(-4.3f, 1.61f), 0.80f);
        BuildCloud(clouds, block, new Vector2( 5.1f, 2.15f), 0.65f);
    }

    /// <summary>One cloud is three white blocks stacked together.</summary>
    static void BuildCloud(Transform parent, Sprite block, Vector2 center, float scale)
    {
        Block("Cloud", parent, block, Cloud, new Vector2(1.5f, 0.42f) * scale, center, OrderCloud);
        Block("Cloud", parent, block, Cloud, new Vector2(1.0f, 0.36f) * scale,
              center + new Vector2(-0.55f, 0.10f) * scale, OrderCloud);
        Block("Cloud", parent, block, Cloud, new Vector2(0.9f, 0.34f) * scale,
              center + new Vector2( 0.60f, 0.06f) * scale, OrderCloud);
    }

    static void BuildGround(Transform parent, Sprite block,
                            out SpriteRenderer grass, out SpriteRenderer dirt)
    {
        var ground = Child("Ground", parent);

        // Drawn in front of the rope and the bucket, so anything below the horizon vanishes.
        dirt  = Block("Dirt",  ground, block, DirtDry,  new Vector2(40f, 4f),
                      new Vector2(0f, GroundTopY - 2f), OrderGround).GetComponent<SpriteRenderer>();
        grass = Block("Grass", ground, block, GrassDry, new Vector2(40f, 0.3f),
                      new Vector2(0f, GroundTopY - 0.15f), OrderGrass).GetComponent<SpriteRenderer>();
    }

    static void BuildWell(Transform parent, Sprite block)
    {
        var well = Child("WellBody", parent);

        float farRimTop  = WellTopY;                        // -0.90
        float mouthTop   = farRimTop - FarRimHeight;        // -0.97
        float nearRimTop = mouthTop - MouthHeight;          // -1.15
        float wallTop    = nearRimTop - NearRimHeight;      // -1.29

        // The far edge of the ring and the dark of the shaft. Both sit BEHIND the bucket,
        // so the bucket stays visible as it enters the mouth.
        Block("RimFar",  well, block, StoneLit, new Vector2(WellWidth + 0.12f, FarRimHeight),
              new Vector2(0f, farRimTop - FarRimHeight * 0.5f), OrderWellBack);
        Block("Opening", well, block, PitDark, new Vector2(WellWidth - 0.5f, MouthHeight),
              new Vector2(0f, mouthTop - MouthHeight * 0.5f), OrderWellBack);

        // The near lip is the front edge of the ring, and it is what the bucket actually
        // disappears behind. Splitting the mouth here is the whole point: drawn as one
        // piece in front of the bucket, the bucket looks like it is standing behind the
        // well instead of going into it.
        Block("RimNear", well, block, StoneLit, new Vector2(WellWidth + 0.12f, NearRimHeight),
              new Vector2(0f, nearRimTop - NearRimHeight * 0.5f), OrderWellNearRim);

        // Masonry courses below the lip.
        float bodyHeight  = GroundTopY - wallTop;
        float bodyCenterY = (GroundTopY + wallTop) * 0.5f;

        const int courses  = 5;
        float courseHeight = bodyHeight / courses;
        for (int i = 0; i < courses; i++)
        {
            float y = GroundTopY - courseHeight * (i + 0.5f);
            Block($"Course{i}", well, block, i % 2 == 0 ? Stone : StoneDark,
                  new Vector2(WellWidth, courseHeight), new Vector2(0f, y), OrderWellFront);
        }

        // Lighter edges down each side, to give the wall some thickness.
        float edgeX = WellWidth * 0.5f - 0.11f;
        Block("LeftEdge",  well, block, StoneLit, new Vector2(0.22f, bodyHeight),
              new Vector2(-edgeX, bodyCenterY), OrderWellEdge);
        Block("RightEdge", well, block, StoneLit, new Vector2(0.22f, bodyHeight),
              new Vector2( edgeX, bodyCenterY), OrderWellEdge);
    }

    static void BuildFrame(Transform parent, Sprite block)
    {
        var frame = Child("Frame", parent);

        float postHeight  = BeamTopY - GroundTopY;
        float postCenterY = (BeamTopY + GroundTopY) * 0.5f;

        Block("PostLeft",  frame, block, Wood, new Vector2(0.24f, postHeight),
              new Vector2(-PostX, postCenterY), OrderPost);
        Block("PostRight", frame, block, Wood, new Vector2(0.24f, postHeight),
              new Vector2( PostX, postCenterY), OrderPost);
        Block("Beam",      frame, block, Wood, new Vector2(PostX * 2f + 0.4f, 0.26f),
              new Vector2(0f, BeamTopY - 0.13f), OrderBeam);
    }

    static Transform BuildWheel(Transform parent, Sprite block)
    {
        // The axle the wheel turns on, spanning the two posts.
        Block("Axle", parent, block, Wood, new Vector2(PostX * 2f + 0.2f, 0.1f),
              new Vector2(0f, AxleY), OrderAxle);

        var wheel = Child("Wheel", parent);
        wheel.localPosition = new Vector3(0f, AxleY, 0f);

        // The rim: eight short bars laid end to end, making an octagon.
        const int rimSegments = 8;
        float segmentLength = 2f * Mathf.PI * WheelRadius / rimSegments * 1.1f;
        for (int i = 0; i < rimSegments; i++)
        {
            float angle  = 360f / rimSegments * i;
            float radian = angle * Mathf.Deg2Rad;
            Block($"Rim{i}", wheel, block, WoodLit,
                  new Vector2(segmentLength, 0.11f),
                  new Vector2(Mathf.Cos(radian) * WheelRadius, Mathf.Sin(radian) * WheelRadius),
                  OrderWheel, angle + 90f);
        }

        // Three full-width spokes, giving six evenly spaced arms.
        // Deliberately not at 30/90/150: a spoke straight down would cover the rope.
        float diameter = WheelRadius * 2f;
        for (int i = 0; i < 3; i++)
        {
            Block($"Spoke{i}", wheel, block, Wood,
                  new Vector2(diameter, 0.08f), Vector2.zero, OrderWheel, 60f * i);
        }

        Block("Hub", wheel, block, Iron, new Vector2(0.18f, 0.18f), Vector2.zero, OrderHub);

        // The crank: an arm reaching out past the rim, with a grip on the end.
        Block("CrankArm",  wheel, block, Iron, new Vector2(0.36f, 0.10f),
              new Vector2(WheelRadius + 0.12f, 0f), OrderHandle);
        Block("CrankGrip", wheel, block, Iron, new Vector2(0.14f, 0.14f),
              new Vector2(WheelRadius + 0.30f, 0f), OrderHandle);

        return wheel;
    }

    static Transform BuildBucket(Transform parent, Sprite block, out Transform water)
    {
        // The bucket's origin is (0, 0) - the end of the rope, at the top of the handle.
        var bucket = Child("Bucket", parent);
        bucket.localPosition = new Vector3(0f, AxleY - 0.8f, 0f);

        // The bucket is drawn as if seen from slightly above, so you can look into it:
        // a back rim, the dark opening, then the front rim with the body below it.
        // The water lives in the opening, behind the front rim, which is what stops it
        // from ever appearing to spill over the lip.
        const float rimHalf       = 0.44f;   // where the handle meets the rim
        const float rimTopY       = -0.30f;  // top of the back rim
        const float rimWidth      = 0.92f;
        const float rimLip        = 0.06f;
        const float openingHeight = 0.11f;

        float backRimCenterY  = rimTopY - rimLip * 0.5f;                        // -0.33
        float openingTopY     = rimTopY - rimLip;                               // -0.36
        float openingCenterY  = openingTopY - openingHeight * 0.5f;             // -0.415
        float frontRimCenterY = openingTopY - openingHeight - rimLip * 0.5f;    // -0.50
        float bodyCenterY     = frontRimCenterY - rimLip * 0.5f - 0.28f;        // -0.81

        // Handle: two bars angled up from the rim to the hanging point.
        float barLength = Mathf.Sqrt(rimHalf * rimHalf + rimTopY * rimTopY);
        float barAngle  = Mathf.Atan2(rimHalf, -rimTopY) * Mathf.Rad2Deg;

        Block("HandleLeft",  bucket, block, Iron, new Vector2(0.06f, barLength),
              new Vector2(-rimHalf * 0.5f, rimTopY * 0.5f), OrderBucket, -barAngle);
        Block("HandleRight", bucket, block, Iron, new Vector2(0.06f, barLength),
              new Vector2( rimHalf * 0.5f, rimTopY * 0.5f), OrderBucket,  barAngle);

        Block("Body",    bucket, block, Bucket,     new Vector2(0.82f, 0.56f),
              new Vector2(0f, bodyCenterY), OrderBucket);
        Block("RimBack", bucket, block, BucketLip,  new Vector2(rimWidth, rimLip),
              new Vector2(0f, backRimCenterY), OrderBucketBack);
        Block("Opening", bucket, block, BucketDark, new Vector2(0.78f, openingHeight),
              new Vector2(0f, openingCenterY), OrderBucketOpen);

        // The water fills the lower part of the opening, leaving a dark sliver of air above it.
        water = Block("Water", bucket, block, Water, new Vector2(0.78f, 0.07f),
                      new Vector2(0f, openingCenterY - 0.02f), OrderBucketWater);
        water.gameObject.SetActive(false);

        // The front rim goes in last and in front, so it always covers the water.
        Block("RimFront", bucket, block, BucketLip, new Vector2(rimWidth, rimLip),
              new Vector2(0f, frontRimCenterY), OrderBucketFront);

        return bucket;
    }

    static Transform[] BuildPlants(Transform parent, Sprite block)
    {
        var plants = new Transform[PlantX.Length];
        for (int i = 0; i < PlantX.Length; i++)
            plants[i] = BuildPlant(parent, block, PlantX[i], i);

        return plants;
    }

    static Transform BuildPlant(Transform parent, Sprite block, float x, int index)
    {
        // The plant's origin sits on the ground, so scaling the root grows it upwards.
        var plant = Child($"Plant{index}", parent);
        plant.localPosition = new Vector3(x, GroundTopY, 0f);

        // Every third one is a flower; the rest are tufts of grass.
        if (index % 3 == 2)
        {
            Block("Stem",  plant, block, Sprout, new Vector2(0.06f, 0.44f),
                  new Vector2(0f, 0.22f), OrderPlant);
            Block("Bloom", plant, block, Blooms[index % Blooms.Length],
                  new Vector2(0.20f, 0.20f), new Vector2(0f, 0.48f), OrderPlant, 45f);
        }
        else
        {
            Block("Blade", plant, block, Sprout, new Vector2(0.07f, 0.36f),
                  new Vector2(-0.10f, 0.18f), OrderPlant,  16f);
            Block("Blade", plant, block, Sprout, new Vector2(0.07f, 0.48f),
                  new Vector2( 0.00f, 0.24f), OrderPlant,  -4f);
            Block("Blade", plant, block, Sprout, new Vector2(0.07f, 0.30f),
                  new Vector2( 0.11f, 0.15f), OrderPlant, -18f);
        }

        plant.localScale = Vector3.zero;
        plant.gameObject.SetActive(false);
        return plant;
    }

    // ================================================================
    //  Audio
    // ================================================================

    static AudioSource BuildAudio(Transform parent, Crank crank, Bucket bucket)
    {
        var audio = Child("Audio", parent);

        AudioSource creak  = AddSource(audio, "WheelCreak", CreakClipPath,  loop: true);
        AudioSource splash = AddSource(audio, "Splash",     SplashClipPath, loop: false);
        AudioSource pour   = AddSource(audio, "Pour",       PourClipPath,   loop: false);

        // A splash at the bottom of a well should reach you muffled. This is only the
        // starting value; WellAudio dials the cutoff per splash, from how deep the water was.
        var lowPass = splash.gameObject.AddComponent<AudioLowPassFilter>();
        lowPass.cutoffFrequency = 800f;
        lowPass.lowpassResonanceQ = 1f;

        var wellAudio = audio.gameObject.AddComponent<WellAudio>();
        wellAudio.Bind(crank, bucket, creak, splash);

        return pour;
    }

    static AudioSource AddSource(Transform parent, string name, string clipPath, bool loop)
    {
        var go = Child(name, parent);

        var source = go.gameObject.AddComponent<AudioSource>();
        source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = 0f;                  // plain 2D sound
        source.volume = loop ? 0f : 1f;

        if (source.clip == null)
            Debug.LogWarning($"[BuildWellScene] No audio clip found at {clipPath}");

        return source;
    }

    // ================================================================
    //  Colour blocks
    // ================================================================

    /// <summary>
    /// One colour block. The sprite is 1x1 at one pixel per metre, so
    /// <paramref name="size"/> is literally the size in metres.
    /// </summary>
    static Transform Block(string name, Transform parent, Sprite sprite, Color color,
                           Vector2 size, Vector2 position, int sortingOrder, float rotationZ = 0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(position.x, position.y, 0f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        return go.transform;
    }

    static Transform Child(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    /// <summary>Generates the shared white block texture if it is not there yet.</summary>
    static Sprite EnsureBlockSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(BlockSpritePath);
        if (existing != null)
            return existing;

        string fullPath = Path.GetFullPath(BlockSpritePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        File.WriteAllBytes(fullPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(BlockSpritePath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(BlockSpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 1f;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(BlockSpritePath);
    }

    // ================================================================
    //  Previews, so the scene can be checked without opening the editor
    // ================================================================

    [MenuItem("Tools/Well/Render Previews")]
    public static void RenderPreview()
    {
        EditorSceneManager.OpenScene(WellScenePath, OpenSceneMode.Single);

        Camera camera            = Object.FindFirstObjectByType<Camera>();
        var crank                = Object.FindFirstObjectByType<Crank>();
        var rope                 = Object.FindFirstObjectByType<Rope>();
        var bucket               = Object.FindFirstObjectByType<Bucket>();
        var greening             = Object.FindFirstObjectByType<GroundGreening>();
        var pouring              = Object.FindFirstObjectByType<Pouring>();

        if (camera == null || crank == null || rope == null || bucket == null ||
            greening == null || pouring == null)
        {
            Debug.LogError("[BuildWellScene] Scene is missing one of the well components");
            return;
        }

        // Fixed depth, so the previews come out the same every time.
        bucket.SetWaterDepth(22f);

        // 1. Untouched: dry ground, empty bucket hanging at the top.
        crank.SetRopeLength(0f);
        bucket.Refresh(crank.RopeLength);
        rope.Refresh();
        greening.SnapToCurrentState();
        RenderTo(camera, PreviewDryPath);

        // 2. Three trips in: half green, bucket just wound back up with water in it.
        SimulateTrips(crank, bucket, 3);
        WindUpWithWater(crank, bucket);
        rope.Refresh();
        greening.SnapToCurrentState();
        RenderTo(camera, PreviewHalfPath);

        // 3. Six trips in: the ground is green.
        SimulateTrips(crank, bucket, 3);
        WindUpWithWater(crank, bucket);
        rope.Refresh();
        greening.SnapToCurrentState();
        RenderTo(camera, PreviewGreenPath);

        // 4. Wound back down. The bucket is carrying water and is below the horizon,
        //    so it has to be completely hidden - nothing may show through the ground.
        crank.SetRopeLength(4f);
        bucket.Refresh(crank.RopeLength);
        rope.Refresh();
        greening.SnapToCurrentState();
        RenderTo(camera, PreviewDescendPath);

        // 5. Mid-pour: swung out past the well wall with the bucket tipped over.
        //    Posed directly, because the pour is a timed animation and Update does not
        //    run in the editor.
        bucket.StartNewTrip();
        crank.SetRopeLength(bucket.WaterDepth + 1f);
        bucket.Refresh(crank.RopeLength);                    // fills
        crank.SetRopeLength(bucket.PourAtRopeLength);
        bucket.Refresh(crank.RopeLength);                    // ready to pour
        pouring.PoseAt(pouring.Duration * 0.6f);             // out and tipped
        RenderTo(camera, PreviewPourPath);

        // 6. Bucket down in the mouth, far enough that the near lip is cutting it. It has
        //    to be visible against the dark and cut off only by the lip - this is the shot
        //    that proves the mouth is split rather than drawn as one piece.
        pouring.ResetPose();
        crank.SetRopeLength(1.3f);
        bucket.Refresh(crank.RopeLength);
        rope.Refresh();
        greening.SnapToCurrentState();
        RenderTo(camera, PreviewMouthPath);

        Debug.Log("[BuildWellScene] Previews written to Logs/");
    }

    /// <summary>Drive the real fill/pour logic the way a player would, so previews show genuine state.</summary>
    static void SimulateTrips(Crank crank, Bucket bucket, int trips)
    {
        for (int i = 0; i < trips; i++)
        {
            bucket.StartNewTrip();
            crank.SetRopeLength(bucket.WaterDepth + 1f);   // down past the water table
            bucket.Refresh(crank.RopeLength);              // fills, splash
            crank.SetRopeLength(0f);                       // all the way back up
            bucket.Refresh(crank.RopeLength);              // ready to pour
            bucket.FinishPour();                           // tipped out
        }
    }

    /// <summary>
    /// Fill the bucket, then leave it hanging just above the height where it would be
    /// ready to pour, so the water is on show.
    /// </summary>
    static void WindUpWithWater(Crank crank, Bucket bucket)
    {
        bucket.StartNewTrip();
        crank.SetRopeLength(bucket.WaterDepth + 1f);
        bucket.Refresh(crank.RopeLength);
        crank.SetRopeLength(bucket.PourAtRopeLength + 0.15f);
        bucket.Refresh(crank.RopeLength);
    }

    /// <summary>
    /// Render the pour as a strip of frames. A still cannot show whether a timed
    /// animation reads properly, and this is the only way to check it without a display.
    /// </summary>
    [MenuItem("Tools/Well/Render Pour Strip")]
    public static void RenderPourStrip()
    {
        EditorSceneManager.OpenScene(WellScenePath, OpenSceneMode.Single);

        Camera camera = Object.FindFirstObjectByType<Camera>();
        var crank     = Object.FindFirstObjectByType<Crank>();
        var bucket    = Object.FindFirstObjectByType<Bucket>();
        var pouring   = Object.FindFirstObjectByType<Pouring>();

        if (camera == null || crank == null || bucket == null || pouring == null)
        {
            Debug.LogError("[BuildWellScene] Scene is missing one of the well components");
            return;
        }

        bucket.SetWaterDepth(22f);

        // Bring a full bucket up to the point where the pour begins.
        bucket.StartNewTrip();
        crank.SetRopeLength(bucket.WaterDepth + 1f);
        bucket.Refresh(crank.RopeLength);
        crank.SetRopeLength(bucket.PourAtRopeLength);
        bucket.Refresh(crank.RopeLength);

        const int frames = 6;
        for (int i = 0; i < frames; i++)
        {
            pouring.PoseAt(pouring.Duration * i / (frames - 1f));
            RenderTo(camera, $"Logs/pour-{i}.png");
        }

        Debug.Log($"[BuildWellScene] {frames} pour frames written to Logs/");
    }

    static void RenderTo(Camera camera, string path)
    {
        const int width = 1280;
        const int height = 720;

        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        target.Create();
        camera.aspect = (float)width / height;
        camera.targetTexture = target;
        camera.Render();

        RenderTexture.active = target;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();

        RenderTexture.active = null;
        camera.targetTexture = null;

        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllBytes(fullPath, image.EncodeToPNG());

        Object.DestroyImmediate(image);
        Object.DestroyImmediate(target);

        Debug.Log($"[BuildWellScene] Preview written to {fullPath}");
    }
}
