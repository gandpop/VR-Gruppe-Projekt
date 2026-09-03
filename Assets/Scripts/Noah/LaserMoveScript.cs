using UnityEngine;

/// <summary>
/// Makes the LaserMiner hover smoothly up and down on the Y-axis.
/// </summary>
public class LaserMoveScript : MonoBehaviour
{
    [Header("Hover Settings")]
    [Tooltip("Maximum distance the miner moves up and down from its starting position (amplitude).")]
    [SerializeField] private float hoverDistance = 0.4f;

    [Tooltip("Speed/frequency of the hovering motion.")]
    [SerializeField] private float hoverSpeed = 1.5f;

    [Tooltip("If true, randomizes the starting phase so multiple miners don't bob in exact sync.")]
    [SerializeField] private bool randomizePhase = true;

    [Tooltip("Manual phase offset in radians.")]
    [SerializeField] private float phaseOffset = 0f;

    [Tooltip("Use unscaled time so hovering continues even when Time.timeScale is altered.")]
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Optional Subtle Tilt")]
    [Tooltip("Adds a slight rotational sway/tilt while hovering for a more natural floating effect.")]
    [SerializeField] private bool enableSubtleTilt = true;

    [Tooltip("Maximum tilt angle in degrees on the X and Z axes.")]
    [SerializeField] private float maxTiltAngle = 1.5f;

    [Tooltip("Speed of the tilt motion.")]
    [SerializeField] private float tiltSpeed = 1.2f;

    [Header("Custom Curve (Optional)")]
    [Tooltip("If enabled, uses the custom animation curve below instead of a pure sine wave.")]
    [SerializeField] private bool useCustomCurve = false;

    [Tooltip("Custom hover curve normalized from time 0 to 1, outputting -1 to 1.")]
    [SerializeField] private AnimationCurve hoverCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 2f, 2f),
        new Keyframe(0.25f, 1f, 0f, 0f),
        new Keyframe(0.75f, -1f, 0f, 0f),
        new Keyframe(1f, 0f, 2f, 2f)
    );

    // Initial transform baselines
    private Vector3 _baseLocalPosition;
    private Quaternion _baseLocalRotation;
    private float _activePhaseOffset;

    /// <summary>
    /// The base local position around which the miner hovers.
    /// Can be updated externally if the miner is repositioned.
    /// </summary>
    public Vector3 BaseLocalPosition
    {
        get => _baseLocalPosition;
        set => _baseLocalPosition = value;
    }

    private void Awake()
    {
        CaptureBaseTransform();
        InitializePhase();
    }

    private void OnEnable()
    {
        // Re-capture base position on enable to ensure proper anchor if placed dynamically
        CaptureBaseTransform();
    }

    private void CaptureBaseTransform()
    {
        _baseLocalPosition = transform.localPosition;
        _baseLocalRotation = transform.localRotation;
    }

    private void InitializePhase()
    {
        _activePhaseOffset = phaseOffset;
        if (randomizePhase)
        {
            _activePhaseOffset += Random.Range(0f, Mathf.PI * 2f);
        }
    }

    private void Update()
    {
        float currentTime = useUnscaledTime ? Time.unscaledTime : Time.time;

        // Calculate normalized vertical bob factor (-1 to +1)
        float verticalFactor;
        if (useCustomCurve && hoverCurve != null)
        {
            float curveTime = Mathf.Repeat((currentTime * hoverSpeed) + _activePhaseOffset, 1f);
            verticalFactor = hoverCurve.Evaluate(curveTime);
        }
        else
        {
            // Smooth harmonic sine oscillation
            verticalFactor = Mathf.Sin((currentTime * hoverSpeed) + _activePhaseOffset);
        }

        // Apply Y-axis hovering offset
        float yOffset = verticalFactor * hoverDistance;
        transform.localPosition = new Vector3(_baseLocalPosition.x, _baseLocalPosition.y + yOffset, _baseLocalPosition.z);

        // Optional subtle tilt on X and Z axes
        if (enableSubtleTilt && maxTiltAngle > 0.001f)
        {
            float tiltTime = currentTime * tiltSpeed + _activePhaseOffset;
            float tiltX = Mathf.Sin(tiltTime) * maxTiltAngle;
            float tiltZ = Mathf.Cos(tiltTime * 0.85f) * (maxTiltAngle * 0.8f);

            transform.localRotation = _baseLocalRotation * Quaternion.Euler(tiltX, 0f, tiltZ);
        }
    }

    /// <summary>
    /// Resets the base anchor position to the current local position.
    /// Useful after moving the miner via script or animation.
    /// </summary>
    public void ResetBaseAnchor()
    {
        CaptureBaseTransform();
    }

    public void SetHoverDistance(float distance) => hoverDistance = distance;
    public void SetHoverSpeed(float speed) => hoverSpeed = speed;
    public void SetTiltEnabled(bool enabled) => enableSubtleTilt = enabled;
}
