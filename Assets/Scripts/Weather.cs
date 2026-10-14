using UnityEngine;

// Sun once enough has been delivered, rain once it has cost enough dry trips. Reads Bucket;
// tells GroundGreening how far to shift the green and how hard to droop the plants.
public class Weather : MonoBehaviour
{
    [SerializeField] Bucket bucket;

    // Where the rain and rays are placed, plus a 1x1 block for them to draw with.
    [SerializeField] Camera sky;
    [SerializeField] SpriteRenderer block;

    const int BucketsBeforeSun = 3;
    const int DryTripsBeforeRain = 3;
    const int RainDropCount = 28;
    const int SunRayCount = 6;
    const float CueDuration = 8f;
    const float FadeSeconds = 1.25f;
    const float TintStrength = 0.35f;

    static readonly Color SunTint = new Color(1f, 0.78f, 0.5f, 1f);
    static readonly Color RainTint = new Color(0.58f, 0.74f, 0.84f, 1f);

    // Buckets subtracted from the greening between the sun and the rain. Plants do not recede.
    public int GreenOffset { get; private set; }

    // 0 = upright, 1 = fully drooped. GroundGreening eases the plants toward it.
    public float DroopTarget { get; private set; }

    int _tripsAtSunArm;
    int _dryHitsAtSun;
    int _tripsAtRainArm;
    bool _sunArmed;
    bool _sunTriggered;
    bool _rainArmed;
    bool _rainTriggered;
    bool _isRaining;
    float _cue;
    float _cueElapsed;
    Transform[] _rainDrops;
    GameObject _rainRoot;
    SpriteRenderer[] _sunRays;
    GameObject _sunRoot;

    void Awake()
    {
        if (sky == null)
            sky = Camera.main;
    }

    void Update()
    {
        if (bucket == null)
            return;

        UpdateState();
        UpdateCue();
        UpdateMood();
        UpdateRain();
        UpdateSunRays();
    }

    // The weather's tint over a base colour. Untouched when nothing is happening.
    public Color Tint(Color colour)
    {
        if (_cue <= 0f)
            return colour;

        return Color.Lerp(colour, _isRaining ? RainTint : SunTint, TintStrength * CueFade());
    }

    // Sun once enough has been delivered, then rain once it has cost enough dry trips.
    void UpdateState()
    {
        if (!_sunArmed && bucket.DeliveredCount >= BucketsBeforeSun)
        {
            _sunArmed = true;
            _tripsAtSunArm = bucket.LastDeliveredTrip;
        }

        if (_sunArmed && !_sunTriggered && bucket.TripsStarted > _tripsAtSunArm)
        {
            _sunTriggered = true;
            _dryHitsAtSun = bucket.HitGroundCount;
            GreenOffset = GreenOffset - 1;
            bucket.ForceDryTrips(DryTripsBeforeRain);
            Begin(false);
        }

        if (_sunTriggered && !_rainArmed &&
            bucket.HitGroundCount >= _dryHitsAtSun + DryTripsBeforeRain)
        {
            _rainArmed = true;
            _tripsAtRainArm = bucket.TripsStarted;
        }

        if (_rainArmed && !_rainTriggered && bucket.TripsStarted > _tripsAtRainArm)
        {
            _rainTriggered = true;
            GreenOffset = GreenOffset + 1;
            Begin(true);
        }
    }

    void UpdateCue()
    {
        if (_cue <= 0f)
            return;

        _cueElapsed += Time.deltaTime;
        _cue = Mathf.Max(0f, _cue - Time.deltaTime);

        if (_cue > 0f)
            return;

        _isRaining = false;

        if (_rainRoot != null)
            _rainRoot.SetActive(false);
        if (_sunRoot != null)
            _sunRoot.SetActive(false);
    }

    // Droops from the sun until the rain starts.
    void UpdateMood()
    {
        DroopTarget = _sunTriggered && !_rainTriggered ? 1f : 0f;
    }

    void Begin(bool rain)
    {
        _cue = CueDuration;
        _cueElapsed = 0f;
        _isRaining = rain;

        if (rain)
        {
            if (_sunRoot != null)
                _sunRoot.SetActive(false);
            ShowRain();
        }
        else
        {
            ShowSunRays();
        }
    }

    float CueFade()
    {
        float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_cueElapsed / FadeSeconds));
        float fadeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_cue / FadeSeconds));
        return fadeIn * fadeOut;
    }

    void ShowRain()
    {
        if (sky == null || block == null || block.sprite == null)
            return;

        if (_rainDrops == null)
        {
            _rainRoot = new GameObject("Rain");
            _rainRoot.transform.SetParent(transform, false);
            _rainDrops = new Transform[RainDropCount];

            for (int i = 0; i < _rainDrops.Length; i++)
            {
                var drop = new GameObject("Drop");
                drop.transform.SetParent(_rainRoot.transform, false);

                var renderer = drop.AddComponent<SpriteRenderer>();
                renderer.sprite = block.sprite;
                renderer.color = new Color(0.62f, 0.82f, 0.92f, 0.7f);
                renderer.sortingOrder = 100;
                drop.transform.localScale = new Vector3(0.035f, 0.18f, 1f);
                _rainDrops[i] = drop.transform;
            }
        }

        _rainRoot.SetActive(true);

        float width = sky.orthographicSize * sky.aspect;
        float top = sky.transform.position.y + sky.orthographicSize + 0.5f;
        float bottom = sky.transform.position.y - sky.orthographicSize;

        foreach (Transform drop in _rainDrops)
        {
            drop.gameObject.SetActive(true);
            drop.position = new Vector3(
                sky.transform.position.x + Random.Range(-width, width),
                Random.Range(bottom, top),
                0f);
        }
    }

    void ShowSunRays()
    {
        if (sky == null || block == null || block.sprite == null)
            return;

        if (_sunRays == null)
        {
            _sunRoot = new GameObject("Sun Rays");
            _sunRoot.transform.SetParent(transform, false);
            _sunRays = new SpriteRenderer[SunRayCount];

            for (int i = 0; i < _sunRays.Length; i++)
            {
                var ray = new GameObject("Ray");
                ray.transform.SetParent(_sunRoot.transform, false);

                var renderer = ray.AddComponent<SpriteRenderer>();
                renderer.sprite = block.sprite;
                renderer.sortingOrder = 32;
                _sunRays[i] = renderer;
            }
        }

        _sunRoot.SetActive(true);

        float halfWidth = sky.orthographicSize * sky.aspect;
        float halfHeight = sky.orthographicSize;
        Vector2 source = new Vector2(
            sky.transform.position.x + halfWidth * 0.9f,
            sky.transform.position.y + halfHeight + 0.4f);

        for (int i = 0; i < _sunRays.Length; i++)
        {
            float t = i / (float)(_sunRays.Length - 1);
            Vector2 target = new Vector2(
                sky.transform.position.x + Mathf.Lerp(-halfWidth * 1.1f, halfWidth * 0.25f, t),
                sky.transform.position.y - halfHeight * 0.7f);
            Vector2 direction = target - source;

            Transform ray = _sunRays[i].transform;
            ray.position = (source + target) * 0.5f;
            ray.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            ray.localScale = new Vector3(0.09f + (i % 2) * 0.04f, direction.magnitude, 1f);
        }
    }

    void UpdateSunRays()
    {
        if (_sunRays == null || _sunRoot == null || !_sunRoot.activeSelf)
            return;

        float alpha = 0.24f * CueFade();
        foreach (SpriteRenderer ray in _sunRays)
            ray.color = new Color(1f, 0.88f, 0.58f, alpha);
    }

    void UpdateRain()
    {
        if (!_isRaining || _rainDrops == null || sky == null)
            return;

        float width = sky.orthographicSize * sky.aspect;
        float top = sky.transform.position.y + sky.orthographicSize + 0.5f;
        float bottom = sky.transform.position.y - sky.orthographicSize;

        foreach (Transform drop in _rainDrops)
        {
            Vector3 position = drop.position;
            position.y -= 8f * Time.deltaTime;

            if (position.y < bottom)
            {
                position.x = sky.transform.position.x + Random.Range(-width, width);
                position.y = top;
            }

            drop.position = position;
        }
    }

    void OnDestroy()
    {
        if (_rainRoot != null)
            Destroy(_rainRoot);
        if (_sunRoot != null)
            Destroy(_sunRoot);
    }
}
