using System;
using UnityEngine;
using UnityEngine.Events;

namespace ActionRPG.Character
{
    public enum AttributeType
    {
        Strength,
        Agility,
        Intelligence,
        Dexterity,
        Luck,
        LifeRegen
    }

    [Serializable]
    public struct StatBonus
    {
        public AttributeType Attribute;
        public int BonusValue;
    }

    public class PlayerStats : MonoBehaviour
    {
        [Header("Base Vitals")]
        [SerializeField] private float baseMaxHealth = 100f;
        [SerializeField] private float baseMaxStamina = 100f;
        [SerializeField] private float baseMaxMP = 100f;

        [Header("Current Vitals")]
        [SerializeField] private float currentHealth = 100f;
        [SerializeField] private float currentStamina = 100f;
        [SerializeField] private float currentMP = 100f;

        [Header("Regeneration Settings")]
        [SerializeField] private float baseStaminaRegenRate = 22f;
        [SerializeField] private float staminaRegenDelay = 1.5f;
        [Range(0f, 1f)] [SerializeField] private float blockRegenMultiplier = 0.35f;
        [SerializeField] private float healthRegenPerPoint = 0.5f;

        [Header("Attributes & Progression")]
        [SerializeField] private int unspentAttributePoints = 5;
        [SerializeField] private int strength = 10;
        [SerializeField] private int agility = 10;
        [SerializeField] private int intelligence = 10;
        [SerializeField] private int dexterity = 10;
        [SerializeField] private int luck = 5;
        [SerializeField] private int lifeRegen = 1;

        private int bonusStrength;
        private int bonusAgility;
        private int bonusIntelligence;
        private int bonusDexterity;
        private int bonusLuck;
        private int bonusLifeRegen;
        private float equippedWeaponDamage;

        private float maxHealth;
        private float maxStamina;
        private float maxMP;

        public event Action<float, float> OnHealthChanged;
        public event Action<float, float> OnStaminaChanged;
        public event Action<float, float> OnMPChanged;
        public event Action OnAttributesChanged;
        public event Action OnStaminaDepleted;
        public event Action OnDeath;

        [Header("Inspector Event Hooks")]
        public UnityEvent OnPlayerDeathUnityEvent;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float CurrentStamina => currentStamina;
        public float MaxStamina => maxStamina;
        public float CurrentMP => currentMP;
        public float MaxMP => maxMP;
        public bool IsDead { get; private set; }

        public int UnspentPoints => unspentAttributePoints;
        public int Strength => strength + bonusStrength;
        public int Agility => agility + bonusAgility;
        public int Intelligence => intelligence + bonusIntelligence;
        public int Dexterity => dexterity + bonusDexterity;
        public int Luck => luck + bonusLuck;
        public int LifeRegen => lifeRegen + bonusLifeRegen;

        public float PhysicalAttackPower => equippedWeaponDamage + (Strength * 1.5f) + (Dexterity * 0.5f);
        public float MagicAttackPower => Intelligence * 2.0f;
        public float CritChance => Mathf.Clamp((Dexterity * 0.4f) + (Luck * 0.6f), 0f, 100f);
        public float HealthRegenRate => LifeRegen * healthRegenPerPoint;

        private float lastStaminaSpentTime = -10f;
        private bool isRegenPausedByAction;
        private bool isBlockingStateActive;

        private void Awake()
        {
            RecalculateDerivedStats(restoreToFull: true);
        }

        private void Start()
        {
            BroadcastAllStats();
        }

        private void Update()
        {
            if (IsDead) return;

            float dt = Time.deltaTime;
            HandleStaminaRegeneration(dt);
            HandleHealthRegeneration(dt);
        }

        public void BroadcastAllStats()
        {
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            OnMPChanged?.Invoke(currentMP, maxMP);
            OnAttributesChanged?.Invoke();
        }

        public int GetTotalAttribute(AttributeType type)
        {
            return type switch
            {
                AttributeType.Strength => Strength,
                AttributeType.Agility => Agility,
                AttributeType.Intelligence => Intelligence,
                AttributeType.Dexterity => Dexterity,
                AttributeType.Luck => Luck,
                AttributeType.LifeRegen => LifeRegen,
                _ => 0
            };
        }

        public int GetBonusAttribute(AttributeType type)
        {
            return type switch
            {
                AttributeType.Strength => bonusStrength,
                AttributeType.Agility => bonusAgility,
                AttributeType.Intelligence => bonusIntelligence,
                AttributeType.Dexterity => bonusDexterity,
                AttributeType.Luck => bonusLuck,
                AttributeType.LifeRegen => bonusLifeRegen,
                _ => 0
            };
        }

        public bool AllocateAttributePoint(AttributeType type)
        {
            if (unspentAttributePoints <= 0) return false;

            unspentAttributePoints--;

            switch (type)
            {
                case AttributeType.Strength: strength++; break;
                case AttributeType.Agility: agility++; break;
                case AttributeType.Intelligence: intelligence++; break;
                case AttributeType.Dexterity: dexterity++; break;
                case AttributeType.Luck: luck++; break;
                case AttributeType.LifeRegen: lifeRegen++; break;
            }

            RecalculateDerivedStats(restoreToFull: false);
            OnAttributesChanged?.Invoke();
            return true;
        }

        public void AddAttributePoints(int amount)
        {
            if (amount <= 0) return;
            unspentAttributePoints += amount;
            OnAttributesChanged?.Invoke();
        }

        public void SetEquipmentBonuses(int str, int agi, int intel, int dex, int lck, int regen, float weaponDamage)
        {
            bonusStrength = str;
            bonusAgility = agi;
            bonusIntelligence = intel;
            bonusDexterity = dex;
            bonusLuck = lck;
            bonusLifeRegen = regen;
            equippedWeaponDamage = weaponDamage;

            RecalculateDerivedStats(restoreToFull: false);
            OnAttributesChanged?.Invoke();
        }

        private void RecalculateDerivedStats(bool restoreToFull)
        {
            maxHealth = baseMaxHealth + (Strength * 5f);
            maxStamina = baseMaxStamina + (Agility * 3f) + (Strength * 1f);
            maxMP = baseMaxMP + (Intelligence * 5f);

            if (restoreToFull)
            {
                currentHealth = maxHealth;
                currentStamina = maxStamina;
                currentMP = maxMP;
            }
            else
            {
                currentHealth = Mathf.Min(currentHealth, maxHealth);
                currentStamina = Mathf.Min(currentStamina, maxStamina);
                currentMP = Mathf.Min(currentMP, maxMP);
            }

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            OnMPChanged?.Invoke(currentMP, maxMP);
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;

            currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f)
            {
                IsDead = true;
                OnDeath?.Invoke();
                OnPlayerDeathUnityEvent?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;

            currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void HandleHealthRegeneration(float deltaTime)
        {
            if (currentHealth >= maxHealth || HealthRegenRate <= 0f) return;

            float previousHealth = currentHealth;
            currentHealth = Mathf.MoveTowards(currentHealth, maxHealth, HealthRegenRate * deltaTime);

            if (Mathf.Abs(currentHealth - previousHealth) > 0.001f)
            {
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
            }
        }

        public bool HasEnoughStamina(float cost) => currentStamina >= cost;

        public bool TryConsumeStamina(float amount, bool allowPartialDrain = false)
        {
            if (amount <= 0f) return true;
            if (!allowPartialDrain && currentStamina < amount) return false;
            if (allowPartialDrain && currentStamina <= 0f) return false;

            currentStamina = Mathf.Clamp(currentStamina - amount, 0f, maxStamina);
            lastStaminaSpentTime = Time.time;

            OnStaminaChanged?.Invoke(currentStamina, maxStamina);

            if (currentStamina <= 0f)
            {
                OnStaminaDepleted?.Invoke();
            }

            return true;
        }

        public void RestoreStamina(float amount)
        {
            if (amount <= 0f) return;

            currentStamina = Mathf.Clamp(currentStamina + amount, 0f, maxStamina);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }

        public void SetStaminaRegenModifiers(bool pauseRegen, bool isBlocking)
        {
            isRegenPausedByAction = pauseRegen;
            isBlockingStateActive = isBlocking;
        }

        private void HandleStaminaRegeneration(float deltaTime)
        {
            if (isRegenPausedByAction || currentStamina >= maxStamina) return;
            if (Time.time < lastStaminaSpentTime + staminaRegenDelay) return;

            float regenRate = baseStaminaRegenRate + (Agility * 0.25f);
            float multiplier = isBlockingStateActive ? blockRegenMultiplier : 1.0f;
            float previousStamina = currentStamina;

            currentStamina = Mathf.MoveTowards(currentStamina, maxStamina, regenRate * multiplier * deltaTime);

            if (Mathf.Abs(currentStamina - previousStamina) > 0.0001f)
            {
                OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            }
        }

        public bool HasEnoughMP(float cost) => currentMP >= cost;

        public bool UseMP(float amount)
        {
            if (amount <= 0f) return true;
            if (!HasEnoughMP(amount)) return false;

            currentMP = Mathf.Clamp(currentMP - amount, 0f, maxMP);
            OnMPChanged?.Invoke(currentMP, maxMP);
            return true;
        }

        public void RestoreMP(float amount)
        {
            if (amount <= 0f) return;

            currentMP = Mathf.Clamp(currentMP + amount, 0f, maxMP);
            OnMPChanged?.Invoke(currentMP, maxMP);
        }
    }
}