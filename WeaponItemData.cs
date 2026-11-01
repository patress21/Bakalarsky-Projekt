using UnityEngine;

namespace ActionRPG.Character
{
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "ActionRPG/Items/Weapon Item")]
    public class WeaponItemData : ItemData
    {
        public WeaponSkillType WeaponType = WeaponSkillType.Sword;
        public float BaseDamage = 18f;
        public StatBonus[] StatBonuses;

        private void OnValidate()
        {
            Category = ItemCategory.Weapon;
            MaxStack = 1;
        }
    }
}