using System;
using System.Collections.Generic;
using UnityEngine;

namespace ActionRPG.Character
{
    public enum PassiveSkillType
    {
        Mining,
        Swimming,
        Acrobatics,
        Blacksmithing,
        Cooking
    }

    [Serializable]
    public class SkillProgressEntry
    {
        [Range(0, 100)] public int Level;
        public float CurrentXP;
        public float XPToNextLevel = 100f;

        public float NormalizedProgress => Level >= 100 ? 1f : Mathf.Clamp01(CurrentXP / Mathf.Max(1f, XPToNextLevel));
    }

    [Serializable]
    public class ActiveWeaponSkillDefinition
    {
        public string SkillID;
        public string SkillName;
        public WeaponSkillType WeaponCategory;
        [Range(0, 100)] public int RequiredMasteryLevel;
        public float MPCost = 15f;
        public float StaminaCost = 20f;
        public float DamageMultiplier = 1.8f;
        [TextArea(2, 3)] public string Description;
    }

    public class PlayerSkillProgression : MonoBehaviour
    {
        public const int MaxSkillLevel = 100;

        [Header("Active Weapon Skills Database")]
        [SerializeField] private List<ActiveWeaponSkillDefinition> activeWeaponSkills = new List<ActiveWeaponSkillDefinition>
        {
            new ActiveWeaponSkillDefinition
            {
                SkillID = "sword_vertical_slash",
                SkillName = "Vertical Slash",
                WeaponCategory = WeaponSkillType.Sword,
                RequiredMasteryLevel = 0,
                MPCost = 10f,
                StaminaCost = 15f,
                DamageMultiplier = 1.6f,
                Description = "A downward overhead sword strike that deals heavy poise and physical damage."
            },
            new ActiveWeaponSkillDefinition
            {
                SkillID = "sword_horizontal_slash",
                SkillName = "Horizontal Slash",
                WeaponCategory = WeaponSkillType.Sword,
                RequiredMasteryLevel = 10,
                MPCost = 14f,
                StaminaCost = 18f,
                DamageMultiplier = 1.85f,
                Description = "A wide sweeping arc that cleaves targets in front of the user."
            }
        };

        private readonly Dictionary<PassiveSkillType, SkillProgressEntry> passiveSkills = new Dictionary<PassiveSkillType, SkillProgressEntry>();
        private readonly Dictionary<WeaponSkillType, SkillProgressEntry> weaponSkills = new Dictionary<WeaponSkillType, SkillProgressEntry>();

        private ActiveWeaponSkillDefinition selectedActiveSkill;

        public event Action<PassiveSkillType, SkillProgressEntry> OnPassiveSkillChanged;
        public event Action<WeaponSkillType, SkillProgressEntry> OnWeaponSkillChanged;
        public event Action<ActiveWeaponSkillDefinition> OnActiveWeaponSkillSelected;

        public ActiveWeaponSkillDefinition SelectedActiveSkill => selectedActiveSkill;

        private void Awake()
        {
            foreach (PassiveSkillType type in Enum.GetValues(typeof(PassiveSkillType)))
            {
                passiveSkills[type] = new SkillProgressEntry { Level = 0, CurrentXP = 0f, XPToNextLevel = CalculateXPRequirement(0) };
            }

            foreach (WeaponSkillType type in Enum.GetValues(typeof(WeaponSkillType)))
            {
                weaponSkills[type] = new SkillProgressEntry { Level = 0, CurrentXP = 0f, XPToNextLevel = CalculateXPRequirement(0) };
            }

            if (activeWeaponSkills.Count > 0)
            {
                selectedActiveSkill = activeWeaponSkills[0];
            }
        }

        public SkillProgressEntry GetPassiveSkill(PassiveSkillType type) => passiveSkills[type];
        public SkillProgressEntry GetWeaponSkill(WeaponSkillType type) => weaponSkills[type];

        public List<ActiveWeaponSkillDefinition> GetSkillsForWeapon(WeaponSkillType weaponType)
        {
            List<ActiveWeaponSkillDefinition> result = new List<ActiveWeaponSkillDefinition>();
            for (int i = 0; i < activeWeaponSkills.Count; i++)
            {
                if (activeWeaponSkills[i].WeaponCategory == weaponType)
                {
                    result.Add(activeWeaponSkills[i]);
                }
            }
            return result;
        }

        public bool IsActiveSkillUnlocked(ActiveWeaponSkillDefinition skill)
        {
            if (skill == null) return false;
            SkillProgressEntry mastery = GetWeaponSkill(skill.WeaponCategory);
            return mastery.Level >= skill.RequiredMasteryLevel;
        }

        public bool SelectActiveWeaponSkill(ActiveWeaponSkillDefinition skill)
        {
            if (!IsActiveSkillUnlocked(skill)) return false;

            selectedActiveSkill = skill;
            OnActiveWeaponSkillSelected?.Invoke(selectedActiveSkill);
            return true;
        }

        public void AddPassiveSkillXP(PassiveSkillType type, float xpAmount)
        {
            if (xpAmount <= 0f || !passiveSkills.TryGetValue(type, out SkillProgressEntry entry)) return;
            if (entry.Level >= MaxSkillLevel) return;

            ApplyXPToEntry(entry, xpAmount);
            OnPassiveSkillChanged?.Invoke(type, entry);
        }

        public void AddWeaponSkillXP(WeaponSkillType type, float xpAmount)
        {
            if (xpAmount <= 0f || !weaponSkills.TryGetValue(type, out SkillProgressEntry entry)) return;
            if (entry.Level >= MaxSkillLevel) return;

            ApplyXPToEntry(entry, xpAmount);
            OnWeaponSkillChanged?.Invoke(type, entry);
        }

        private void ApplyXPToEntry(SkillProgressEntry entry, float xpAmount)
        {
            entry.CurrentXP += xpAmount;

            while (entry.Level < MaxSkillLevel && entry.CurrentXP >= entry.XPToNextLevel)
            {
                entry.CurrentXP -= entry.XPToNextLevel;
                entry.Level++;
                entry.XPToNextLevel = CalculateXPRequirement(entry.Level);
            }

            if (entry.Level >= MaxSkillLevel)
            {
                entry.Level = MaxSkillLevel;
                entry.CurrentXP = 0f;
            }
        }

        private float CalculateXPRequirement(int currentLevel)
        {
            return 100f + (currentLevel * 35f);
        }
    }
}