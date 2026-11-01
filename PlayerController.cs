using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ActionRPG.Character
{
    public enum PlayerStateType
    {
        Idle,
        Moving,
        Jumping,
        Falling,
        Rolling,
        Attacking,
        Blocking,
        GuardBroken,
        Riposting
    }

    [Serializable]
    public struct AttackStepData
    {
        public string AnimationStateName;
        [Tooltip("Total duration of this attack animation.")]
        public float TotalDuration;
        [Tooltip("Normalized time (0 to 1) when the player can buffer the next attack.")]
        [Range(0f, 1f)] public float ComboWindowStart;
        [Tooltip("Normalized time (0 to 1) when the attack enters recovery (can be roll-canceled).")]
        [Range(0f, 1f)] public float RecoveryWindowStart;
        [Tooltip("Forward lunge speed applied during the early active frames.")]
        public float ForwardLungeSpeed;
        [Tooltip("Stamina consumed when executing this combo step.")]
        public float StaminaCost;
    }

    [RequireComponent(typeof(CharacterController), typeof(PlayerInputReader), typeof(PlayerStats))]
    public class PlayerController : MonoBehaviour, IDamageable
    {
        [Header("State Debug (Read-Only)")]
        [SerializeField] private PlayerStateType currentStateType;
        [SerializeField] private bool isInvincible;
        [SerializeField] private bool isParryWindowActive;
        [SerializeField] private bool isRiposteAvailable;
        [SerializeField] private float currentEffectiveParryWindow;
        [SerializeField] private float currentGroundAngle;

        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Animator animator;
        [SerializeField] private Collider hurtboxCollider;

        [Header("Locomotion Settings")]
        public float MoveSpeed = 6.0f;
        public float BlockMoveSpeed = 2.5f;
        public float AttackMoveSpeed = 1.0f;
        public float RotationSmoothTime = 0.08f;

        [Header("Jump & Air Physics (Spacebar)")]
        public float JumpForce = 11.5f;
        public float JumpStaminaCost = 12f;
        public float BaseGravity = -22.0f;
        public float AscendMultiplier = 1.0f;
        public float LowJumpMultiplier = 2.8f;
        public float FallMultiplier = 2.2f;
        public float MaxFallSpeed = -35.0f;
        [Range(0f, 1f)] public float AirControlMultiplier = 0.5f;
        public float AirSteeringAcceleration = 18.0f;
        public float CoyoteTime = 0.15f;

        [Header("Ground & Slope Detection")]
        [SerializeField] private float groundCheckDistance = 0.2f;
        [SerializeField] private float groundedGravity = -2.5f;
        public float MaxSlopeAngle = 45.0f;

        [Header("Combat - 3-Hit Combo (Left Click)")]
        public float ComboResetTime = 0.8f;
        public AttackStepData[] LightCombo = new AttackStepData[3]
        {
            new AttackStepData { AnimationStateName = "Attack_1", TotalDuration = 0.75f, ComboWindowStart = 0.35f, RecoveryWindowStart = 0.65f, ForwardLungeSpeed = 3.5f, StaminaCost = 14f },
            new AttackStepData { AnimationStateName = "Attack_2", TotalDuration = 0.80f, ComboWindowStart = 0.35f, RecoveryWindowStart = 0.65f, ForwardLungeSpeed = 4.0f, StaminaCost = 16f },
            new AttackStepData { AnimationStateName = "Attack_3", TotalDuration = 1.05f, ComboWindowStart = 0.50f, RecoveryWindowStart = 0.70f, ForwardLungeSpeed = 5.5f, StaminaCost = 22f }
        };

        [Header("Combat - Dodge Roll (R Key)")]
        public float RollSpeed = 10f;
        public float RollDuration = 0.55f;
        public float RollCooldown = 0.45f;
        public float RollStaminaCost = 20f;
        public float IFrameStartTime = 0.05f;
        public float IFrameEndTime = 0.40f;

        [Header("Combat - Timed Parry & Standard Block (Right Click)")]
        public float ParryWindowDuration = 0.20f;
        public float ParryCooldown = 0.50f;
        [Range(0f, 1f)] public float ParrySpamDecayFactor = 0.5f;
        [Range(30f, 180f)] public float BlockAngle = 120f;
        [Range(0f, 1f)] public float BlockDamageReduction = 0.85f;
        public float DefaultBlockPushbackForce = 6.5f;
        public float PushbackDrag = 14.0f;
        public float ParryStaminaRestore = 30f;
        public float GuardBreakDuration = 1.5f;

        [Header("Combat - Riposte / Counter-Attack")]
        public float RiposteWindowDuration = 1.2f;
        public AttackStepData RiposteAttack = new AttackStepData
        {
            AnimationStateName = "Riposte_Attack",
            TotalDuration = 0.90f,
            ComboWindowStart = 0.8f,
            RecoveryWindowStart = 0.65f,
            ForwardLungeSpeed = 6.5f,
            StaminaCost = 0f
        };
        public float RiposteDamage = 65f;

        [Header("Game Feel & Feedback Hooks (VFX / SFX / Hit-Stop)")]
        [SerializeField] private bool enableParryHitStop = true;
        [SerializeField] private float parryHitStopDuration = 0.08f;
        [SerializeField] private float parryHitStopTimeScale = 0.1f;

        public UnityEvent OnParrySuccess;
        public UnityEvent OnBlockImpact;
        public UnityEvent OnGuardBreak;
        public UnityEvent OnRiposteExecuted;
        public UnityEvent<float> OnDamageTaken;

        // C# Event for HUD Roll Cooldown Indicator (passes total duration: RollDuration + RollCooldown)
        public event Action<float> OnRollCooldownStarted;

        // Public Accessors for States
        public CharacterController Controller { get; private set; }
        public PlayerInputReader Input { get; private set; }
        public PlayerStats Stats { get; private set; }
        public Animator Anim => animator;
        public Transform CamTransform => cameraTransform;

        // Runtime Physics & Combat Tracking
        public bool IsGrounded { get; private set; }
        public bool IsOnSteepSlope { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public float VerticalVelocity { get; private set; }
        public Vector3 HorizontalVelocity { get; private set; }

        public int CurrentComboIndex { get; set; }
        public float LastAttackEndTime { get; set; } = -10f;
        public float LastRollEndTime { get; set; } = -10f;

        public float BlockStartTime { get; private set; } = -10f;
        public float LastBlockReleaseTime { get; private set; } = -10f;
        public IAttacker LastParriedAttacker { get; private set; }

        private int consecutiveParrySpamCount;
        private float riposteWindowExpireTime = -10f;
        private Vector3 externalPushbackVelocity;

        private float lastGroundedTime = -10f;
        private float lastJumpExecutedTime = -10f;
        private float turnSmoothVelocity;
        private Coroutine hitStopCoroutine;

        private Dictionary<PlayerStateType, PlayerBaseState> states;
        private PlayerBaseState currentState;

        private void Awake()
        {
            Controller = GetComponent<CharacterController>();
            Input = GetComponent<PlayerInputReader>();
            Stats = GetComponent<PlayerStats>();

            Controller.slopeLimit = MaxSlopeAngle;

            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            InitializeStateMachine();
        }

        private void InitializeStateMachine()
        {
            states = new Dictionary<PlayerStateType, PlayerBaseState>
            {
                { PlayerStateType.Idle, new IdleState(this) },
                { PlayerStateType.Moving, new MovingState(this) },
                { PlayerStateType.Jumping, new JumpingState(this) },
                { PlayerStateType.Falling, new FallingState(this) },
                { PlayerStateType.Rolling, new RollingState(this) },
                { PlayerStateType.Attacking, new AttackingState(this) },
                { PlayerStateType.Blocking, new BlockingState(this) },
                { PlayerStateType.GuardBroken, new GuardBrokenState(this) },
                { PlayerStateType.Riposting, new RipostingState(this) }
            };

            TransitionToState(PlayerStateType.Idle);
        }

        private void Update()
        {
            UpdateGroundAndSlopeDetection();
            ApplyCustomGravity(Time.deltaTime);
            UpdateExternalPushback(Time.deltaTime);
            SyncStaminaRegenRulesWithState();
            UpdateDebugFlags();

            currentState?.Tick(Time.deltaTime);
        }

        private void SyncStaminaRegenRulesWithState()
        {
            bool pauseRegen = currentStateType == PlayerStateType.Rolling ||
                              currentStateType == PlayerStateType.Attacking ||
                              currentStateType == PlayerStateType.Riposting ||
                              currentStateType == PlayerStateType.GuardBroken;

            bool isBlocking = currentStateType == PlayerStateType.Blocking;
            Stats.SetStaminaRegenModifiers(pauseRegen, isBlocking);
        }

        private void UpdateDebugFlags()
        {
            isParryWindowActive = (currentStateType == PlayerStateType.Blocking) &&
                                  (Time.time <= BlockStartTime + currentEffectiveParryWindow);
            isRiposteAvailable = Time.time <= riposteWindowExpireTime;
        }

        public void TransitionToState(PlayerStateType newStateType)
        {
            currentState?.Exit();
            currentStateType = newStateType;
            currentState = states[newStateType];
            currentState.Enter();
        }

        #region Parry, Block & Incoming Hit Resolution (IDamageable)

        public void RegisterBlockStart()
        {
            float timeSinceLastRelease = Time.time - LastBlockReleaseTime;

            if (timeSinceLastRelease < ParryCooldown)
            {
                consecutiveParrySpamCount++;
                currentEffectiveParryWindow = consecutiveParrySpamCount >= 3
                    ? 0f
                    : ParryWindowDuration * Mathf.Pow(ParrySpamDecayFactor, consecutiveParrySpamCount);
            }
            else
            {
                consecutiveParrySpamCount = 0;
                currentEffectiveParryWindow = ParryWindowDuration;
            }

            BlockStartTime = Time.time;
        }

        public void RegisterBlockRelease()
        {
            LastBlockReleaseTime = Time.time;
        }

        public bool CanExecuteRiposte()
        {
            return IsGrounded && Time.time <= riposteWindowExpireTime;
        }

        public void ConsumeRiposteWindow()
        {
            riposteWindowExpireTime = -10f;
        }

        public void NotifyRollCooldownStarted()
        {
            OnRollCooldownStarted?.Invoke(RollDuration + RollCooldown);
        }

        public void ReceiveAttack(AttackData attack)
        {
            if (isInvincible || Stats.IsDead)
            {
                Debug.Log("<color=cyan>[Combat] Attack evaded via I-Frames!</color>");
                return;
            }

            if (currentStateType == PlayerStateType.Blocking)
            {
                if (IsAttackWithinFrontArc(attack.HitOrigin))
                {
                    bool withinParryWindow = attack.CanBeParried &&
                                             (Time.time <= BlockStartTime + currentEffectiveParryWindow);

                    if (withinParryWindow)
                    {
                        ResolvePerfectParry(attack);
                        return;
                    }
                    else
                    {
                        ResolveStandardBlock(attack);
                        return;
                    }
                }
                else
                {
                    Debug.Log("<color=orange>[Combat] Hit from BEHIND / OUTSIDE guard arc! Guard bypassed.</color>");
                }
            }

            ResolveDirectHit(attack);
        }

        public void ReceiveIncomingHit(float damage, Vector3 attackerPosition)
        {
            AttackData simpleAttack = new AttackData(
                healthDamage: damage,
                staminaDamage: damage * 1.2f,
                pushbackForce: DefaultBlockPushbackForce,
                hitOrigin: attackerPosition,
                attacker: null,
                canBeParried: true
            );
            ReceiveAttack(simpleAttack);
        }

        private bool IsAttackWithinFrontArc(Vector3 hitOrigin)
        {
            Vector3 dirToAttacker = hitOrigin - transform.position;
            dirToAttacker.y = 0f;

            if (dirToAttacker.sqrMagnitude < 0.0001f)
                return true;

            dirToAttacker.Normalize();

            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;
            flatForward.Normalize();

            float angleToAttacker = Vector3.Angle(flatForward, dirToAttacker);
            return angleToAttacker <= (BlockAngle * 0.5f);
        }

        private void ResolvePerfectParry(AttackData attack)
        {
            Stats.RestoreStamina(ParryStaminaRestore);
            consecutiveParrySpamCount = 0;

            riposteWindowExpireTime = Time.time + RiposteWindowDuration;
            LastParriedAttacker = attack.Attacker;

            PlayAnimationCrossFade("Parry_Success", 0.05f);
            ApplyPushback(attack.HitOrigin, (attack.PushbackForce > 0f ? attack.PushbackForce : DefaultBlockPushbackForce) * 0.35f);

            attack.Attacker?.OnParried(this);

            if (enableParryHitStop)
            {
                TriggerHitStop(parryHitStopDuration, parryHitStopTimeScale);
            }

            OnParrySuccess?.Invoke();
            Debug.Log("<color=lime><b>[Combat] PERFECT PARRY!</b> 0 Damage taken, Stamina Restored, Riposte Window Open!</color>");
        }

        private void ResolveStandardBlock(AttackData attack)
        {
            float chipDamage = attack.HealthDamage * (1f - BlockDamageReduction);
            if (chipDamage > 0f)
            {
                Stats.TakeDamage(chipDamage);
            }

            float staminaCost = attack.StaminaDamage > 0f ? attack.StaminaDamage : attack.HealthDamage;
            Stats.TryConsumeStamina(staminaCost, allowPartialDrain: true);

            float pushForce = attack.PushbackForce > 0f ? attack.PushbackForce : DefaultBlockPushbackForce;
            ApplyPushback(attack.HitOrigin, pushForce);

            if (Stats.CurrentStamina <= 0f)
            {
                OnGuardBreak?.Invoke();
                Debug.Log("<color=red><b>[Combat] GUARD BROKEN!</b> Stamina depleted — Player stunned for " + GuardBreakDuration + "s!</color>");
                TransitionToState(PlayerStateType.GuardBroken);
                return;
            }

            PlayAnimationCrossFade("Block_Impact", 0.05f);
            OnBlockImpact?.Invoke();
            Debug.Log($"<color=yellow>[Combat] Standard Block! Chip Damage: {chipDamage:F1}, Stamina Remaining: {Stats.CurrentStamina:F1}</color>");
        }

        private void ResolveDirectHit(AttackData attack)
        {
            Stats.TakeDamage(attack.HealthDamage);
            ApplyPushback(attack.HitOrigin, DefaultBlockPushbackForce * 0.5f);

            OnDamageTaken?.Invoke(attack.HealthDamage);
            Debug.Log($"<color=red>[Combat] Direct Hit Taken! Damage: {attack.HealthDamage:F1}, Health Remaining: {Stats.CurrentHealth:F1}</color>");
        }

        public void ApplyPushback(Vector3 sourcePosition, float force)
        {
            Vector3 pushDir = transform.position - sourcePosition;
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude < 0.001f)
                pushDir = -transform.forward;

            externalPushbackVelocity = pushDir.normalized * force;
        }

        private void UpdateExternalPushback(float deltaTime)
        {
            if (externalPushbackVelocity.sqrMagnitude > 0.001f)
            {
                externalPushbackVelocity = Vector3.MoveTowards(externalPushbackVelocity, Vector3.zero, PushbackDrag * deltaTime);
            }
            else
            {
                externalPushbackVelocity = Vector3.zero;
            }
        }

        public void TriggerHitStop(float duration, float timeScale)
        {
            if (hitStopCoroutine != null)
                StopCoroutine(hitStopCoroutine);
            hitStopCoroutine = StartCoroutine(HitStopRoutine(duration, timeScale));
        }

        private IEnumerator HitStopRoutine(float duration, float timeScale)
        {
            Time.timeScale = timeScale;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = 1.0f;
            hitStopCoroutine = null;
        }

        #endregion

        #region Grounding, Slopes, Jump & Air Physics

        private void UpdateGroundAndSlopeDetection()
        {
            if (Time.time < lastJumpExecutedTime + 0.12f)
            {
                IsGrounded = false;
                IsOnSteepSlope = false;
                SetAnimatorBool("IsGrounded", false);
                return;
            }

            Vector3 capsuleCenterWorld = transform.position + Controller.center;
            float halfHeight = Mathf.Max(Controller.height * 0.5f, Controller.radius);
            Vector3 bottomSphereCenter = capsuleCenterWorld - Vector3.up * (halfHeight - Controller.radius);

            float castRadius = Controller.radius * 0.9f;
            Vector3 castOrigin = bottomSphereCenter + Vector3.up * 0.2f;
            float castDistance = 0.2f + Controller.skinWidth + Mathf.Max(0.15f, groundCheckDistance);

            RaycastHit[] hits = Physics.SphereCastAll(
                castOrigin,
                castRadius,
                Vector3.down,
                castDistance,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            bool foundFloorBelow = false;
            RaycastHit closestHit = default;
            float closestDist = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform == transform || hits[i].transform.IsChildOf(transform))
                    continue;

                if (hits[i].distance < closestDist)
                {
                    closestDist = hits[i].distance;
                    closestHit = hits[i];
                    foundFloorBelow = true;
                }
            }

            if (foundFloorBelow || Controller.isGrounded)
            {
                GroundNormal = foundFloorBelow ? closestHit.normal : Vector3.up;
                currentGroundAngle = Vector3.Angle(GroundNormal, Vector3.up);

                float slopeLimit = MaxSlopeAngle > 1f ? MaxSlopeAngle : 45f;

                if (currentGroundAngle <= slopeLimit)
                {
                    IsGrounded = true;
                    IsOnSteepSlope = false;
                    lastGroundedTime = Time.time;
                }
                else
                {
                    IsGrounded = false;
                    IsOnSteepSlope = true;
                }
            }
            else
            {
                IsGrounded = false;
                IsOnSteepSlope = false;
                GroundNormal = Vector3.up;
                currentGroundAngle = 0f;
            }

            SetAnimatorBool("IsGrounded", IsGrounded);
        }

        private void ApplyCustomGravity(float deltaTime)
        {
            if (IsGrounded && VerticalVelocity <= 0f)
            {
                VerticalVelocity = groundedGravity;
            }
            else
            {
                float gravityMultiplier = FallMultiplier;

                if (VerticalVelocity > 0f)
                {
                    gravityMultiplier = Input.IsJumpHeld ? AscendMultiplier : LowJumpMultiplier;
                }

                VerticalVelocity += BaseGravity * gravityMultiplier * deltaTime;
                VerticalVelocity = Mathf.Max(VerticalVelocity, MaxFallSpeed);
            }

            SetAnimatorFloat("VerticalVelocity", VerticalVelocity);
        }

        public bool CanExecuteJump()
        {
            bool withinCoyoteWindow = (Time.time - lastGroundedTime) <= CoyoteTime;
            bool recentlyJumped = (Time.time - lastJumpExecutedTime) <= (CoyoteTime + 0.05f);
            bool hasStamina = Stats.HasEnoughStamina(JumpStaminaCost);
            return (IsGrounded || withinCoyoteWindow) && !recentlyJumped && !IsOnSteepSlope && hasStamina;
        }

        public void ExecuteJumpImpulse()
        {
            Stats.TryConsumeStamina(JumpStaminaCost);
            VerticalVelocity = JumpForce;
            IsGrounded = false;
            lastGroundedTime = -10f;
            lastJumpExecutedTime = Time.time;
            SetAnimatorBool("IsGrounded", false);
        }

        public void HandleAirborneMovement(float deltaTime)
        {
            Vector3 inputDir = GetCameraRelativeMovementInput();

            if (inputDir.sqrMagnitude > 0.01f)
            {
                Vector3 targetAirVelocity = inputDir * MoveSpeed;
                float steerRate = AirSteeringAcceleration * AirControlMultiplier;
                HorizontalVelocity = Vector3.MoveTowards(HorizontalVelocity, targetAirVelocity, steerRate * deltaTime);

                RotateTowardsVector(inputDir, immediate: false, customSmoothTime: RotationSmoothTime / Mathf.Max(0.15f, AirControlMultiplier));
            }
            else
            {
                HorizontalVelocity = Vector3.MoveTowards(HorizontalVelocity, Vector3.zero, 1.5f * deltaTime);
            }

            if (IsOnSteepSlope)
            {
                Vector3 slopeSlideDir = Vector3.ProjectOnPlane(Vector3.down, GroundNormal).normalized;
                HorizontalVelocity += slopeSlideDir * (Mathf.Abs(BaseGravity) * 0.5f * deltaTime);
            }

            MoveCharacter(HorizontalVelocity, deltaTime);
        }

        #endregion

        #region Locomotion & Combat Helpers

        public Vector3 GetCameraRelativeMovementInput()
        {
            Vector2 moveInput = Input.MoveInput;
            if (moveInput.sqrMagnitude < 0.01f) return Vector3.zero;

            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 moveDir = (camForward * moveInput.y + camRight * moveInput.x);
            return moveDir.sqrMagnitude > 1f ? moveDir.normalized : moveDir;
        }

        public void MoveCharacter(Vector3 desiredHorizontalVelocity, float deltaTime)
        {
            HorizontalVelocity = new Vector3(desiredHorizontalVelocity.x, 0f, desiredHorizontalVelocity.z);

            Vector3 combinedHorizontal = HorizontalVelocity + externalPushbackVelocity;
            Vector3 finalDisplacement = combinedHorizontal;

            if (IsGrounded && VerticalVelocity <= 0f)
            {
                finalDisplacement = Vector3.ProjectOnPlane(combinedHorizontal, GroundNormal).normalized * combinedHorizontal.magnitude;
            }

            finalDisplacement.y += VerticalVelocity;
            CollisionFlags flags = Controller.Move(finalDisplacement * deltaTime);

            if ((flags & CollisionFlags.Above) != 0 && VerticalVelocity > 0f)
            {
                VerticalVelocity = 0f;
            }
        }

        public void RotateTowardsVector(Vector3 direction, bool immediate = false, float customSmoothTime = -1f)
        {
            if (direction.sqrMagnitude < 0.001f) return;

            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            if (immediate)
            {
                transform.rotation = Quaternion.Euler(0f, targetAngle, 0f);
                return;
            }

            float smoothTime = customSmoothTime > 0f ? customSmoothTime : RotationSmoothTime;
            float smoothedAngle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetAngle,
                ref turnSmoothVelocity,
                smoothTime
            );
            transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);
        }

        public bool CanPerformRoll()
        {
            bool cooldownReady = Time.time >= LastRollEndTime + RollCooldown;
            bool hasStamina = Stats.HasEnoughStamina(RollStaminaCost);
            return IsGrounded && cooldownReady && hasStamina;
        }

        public bool CanPerformAttack()
        {
            return IsGrounded && Stats.CurrentStamina > 0f;
        }

        public void SetInvincibility(bool state)
        {
            isInvincible = state;
            if (hurtboxCollider != null)
            {
                hurtboxCollider.enabled = !state;
            }
        }

        public void PlayAnimationCrossFade(string stateName, float transitionDuration = 0.1f)
        {
            if (animator != null && !string.IsNullOrEmpty(stateName))
            {
                animator.CrossFadeInFixedTime(stateName, transitionDuration);
            }
        }

        public void SetAnimatorFloat(string paramName, float value)
        {
            if (animator != null) animator.SetFloat(paramName, value, 0.1f, Time.deltaTime);
        }

        public void SetAnimatorBool(string paramName, bool value)
        {
            if (animator != null) animator.SetBool(paramName, value);
        }

        #endregion
    }

    // ===================================================================================
    // MODULAR FINITE STATE MACHINE STATES
    // ===================================================================================

    public abstract class PlayerBaseState
    {
        protected readonly PlayerController ctx;
        protected PlayerBaseState(PlayerController controller) { ctx = controller; }

        public abstract void Enter();
        public abstract void Tick(float deltaTime);
        public abstract void Exit();

        protected bool CheckGroundedStateTransitions()
        {
            if (ctx.Input.HasBufferedJump && ctx.CanExecuteJump())
            {
                ctx.Input.ConsumeJumpBuffer();
                ctx.TransitionToState(PlayerStateType.Jumping);
                return true;
            }

            if (!ctx.IsGrounded && !ctx.CanExecuteJump())
            {
                ctx.TransitionToState(PlayerStateType.Falling);
                return true;
            }

            if (ctx.Input.HasBufferedRoll && ctx.CanPerformRoll())
            {
                ctx.Input.ConsumeRollBuffer();
                ctx.TransitionToState(PlayerStateType.Rolling);
                return true;
            }

            if (ctx.Input.HasBufferedAttack && ctx.CanExecuteRiposte())
            {
                ctx.Input.ConsumeAttackBuffer();
                ctx.TransitionToState(PlayerStateType.Riposting);
                return true;
            }

            if (ctx.Input.IsBlocking && ctx.IsGrounded)
            {
                ctx.TransitionToState(PlayerStateType.Blocking);
                return true;
            }

            if (ctx.Input.HasBufferedAttack && ctx.CanPerformAttack())
            {
                ctx.Input.ConsumeAttackBuffer();
                ctx.TransitionToState(PlayerStateType.Attacking);
                return true;
            }

            return false;
        }
    }

    public class IdleState : PlayerBaseState
    {
        public IdleState(PlayerController controller) : base(controller) { }

        public override void Enter()
        {
            ctx.SetAnimatorFloat("Speed", 0f);
        }

        public override void Tick(float deltaTime)
        {
            ctx.MoveCharacter(Vector3.zero, deltaTime);

            if (CheckGroundedStateTransitions()) return;

            if (ctx.Input.MoveInput.sqrMagnitude > 0.01f)
            {
                ctx.TransitionToState(PlayerStateType.Moving);
            }
        }

        public override void Exit() { }
    }

    public class MovingState : PlayerBaseState
    {
        public MovingState(PlayerController controller) : base(controller) { }

        public override void Enter() { }

        public override void Tick(float deltaTime)
        {
            if (CheckGroundedStateTransitions()) return;

            Vector3 moveDir = ctx.GetCameraRelativeMovementInput();
            if (moveDir.sqrMagnitude < 0.01f)
            {
                ctx.TransitionToState(PlayerStateType.Idle);
                return;
            }

            ctx.RotateTowardsVector(moveDir);
            ctx.MoveCharacter(moveDir * ctx.MoveSpeed, deltaTime);
            ctx.SetAnimatorFloat("Speed", moveDir.magnitude);
        }

        public override void Exit() { }
    }

    public class JumpingState : PlayerBaseState
    {
        public JumpingState(PlayerController controller) : base(controller) { }

        public override void Enter()
        {
            ctx.ExecuteJumpImpulse();
            ctx.PlayAnimationCrossFade("Jump", 0.08f);
        }

        public override void Tick(float deltaTime)
        {
            if (ctx.Input.HasBufferedRoll) ctx.Input.ConsumeRollBuffer();
            if (ctx.Input.HasBufferedAttack) ctx.Input.ConsumeAttackBuffer();

            ctx.HandleAirborneMovement(deltaTime);

            if (ctx.VerticalVelocity <= 0f)
            {
                ctx.TransitionToState(PlayerStateType.Falling);
                return;
            }

            if (ctx.IsGrounded)
            {
                if (ctx.Input.IsBlocking)
                    ctx.TransitionToState(PlayerStateType.Blocking);
                else if (ctx.Input.MoveInput.sqrMagnitude > 0.01f)
                    ctx.TransitionToState(PlayerStateType.Moving);
                else
                    ctx.TransitionToState(PlayerStateType.Idle);
            }
        }

        public override void Exit() { }
    }

    public class FallingState : PlayerBaseState
    {
        public FallingState(PlayerController controller) : base(controller) { }

        public override void Enter()
        {
            ctx.PlayAnimationCrossFade("Fall", 0.12f);
        }

        public override void Tick(float deltaTime)
        {
            if (ctx.Input.HasBufferedRoll) ctx.Input.ConsumeRollBuffer();
            if (ctx.Input.HasBufferedAttack) ctx.Input.ConsumeAttackBuffer();

            ctx.HandleAirborneMovement(deltaTime);

            if (ctx.IsGrounded)
            {
                if (ctx.Input.HasBufferedJump && ctx.CanExecuteJump())
                {
                    ctx.Input.ConsumeJumpBuffer();
                    ctx.TransitionToState(PlayerStateType.Jumping);
                    return;
                }

                if (ctx.Input.IsBlocking)
                    ctx.TransitionToState(PlayerStateType.Blocking);
                else if (ctx.Input.MoveInput.sqrMagnitude > 0.01f)
                    ctx.TransitionToState(PlayerStateType.Moving);
                else
                    ctx.TransitionToState(PlayerStateType.Idle);
            }
        }

        public override void Exit() { }
    }

    public class BlockingState : PlayerBaseState
    {
        public BlockingState(PlayerController controller) : base(controller) { }

        public override void Enter()
        {
            ctx.RegisterBlockStart();
            ctx.SetAnimatorBool("IsBlocking", true);
            ctx.Input.ConsumeAttackBuffer();
        }

        public override void Tick(float deltaTime)
        {
            if (ctx.Input.HasBufferedAttack && ctx.CanExecuteRiposte())
            {
                ctx.Input.ConsumeAttackBuffer();
                ctx.TransitionToState(PlayerStateType.Riposting);
                return;
            }

            if (ctx.Input.HasBufferedAttack)
            {
                ctx.Input.ConsumeAttackBuffer();
            }

            if (ctx.Input.HasBufferedJump && ctx.CanExecuteJump())
            {
                ctx.Input.ConsumeJumpBuffer();
                ctx.TransitionToState(PlayerStateType.Jumping);
                return;
            }

            if (!ctx.IsGrounded && !ctx.CanExecuteJump())
            {
                ctx.TransitionToState(PlayerStateType.Falling);
                return;
            }

            if (ctx.Input.HasBufferedRoll && ctx.CanPerformRoll())
            {
                ctx.Input.ConsumeRollBuffer();
                ctx.TransitionToState(PlayerStateType.Rolling);
                return;
            }

            if (!ctx.Input.IsBlocking)
            {
                ctx.TransitionToState(ctx.Input.MoveInput.sqrMagnitude > 0.01f
                    ? PlayerStateType.Moving
                    : PlayerStateType.Idle);
                return;
            }

            Vector3 cameraForward = ctx.CamTransform.forward;
            cameraForward.y = 0f;
            ctx.RotateTowardsVector(cameraForward.normalized);

            Vector3 moveDir = ctx.GetCameraRelativeMovementInput();
            ctx.MoveCharacter(moveDir * ctx.BlockMoveSpeed, deltaTime);
            ctx.SetAnimatorFloat("Speed", moveDir.magnitude * 0.5f);
        }

        public override void Exit()
        {
            ctx.RegisterBlockRelease();
            ctx.SetAnimatorBool("IsBlocking", false);
        }
    }

    public class GuardBrokenState : PlayerBaseState
    {
        private float stunTimer;

        public GuardBrokenState(PlayerController controller) : base(controller) { }

        public override void Enter()
        {
            stunTimer = 0f;
            ctx.PlayAnimationCrossFade("Guard_Break", 0.08f);
            ctx.SetAnimatorBool("IsGuardBroken", true);
        }

        public override void Tick(float deltaTime)
        {
            stunTimer += deltaTime;

            if (ctx.Input.HasBufferedJump) ctx.Input.ConsumeJumpBuffer();
            if (ctx.Input.HasBufferedRoll) ctx.Input.ConsumeRollBuffer();
            if (ctx.Input.HasBufferedAttack) ctx.Input.ConsumeAttackBuffer();

            ctx.MoveCharacter(Vector3.zero, deltaTime);

            if (stunTimer >= ctx.GuardBreakDuration)
            {
                ctx.Stats.RestoreStamina(ctx.Stats.MaxStamina * 0.25f);

                if (!ctx.IsGrounded)
                    ctx.TransitionToState(PlayerStateType.Falling);
                else if (ctx.Input.MoveInput.sqrMagnitude > 0.01f)
                    ctx.TransitionToState(PlayerStateType.Moving);
                else
                    ctx.TransitionToState(PlayerStateType.Idle);
            }
        }

        public override void Exit()
        {
            ctx.SetAnimatorBool("IsGuardBroken", false);
        }
    }

    public class RipostingState : PlayerBaseState
    {
        private float stateTimer;
        private bool damageDealt;

        public RipostingState(PlayerController controller) : base(controller) { }

        public override void Enter()
        {
            stateTimer = 0f;
            damageDealt = false;
            ctx.ConsumeRiposteWindow();
            ctx.SetInvincibility(true);

            if (ctx.LastParriedAttacker != null && ctx.LastParriedAttacker.GetTransform() != null)
            {
                Vector3 dirToEnemy = ctx.LastParriedAttacker.GetTransform().position - ctx.transform.position;
                dirToEnemy.y = 0f;
                ctx.RotateTowardsVector(dirToEnemy.normalized, immediate: true);
            }

            ctx.PlayAnimationCrossFade(ctx.RiposteAttack.AnimationStateName, 0.05f);
            ctx.OnRiposteExecuted?.Invoke();
        }

        public override void Tick(float deltaTime)
        {
            stateTimer += deltaTime;
            float normalizedTime = Mathf.Clamp01(stateTimer / ctx.RiposteAttack.TotalDuration);

            if (ctx.Input.HasBufferedJump) ctx.Input.ConsumeJumpBuffer();

            if (!damageDealt && normalizedTime >= 0.35f)
            {
                damageDealt = true;
                if (ctx.LastParriedAttacker != null && ctx.LastParriedAttacker.GetTransform() != null)
                {
                    if (ctx.LastParriedAttacker.GetTransform().TryGetComponent<IDamageable>(out var enemyDamageable))
                    {
                        float dist = Vector3.Distance(ctx.transform.position, ctx.LastParriedAttacker.GetTransform().position);
                        if (dist <= 3.5f)
                        {
                            AttackData riposteHit = new AttackData(
                                healthDamage: ctx.RiposteDamage,
                                staminaDamage: 100f,
                                pushbackForce: 10f,
                                hitOrigin: ctx.transform.position,
                                attacker: null,
                                canBeParried: false
                            );
                            enemyDamageable.ReceiveAttack(riposteHit);
                        }
                    }
                }
            }

            Vector3 velocity = normalizedTime < 0.4f
                ? ctx.transform.forward * ctx.RiposteAttack.ForwardLungeSpeed
                : Vector3.zero;

            ctx.MoveCharacter(velocity, deltaTime);

            if (normalizedTime >= ctx.RiposteAttack.RecoveryWindowStart && ctx.Input.HasBufferedRoll && ctx.CanPerformRoll())
            {
                ctx.Input.ConsumeRollBuffer();
                ctx.TransitionToState(PlayerStateType.Rolling);
                return;
            }

            if (stateTimer >= ctx.RiposteAttack.TotalDuration)
            {
                ctx.TransitionToState(ctx.Input.MoveInput.sqrMagnitude > 0.01f
                    ? PlayerStateType.Moving
                    : PlayerStateType.Idle);
            }
        }

        public override void Exit()
        {
            ctx.SetInvincibility(false);
            ctx.LastAttackEndTime = Time.time;
        }
    }

    public class RollingState : PlayerBaseState
    {
        private float rollTimer;
        private Vector3 rollDirection;

        public RollingState(PlayerController controller) : base(controller) { }

        public override void Enter()
        {
            rollTimer = 0f;
            ctx.Stats.TryConsumeStamina(ctx.RollStaminaCost, allowPartialDrain: true);
            ctx.NotifyRollCooldownStarted();

            Vector3 inputDir = ctx.GetCameraRelativeMovementInput();
            rollDirection = inputDir.sqrMagnitude > 0.01f ? inputDir.normalized : ctx.transform.forward;

            ctx.RotateTowardsVector(rollDirection, immediate: true);
            ctx.PlayAnimationCrossFade("Roll", 0.05f);
        }

        public override void Tick(float deltaTime)
        {
            rollTimer += deltaTime;

            if (ctx.Input.HasBufferedJump) ctx.Input.ConsumeJumpBuffer();

            if (rollTimer < ctx.RollDuration * 0.75f && ctx.Input.HasBufferedRoll)
            {
                ctx.Input.ConsumeRollBuffer();
            }

            bool inIFrameWindow = rollTimer >= ctx.IFrameStartTime && rollTimer <= ctx.IFrameEndTime;
            ctx.SetInvincibility(inIFrameWindow);

            float normalizedTime = Mathf.Clamp01(rollTimer / ctx.RollDuration);
            float speedMultiplier = Mathf.Lerp(1.15f, 0.6f, normalizedTime);
            ctx.MoveCharacter(rollDirection * (ctx.RollSpeed * speedMultiplier), deltaTime);

            if (rollTimer >= ctx.RollDuration)
            {
                if (!ctx.IsGrounded)
                    ctx.TransitionToState(PlayerStateType.Falling);
                else if (ctx.Input.IsBlocking)
                    ctx.TransitionToState(PlayerStateType.Blocking);
                else if (ctx.Input.MoveInput.sqrMagnitude > 0.01f)
                    ctx.TransitionToState(PlayerStateType.Moving);
                else
                    ctx.TransitionToState(PlayerStateType.Idle);
            }
        }

        public override void Exit()
        {
            ctx.SetInvincibility(false);
            ctx.LastRollEndTime = Time.time;
        }
    }

    public class AttackingState : PlayerBaseState
    {
        private float stateTimer;
        private bool comboQueued;
        private AttackStepData currentStepData;

        public AttackingState(PlayerController controller) : base(controller) { }

        public override void Enter()
        {
            if (Time.time - ctx.LastAttackEndTime > ctx.ComboResetTime)
            {
                ctx.CurrentComboIndex = 0;
            }

            StartComboStep(ctx.CurrentComboIndex);
        }

        private void StartComboStep(int index)
        {
            stateTimer = 0f;
            comboQueued = false;
            currentStepData = ctx.LightCombo[index];

            float stepStaminaCost = currentStepData.StaminaCost > 0f ? currentStepData.StaminaCost : 14f;
            ctx.Stats.TryConsumeStamina(stepStaminaCost, allowPartialDrain: true);

            Vector3 steerDir = ctx.GetCameraRelativeMovementInput();
            if (steerDir.sqrMagnitude > 0.01f)
            {
                ctx.RotateTowardsVector(steerDir, immediate: false);
            }

            ctx.PlayAnimationCrossFade(currentStepData.AnimationStateName, 0.08f);
        }

        public override void Tick(float deltaTime)
        {
            stateTimer += deltaTime;
            float normalizedTime = Mathf.Clamp01(stateTimer / currentStepData.TotalDuration);

            if (ctx.Input.HasBufferedJump) ctx.Input.ConsumeJumpBuffer();

            if (!ctx.IsGrounded && !ctx.CanExecuteJump())
            {
                ctx.TransitionToState(PlayerStateType.Falling);
                return;
            }

            if (normalizedTime >= currentStepData.ComboWindowStart && ctx.Input.HasBufferedAttack)
            {
                ctx.Input.ConsumeAttackBuffer();
                if (ctx.CurrentComboIndex < ctx.LightCombo.Length - 1 && ctx.Stats.CurrentStamina > 0f)
                {
                    comboQueued = true;
                }
            }

            if (normalizedTime >= currentStepData.RecoveryWindowStart)
            {
                if (ctx.Input.HasBufferedRoll && ctx.CanPerformRoll())
                {
                    ctx.Input.ConsumeRollBuffer();
                    ctx.TransitionToState(PlayerStateType.Rolling);
                    return;
                }

                if (ctx.Input.IsBlocking && ctx.IsGrounded)
                {
                    ctx.TransitionToState(PlayerStateType.Blocking);
                    return;
                }

                if (comboQueued)
                {
                    ctx.CurrentComboIndex++;
                    ctx.LastAttackEndTime = Time.time;
                    StartComboStep(ctx.CurrentComboIndex);
                    return;
                }
            }

            Vector3 velocity = Vector3.zero;
            if (normalizedTime < 0.35f)
            {
                velocity = ctx.transform.forward * currentStepData.ForwardLungeSpeed;
            }
            else
            {
                Vector3 moveDir = ctx.GetCameraRelativeMovementInput();
                velocity = moveDir * ctx.AttackMoveSpeed;
            }
            ctx.MoveCharacter(velocity, deltaTime);

            if (stateTimer >= currentStepData.TotalDuration)
            {
                ctx.LastAttackEndTime = Time.time;
                ctx.CurrentComboIndex = (ctx.CurrentComboIndex + 1) % ctx.LightCombo.Length;

                ctx.TransitionToState(ctx.Input.MoveInput.sqrMagnitude > 0.01f
                    ? PlayerStateType.Moving
                    : PlayerStateType.Idle);
            }
        }

        public override void Exit()
        {
            ctx.LastAttackEndTime = Time.time;
        }
    }
}