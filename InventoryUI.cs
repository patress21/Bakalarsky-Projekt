using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ActionRPG.Character.UI
{
    public class InventoryUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private Transform slotGridContainer;
        [SerializeField] private InventorySlotUI slotPrefab;

        [Header("SAO Side Details Panel")]
        [SerializeField] private GameObject detailsPanelRoot;
        [SerializeField] private Image inspectIconImage;
        [SerializeField] private TextMeshProUGUI inspectNameText;
        [SerializeField] private TextMeshProUGUI inspectCategoryText;
        [SerializeField] private TextMeshProUGUI inspectDescriptionText;
        [SerializeField] private Button equipOrUseButton;
        [SerializeField] private TextMeshProUGUI equipButtonLabel;
        [SerializeField] private Button dropButton;

        private readonly List<InventorySlotUI> spawnedSlots = new List<InventorySlotUI>();
        private int selectedSlotIndex = -1;

        private void Awake()
        {
            if (playerInventory == null)
                playerInventory = FindFirstObjectByType<PlayerInventory>();

            if (equipOrUseButton != null)
            {
                equipOrUseButton.onClick.RemoveAllListeners();
                equipOrUseButton.onClick.AddListener(HandlePrimaryActionClicked);
            }

            if (dropButton != null)
            {
                dropButton.onClick.RemoveAllListeners();
                dropButton.onClick.AddListener(HandleDropClicked);
            }
        }

        private void OnEnable()
        {
            if (playerInventory == null) return;

            playerInventory.OnInventoryUpdated += RefreshInventoryUI;
            EnsureSlotInstances();
            RefreshInventoryUI();
        }

        private void OnDisable()
        {
            if (playerInventory == null) return;
            playerInventory.OnInventoryUpdated -= RefreshInventoryUI;
        }

        private void EnsureSlotInstances()
        {
            if (slotPrefab == null || slotGridContainer == null || playerInventory == null) return;

            int targetCount = playerInventory.Slots.Count;
            while (spawnedSlots.Count < targetCount)
            {
                int index = spawnedSlots.Count;
                InventorySlotUI newSlot = Instantiate(slotPrefab, slotGridContainer);
                newSlot.Initialize(index, SelectSlot);
                spawnedSlots.Add(newSlot);
            }
        }

        private void RefreshInventoryUI()
        {
            EnsureSlotInstances();

            var slots = playerInventory.Slots;
            for (int i = 0; i < spawnedSlots.Count; i++)
            {
                if (i < slots.Count)
                    spawnedSlots[i].Bind(slots[i]);
                else
                    spawnedSlots[i].ClearSlot();
            }

            RefreshDetailsPanel();
        }

        private void SelectSlot(int index)
        {
            selectedSlotIndex = index;
            RefreshDetailsPanel();
        }

        private void RefreshDetailsPanel()
        {
            if (playerInventory == null || selectedSlotIndex < 0 || selectedSlotIndex >= playerInventory.Slots.Count)
            {
                if (detailsPanelRoot != null) detailsPanelRoot.SetActive(false);
                return;
            }

            InventorySlot slot = playerInventory.Slots[selectedSlotIndex];
            if (slot.IsEmpty)
            {
                if (detailsPanelRoot != null) detailsPanelRoot.SetActive(false);
                return;
            }

            if (detailsPanelRoot != null) detailsPanelRoot.SetActive(true);

            if (inspectIconImage != null)
            {
                inspectIconImage.enabled = slot.Item.Icon != null;
                inspectIconImage.sprite = slot.Item.Icon;
            }

            if (inspectNameText != null) inspectNameText.text = slot.Item.ItemName;
            if (inspectCategoryText != null) inspectCategoryText.text = slot.Item.Category.ToString();
            if (inspectDescriptionText != null) inspectDescriptionText.text = slot.Item.Description;

            if (equipOrUseButton != null)
            {
                bool isWeapon = slot.Item is WeaponItemData;
                equipOrUseButton.gameObject.SetActive(isWeapon);

                if (isWeapon && equipButtonLabel != null)
                {
                    equipButtonLabel.text = slot.IsEquipped ? "Unequip" : "Equip";
                }
            }
        }

        private void HandlePrimaryActionClicked()
        {
            if (selectedSlotIndex < 0) return;
            playerInventory.ToggleEquipAt(selectedSlotIndex);
        }

        private void HandleDropClicked()
        {
            if (selectedSlotIndex < 0) return;
            playerInventory.RemoveAt(selectedSlotIndex, 1);
        }
    }
}