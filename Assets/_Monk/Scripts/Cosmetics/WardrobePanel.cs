using UnityEngine;

namespace Monk.Cosmetics
{
    /// <summary>Shows the currently equipped option next to each wardrobe row.</summary>
    public class WardrobePanel : MonoBehaviour
    {
        public AvatarCosmetics cosmetics;
        [Tooltip("In CosmeticSlot order: hat, visor, badge, suit.")]
        public TextMesh[] valueTexts;

        private void OnEnable()
        {
            if (cosmetics != null) cosmetics.Changed += Refresh;
        }

        private void OnDisable()
        {
            if (cosmetics != null) cosmetics.Changed -= Refresh;
        }

        private void Start() => Refresh();

        private void Refresh()
        {
            for (int i = 0; i < valueTexts.Length && i < 4; i++)
            {
                if (valueTexts[i] == null) continue;
                var slot = (CosmeticSlot)i;
                valueTexts[i].text = CosmeticModels.NameOf(slot, cosmetics.Get(slot));
            }
        }
    }
}
