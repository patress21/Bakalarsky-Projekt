using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ActionRPG.Character.UI
{
    public class SkillRowUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI skillNameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Image progressFillImage;
        [SerializeField] private Button rowButton;

        private Action onRowClicked;
        private static Sprite fallbackWhiteSprite;

        private void Awake()
        {
            EnsureFillBarConfigured();
        }

        private void EnsureFillBarConfigured()
        {
            if (progressFillImage == null) return;

            if (progressFillImage.sprite == null)
            {
                if (fallbackWhiteSprite == null)
                {
                    Texture2D tex = Texture2D.whiteTexture;
                    fallbackWhiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
                progressFillImage.sprite = fallbackWhiteSprite;
            }

            progressFillImage.type = Image.Type.Filled;
            progressFillImage.fillMethod = Image.FillMethod.Horizontal;
            progressFillImage.fillOrigin = 0;
        }

        public void Initialize(string displayName, Action clickCallback = null)
        {
            EnsureFillBarConfigured();

            if (skillNameText != null)
                skillNameText.text = displayName;

            onRowClicked = clickCallback;

            if (rowButton != null)
            {
                rowButton.onClick.RemoveAllListeners();
                if (onRowClicked != null)
                {
                    rowButton.interactable = true;
                    rowButton.onClick.AddListener(() => onRowClicked.Invoke());
                }
                else
                {
                    rowButton.interactable = false;
                }
            }
        }

        public void Refresh(SkillProgressEntry entry)
        {
            if (entry == null) return;

            if (levelText != null)
            {
                levelText.text = $"{entry.Level} / {PlayerSkillProgression.MaxSkillLevel}";
            }

            if (progressFillImage != null)
            {
                progressFillImage.fillAmount = entry.NormalizedProgress;
            }
        }
    }
}