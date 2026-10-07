using UnityEngine;

// The outro. Starts once, when the ground is fully green: pulls the camera back, drifts the
// clouds, sways the plants, plays the success sting, then hands over to the restart button.
// Clouds and sway keep running afterwards, so the results screen is not a frozen frame.
//
// Pull-back must stop above the dirt (bottom y = -6.8) or the sky shows through the floor.
// cloudDrift has to beat the zoom, or the right-hand clouds appear to drift backwards.
public class Ending : MonoBehaviour
{
    [SerializeField] GroundGreening greening;
    [SerializeField] RestartButton button;
    [SerializeField] Crank crank;

    [SerializeField] Camera view;
    [SerializeField] float duration = 10f;
    [SerializeField] float zoomOutTo = 4.2f;
    [SerializeField] float endY = -0.1f;

    // One entry per cloud, each a group of blocks. Wrapped individually, so they must not
    // share a parent that moves - the blocks of one cloud would wrap at different times.
    [SerializeField] Transform[] clouds;
    [SerializeField] float cloudDrift = 0.25f;
    [SerializeField] float cloudWrapMargin = 1.5f;

    [SerializeField] float swayDegrees = 15f;
    [SerializeField] float swaySpeed = 1.2f;

    [SerializeField] AudioSource victory;

    bool _started;
    bool _handedOver;
    float _elapsed;
    float _startSize;
    float _startY;

    void Update()
    {
        if (!_started)
        {
            // Waits for the pour to end, or Pouring.Finish would unlock the crank mid-outro.
            if (greening != null && greening.Green >= 1f && (crank == null || !crank.Locked))
                Begin();
            return;
        }

        _elapsed += Time.deltaTime;

        float t = Mathf.Clamp01(_elapsed / duration);

        if (t < 1f)
            MoveCamera(Smooth(t));

        DriftClouds();
        Sway();

        if (t >= 1f && !_handedOver)
        {
            _handedOver = true;

            if (button != null)
                button.Show();
        }
    }

    void Begin()
    {
        _started = true;
        _elapsed = 0f;

        if (crank != null)
            crank.Locked = true;

        if (view != null)
        {
            _startSize = view.orthographicSize;
            _startY = view.transform.position.y;
        }

        if (victory != null)
            victory.Play();
    }

    void MoveCamera(float k)
    {
        if (view == null)
            return;

        view.orthographicSize = Mathf.Lerp(_startSize, zoomOutTo, k);

        Vector3 position = view.transform.position;
        position.y = Mathf.Lerp(_startY, endY, k);
        view.transform.position = position;
    }

    // Moved a little each frame rather than from the elapsed time, so the wrap can just
    // subtract the full span instead of accumulating an ever-growing offset.
    void DriftClouds()
    {
        if (clouds == null || view == null)
            return;

        float wrapAt = view.orthographicSize * view.aspect + cloudWrapMargin;

        // Eased in over the first second so the drift does not start with a jolt.
        float ramp = Mathf.Clamp01(_elapsed);

        for (int i = 0; i < clouds.Length; i++)
        {
            if (clouds[i] == null)
                continue;

            Vector3 position = clouds[i].localPosition;
            position.x += cloudDrift * ramp * Time.deltaTime;

            if (position.x > wrapAt)
                position.x -= wrapAt * 2f;

            clouds[i].localPosition = position;
        }
    }

    // Rotates each plant about its base. Phased by index so they do not move as one, and
    // driven by elapsed time rather than the outro clock, so it carries on afterwards.
    void Sway()
    {
        Transform[] plants = greening != null ? greening.Plants : null;
        if (plants == null)
            return;

        for (int i = 0; i < plants.Length; i++)
        {
            if (plants[i] == null)
                continue;

            float angle = Mathf.Sin(_elapsed * swaySpeed + i * 0.7f) * swayDegrees;
            plants[i].localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);
}
