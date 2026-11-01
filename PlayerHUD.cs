using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ActionRPG.Character.UI
{
    public class PlayerHUD : MonoBehaviour
    {
        [Header("Player References")]
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerInputReader inputReader;

        [Header("Top-Left Status Bars")]
        [SerializeField] private Image healthFillImage;
        [SerializeField] private Image healthGhostImage;
        [SerializeField] private Image staminaFillImage;
        [SerializeField] private Image staminaGhostImage;
        [SerializeField] private Image mpFillImage;
        [SerializeField] private Image mpGhostImage;

        [Header("Ghost Bar Drain Tuning (Souls-like Trail)")]
        [SerializeField] private float ghostDrainDelay = 0.40f;
        [SerializeField] private float ghostDrainSpeed = 0.65f;

        [Header("Bottom-Left Dodge Roll Cooldown Widget")]
        [SerializeField] private CanvasGroup rollWidgetCanvasGroup;
        [SerializeField] private Image rollCooldownFillImage;
        [SerializeField] private Image rollReadyFlashImage;
        [SerializeField] private RectTransform rollWidgetRect;
        [SerializeField] private float rollIdleAlpha = 0.28f;
        [SerializeField] private float rollActiveAlpha = 1.0f;

        [Header("Bottom-Right Keybind Prompts (CanvasGroups)")]
        [SerializeField] private CanvasGroup promptWASD;
        [SerializeField] private CanvasGroup promptScroll;
        [SerializeField] private CanvasGroup promptJump;
        [SerializeField] private CanvasGroup promptRoll;
        [SerializeField] private CanvasGroup promptAttack;
        [SerializeField] private CanvasGroup promptBlock;

        [Header("Keybind Prompt Highlight Tuning")]
        [SerializeField] private float promptNormalAlpha = 0.65f;
        [SerializeField] private float promptHighlightAlpha = 1.0f;
        [SerializeField] private float promptDisabledAlpha = 0.22f;
        [SerializeField] private float promptPunchScale = 1.18f;

        private Coroutine healthGhostRoutine;
        private Coroutine staminaGhostRoutine;
        private Coroutine mpGhostRoutine;
        private Coroutine rollCooldownRoutine;

        private bool isRollOnCooldown;
        private Sprite generatedWhiteSprite;

        private void Awake()
        {
            if (playerStats == null) playerStats = FindFirstObjectByType<PlayerStats>();
            if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
            if (inputReader == null) inputReader = FindFirstObjectByType<PlayerInputReader>();

            // Create a white sprite in memory so the bars can shrink/grow using fillAmount
            Texture2D tex = Texture2D.whiteTexture;
            generatedWhiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

            // Turn the images into Horizontal/Radial progress bars WITHOUT moving or resizing them!
            AutoConfigureBarImage(healthFillImage, Image.FillMethod.Horizontal);
            AutoConfigureBarImage(healthGhostImage, Image.FillMethod.Horizontal);
            AutoConfigureBarImage(staminaFillImage, Image.FillMethod.Horizontal);
            AutoConfigureBarImage(staminaGhostImage, Image.FillMethod.Horizontal);
            AutoConfigureBarImage(mpFillImage, Image.FillMethod.Horizontal);
            AutoConfigureBarImage(mpGhostImage, Image.FillMethod.Horizontal);
            AutoConfigureBarImage(rollCooldownFillImage, Image.FillMethod.Radial360);

            InitializeRollWidgetVisuals();
            InitializePromptVisuals();
        }

        private void AutoConfigureBarImage(Image img, Image.FillMethod fillMethod)
        {
            if (img == null) return;

            if (img.sprite == null)
            {
                img.sprite = generatedWhiteSprite;
            }

            img.type = Image.Type.Filled;
            img.fillMethod = fillMethod;
            img.fillOrigin = 0; // Left for Horizontal, Bottom for Radial
            img.fillAmount = 1f;
        }

        private void OnEnable()
        {
            if (playerStats != null)
            {
                playerStats.OnHealthChanged += HandleHealthChanged;
                playerStats.OnStaminaChanged += HandleStaminaChanged;
                playerStats.OnMPChanged += HandleMPChanged;
            }

            if (playerController != null)
            {
                playerController.OnRollCooldownStarted += HandleRollCooldownStarted;
            }

            if (inputReader != null)
            {
                inputReader.OnMoveInputChanged += HandleMovePrompt;
                inputReader.OnZoomInputTriggered += HandleZoomPrompt;
                inputReader.OnJumpInputTriggered += () => TriggerPromptPulse(promptJump);
                inputReader.OnRollInputTriggered += () => TriggerPromptPulse(promptRoll);
                inputReader.OnAttackInputTriggered += () => TriggerPromptPulse(promptAttack);
                inputReader.OnBlockStateChanged += HandleBlockPrompt;
            }
        }

        private void OnDisable()
        {
            if (playerStats != null)
            {
                playerStats.OnHealthChanged -= HandleHealthChanged;
                playerStats.OnStaminaChanged -= HandleStaminaChanged;
                playerStats.OnMPChanged -= HandleMPChanged;
            }

            if (playerController != null)
            {
                playerController.OnRollCooldownStarted -= HandleRollCooldownStarted;
            }

            if (inputReader != null)
            {
                inputReader.OnMoveInputChanged -= HandleMovePrompt;
                inputReader.OnZoomInputTriggered -= HandleZoomPrompt;
                inputReader.OnBlockStateChanged -= HandleBlockPrompt;
            }
        }

        #region 1. Status Bars & Ghost Drain Logic

        private void HandleHealthChanged(float current, float max)
        {
            float targetRatio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            UpdateBarWithGhostTrail(healthFillImage, healthGhostImage, targetRatio, ref healthGhostRoutine);
        }

        private void HandleStaminaChanged(float current, float max)
        {
            float targetRatio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            UpdateBarWithGhostTrail(staminaFillImage, staminaGhostImage, targetRatio, ref staminaGhostRoutine);
            RefreshActionPromptAvailability();
        }

        private void HandleMPChanged(float current, float max)
        {
            float targetRatio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            UpdateBarWithGhostTrail(mpFillImage, mpGhostImage, targetRatio, ref mpGhostRoutine);
        }

        private void UpdateBarWithGhostTrail(Image primaryBar, Image ghostBar, float targetFill, ref Coroutine ghostCoroutine)
        {
            if (primaryBar == null) return;

            float previousFill = primaryBar.fillAmount;
            primaryBar.fillAmount = targetFill;

            if (ghostBar == null) return;

            if (targetFill >= ghostBar.fillAmount)
            {
                if (ghostCoroutine != null)
                {
                    StopCoroutine(ghostCoroutine);
                    ghostCoroutine = null;
                }
                ghostBar.fillAmount = targetFill;
            }
            else if (targetFill < previousFill)
            {
                if (ghostCoroutine != null)
                    StopCoroutine(ghostCoroutine);

                ghostCoroutine = StartCoroutine(AnimateGhostBarRoutine(ghostBar, targetFill));
            }
        }

        private IEnumerator AnimateGhostBarRoutine(Image ghostBar, float targetFill)
        {
            yield return new WaitForSeconds(ghostDrainDelay);

            while (ghostBar.fillAmount > targetFill + 0.001f)
            {
                ghostBar.fillAmount = Mathf.MoveTowards(ghostBar.fillAmount, targetFill, ghostDrainSpeed * Time.deltaTime);
                yield return null;
            }

            ghostBar.fillAmount = targetFill;
        }

        #endregion

        #region 2. Bottom-Left Dodge Roll Cooldown Indicator

        private void InitializeRollWidgetVisuals()
        {
            if (rollWidgetCanvasGroup != null)
                rollWidgetCanvasGroup.alpha = rollIdleAlpha;

            if (rollCooldownFillImage != null)
                rollCooldownFillImage.fillAmount = 1f;

            if (rollReadyFlashImage != null)
                rollReadyFlashImage.color = new Color(1f, 1f, 1f, 0f);
        }

        private void HandleRollCooldownStarted(float totalCooldownDuration)
        {
            if (rollCooldownRoutine != null)
                StopCoroutine(rollCooldownRoutine);

            rollCooldownRoutine = StartCoroutine(RollCooldownRoutine(totalCooldownDuration));
        }

        private IEnumerator RollCooldownRoutine(float duration)
        {
            isRollOnCooldown = true;
            RefreshActionPromptAvailability();

            if (rollWidgetCanvasGroup != null)
                rollWidgetCanvasGroup.alpha = rollActiveAlpha;

            if (rollCooldownFillImage != null)
                rollCooldownFillImage.fillAmount = 0f;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (rollCooldownFillImage != null)
                    rollCooldownFillImage.fillAmount = Mathf.Clamp01(elapsed / duration);

                yield return null;
            }

            if (rollCooldownFillImage != null)
                rollCooldownFillImage.fillAmount = 1f;

            isRollOnCooldown = false;
            RefreshActionPromptAvailability();

            float flashDuration = 0.28f;
            float flashTimer = 0f;

            while (flashTimer < flashDuration)
            {
                flashTimer += Time.deltaTime;
                float t = flashTimer / flashDuration;

                if (rollReadyFlashImage != null)
                    rollReadyFlashImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.85f, 0f, t));

                if (rollWidgetRect != null)
                {
                    float scale = Mathf.Lerp(1.25f, 1.0f, t);
                    rollWidgetRect.localScale = new Vector3(scale, scale, 1f);
                }

                yield return null;
            }

            if (rollWidgetCanvasGroup != null)
                rollWidgetCanvasGroup.alpha = rollIdleAlpha;

            rollCooldownRoutine = null;
        }

        #endregion

        #region 3. Bottom-Right Controls & Dynamic Keybind Prompts

        private void InitializePromptVisuals()
        {
            SetPromptAlpha(promptWASD, promptNormalAlpha);
            SetPromptAlpha(promptScroll, promptNormalAlpha);
            SetPromptAlpha(promptJump, promptNormalAlpha);
            SetPromptAlpha(promptRoll, promptNormalAlpha);
            SetPromptAlpha(promptAttack, promptNormalAlpha);
            SetPromptAlpha(promptBlock, promptNormalAlpha);
        }

        private void HandleMovePrompt(bool isMoving)
        {
            if (promptWASD == null) return;
            promptWASD.alpha = isMoving ? promptHighlightAlpha : promptNormalAlpha;
            promptWASD.transform.localScale = isMoving ? Vector3.one * 1.08f : Vector3.one;
        }

        private void HandleZoomPrompt()
        {
            TriggerPromptPulse(promptScroll);
        }

        private void HandleBlockPrompt(bool isBlocking)
        {
            if (promptBlock == null) return;
            promptBlock.alpha = isBlocking ? promptHighlightAlpha : promptNormalAlpha;
            promptBlock.transform.localScale = isBlocking ? Vector3.one * promptPunchScale : Vector3.one;
        }

        private void RefreshActionPromptAvailability()
        {
            if (playerStats == null || playerController == null) return;

            bool canRoll = !isRollOnCooldown && playerStats.HasEnoughStamina(playerController.RollStaminaCost);
            bool canJump = playerStats.HasEnoughStamina(playerController.JumpStaminaCost);
            bool canAttack = playerStats.CurrentStamina > 0f;

            if (promptRoll != null)
                promptRoll.alpha = canRoll ? promptNormalAlpha : promptDisabledAlpha;

            if (promptJump != null)
                promptJump.alpha = canJump ? promptNormalAlpha : promptDisabledAlpha;

            if (promptAttack != null)
                promptAttack.alpha = canAttack ? promptNormalAlpha : promptDisabledAlpha;
        }

        private void TriggerPromptPulse(CanvasGroup promptGroup)
        {
            if (promptGroup == null) return;
            StartCoroutine(PulsePromptRoutine(promptGroup));
        }

        private IEnumerator PulsePromptRoutine(CanvasGroup promptGroup)
        {
            Transform tr = promptGroup.transform;
            promptGroup.alpha = promptHighlightAlpha;
            tr.localScale = Vector3.one * promptPunchScale;

            float elapsed = 0f;
            float duration = 0.16f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                tr.localScale = Vector3.Lerp(Vector3.one * promptPunchScale, Vector3.one, t);
                yield return null;
            }

            tr.localScale = Vector3.one;
            RefreshActionPromptAvailability();
            if (promptGroup == promptScroll)
                promptGroup.alpha = promptNormalAlpha;
        }

        private void SetPromptAlpha(CanvasGroup cg, float alpha)
        {
            if (cg != null) cg.alpha = alpha;
        }

        #endregion
    }
}