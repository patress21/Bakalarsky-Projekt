using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionRPG.Character
{
    public class PlayerInputReader : MonoBehaviour
    {
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public float ZoomInput { get; private set; }
        public bool IsBlocking { get; private set; }
        public bool IsJumpHeld { get; private set; }
        public bool GameplayInputBlocked { get; private set; }

        [Header("Input Buffer Settings")]
        [SerializeField] private float combatBufferTime = 0.2f;
        [SerializeField] private float jumpBufferTime = 0.15f;

        private float lastAttackPressedTime = -10f;
        private float lastRollPressedTime = -10f;
        private float lastJumpPressedTime = -10f;

        public bool HasBufferedAttack => !GameplayInputBlocked && Time.time - lastAttackPressedTime <= combatBufferTime;
        public bool HasBufferedRoll => !GameplayInputBlocked && Time.time - lastRollPressedTime <= combatBufferTime;
        public bool HasBufferedJump => !GameplayInputBlocked && Time.time - lastJumpPressedTime <= jumpBufferTime;

        public void ConsumeAttackBuffer() => lastAttackPressedTime = -10f;
        public void ConsumeRollBuffer() => lastRollPressedTime = -10f;
        public void ConsumeJumpBuffer() => lastJumpPressedTime = -10f;

        public event Action<bool> OnMoveInputChanged;
        public event Action OnZoomInputTriggered;
        public event Action OnJumpInputTriggered;
        public event Action OnRollInputTriggered;
        public event Action OnAttackInputTriggered;
        public event Action<bool> OnBlockStateChanged;

        public event Action OnInventoryTogglePressed;
        public event Action OnAttributesTogglePressed;
        public event Action OnPassiveSkillsTogglePressed;
        public event Action OnWeaponSkillsTogglePressed;
        public event Action OnCloseMenuPressed;

        private bool wasMovingLastFrame;
        private bool wasBlockingLastFrame;

        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction zoomAction;
        private InputAction attackAction;
        private InputAction rollAction;
        private InputAction jumpAction;
        private InputAction blockAction;

        private InputAction inventoryAction;
        private InputAction attributesAction;
        private InputAction passiveSkillsAction;
        private InputAction weaponSkillsAction;
        private InputAction closeMenuAction;

        private void Awake()
        {
            SetupInputActions();
        }

        public void SetGameplayInputBlocked(bool blocked)
        {
            GameplayInputBlocked = blocked;

            if (blocked)
            {
                MoveInput = Vector2.zero;
                LookInput = Vector2.zero;
                ZoomInput = 0f;
                IsBlocking = false;
                IsJumpHeld = false;

                ConsumeAttackBuffer();
                ConsumeRollBuffer();
                ConsumeJumpBuffer();

                if (wasMovingLastFrame)
                {
                    wasMovingLastFrame = false;
                    OnMoveInputChanged?.Invoke(false);
                }

                if (wasBlockingLastFrame)
                {
                    wasBlockingLastFrame = false;
                    OnBlockStateChanged?.Invoke(false);
                }
            }
        }

        private void SetupInputActions()
        {
            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddBinding("<Gamepad>/leftStick");

            lookAction = new InputAction("Look", InputActionType.Value);
            lookAction.AddBinding("<Mouse>/delta");
            lookAction.AddBinding("<Gamepad>/rightStick");

            zoomAction = new InputAction("Zoom", InputActionType.Value, "<Mouse>/scroll/y");

            attackAction = new InputAction("Attack", InputActionType.Button);
            attackAction.AddBinding("<Mouse>/leftButton");
            attackAction.AddBinding("<Gamepad>/buttonWest");
            attackAction.started += _ =>
            {
                if (GameplayInputBlocked) return;
                lastAttackPressedTime = Time.time;
                OnAttackInputTriggered?.Invoke();
            };

            rollAction = new InputAction("Roll", InputActionType.Button);
            rollAction.AddBinding("<Keyboard>/r");
            rollAction.AddBinding("<Gamepad>/buttonEast");
            rollAction.started += _ =>
            {
                if (GameplayInputBlocked) return;
                lastRollPressedTime = Time.time;
                OnRollInputTriggered?.Invoke();
            };

            jumpAction = new InputAction("Jump", InputActionType.Button);
            jumpAction.AddBinding("<Keyboard>/space");
            jumpAction.AddBinding("<Gamepad>/buttonSouth");
            jumpAction.started += _ =>
            {
                if (GameplayInputBlocked) return;
                lastJumpPressedTime = Time.time;
                OnJumpInputTriggered?.Invoke();
            };

            blockAction = new InputAction("Block", InputActionType.Button);
            blockAction.AddBinding("<Mouse>/rightButton");
            blockAction.AddBinding("<Gamepad>/leftShoulder");

            inventoryAction = new InputAction("Inventory", InputActionType.Button, "<Keyboard>/i");
            inventoryAction.started += _ => OnInventoryTogglePressed?.Invoke();

            attributesAction = new InputAction("Attributes", InputActionType.Button, "<Keyboard>/o");
            attributesAction.started += _ => OnAttributesTogglePressed?.Invoke();

            passiveSkillsAction = new InputAction("PassiveSkills", InputActionType.Button, "<Keyboard>/p");
            passiveSkillsAction.started += _ => OnPassiveSkillsTogglePressed?.Invoke();

            weaponSkillsAction = new InputAction("WeaponSkills", InputActionType.Button, "<Keyboard>/l");
            weaponSkillsAction.started += _ => OnWeaponSkillsTogglePressed?.Invoke();

            closeMenuAction = new InputAction("CloseMenu", InputActionType.Button, "<Keyboard>/escape");
            closeMenuAction.started += _ => OnCloseMenuPressed?.Invoke();
        }

        private void OnEnable()
        {
            moveAction.Enable();
            lookAction.Enable();
            zoomAction.Enable();
            attackAction.Enable();
            rollAction.Enable();
            jumpAction.Enable();
            blockAction.Enable();

            inventoryAction.Enable();
            attributesAction.Enable();
            passiveSkillsAction.Enable();
            weaponSkillsAction.Enable();
            closeMenuAction.Enable();
        }

        private void OnDisable()
        {
            moveAction.Disable();
            lookAction.Disable();
            zoomAction.Disable();
            attackAction.Disable();
            rollAction.Disable();
            jumpAction.Disable();
            blockAction.Disable();

            inventoryAction.Disable();
            attributesAction.Disable();
            passiveSkillsAction.Disable();
            weaponSkillsAction.Disable();
            closeMenuAction.Disable();
        }

        private void Update()
        {
            if (GameplayInputBlocked) return;

            MoveInput = moveAction.ReadValue<Vector2>();
            LookInput = lookAction.ReadValue<Vector2>();

            float rawZoom = zoomAction.ReadValue<float>();
            ZoomInput = Mathf.Clamp(rawZoom, -1f, 1f);
            if (Mathf.Abs(ZoomInput) > 0.01f)
            {
                OnZoomInputTriggered?.Invoke();
            }

            IsBlocking = blockAction.IsPressed();
            IsJumpHeld = jumpAction.IsPressed();

            bool isMovingNow = MoveInput.sqrMagnitude > 0.01f;
            if (isMovingNow != wasMovingLastFrame)
            {
                wasMovingLastFrame = isMovingNow;
                OnMoveInputChanged?.Invoke(isMovingNow);
            }

            if (IsBlocking != wasBlockingLastFrame)
            {
                wasBlockingLastFrame = IsBlocking;
                OnBlockStateChanged?.Invoke(IsBlocking);
            }
        }
    }
}