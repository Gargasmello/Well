using UnityEngine;

// Hand cursor. Sprites come from Kenney's Cursor Pack (CC0), imported as Resources/HandOpen.png
// and Resources/HandClosed.png. Crank creates one and drives it with Follow, Grab and Hide.
// Order 33, above the wheel: CrankArm and CrankGrip are 31, the beam is 32.
public class Hand : MonoBehaviour
{
    const int HandOrder = 33;

    // Metres across, which at 720p is about 32 screen pixels.
    const float HandMetres = 0.30f;

    SpriteRenderer _renderer;
    Sprite _open;
    Sprite _closed;
    bool _shown;

    public bool Shown => _shown;

    void Awake()
    {
        _open = Resources.Load<Sprite>("HandOpen");
        _closed = Resources.Load<Sprite>("HandClosed");

        if (_open == null || _closed == null)
            Debug.LogWarning("Hand: HandOpen or HandClosed is missing from Assets/Resources.");

        _renderer = gameObject.AddComponent<SpriteRenderer>();
        _renderer.sortingOrder = HandOrder;
        _renderer.sprite = _open;
        _renderer.enabled = false;

        // The sprites are imported at their own scale, so the transform carries the size.
        float width = _open != null ? _open.bounds.size.x : 1f;
        transform.localScale = Vector3.one * (HandMetres / Mathf.Max(width, 0.0001f));
    }

    // Open hand at the pointer, where the wheel can be grabbed.
    public void Follow(Vector3 worldPosition)
    {
        SetPose(_open, worldPosition, 0f);
    }

    // Fist, stuck on the wheel where it was grabbed.
    public void Grab(Vector3 worldPosition)
    {
        SetPose(_closed, worldPosition, 0f);
    }

    public void Hide()
    {
        if (!_shown)
            return;

        _shown = false;
        _renderer.enabled = false;
    }

    void SetPose(Sprite sprite, Vector3 worldPosition, float tilt)
    {
        if (!_shown)
        {
            _shown = true;
            _renderer.enabled = true;
        }

        // World space: the wheel turns underneath, the hand does not inherit its spin.
        _renderer.sprite = sprite;
        transform.position = worldPosition;
        transform.rotation = Quaternion.Euler(0f, 0f, tilt);
    }
}
