using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ActionRPG.Character.UI
{
    public class AttributeRowUI : MonoBehaviour
    {
        [SerializeField] private AttributeType attributeType;
        [SerializeField] private TextMeshProUGUI labelText;
        [SerializeField] private TextMeshProUGUI valueText;
        [SerializeField] private Button allocateButton;

        public AttributeType Type => attributeType;

        public void Initialize(AttributeType type, Action<AttributeType> onAllocateClicked)
        {
            attributeType = type;

            if (labelText != null)
                labelText.text = FormatAttributeName(type);

            if (allocateButton != null)
            {
                allocateButton.onClick.RemoveAllListeners();
                allocateButton.onClick.AddListener(() => onAllocateClicked?.Invoke(attributeType));
            }
        }

        public void Refresh(int totalValue, int bonusValue, bool canAllocate)
        {
            if (labelText != null && string.IsNullOrEmpty(labelText.text))
                labelText.text = FormatAttributeName(attributeType);

            if (valueText != null)
            {
                valueText.text = bonusValue > 0 ? $"{totalValue} (+{bonusValue})" : totalValue.ToString();
            }

            if (allocateButton != null)
            {
                allocateButton.interactable = canAllocate;
            }
        }

        private string FormatAttributeName(AttributeType type)
        {
            return type == AttributeType.LifeRegen ? "Life Regen" : type.ToString();
        }
    }
}