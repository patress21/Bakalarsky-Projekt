using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ActionRPG.Character.UI
{
    public class WeaponSkillEntryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI skillNameText;
        [SerializeField] private TextMeshProUGUI requirementText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Button selectButton;
        [SerializeField] private TextMeshProUGUI selectButtonLabel;

        private ActiveWeaponSkillDefinition boundSkill;
        private Action<ActiveWeaponSkillDefinition> onSelectClicked;

        public void Bind(
            ActiveWeaponSkillDefinition skill,
            bool isUnlocked,
            bool isSelected,
            Action<ActiveWeaponSkillDefinition> selectCallback)
        {
            boundSkill = skill;
            onSelectClicked = selectCallback;

            if (skillNameText != null)
                skillNameText.text = skill.SkillName;

            if (requirementText != null)
            {
                requirementText.text = isUnlocked
                    ? $"MP: {skill.MPCost:F0} | Stamina: {skill.StaminaCost:F0}"
                    : $"Requires {skill.WeaponCategory} Mastery Lv. {skill.RequiredMasteryLevel}";
            }

            if (descriptionText != null)
                descriptionText.text = skill.Description;

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.interactable = isUnlocked && !isSelected;
                selectButton.onClick.AddListener(() => onSelectClicked?.Invoke(boundSkill));
            }

            if (selectButtonLabel != null)
            {
                if (!isUnlocked)
                    selectButtonLabel.text = "Locked";
                else if (isSelected)
                    selectButtonLabel.text = "Equipped";
                else
                    selectButtonLabel.text = "Select";
            }
        }
    }
}