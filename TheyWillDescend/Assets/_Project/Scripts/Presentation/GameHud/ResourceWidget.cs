using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Session;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Displays fixed scene-authored resource chips and energy reservoir.
    /// Does not build UI dynamically — binds to inspector-assigned scene elements.
    /// </summary>
    public sealed class ResourceWidget : MonoBehaviour
    {
        [Header("Строительные материалы (Слева)")]
        [SerializeField] TMP_Text woodValue;
        [SerializeField] TMP_Text copalValue;
        [SerializeField] TMP_Text obsidianValue;
        [SerializeField] TMP_Text shardValue;

        [Header("Продовольствие (Справа)")]
        [SerializeField] TMP_Text cornValue;
        [SerializeField] TMP_Text meatValue;
        [SerializeField] TMP_Text livestockValue;
        [SerializeField] TMP_Text rationsValue;

        [Header("Энергия (По центру)")]
        [SerializeField] TMP_Text energyValue;
        [SerializeField] Image energyFill;
        [SerializeField] float defaultEnergyCap = 100f;

        static readonly FixedString64Bytes WoodId = new("wood");
        static readonly FixedString64Bytes CopalId = new("copal");
        static readonly FixedString64Bytes ObsidianId = new("obsidian");
        static readonly FixedString64Bytes ShardId = new("pyramid_shard");
        static readonly FixedString64Bytes CornId = new("food");
        static readonly FixedString64Bytes MeatId = new("raw_meat");
        static readonly FixedString64Bytes LivestockId = new("livestock");
        static readonly FixedString64Bytes RationsId = new("rations");
        static readonly FixedString64Bytes EnergyId = new("energy");

        // Public properties for setup / inspector validation
        public TMP_Text WoodValue { get => woodValue; set => woodValue = value; }
        public TMP_Text CopalValue { get => copalValue; set => copalValue = value; }
        public TMP_Text ObsidianValue { get => obsidianValue; set => obsidianValue = value; }
        public TMP_Text ShardValue { get => shardValue; set => shardValue = value; }

        public TMP_Text CornValue { get => cornValue; set => cornValue = value; }
        public TMP_Text MeatValue { get => meatValue; set => meatValue = value; }
        public TMP_Text LivestockValue { get => livestockValue; set => livestockValue = value; }
        public TMP_Text RationsValue { get => rationsValue; set => rationsValue = value; }

        public TMP_Text EnergyValue { get => energyValue; set => energyValue = value; }
        public Image EnergyFill { get => energyFill; set => energyFill = value; }

        void Update()
        {
            if (!SimWorld.TryGet(out var em, out var bag)
                || !em.HasBuffer<ResourceAmount>(bag))
                return;

            var stock = em.GetBuffer<ResourceAmount>(bag);

            // Left: Materials
            SetLabel(woodValue, ResourceLedger.Get(stock, WoodId));
            SetLabel(copalValue, ResourceLedger.Get(stock, CopalId));
            SetLabel(obsidianValue, ResourceLedger.Get(stock, ObsidianId));
            SetLabel(shardValue, ResourceLedger.Get(stock, ShardId));

            // Right: Provisions
            SetLabel(cornValue, ResourceLedger.Get(stock, CornId));
            SetLabel(meatValue, ResourceLedger.Get(stock, MeatId));
            SetLabel(livestockValue, ResourceLedger.Get(stock, LivestockId));
            SetLabel(rationsValue, ResourceLedger.Get(stock, RationsId));

            // Center: Energy
            var energyAmount = ResourceLedger.Get(stock, EnergyId);
            SetLabel(energyValue, energyAmount);

            if (energyFill != null)
            {
                var cap = defaultEnergyCap;
                if (em.HasBuffer<ResourceInfo>(bag))
                {
                    var infoCap = ResourceLedger.StockCap(em.GetBuffer<ResourceInfo>(bag), EnergyId);
                    if (infoCap > 0.001f)
                        cap = infoCap;
                }
                energyFill.fillAmount = Mathf.Clamp01(energyAmount / cap);
            }
        }

        static void SetLabel(TMP_Text label, float amount)
        {
            if (label != null)
                label.text = Mathf.FloorToInt(amount).ToString();
        }
    }
}
