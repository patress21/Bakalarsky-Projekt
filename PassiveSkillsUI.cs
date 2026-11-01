using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ActionRPG.Character.UI
{
    [Serializable]
    public struct PassiveSkillRowBinding
    {
        public PassiveSkillType SkillType;
        public TextMeshProUGUI LevelText;
        public Image ProgressBarFill;
    }

    public class PassiveSkillsUI : MonoBehaviour
    {
        [SerializeField] private PlayerSkillProgression skillProgression;
        [SerializeField] private PassiveSkillRowBinding[] skillRows;

        private void Awake()
        {
            if (skillProgression == null)
                skillProgression = FindFirstObjectByType<PlayerSkillProgression>();
        }

        private void OnEnable()
        {
            if (skillProgression == null) return;

            skillProgression.OnPassiveSkillChanged += HandleSkillChanged;
            RefreshAllRows();
        }

        private void OnDisable()
        {
            if (skillProgression == null) return;

            skillProgression.OnPassiveSkillChanged -= HandleSkillChanged;
        }

        private void HandleSkillChanged(PassiveSkillType type, SkillProgressEntry entry)
        {
            if (skillRows == null) return;

            for (int i = 0; i < skillRows.Length; i++)
            {
                if (skillRows[i].SkillType == type)
                {
                    UpdateRowVisuals(skillRows[i], entry);
                    break;
                }
            }
        }

        private void RefreshAllRows()
        {
            if (skillRows == null || skillProgression == null) return;

            for (int i = 0; i < skillRows.Length; i++)
            {
                SkillProgressEntry entry = skillProgression.GetPassiveSkill(skillRows[i].SkillType);
                UpdateRowVisuals(skillRows[i], entry);
            }
        }

        private void UpdateRowVisuals(PassiveSkillRowBinding row, SkillProgressEntry entry)
        {
            if (row.LevelText != null)
            {
                row.LevelText.text = $"{entry.Level} / {PlayerSkillProgression.MaxSkillLevel}";
            }

            if (row.ProgressBarFill != null)
            {
                row.ProgressBarFill.fillAmount = entry.NormalizedProgress;
            }
        }
    }
}