using UnityEngine;
using UnityEngine.UI;

namespace Tycoon
{
    /// <summary>
    /// Runtime-built 3D building: keeps its own tier-colored body/roof/windows
    /// untouched, and instead reflects ownership state via a small colored
    /// ground plate under it. Its child primitives each carry a Collider (from
    /// GameObject.CreatePrimitive), so click raycasts resolve up to this
    /// component via GetComponentInParent from GameManager.
    /// </summary>
    public class PropertyTileView : MonoBehaviour
    {
        public int index;
        public Renderer statusPlate;

        /// <summary>Small UI price tag above the building (World Space Canvas),
        /// matching the reference game's compact rounded-pill-with-badge look.
        /// Content is owned by PlotManager.RefreshPriceLabel(), since it
        /// needs economy knowledge (rent rate, market value) this view doesn't have.</summary>
        public Image priceTagPill;
        public Image priceTagBadge;
        public Text priceTagArrow;
        public Text priceTagText;

        PropertyState state;

        public void Bind(PropertyState propertyState)
        {
            state = propertyState;
            state.view = this;
            // Not calling Refresh() here: statusPlate doesn't exist yet at bind
            // time — PlotManager.RerollPlot() builds the mesh right after
            // binding and calls Refresh() itself once it's ready.
        }

        public void Refresh()
        {
            Color c;
            switch (state.ownership)
            {
                case PropertyOwnership.Rented: c = new Color(0.35f, 0.7f, 0.95f); break;
                case PropertyOwnership.Owned: c = new Color(0.3f, 0.85f, 0.35f); break;
                case PropertyOwnership.NeedsDecision: c = new Color(1f, 0.5f, 0.15f); break;
                default: c = new Color(0.55f, 0.55f, 0.5f); break;
            }
            statusPlate.material.color = c;
        }
    }
}
