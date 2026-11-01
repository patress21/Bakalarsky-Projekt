using System;
using System.Collections.Generic;
using UnityEngine;

namespace ActionRPG.Character
{
    [Serializable]
    public class InventorySlot
    {
        public ItemData Item;
        public int Quantity;
        public bool IsEquipped;

        public bool IsEmpty => Item == null || Quantity <= 0;

        public void Clear()
        {
            Item = null;
            Quantity = 0;
            IsEquipped = false;
        }
    }

    public class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private int capacity = 24;
        [SerializeField] private List<InventorySlot> slots = new List<InventorySlot>();
        [SerializeField] private PlayerStats playerStats;

        [Header("Starting Test Items (Optional)")]
        [SerializeField] private ItemData[] startingItems;

        private WeaponItemData equippedWeapon;

        public event Action OnInventoryUpdated;
        public event Action<WeaponItemData> OnWeaponEquippedChanged;

        public IReadOnlyList<InventorySlot> Slots => slots;
        public WeaponItemData EquippedWeapon => equippedWeapon;

        private void Awake()
        {
            if (playerStats == null)
                playerStats = GetComponent<PlayerStats>();

            while (slots.Count < capacity)
            {
                slots.Add(new InventorySlot());
            }

            if (startingItems != null)
            {
                for (int i = 0; i < startingItems.Length; i++)
                {
                    if (startingItems[i] != null)
                        AddItem(startingItems[i], 1);
                }
            }
        }

        public bool AddItem(ItemData item, int amount = 1)
        {
            if (item == null || amount <= 0) return false;

            int remaining = amount;

            if (item.MaxStack > 1)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i].Item == item && slots[i].Quantity < item.MaxStack)
                    {
                        int space = item.MaxStack - slots[i].Quantity;
                        int toAdd = Mathf.Min(space, remaining);
                        slots[i].Quantity += toAdd;
                        remaining -= toAdd;

                        if (remaining <= 0)
                        {
                            OnInventoryUpdated?.Invoke();
                            return true;
                        }
                    }
                }
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsEmpty)
                {
                    int toAdd = Mathf.Min(item.MaxStack, remaining);
                    slots[i].Item = item;
                    slots[i].Quantity = toAdd;
                    slots[i].IsEquipped = false;
                    remaining -= toAdd;

                    if (remaining <= 0)
                    {
                        OnInventoryUpdated?.Invoke();
                        return true;
                    }
                }
            }

            OnInventoryUpdated?.Invoke();
            return remaining == 0;
        }

        public void RemoveAt(int slotIndex, int amount = 1)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count) return;

            InventorySlot slot = slots[slotIndex];
            if (slot.IsEmpty) return;

            if (slot.IsEquipped)
            {
                UnequipCurrentWeapon();
            }

            slot.Quantity -= amount;
            if (slot.Quantity <= 0)
            {
                slot.Clear();
            }

            OnInventoryUpdated?.Invoke();
        }

        public void ToggleEquipAt(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count) return;

            InventorySlot slot = slots[slotIndex];
            if (slot.IsEmpty || !(slot.Item is WeaponItemData weapon)) return;

            if (slot.IsEquipped)
            {
                UnequipCurrentWeapon();
            }
            else
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    slots[i].IsEquipped = false;
                }

                slot.IsEquipped = true;
                equippedWeapon = weapon;
                ApplyWeaponBonuses(weapon);
                OnWeaponEquippedChanged?.Invoke(equippedWeapon);
            }

            OnInventoryUpdated?.Invoke();
        }

        public void UnequipCurrentWeapon()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].IsEquipped = false;
            }

            equippedWeapon = null;
            ApplyWeaponBonuses(null);
            OnWeaponEquippedChanged?.Invoke(null);
            OnInventoryUpdated?.Invoke();
        }

        private void ApplyWeaponBonuses(WeaponItemData weapon)
        {
            if (playerStats == null) return;

            int str = 0, agi = 0, intel = 0, dex = 0, lck = 0, regen = 0;
            float dmg = 0f;

            if (weapon != null)
            {
                dmg = weapon.BaseDamage;
                if (weapon.StatBonuses != null)
                {
                    for (int i = 0; i < weapon.StatBonuses.Length; i++)
                    {
                        StatBonus b = weapon.StatBonuses[i];
                        switch (b.Attribute)
                        {
                            case AttributeType.Strength: str += b.BonusValue; break;
                            case AttributeType.Agility: agi += b.BonusValue; break;
                            case AttributeType.Intelligence: intel += b.BonusValue; break;
                            case AttributeType.Dexterity: dex += b.BonusValue; break;
                            case AttributeType.Luck: lck += b.BonusValue; break;
                            case AttributeType.LifeRegen: regen += b.BonusValue; break;
                        }
                    }
                }
            }

            playerStats.SetEquipmentBonuses(str, agi, intel, dex, lck, regen, dmg);
        }
    }
}