using UnityEngine;

namespace ActionRPG.Character
{
    public class CombatDummyAttacker : MonoBehaviour, IAttacker, IDamageable
    {
        [Header("Target & Range")]
        [SerializeField] private PlayerController targetPlayer;
        [SerializeField] private float attackRange = 3.2f;

        [Header("Attack Timing & Telegraph")]
        [SerializeField] private float attackInterval = 2.5f;
        [SerializeField] private float windupDuration = 0.6f;
        [SerializeField] private float staggerDuration = 2.0f;

        [Header("Attack Payload")]
        [SerializeField] private float healthDamage = 25f;
        [SerializeField] private float staminaDamage = 38f;
        [SerializeField] private float pushbackForce = 7.5f;

        private enum DummyState { Idle, Windup, Staggered }
        [SerializeField] private DummyState currentState = DummyState.Idle;

        private float stateTimer;
        private Renderer meshRenderer;
        private Color defaultColor = Color.gray;

        private void Awake()
        {
            meshRenderer = GetComponentInChildren<Renderer>();
            if (meshRenderer != null)
                defaultColor = meshRenderer.material.color;

            if (targetPlayer == null)
                targetPlayer = FindFirstObjectByType<PlayerController>();
        }

        private void Update()
        {
            if (targetPlayer == null) return;

            stateTimer += Time.deltaTime;

            // Face the player while not staggered
            if (currentState != DummyState.Staggered)
            {
                Vector3 dir = targetPlayer.transform.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(dir.normalized);
            }

            switch (currentState)
            {
                case DummyState.Idle:
                    SetColor(defaultColor);
                    float dist = Vector3.Distance(transform.position, targetPlayer.transform.position);
                    if (dist <= attackRange && stateTimer >= attackInterval)
                    {
                        currentState = DummyState.Windup;
                        stateTimer = 0f;
                    }
                    break;

                case DummyState.Windup:
                    // Telegraph the attack by lerping color from Yellow to Bright Red right before impact!
                    float t = Mathf.Clamp01(stateTimer / windupDuration);
                    SetColor(Color.Lerp(Color.yellow, Color.red, t));

                    if (stateTimer >= windupDuration)
                    {
                        ExecuteStrike();
                        if (currentState != DummyState.Staggered)
                        {
                            currentState = DummyState.Idle;
                            stateTimer = 0f;
                        }
                    }
                    break;

                case DummyState.Staggered:
                    // Flash Cyan while staggered and vulnerable to a Riposte
                    SetColor(Color.cyan);
                    if (stateTimer >= staggerDuration)
                    {
                        currentState = DummyState.Idle;
                        stateTimer = 0f;
                    }
                    break;
            }
        }

        private void ExecuteStrike()
        {
            float dist = Vector3.Distance(transform.position, targetPlayer.transform.position);
            if (dist <= attackRange + 0.5f)
            {
                AttackData attack = new AttackData(
                    healthDamage: healthDamage,
                    staminaDamage: staminaDamage,
                    pushbackForce: pushbackForce,
                    hitOrigin: transform.position,
                    attacker: this,
                    canBeParried: true
                );

                targetPlayer.ReceiveAttack(attack);
            }
        }

        public Transform GetTransform() => transform;

        public void OnParried(PlayerController parryingPlayer)
        {
            currentState = DummyState.Staggered;
            stateTimer = 0f;
            Debug.Log("<color=cyan>[Dummy Enemy] STAGGERED by Player's Perfect Parry!</color>");
        }

        public void ReceiveAttack(AttackData attack)
        {
            Debug.Log($"<color=lime>[Dummy Enemy] Took {attack.HealthDamage} Riposte Damage from Player!</color>");
            // Recover from stagger after taking the Riposte
            currentState = DummyState.Idle;
            stateTimer = 0f;
        }

        private void SetColor(Color c)
        {
            if (meshRenderer != null)
                meshRenderer.material.color = c;
        }
    }
}