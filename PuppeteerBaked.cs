// ============================================================================
//  PuppeteerBaked.cs
//
//  The complete "Township Puppeteer" (a.k.a. the Puppet menu) baked straight
//  into TavernFun, so you no longer need a second DLL in your Mods folder.
//
//  Everything below was decompiled out of TownshipPuppeteerV2 (made by Back &
//  CallMeQuit) and merged into this single file, alongside your existing
//  TavernFun sources:
//
//    TownshipPuppeteer.UI.MenuPanel        - the UniverseLib menu panel itself
//    TownshipPuppeteer.UI.UIManager        - UniverseLib init + show/hide menu
//    TownshipPuppeteerHeightKeys.HeightKeysMod - [ / ] height keys, R key
//                                                left-hand PanKake grab
//    TownshipPuppeteerV2.Core              - the puppet MelonMod (FOV, fly,
//                                            full bright, yoink, bhop, pankake,
//                                            join/leave, keyboard shortcuts)
//    TownshipPuppeteerV2.LocalPlatformProviderPatch /
//    TownshipPuppeteerV2.PlatformProviderPatches /
//    TownshipPuppeteerV2.StatusPlatformPatch  - platform-spoof Harmony patches
//
//  A few notes on what was NOT a straight copy:
//
//   * PanKakeBehaviour and SourceEngineMovement ("Bhop") are the ORIGINAL
//     decompiled classes - they were missing from the first paste and came in
//     with a second decompile pass. Only two cleanups were needed so they
//     recompile: the C# `dynamic` interactor calls inside
//     PanKakeBehaviour.SafeInteractWithObject (the decompiler shows them as a
//     Microsoft.CSharp binder cache, which cannot be recompiled) are now plain
//     Interactor calls, and the little Cmd input-holder struct in
//     SourceEngineMovement (a field type that was not in the paste) was added
//     back.
//
//   * Core.ConnectServer / Core.LeaveServer were rebuilt the same way (the
//     decompiled async state machines didn't include the original bodies).
//     They join/leave through the game's own VrMainMenu + JoinServer methods,
//     the same approach TavernFun's own server browser uses.
//
//   * CreateSlider is called with `out` (UniverseLib declares it as
//     `out Slider`; the decompiler just showed `ref`).
//
//   * The BaseCanvas in the MenuPanel constructor now falls back to
//     AddComponent<Canvas>() because UniverseLib doesn't put a Canvas on the
//     panel content root (the decompiled code would NRE on modern UniverseLib).
//
//  How to open it:  TavernFun menu -> Anything tab -> "Puppet - quit/mearly".
//  (Tab also opens it while playing, exactly like the original mod did.)
//
//  Building: your project needs a reference to UniverseLib.dll (the same one
//  the puppet mod shipped with) in addition to the game/MelonLoader references
//  you already use.
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using TownshipPuppeteer.UI;
using TownshipPuppeteerV2;
using Alta.Api.Client.HighLevel;
using Alta.PlatformInformation;
using Alta.Utilities;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UniverseLib;
using UniverseLib.Config;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Panels;
using Object = UnityEngine.Object;

namespace TownshipPuppeteer
{
    // Tiny bridge so TavernFun's own IMGUI menu can open/close the Puppet menu.
    // The "Puppet - quit/mearly" button on the Anything tab calls Toggle().
    internal static class PuppetMenuBridge
    {
        public static bool IsOpen
        {
            get
            {
                TownshipPuppeteer.UI.MenuPanel panel = TownshipPuppeteer.UI.UIManager.MenuPanel;
                return panel != null && panel.Enabled;
            }
        }

        public static void Toggle()
        {
            if (TownshipPuppeteer.UI.UIManager.UiBase == null)
            {
                // UniverseLib initialises a few seconds after game start - nothing
                // to toggle until it has.
                MelonLogger.Msg("[Puppet] Menu isn't ready yet, try the button again in a few seconds.");
                return;
            }
            if (TownshipPuppeteer.UI.UIManager.MenuPanel == null)
            {
                TownshipPuppeteer.UI.UIManager.CreateMenu();
            }
            if (IsOpen)
            {
                TownshipPuppeteer.UI.UIManager.HideMenu();
            }
            else
            {
                TownshipPuppeteer.UI.UIManager.ShowMenu();
            }
        }
    }
}

namespace TownshipPuppeteer.UI
{
    public class MenuPanel : PanelBase
    {
        public static MenuPanel Instance { get; private set; }

        public override string Name
        {
            get
            {
                return "Township Puppeteer\nMade by Back & CallMeQuit\n";
            }
        }

        public override int MinWidth
        {
            get
            {
                return 205;
            }
        }

        public override int MinHeight
        {
            get
            {
                return 40;
            }
        }

        public override Vector2 DefaultAnchorMin
        {
            get
            {
                return new Vector2(0.5f, 0.5f);
            }
        }

        public override Vector2 DefaultAnchorMax
        {
            get
            {
                return new Vector2(0.5f, 0.5f);
            }
        }

        public override Vector2 DefaultPosition
        {
            get
            {
                return new Vector2((float)(-(float)this.MinWidth / 2), (float)(Screen.height / 2));
            }
        }

        public override bool CanDragAndResize
        {
            get
            {
                return true;
            }
        }

        public static RectTransform NavBarRect { get; private set; }

        public GameObject NavbarTabButtonHolder { get; private set; }

        public static ButtonRef ToggleAmbience { get; private set; }

        public static ButtonRef SetFOV { get; private set; }

        public static ButtonRef ThirdPerson { get; private set; }

        public static ButtonRef RotateWholeBody { get; private set; }

        public static ButtonRef FBBtn { get; private set; }

        public static ButtonRef FlyBtn { get; private set; }

        public static ButtonRef YoinkBtn { get; private set; }

        public static ButtonRef YoinkBagsBtn { get; private set; }

        public static ButtonRef YoinkCoinBtn { get; private set; }

        public static ButtonRef FreeCBtn { get; private set; }

        public static ButtonRef JoinBtn { get; private set; }

        public static ButtonRef LeaveBtn { get; private set; }

        public static InputFieldRef ServerInput { get; private set; }

        public static ButtonRef SEMBtn { get; private set; }

        public bool Active { get; private set; }

        public MenuPanel(UIBase owner) : base(owner)
        {
            MenuPanel.Instance = this;
            MenuPanel.BaseCanvas = base.ContentRoot.GetComponent<Canvas>();
            if (MenuPanel.BaseCanvas == null)
            {
                // UniverseLib does not put a Canvas on the panel content root, so
                // attach one ourselves - Core.UpdateBaseCanvasPosition re-parents
                // BaseCanvas to the left controller every frame.
                MenuPanel.BaseCanvas = base.ContentRoot.AddComponent<Canvas>();
            }
            MenuPanel.BaseCanvas.renderMode = 0;
            MenuPanel.BaseCanvas.transform.position = new Vector3((float)Screen.width / 2f, (float)Screen.height / 2f, 0f);
            MenuPanel.BaseCanvas.transform.localScale = Vector3.one;
        }

        protected override void ConstructPanelContent()
        {
            MenuPanel.ToggleAmbience = UIFactory.CreateButton(base.ContentRoot, "DESTROY Ambience", "DESTROY Ambience", new Color?(new Color(0.15f, 0.15f, 0.15f, 1f)));
            UIFactory.SetLayoutElement(MenuPanel.ToggleAmbience.GameObject, new int?(200), new int?(25), null, null, null, null, null);
            GameObject gameObject = UIFactory.CreateUIObject("Spacer", base.ContentRoot, default(Vector2));
            UIFactory.SetLayoutElement(gameObject, new int?(200), new int?(25), null, null, null, null, null);
            MenuPanel.ThirdPerson = UIFactory.CreateButton(base.ContentRoot, "Toggle 3rd Person", "Toggle 3rd Person", new Color?(new Color(0.15f, 0.15f, 0.15f, 1f)));
            UIFactory.SetLayoutElement(MenuPanel.ThirdPerson.GameObject, new int?(200), new int?(25), null, null, null, null, null);
            MenuPanel.RotateWholeBody = UIFactory.CreateButton(base.ContentRoot, "Toggle Full Rotate", "Toggle Full Rotate", new Color?(new Color(0.15f, 0.15f, 0.15f, 1f)));
            UIFactory.SetLayoutElement(MenuPanel.RotateWholeBody.GameObject, new int?(200), new int?(25), null, null, null, null, null);
            GameObject gameObject2 = UIFactory.CreateUIObject("Spacer", base.ContentRoot, default(Vector2));
            UIFactory.SetLayoutElement(gameObject2, new int?(200), new int?(25), null, null, null, null, null);
            GameObject gameObject3 = UIFactory.CreateSlider(base.ContentRoot, "FOV Slider", out this.slider);
            UIFactory.SetLayoutElement(gameObject3, new int?(200), new int?(25), null, null, null, null, null);
            this.slider.maxValue = 100f;
            this.slider.minValue = 1f;
            MenuPanel.SetFOV = UIFactory.CreateButton(base.ContentRoot, "Set FOV", "Set FOV", new Color?(new Color(0.15f, 0.15f, 0.15f, 1f)));
            UIFactory.SetLayoutElement(MenuPanel.SetFOV.GameObject, new int?(200), new int?(25), null, null, null, null, null);
            GameObject gameObject4 = UIFactory.CreateUIObject("Spacer", base.ContentRoot, default(Vector2));
            UIFactory.SetLayoutElement(gameObject4, new int?(200), new int?(25), null, null, null, null, null);
            MenuPanel.FBBtn = UIFactory.CreateButton(base.ContentRoot, "Full Bright", "Full Bright", new Color?(new Color(0.15f, 0.15f, 0.15f, 1f)));
            UIFactory.SetLayoutElement(MenuPanel.FBBtn.GameObject, new int?(200), new int?(25), null, null, null, null, null);
            MenuPanel.FlyBtn = UIFactory.CreateButton(base.ContentRoot, "Fly", "Fly", new Color?(new Color(0.15f, 0.15f, 0.15f, 1f)));
            UIFactory.SetLayoutElement(MenuPanel.FlyBtn.GameObject, new int?(200), new int?(25), null, null, null, null, null);
            MenuPanel.YoinkBtn = UIFactory.CreateButton(base.ContentRoot, "Yoink", "Yoink", new Color?(new Color(0.15f, 0.15f, 0.15f, 1f)));
            UIFactory.SetLayoutElement(MenuPanel.YoinkBtn.GameObject, new int?(200), new int?(25), null, null, null, null, null);
            MenuPanel.YoinkBagsBtn = UIFactory.CreateButton(base.ContentRoot, "Yoink Bags", "Yoink Bags", new Color?(new Color(0.15f, 0.15f, 0.15f, 1f)));
            UIFactory.SetLayoutElement(MenuPanel.YoinkBagsBtn.GameObject, new int?(200), new int?(25), null, null, null, null, null);
            MenuPanel.YoinkCoinBtn = UIFactory.CreateButton(base.ContentRoot, "Yoink Coins", "Yoink Coins", new Color?(new Color(0.15f, 0.15f, 0.15f, 1f)));
            UIFactory.SetLayoutElement(MenuPanel.YoinkCoinBtn.GameObject, new int?(200), new int?(25), null, null, null, null, null);
            MenuPanel.FreeCBtn = UIFactory.CreateButton(base.ContentRoot, "PanKake", "PanKake", new Color?(new Color(0.15f, 0.15f, 0.15f, 1f)));
            UIFactory.SetLayoutElement(MenuPanel.FreeCBtn.GameObject, new int?(200), new int?(25), null, null, null, null, null);
            MenuPanel.SEMBtn = UIFactory.CreateButton(base.ContentRoot, "Bhop", "Bhop", new Color?(new Color(0.15f, 0.15f, 0.15f, 1f)));
            UIFactory.SetLayoutElement(MenuPanel.SEMBtn.GameObject, new int?(200), new int?(25), null, null, null, null, null);
            GameObject gameObject5 = UIFactory.CreateUIObject("Spacer", base.ContentRoot, default(Vector2));
            UIFactory.SetLayoutElement(gameObject5, new int?(200), new int?(25), null, null, null, null, null);
            MenuPanel.ServerInput = UIFactory.CreateInputField(base.ContentRoot, "ServerInput", "ServerInput");
            UIFactory.SetLayoutElement(MenuPanel.ServerInput.GameObject, new int?(200), new int?(25), null, null, null, null, null);
            MenuPanel.JoinBtn = UIFactory.CreateButton(base.ContentRoot, "Join", "Join", new Color?(new Color(0.15f, 0.15f, 0.15f, 1f)));
            UIFactory.SetLayoutElement(MenuPanel.JoinBtn.GameObject, new int?(200), new int?(25), null, null, null, null, null);
            MenuPanel.LeaveBtn = UIFactory.CreateButton(base.ContentRoot, "Leave", "Leave", new Color?(new Color(0.15f, 0.15f, 0.15f, 1f)));
            UIFactory.SetLayoutElement(MenuPanel.LeaveBtn.GameObject, new int?(200), new int?(25), null, null, null, null, null);
            ButtonRef toggleAmbience = MenuPanel.ToggleAmbience;
            toggleAmbience.OnClick = (Action)Delegate.Combine(toggleAmbience.OnClick, new Action(delegate()
            {
                try
                {
                    GameObject.Find("Day ambience").SetActive(false);
                    GameObject.Find("Night ambience").SetActive(false);
                }
                catch
                {
                }
            }));
            ButtonRef setFOV = MenuPanel.SetFOV;
            setFOV.OnClick = (Action)Delegate.Combine(setFOV.OnClick, new Action(delegate()
            {
                try
                {
                    bool flag = this.slider.value != PlayerController.Current.Camera.GetComponent<Camera>().fieldOfView;
                    if (flag)
                    {
                        PlayerController.Current.Camera.GetComponent<Camera>().fieldOfView = this.slider.value;
                        TownshipPuppeteerV2.Core.fovSliderValue = this.slider.value;
                    }
                }
                catch
                {
                }
            }));
            ButtonRef thirdPerson = MenuPanel.ThirdPerson;
            thirdPerson.OnClick = (Action)Delegate.Combine(thirdPerson.OnClick, new Action(delegate()
            {
                TownshipPuppeteerV2.Core.activateThirdPerson = true;
            }));
            ButtonRef buttonRef = MenuPanel.RotateWholeBody;
            buttonRef.OnClick = (Action)Delegate.Combine(buttonRef.OnClick, new Action(delegate()
            {
                MenuPanel.rotateWholeBody = !MenuPanel.rotateWholeBody;
            }));
            ButtonRef fbbtn = MenuPanel.FBBtn;
            fbbtn.OnClick = (Action)Delegate.Combine(fbbtn.OnClick, new Action(delegate()
            {
                bool flag = TownshipPuppeteerV2.Core.GhostHandCE != null;
                if (flag)
                {
                    LightHands component = TownshipPuppeteerV2.Core.GhostHandCE.GetComponent<LightHands>();
                    if (component != null)
                    {
                        component.Toggle();
                    }
                }
            }));
            ButtonRef flyBtn = MenuPanel.FlyBtn;
            flyBtn.OnClick = (Action)Delegate.Combine(flyBtn.OnClick, new Action(delegate()
            {
                GameObject smoothLoco = TownshipPuppeteerV2.Core.SmoothLoco;
                if (smoothLoco != null)
                {
                    SmoothLocomotion component = smoothLoco.GetComponent<SmoothLocomotion>();
                    if (component != null)
                    {
                        component.ToggleDebug();
                    }
                }
            }));
            ButtonRef yoinkBtn = MenuPanel.YoinkBtn;
            yoinkBtn.OnClick = (Action)Delegate.Combine(yoinkBtn.OnClick, new Action(delegate()
            {
                TownshipPuppeteerV2.Core.GrabAll();
            }));
            ButtonRef yoinkBagsBtn = MenuPanel.YoinkBagsBtn;
            yoinkBagsBtn.OnClick = (Action)Delegate.Combine(yoinkBagsBtn.OnClick, new Action(delegate()
            {
                TownshipPuppeteerV2.Core.GrabBags();
            }));
            ButtonRef yoinkCoinBtn = MenuPanel.YoinkCoinBtn;
            yoinkCoinBtn.OnClick = (Action)Delegate.Combine(yoinkCoinBtn.OnClick, new Action(delegate()
            {
                TownshipPuppeteerV2.Core.GrabCoins();
            }));
            ButtonRef freeCBtn = MenuPanel.FreeCBtn;
            freeCBtn.OnClick = (Action)Delegate.Combine(freeCBtn.OnClick, new Action(delegate()
            {
                bool flag = MenuPanel.inSEMMode;
                if (flag)
                {
                    TownshipPuppeteerV2.Core.PlayerMessage("You cannot toggle PanKake mode while in Bhop mode. Please disable Bhop mode first.", 2f);
                }
                else
                {
                    MenuPanel.inPanKakeMode = !MenuPanel.inPanKakeMode;
                    bool flag2 = MenuPanel.inPanKakeMode && PlayerController.Current != null;
                    if (flag2)
                    {
                        PanKakeBehaviour panKakeBehaviour = PlayerController.Current.gameObject.GetComponent<PanKakeBehaviour>();
                        bool flag3 = panKakeBehaviour == null;
                        if (flag3)
                        {
                            panKakeBehaviour = PlayerController.Current.gameObject.AddComponent<PanKakeBehaviour>();
                            panKakeBehaviour.Setup();
                        }
                        else
                        {
                            panKakeBehaviour.enabled = true;
                            panKakeBehaviour.Setup();
                        }
                    }
                }
            }));
            ButtonRef joinBtn = MenuPanel.JoinBtn;
            joinBtn.OnClick = (Action)Delegate.Combine(joinBtn.OnClick, new Action(delegate()
            {
                InputFieldRef serverInput = MenuPanel.ServerInput;
                int serverIdentifier = 0;
                bool flag = ((serverInput != null) ? serverInput.Text : null) != null && int.TryParse(MenuPanel.ServerInput.Text, out serverIdentifier);
                if (flag)
                {
                    TownshipPuppeteerV2.Core.ConnectServer(serverIdentifier);
                }
            }));
            ButtonRef leaveBtn = MenuPanel.LeaveBtn;
            leaveBtn.OnClick = (Action)Delegate.Combine(leaveBtn.OnClick, new Action(delegate()
            {
                TownshipPuppeteerV2.Core.LeaveServer();
            }));
            ButtonRef sembtn = MenuPanel.SEMBtn;
            sembtn.OnClick = (Action)Delegate.Combine(sembtn.OnClick, new Action(delegate()
            {
                bool flag = MenuPanel.inPanKakeMode;
                if (flag)
                {
                    TownshipPuppeteerV2.Core.PlayerMessage("You cannot toggle Bhop while in PanKake mode. Please disable PanKake mode first.", 2f);
                }
                else
                {
                    MenuPanel.inSEMMode = !MenuPanel.inSEMMode;
                    bool flag2 = MenuPanel.inSEMMode;
                    if (flag2)
                    {
                        bool flag3 = PlayerController.Current != null;
                        if (flag3)
                        {
                            SourceEngineMovement sourceEngineMovement = PlayerController.Current.gameObject.GetComponent<SourceEngineMovement>();
                            bool flag4 = sourceEngineMovement == null;
                            if (flag4)
                            {
                                sourceEngineMovement = PlayerController.Current.gameObject.AddComponent<SourceEngineMovement>();
                            }
                            bool flag5 = Camera.main != null;
                            if (flag5)
                            {
                                sourceEngineMovement.playerView = PlayerController.Current.gameObject.transform.Find("Height Fixer").transform.Find("VR Head (eye)").transform;
                            }
                            sourceEngineMovement.ToggleMovement(true);
                        }
                    }
                    else
                    {
                        bool flag6 = PlayerController.Current != null;
                        if (flag6)
                        {
                            SourceEngineMovement component = PlayerController.Current.gameObject.GetComponent<SourceEngineMovement>();
                            bool flag7 = component != null;
                            if (flag7)
                            {
                                component.ToggleMovement(false);
                            }
                        }
                    }
                }
            }));
        }

        public static Canvas BaseCanvas;

        public static float sprintSpeed = 1.25f;

        public static bool rotateWholeBody = false;

        public static bool crouching = false;

        public float GlobalSliderValue;

        public static bool isSprinting = false;

        public static bool toggledEnvironment = false;

        public static int holdDelay;

        public Slider slider;

        internal static bool inPanKakeMode;

        internal static float desiredMoveSpeed = 5f;

        internal static Vector3? currentUserPosition;

        internal static Quaternion? currentUserRotation;

        internal static Vector3 previousMousePosition;

        internal static bool inSEMMode = false;
    }

    public class UIManager
    {
        public static UIBase UiBase { get; private set; }

        public static MenuPanel MenuPanel { get; private set; }

        public static int SizeOfProdutcs { get; set; }

        internal static void Initialize()
        {
            UniverseLibConfig universeLibConfig = default(UniverseLibConfig);
            universeLibConfig.Disable_EventSystem_Override = new bool?(false);
            universeLibConfig.Force_Unlock_Mouse = new bool?(false);
            float startupDelay = 3f;
            Action onInitialized = new Action(UIManager.OnInitialized);
            Action<string, LogType> logHandler = new Action<string, LogType>(UIManager.LogHandler);
            Universe.Init(startupDelay, onInitialized, logHandler, universeLibConfig);
        }

        private static void OnInitialized()
        {
            UIManager.UiBase = UniversalUI.RegisterUI("TPUI.Merely", new Action(UIManager.UiUpdate));
            UIManager.CreateMenu();
            UIManager.LogHandler("Menu Created", LogType.Log);
            TownshipPuppeteerV2.Core.Loaded = true;

            // The original attached this extra "Rotate Full Body: ..." toast in
            // Core.OnApplicationStart, but by then the panel doesn't exist yet
            // (UniverseLib initialises a few seconds after game start), so it
            // belonged here instead.
            ButtonRef rotateWholeBody = MenuPanel.RotateWholeBody;
            if (rotateWholeBody != null)
            {
                rotateWholeBody.OnClick = (Action)Delegate.Combine(rotateWholeBody.OnClick, new Action(delegate()
                {
                    TownshipPuppeteerV2.Core.PlayerMessage(string.Format("Rotate Full Body: {0}", MenuPanel.rotateWholeBody), 0.6f);
                }));
            }
        }

        public static void CreateMenu()
        {
            UIManager.MenuPanel = new MenuPanel(UIManager.UiBase);
            // Start hidden - TavernFun's "Puppet - quit/mearly" button (Anything
            // tab) and the Tab key open it. The original mod showed it instantly.
            UIManager.MenuPanel.SetActive(false);
        }

        public static void ShowMenu()
        {
            UIManager.MenuPanel.SetActive(true);
        }

        public static void HideMenu()
        {
            UIManager.MenuPanel.SetActive(false);
        }

        public static void DestroyMenu()
        {
            UIManager.MenuPanel.Destroy();
        }

        public static void UiUpdate()
        {
        }

        public static void LogHandler(string message, LogType type)
        {
            MelonLogger.Msg(string.Format("{0}: {1}", type, message));
        }
    }
}

namespace TownshipPuppeteerHeightKeys
{
    public class HeightKeysMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            this._harmony = new HarmonyLib.Harmony("TownshipPuppeteerHeightKeys.RGrab");
            this.TryResolvePanKakeState();
            this.TryPatchRGrabHooks();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            this._heightSettings = null;
            this._activeLeftHandPickup = null;
            this._leftHandGrabActive = false;
            this._yoinkButtonsHidden = false;
            this._wasPanKakeActive = false;
            this.TryResolvePanKakeState();
            this.TryPatchRGrabHooks();
            this.ApplyCursorState(false);
        }

        public override void OnUpdate()
        {
            this.TryResolvePanKakeState();
            this.TryPatchRGrabHooks();
            this.TryHideYoinkButtons();
            Keyboard current = Keyboard.current;
            bool panKakeEnabled = this.IsPanKakeModeEnabled();
            this.UpdateCursorToggle(current, panKakeEnabled);
            bool flag = current != null && !(PlayerController.Current == null);
            if (flag)
            {
                this.ProcessLeftHandHoldGrab(current, panKakeEnabled);
                bool wasPressedThisFrame = current.rightBracketKey.wasPressedThisFrame;
                if (wasPressedThisFrame)
                {
                    this.AdjustHeight(false);
                }
                bool wasPressedThisFrame2 = current.leftBracketKey.wasPressedThisFrame;
                if (wasPressedThisFrame2)
                {
                    this.AdjustHeight(true);
                }
            }
        }

        private void AdjustHeight(bool increase)
        {
            PlayerAdjustableHeightFixerSettings playerAdjustableHeightFixerSettings = this.ResolveHeightSettings();
            bool flag = playerAdjustableHeightFixerSettings == null;
            if (flag)
            {
                MelonLogger.Warning("Unable to find PlayerAdjustableHeightFixerSettings.");
            }
            else
            {
                bool flag2 = !SettingsManager.GameConfig.Settings.HasAccessibilityFeatures;
                if (flag2)
                {
                    this.ShowPlayerMessage("Enable seated/standing accessibility first.", 1.2f);
                }
                else if (increase)
                {
                    bool flag3 = !playerAdjustableHeightFixerSettings.CanIncreaseHeight;
                    if (flag3)
                    {
                        this.ShowPlayerMessage("Height is already at the maximum.", 0.6f);
                    }
                    else
                    {
                        playerAdjustableHeightFixerSettings.IncreaseHeight();
                        this.ShowPlayerMessage("Raised height.", 0.6f);
                    }
                }
                else
                {
                    bool flag4 = !playerAdjustableHeightFixerSettings.CanDecreaseHeight;
                    if (flag4)
                    {
                        this.ShowPlayerMessage("Height is already at the minimum.", 0.6f);
                    }
                    else
                    {
                        playerAdjustableHeightFixerSettings.DecreaseHeight();
                        this.ShowPlayerMessage("Lowered height.", 0.6f);
                    }
                }
            }
        }

        private PlayerAdjustableHeightFixerSettings ResolveHeightSettings()
        {
            bool flag = this._heightSettings != null;
            PlayerAdjustableHeightFixerSettings result;
            if (flag)
            {
                result = this._heightSettings;
            }
            else
            {
                PlayerAdjustableHeightFixerSettings[] array = Resources.FindObjectsOfTypeAll<PlayerAdjustableHeightFixerSettings>();
                bool flag2 = array == null || array.Length == 0;
                if (flag2)
                {
                    result = null;
                }
                else
                {
                    PlayerAdjustableHeightFixerSettings[] array2 = array;
                    foreach (PlayerAdjustableHeightFixerSettings playerAdjustableHeightFixerSettings in array2)
                    {
                        bool flag3 = playerAdjustableHeightFixerSettings != null;
                        if (flag3)
                        {
                            this._heightSettings = playerAdjustableHeightFixerSettings;
                            break;
                        }
                    }
                    result = this._heightSettings;
                }
            }
            return result;
        }

        private void TryPatchRGrabHooks()
        {
            bool rGrabPatchApplied = this._rGrabPatchApplied;
            if (!rGrabPatchApplied)
            {
                Type type = AccessTools.TypeByName("PuppeteerUiRevamp");
                bool flag = !(type == null);
                if (flag)
                {
                    MethodInfo methodInfo = AccessTools.Method(type, "CleanDestroyLookedAtObject", null, null);
                    HeightKeysMod._tryGetLookedAtInteractableMethod = AccessTools.Method(type, "TryGetLookedAtInteractable", null, null);
                    HeightKeysMod._startLeftHandInteractionMethod = AccessTools.Method(type, "StartLeftHandInteraction", null, null);
                    HeightKeysMod._forceDropLeftHandPickupMethod = AccessTools.Method(type, "ForceDropLeftHandPickup", null, null);
                    bool flag2 = !(methodInfo == null) && !(HeightKeysMod._tryGetLookedAtInteractableMethod == null) && !(HeightKeysMod._startLeftHandInteractionMethod == null) && !(HeightKeysMod._forceDropLeftHandPickupMethod == null);
                    if (flag2)
                    {
                        this._harmony.Patch(methodInfo, new HarmonyMethod(typeof(HeightKeysMod), "CleanDestroyPrefix", null), null, null, null, null);
                        this._rGrabPatchApplied = true;
                        MelonLogger.Msg("Enabled R key left-hand PanKake grab.");
                    }
                }
            }
        }

        private void TryHideYoinkButtons()
        {
            bool flag = !this._yoinkButtonsHidden;
            if (flag)
            {
                // The menu now lives inside the TavernFun assembly, so resolve it
                // directly instead of by the old "Type, TownshipPuppeteerV2" string.
                Type type = typeof(TownshipPuppeteer.UI.MenuPanel);
                bool flag2 = !(type == null) && (0 | (this.TryHideMenuButton(type, "YoinkBtn") ? 1 : 0) | (this.TryHideMenuButton(type, "YoinkBagsBtn") ? 1 : 0) | (this.TryHideMenuButton(type, "YoinkCoinBtn") ? 1 : 0)) != 0;
                if (flag2)
                {
                    this._yoinkButtonsHidden = true;
                    MelonLogger.Msg("Removed Yoink buttons from the menu.");
                }
            }
        }

        private bool TryHideMenuButton(Type menuPanelType, string propertyName)
        {
            PropertyInfo property = menuPanelType.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            object obj = (property != null) ? property.GetValue(null, null) : null;
            bool flag = obj == null;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                PropertyInfo property2 = obj.GetType().GetProperty("GameObject", BindingFlags.Instance | BindingFlags.Public);
                GameObject gameObject = ((property2 != null) ? property2.GetValue(obj, null) : null) as GameObject;
                bool flag2 = gameObject == null;
                if (flag2)
                {
                    result = false;
                }
                else
                {
                    gameObject.SetActive(false);
                    result = true;
                }
            }
            return result;
        }

        private void TryResolvePanKakeState()
        {
            bool flag = !(HeightKeysMod._inPanKakeModeField != null);
            if (flag)
            {
                // Same-assembly lookup now (the old assembly-qualified string
                // would never resolve inside TavernFun's own DLL).
                HeightKeysMod._inPanKakeModeField = typeof(TownshipPuppeteer.UI.MenuPanel).GetField("inPanKakeMode", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }

        private bool IsPanKakeModeEnabled()
        {
            bool result;
            try
            {
                bool flag = HeightKeysMod._inPanKakeModeField == null;
                if (flag)
                {
                    result = false;
                }
                else
                {
                    object value = HeightKeysMod._inPanKakeModeField.GetValue(null);
                    bool flag2 = false;
                    bool flag3 = value is bool;
                    int num;
                    if (flag3)
                    {
                        flag2 = (bool)value;
                        num = 1;
                    }
                    else
                    {
                        num = 0;
                    }
                    result = ((byte)(num & (flag2 ? 1 : 0)) > 0);
                }
            }
            catch
            {
                result = false;
            }
            return result;
        }

        private void UpdateCursorToggle(Keyboard keyboard, bool panKakeEnabled)
        {
            bool flag = panKakeEnabled && !this._wasPanKakeActive;
            if (flag)
            {
                this.ApplyCursorState(true);
            }
            else
            {
                bool flag2 = !panKakeEnabled && this._wasPanKakeActive;
                if (flag2)
                {
                    this.ApplyCursorState(false);
                }
            }
            this.ApplyCursorState(panKakeEnabled);
            this._wasPanKakeActive = panKakeEnabled;
        }

        private void ApplyCursorState(bool locked)
        {
            Cursor.visible = !locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        }

        private void ProcessLeftHandHoldGrab(Keyboard keyboard, bool panKakeEnabled)
        {
            bool flag = keyboard == null || !this._rGrabPatchApplied;
            if (!flag)
            {
                bool flag2 = !panKakeEnabled;
                if (flag2)
                {
                    bool leftHandGrabActive = this._leftHandGrabActive;
                    if (leftHandGrabActive)
                    {
                        this.TryStopLeftHandGrab();
                    }
                }
                else
                {
                    bool wasPressedThisFrame = keyboard.rKey.wasPressedThisFrame;
                    if (wasPressedThisFrame)
                    {
                        this.TryStartLeftHandGrab();
                    }
                    bool flag3 = this._leftHandGrabActive && !keyboard.rKey.isPressed;
                    if (flag3)
                    {
                        this.TryStopLeftHandGrab();
                    }
                }
            }
        }

        private void TryStartLeftHandGrab()
        {
            try
            {
                bool flag = !this._leftHandGrabActive;
                if (flag)
                {
                    object[] array = new object[3];
                    MethodInfo tryGetLookedAtInteractableMethod = HeightKeysMod._tryGetLookedAtInteractableMethod;
                    object obj = (tryGetLookedAtInteractableMethod != null) ? tryGetLookedAtInteractableMethod.Invoke(null, array) : null;
                    Component component = array[2] as Component;
                    bool flag2 = obj is bool && (bool)obj && array[0] != null && array[1] != null && !(component == null);
                    if (flag2)
                    {
                        MethodInfo startLeftHandInteractionMethod = HeightKeysMod._startLeftHandInteractionMethod;
                        if (startLeftHandInteractionMethod != null)
                        {
                            startLeftHandInteractionMethod.Invoke(null, new object[]
                            {
                                array[0],
                                array[1],
                                component
                            });
                        }
                        this._activeLeftHandPickup = component;
                        this._leftHandGrabActive = true;
                    }
                }
            }
            catch (Exception arg)
            {
                MelonLogger.Warning(string.Format("Left-hand R grab failed to start: {0}", arg));
            }
        }

        private void TryStopLeftHandGrab()
        {
            try
            {
                object obj;
                bool flag = this.TryGetHandInteractor(true, out obj);
                if (flag)
                {
                    MethodInfo methodInfo = (this._activeLeftHandPickup != null) ? this._activeLeftHandPickup.GetType().GetMethod("Undock", BindingFlags.Instance | BindingFlags.Public) : null;
                    bool flag2 = methodInfo != null;
                    if (flag2)
                    {
                        methodInfo.Invoke(this._activeLeftHandPickup, new object[]
                        {
                            obj,
                            true,
                            1
                        });
                    }
                    MethodInfo method = obj.GetType().GetMethod("StopInteract", BindingFlags.Instance | BindingFlags.Public);
                    bool flag3 = method != null;
                    if (flag3)
                    {
                        method.Invoke(obj, new object[]
                        {
                            true,
                            true
                        });
                    }
                }
                else
                {
                    MethodInfo forceDropLeftHandPickupMethod = HeightKeysMod._forceDropLeftHandPickupMethod;
                    if (forceDropLeftHandPickupMethod != null)
                    {
                        forceDropLeftHandPickupMethod.Invoke(null, null);
                    }
                }
            }
            catch (Exception arg)
            {
                MelonLogger.Warning(string.Format("Left-hand R grab failed to stop: {0}", arg));
            }
            this._activeLeftHandPickup = null;
            this._leftHandGrabActive = false;
        }

        private static bool CleanDestroyPrefix()
        {
            return false;
        }

        private bool TryGetHandInteractor(bool isLeftHand, out object handInteractor)
        {
            handInteractor = null;
            Type type = AccessTools.TypeByName("PlayerController");
            bool flag = type == null;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                PropertyInfo property = type.GetProperty("Current", BindingFlags.Static | BindingFlags.Public);
                object obj = (property != null) ? property.GetValue(null, null) : null;
                bool flag2 = obj == null;
                if (flag2)
                {
                    result = false;
                }
                else
                {
                    PropertyInfo property2 = type.GetProperty(isLeftHand ? "LeftController" : "RightController", BindingFlags.Instance | BindingFlags.Public);
                    object obj2 = (property2 != null) ? property2.GetValue(obj, null) : null;
                    bool flag3 = obj2 == null;
                    if (flag3)
                    {
                        result = false;
                    }
                    else
                    {
                        PropertyInfo property3 = obj2.GetType().GetProperty("Interactor", BindingFlags.Instance | BindingFlags.Public);
                        handInteractor = ((property3 != null) ? property3.GetValue(obj2, null) : null);
                        result = (handInteractor != null);
                    }
                }
            }
            return result;
        }

        private void ShowPlayerMessage(string text, float duration)
        {
            try
            {
                PlayerController playerController = PlayerController.Current;
                PlayerMessageDisplay playerMessageDisplay;
                if (playerController == null)
                {
                    playerMessageDisplay = null;
                }
                else
                {
                    Transform transform = playerController.transform;
                    playerMessageDisplay = ((transform != null) ? transform.GetComponent<PlayerMessageDisplay>() : null);
                }
                PlayerMessageDisplay playerMessageDisplay2 = playerMessageDisplay;
                bool flag = playerMessageDisplay2 != null;
                if (flag)
                {
                    playerMessageDisplay2.Display(text, duration, (DisplayMessageType)2);
                }
            }
            catch
            {
            }
        }

        private static FieldInfo _inPanKakeModeField;

        private static MethodInfo _tryGetLookedAtInteractableMethod;

        private static MethodInfo _startLeftHandInteractionMethod;

        private static MethodInfo _forceDropLeftHandPickupMethod;

        private PlayerAdjustableHeightFixerSettings _heightSettings;

        private HarmonyLib.Harmony _harmony;

        private Component _activeLeftHandPickup;

        private bool _leftHandGrabActive;

        private bool _rGrabPatchApplied;

        private bool _yoinkButtonsHidden;

        private bool _wasPanKakeActive;
    }
}

namespace TownshipPuppeteerV2
{
    public class Core : MelonMod
    {
        public override void OnInitializeMelon()
        {
            bool flag = !Core.initialized;
            if (flag)
            {
                TownshipPuppeteer.UI.UIManager.Initialize();
                TownshipPuppeteer.UI.UIManager.LogHandler("UI Initialized", LogType.Log);
                Core.initialized = true;
                Core.Loaded = true;
                // The extra "Rotate Full Body: ..." toast used to be attached here,
                // but the panel doesn't exist yet at this point (UniverseLib comes
                // up a few seconds later) - UIManager.OnInitialized attaches it now.
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            base.OnSceneWasLoaded(buildIndex, sceneName);
            Core.CheckSmoothLoco();
        }

        public override void OnUpdate()
        {
            bool flag = !Core.Loaded;
            if (!flag)
            {
                bool flag2 = TownshipPuppeteer.UI.MenuPanel.Instance != null;
                if (flag2)
                {
                    this.HandleKeyboardInput();
                    this.UpdateBaseCanvasPosition();
                    this.EnsurePanKakeBehaviour();
                }
                bool flag3 = Core.activateThirdPerson;
                if (flag3)
                {
                    Core.activateThirdPerson = false;
                    this.OnThirdPerson();
                }
                bool flag4 = Core.fovSliderValue != Core.oldFovSliderValue;
                if (flag4)
                {
                    Core.PlayerMessage(string.Format("FOV: {0}", Core.fovSliderValue), 0.6f);
                }
                Core.oldFovSliderValue = Core.fovSliderValue;
                this.UpdateGhostHandAndSmoothLoco();
                bool flag5 = Gamepad.current == null || !Gamepad.current.aButton.wasPressedThisFrame || Gamepad.current.rightTrigger.isPressed;
                if (!flag5)
                {
                    TownshipPuppeteer.UI.MenuPanel.inPanKakeMode = !TownshipPuppeteer.UI.MenuPanel.inPanKakeMode;
                    bool flag6 = TownshipPuppeteer.UI.MenuPanel.inPanKakeMode && PlayerController.Current != null;
                    if (flag6)
                    {
                        PanKakeBehaviour panKakeBehaviour = PlayerController.Current.gameObject.GetComponent<PanKakeBehaviour>();
                        bool flag7 = panKakeBehaviour == null;
                        if (flag7)
                        {
                            panKakeBehaviour = PlayerController.Current.gameObject.AddComponent<PanKakeBehaviour>();
                        }
                        panKakeBehaviour.Setup();
                    }
                }
            }
        }

        private static void CheckSmoothLoco()
        {
            Core.SmoothLocoCheck = true;
            SmoothLocomotion[] array = Object.FindObjectsOfType<SmoothLocomotion>();
            foreach (SmoothLocomotion smoothLocomotion in array)
            {
                bool flag = smoothLocomotion.transform.parent != null;
                if (flag)
                {
                    smoothLocomotion.gameObject.name = "Skibidi Sigma";
                }
            }
        }

        private static PlayerMessageDisplay ReadCMQMenu()
        {
            PlayerMessageDisplay result;
            try
            {
                result = PlayerController.Current.transform.GetComponent<PlayerMessageDisplay>();
            }
            catch
            {
                result = null;
            }
            return result;
        }

        public static void PlayerMessage(string text, float duration)
        {
            bool flag = Core.ReadCMQMenu() != null;
            if (flag)
            {
                Core.CheckSmoothLoco();
                Core.ReadCMQMenu().Display(text, duration, (DisplayMessageType)2);
            }
        }

        private void HandleKeyboardInput()
        {
            bool wasReleasedThisFrame = Keyboard.current.tabKey.wasReleasedThisFrame;
            if (wasReleasedThisFrame)
            {
                TownshipPuppeteer.UI.UIManager.ShowMenu();
                TownshipPuppeteer.UI.UIManager.LogHandler("Menu Shown", LogType.Log);
            }
            bool wasReleasedThisFrame2 = Keyboard.current.fKey.wasReleasedThisFrame;
            if (wasReleasedThisFrame2)
            {
                GameObject smoothLoco = Core.SmoothLoco;
                if (smoothLoco != null)
                {
                    SmoothLocomotion component = smoothLoco.GetComponent<SmoothLocomotion>();
                    if (component != null)
                    {
                        component.ToggleDebug();
                    }
                }
            }
            bool flag = Mouse.current.scroll.y.ReadValue() > 0f && Keyboard.current.shiftKey.isPressed;
            if (flag)
            {
                TownshipPuppeteer.UI.MenuPanel.sprintSpeed += 0.5f;
                Core.PlayerMessage(string.Format("Sprint Speed: {0}", TownshipPuppeteer.UI.MenuPanel.sprintSpeed), 0.3f);
            }
            bool flag2 = Mouse.current.scroll.y.ReadValue() < 0f && TownshipPuppeteer.UI.MenuPanel.sprintSpeed > 1.5f && Keyboard.current.shiftKey.isPressed;
            if (flag2)
            {
                TownshipPuppeteer.UI.MenuPanel.sprintSpeed -= 0.5f;
                Core.PlayerMessage(string.Format("Sprint Speed: {0}", TownshipPuppeteer.UI.MenuPanel.sprintSpeed), 0.3f);
            }
            bool flag3 = Mouse.current.middleButton.wasReleasedThisFrame && Keyboard.current.shiftKey.isPressed;
            if (flag3)
            {
                TownshipPuppeteer.UI.MenuPanel.sprintSpeed = 1.25f;
                Core.PlayerMessage(string.Format("Sprint Speed: {0}", TownshipPuppeteer.UI.MenuPanel.sprintSpeed), 0.3f);
            }
            bool wasReleasedThisFrame3 = Keyboard.current.f3Key.wasReleasedThisFrame;
            if (wasReleasedThisFrame3)
            {
                this.OnThirdPerson();
            }
        }

        public void UpdateBaseCanvasPosition()
        {
            bool flag = TownshipPuppeteer.UI.MenuPanel.BaseCanvas != null && PlayerController.Current != null;
            if (flag)
            {
                bool flag2 = TownshipPuppeteer.UI.MenuPanel.BaseCanvas.transform.parent != PlayerController.Current.LeftController.transform;
                if (flag2)
                {
                    TownshipPuppeteer.UI.MenuPanel.BaseCanvas.transform.SetParent(PlayerController.Current.LeftController.transform);
                }
                TownshipPuppeteer.UI.MenuPanel.BaseCanvas.transform.position = PlayerController.Current.LeftController.transform.position + new Vector3(0f, 0.0524f, -0.088f);
                TownshipPuppeteer.UI.MenuPanel.BaseCanvas.transform.localRotation = Quaternion.Euler(0f, 270f, 270f);
            }
        }

        public void OnThirdPerson()
        {
            bool flag = !Core.thirdPerson;
            if (flag)
            {
                Core.thirdPerson = !Core.thirdPerson;
                Transform transform = new GameObject("3rdPerson").transform;
                Transform transform2 = PlayerController.Current.Camera.transform;
                transform.parent = transform2;
                transform.localPosition = new Vector3(0f, -0.3891f, -2.3635f);
                transform.localEulerAngles = Vector3.zero;
                Camera camera = transform.gameObject.AddComponent<Camera>();
                camera.focalLength = 89f;
                camera.fieldOfView = 90f;
                camera.depth = 100f;
            }
            else
            {
                Core.thirdPerson = !Core.thirdPerson;
                Object.Destroy(GameObject.Find("3rdPerson"));
            }
        }

        private void EnsurePanKakeBehaviour()
        {
            bool flag = PlayerController.Current != null && PlayerController.Current.gameObject.GetComponent<PanKakeBehaviour>() == null;
            if (flag)
            {
                PlayerController.Current.gameObject.AddComponent<PanKakeBehaviour>();
                bool inPanKakeMode = TownshipPuppeteer.UI.MenuPanel.inPanKakeMode;
                if (inPanKakeMode)
                {
                    PlayerController.Current.gameObject.GetComponent<PanKakeBehaviour>().Setup();
                }
            }
        }

        private void EnsureBhopBehaviour()
        {
            bool flag = PlayerController.Current != null && PlayerController.Current.gameObject.GetComponent<SourceEngineMovement>() == null;
            if (flag)
            {
                PlayerController.Current.gameObject.AddComponent<SourceEngineMovement>();
                bool inSEMMode = TownshipPuppeteer.UI.MenuPanel.inSEMMode;
                if (inSEMMode)
                {
                    PlayerController.Current.gameObject.GetComponent<SourceEngineMovement>().ToggleMovement(true);
                }
            }
        }

        private void UpdateGhostHandAndSmoothLoco()
        {
            bool flag = SceneManager.GetActiveScene().name == "Overworld Chunked";
            if (flag)
            {
                bool flag2 = Core.GhostHandCE == null;
                if (flag2)
                {
                    Core.GhostHandCE = GameObject.Find("Owned Controller Extensions(Clone) (Ghost Hand)");
                }
                bool flag3 = Core.GhostHandCE != null;
                if (flag3)
                {
                    Core.GhostHandCE.GetComponent<LightHands>().enabled = true;
                }
            }
            bool flag4 = Core.SmoothLoco == null;
            if (flag4)
            {
                Core.SmoothLoco = GameObject.Find("Smooth Locomotion");
            }
        }

        public static void GrabAll()
        {
            GameObject[] array = Object.FindObjectsOfType<GameObject>();
            PlayerController playerController = PlayerController.Current;
            Controller controller = (playerController != null) ? playerController.RightController : null;
            Interactor interactor = (controller != null) ? controller.Interactor : null;
            GameObject[] array2 = array;
            foreach (GameObject obj in array2)
            {
                bool flag = Core.ShouldPickupObject(obj);
                if (flag)
                {
                    Core.PickupObject(obj);
                }
            }
            GameObject[] array4 = array;
            foreach (GameObject gameObject in array4)
            {
                Interactable component = gameObject.GetComponent<Interactable>();
                bool flag2 = component != null;
                if (flag2)
                {
                    if (interactor != null)
                    {
                        interactor.ResetTimeout();
                    }
                    if (interactor != null)
                    {
                        interactor.StartInteract(component, false, false, false);
                    }
                    if (interactor != null)
                    {
                        interactor.StopInteract(false, false);
                    }
                }
            }
        }

        public static bool ShouldPickupObject(GameObject obj)
        {
            string name = obj.name;
            return name.EndsWith("Bag(Clone)") || (name.Contains("Handle") && name.EndsWith("(Clone)")) || (name.Contains("Fabric") && name.EndsWith("(Clone)")) || (name.Contains("Coin") && name.EndsWith("(Clone)")) || (name.Contains("Ingot") && name.EndsWith("(Clone)")) || (name.Contains("Pouch") && name.EndsWith("(Clone)")) || (name.Contains("Cooked") && name.EndsWith("(Clone)"));
        }

        public static void PickupObject(GameObject obj)
        {
            Pickup component = obj.GetComponent<Pickup>();
            if (component != null)
            {
                component.Undock(null, true, 1);
            }
        }

        public static void GrabBags()
        {
            Core.GrabObjectsByName("Bag", "SussyBaka");
        }

        public static void GrabCoins()
        {
            Core.GrabObjectsByName("Coin", "SusssssyBaka");
        }

        private static void GrabObjectsByName(string objName, string tempName)
        {
            GameObject[] array = Object.FindObjectsOfType<GameObject>();
            PlayerController playerController = PlayerController.Current;
            Controller controller = (playerController != null) ? playerController.RightController : null;
            Interactor interactor = (controller != null) ? controller.Interactor : null;
            GameObject[] array2 = array;
            foreach (GameObject gameObject in array2)
            {
                bool flag = gameObject.name.EndsWith(objName);
                if (flag)
                {
                    Core.PickupObject(gameObject);
                }
            }
            GameObject[] array4 = array;
            foreach (GameObject gameObject2 in array4)
            {
                Interactable component = gameObject2.GetComponent<Interactable>();
                bool flag2 = component != null && gameObject2.name.Contains(objName);
                if (flag2)
                {
                    gameObject2.name = tempName;
                    if (interactor != null)
                    {
                        interactor.ResetTimeout();
                    }
                    if (interactor != null)
                    {
                        interactor.StartInteract(component, false, false, false);
                    }
                    if (interactor != null)
                    {
                        interactor.StopInteract(false, false);
                    }
                }
            }
            GameObject[] array6 = array;
            foreach (GameObject gameObject3 in array6)
            {
                bool flag3 = gameObject3.name.EndsWith(tempName);
                if (flag3)
                {
                    Core.PickupObject(gameObject3);
                }
            }
            GameObject[] array8 = array;
            foreach (GameObject gameObject4 in array8)
            {
                Interactable component2 = gameObject4.GetComponent<Interactable>();
                bool flag4 = component2 != null && gameObject4.name.Contains(tempName);
                if (flag4)
                {
                    gameObject4.name = objName;
                    if (interactor != null)
                    {
                        interactor.ResetTimeout();
                    }
                    if (interactor != null)
                    {
                        interactor.StartInteract(component2, false, false, false);
                    }
                    if (interactor != null)
                    {
                        interactor.StopInteract(false, false);
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        //  Join / Leave - RECONSTRUCTED.
        //
        //  The decompiled Core.<ConnectServer>d__33 / <LeaveServer>d__34 state
        //  machines didn't include the original bodies, so these were rebuilt
        //  around the game's own multiplayer objects (VrMainMenu + JoinServer),
        //  the same way TavernFun's built-in server browser does it.
        // ------------------------------------------------------------------

        [DebuggerStepThrough]
        public static Task ConnectServer(int serverIdentifier)
        {
            try
            {
                object host = Core.FindServerHost();
                if (host == null)
                {
                    Core.PlayerMessage("Puppet: no server list available - open the game's multiplayer menu first.", 2f);
                    return Task.FromResult(true);
                }
                IList servers = Core.GetServerList(host);
                if (servers == null || servers.Count == 0)
                {
                    Core.PlayerMessage("Puppet: server list is empty.", 2f);
                    return Task.FromResult(true);
                }
                object server = Core.PickServer(servers, serverIdentifier);
                if (server == null)
                {
                    Core.PlayerMessage(string.Format("Puppet: no server #{0} in the list ({1} available).", serverIdentifier, servers.Count), 2f);
                    return Task.FromResult(true);
                }
                MethodInfo join = Core.FindInvokeMethod(host, new string[] { "JoinServer", "Join", "Connect" }, 1);
                if (join == null)
                {
                    Core.PlayerMessage("Puppet: could not find a JoinServer method on " + host.GetType().Name, 2f);
                    return Task.FromResult(true);
                }
                join.Invoke(host, new object[] { server });
                Core.PlayerMessage("Puppet: joining server #" + serverIdentifier + "...", 2f);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[Puppet] ConnectServer failed: " + ex.Message);
            }
            return Task.FromResult(true);
        }

        [DebuggerStepThrough]
        public static void LeaveServer()
        {
            try
            {
                object host = Core.FindServerHost();
                if (host != null && Core.TryInvokeFirstNoArg(host, new string[] { "LeaveServer", "Leave", "Disconnect", "LeaveGame" }))
                {
                    Core.PlayerMessage("Puppet: leaving server...", 1.5f);
                    return;
                }
                MonoBehaviour[] all = Object.FindObjectsOfType<MonoBehaviour>();
                foreach (MonoBehaviour monoBehaviour in all)
                {
                    if (monoBehaviour == null)
                    {
                        continue;
                    }
                    string typeName = monoBehaviour.GetType().Name;
                    if (typeName.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) < 0 &&
                        typeName.IndexOf("Connection", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                    if (Core.TryInvokeFirstNoArg(monoBehaviour, new string[] { "LeaveServer", "Disconnect" }))
                    {
                        Core.PlayerMessage("Puppet: leaving server...", 1.5f);
                        return;
                    }
                }
                // Last resort: ask the game mode manager to stop (the same call
                // TavernFun's "Quit Game" flow falls back to).
                Type gameModeManagerType = Core.FindType("GameModeManager");
                if (gameModeManagerType != null)
                {
                    MethodInfo stopMethod = gameModeManagerType.GetMethod("StopCurrentModeAsync", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (stopMethod != null)
                    {
                        stopMethod.Invoke(null, new object[] { "return to menu", true });
                        return;
                    }
                }
                Core.PlayerMessage("Puppet: could not find a way to leave the server.", 2f);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[Puppet] LeaveServer failed: " + ex.Message);
            }
        }

        private static Type FindType(string typeName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                Type type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }

        private static object FindServerHost()
        {
            string[] hostNames = new string[] { "VrMainMenu", "MainMenu", "ServerSelectionMenu", "MultiplayerMenu" };
            MonoBehaviour[] all = Object.FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour monoBehaviour in all)
            {
                if (monoBehaviour == null)
                {
                    continue;
                }
                string typeName = monoBehaviour.GetType().Name;
                for (int i = 0; i < hostNames.Length; i++)
                {
                    if (typeName == hostNames[i])
                    {
                        return monoBehaviour;
                    }
                }
            }
            return null;
        }

        private static IList GetServerList(object host)
        {
            string[] listNames = new string[] { "lastReceivedServers", "lastFilteredServers", "servers", "cachedServers", "serverList" };
            for (int i = 0; i < listNames.Length; i++)
            {
                object value = Core.ReadMember(host, listNames[i]);
                if (value is IList)
                {
                    return (IList)value;
                }
            }
            return null;
        }

        private static object PickServer(IList servers, int serverIdentifier)
        {
            // First try to match a numeric id on the server object itself.
            for (int i = 0; i < servers.Count; i++)
            {
                object server = servers[i];
                if (server == null)
                {
                    continue;
                }
                string[] idNames = new string[] { "Id", "ID", "ServerId", "IdValue" };
                for (int j = 0; j < idNames.Length; j++)
                {
                    object idValue = Core.ReadMember(server, idNames[j]);
                    if (idValue is int && (int)idValue == serverIdentifier)
                    {
                        return server;
                    }
                    if (idValue is long && (long)idValue == serverIdentifier)
                    {
                        return server;
                    }
                }
            }
            // Otherwise treat the input as a position in the list (1-based like
            // the in-game browser, with a 0-based fallback).
            int index = serverIdentifier - 1;
            if (index >= 0 && index < servers.Count)
            {
                return servers[index];
            }
            if (serverIdentifier >= 0 && serverIdentifier < servers.Count)
            {
                return servers[serverIdentifier];
            }
            return null;
        }

        private static object ReadMember(object instance, string name)
        {
            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                try
                {
                    return property.GetValue(instance, null);
                }
                catch
                {
                    return null;
                }
            }
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                try
                {
                    return field.GetValue(instance);
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        private static MethodInfo FindInvokeMethod(object host, string[] names, int paramCount)
        {
            MethodInfo[] methods = host.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < names.Length; i++)
            {
                foreach (MethodInfo method in methods)
                {
                    if (method.Name == names[i] && method.GetParameters().Length == paramCount)
                    {
                        return method;
                    }
                }
            }
            return null;
        }

        private static bool TryInvokeFirstNoArg(object target, string[] names)
        {
            MethodInfo[] methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < names.Length; i++)
            {
                foreach (MethodInfo method in methods)
                {
                    if (method.Name == names[i] && method.GetParameters().Length == 0)
                    {
                        try
                        {
                            method.Invoke(target, null);
                            return true;
                        }
                        catch
                        {
                        }
                    }
                }
            }
            return false;
        }

        private static bool initialized = false;

        public static GameObject GhostHandCE;

        public static GameObject SmoothLoco;

        public static bool FBEnabled;

        public static bool Loaded = false;

        public static bool loadedFOV = false;

        public static bool loadedMessages = false;

        public static bool thirdPerson = false;

        public static bool activateThirdPerson = false;

        public static bool canDisplayFov = false;

        public static bool canSaveFOV = false;

        public static float oldFovSliderValue;

        public static float fovSliderValue;

        public static bool SmoothLocoCheck = true;
    }

    // ----------------------------------------------------------------------
    //  PanKakeBehaviour - the ORIGINAL decompiled class (second decompile
    //  pass; it was missing from the first one). Cleaned up so it recompiles:
    //  the C# `dynamic` interactor calls inside SafeInteractWithObject (the
    //  decompiler shows them as a Microsoft.CSharp.RuntimeBinder CallSite
    //  cache, which cannot be recompiled) are now plain Interactor calls.
    // ----------------------------------------------------------------------
    internal class PanKakeBehaviour : MonoBehaviour
    {
        private const float MIN_FOV = 10f;

        private const float MAX_FOV = 170f;

        private const float MIN_SPRINT_SPEED = 0.1f;

        private const float MAX_SPRINT_SPEED = 10f;

        private const float MOUSE_SENSITIVITY = 0.3f;

        private const float RAYCAST_DISTANCE = 100f;

        private bool _lastFrameValid = false;

        internal void Setup()
        {
            try
            {
                if (this.ValidatePlayerController("Setup", true))
                {
                    PlayerController playerController = PlayerController.Current;
                    this.SetCameraPosition(playerController);
                    this.SetControllerPositions(playerController);
                    UIManager.LogHandler("PanKake setup completed successfully.", LogType.Log);
                }
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error in Setup: " + ex.Message, LogType.Error);
            }
        }

        internal void Update()
        {
            try
            {
                this.UpdateHoldDelay();
                if (!this.IsValidForUpdate())
                {
                    this._lastFrameValid = false;
                }
                else
                {
                    this._lastFrameValid = true;
                    this.ProcessInput();
                }
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error in Update: " + ex.Message, LogType.Error);
                if (this._lastFrameValid)
                {
                    UIManager.LogHandler("PanKake movement temporarily disabled due to error.", LogType.Warning);
                }
                this._lastFrameValid = false;
            }
        }

        private void UpdateHoldDelay()
        {
            MenuPanel.holdDelay++;
            if (MenuPanel.holdDelay > 14)
            {
                MenuPanel.holdDelay = 0;
            }
        }

        private bool IsValidForUpdate()
        {
            return Core.Loaded && MenuPanel.inPanKakeMode && this.ValidatePlayerController("Update", false);
        }

        private void ProcessInput()
        {
            if (Gamepad.current != null)
            {
                this.HandleGamepadInput();
            }
            if (Keyboard.current != null || Mouse.current != null)
            {
                this.HandleKeyboardInput();
            }
        }

        private bool ValidatePlayerController(string context, bool logErrors = true)
        {
            if (PlayerController.Current == null)
            {
                if (logErrors)
                {
                    UIManager.LogHandler("PlayerController.Current is null in " + context + ".", LogType.Error);
                }
                return false;
            }
            return true;
        }

        private void SetCameraPosition(PlayerController player)
        {
            try
            {
                Camera camera = player.Camera;
                Transform transform = (camera != null) ? camera.transform : null;
                if (transform != null)
                {
                    transform.localPosition = new Vector3(0f, 1.45f, 0f);
                    UIManager.LogHandler("Camera position set.", LogType.Log);
                }
                else
                {
                    UIManager.LogHandler("Camera transform is null.", LogType.Warning);
                }
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error setting camera position: " + ex.Message, LogType.Error);
            }
        }

        private void SetControllerPositions(PlayerController player)
        {
            try
            {
                Controller rightController = player.RightController;
                this.SetControllerPosition((rightController != null) ? rightController.transform : null, new Vector3(0.195f, 1.3f, 0.25f), "Right");
                Controller leftController = player.LeftController;
                this.SetControllerPosition((leftController != null) ? leftController.transform : null, new Vector3(-0.195f, 1.3f, 0.25f), "Left");
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error setting controller positions: " + ex.Message, LogType.Error);
            }
        }

        private void SetControllerPosition(Transform controllerTransform, Vector3 position, string side)
        {
            if (controllerTransform != null)
            {
                controllerTransform.localPosition = position;
                controllerTransform.localRotation = Quaternion.Euler(298.5f, 0f, 0f);
                UIManager.LogHandler(side + " controller position and rotation set.", LogType.Log);
            }
            else
            {
                UIManager.LogHandler(side + " controller transform is null.", LogType.Warning);
            }
        }

        private void HandleGamepadInput()
        {
            Gamepad current = Gamepad.current;
            if (current != null)
            {
                try
                {
                    if (current.rightTrigger.wasPressedThisFrame)
                    {
                        Core.PlayerMessage("Selection Menu 2", 2f);
                    }
                    if (current.rightTrigger.isPressed)
                    {
                        this.ProcessGamepadTriggerActions(current);
                    }
                }
                catch (Exception ex)
                {
                    UIManager.LogHandler("Error in gamepad input: " + ex.Message, LogType.Error);
                }
            }
        }

        private void ProcessGamepadTriggerActions(Gamepad gamepad)
        {
            if (gamepad.dpad.down.wasPressedThisFrame)
            {
                this.SafeToggleEnvironment();
            }
            if (gamepad.dpad.up.wasPressedThisFrame)
            {
                this.SafeActivateThirdPerson();
            }
            if (gamepad.dpad.left.wasPressedThisFrame)
            {
                this.ToggleFullBodyRotation();
            }
            if (gamepad.aButton.wasPressedThisFrame)
            {
                this.SafeGrabAll();
            }
            if (gamepad.bButton.wasPressedThisFrame)
            {
                this.SafeGrabBags();
            }
            if (gamepad.xButton.wasPressedThisFrame)
            {
                this.SafeGrabCoins();
            }
            if (gamepad.yButton.wasPressedThisFrame)
            {
                this.SafeToggleFlight();
            }
            if (gamepad.leftStickButton.wasPressedThisFrame)
            {
                this.ResetSprintSpeed();
            }
            if (MenuPanel.holdDelay > 11)
            {
                this.ProcessGamepadHeldActions(gamepad);
            }
        }

        private void ProcessGamepadHeldActions(Gamepad gamepad)
        {
            // Note: the original really does check dpad.right twice here (debug
            // light + FOV up) and dpad.left for FOV down - kept exactly as shipped.
            if (gamepad.dpad.right.isPressed)
            {
                this.SafeToggleDebugLight();
            }
            if (gamepad.dpad.up.isPressed)
            {
                this.AdjustSprintSpeed(0.5f);
            }
            if (gamepad.dpad.down.isPressed)
            {
                this.AdjustSprintSpeed(-0.5f);
            }
            if (gamepad.dpad.right.isPressed)
            {
                this.AdjustFOV(5f);
            }
            if (gamepad.dpad.left.isPressed)
            {
                this.AdjustFOV(-5f);
            }
        }

        private void HandleKeyboardInput()
        {
            if (this.ValidatePlayerController("HandleKeyboardInput", false))
            {
                PlayerController playerController = PlayerController.Current;
                Transform transform = playerController.transform;
                if (transform != null)
                {
                    try
                    {
                        this.ProcessMovementInput(transform);
                        this.ProcessMouseInput();
                    }
                    catch (Exception ex)
                    {
                        UIManager.LogHandler("Error in keyboard input: " + ex.Message, LogType.Error);
                    }
                }
            }
        }

        // Movement keys are the InputSystem Key enum values the original was
        // compiled against: 61/62/63/64 = WASD, 15/18/33/37 = QEIJ, 1/67 = up,
        // 55/66 = down. Passed as literals, exactly like the shipped binary.
        private void ProcessMovementInput(Transform transform)
        {
            float num = this.CalculateMoveSpeed();
            if (this.IsKeyPressed((Key)61) || this.IsKeyPressed((Key)15))
            {
                transform.position += transform.right * -1f * num;
            }
            if (this.IsKeyPressed((Key)62) || this.IsKeyPressed((Key)18))
            {
                transform.position += transform.right * num;
            }
            if (this.IsKeyPressed((Key)63) || this.IsKeyPressed((Key)37))
            {
                transform.position += transform.forward * num;
            }
            if (this.IsKeyPressed((Key)64) || this.IsKeyPressed((Key)33))
            {
                transform.position += transform.forward * -1f * num;
            }
            bool isFlightModeEnabled = this.IsFlightModeEnabled();
            if ((this.IsKeyPressed((Key)1) || this.IsKeyPressed((Key)67)) && isFlightModeEnabled)
            {
                transform.position += transform.up * num;
            }
            if ((this.IsKeyPressed((Key)55) || this.IsKeyPressed((Key)66)) && isFlightModeEnabled)
            {
                transform.position += transform.up * -1f * num;
            }
            if (!isFlightModeEnabled)
            {
                this.HandleCrouchJump();
            }
        }

        private float CalculateMoveSpeed()
        {
            float num = MenuPanel.desiredMoveSpeed * Time.deltaTime;
            bool sprinting;
            Gamepad current = Gamepad.current;
            if (current != null && current.leftTrigger.isPressed)
            {
                sprinting = true;
            }
            else
            {
                Keyboard keyboard = Keyboard.current;
                sprinting = (keyboard != null) && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            }
            MenuPanel.isSprinting = sprinting;
            return (sprinting) ? (num * MenuPanel.sprintSpeed) : num;
        }

        private bool IsKeyPressed(KeyCode legacyKey, Key newKey)
        {
            Keyboard current = Keyboard.current;
            return current != null && current[newKey].isPressed;
        }

        private bool IsKeyPressed(Key key)
        {
            Keyboard current = Keyboard.current;
            return current != null && current[key].isPressed;
        }

        private bool IsFlightModeEnabled()
        {
            try
            {
                GameObject smoothLoco = Core.SmoothLoco;
                SmoothLocomotion smoothLocomotion = (smoothLoco != null) ? smoothLoco.GetComponent<SmoothLocomotion>() : null;
                return (smoothLocomotion != null) && smoothLocomotion.IsDebugMovement;
            }
            catch
            {
                return false;
            }
        }

        private void HandleCrouchJump()
        {
            Keyboard current = Keyboard.current;
            if (current != null)
            {
                if (current.spaceKey.wasReleasedThisFrame || current.pageUpKey.wasReleasedThisFrame)
                {
                    Transform transform = PlayerController.Current.transform;
                    transform.position += transform.up * (MenuPanel.desiredMoveSpeed * Time.deltaTime);
                }
                if (current.leftCtrlKey.wasPressedThisFrame || current.pageDownKey.wasReleasedThisFrame)
                {
                    this.ToggleCrouch();
                }
            }
        }

        private void ProcessMouseInput()
        {
            Mouse current = Mouse.current;
            if (current != null)
            {
                if (current.leftButton.wasPressedThisFrame)
                {
                    this.HandleMouseLeftClick();
                }
                if (current.leftButton.wasReleasedThisFrame)
                {
                    this.StopInteraction();
                }
                if (current.rightButton.isPressed)
                {
                    this.HandleMouseLook();
                }
            }
        }

        private void HandleMouseLeftClick()
        {
            try
            {
                if (this.ValidatePlayerController("HandleMouseLeftClick", false))
                {
                    PlayerController playerController = PlayerController.Current;
                    Camera camera = playerController.Camera;
                    Transform transform = (camera != null) ? camera.transform : null;
                    if (transform == null)
                    {
                        UIManager.LogHandler("Camera transform is null during mouse click.", LogType.Warning);
                    }
                    else
                    {
                        Vector3 position = transform.position;
                        Vector3 forward = transform.forward;
                        RaycastHit[] hits = Physics.RaycastAll(position, forward, RAYCAST_DISTANCE, ~LayerMask.NameToLayer("Player"));
                        if (hits.Length != 0)
                        {
                            this.ProcessRaycastHits(hits, playerController);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error in mouse click: " + ex.Message, LogType.Error);
            }
        }

        private void ProcessRaycastHits(RaycastHit[] hits, PlayerController player)
        {
            try
            {
                Array.Sort<RaycastHit>(hits, delegate(RaycastHit a, RaycastHit b)
                {
                    return a.distance.CompareTo(b.distance);
                });
                foreach (RaycastHit raycastHit in hits)
                {
                    Transform transform = raycastHit.transform;
                    GameObject gameObject = (transform != null) ? transform.gameObject : null;
                    if (gameObject != null)
                    {
                        UIManager.LogHandler("Hit object: " + gameObject.name, LogType.Log);
                        Interactable component = gameObject.GetComponent<Interactable>();
                        if (component != null && player.GetComponentInChildren<Interactable>() == null)
                        {
                            this.StartInteraction(component, player);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error processing raycast hits: " + ex.Message, LogType.Error);
            }
        }

        private void StartInteraction(Interactable interactable, PlayerController player)
        {
            try
            {
                Controller rightController = player.RightController;
                Interactor interactor = (rightController != null) ? rightController.Interactor : null;
                if (interactor != null)
                {
                    interactor.ResetTimeout();
                    interactor.StartInteract(interactable, false, false, false);
                    UIManager.LogHandler("Started interaction with: " + interactable.name, LogType.Log);
                }
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error starting interaction: " + ex.Message, LogType.Error);
            }
        }

        private void StopInteraction()
        {
            try
            {
                if (this.ValidatePlayerController("StopInteraction", false))
                {
                    Controller rightController = PlayerController.Current.RightController;
                    Interactor interactor = (rightController != null) ? rightController.Interactor : null;
                    if (interactor != null)
                    {
                        interactor.StopInteract(true, true);
                        UIManager.LogHandler("Stopped interaction.", LogType.Log);
                    }
                }
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error stopping interaction: " + ex.Message, LogType.Error);
            }
        }

        private void HandleMouseLook()
        {
            try
            {
                if (this.ValidatePlayerController("HandleMouseLook", false))
                {
                    Vector2 mouseDelta = this.GetMouseDelta();
                    PlayerController player = PlayerController.Current;
                    if (MenuPanel.rotateWholeBody)
                    {
                        this.RotateWholeBody(player, mouseDelta);
                    }
                    else
                    {
                        this.RotateCameraOnly(player, mouseDelta);
                    }
                }
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error in mouse look: " + ex.Message, LogType.Error);
            }
        }

        private Vector2 GetMouseDelta()
        {
            Vector2 result = Vector2.zero;
            Mouse current = Mouse.current;
            if (current != null)
            {
                result = current.delta.ReadValue();
            }
            Gamepad current2 = Gamepad.current;
            if (current2 != null && current2.rightStick.ReadValue() != Vector2.zero)
            {
                result = current2.rightStick.ReadValue() * 10f;
            }
            return result;
        }

        private void RotateWholeBody(PlayerController player, Vector2 mouseDelta)
        {
            Camera camera = player.Camera;
            Transform transform = (camera != null) ? camera.transform : null;
            Transform transform2 = player.transform;
            if (transform != null && transform2 != null)
            {
                transform.localEulerAngles = Vector3.zero;
                float num = transform2.localEulerAngles.y + mouseDelta.x * MOUSE_SENSITIVITY;
                float num2 = transform2.localEulerAngles.x - mouseDelta.y * MOUSE_SENSITIVITY;
                transform2.localEulerAngles = new Vector3(num2, num, 0f);
            }
        }

        private void RotateCameraOnly(PlayerController player, Vector2 mouseDelta)
        {
            Camera camera = player.Camera;
            Transform transform = (camera != null) ? camera.transform : null;
            Transform transform2 = player.transform;
            if (transform != null && transform2 != null)
            {
                if (transform2.localEulerAngles.x != 0f)
                {
                    transform2.localEulerAngles = Vector3.zero;
                    MelonLogger.Msg("Reset player body rotation to zero.");
                }
                float num = transform.localEulerAngles.x - mouseDelta.y * MOUSE_SENSITIVITY;
                float num2 = transform2.localEulerAngles.y + mouseDelta.x * MOUSE_SENSITIVITY;
                transform.localEulerAngles = new Vector3(num, 0f, 0f);
                transform2.localEulerAngles = new Vector3(0f, num2, 0f);
            }
        }

        private void SafeToggleEnvironment()
        {
            try
            {
                this.ToggleEnvironment();
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error toggling environment: " + ex.Message, LogType.Error);
            }
        }

        private void SafeActivateThirdPerson()
        {
            try
            {
                Core.activateThirdPerson = true;
                Core.PlayerMessage("Third Person", 2f);
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error activating third person: " + ex.Message, LogType.Error);
            }
        }

        private void SafeToggleFlight()
        {
            try
            {
                GameObject smoothLoco = Core.SmoothLoco;
                SmoothLocomotion smoothLocomotion = (smoothLoco != null) ? smoothLoco.GetComponent<SmoothLocomotion>() : null;
                if (smoothLocomotion != null)
                {
                    smoothLocomotion.ToggleDebug();
                    Core.PlayerMessage(string.Format("Flight: {0}", smoothLocomotion.IsDebugMovement), 0.3f);
                }
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error toggling flight: " + ex.Message, LogType.Error);
            }
        }

        private void SafeToggleDebugLight()
        {
            try
            {
                GameObject ghostHandCE = Core.GhostHandCE;
                LightHands lightHands = (ghostHandCE != null) ? ghostHandCE.GetComponent<LightHands>() : null;
                if (lightHands != null)
                {
                    lightHands.Toggle();
                    Core.PlayerMessage("Debug Light", 0.3f);
                }
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error toggling debug light: " + ex.Message, LogType.Error);
            }
        }

        private void SafeGrabAll()
        {
            try
            {
                this.GrabAll();
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error in GrabAll: " + ex.Message, LogType.Error);
            }
        }

        private void SafeGrabBags()
        {
            try
            {
                this.GrabBags();
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error in GrabBags: " + ex.Message, LogType.Error);
            }
        }

        private void SafeGrabCoins()
        {
            try
            {
                this.GrabCoins();
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error in GrabCoins: " + ex.Message, LogType.Error);
            }
        }

        private void ToggleEnvironment()
        {
            GameObject gameObject = GameObject.Find("Environment");
            if (gameObject == null)
            {
                UIManager.LogHandler("Environment object not found.", LogType.Warning);
            }
            else if (!MenuPanel.toggledEnvironment)
            {
                gameObject.transform.position = new Vector3(0f, -300f, 0f);
                MenuPanel.toggledEnvironment = true;
                Core.PlayerMessage("Environment Hidden", 2f);
            }
            else
            {
                gameObject.transform.position = new Vector3(0f, 0f, 0f);
                MenuPanel.toggledEnvironment = false;
                Core.PlayerMessage("Environment Shown", 2f);
            }
        }

        private void ToggleFullBodyRotation()
        {
            MenuPanel.rotateWholeBody = !MenuPanel.rotateWholeBody;
            Core.PlayerMessage(string.Format("Full Body Rotation: {0}", MenuPanel.rotateWholeBody), 2f);
        }

        private void ToggleCrouch()
        {
            try
            {
                if (this.ValidatePlayerController("ToggleCrouch", false))
                {
                    Camera camera = PlayerController.Current.Camera;
                    Transform transform = (camera != null) ? camera.transform : null;
                    if (transform != null)
                    {
                        MenuPanel.crouching = !MenuPanel.crouching;
                        if (MenuPanel.crouching)
                        {
                            transform.localPosition = new Vector3(0f, 1.0391f, 0f);
                            Core.PlayerMessage("Crouching", 1f);
                        }
                        else
                        {
                            transform.localPosition = new Vector3(0f, 1.45f, 0f);
                            Core.PlayerMessage("Standing", 1f);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error toggling crouch: " + ex.Message, LogType.Error);
            }
        }

        private void AdjustSprintSpeed(float delta)
        {
            try
            {
                MenuPanel.sprintSpeed = Mathf.Clamp(MenuPanel.sprintSpeed + delta, MIN_SPRINT_SPEED, MAX_SPRINT_SPEED);
                Core.PlayerMessage(string.Format("Sprint Speed: {0:F1}", MenuPanel.sprintSpeed), 0.3f);
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error adjusting sprint speed: " + ex.Message, LogType.Error);
            }
        }

        private void ResetSprintSpeed()
        {
            try
            {
                MenuPanel.sprintSpeed = 1.25f;
                Core.PlayerMessage(string.Format("Sprint Speed Reset: {0}", MenuPanel.sprintSpeed), 0.3f);
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error resetting sprint speed: " + ex.Message, LogType.Error);
            }
        }

        private void AdjustFOV(float delta)
        {
            try
            {
                if (this.ValidatePlayerController("AdjustFOV", false))
                {
                    Camera camera = PlayerController.Current.Camera;
                    Camera camera2 = (camera != null) ? camera.GetComponent<Camera>() : null;
                    if (camera2 != null)
                    {
                        camera2.fieldOfView = Mathf.Clamp(camera2.fieldOfView + delta, MIN_FOV, MAX_FOV);
                        Core.PlayerMessage(string.Format("FOV: {0:F0}", camera2.fieldOfView), 0.3f);
                    }
                }
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error adjusting FOV: " + ex.Message, LogType.Error);
            }
        }

        public void GrabAll()
        {
            if (this.ValidatePlayerController("GrabAll", true))
            {
                try
                {
                    GameObject[] array = Object.FindObjectsOfType<GameObject>();
                    int num = 0;
                    foreach (GameObject gameObject in array)
                    {
                        if (gameObject != null && this.ShouldPickupObject(gameObject) && this.SafePickupObject(gameObject))
                        {
                            num++;
                        }
                    }
                    this.ProcessInteractables(array);
                    Core.PlayerMessage(string.Format("Grabbed {0} items", num), 2f);
                }
                catch (Exception ex)
                {
                    UIManager.LogHandler("Error in GrabAll: " + ex.Message, LogType.Error);
                }
            }
        }

        private void ProcessInteractables(GameObject[] gameObjects)
        {
            try
            {
                PlayerController playerController = PlayerController.Current;
                Controller rightController = (playerController != null) ? playerController.RightController : null;
                Interactor interactor = (rightController != null) ? rightController.Interactor : null;
                if (interactor != null)
                {
                    foreach (GameObject gameObject in gameObjects)
                    {
                        if (gameObject == null)
                        {
                            continue;
                        }
                        Interactable component = gameObject.GetComponent<Interactable>();
                        if (component != null)
                        {
                            try
                            {
                                interactor.ResetTimeout();
                                interactor.StartInteract(component, false, false, false);
                                interactor.StopInteract(false, false);
                            }
                            catch (Exception ex)
                            {
                                UIManager.LogHandler("Error interacting with " + gameObject.name + ": " + ex.Message, LogType.Warning);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error processing interactables: " + ex.Message, LogType.Error);
            }
        }

        private bool ShouldPickupObject(GameObject obj)
        {
            if (obj == null || string.IsNullOrEmpty(obj.name))
            {
                return false;
            }
            string name = obj.name;
            return name.EndsWith("Bag(Clone)") || (name.Contains("Handle") && name.EndsWith("(Clone)")) || (name.Contains("Fabric") && name.EndsWith("(Clone)")) || (name.Contains("Coin") && name.EndsWith("(Clone)")) || (name.Contains("Ingot") && name.EndsWith("(Clone)")) || (name.Contains("Pouch") && name.EndsWith("(Clone)")) || (name.Contains("Cooked") && name.EndsWith("(Clone)"));
        }

        private bool SafePickupObject(GameObject obj)
        {
            try
            {
                Pickup pickup = (obj != null) ? obj.GetComponent<Pickup>() : null;
                if (pickup != null)
                {
                    pickup.Undock(null, true, 1);
                    return true;
                }
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error picking up " + ((obj != null) ? obj.name : null) + ": " + ex.Message, LogType.Warning);
            }
            return false;
        }

        public void GrabBags()
        {
            this.SafeGrabObjectsByName("Bag");
        }

        public void GrabCoins()
        {
            this.SafeGrabObjectsByName("Coin");
        }

        private void SafeGrabObjectsByName(string objName)
        {
            try
            {
                this.GrabObjectsByName(objName, string.Format("Temp{0}_{1}", objName, DateTime.Now.Ticks));
                Core.PlayerMessage("Grabbed all " + objName + "s", 2f);
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error grabbing " + objName + "s: " + ex.Message, LogType.Error);
            }
        }

        private void GrabObjectsByName(string objName, string tempName)
        {
            if (!this.ValidatePlayerController("GrabObjectsByName", true))
            {
                return;
            }
            GameObject[] array = Object.FindObjectsOfType<GameObject>();
            PlayerController playerController = PlayerController.Current;
            Controller rightController = (playerController != null) ? playerController.RightController : null;
            Interactor interactor = (rightController != null) ? rightController.Interactor : null;
            if (interactor == null)
            {
                UIManager.LogHandler("Interactor is null in GrabObjectsByName.", LogType.Error);
                return;
            }
            HashSet<GameObject> processed = new HashSet<GameObject>();
            try
            {
                foreach (GameObject gameObject in array)
                {
                    if (gameObject == null || processed.Contains(gameObject) || !gameObject.name.Contains(objName))
                    {
                        continue;
                    }
                    this.SafePickupObject(gameObject);
                    Interactable component = gameObject.GetComponent<Interactable>();
                    if (component != null)
                    {
                        gameObject.name = tempName;
                        this.SafeInteractWithObject(interactor, component);
                    }
                    processed.Add(gameObject);
                }
                foreach (GameObject gameObject2 in array)
                {
                    if (gameObject2 == null || processed.Contains(gameObject2) || !gameObject2.name.Contains(tempName))
                    {
                        continue;
                    }
                    this.SafePickupObject(gameObject2);
                    Interactable component2 = gameObject2.GetComponent<Interactable>();
                    if (component2 != null)
                    {
                        gameObject2.name = objName;
                        this.SafeInteractWithObject(interactor, component2);
                    }
                    processed.Add(gameObject2);
                }
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error in GrabObjectsByName for " + objName + ": " + ex.Message, LogType.Error);
            }
        }

        private void SafeInteractWithObject(Interactor interactor, Interactable interactable)
        {
            try
            {
                if (interactor != null)
                {
                    interactor.ResetTimeout();
                    interactor.StartInteract(interactable, false, false, false);
                    interactor.StopInteract(false, false);
                }
            }
            catch (Exception ex)
            {
                UIManager.LogHandler("Error interacting with object: " + ex.Message, LogType.Warning);
            }
        }
    }

    // ----------------------------------------------------------------------
    //  SourceEngineMovement - the ORIGINAL decompiled "Bhop" class (second
    //  decompile pass; missing from the first one). Two fixes so it
    //  recompiles: the broken `vector..ctor(...)` decompiler artifact became
    //  a normal `new Vector3(...)`, and the little Cmd input-holder struct
    //  (declared as a field but not included in the paste) was added back.
    // ----------------------------------------------------------------------
    public class SourceEngineMovement : MonoBehaviour
    {
        public Transform playerView;

        public float playerViewYOffset = 1.5f;

        public float xMouseSensitivity = 30f;

        public float yMouseSensitivity = 30f;

        public float gravity = 20f;

        public float friction = 6f;

        public float moveSpeed = 5f;

        public float runAcceleration = 14f;

        public float runDeacceleration = 10f;

        public float airAcceleration = 5f;

        public float airDecceleration = 5f;

        public float airControl = 0.3f;

        public float sideStrafeAcceleration = 50f;

        public float sideStrafeSpeed = 1f;

        public float jumpSpeed = 6f;

        public bool holdJumpToBhop = true;

        public float fpsDisplayRate = 4f;

        private int frameCount = 0;

        private float dt = 0f;

        private float fps = 0f;

        private CharacterController _controller;

        private float rotX = 0f;

        private float rotY = 0f;

        private Vector3 moveDirectionNorm = Vector3.zero;

        private Vector3 playerVelocity = Vector3.zero;

        private float playerTopVelocity = 0f;

        private bool wishJump = false;

        private float playerFriction = 0f;

        private Cmd _cmd;

        private bool isEnabled = false;

        // Small input holder the original keeps in a field (not included in
        // the paste). It's a struct - the field has no initializer and the
        // original code assigns into it without null checks.
        private struct Cmd
        {
            public float forwardMove;

            public float rightMove;
        }

        private void Start()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            if (this.playerView != null)
            {
                this.playerView.position = new Vector3(base.transform.position.x, base.transform.position.y + this.playerViewYOffset, base.transform.position.z);
            }
            this._controller = base.gameObject.GetComponent<CharacterController>();
            if (this._controller == null)
            {
                this._controller = base.gameObject.AddComponent<CharacterController>();
                this._controller.center = new Vector3(0f, 1f, 0f);
                this._controller.height = 2f;
                this._controller.radius = 0.5f;
            }
        }

        private void Update()
        {
            if (!this.isEnabled)
            {
                return;
            }
            if (this.playerView == null)
            {
                this.FindPlayerView();
            }
            if (this.playerView != null)
            {
                this.playerView.position = new Vector3(base.transform.position.x, base.transform.position.y + this.playerViewYOffset, base.transform.position.z);
            }
            this.CalculateFPS();
            this.LockCursor();
            this.RotateCamera();
            this.QueueJump();
            if (this._controller.isGrounded)
            {
                this.GroundMove();
            }
            else
            {
                this.AirMove();
            }
            this._controller.Move(this.playerVelocity * Time.deltaTime);
            this.CalculateTopVelocity();
        }

        private void FindPlayerView()
        {
            Transform transform = base.transform.Find("Relative Controllers");
            if (transform != null)
            {
                Transform transform2 = null;
                foreach (Transform transform3 in transform)
                {
                    if (transform3.name.Contains("Heads"))
                    {
                        transform2 = transform3;
                        break;
                    }
                }
                if (transform2 != null)
                {
                    Transform transform4 = transform2.Find("Height Fixer");
                    if (transform4 != null)
                    {
                        Transform transform5 = transform4.Find("VR Head (eye)");
                        if (transform5 != null)
                        {
                            this.playerView = transform5;
                        }
                        else
                        {
                            Debug.LogError("VR Head (eye) not found under Heads child.", null);
                        }
                    }
                    else
                    {
                        Debug.LogError("Height Fixer not found under Heads child.", null);
                    }
                }
                else
                {
                    Debug.LogError("Child containing 'Heads' not found under Relative Controllers.", null);
                }
            }
            else
            {
                Transform transform6 = PlayerController.Current.gameObject.transform.Find("Height Fixer");
                if (transform6 != null)
                {
                    Transform transform7 = transform6.Find("VR Head (eye)");
                    if (transform7 != null)
                    {
                        this.playerView = transform7;
                    }
                    else
                    {
                        Debug.LogError("VR Head (eye) not found under Height Fixer.", null);
                    }
                }
                else
                {
                    Debug.LogError("Height Fixer not found under PlayerController.Current GameObject.", null);
                }
            }
        }

        private void CalculateFPS()
        {
            this.frameCount++;
            this.dt += Time.deltaTime;
            if ((double)this.dt > 1.0 / (double)this.fpsDisplayRate)
            {
                this.fps = Mathf.Round((float)this.frameCount / this.dt);
                this.frameCount = 0;
                this.dt -= 1f / this.fpsDisplayRate;
            }
        }

        private void LockCursor()
        {
            if (Cursor.lockState != CursorLockMode.Locked && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        private void RotateCamera()
        {
            if (Mouse.current.rightButton.isPressed)
            {
                this.rotX -= Mouse.current.delta.y.ReadValue() * this.xMouseSensitivity * 0.02f;
                this.rotY += Mouse.current.delta.x.ReadValue() * this.yMouseSensitivity * 0.02f;
            }
            this.rotX = Mathf.Clamp(this.rotX, -90f, 90f);
            base.transform.rotation = Quaternion.Euler(0f, this.rotY, 0f);
            if (this.playerView != null)
            {
                this.playerView.rotation = Quaternion.Euler(this.rotX, this.rotY, 0f);
            }
        }

        private void GroundMove()
        {
            if (!this.wishJump)
            {
                this.ApplyFriction(1f);
            }
            else
            {
                this.ApplyFriction(0f);
            }
            this.SetMovementDir();
            Vector3 vector = new Vector3(this._cmd.rightMove, 0f, this._cmd.forwardMove);
            vector = base.transform.TransformDirection(vector);
            vector.Normalize();
            this.moveDirectionNorm = vector;
            float wishspeed = vector.magnitude * this.moveSpeed;
            this.Accelerate(vector, wishspeed, this.runAcceleration);
            this.playerVelocity.y = (0f - this.gravity) * Time.deltaTime;
            if (this.wishJump)
            {
                this.playerVelocity.y = this.jumpSpeed;
                this.wishJump = false;
            }
        }

        private void AirMove()
        {
            this.SetMovementDir();
            Vector3 vector = new Vector3(this._cmd.rightMove, 0f, this._cmd.forwardMove);
            vector = base.transform.TransformDirection(vector);
            vector.Normalize();
            this.moveDirectionNorm = vector;
            float wishspeed = vector.magnitude * this.moveSpeed;
            this.Accelerate(vector, wishspeed, this.airAcceleration);
            if (this.airControl > 0f)
            {
                this.AirControl(vector, wishspeed);
            }
            this.playerVelocity.y = this.playerVelocity.y - this.gravity * Time.deltaTime;
        }

        private void SetMovementDir()
        {
            this._cmd.forwardMove = (Keyboard.current.wKey.isPressed ? 1f : (Keyboard.current.sKey.isPressed ? -1f : 0f));
            this._cmd.rightMove = (Keyboard.current.dKey.isPressed ? 1f : (Keyboard.current.aKey.isPressed ? -1f : 0f));
        }

        private void ApplyFriction(float t)
        {
            Vector3 vector = this.playerVelocity;
            vector.y = 0f;
            float magnitude = vector.magnitude;
            float num = (magnitude < this.runDeacceleration) ? this.runDeacceleration : magnitude;
            float num2 = num * this.friction * Time.deltaTime * t;
            float num3 = Mathf.Max(magnitude - num2, 0f);
            if (magnitude > 0f)
            {
                num3 /= magnitude;
            }
            this.playerVelocity.x = this.playerVelocity.x * num3;
            this.playerVelocity.z = this.playerVelocity.z * num3;
        }

        private void Accelerate(Vector3 wishdir, float wishspeed, float accel)
        {
            float num = Vector3.Dot(this.playerVelocity, wishdir);
            float num2 = wishspeed - num;
            float num3 = Mathf.Min(num2, accel * Time.deltaTime * wishspeed);
            this.playerVelocity.x = this.playerVelocity.x + num3 * wishdir.x;
            this.playerVelocity.z = this.playerVelocity.z + num3 * wishdir.z;
        }

        private void AirControl(Vector3 wishdir, float wishspeed)
        {
            if ((double)Mathf.Abs(this._cmd.forwardMove) >= 0.001 && (double)Mathf.Abs(wishspeed) >= 0.001)
            {
                float y = this.playerVelocity.y;
                this.playerVelocity.y = 0f;
                float magnitude = this.playerVelocity.magnitude;
                this.playerVelocity.Normalize();
                float num = Vector3.Dot(this.playerVelocity, wishdir);
                float num2 = 32f * this.airControl * num * num * Time.deltaTime;
                if (num > 0f)
                {
                    this.playerVelocity.x = this.playerVelocity.x * magnitude + wishdir.x * num2;
                    this.playerVelocity.y = this.playerVelocity.y * magnitude + wishdir.y * num2;
                    this.playerVelocity.z = this.playerVelocity.z * magnitude + wishdir.z * num2;
                    this.playerVelocity.Normalize();
                    this.moveDirectionNorm = this.playerVelocity;
                }
                this.playerVelocity.x = this.playerVelocity.x * magnitude;
                this.playerVelocity.y = y;
                this.playerVelocity.z = this.playerVelocity.z * magnitude;
            }
        }

        private void CalculateTopVelocity()
        {
            Vector3 vector = this.playerVelocity;
            vector.y = 0f;
            this.playerTopVelocity = Mathf.Max(this.playerTopVelocity, vector.magnitude);
        }

        private void QueueJump()
        {
            if (this.holdJumpToBhop)
            {
                this.wishJump = Keyboard.current.spaceKey.isPressed;
            }
            else
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame && !this.wishJump)
                {
                    this.wishJump = true;
                }
                if (Keyboard.current.spaceKey.wasReleasedThisFrame)
                {
                    this.wishJump = false;
                }
            }
        }

        public void ToggleMovement(bool value)
        {
            this.isEnabled = value;
        }
    }

    // Forces LocalPlatformProvider.GetRuntimePlatform to report platform 36
    // (the Pico value the original mod spoofed). The decompiled attribute
    // pointed at typeof(object) - which has no such method - while the
    // TargetType() helper next to it made the intent clear, so this uses the
    // standard [HarmonyPatch] + TargetType/TargetMethod pattern instead.
    [HarmonyPatch]
    public static class LocalPlatformProviderPatch
    {
        private static Type TargetType()
        {
            return AccessTools.TypeByName("Alta.PlatformInformation.LocalPlatformProvider");
        }

        private static string TargetMethod()
        {
            return "GetRuntimePlatform";
        }

        private static bool Prefix(ref object __result)
        {
            __result = 36;
            return false;
        }
    }

    [HarmonyPatch(typeof(PlatformProvider))]
    public static class PlatformProviderPatches
    {
        [HarmonyPatch("IsWindows")]
        [HarmonyPrefix]
        private static bool IsWindows_Prefix(ref bool __result)
        {
            __result = false;
            return false;
        }

        [HarmonyPatch("IsPico")]
        [HarmonyPrefix]
        private static bool IsPico_Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Status))]
    [HarmonyPatch("get_Platform")]
    public class StatusPlatformPatch
    {
        private static bool Prefix(ref Status.StatusPlatform __result)
        {
            __result = (Status.StatusPlatform)2;
            return false;
        }
    }
}
