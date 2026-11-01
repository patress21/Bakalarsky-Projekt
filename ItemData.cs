using UnityEngine;

namespace ActionRPG.Character
{
    public enum ItemCategory
    {
        Weapon,
        Armor,
        Consumable,
        Material
    }

    public enum WeaponSkillType
    {
        Sword,
        Axe,
        Dagger,
        Rapier,
        Spear,
        TwoHandSword,
        TwoHandAxe
    }

    [CreateAssetMenu(fileName = "NewItem", menuName = "ActionRPG/Items/Base Item")]
    public class ItemData : ScriptableObject
    {
        public string ItemID;
        public string ItemName;
        [TextArea(2, 4)] public string Description;
        public Sprite Icon;
        public ItemCategory Category = ItemCategory.Material;
        [Min(1)] public int MaxStack = 99;
    }
}