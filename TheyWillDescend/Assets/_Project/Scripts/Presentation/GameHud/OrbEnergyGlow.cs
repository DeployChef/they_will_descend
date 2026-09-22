using System;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Drives the energy orb shader from the simulation energy reservoir.
    ///
    /// One normalized energy value (0..1) is smoothed once and then fed through a
    /// per-property AnimationCurve: X is the normalized energy, Y is the number sent to
    /// the shader. That is what solves "one property must rise while another falls" —
    /// direction and range live in the curve keys, not in code. A curve can also rise and
    /// then fall, which no single Remap would express.
    ///
    /// The scene keeps referencing the original material asset: the component is an
    /// IMaterialModifier, so rendering uses a private writable instance instead.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    public sealed class OrbEnergyGlow : MonoBehaviour, IMaterialModifier
    {
        [Serializable]
        public struct PropertyBinding
        {
            [Tooltip("Shader property reference, e.g. _CoreStrength")]
            public string property;
             
            [Tooltip("X = normalized energy (0..1), Y = value sent to the shader. Make Y go down to invert the property.")]
            public AnimationCurve curve;

            [Tooltip("Off keeps the property at whatever the material asset defines.")]
            public bool enabled;
        }

        [Header("Shader properties")]
        [SerializeField] PropertyBinding[] bindings = CreateDefaultBindings();

        [Header("Color")]
        [Tooltip("Off leaves the material Color alone. Tick it once the tint should move with energy.")]
        [SerializeField] bool useColor;
        [SerializeField] string colorProperty = "_Color";
        [Tooltip("Color at an empty reservoir.")]
        [SerializeField] Color lowColor = new(0f, 0.4547f, 0.6415f, 1f);
        [Tooltip("Color at a full reservoir.")]
        [SerializeField] Color highColor = new(0.35f, 0.9f, 1f, 1f);

        [Header("Smoothing")]
        [Tooltip("Seconds the glow needs to catch up with an energy change. 0 applies it instantly.")]
        [SerializeField] float smoothTime = 0.3f;
        [SerializeField] bool useUnscaledTime;

        [Header("Energy source")]
        [Tooltip("Ticked and the simulation world exists: energy comes from the reservoir, the slider below is ignored. Unticked: the slider decides.")]
        [SerializeField] bool driveFromSimulation = true;
        [Tooltip("Used only while Drive From Simulation is unticking, or while the simulation world does not exist yet (Edit Mode included).")]
        [SerializeField, Range(0f, 1f)] float manualEnergy01 = 1f;
        [Tooltip("Reservoir cap used when the simulation carries none, same role as in ResourceWidget.")]
        [SerializeField] float fallbackEnergyCap = 100f;

        [Header("Working range (absolute energy units)")]
        [Tooltip("Energy at and below this counts as an empty orb. 0 = no offset.")]
        [SerializeField, Min(0f)] float rangeMin;
        [Tooltip("Energy at and above this counts as a full orb. 0 = the reservoir cap from the simulation. Example: the game starts with 50 energy out of a 2000 cap, so the default cap would pin the orb near empty — set 100 here to sweep the whole glow between 0 and 100 energy.")]
        [SerializeField, Min(0f)] float rangeMax = 100f;


        Material _instance;
        Material _source;
        int[] _ids;
        bool[] _hasId;
        int _colorId;
        bool _hasColorId;
        float _smoothed;
        float _velocity;

        void Awake()
        {
            ResolveIds();
        }

        void OnValidate()
        {
            // Runs on every inspector edit, also before Awake — keep it allocation light and
            // never touch materials here.
            if (bindings != null)
            {
                for (var i = 0; i < bindings.Length; i++)
                {
                    var binding = bindings[i];
                    if (IsEmpty(binding.curve))
                    {
                        binding.curve = CreateLinearCurve();
                        bindings[i] = binding;
                    }
                }
            }

            ResolveIds();
        }

        void LateUpdate()
        {
            var value = Smooth(ResolveTarget());
            Apply(value);
        }

        void OnDestroy()
        {
            ReleaseInstance();
        }

        /// <summary>
        /// Reloads the built-in curve set. A component already in the scene keeps whatever was
        /// serialized into it, so this is the way to pick up new defaults without re-adding it.
        /// </summary>
        [ContextMenu("Restore Default Curves")]
        void RestoreDefaultCurves()
        {
            bindings = CreateDefaultBindings();
            ResolveIds();
        }

        /// <summary>
        /// Prints what the component currently reads from the simulation — the fastest way to
        /// tell "shader not reacting" from "shader reacting but pinned at one end".
        /// </summary>
        [ContextMenu("Debug: Log Current Energy")]
        void DebugLogCurrentEnergy()
        {
            if (!EnergyReadout.TryGet(fallbackEnergyCap, out var amount, out var cap, out _))
            {
                Debug.Log($"{name}: simulation world not available, manual slider is in charge.", this);
                return;
            }

            Debug.Log(
                $"{name}: energy {amount:F1} / cap {cap:F1}, working range {rangeMin:F1}..{(rangeMax > 0.0001f ? rangeMax : cap):F1} " +
                $"-> normalized {Remap(amount, cap):F3}, smoothed {_smoothed:F3}", this);
        }

        /// <summary>
        /// Mirrors every curve on the energy axis, so the look that belonged to a full reservoir
        /// moves to an empty one and the other way around. Curve shapes survive, only their
        /// direction flips.
        /// </summary>
        [ContextMenu("Flip Curve Direction")]
        void FlipCurveDirection()
        {
            if (bindings == null)
                return;

            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (IsEmpty(binding.curve))
                    continue;

                binding.curve = Mirror(binding.curve);
                bindings[i] = binding;
            }
        }

        static AnimationCurve Mirror(AnimationCurve source)
        {
            var count = source.length;
            var first = source[0].time;
            var last = source[count - 1].time;
            var keys = new Keyframe[count];

            // Walking backwards keeps the keys time-ordered after mirroring. Tangents swap sides
            // and change sign because the slope is taken against a reversed axis. The span of the
            // curve is preserved, so a curve authored outside 0..1 stays where it was authored.
            for (var i = 0; i < count; i++)
            {
                var key = source[count - 1 - i];
                keys[i] = new Keyframe(first + last - key.time, key.value, -key.outTangent, -key.inTangent);
            }

            var mirrored = new AnimationCurve(keys);
            for (var i = 0; i < count; i++)
            {
                var key = mirrored[i];
                key.inTangent = keys[i].inTangent;
                key.outTangent = keys[i].outTangent;
                mirrored.MoveKey(i, key);
            }

            return mirrored;
        }

        /// <summary>
        /// Hands the private material instance to the Graphic instead of the shared asset.
        /// </summary>
        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (baseMaterial == null)
                return baseMaterial;

            if (_instance != null && _source == baseMaterial)
                return _instance;

            ReleaseInstance();
            _source = baseMaterial;
            _instance = new Material(baseMaterial)
            {
                name = baseMaterial.name + " (energy glow)"
            };
            return _instance;
        }

        float ResolveTarget()
        {
            if (driveFromSimulation
                && EnergyReadout.TryGet(fallbackEnergyCap, out var amount, out var cap, out _))
                return Remap(amount, cap);

            // Edit Mode and the frames before the world exists fall back to the slider,
            // so the curves stay tunable without running the simulation.
            return Mathf.Clamp01(manualEnergy01);
        }

        /// <summary>
        /// Maps the raw energy amount onto 0..1 through the working range. The reservoir cap
        /// (2000 by the default rules) is usually far above the energy the game actually runs at,
        /// so dividing by it would pin the orb near empty no matter how the energy jumps.
        /// </summary>
        float Remap(float amount, float cap)
        {
            var max = rangeMax > 0.0001f ? rangeMax : cap;
            if (max <= rangeMin + 0.0001f)
                return 0f;

            return Mathf.Clamp01((amount - rangeMin) / (max - rangeMin));
        }

        float Smooth(float target)
        {
            var deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (smoothTime <= 0.0001f || deltaTime <= 0f)
            {
                _smoothed = target;
                _velocity = 0f;
                return _smoothed;
            }

            _smoothed = Mathf.SmoothDamp(_smoothed, target, ref _velocity, smoothTime, float.MaxValue, deltaTime);
            return _smoothed;
        }

        void Apply(float energy01)
        {
            if (_instance == null)
                return;

            if (_ids == null || _ids.Length != (bindings?.Length ?? 0))
                ResolveIds();

            if (bindings != null)
            {
                for (var i = 0; i < bindings.Length; i++)
                {
                    var binding = bindings[i];
                    if (!binding.enabled || !_hasId[i] || IsEmpty(binding.curve))
                        continue;

                    _instance.SetFloat(_ids[i], binding.curve.Evaluate(energy01));
                }
            }

            if (useColor && _hasColorId)
                _instance.SetColor(_colorId, Color.Lerp(lowColor, highColor, energy01));
        }

        void ResolveIds()
        {
            var count = bindings?.Length ?? 0;

            if (_ids == null || _ids.Length != count)
            {
                _ids = new int[count];
                _hasId = new bool[count];
            }

            for (var i = 0; i < count; i++)
            {
                _hasId[i] = !string.IsNullOrEmpty(bindings[i].property);
                if (_hasId[i])
                    _ids[i] = Shader.PropertyToID(bindings[i].property);
            }

            _hasColorId = !string.IsNullOrEmpty(colorProperty);
            if (_hasColorId)
                _colorId = Shader.PropertyToID(colorProperty);
        }

        void ReleaseInstance()
        {
            if (_instance == null)
                return;

            if (Application.isPlaying)
                Destroy(_instance);
            else
                DestroyImmediate(_instance);

            _instance = null;
            _source = null;
        }

        static bool IsEmpty(AnimationCurve curve)
        {
            return curve == null || curve.length == 0;
        }

        static AnimationCurve CreateLinearCurve()
        {
            return new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
        }

        /// <summary>
        /// Tuned for the OrbSG UI properties: Y of the first key is what the material shows at an
        /// empty reservoir, Y of the second key at a full one. Properties that read the same at
        /// both ends stay disabled so the material asset keeps ownership of them.
        /// </summary>
        static PropertyBinding[] CreateDefaultBindings()
        {
            return new[]
            {
                new PropertyBinding
                {
                    property = "_DirectionalSpeed",
                    curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0.2f)),
                    enabled = true
                },
                new PropertyBinding
                {
                    property = "_NoiseContrast",
                    curve = new AnimationCurve(new Keyframe(0f, 0.37f), new Keyframe(1f, 2f)),
                    enabled = true
                },
                new PropertyBinding
                {
                    property = "_Noise2Contrast",
                    curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 10f)),
                    enabled = true
                },
                new PropertyBinding
                {
                    property = "_CoreContrast",
                    curve = new AnimationCurve(new Keyframe(0f, 1.44f), new Keyframe(1f, 2.02f)),
                    enabled = true
                },
                // Identical at both ends, kept from the material asset:
                // DistrortionStrength 0, Speed 0.07, CoreStrength 0, Contrast 2.
                new PropertyBinding
                {
                    property = "_DistrortionStrength",
                    curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f)),
                    enabled = false
                },
                new PropertyBinding
                {
                    property = "_Speed",
                    curve = new AnimationCurve(new Keyframe(0f, 0.07f), new Keyframe(1f, 0.07f)),
                    enabled = false
                },
                new PropertyBinding
                {
                    property = "_CoreStrength",
                    curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f)),
                    enabled = false
                },
                new PropertyBinding
                {
                    property = "_Contrast",
                    curve = new AnimationCurve(new Keyframe(0f, 2f), new Keyframe(1f, 2f)),
                    enabled = false
                }
            };
        }
    }
}
