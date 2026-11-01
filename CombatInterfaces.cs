using UnityEngine;

namespace ActionRPG.Character
{
    [System.Serializable]
    public struct AttackData
    {
        public float HealthDamage;
        public float StaminaDamage;
        public float PushbackForce;
        public Vector3 HitOrigin;
        public IAttacker Attacker;
        public bool CanBeParried;

        public AttackData(float healthDamage, float staminaDamage, float pushbackForce, Vector3 hitOrigin, IAttacker attacker, bool canBeParried = true)
        {
            HealthDamage = healthDamage;
            StaminaDamage = staminaDamage;
            PushbackForce = pushbackForce;
            HitOrigin = hitOrigin;
            Attacker = attacker;
            CanBeParried = canBeParried;
        }
    }

    public interface IDamageable
    {
        void ReceiveAttack(AttackData attack);
    }

    public interface IAttacker
    {
        Transform GetTransform();
        void OnParried(PlayerController parryingPlayer);
    }
}