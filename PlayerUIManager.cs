using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionRPG.Character.UI
{
    public enum MenuWindowType
    {
        None,
        Inventory,
        Attributes,
        PassiveSkills,
        WeaponSkills
    }

    public class PlayerUIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader inputReader;

        [Header("Window Panels")]
        [SerializeField] private GameObject menuHubRoot;
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private GameObject attributesPanel;
        [SerializeField] private GameObject passiveSkillsPanel;
        [SerializeField] private GameObject weaponSkillsPanel;

        [SerializeField] private MenuWindowType currentOpenWindow = MenuWindowType.None;

        public bool IsAnyWindowOpen => currentOpenWindow != MenuWindowType.None;

        private void Awake()
        {
            if (inputReader == null)
                inputReader = FindFirstObjectByType<PlayerInputReader>();

            CloseAllWindows();
        }

        private void OnEnable()
        {
            if (inputReader == null) return;

            inputReader.OnInventoryTogglePressed += ToggleInventory;
            inputReader.OnAttributesTogglePressed += ToggleAttributes;
            inputReader.OnPassiveSkillsTogglePressed += TogglePassiveSkills;
            inputReader.OnWeaponSkillsTogglePressed += ToggleWeaponSkills;
            inputReader.OnCloseMenuPressed += CloseAllWindows;
        }

        private void OnDisable()
        {
            if (inputReader == null) return;

            inputReader.OnInventoryTogglePressed -= ToggleInventory;
            inputReader.OnAttributesTogglePressed -= ToggleAttributes;
            inputReader.OnPassiveSkillsTogglePressed -= TogglePassiveSkills;
            inputReader.OnWeaponSkillsTogglePressed -= ToggleWeaponSkills;
            inputReader.OnCloseMenuPressed -= CloseAllWindows;
        }

        private void Update()
        {
            if (IsAnyWindowOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseAllWindows();
            }
        }

        public void ToggleInventory() => ToggleWindow(MenuWindowType.Inventory);
        public void ToggleAttributes() => ToggleWindow(MenuWindowType.Attributes);
        public void TogglePassiveSkills() => ToggleWindow(MenuWindowType.PassiveSkills);
        public void ToggleWeaponSkills() => ToggleWindow(MenuWindowType.WeaponSkills);

        public void ToggleWindow(MenuWindowType targetWindow)
        {
            if (currentOpenWindow == targetWindow)
            {
                CloseAllWindows();
            }
            else
            {
                OpenWindow(targetWindow);
            }
        }

        public void OpenWindow(MenuWindowType targetWindow)
        {
            currentOpenWindow = targetWindow;

            if (menuHubRoot != null)
                menuHubRoot.SetActive(targetWindow != MenuWindowType.None);

            if (inventoryPanel != null)
                inventoryPanel.SetActive(targetWindow == MenuWindowType.Inventory);

            if (attributesPanel != null)
                attributesPanel.SetActive(targetWindow == MenuWindowType.Attributes);

            if (passiveSkillsPanel != null)
                passiveSkillsPanel.SetActive(targetWindow == MenuWindowType.PassiveSkills);

            if (weaponSkillsPanel != null)
                weaponSkillsPanel.SetActive(targetWindow == MenuWindowType.WeaponSkills);

            RefreshCursorAndGameplayLock();
        }

        public void CloseAllWindows()
        {
            currentOpenWindow = MenuWindowType.None;

            if (menuHubRoot != null) menuHubRoot.SetActive(false);
            if (inventoryPanel != null) inventoryPanel.SetActive(false);
            if (attributesPanel != null) attributesPanel.SetActive(false);
            if (passiveSkillsPanel != null) passiveSkillsPanel.SetActive(false);
            if (weaponSkillsPanel != null) weaponSkillsPanel.SetActive(false);

            RefreshCursorAndGameplayLock();
        }

        private void RefreshCursorAndGameplayLock()
        {
            bool menuOpen = IsAnyWindowOpen;

            Cursor.lockState = menuOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = menuOpen;

            if (inputReader != null)
            {
                inputReader.SetGameplayInputBlocked(menuOpen);
            }
        }
    }
}