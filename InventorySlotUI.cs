using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ActionRPG.Character.UI
{
    public class InventorySlotUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private GameObject equippedBadge;
        [SerializeField] private Button slotButton;

        private int slotIndex;
        private Action<int> onSlotClicked;

        public void Initialize(int index, Action<int> clickCallback)
        {
            slotIndex = index;
            onSlotClicked = clickCallback;

            if (slotButton != null)
            {
                slotButton.onClick.RemoveAllListeners();
                slotButton.onClick.AddListener(() => onSlotClicked?.Invoke(slotIndex));
            }
        }

        public void Bind(InventorySlot slot)
        {
            if (slot == null || slot.IsEmpty)
            {
                ClearSlot();
                return;
            }

            if (iconImage != null)
            {
                iconImage.enabled = slot.Item.Icon != null;
                iconImage.sprite = slot.Item.Icon;
            }

            if (quantityText != null)
            {
                quantityText.text = slot.Quantity > 1 ? slot.Quantity.ToString() : string.Empty;
            }

            if (equippedBadge != null)
            {
                equippedBadge.SetActive(slot.IsEquipped);
            }
        }

        public void ClearSlot()
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            if (quantityText != null)
            {
                quantityText.text = string.Empty;
            }

            if (equippedBadge != null)
            {
                equippedBadge.SetActive(false);
            }
        }
    }
}