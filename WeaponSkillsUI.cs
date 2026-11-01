using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace ActionRPG.Character.UI
{
    public class WeaponSkillsUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerSkillProgression skillProgression;

        [Header("Left Column: Weapon Mastery (0 - 100)")]
        [SerializeField] private Transform masteryListContainer;
        [SerializeField] private SkillRowUI masteryRowPrefab;

        [Header("Right Column: Active Skills for Selected Weapon")]
        [SerializeField] private TextMeshProUGUI selectedCategoryTitleText;
        [SerializeField] private Transform activeSkillListContainer;
        [SerializeField] private WeaponSkillEntryUI activeSkillEntryPrefab;

        private readonly Dictionary<WeaponSkillType, SkillRowUI> masteryRows = new Dictionary<WeaponSkillType, SkillRowUI>();
        private readonly List<WeaponSkillEntryUI> spawnedSkillEntries = new List<WeaponSkillEntryUI>();
        private WeaponSkillType currentInspectedWeapon = WeaponSkillType.Sword;

        private void Awake()
        {
            if (skillProgression == null)
                skillProgression = FindFirstObjectByType<PlayerSkillProgression>();

            BuildMasteryRowsIfNeeded();
        }

        private void OnEnable()
        {
            if (skillProgression == null) return;

            skillProgression.OnWeaponSkillChanged += HandleWeaponMasteryChanged;
            skillProgression.OnActiveWeaponSkillSelected += HandleActiveSkillSelected;

            BuildMasteryRowsIfNeeded();
            RefreshAllMasteryRows();
            InspectWeaponCategory(currentInspectedWeapon);
        }

        private void OnDisable()
        {
            if (skillProgression == null) return;

            skillProgression.OnWeaponSkillChanged -= HandleWeaponMasteryChanged;
            skillProgression.OnActiveWeaponSkillSelected -= HandleActiveSkillSelected;
        }

        private void BuildMasteryRowsIfNeeded()
        {
            if (masteryRows.Count > 0 || masteryRowPrefab == null || masteryListContainer == null) return;

            foreach (WeaponSkillType type in Enum.GetValues(typeof(WeaponSkillType)))
            {
                WeaponSkillType capturedType = type;
                SkillRowUI row = Instantiate(masteryRowPrefab, masteryListContainer);
                string displayName = FormatWeaponName(type);

                row.Initialize(displayName, () =>
                {
                    skillProgression.AddWeaponSkillXP(capturedType, 35f);
                    InspectWeaponCategory(capturedType);
                });

                masteryRows[type] = row;
            }
        }

        public void InspectWeaponCategory(WeaponSkillType weaponType)
        {
            currentInspectedWeapon = weaponType;

            if (selectedCategoryTitleText != null)
            {
                selectedCategoryTitleText.text = $"{FormatWeaponName(weaponType)} Skills";
            }

            RefreshActiveSkillsList();
        }

        private void HandleWeaponMasteryChanged(WeaponSkillType type, SkillProgressEntry entry)
        {
            if (masteryRows.TryGetValue(type, out SkillRowUI row))
            {
                row.Refresh(entry);
            }

            if (type == currentInspectedWeapon)
            {
                RefreshActiveSkillsList();
            }
        }

        private void HandleActiveSkillSelected(ActiveWeaponSkillDefinition selectedSkill)
        {
            RefreshActiveSkillsList();
        }

        private void RefreshAllMasteryRows()
        {
            if (skillProgression == null) return;

            foreach (var kvp in masteryRows)
            {
                kvp.Value.Refresh(skillProgression.GetWeaponSkill(kvp.Key));
            }
        }

        private void RefreshActiveSkillsList()
        {
            if (skillProgression == null || activeSkillEntryPrefab == null || activeSkillListContainer == null)
                return;

            List<ActiveWeaponSkillDefinition> skills = skillProgression.GetSkillsForWeapon(currentInspectedWeapon);

            while (spawnedSkillEntries.Count < skills.Count)
            {
                WeaponSkillEntryUI newEntry = Instantiate(activeSkillEntryPrefab, activeSkillListContainer);
                spawnedSkillEntries.Add(newEntry);
            }

            for (int i = 0; i < spawnedSkillEntries.Count; i++)
            {
                if (i < skills.Count)
                {
                    spawnedSkillEntries[i].gameObject.SetActive(true);
                    ActiveWeaponSkillDefinition skill = skills[i];
                    bool unlocked = skillProgression.IsActiveSkillUnlocked(skill);
                    bool isSelected = skillProgression.SelectedActiveSkill == skill;

                    spawnedSkillEntries[i].Bind(skill, unlocked, isSelected, HandleSkillSelectClicked);
                }
                else
                {
                    spawnedSkillEntries[i].gameObject.SetActive(false);
                }
            }
        }

        private void HandleSkillSelectClicked(ActiveWeaponSkillDefinition skill)
        {
            skillProgression.SelectActiveWeaponSkill(skill);
        }

        private string FormatWeaponName(WeaponSkillType type)
        {
            return type switch
            {
                WeaponSkillType.TwoHandSword => "Two-Hand Sword",
                WeaponSkillType.TwoHandAxe => "Two-Hand Axe",
                _ => type.ToString()
            };
        }
    }
}