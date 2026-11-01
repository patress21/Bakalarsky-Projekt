using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace ActionRPG.Character.UI
{
    public class AttributesUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private Transform attributeRowContainer;
        [SerializeField] private AttributeRowUI attributeRowPrefab;

        [Header("Summary Labels")]
        [SerializeField] private TextMeshProUGUI unspentPointsText;
        [SerializeField] private TextMeshProUGUI derivedStatsText;

        private readonly List<AttributeRowUI> spawnedRows = new List<AttributeRowUI>();

        private void Awake()
        {
            if (playerStats == null)
                playerStats = FindFirstObjectByType<PlayerStats>();

            BuildRowsIfNeeded();
        }

        private void OnEnable()
        {
            if (playerStats == null) return;

            playerStats.OnAttributesChanged += RefreshUI;
            playerStats.OnHealthChanged += HandleVitalsChanged;
            playerStats.OnStaminaChanged += HandleVitalsChanged;
            playerStats.OnMPChanged += HandleVitalsChanged;

            BuildRowsIfNeeded();
            RefreshUI();
        }

        private void OnDisable()
        {
            if (playerStats == null) return;

            playerStats.OnAttributesChanged -= RefreshUI;
            playerStats.OnHealthChanged -= HandleVitalsChanged;
            playerStats.OnStaminaChanged -= HandleVitalsChanged;
            playerStats.OnMPChanged -= HandleVitalsChanged;
        }

        private void BuildRowsIfNeeded()
        {
            if (spawnedRows.Count > 0 || attributeRowPrefab == null || attributeRowContainer == null) return;

            foreach (AttributeType type in Enum.GetValues(typeof(AttributeType)))
            {
                AttributeRowUI row = Instantiate(attributeRowPrefab, attributeRowContainer);
                row.Initialize(type, HandleAllocateClicked);
                spawnedRows.Add(row);
            }
        }

        private void HandleAllocateClicked(AttributeType type)
        {
            if (playerStats != null)
            {
                playerStats.AllocateAttributePoint(type);
            }
        }

        private void HandleVitalsChanged(float current, float max)
        {
            RefreshDerivedStatsSummary();
        }

        private void RefreshUI()
        {
            if (playerStats == null) return;

            bool hasPoints = playerStats.UnspentPoints > 0;

            if (unspentPointsText != null)
            {
                unspentPointsText.text = $"Available Points: {playerStats.UnspentPoints}";
            }

            for (int i = 0; i < spawnedRows.Count; i++)
            {
                AttributeType type = spawnedRows[i].Type;
                int total = playerStats.GetTotalAttribute(type);
                int bonus = playerStats.GetBonusAttribute(type);
                spawnedRows[i].Refresh(total, bonus, hasPoints);
            }

            RefreshDerivedStatsSummary();
        }

        private void RefreshDerivedStatsSummary()
        {
            if (playerStats == null || derivedStatsText == null) return;

            derivedStatsText.text =
                $"HP: {Mathf.CeilToInt(playerStats.CurrentHealth)} / {Mathf.CeilToInt(playerStats.MaxHealth)}\n" +
                $"Stamina: {Mathf.CeilToInt(playerStats.CurrentStamina)} / {Mathf.CeilToInt(playerStats.MaxStamina)}\n" +
                $"MP: {Mathf.CeilToInt(playerStats.CurrentMP)} / {Mathf.CeilToInt(playerStats.MaxMP)}\n" +
                $"Attack Power: {playerStats.PhysicalAttackPower:F1}\n" +
                $"Crit Chance: {playerStats.CritChance:F1}%\n" +
                $"HP Regen: +{playerStats.HealthRegenRate:F1}/s";
        }
    }
}