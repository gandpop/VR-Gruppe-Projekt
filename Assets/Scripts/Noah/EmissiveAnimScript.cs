using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animates the emissive map intensity on a model's material (such as the Pillar model),
/// creating a pulsating glow effect.
/// </summary>
public class EmissiveAnimScript : MonoBehaviour
{
    public enum TargetMaterialMode
    {
        MaterialsWithEmissiveTexture,
        AllEmissiveMaterials,
        SpecificMaterialIndex
    }

    [Header("Target & Texture")]
    [Tooltip("The renderer of the model. If not assigned, will be auto-detected on this GameObject or its children.")]
    [SerializeField] private Renderer targetRenderer;

    [Tooltip("Emissive map texture from the pillar model. If left empty, it will be automatically retrieved from the model's material.")]
    [SerializeField] private Texture emissiveMap;

    [Tooltip("Which materials on the renderer should be animated.")]
    [SerializeField] private TargetMaterialMode materialMode = TargetMaterialMode.MaterialsWithEmissiveTexture;

    [Tooltip("Index of material to animate if TargetMaterialMode is set to SpecificMaterialIndex.")]
    [SerializeField] private int specificMaterialIndex = 1;

    [Header("Color Settings")]
    [Tooltip("Use the material's existing emission color as the base color and intensity.")]
    [SerializeField] private bool useMaterialEmissionColor = true;

    [Tooltip("Custom base emission color used when useMaterialEmissionColor is false, or if material had no emission color.")]
    [ColorUsage(true, true)]
    [SerializeField] private Color customEmissionColor = Color.white;

    [Header("Pulsation Settings")]
    [Tooltip("Minimum intensity multiplier during the pulse.")]
    [SerializeField] private float minIntensity = 0.2f;

    [Tooltip("Maximum intensity multiplier during the pulse.")]
    [SerializeField] private float maxIntensity = 1.5f;

    [Tooltip("Speed of the pulse cycle.")]
    [SerializeField] private float pulseSpeed = 2.0f;

    [Tooltip("If true, uses the custom animation curve below instead of a smooth sine wave.")]
    [SerializeField] private bool useCustomCurve = false;

    [Tooltip("Custom pulsation curve evaluated over normalized time (0 to 1).")]
    [SerializeField] private AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Optional Light Sync")]
    [Tooltip("Optional light component (e.g. child Point Light) to pulsate in sync with the emissive map.")]
    [SerializeField] private Light targetLight;

    [Tooltip("Whether to sync the light's intensity with the emissive pulse.")]
    [SerializeField] private bool syncLightIntensity = true;

    [Tooltip("Minimum light intensity.")]
    [SerializeField] private float minLightIntensity = 5.0f;

    [Tooltip("Maximum light intensity.")]
    [SerializeField] private float maxLightIntensity = 25.0f;

    // Shader property IDs
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");

    private class AnimatedMaterial
    {
        public Material MaterialInstance;
        public Color BaseEmissionColor;
    }

    private readonly List<AnimatedMaterial> _animatedMaterials = new List<AnimatedMaterial>();

    private void Awake()
    {
        InitializeRenderer();
        InitializeMaterials();
        InitializeLight();
    }

    private void InitializeRenderer()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }
        }
    }

    private void InitializeMaterials()
    {
        if (targetRenderer == null)
        {
            Debug.LogWarning($"[EmissiveAnimScript] No Renderer found on '{name}' or its children.", this);
            return;
        }

        // Accessing renderer.materials creates instantiated instances for runtime modification
        Material[] materials = targetRenderer.materials;
        if (materials == null || materials.Length == 0)
        {
            Debug.LogWarning($"[EmissiveAnimScript] Renderer on '{name}' has no materials.", this);
            return;
        }

        // First pass: if emissiveMap wasn't explicitly assigned, try to find an existing one on any material
        if (emissiveMap == null)
        {
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && materials[i].HasProperty(EmissionMapId))
                {
                    Texture tex = materials[i].GetTexture(EmissionMapId);
                    if (tex != null)
                    {
                        emissiveMap = tex;
                        break;
                    }
                }
            }
        }

        // Second pass: determine which materials to animate
        for (int i = 0; i < materials.Length; i++)
        {
            Material mat = materials[i];
            if (mat == null) continue;

            bool shouldAnimate = false;
            switch (materialMode)
            {
                case TargetMaterialMode.MaterialsWithEmissiveTexture:
                    // Matches if it currently has an emissive map or if emissiveMap is assigned
                    Texture curTex = mat.HasProperty(EmissionMapId) ? mat.GetTexture(EmissionMapId) : null;
                    if (curTex != null || (emissiveMap != null && curTex == emissiveMap))
                    {
                        shouldAnimate = true;
                    }
                    break;

                case TargetMaterialMode.AllEmissiveMaterials:
                    shouldAnimate = mat.HasProperty(EmissionColorId);
                    break;

                case TargetMaterialMode.SpecificMaterialIndex:
                    shouldAnimate = (i == specificMaterialIndex);
                    break;
            }

            if (shouldAnimate)
            {
                RegisterMaterial(mat);
            }
        }

        // Fallback: if MaterialsWithEmissiveTexture was selected but no material matched,
        // animate any material that supports emission
        if (_animatedMaterials.Count == 0 && materialMode == TargetMaterialMode.MaterialsWithEmissiveTexture)
        {
            for (int j = 0; j < materials.Length; j++)
            {
                if (materials[j] != null && materials[j].HasProperty(EmissionColorId))
                {
                    RegisterMaterial(materials[j]);
                }
            }
        }
    }

    private void RegisterMaterial(Material mat)
    {
        // Enable emission keyword for URP and Standard shaders
        mat.EnableKeyword("_EMISSION");

        // Assign emissive map texture if provided and material supports it
        if (emissiveMap != null && mat.HasProperty(EmissionMapId))
        {
            mat.SetTexture(EmissionMapId, emissiveMap);
        }

        // Determine base emission color
        Color baseColor = customEmissionColor;
        if (useMaterialEmissionColor && mat.HasProperty(EmissionColorId))
        {
            Color matColor = mat.GetColor(EmissionColorId);
            // If the material has an existing non-black emission color, use it
            if (matColor.maxColorComponent > 0.001f)
            {
                baseColor = matColor;
            }
        }

        _animatedMaterials.Add(new AnimatedMaterial
        {
            MaterialInstance = mat,
            BaseEmissionColor = baseColor
        });
    }

    private void InitializeLight()
    {
        if (targetLight == null)
        {
            targetLight = GetComponentInChildren<Light>();
        }

        if (targetLight != null && Mathf.Approximately(minLightIntensity, 5.0f) && Mathf.Approximately(maxLightIntensity, 25.0f))
        {
            // Auto-scale light range around the light's current intensity if default values are untouched
            float current = targetLight.intensity;
            if (current > 0f)
            {
                minLightIntensity = current * 0.25f;
                maxLightIntensity = current * 1.25f;
            }
        }
    }

    private void Update()
    {
        if (_animatedMaterials.Count == 0 && targetLight == null) return;

        // Calculate pulsation factor (0 to 1)
        float normalizedPulse;
        if (useCustomCurve && pulseCurve != null)
        {
            float curveTime = Mathf.Repeat(Time.time * pulseSpeed, 1f);
            normalizedPulse = Mathf.Clamp01(pulseCurve.Evaluate(curveTime));
        }
        else
        {
            // Smooth sine wave oscillating between 0 and 1
            normalizedPulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        }

        // Interpolate emission multiplier
        float currentMultiplier = Mathf.Lerp(minIntensity, maxIntensity, normalizedPulse);

        // Apply emission to materials
        for (int i = 0; i < _animatedMaterials.Count; i++)
        {
            AnimatedMaterial animMat = _animatedMaterials[i];
            if (animMat.MaterialInstance != null)
            {
                Color animatedColor = animMat.BaseEmissionColor * currentMultiplier;
                animMat.MaterialInstance.SetColor(EmissionColorId, animatedColor);
            }
        }

        // Sync light if active
        if (syncLightIntensity && targetLight != null)
        {
            targetLight.intensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, normalizedPulse);
        }
    }

    private void OnDestroy()
    {
        // Clean up instantiated materials when destroyed at runtime
        for (int i = 0; i < _animatedMaterials.Count; i++)
        {
            if (_animatedMaterials[i].MaterialInstance != null)
            {
                Destroy(_animatedMaterials[i].MaterialInstance);
            }
        }
        _animatedMaterials.Clear();
    }

    // Public API for scripting / events
    public void SetEmissiveMap(Texture texture)
    {
        emissiveMap = texture;
        for (int i = 0; i < _animatedMaterials.Count; i++)
        {
            if (_animatedMaterials[i].MaterialInstance != null && _animatedMaterials[i].MaterialInstance.HasProperty(EmissionMapId))
            {
                _animatedMaterials[i].MaterialInstance.SetTexture(EmissionMapId, emissiveMap);
            }
        }
    }

    public void SetPulseSpeed(float speed) => pulseSpeed = speed;

    public void SetIntensityRange(float min, float max)
    {
        minIntensity = min;
        maxIntensity = max;
    }

    public void SetBaseEmissionColor(Color color)
    {
        for (int i = 0; i < _animatedMaterials.Count; i++)
        {
            _animatedMaterials[i].BaseEmissionColor = color;
        }
    }
}
