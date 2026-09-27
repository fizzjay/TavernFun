using Alta.Character;
using Alta.Chunks;
using Alta.Impact;
using Alta.StatSystem;
using Harmony;
using HarmonyLib;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[assembly: MelonInfo(typeof(TavernFun.TavernFunMod), "TavernFun", "1.0.0", "fizzjay")]
[assembly: MelonGame("Alta", "A Township Tale")]

namespace TavernFun
{
    // Standalone MelonMod. Tan/light-brown outer frame, dark-brown interior, clean gold
    // buttons with white shadowed labels. Toggled with M.
    //
    // This single mod now owns everything that used to live in the separate Flatscreen ATT
    // mod (desktop-input VR emulation, hand emulation, look targeting, camera stabilizer /
    // OpenXR Harmony patches, server browser). PanKake no longer needs a second mod loaded -
    // FlatscreenCore is just a plain class driven from here.
    public sealed class TavernFunMod : MelonMod
    {
        private static readonly HarmonyLib.Harmony TavernFunHarmony = new HarmonyLib.Harmony("Tavernfun.fizzjay");

        public override void OnInitializeMelon()
        {
            TavernFunHarmony.PatchAll();
            TavernFun.AmbienceSoundPatch.TryApply(TavernFunHarmony);
            _menu.Init();
        }

        public override void OnUpdate()
        {
            _menu.Update();
        }

        public override void OnLateUpdate()
        {
            _menu.LateUpdate();
        }

        public override void OnGUI()
        {
            _menu.Draw();
        }

        private readonly ControlMenu _menu = new ControlMenu();
    }


    // Token: 0x02000005 RID: 5
    internal sealed class GameReflection
    {
        // Token: 0x1700002E RID: 46
        // (get) Token: 0x0600006A RID: 106 RVA: 0x00004E94 File Offset: 0x00003094
        // (set) Token: 0x0600006B RID: 107 RVA: 0x00004EAB File Offset: 0x000030AB
        public string LastInteractorName { get; private set; }

        // Token: 0x1700002F RID: 47
        // (get) Token: 0x0600006C RID: 108 RVA: 0x00004EB4 File Offset: 0x000030B4
        // (set) Token: 0x0600006D RID: 109 RVA: 0x00004ECB File Offset: 0x000030CB
        public string LastInteractResult { get; private set; }
        private object _cachedQuickAccessMenu;
        private MethodInfo _qamActivateMethod;
        private MethodInfo _qamCloseAndActivateMethod;
        private FieldInfo _qamSpawnedMenusField;
        private MethodInfo _qamUpdateMarkClosestMenuMethod;
        private int _quickAccessSelectedIndex = -1;
        // Sub-bubble navigation — the controller stores sub-menus in devQuickAccessMenus
        // and quickAccessMenus fields that populate when a parent bubble is hovered.
        private FieldInfo _qamDevMenusField;
        private FieldInfo _qamExtraMenusField;
        private int _subBubbleSelectedIndex = -1;
        private bool _isInSubBubble;
        // Track whether we are one level deep (inside a sub-bubble ring).
        internal bool IsInSubBubble { get { return _isInSubBubble; } }

        private object FindQuickAccessMenu()
        {
            Component cached = this._cachedQuickAccessMenu as Component;
            if (cached != null && cached.gameObject != null && cached.gameObject.activeInHierarchy)
            {
                return this._cachedQuickAccessMenu;
            }
            this._cachedQuickAccessMenu = null;
            MonoBehaviour[] array = Object.FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour monoBehaviour in array)
            {
                if (monoBehaviour != null && monoBehaviour.GetType().Name == "QuickAccessMenuController")
                {
                    this._cachedQuickAccessMenu = monoBehaviour;
                    return monoBehaviour;
                }
            }
            return null;
        }
        internal int GrabByName(string nameFilter, int limit = int.MaxValue)
        {
            object player = this.FindLocalPlayer();
            object rightInput = this.GetRightInput(player);
            object interactor = this.FindInteractorForInput(rightInput);
            if (interactor == null)
            {
                this.LastInteractResult = "grab by name failed: no interactor";
                return 0;
            }
            string filterLower = string.IsNullOrEmpty(nameFilter) ? null : nameFilter.ToLowerInvariant();
            int grabbedCount = 0;
            MonoBehaviour[] all = Object.FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour mb in all)
            {
                if (grabbedCount >= limit)
                {
                    break;
                }
                if (mb == null || !GameReflection.IsPickupTyped(mb.GetType()))
                {
                    continue;
                }
                if (filterLower != null && mb.name.ToLowerInvariant().IndexOf(filterLower, StringComparison.Ordinal) < 0)
                {
                    continue;
                }
                if (this.TryPickupGrab(interactor, mb))
                {
                    grabbedCount++;
                }
            }
            this.LastInteractResult = "grabbed " + grabbedCount + (string.IsNullOrEmpty(nameFilter) ? "" : " matching '" + nameFilter + "'");
            return grabbedCount;
        }

        private static bool IsPickupTyped(Type type)
        {
            while (type != null)
            {
                if (type.Name == "Pickup")
                {
                    return true;
                }
                type = type.BaseType;
            }
            return false;
        }
        internal bool TryOpenQuickAccessMenu()
        {
            object qam = this.FindQuickAccessMenu();
            if (qam == null)
            {
                this.LastInteractResult = "QuickAccessMenuController not found";
                return false;
            }
            Type type = qam.GetType();
            if (this._qamActivateMethod == null || this._qamActivateMethod.DeclaringType != type)
            {
                this._qamActivateMethod = type.GetMethod("Activate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            if (this._qamActivateMethod == null)
            {
                this.LastInteractResult = "QAM Activate not found";
                return false;
            }
            try
            {
                this._qamActivateMethod.Invoke(qam, null);
                this.LastInteractResult = "QAM opened";
                return true;
            }
            catch
            {
                this.LastInteractResult = "QAM Activate threw";
                return false;
            }
        }

        internal bool TryCloseQuickAccessMenu()
        {
            object qam = this._cachedQuickAccessMenu;
            if (qam == null)
            {
                this.LastInteractResult = "QAM not open";
                return false;
            }
            Type type = qam.GetType();
            if (this._qamCloseAndActivateMethod == null || this._qamCloseAndActivateMethod.DeclaringType != type)
            {
                this._qamCloseAndActivateMethod = type.GetMethod("CloseAndTryActivateSelectedMenu", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            if (this._qamCloseAndActivateMethod == null)
            {
                this.LastInteractResult = "QAM close method not found";
                return false;
            }
            try
            {
                this._qamCloseAndActivateMethod.Invoke(qam, null);
                this._quickAccessSelectedIndex = -1;
                this.LastInteractResult = "QAM closed";
                return true;
            }
            catch
            {
                this.LastInteractResult = "QAM close threw";
                return false;
            }
        }

        private void EnsureQuickAccessSelectionReflection(Type type)
        {
            if (this._qamSpawnedMenusField == null || this._qamSpawnedMenusField.DeclaringType != type)
            {
                this._qamSpawnedMenusField = type.GetField("spawnedMenus", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            if (this._qamUpdateMarkClosestMenuMethod == null || this._qamUpdateMarkClosestMenuMethod.DeclaringType != type)
            {
                this._qamUpdateMarkClosestMenuMethod = type.GetMethod("UpdateMarkClosestMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            }
        }

        private IList GetQuickAccessSpawnedMenus(object qam)
        {
            if (qam == null)
            {
                return null;
            }
            Type type = qam.GetType();
            this.EnsureQuickAccessSelectionReflection(type);
            if (this._qamSpawnedMenusField == null)
            {
                return null;
            }
            try
            {
                return this._qamSpawnedMenusField.GetValue(qam) as IList;
            }
            catch
            {
                return null;
            }
        }

        internal bool TrySelectQuickAccessBubble(int index)
        {
            object qam = this._cachedQuickAccessMenu;
            IList spawnedMenus = this.GetQuickAccessSpawnedMenus(qam);
            if (spawnedMenus == null || spawnedMenus.Count == 0)
            {
                return false;
            }
            index = ((index % spawnedMenus.Count) + spawnedMenus.Count) % spawnedMenus.Count;
            object bubble = spawnedMenus[index];
            if (bubble == null)
            {
                return false;
            }
            Type type = qam.GetType();
            this.EnsureQuickAccessSelectionReflection(type);
            if (this._qamUpdateMarkClosestMenuMethod == null)
            {
                return false;
            }
            try
            {
                this._qamUpdateMarkClosestMenuMethod.Invoke(qam, new object[]
                {
                    bubble
                });
                this._quickAccessSelectedIndex = index;
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal bool TryCycleQuickAccessSelection(int direction)
        {
            object qam = this._cachedQuickAccessMenu;
            IList spawnedMenus = this.GetQuickAccessSpawnedMenus(qam);
            if (spawnedMenus == null || spawnedMenus.Count == 0)
            {
                return false;
            }
            int index = this._quickAccessSelectedIndex;
            if (index < 0 || index >= spawnedMenus.Count)
            {
                index = 0;
            }
            else
            {
                index = (index + direction + spawnedMenus.Count) % spawnedMenus.Count;
            }
            return this.TrySelectQuickAccessBubble(index);
        }

        internal void TryMaintainQuickAccessSelection()
        {
            if (this._quickAccessSelectedIndex < 0)
            {
                return;
            }
            this.TrySelectQuickAccessBubble(this._quickAccessSelectedIndex);
        }

        // Enter the sub-bubble ring of the currently selected main bubble.
        // The controller exposes populated child lists in devQuickAccessMenus and quickAccessMenus
        // once a parent bubble is hovered/selected — we read those directly.
        internal bool TryEnterSelectedBubble()
        {
            IList subMenus = GetCurrentSubMenus();
            if (subMenus == null || subMenus.Count == 0) return false;
            _isInSubBubble = true;
            _subBubbleSelectedIndex = 0;
            return TrySelectInList(subMenus, 0);
        }

        // Navigate back from sub-bubbles to the main ring.
        internal bool TryBackOutOfSubBubble()
        {
            if (!_isInSubBubble) return false;
            _isInSubBubble = false;
            _subBubbleSelectedIndex = -1;
            TryMaintainQuickAccessSelection();
            return true;
        }

        internal bool TryCycleSubBubble(int direction)
        {
            IList subMenus = GetCurrentSubMenus();
            if (subMenus == null || subMenus.Count == 0) { _isInSubBubble = false; return false; }
            int idx = _subBubbleSelectedIndex;
            if (idx < 0 || idx >= subMenus.Count) idx = 0;
            else idx = (idx + direction + subMenus.Count) % subMenus.Count;
            // Wrap-around exits the sub-ring back to the main ring
            if (idx == 0 && direction != 0 && _subBubbleSelectedIndex == 0)
            {
                _isInSubBubble = false;
                _subBubbleSelectedIndex = -1;
                return TryCycleQuickAccessSelection(direction);
            }
            return TrySelectInList(subMenus, idx);
        }

        // Get the sub-menus that are currently active on the controller for the selected bubble.
        private IList GetCurrentSubMenus()
        {
            object qam = _cachedQuickAccessMenu;
            if (qam == null) return null;
            Type type = qam.GetType();
            // Ensure field references are up to date.
            if (_qamDevMenusField == null || _qamDevMenusField.DeclaringType != type)
                _qamDevMenusField = type.GetField("devQuickAccessMenus", BindingFlags.Instance | BindingFlags.NonPublic);
            if (_qamExtraMenusField == null || _qamExtraMenusField.DeclaringType != type)
                _qamExtraMenusField = type.GetField("quickAccessMenus", BindingFlags.Instance | BindingFlags.NonPublic);

            // Prefer devQuickAccessMenus if it has entries, then quickAccessMenus.
            foreach (var field in new[] { _qamDevMenusField, _qamExtraMenusField })
            {
                if (field == null) continue;
                try
                {
                    IList list = field.GetValue(qam) as IList;
                    if (list != null && list.Count > 0) return list;
                }
                catch { }
            }
            return null;
        }

        private bool TrySelectInList(IList list, int index)
        {
            if (list == null || index < 0 || index >= list.Count) return false;
            object item = list[index];
            if (item == null) return false;
            object qam = _cachedQuickAccessMenu;
            if (qam == null) return false;
            EnsureQuickAccessSelectionReflection(qam.GetType());
            if (_qamUpdateMarkClosestMenuMethod == null) return false;
            try
            {
                _qamUpdateMarkClosestMenuMethod.Invoke(qam, new object[] { item });
                _subBubbleSelectedIndex = index;
                return true;
            }
            catch { return false; }
        }

        // Clear sub-bubble state when the QAM is closed.
        internal void ResetSubBubbleState()
        {
            _isInSubBubble = false;
            _subBubbleSelectedIndex = -1;
        }
        // Token: 0x17000030 RID: 48
        // (get) Token: 0x0600006E RID: 110 RVA: 0x00004ED4 File Offset: 0x000030D4
        public string PlayerTypeName
        {
            get
            {
                return (this._playerType == null) ? "not found" : this._playerType.FullName;
            }
        }

        // Token: 0x17000031 RID: 49
        // (get) Token: 0x0600006F RID: 111 RVA: 0x00004F08 File Offset: 0x00003108
        public string PlayerControllerTypeName
        {
            get
            {
                return (this._playerControllerType == null) ? "not found" : this._playerControllerType.FullName;
            }
        }

        // Token: 0x17000032 RID: 50
        // (get) Token: 0x06000070 RID: 112 RVA: 0x00004F3C File Offset: 0x0000313C
        public bool HasCurrentPlayerProperty
        {
            get
            {
                return this._currentPlayerProperty != null;
            }
        }

        // Token: 0x17000033 RID: 51
        // (get) Token: 0x06000071 RID: 113 RVA: 0x00004F5C File Offset: 0x0000315C
        public bool HasCurrentPlayerControllerProperty
        {
            get
            {
                return this._currentPlayerControllerProperty != null;
            }
        }

        // Token: 0x17000034 RID: 52
        // (get) Token: 0x06000072 RID: 114 RVA: 0x00004F7C File Offset: 0x0000317C
        public bool HasPlayerTransformProperty
        {
            get
            {
                return this._playerTransformProperty != null;
            }
        }

        // Token: 0x17000035 RID: 53
        // (get) Token: 0x06000073 RID: 115 RVA: 0x00004F9C File Offset: 0x0000319C
        public bool HasLeftInputProperty
        {
            get
            {
                return this._leftInputProperty != null;
            }
        }

        // Token: 0x17000036 RID: 54
        // (get) Token: 0x06000074 RID: 116 RVA: 0x00004FBC File Offset: 0x000031BC
        public bool HasRightInputProperty
        {
            get
            {
                return this._rightInputProperty != null;
            }
        }

        // Token: 0x17000037 RID: 55
        // (get) Token: 0x06000075 RID: 117 RVA: 0x00004FDC File Offset: 0x000031DC
        public bool HasLocomotionController
        {
            get
            {
                return this._locomotionController as Component != null;
            }
        }

        // Token: 0x06000076 RID: 118 RVA: 0x00005000 File Offset: 0x00003200
        public object FindLocalPlayer()
        {
            this.EnsurePlayerType();
            object obj = (this._currentPlayerProperty == null) ? null : this._currentPlayerProperty.GetValue(null, null);
            object result;
            if (this.IsPlayableLocalPlayer(obj))
            {
                result = obj;
            }
            else
            {
                object obj2 = this.FindScenePlayer();
                if (obj2 != null)
                {
                    result = obj2;
                }
                else
                {
                    result = obj;
                }
            }
            return result;
        }

        // Token: 0x06000077 RID: 119 RVA: 0x00005064 File Offset: 0x00003264
        public Transform GetPlayerTransform(object player)
        {
            Transform result;
            if (player == null)
            {
                result = null;
            }
            else
            {
                this.EnsurePlayerMembers(player.GetType());
                if (this._playerTransformProperty != null)
                {
                    try
                    {
                        object value = this._playerTransformProperty.GetValue(player, null);
                        Transform transform = value as Transform;
                        if (transform != null)
                        {
                            return transform;
                        }
                    }
                    catch
                    {
                    }
                }
                Component component = player as Component;
                if (component != null)
                {
                    result = component.transform;
                }
                else
                {
                    result = GameReflection.GetProperty<Transform>(player, "transform");
                }
            }
            return result;
        }

        // Token: 0x06000078 RID: 120 RVA: 0x00005120 File Offset: 0x00003320
        public Transform GetPlayerRootTransform(object player)
        {
            Transform playerTransform = this.GetPlayerTransform(player);
            Transform result;
            if (playerTransform == null)
            {
                result = null;
            }
            else
            {
                result = ((playerTransform.root != null) ? playerTransform.root : playerTransform);
            }
            return result;
        }

        // Token: 0x06000079 RID: 121 RVA: 0x00005168 File Offset: 0x00003368
        public Transform GetPlayerHeadTransform(object player)
        {
            Transform smoothLocomotionHead = this.GetSmoothLocomotionHead();
            Transform result;
            if (smoothLocomotionHead != null)
            {
                result = smoothLocomotionHead;
            }
            else
            {
                Camera playerCamera = this.GetPlayerCamera(player);
                if (playerCamera != null)
                {
                    result = playerCamera.transform;
                }
                else
                {
                    result = this.GetPlayerTransform(player);
                }
            }
            return result;
        }

        // Token: 0x0600007A RID: 122 RVA: 0x000051BC File Offset: 0x000033BC
        public Camera GetPlayerCamera(object player)
        {
            Camera result;
            if (player == null)
            {
                result = null;
            }
            else
            {
                Camera camera = GameReflection.GetProperty<Camera>(player, "Camera");
                if (camera != null)
                {
                    result = camera;
                }
                else
                {
                    Component component = player as Component;
                    if (component != null)
                    {
                        camera = component.GetComponentInChildren<Camera>(true);
                        if (camera != null)
                        {
                            return camera;
                        }
                    }
                    result = null;
                }
            }
            return result;
        }

        // Token: 0x0600007B RID: 123 RVA: 0x00005230 File Offset: 0x00003430
        public object FindLocalController()
        {
            this.EnsurePlayerControllerType();
            object obj = (this._currentPlayerControllerProperty == null) ? null : this._currentPlayerControllerProperty.GetValue(null, null);
            object result;
            if (obj != null)
            {
                result = obj;
            }
            else
            {
                result = this.FindSceneController();
            }
            return result;
        }
        internal CharacterController FindLocalCharacterController(object player)
        {
            object locomotion = this.FindSmoothLocomotion(player);
            SmoothLocomotion smoothLocomotion = locomotion as SmoothLocomotion;
            return (smoothLocomotion != null) ? smoothLocomotion.CharacterController : null;
        }
        // Token: 0x0600007C RID: 124 RVA: 0x0000527C File Offset: 0x0000347C
        public Transform GetControllerTransform(object controller)
        {
            Transform result;
            if (controller == null)
            {
                result = null;
            }
            else
            {
                Component component = controller as Component;
                if (component != null)
                {
                    result = component.transform;
                }
                else
                {
                    result = GameReflection.GetProperty<Transform>(controller, "transform");
                }
            }
            return result;
        }

        // Token: 0x0600007D RID: 125 RVA: 0x000052C8 File Offset: 0x000034C8
        public Camera GetControllerCamera(object controller)
        {
            Camera result;
            if (controller == null)
            {
                result = null;
            }
            else
            {
                Camera camera = GameReflection.GetProperty<Camera>(controller, "Camera");
                if (camera != null)
                {
                    result = camera;
                }
                else
                {
                    Component component = controller as Component;
                    if (component != null)
                    {
                        camera = component.GetComponentInChildren<Camera>(true);
                        if (camera != null)
                        {
                            return camera;
                        }
                    }
                    result = null;
                }
            }
            return result;
        }

        // Token: 0x0600007E RID: 126 RVA: 0x0000533C File Offset: 0x0000353C
        public object GetLeftInput(object player)
        {
            this.EnsurePlayerMembers(player.GetType());
            return (this._leftInputProperty == null) ? null : this._leftInputProperty.GetValue(player, null);
        }

        // Token: 0x0600007F RID: 127 RVA: 0x0000537C File Offset: 0x0000357C
        public object GetRightInput(object player)
        {
            this.EnsurePlayerMembers(player.GetType());
            return (this._rightInputProperty == null) ? null : this._rightInputProperty.GetValue(player, null);
        }

        // Token: 0x06000080 RID: 128 RVA: 0x000053BC File Offset: 0x000035BC
        public GameReflection.PlayerInputPair FindLoosePlayerInputs()
        {
            object obj = this.IsUsablePlayerInput(this._looseInputPair.Left) ? this._looseInputPair.Left : null;
            object obj2 = this.IsUsablePlayerInput(this._looseInputPair.Right) ? this._looseInputPair.Right : null;
            GameReflection.PlayerInputPair looseInputPair;
            if (obj != null && obj2 != null)
            {
                looseInputPair = this._looseInputPair;
            }
            else
            {
                this._looseInputPair = new GameReflection.PlayerInputPair
                {
                    Left = obj,
                    Right = obj2
                };
                MonoBehaviour[] array = Object.FindObjectsOfType<MonoBehaviour>();
                foreach (MonoBehaviour monoBehaviour in array)
                {
                    if (!(monoBehaviour == null) && !(monoBehaviour.GetType().Name != "PlayerInput") && this.IsUsablePlayerInput(monoBehaviour))
                    {
                        object property = GameReflection.GetProperty<object>(monoBehaviour, "HandIndex");
                        string text = (property == null) ? string.Empty : property.ToString().ToLowerInvariant();
                        if (text.Contains("left") && this._looseInputPair.Left == null)
                        {
                            this._looseInputPair.Left = monoBehaviour;
                        }
                        else if (text.Contains("right") && this._looseInputPair.Right == null)
                        {
                            this._looseInputPair.Right = monoBehaviour;
                        }
                        else if (this._looseInputPair.Left == null)
                        {
                            this._looseInputPair.Left = monoBehaviour;
                        }
                        else if (this._looseInputPair.Right == null && !object.ReferenceEquals(this._looseInputPair.Left, monoBehaviour))
                        {
                            this._looseInputPair.Right = monoBehaviour;
                        }
                    }
                }
                if (this._looseInputPair.Left == null && this._looseInputPair.Right != null)
                {
                    this._looseInputPair.Left = this._looseInputPair.Right;
                }
                if (this._looseInputPair.Right == null && this._looseInputPair.Left != null)
                {
                    this._looseInputPair.Right = this._looseInputPair.Left;
                }
                looseInputPair = this._looseInputPair;
            }
            return looseInputPair;
        }

        // Token: 0x06000081 RID: 129 RVA: 0x00005638 File Offset: 0x00003838
        public bool TryTranslateWithLocomotion(object player, Vector3 translation)
        {
            object obj = this.FindLocomotionController(player);
            bool result;
            if (obj == null)
            {
                result = this.TryTranslateWithController(player, translation);
            }
            else
            {
                if (this._locomotionTranslateMethod == null)
                {
                    this._locomotionTranslateMethod = obj.GetType().GetMethod("Translate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[]
                    {
                        typeof(Vector3)
                    }, null);
                }
                if (this._locomotionTranslateMethod == null)
                {
                    result = false;
                }
                else
                {
                    try
                    {
                        this._locomotionTranslateMethod.Invoke(obj, new object[]
                        {
                            translation
                        });
                        result = true;
                    }
                    catch
                    {
                        result = this.TryTranslateWithController(player, translation);
                    }
                }
            }
            return result;
        }

        // Token: 0x06000082 RID: 130 RVA: 0x0000570C File Offset: 0x0000390C
        public bool TryTranslateWithController(object controller, Vector3 translation)
        {
            Component component = controller as Component;
            bool result;
            if (component == null)
            {
                result = this.TryTranslateWithCharacterController(controller, translation);
            }
            else
            {
                Type type = controller.GetType();
                MethodInfo method = type.GetMethod("Move", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[]
                {
                    typeof(Vector3)
                }, null);
                if (method == null)
                {
                    method = type.GetMethod("Translate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[]
                    {
                        typeof(Vector3)
                    }, null);
                }
                if (method != null)
                {
                    try
                    {
                        method.Invoke(controller, new object[]
                        {
                            translation
                        });
                        this.LastInteractResult = type.Name + "." + method.Name + " called";
                        return true;
                    }
                    catch
                    {
                    }
                }
                result = this.TryTranslateWithCharacterController(controller, translation);
            }
            return result;
        }

        // Token: 0x06000083 RID: 131 RVA: 0x00005824 File Offset: 0x00003A24
        public bool IsMovementBlocked(object player, object controller)
        {
            return this.IsBlockedPlayerState(player);
        }

        // Token: 0x06000084 RID: 132 RVA: 0x00005840 File Offset: 0x00003A40
        public bool IsCustomizationActive()
        {
            bool result;
            if (Time.unscaledTime < this._customizationScanTime + 0.5f)
            {
                result = this._customizationActive;
            }
            else
            {
                this._customizationScanTime = Time.unscaledTime;
                this._customizationActive = false;
                MonoBehaviour[] array = Object.FindObjectsOfType<MonoBehaviour>();
                foreach (MonoBehaviour monoBehaviour in array)
                {
                    if (monoBehaviour != null && monoBehaviour.gameObject.activeInHierarchy && (GameReflection.TextMatchesAny(monoBehaviour.GetType().Name, GameReflection.CustomizationHints) || GameReflection.TextMatchesAny(monoBehaviour.GetType().FullName, GameReflection.CustomizationHints) || GameReflection.TextMatchesAny(monoBehaviour.name, GameReflection.CustomizationHints)))
                    {
                        this._customizationActive = true;
                        return true;
                    }
                }
                GameObject[] array3 = Object.FindObjectsOfType<GameObject>();
                foreach (GameObject gameObject in array3)
                {
                    if (gameObject != null && gameObject.activeInHierarchy && GameReflection.TextMatchesAny(gameObject.name, GameReflection.CustomizationHints))
                    {
                        this._customizationActive = true;
                        return true;
                    }
                }
                result = false;
            }
            return result;
        }

        // Token: 0x06000085 RID: 133 RVA: 0x000059A0 File Offset: 0x00003BA0
        public bool IsCustomizationObject(object value)
        {
            Component component = value as Component;
            bool result;
            if (component == null)
            {
                result = (value != null && (GameReflection.TextMatchesAny(value.GetType().Name, GameReflection.CustomizationHints) || GameReflection.TextMatchesAny(value.GetType().FullName, GameReflection.CustomizationHints)));
            }
            else
            {
                result = (GameReflection.TextMatchesAny(component.GetType().Name, GameReflection.CustomizationHints) || GameReflection.TextMatchesAny(component.GetType().FullName, GameReflection.CustomizationHints) || GameReflection.TextMatchesAny(component.name, GameReflection.CustomizationHints) || this.IsCustomizationTransform(component.transform));
            }
            return result;
        }

        // Token: 0x06000086 RID: 134 RVA: 0x00005A60 File Offset: 0x00003C60
        public bool IsCustomizationTransform(Transform transform)
        {
            while (transform != null)
            {
                if (GameReflection.TextMatchesAny(transform.name, GameReflection.CustomizationHints))
                {
                    return true;
                }
                transform = transform.parent;
            }
            return false;
        }

        // Token: 0x06000087 RID: 135 RVA: 0x00005AA8 File Offset: 0x00003CA8
        private bool IsBlockedPlayerState(object target)
        {
            object playerStateManager = this.GetPlayerStateManager(target);
            if (playerStateManager != null)
            {
                object obj = this.ReadCurrentStateType(playerStateManager);
                if (GameReflection.TextLooksBlockedPlayerState((obj == null) ? null : obj.ToString()))
                {
                    return true;
                }
            }
            object obj2;
            if ((obj2 = GameReflection.GetProperty<object>(target, "State")) == null && (obj2 = GameReflection.GetProperty<object>(target, "Status")) == null)
            {
                obj2 = (GameReflection.GetProperty<object>(target, "CurrentState") ?? GameReflection.GetField<object>(target, "currentState"));
            }
            object obj3 = obj2;
            return GameReflection.TextLooksBlockedPlayerState((obj3 == null) ? null : obj3.ToString());
        }

        // Token: 0x06000088 RID: 136 RVA: 0x00005B44 File Offset: 0x00003D44
        private object GetPlayerStateManager(object target)
        {
            object obj = GameReflection.GetProperty<object>(target, "StateManager") ?? GameReflection.GetField<object>(target, "stateManager");
            object result;
            if (obj != null)
            {
                result = obj;
            }
            else
            {
                Component component = target as Component;
                if (component == null)
                {
                    result = null;
                }
                else if (object.ReferenceEquals(this._stateManagerOwner, target))
                {
                    result = this._stateManagerComponent;
                }
                else
                {
                    this._stateManagerOwner = target;
                    this._stateManagerComponent = null;
                    this.EnsurePlayerStateManagerType();
                    if (this._playerStateManagerType == null)
                    {
                        result = null;
                    }
                    else
                    {
                        this._stateManagerComponent = (component.GetComponent(this._playerStateManagerType) ?? component.GetComponentInParent(this._playerStateManagerType));
                        result = this._stateManagerComponent;
                    }
                }
            }
            return result;
        }

        // Token: 0x06000089 RID: 137 RVA: 0x00005C10 File Offset: 0x00003E10
        private object ReadCurrentStateType(object stateManager)
        {
            object result;
            if (stateManager == null)
            {
                result = null;
            }
            else
            {
                this.EnsurePlayerStateManagerType();
                if (this._currentStateTypeProperty != null)
                {
                    try
                    {
                        return this._currentStateTypeProperty.GetValue(stateManager, null);
                    }
                    catch
                    {
                    }
                }
                object obj;
                if ((obj = GameReflection.GetProperty<object>(stateManager, "CurrentStateType")) == null)
                {
                    obj = (GameReflection.GetProperty<object>(stateManager, "CurrentState") ?? GameReflection.GetField<object>(stateManager, "currentState"));
                }
                result = obj;
            }
            return result;
        }

        // Token: 0x0600008A RID: 138 RVA: 0x00005CA0 File Offset: 0x00003EA0
        private void EnsurePlayerStateManagerType()
        {
            if (!(this._playerStateManagerType != null))
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly assembly in assemblies)
                {
                    this._playerStateManagerType = assembly.GetType("Alta.PlayerStates.PlayerStateManager");
                    if (this._playerStateManagerType != null)
                    {
                        this._currentStateTypeProperty = this._playerStateManagerType.GetProperty("CurrentStateType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        break;
                    }
                }
            }
        }

        // Token: 0x0600008B RID: 139 RVA: 0x00005D30 File Offset: 0x00003F30
        private static bool TextLooksBlockedPlayerState(string value)
        {
            return !string.IsNullOrEmpty(value) && (value.IndexOf("Dead", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("Downed", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("Spirit", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // Token: 0x0600008C RID: 140 RVA: 0x00005D88 File Offset: 0x00003F88
        public bool IsTargetHeldByOther(object target)
        {
            bool result;
            if (target == null)
            {
                result = false;
            }
            else if (this.IsTargetAttachedToRemotePlayer(target))
            {
                this.LastInteractResult = "blocked: target attached to remote player";
                result = true;
            }
            else
            {
                string[] array = new string[]
                {
                    "Holder",
                    "HeldBy",
                    "Interactor",
                    "CurrentInteractor",
                    "Grabber",
                    "Owner",
                    "Player",
                    "Controller"
                };
                for (int i = 0; i < array.Length; i++)
                {
                    object obj = GameReflection.GetProperty<object>(target, array[i]) ?? GameReflection.GetField<object>(target, array[i]);
                    if (obj != null && !this.IsLocalLike(obj))
                    {
                        this.LastInteractResult = "blocked: target held by other";
                        return true;
                    }
                }
                string[] array2 = new string[]
                {
                    "IsHeld",
                    "Held",
                    "IsGrabbed",
                    "Grabbed",
                    "InHand"
                };
                bool flag = false;
                for (int i = 0; i < array2.Length; i++)
                {
                    if (GameReflection.ReadBoolMember(target, array2[i]))
                    {
                        flag = true;
                        break;
                    }
                }
                if (!flag)
                {
                    result = false;
                }
                else
                {
                    Component component = target as Component;
                    if (component != null)
                    {
                        MonoBehaviour[] componentsInParent = component.GetComponentsInParent<MonoBehaviour>(true);
                        foreach (MonoBehaviour monoBehaviour in componentsInParent)
                        {
                            if (!(monoBehaviour == null) && !object.ReferenceEquals(monoBehaviour, target))
                            {
                                for (int i = 0; i < array.Length; i++)
                                {
                                    object obj = GameReflection.GetProperty<object>(monoBehaviour, array[i]) ?? GameReflection.GetField<object>(monoBehaviour, array[i]);
                                    if (obj != null && !this.IsLocalLike(obj))
                                    {
                                        this.LastInteractResult = "blocked: target held by other";
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                    result = false;
                }
            }
            return result;
        }

        // Token: 0x0600008D RID: 141 RVA: 0x00005FC8 File Offset: 0x000041C8
        public Transform GetInputTargetTransform(object playerInput)
        {
            return GameReflection.GetProperty<Transform>(playerInput, "TargetTransform");
        }

        // Token: 0x0600008E RID: 142 RVA: 0x00005FE8 File Offset: 0x000041E8
        public object GetRawInput(object playerInput)
        {
            return GameReflection.GetProperty<object>(playerInput, "RawInput");
        }

        // Token: 0x0600008F RID: 143 RVA: 0x00006008 File Offset: 0x00004208
        public void SetButton(object playerInput, object rawInput, string propertyName, bool value)
        {
            object property = GameReflection.GetProperty<object>(rawInput, propertyName);
            GameReflection.SetProperty(property, "State", value);
            this.InvokeInputEvent(property, playerInput);
        }

        // Token: 0x06000090 RID: 144 RVA: 0x0000603A File Offset: 0x0000423A
        public void SetRawInputVector2(object rawInput, string propertyName, Vector2 value)
        {
            GameReflection.SetProperty(rawInput, propertyName, value);
        }

        // Token: 0x06000091 RID: 145 RVA: 0x0000604B File Offset: 0x0000424B
        public void SetRawInputVector3(object rawInput, string propertyName, Vector3 value)
        {
            GameReflection.SetProperty(rawInput, propertyName, value);
        }

        // Token: 0x06000092 RID: 146 RVA: 0x0000605C File Offset: 0x0000425C
        public void SetRawInputFloat(object rawInput, string propertyName, float value)
        {
            GameReflection.SetProperty(rawInput, propertyName, value);
        }

        // Token: 0x06000093 RID: 147 RVA: 0x00006070 File Offset: 0x00004270
        public object FindInteractorForInput(object playerInput)
        {
            this.LastInteractorName = "none";
            object result;
            if (playerInput == null)
            {
                result = null;
            }
            else
            {
                Transform inputTargetTransform = this.GetInputTargetTransform(playerInput);
                object obj = null;
                float num = float.MaxValue;
                MonoBehaviour[] array = Object.FindObjectsOfType<MonoBehaviour>();
                foreach (MonoBehaviour monoBehaviour in array)
                {
                    if (!(monoBehaviour == null) && !(monoBehaviour.GetType().Name != "Interactor"))
                    {
                        object property = GameReflection.GetProperty<object>(monoBehaviour, "Input");
                        if (object.ReferenceEquals(property, playerInput))
                        {
                            this.LastInteractorName = monoBehaviour.name;
                            return monoBehaviour;
                        }
                        object property2 = GameReflection.GetProperty<object>(monoBehaviour, "Controller");
                        object property3 = GameReflection.GetProperty<object>(property2, "PlayerInput");
                        if (object.ReferenceEquals(property3, playerInput))
                        {
                            this.LastInteractorName = monoBehaviour.name;
                            return monoBehaviour;
                        }
                        if (inputTargetTransform != null)
                        {
                            float sqrMagnitude = (monoBehaviour.transform.position - inputTargetTransform.position).sqrMagnitude;
                            if (sqrMagnitude < num)
                            {
                                num = sqrMagnitude;
                                obj = monoBehaviour;
                            }
                        }
                    }
                }
                if (obj != null && num < 4f)
                {
                    this.LastInteractorName = ((Component)obj).name + " (nearest)";
                    result = obj;
                }
                else
                {
                    object obj2 = this.FindLocalController();
                    if (obj2 != null)
                    {
                        object property4 = GameReflection.GetProperty<object>(playerInput, "HandIndex");
                        string text = (property4 == null) ? string.Empty : property4.ToString().ToLowerInvariant();
                        string text2 = text.Contains("left") ? "LeftController" : "RightController";
                        object property5 = GameReflection.GetProperty<object>(obj2, text2);
                        object property6 = GameReflection.GetProperty<object>(property5, "Interactor");
                        if (property6 != null)
                        {
                            this.LastInteractorName = text2 + ".Interactor";
                            return property6;
                        }
                    }
                    result = null;
                }
            }
            return result;
        }

        // Token: 0x06000094 RID: 148 RVA: 0x000062BC File Offset: 0x000044BC
        public bool IsObjectAlive(object value)
        {
            bool result;
            if (value == null)
            {
                result = false;
            }
            else
            {
                Object @object = value as Object;
                result = (@object != null || !(value is Object));
            }
            return result;
        }

        // Token: 0x06000095 RID: 149 RVA: 0x00006304 File Offset: 0x00004504
        public bool TryStartInteract(object interactor, object interactable)
        {
            bool result;
            if (interactor == null || interactable == null)
            {
                this.LastInteractResult = "start failed: missing " + ((interactor == null) ? "interactor" : "interactable");
                result = false;
            }
            else
            {
                if (this._startInteractMethod == null || this._startInteractMethod.DeclaringType != interactor.GetType())
                {
                    this._startInteractMethod = interactor.GetType().GetMethod("StartInteract", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                if (this._startInteractMethod == null)
                {
                    this.LastInteractResult = "start failed: no StartInteract";
                    result = false;
                }
                else
                {
                    try
                    {
                        this._startInteractMethod.Invoke(interactor, new object[]
                        {
                            interactable,
                            false,
                            true,
                            true
                        });
                        this.LastInteractResult = "start called";
                        result = true;
                    }
                    catch
                    {
                        this.LastInteractResult = "start threw";
                        result = false;
                    }
                }
            }
            return result;
        }

        // Token: 0x06000096 RID: 150 RVA: 0x00006424 File Offset: 0x00004624
        public bool TryStopInteract(object interactor)
        {
            bool result;
            if (interactor == null)
            {
                result = false;
            }
            else
            {
                if (this._stopInteractMethod == null || this._stopInteractMethod.DeclaringType != interactor.GetType())
                {
                    this._stopInteractMethod = interactor.GetType().GetMethod("StopInteract", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                if (this._stopInteractMethod == null)
                {
                    result = false;
                }
                else
                {
                    try
                    {
                        this._stopInteractMethod.Invoke(interactor, new object[]
                        {
                            false,
                            true
                        });
                        this.LastInteractResult = "stop called";
                        result = true;
                    }
                    catch
                    {
                        this.LastInteractResult = "stop threw";
                        result = false;
                    }
                }
            }
            return result;
        }

        // Token: 0x06000097 RID: 151 RVA: 0x00006504 File Offset: 0x00004704
        public bool TryReleaseHeldHands(object player)
        {
            bool flag = false;
            bool result;
            if (player == null)
            {
                result = false;
            }
            else
            {
                object leftInput = this.GetLeftInput(player);
                object rightInput = this.GetRightInput(player);
                object obj = this.FindInteractorForInput(leftInput);
                if (obj != null && this.TryStopInteract(obj))
                {
                    flag = true;
                }
                object obj2 = this.FindInteractorForInput(rightInput);
                if (obj2 != null && this.TryStopInteract(obj2))
                {
                    flag = true;
                }
                if (flag)
                {
                    this.LastInteractResult = "held hands released";
                }
                result = flag;
            }
            return result;
        }

        // Token: 0x06000098 RID: 152 RVA: 0x000065A0 File Offset: 0x000047A0
        public bool IsInteractorInteracting(object interactor)
        {
            bool result;
            if (interactor == null)
            {
                result = false;
            }
            else
            {
                object property = GameReflection.GetProperty<object>(interactor, "IsInteracting");
                result = (property is bool && (bool)property);
            }
            return result;
        }

        // Token: 0x06000099 RID: 153 RVA: 0x000065E0 File Offset: 0x000047E0
        public bool TryPickupGrab(object interactor, object pickup)
        {
            bool result;
            if (interactor == null || pickup == null)
            {
                this.LastInteractResult = "pickup grab failed: missing " + ((interactor == null) ? "interactor" : "pickup");
                result = false;
            }
            else if (this.IsInteractorInteracting(interactor))
            {
                this.TryStopInteract(interactor);
                this.LastInteractResult = "pickup grab toggled stop";
                result = true;
            }
            else
            {
                this.TryUndockPickup(pickup, interactor);
                this.TryResetTimeout(interactor);
                if (this._startInteractPickupMethod == null || this._startInteractPickupMethod.DeclaringType != interactor.GetType())
                {
                    this._startInteractPickupMethod = interactor.GetType().GetMethod("StartInteract", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                if (this._startInteractPickupMethod == null)
                {
                    this.LastInteractResult = "pickup grab failed: no StartInteract";
                    result = false;
                }
                else
                {
                    try
                    {
                        this._startInteractPickupMethod.Invoke(interactor, new object[]
                        {
                            pickup,
                            false,
                            false,
                            false
                        });
                        this.LastInteractResult = "pickup StartInteract called";
                        result = true;
                    }
                    catch
                    {
                        this.LastInteractResult = "pickup StartInteract threw";
                        result = false;
                    }
                }
            }
            return result;
        }

        // Token: 0x0600009A RID: 154 RVA: 0x0000673C File Offset: 0x0000493C
        private void TryUndockPickup(object pickup, object interactor)
        {
            object property = GameReflection.GetProperty<object>(pickup, "IsDocked");
            if (property is bool && (bool)property)
            {
                MethodInfo method = pickup.GetType().GetMethod("Undock", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (!(method == null))
                {
                    try
                    {
                        method.Invoke(pickup, new object[]
                        {
                            interactor,
                            true,
                            1
                        });
                    }
                    catch
                    {
                    }
                }
            }
        }

        // Token: 0x0600009B RID: 155 RVA: 0x000067D4 File Offset: 0x000049D4
        private void TryResetTimeout(object interactor)
        {
            MethodInfo method = interactor.GetType().GetMethod("ResetTimeout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (!(method == null))
            {
                try
                {
                    method.Invoke(interactor, null);
                }
                catch
                {
                }
            }
        }

        // Token: 0x0600009C RID: 156 RVA: 0x0000682C File Offset: 0x00004A2C
        public bool TryTestGrab(object interactable, bool isLeft)
        {
            bool result;
            if (interactable == null)
            {
                this.LastInteractResult = "test grab failed: missing interactable";
                result = false;
            }
            else
            {
                MethodInfo testGrabMethod = this.GetTestGrabMethod(interactable.GetType(), isLeft, false);
                if (testGrabMethod == null)
                {
                    this.LastInteractResult = "test grab failed: no method";
                    result = false;
                }
                else
                {
                    try
                    {
                        testGrabMethod.Invoke(interactable, null);
                        this.LastInteractResult = (isLeft ? "TestLeftHand called" : "TestRightHand called");
                        result = true;
                    }
                    catch
                    {
                        this.LastInteractResult = "test grab threw";
                        result = false;
                    }
                }
            }
            return result;
        }

        // Token: 0x0600009D RID: 157 RVA: 0x000068D0 File Offset: 0x00004AD0
        public bool TryTestGrabEnd(object interactable, bool isLeft)
        {
            bool result;
            if (interactable == null)
            {
                result = false;
            }
            else
            {
                MethodInfo testGrabMethod = this.GetTestGrabMethod(interactable.GetType(), isLeft, true);
                if (testGrabMethod == null)
                {
                    result = false;
                }
                else
                {
                    try
                    {
                        testGrabMethod.Invoke(interactable, null);
                        this.LastInteractResult = (isLeft ? "TestLeftHandEnd called" : "TestRightHandEnd called");
                        result = true;
                    }
                    catch
                    {
                        this.LastInteractResult = "test grab end threw";
                        result = false;
                    }
                }
            }
            return result;
        }

        // Token: 0x0600009E RID: 158 RVA: 0x0000695C File Offset: 0x00004B5C
        private MethodInfo GetTestGrabMethod(Type type, bool isLeft, bool isEnd)
        {
            MethodInfo result;
            if (isLeft && !isEnd)
            {
                MethodInfo methodInfo;
                if ((methodInfo = this._testLeftHandMethod) == null)
                {
                    methodInfo = (this._testLeftHandMethod = type.GetMethod("TestLeftHand", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
                }
                result = methodInfo;
            }
            else if (!isLeft && !isEnd)
            {
                MethodInfo methodInfo2;
                if ((methodInfo2 = this._testRightHandMethod) == null)
                {
                    methodInfo2 = (this._testRightHandMethod = type.GetMethod("TestRightHand", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
                }
                result = methodInfo2;
            }
            else if (isLeft)
            {
                MethodInfo methodInfo3;
                if ((methodInfo3 = this._testLeftHandEndMethod) == null)
                {
                    methodInfo3 = (this._testLeftHandEndMethod = type.GetMethod("TestLeftHandEnd", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
                }
                result = methodInfo3;
            }
            else
            {
                MethodInfo methodInfo4;
                if ((methodInfo4 = this._testRightHandEndMethod) == null)
                {
                    methodInfo4 = (this._testRightHandEndMethod = type.GetMethod("TestRightHandEnd", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
                }
                result = methodInfo4;
            }
            return result;
        }

        // Token: 0x0600009F RID: 159 RVA: 0x00006A1C File Offset: 0x00004C1C
        public bool TryAdjustMenuWheel(object menuTarget, float direction)
        {
            bool result;
            if (menuTarget == null)
            {
                this.LastInteractResult = "menu wheel failed: missing target";
                result = false;
            }
            else
            {
                Type type = menuTarget.GetType();
                string name = type.Name;
                if (name == "CaptainsWheel" && GameReflection.TryAdjustFloatProperty(menuTarget, "Value", direction * 0.2f))
                {
                    this.LastInteractResult = "CaptainsWheel.Value adjusted";
                    result = true;
                }
                else if (name == "WheelGrab" && GameReflection.TryAdjustFloatProperty(menuTarget, "Progress", direction * 0.2f))
                {
                    this.LastInteractResult = "WheelGrab.Progress adjusted";
                    result = true;
                }
                else
                {
                    object obj = (name == "ServerSelectionMenu") ? menuTarget : GameReflection.FindParentComponent(menuTarget as Component, "ServerSelectionMenu");
                    if (obj != null && GameReflection.TryCallScrollServerListFromWheel(obj, direction))
                    {
                        this.LastInteractResult = "ServerSelectionMenu wheel scroll called";
                        result = true;
                    }
                    else
                    {
                        this.LastInteractResult = "menu wheel failed: " + name;
                        result = false;
                    }
                }
            }
            return result;
        }

        // Token: 0x060000A0 RID: 160 RVA: 0x00006B38 File Offset: 0x00004D38
        public bool TryReturnToMainMenu()
        {
            Type type = GameReflection.FindType("GameModeManager");
            if (type != null)
            {
                MethodInfo method = type.GetMethod("StopCurrentModeAsync", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null)
                {
                    try
                    {
                        method.Invoke(null, new object[]
                        {
                            "return to menu",
                            true
                        });
                        this.LastInteractResult = "GameModeManager.StopCurrentModeAsync called";
                        return true;
                    }
                    catch
                    {
                    }
                }
            }
            string[] array = new string[]
            {
                "Disconnect",
                "LeaveServer",
                "ReturnToMainMenu",
                "QuitToMainMenu",
                "BackToMenu",
                "LoadMainMenu"
            };
            MonoBehaviour[] array2 = Object.FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour monoBehaviour in array2)
            {
                if (!(monoBehaviour == null))
                {
                    string[] array4 = array;
                    foreach (string text in array4)
                    {
                        MethodInfo method2 = monoBehaviour.GetType().GetMethod(text, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (!(method2 == null) && method2.GetParameters().Length == 0)
                        {
                            try
                            {
                                method2.Invoke(monoBehaviour, null);
                                this.LastInteractResult = text + " called";
                                return true;
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
            this.LastInteractResult = "disconnect failed: no main-menu method";
            return false;
        }

        // Token: 0x060000A1 RID: 161 RVA: 0x00006D08 File Offset: 0x00004F08
        public bool TryEscapeCustomization()
        {
            string[] methodNames = new string[]
            {
                "OnEndButtonPressed",
                "EndButtonPressed",
                "PressEndButton",
                "FinishCustomization",
                "CompleteCustomization",
                "ConfirmCustomization",
                "SaveAndExit",
                "ApplyAndExit",
                "Done",
                "Finish"
            };
            MonoBehaviour[] array = Object.FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour monoBehaviour in array)
            {
                if (!(monoBehaviour == null) && GameReflection.TypeMatchesAny(monoBehaviour.GetType(), new string[]
                {
                    "CosmeticCustomizationManager",
                    "CustomizationRoom",
                    "CustomizationManager"
                }))
                {
                    bool result;
                    if (this.TryRaiseEventField(monoBehaviour, "EndButtonPressed"))
                    {
                        result = true;
                    }
                    else
                    {
                        if (!this.TryInvokeFirstNoArgMethod(monoBehaviour, methodNames))
                        {
                            goto IL_F8;
                        }
                        result = true;
                    }
                    return result;
                }
            IL_F8:;
            }
            this.LastInteractResult = "customization escape failed: EndButtonPressed not found";
            return false;
        }

        // Token: 0x060000A2 RID: 162 RVA: 0x00006E38 File Offset: 0x00005038
        public bool TryToggleSceneBoolean(string[] typeNameHints, string[] memberNames)
        {
            MonoBehaviour[] array = Object.FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour monoBehaviour in array)
            {
                if (!(monoBehaviour == null) && GameReflection.TypeMatchesAny(monoBehaviour.GetType(), typeNameHints) && GameReflection.TryToggleBooleanMember(monoBehaviour, memberNames))
                {
                    this.LastInteractResult = monoBehaviour.GetType().Name + " toggled";
                    return true;
                }
            }
            this.LastInteractResult = "toggle failed: no matching scene member";
            return false;
        }

        // Token: 0x060000A3 RID: 163 RVA: 0x00006ECC File Offset: 0x000050CC
        public bool TryInvokeSceneMethod(string[] typeNameHints, string[] methodNames)
        {
            MonoBehaviour[] array = Object.FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour monoBehaviour in array)
            {
                if (!(monoBehaviour == null) && GameReflection.TypeMatchesAny(monoBehaviour.GetType(), typeNameHints))
                {
                    Type type = monoBehaviour.GetType();
                    foreach (string text in methodNames)
                    {
                        MethodInfo method = type.GetMethod(text, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (!(method == null) && method.GetParameters().Length == 0)
                        {
                            try
                            {
                                method.Invoke(monoBehaviour, null);
                                this.LastInteractResult = type.Name + "." + text + " called";
                                return true;
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
            this.LastInteractResult = "invoke failed: no matching scene method";
            return false;
        }

        // Token: 0x060000A4 RID: 164 RVA: 0x00006FE4 File Offset: 0x000051E4
        public bool TryRunQuickAccessAction(string typeName, string assetName, string openMethodName, string runMethodName)
        {
            return this.TryRunQuickAccessAction(string.IsNullOrEmpty(typeName) ? null : new string[]
            {
                typeName
            }, string.IsNullOrEmpty(assetName) ? null : new string[]
            {
                assetName
            }, string.IsNullOrEmpty(openMethodName) ? null : new string[]
            {
                openMethodName
            }, string.IsNullOrEmpty(runMethodName) ? null : new string[]
            {
                runMethodName
            });
        }

        // Token: 0x060000A5 RID: 165 RVA: 0x00007060 File Offset: 0x00005260
        public bool TryRunQuickAccessAction(string[] typeNameHints, string[] assetNameHints, string[] openMethodNames, string[] runMethodNames)
        {
            object obj = GameReflection.FindResourceObject(typeNameHints, assetNameHints);
            bool result;
            if (obj == null)
            {
                this.LastInteractResult = "quick access failed: " + GameReflection.DescribeHints(typeNameHints, assetNameHints) + " not found";
                result = false;
            }
            else
            {
                if (openMethodNames != null)
                {
                    for (int i = 0; i < openMethodNames.Length; i++)
                    {
                        GameReflection.InvokeIfExists(obj, openMethodNames[i], null);
                    }
                }
                if (runMethodNames != null)
                {
                    foreach (string text in runMethodNames)
                    {
                        object[] args = new object[1];
                        if (GameReflection.InvokeIfExists(obj, text, args))
                        {
                            this.LastInteractResult = obj.GetType().Name + "." + text + " called";
                            return true;
                        }
                        if (GameReflection.InvokeIfExists(obj, text, null))
                        {
                            this.LastInteractResult = obj.GetType().Name + "." + text + " called";
                            return true;
                        }
                    }
                }
                this.LastInteractResult = "quick access failed: " + obj.GetType().Name;
                result = false;
            }
            return result;
        }

        // Token: 0x060000A6 RID: 166 RVA: 0x000071B0 File Offset: 0x000053B0
        public bool TrySetShowNames(bool enabled)
        {
            object obj = GameReflection.FindResourceObject("ShowNames", null);
            bool result;
            if (obj == null)
            {
                this.LastInteractResult = "ShowNames asset not found";
                result = false;
            }
            else
            {
                MethodInfo method = obj.GetType().GetMethod("Activate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null)
                {
                    this.LastInteractResult = "ShowNames.Activate not found";
                    result = false;
                }
                else
                {
                    try
                    {
                        method.Invoke(obj, new object[]
                        {
                            enabled
                        });
                        this.LastInteractResult = "ShowNames set to " + enabled;
                        result = true;
                    }
                    catch
                    {
                        this.LastInteractResult = "ShowNames activate threw";
                        result = false;
                    }
                }
            }
            return result;
        }

        // Token: 0x060000A7 RID: 167 RVA: 0x00007274 File Offset: 0x00005474
        public bool TryTogglePlayerBag(Transform cameraTransform)
        {
            bool result;
            if (cameraTransform == null)
            {
                this.LastInteractResult = "bag toggle failed: missing camera";
                result = false;
            }
            else
            {
                Transform localBagTransform = this.GetLocalBagTransform();
                if (localBagTransform == null)
                {
                    this.LastInteractResult = "bag toggle failed: local bag not found";
                    result = false;
                }
                else
                {
                    Transform playerRootTransform = this.GetPlayerRootTransform(this.FindLocalPlayer());
                    if (playerRootTransform == null)
                    {
                        this.LastInteractResult = "bag toggle failed: local player root not found";
                        result = false;
                    }
                    else
                    {
                        Transform transform = localBagTransform.Find("Storage");
                        if (transform == null)
                        {
                            this.LastInteractResult = "bag toggle failed: storage child not found";
                            result = false;
                        }
                        else
                        {
                            int instanceID = localBagTransform.gameObject.GetInstanceID();
                            GameReflection.BagState bagState;
                            if (!this._openBags.TryGetValue(instanceID, out bagState))
                            {
                                bagState = new GameReflection.BagState();
                                bagState.Parent = localBagTransform.parent;
                                bagState.LocalPosition = localBagTransform.localPosition;
                                bagState.LocalRotation = localBagTransform.localRotation;
                                bagState.LocalScale = localBagTransform.localScale;
                                this._openBags[instanceID] = bagState;
                            }
                            if (!bagState.IsOpen)
                            {
                                transform.gameObject.SetActive(true);
                                localBagTransform.SetParent(playerRootTransform, true);
                                localBagTransform.position = cameraTransform.position + cameraTransform.forward * 0.46f + cameraTransform.up * -0.18f;
                                localBagTransform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up), Vector3.up);
                                localBagTransform.localScale = bagState.LocalScale;
                                bagState.IsOpen = true;
                                this.LastInteractResult = "bag opened";
                                result = true;
                            }
                            else
                            {
                                localBagTransform.SetParent(bagState.Parent, true);
                                localBagTransform.localPosition = bagState.LocalPosition;
                                localBagTransform.localRotation = bagState.LocalRotation;
                                localBagTransform.localScale = bagState.LocalScale;
                                transform.gameObject.SetActive(false);
                                bagState.IsOpen = false;
                                this._openBags.Remove(instanceID);
                                this.LastInteractResult = "bag closed";
                                result = true;
                            }
                        }
                    }
                }
            }
            return result;
        }

        // Token: 0x060000A8 RID: 168 RVA: 0x000074B8 File Offset: 0x000056B8
        public Transform GetLocalBagTransform()
        {
            Transform transform = this.FindLocalBag();
            Transform result;
            if (transform != null)
            {
                this._cachedLocalBagTransform = transform;
                result = transform;
            }
            else if (this._cachedLocalBagTransform != null)
            {
                result = this._cachedLocalBagTransform;
            }
            else
            {
                result = null;
            }
            return result;
        }

        // Token: 0x060000A9 RID: 169 RVA: 0x0000750C File Offset: 0x0000570C
        private static bool TryAdjustFloatProperty(object target, string propertyName, float delta)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            bool result;
            if (property == null || !property.CanRead || !property.CanWrite)
            {
                result = false;
            }
            else
            {
                try
                {
                    float num = (float)property.GetValue(target, null);
                    property.SetValue(target, num + delta, null);
                    result = true;
                }
                catch
                {
                    result = false;
                }
            }
            return result;
        }

        // Token: 0x060000AA RID: 170 RVA: 0x0000758C File Offset: 0x0000578C
        private static bool TryToggleBooleanMember(object target, string[] memberNames)
        {
            Type type = target.GetType();
            foreach (string name in memberNames)
            {
                PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.CanRead && property.CanWrite && property.PropertyType == typeof(bool))
                {
                    try
                    {
                        bool flag = (bool)property.GetValue(target, null);
                        property.SetValue(target, !flag, null);
                        return true;
                    }
                    catch
                    {
                    }
                }
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(bool))
                {
                    try
                    {
                        bool flag = (bool)field.GetValue(target);
                        field.SetValue(target, !flag);
                        return true;
                    }
                    catch
                    {
                    }
                }
            }
            return false;
        }

        // Token: 0x060000AB RID: 171 RVA: 0x000076CC File Offset: 0x000058CC
        private static bool TypeMatchesAny(Type type, string[] typeNameHints)
        {
            bool result;
            if (typeNameHints == null || typeNameHints.Length == 0)
            {
                result = true;
            }
            else
            {
                while (type != null)
                {
                    foreach (string text in typeNameHints)
                    {
                        if (!string.IsNullOrEmpty(text))
                        {
                            if (type.Name == text || type.FullName == text)
                            {
                                return true;
                            }
                            if ((type.Name != null && type.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) || (type.FullName != null && type.FullName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                return true;
                            }
                        }
                    }
                    type = type.BaseType;
                }
                result = false;
            }
            return result;
        }

        // Token: 0x060000AC RID: 172 RVA: 0x000077B8 File Offset: 0x000059B8
        private static Type FindType(string typeName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            Assembly[] array = assemblies;
            int i = 0;
            while (i < array.Length)
            {
                Assembly assembly = array[i];
                Type type = assembly.GetType(typeName);
                if (!(type != null))
                {
                    Type[] types = assembly.GetTypes();
                    foreach (Type type2 in types)
                    {
                        if (!(type2 == null) && (type2.Name == typeName || type2.FullName == typeName || (type2.Name != null && type2.Name.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) >= 0) || (type2.FullName != null && type2.FullName.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) >= 0)))
                        {
                            return type2;
                        }
                    }
                    i++;
                    continue;
                }
                return type;
            }
            return null;
        }

        // Token: 0x060000AD RID: 173 RVA: 0x000078D0 File Offset: 0x00005AD0
        private static Type FindTypeByHint(string typeNameHint)
        {
            Type result;
            if (string.IsNullOrEmpty(typeNameHint))
            {
                result = null;
            }
            else
            {
                result = GameReflection.FindType(typeNameHint);
            }
            return result;
        }

        // Token: 0x060000AE RID: 174 RVA: 0x000078FC File Offset: 0x00005AFC
        private static object FindResourceObject(string typeNameHint, string nameHint)
        {
            return GameReflection.FindResourceObject(string.IsNullOrEmpty(typeNameHint) ? null : new string[]
            {
                typeNameHint
            }, string.IsNullOrEmpty(nameHint) ? null : new string[]
            {
                nameHint
            });
        }

        // Token: 0x060000AF RID: 175 RVA: 0x00007944 File Offset: 0x00005B44
        private static object FindResourceObject(string[] typeNameHints, string[] nameHints)
        {
            Object[] array = Resources.FindObjectsOfTypeAll(typeof(Object));
            foreach (Object @object in array)
            {
                if (!(@object == null))
                {
                    Type type = @object.GetType();
                    if (GameReflection.TypeMatchesAny(type, typeNameHints) && GameReflection.NameMatchesAny(@object.name, nameHints))
                    {
                        return @object;
                    }
                }
            }
            return null;
        }

        // Token: 0x060000B0 RID: 176 RVA: 0x000079CC File Offset: 0x00005BCC
        private static bool NameMatchesAny(string value, string[] nameHints)
        {
            bool result;
            if (nameHints == null || nameHints.Length == 0)
            {
                result = true;
            }
            else
            {
                foreach (string value2 in nameHints)
                {
                    if (!string.IsNullOrEmpty(value2) && !string.IsNullOrEmpty(value) && value.IndexOf(value2, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
                result = false;
            }
            return result;
        }

        // Token: 0x060000B1 RID: 177 RVA: 0x00007A44 File Offset: 0x00005C44
        private static bool InvokeIfExists(object target, string methodName, object[] args)
        {
            bool result;
            if (target == null || string.IsNullOrEmpty(methodName))
            {
                result = false;
            }
            else
            {
                MethodInfo[] methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (MethodInfo methodInfo in methods)
                {
                    if (!(methodInfo.Name != methodName))
                    {
                        ParameterInfo[] parameters = methodInfo.GetParameters();
                        int num = (args != null) ? args.Length : 0;
                        if (parameters.Length == num)
                        {
                            try
                            {
                                methodInfo.Invoke(target, args);
                                return true;
                            }
                            catch
                            {
                            }
                        }
                    }
                }
                result = false;
            }
            return result;
        }

        // Token: 0x060000B2 RID: 178 RVA: 0x00007B0C File Offset: 0x00005D0C
        private Transform FindLocalBag()
        {
            if (this._cachedLocalBagTransform != null)
            {
                Transform cachedLocalBagTransform = this._cachedLocalBagTransform;
                Transform playerRootTransform = this.GetPlayerRootTransform(this.FindLocalPlayer());
                if (cachedLocalBagTransform != null && this.IsLocalBagTransform(cachedLocalBagTransform, playerRootTransform))
                {
                    return cachedLocalBagTransform;
                }
            }
            object player = this.FindLocalPlayer();
            Transform playerRootTransform2 = this.GetPlayerRootTransform(player);
            Object[] array = Resources.FindObjectsOfTypeAll(typeof(GameObject));
            for (int i = 0; i < array.Length; i++)
            {
                GameObject gameObject = array[i] as GameObject;
                if (!(gameObject == null) && gameObject.name.EndsWith("Bag(Clone)", StringComparison.Ordinal))
                {
                    Transform transform = gameObject.transform;
                    if (this.IsLocalBagTransform(transform, playerRootTransform2))
                    {
                        this._cachedLocalBagTransform = transform;
                        return transform;
                    }
                }
            }
            return null;
        }

        // Token: 0x060000B3 RID: 179 RVA: 0x00007C10 File Offset: 0x00005E10
        private bool IsLocalBagTransform(Transform bagTransform, Transform playerRoot)
        {
            bool result;
            if (bagTransform == null)
            {
                result = false;
            }
            else
            {
                string[] array = new string[]
                {
                    "Owner",
                    "Player",
                    "Holder",
                    "HeldBy",
                    "Controller",
                    "Interactor",
                    "CurrentInteractor"
                };
                MonoBehaviour[] componentsInParent = bagTransform.GetComponentsInParent<MonoBehaviour>(true);
                foreach (MonoBehaviour monoBehaviour in componentsInParent)
                {
                    if (!(monoBehaviour == null))
                    {
                        if (this.IsLocalLike(monoBehaviour))
                        {
                            return true;
                        }
                        for (int j = 0; j < array.Length; j++)
                        {
                            object obj = GameReflection.GetProperty<object>(monoBehaviour, array[j]) ?? GameReflection.GetField<object>(monoBehaviour, array[j]);
                            if (obj != null && this.IsLocalLike(obj))
                            {
                                return true;
                            }
                        }
                    }
                }
                result = (playerRoot != null && bagTransform.IsChildOf(playerRoot));
            }
            return result;
        }

        // Token: 0x060000B4 RID: 180 RVA: 0x00007D48 File Offset: 0x00005F48
        private bool IsLocalLike(object owner)
        {
            return this.IsLocalLike(owner, 0);
        }

        // Token: 0x060000B5 RID: 181 RVA: 0x00007D64 File Offset: 0x00005F64
        private bool IsLocalLike(object owner, int depth)
        {
            bool result;
            if (owner == null || depth > 4)
            {
                result = false;
            }
            else
            {
                object obj = this.FindLocalPlayer();
                if (object.ReferenceEquals(owner, obj))
                {
                    result = true;
                }
                else
                {
                    object objB = this.FindLocalController();
                    if (object.ReferenceEquals(owner, objB))
                    {
                        result = true;
                    }
                    else
                    {
                        object objB2 = (obj == null) ? null : this.GetLeftInput(obj);
                        object objB3 = (obj == null) ? null : this.GetRightInput(obj);
                        if (object.ReferenceEquals(owner, objB2) || object.ReferenceEquals(owner, objB3))
                        {
                            result = true;
                        }
                        else if (GameReflection.ReadBoolMember(owner, "IsLocal") || GameReflection.ReadBoolMember(owner, "IsLocalPlayer") || GameReflection.ReadBoolMember(owner, "IsOwned") || GameReflection.ReadBoolMember(owner, "IsOwner") || GameReflection.ReadBoolMember(owner, "HasAuthority") || GameReflection.ReadBoolMember(owner, "IsMine"))
                        {
                            result = true;
                        }
                        else
                        {
                            object property = GameReflection.GetProperty<object>(owner, "PlayerInput");
                            if (property != null && (object.ReferenceEquals(property, objB2) || object.ReferenceEquals(property, objB3)))
                            {
                                result = true;
                            }
                            else
                            {
                                object property2 = GameReflection.GetProperty<object>(owner, "Input");
                                if (property2 != null && (object.ReferenceEquals(property2, objB2) || object.ReferenceEquals(property2, objB3)))
                                {
                                    result = true;
                                }
                                else
                                {
                                    object property3 = GameReflection.GetProperty<object>(owner, "Controller");
                                    if (property3 != null && (object.ReferenceEquals(property3, objB) || this.IsLocalLike(property3, depth + 1)))
                                    {
                                        result = true;
                                    }
                                    else
                                    {
                                        object property4 = GameReflection.GetProperty<object>(owner, "Player");
                                        if (property4 != null && (object.ReferenceEquals(property4, obj) || this.IsLocalLike(property4, depth + 1)))
                                        {
                                            result = true;
                                        }
                                        else
                                        {
                                            object property5 = GameReflection.GetProperty<object>(owner, "Holder");
                                            result = (property5 != null && this.IsLocalLike(property5, depth + 1));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }

        // Token: 0x060000B6 RID: 182 RVA: 0x00007FA0 File Offset: 0x000061A0
        private bool IsTargetAttachedToRemotePlayer(object target)
        {
            Component component = target as Component;
            bool result;
            if (component == null)
            {
                result = false;
            }
            else
            {
                object player = this.FindLocalPlayer();
                Transform playerRootTransform = this.GetPlayerRootTransform(player);
                object controller = this.FindLocalController();
                Transform controllerTransform = this.GetControllerTransform(controller);
                Transform transform = component.transform;
                while (transform != null)
                {
                    if (playerRootTransform != null && object.ReferenceEquals(transform, playerRootTransform))
                    {
                        return false;
                    }
                    if (controllerTransform != null && object.ReferenceEquals(transform, controllerTransform))
                    {
                        return false;
                    }
                    MonoBehaviour[] components = transform.GetComponents<MonoBehaviour>();
                    foreach (MonoBehaviour monoBehaviour in components)
                    {
                        if (!(monoBehaviour == null))
                        {
                            if (this.IsPlayablePlayerCandidate(monoBehaviour) && !this.IsLocalLike(monoBehaviour))
                            {
                                return true;
                            }
                            if (monoBehaviour.GetType().Name == "PlayerController" && !this.IsLocalLike(monoBehaviour))
                            {
                                return true;
                            }
                        }
                    }
                    transform = transform.parent;
                }
                result = false;
            }
            return result;
        }

        // Token: 0x060000B7 RID: 183 RVA: 0x00008100 File Offset: 0x00006300
        private static string DescribeHints(string[] typeNameHints, string[] nameHints)
        {
            string result;
            if (typeNameHints != null && typeNameHints.Length > 0 && !string.IsNullOrEmpty(typeNameHints[0]))
            {
                result = typeNameHints[0];
            }
            else if (nameHints != null && nameHints.Length > 0 && !string.IsNullOrEmpty(nameHints[0]))
            {
                result = nameHints[0];
            }
            else
            {
                result = "target";
            }
            return result;
        }

        // Token: 0x060000B8 RID: 184 RVA: 0x00008158 File Offset: 0x00006358
        private static bool TryCallScrollServerListFromWheel(object menu, float direction)
        {
            MethodInfo method = menu.GetType().GetMethod("ScrollServerListFromWheel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            bool result;
            if (method == null)
            {
                result = false;
            }
            else
            {
                try
                {
                    method.Invoke(menu, new object[]
                    {
                        0f,
                        direction * 0.2f
                    });
                    result = true;
                }
                catch
                {
                    result = false;
                }
            }
            return result;
        }

        // Token: 0x060000B9 RID: 185 RVA: 0x000081D8 File Offset: 0x000063D8
        private static object FindParentComponent(Component component, string typeName)
        {
            object result;
            if (component == null)
            {
                result = null;
            }
            else
            {
                MonoBehaviour[] componentsInParent = component.GetComponentsInParent<MonoBehaviour>(true);
                foreach (MonoBehaviour monoBehaviour in componentsInParent)
                {
                    if (monoBehaviour != null && monoBehaviour.GetType().Name == typeName)
                    {
                        return monoBehaviour;
                    }
                }
                result = null;
            }
            return result;
        }
        // Dedicated finder for SmoothLocomotion specifically - deliberately does NOT reuse
        // FindLocomotionController, since that prefers "PlayerLocomotionController" (a wrapper with
        // no IsDebugMovement/ToggleDebug members) and only falls back to SmoothLocomotion as a last
        // resort. Using that here meant TrySetDebugMovement was silently no-oping against the wrong
        // component - property/method lookups came back null every time, gravity never actually
        // turned off, and direct position writes in MovePlayer got fought back down every frame.
        private object _debugMovementLocomotion;

        private object FindSmoothLocomotion(object player)
        {
            Component cached = this._debugMovementLocomotion as Component;
            if (GameReflection.IsUsableComponent(cached))
            {
                return this._debugMovementLocomotion;
            }
            this._debugMovementLocomotion = null;
            Transform playerTransform = this.GetPlayerTransform(player);
            if (playerTransform != null)
            {
                MonoBehaviour[] children = playerTransform.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (MonoBehaviour mb in children)
                {
                    if (mb != null && mb.GetType().Name == "SmoothLocomotion")
                    {
                        this._debugMovementLocomotion = mb;
                        return mb;
                    }
                }
            }
            // Fallback: scene-wide search, filtered to avoid grabbing a remote player's instance.
            MonoBehaviour[] all = Object.FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour mb in all)
            {
                if (mb != null && mb.GetType().Name == "SmoothLocomotion" && this.IsLikelyActiveLocomotion(mb))
                {
                    this._debugMovementLocomotion = mb;
                    return mb;
                }
            }
            return null;
        }

        private PropertyInfo _isDebugMovementProperty;
        private MethodInfo _toggleDebugMethod;
        private Type _debugMovementLocomotionType;

        internal bool TrySetDebugMovement(object player, bool enabled)
        {
            object locomotion = this.FindSmoothLocomotion(player);
            if (locomotion == null)
            {
                return false;
            }
            Type type = locomotion.GetType();
            if (this._debugMovementLocomotionType != type)
            {
                this._debugMovementLocomotionType = type;
                this._isDebugMovementProperty = type.GetProperty("IsDebugMovement", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                this._toggleDebugMethod = type.GetMethod("ToggleDebug", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            if (this._isDebugMovementProperty == null || this._toggleDebugMethod == null)
            {
                return false;
            }
            try
            {
                bool current = (bool)this._isDebugMovementProperty.GetValue(locomotion, null);
                if (current != enabled)
                {
                    this._toggleDebugMethod.Invoke(locomotion, null);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        // Token: 0x060000BA RID: 186 RVA: 0x00008254 File Offset: 0x00006454
        private object FindLocomotionController(object player)
        {
            Component component = this._locomotionController as Component;
            object result;
            if (this.IsUsableLocomotionController(component, player))
            {
                result = this._locomotionController;
            }
            else
            {
                this._locomotionController = null;
                this._locomotionTranslateMethod = null;
                Transform playerTransform = this.GetPlayerTransform(player);
                if (playerTransform == null)
                {
                    result = null;
                }
                else
                {
                    MonoBehaviour[] array = playerTransform.GetComponentsInChildren<MonoBehaviour>(true);
                    foreach (MonoBehaviour monoBehaviour in array)
                    {
                        if (monoBehaviour != null && monoBehaviour.GetType().Name == "PlayerLocomotionController")
                        {
                            this._locomotionController = monoBehaviour;
                            return monoBehaviour;
                        }
                    }
                    array = Object.FindObjectsOfType<MonoBehaviour>();
                    foreach (MonoBehaviour monoBehaviour in array)
                    {
                        if (monoBehaviour != null && (monoBehaviour.GetType().Name == "PlayerLocomotionController" || monoBehaviour.GetType().Name == "SmoothLocomotion") && this.IsLikelyActiveLocomotion(monoBehaviour))
                        {
                            this._locomotionController = monoBehaviour;
                            return monoBehaviour;
                        }
                    }
                    result = null;
                }
            }
            return result;
        }

        // Token: 0x060000BB RID: 187 RVA: 0x000083B0 File Offset: 0x000065B0
        private Transform GetSmoothLocomotionHead()
        {
            MonoBehaviour[] array = Object.FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour monoBehaviour in array)
            {
                if (!(monoBehaviour == null) && !(monoBehaviour.GetType().Name != "SmoothLocomotion") && this.IsLikelyActiveLocomotion(monoBehaviour))
                {
                    object obj = GameReflection.GetProperty<object>(monoBehaviour, "Character") ?? GameReflection.GetField<object>(monoBehaviour, "Character");
                    Component component = obj as Component;
                    if (!(component == null))
                    {
                        Transform transform = component.transform.Find("Relative Controllers");
                        Transform result;
                        if (transform != null && transform.childCount > 2)
                        {
                            result = transform.GetChild(2);
                        }
                        else
                        {
                            Camera componentInChildren = component.GetComponentInChildren<Camera>(true);
                            if (!(componentInChildren != null))
                            {
                                goto IL_ED;
                            }
                            result = componentInChildren.transform;
                        }
                        return result;
                    }
                }
            IL_ED:;
            }
            return null;
        }

        // Token: 0x060000BC RID: 188 RVA: 0x000084CC File Offset: 0x000066CC
        private bool IsLikelyActiveLocomotion(object locomotion)
        {
            Component component = locomotion as Component;
            bool result;
            if (!GameReflection.IsUsableComponent(component))
            {
                result = false;
            }
            else
            {
                object obj = GameReflection.GetProperty<object>(locomotion, "IsEnabled") ?? GameReflection.GetField<object>(locomotion, "IsEnabled");
                if (obj is bool)
                {
                    result = (bool)obj;
                }
                else
                {
                    Behaviour behaviour = component as Behaviour;
                    result = (behaviour == null || behaviour.enabled);
                }
            }
            return result;
        }

        // Token: 0x060000BD RID: 189 RVA: 0x00008548 File Offset: 0x00006748
        private bool IsUsablePlayerInput(object input)
        {
            Component component = input as Component;
            return GameReflection.IsUsableComponent(component) && this.GetInputTargetTransform(input) != null;
        }

        // Token: 0x060000BE RID: 190 RVA: 0x0000857C File Offset: 0x0000677C
        private bool IsUsableLocomotionController(Component component, object player)
        {
            bool result;
            if (!GameReflection.IsUsableComponent(component))
            {
                result = false;
            }
            else
            {
                Transform playerTransform = this.GetPlayerTransform(player);
                if (playerTransform == null)
                {
                    result = true;
                }
                else
                {
                    Transform transform = (playerTransform.root != null) ? playerTransform.root : playerTransform;
                    result = (component.transform == playerTransform || component.transform.IsChildOf(playerTransform) || component.transform == transform || component.transform.IsChildOf(transform));
                }
            }
            return result;
        }

        // Token: 0x060000BF RID: 191 RVA: 0x0000860C File Offset: 0x0000680C
        private static bool IsUsableComponent(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        // Token: 0x060000C0 RID: 192 RVA: 0x00008644 File Offset: 0x00006844
        private static bool TextMatchesAny(string value, string[] hints)
        {
            bool result;
            if (string.IsNullOrEmpty(value) || hints == null)
            {
                result = false;
            }
            else
            {
                foreach (string value2 in hints)
                {
                    if (!string.IsNullOrEmpty(value2) && value.IndexOf(value2, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
                result = false;
            }
            return result;
        }

        // Token: 0x060000C1 RID: 193 RVA: 0x000086B4 File Offset: 0x000068B4
        private bool TryInvokeFirstNoArgMethod(object target, string[] methodNames)
        {
            bool result;
            if (target == null || methodNames == null)
            {
                result = false;
            }
            else
            {
                Type type = target.GetType();
                foreach (string text in methodNames)
                {
                    MethodInfo method = type.GetMethod(text, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (!(method == null) && method.GetParameters().Length == 0)
                    {
                        try
                        {
                            method.Invoke(target, null);
                            this.LastInteractResult = type.Name + "." + text + " called";
                            return true;
                        }
                        catch
                        {
                        }
                    }
                }
                result = false;
            }
            return result;
        }

        // Token: 0x060000C2 RID: 194 RVA: 0x0000877C File Offset: 0x0000697C
        private bool TryRaiseEventField(object target, string eventName)
        {
            bool result;
            if (target == null || string.IsNullOrEmpty(eventName))
            {
                result = false;
            }
            else
            {
                Type type = target.GetType();
                while (type != null)
                {
                    FieldInfo field = type.GetField(eventName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field != null)
                    {
                        try
                        {
                            Delegate @delegate = field.GetValue(target) as Delegate;
                            if (@delegate != null)
                            {
                                @delegate.DynamicInvoke(null);
                                this.LastInteractResult = target.GetType().Name + "." + eventName + " raised";
                                return true;
                            }
                        }
                        catch
                        {
                        }
                    }
                    type = type.BaseType;
                }
                result = false;
            }
            return result;
        }

        // Token: 0x060000C3 RID: 195 RVA: 0x0000884C File Offset: 0x00006A4C
        private bool TryTranslateWithCharacterController(object player, Vector3 translation)
        {
            Component component = player as Component;
            bool result;
            if (component == null)
            {
                result = false;
            }
            else
            {
                Component component2 = component.GetComponent("CharacterController");
                if (component2 != null)
                {
                    try
                    {
                        component2.GetType().GetMethod("Move", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[]
                        {
                            typeof(Vector3)
                        }, null).Invoke(component2, new object[]
                        {
                            translation
                        });
                        this.LastInteractResult = "character controller moved";
                        return true;
                    }
                    catch
                    {
                    }
                }
                Component component3 = component.GetComponent("Rigidbody");
                if (component3 != null)
                {
                    try
                    {
                        MethodInfo method = component3.GetType().GetMethod("MovePosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[]
                        {
                            typeof(Vector3)
                        }, null);
                        if (method != null)
                        {
                            PropertyInfo property = component3.GetType().GetProperty("position", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            Vector3 vector = (property == null) ? component.transform.position : ((Vector3)property.GetValue(component3, null));
                            method.Invoke(component3, new object[]
                            {
                                vector + translation
                            });
                        }
                        this.LastInteractResult = "rigidbody moved";
                        return true;
                    }
                    catch
                    {
                    }
                }
                result = false;
            }
            return result;
        }

        // Token: 0x060000C4 RID: 196 RVA: 0x000089F8 File Offset: 0x00006BF8
        private object FindScenePlayer()
        {
            object result;
            if (this._playerType == null)
            {
                result = null;
            }
            else
            {
                object obj = null;
                MonoBehaviour[] array = Object.FindObjectsOfType<MonoBehaviour>();
                foreach (MonoBehaviour monoBehaviour in array)
                {
                    if (!(monoBehaviour == null) && !(monoBehaviour.GetType() != this._playerType))
                    {
                        if (this.IsPlayableLocalPlayer(monoBehaviour))
                        {
                            return monoBehaviour;
                        }
                        if (obj == null && this.IsPlayablePlayerCandidate(monoBehaviour))
                        {
                            obj = monoBehaviour;
                        }
                    }
                }
                result = obj;
            }
            return result;
        }

        // Token: 0x060000C5 RID: 197 RVA: 0x00008AAC File Offset: 0x00006CAC
        private object FindSceneController()
        {
            this.EnsurePlayerControllerType();
            object result;
            if (this._playerControllerType == null)
            {
                result = null;
            }
            else
            {
                MonoBehaviour[] array = Object.FindObjectsOfType<MonoBehaviour>();
                foreach (MonoBehaviour monoBehaviour in array)
                {
                    if (monoBehaviour != null && monoBehaviour.GetType() == this._playerControllerType)
                    {
                        return monoBehaviour;
                    }
                }
                result = null;
            }
            return result;
        }

        // Token: 0x060000C6 RID: 198 RVA: 0x00008B34 File Offset: 0x00006D34
        private bool IsPlayableLocalPlayer(object player)
        {
            return player != null && (GameReflection.ReadBoolMember(player, "IsLocal") || GameReflection.ReadBoolMember(player, "IsLocalPlayer") || GameReflection.ReadBoolMember(player, "IsOwned") || GameReflection.ReadBoolMember(player, "IsOwner") || GameReflection.ReadBoolMember(player, "HasAuthority") || GameReflection.ReadBoolMember(player, "IsMine") || this.IsPlayablePlayerCandidate(player));
        }

        // Token: 0x060000C7 RID: 199 RVA: 0x00008BBC File Offset: 0x00006DBC
        private bool IsPlayablePlayerCandidate(object player)
        {
            bool result;
            if (player == null)
            {
                result = false;
            }
            else
            {
                Transform playerTransform;
                try
                {
                    playerTransform = this.GetPlayerTransform(player);
                }
                catch
                {
                    return false;
                }
                if (playerTransform == null)
                {
                    result = false;
                }
                else
                {
                    object leftInput = this.GetLeftInput(player);
                    object rightInput = this.GetRightInput(player);
                    result = (leftInput != null || rightInput != null || this.FindLocomotionController(player) != null);
                }
            }
            return result;
        }

        // Token: 0x060000C8 RID: 200 RVA: 0x00008C4C File Offset: 0x00006E4C
        private static bool ReadBoolMember(object instance, string name)
        {
            bool result;
            if (instance == null)
            {
                result = false;
            }
            else
            {
                Type type = instance.GetType();
                PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                {
                    try
                    {
                        object value = property.GetValue(instance, null);
                        if (value is bool)
                        {
                            return (bool)value;
                        }
                    }
                    catch
                    {
                    }
                }
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    try
                    {
                        object value = field.GetValue(instance);
                        if (value is bool)
                        {
                            return (bool)value;
                        }
                    }
                    catch
                    {
                    }
                }
                result = false;
            }
            return result;
        }

        // Token: 0x060000C9 RID: 201 RVA: 0x00008D34 File Offset: 0x00006F34
        private void EnsurePlayerType()
        {
            if (!(this._playerType != null))
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly assembly in assemblies)
                {
                    this._playerType = assembly.GetType("Player");
                    if (this._playerType != null)
                    {
                        break;
                    }
                }
                this._currentPlayerProperty = ((this._playerType == null) ? null : this._playerType.GetProperty("Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
            }
        }

        // Token: 0x060000CA RID: 202 RVA: 0x00008DD4 File Offset: 0x00006FD4
        private void EnsurePlayerControllerType()
        {
            if (!(this._playerControllerType != null))
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly assembly in assemblies)
                {
                    this._playerControllerType = assembly.GetType("PlayerController");
                    if (this._playerControllerType != null)
                    {
                        break;
                    }
                }
                this._currentPlayerControllerProperty = ((this._playerControllerType == null) ? null : this._playerControllerType.GetProperty("Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
            }
        }

        // Token: 0x060000CB RID: 203 RVA: 0x00008E74 File Offset: 0x00007074
        private void EnsurePlayerMembers(Type playerType)
        {
            if (!(this._playerTransformProperty != null))
            {
                this._playerTransformProperty = playerType.GetProperty("PlayerTransform", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                this._leftInputProperty = playerType.GetProperty("LeftInput", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                this._rightInputProperty = playerType.GetProperty("RightInput", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }

        // Token: 0x060000CC RID: 204 RVA: 0x00008ED0 File Offset: 0x000070D0
        private static T GetProperty<T>(object instance, string propertyName)
        {
            T result;
            if (instance == null)
            {
                result = default(T);
            }
            else
            {
                PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property == null)
                {
                    result = default(T);
                }
                else
                {
                    try
                    {
                        object value = property.GetValue(instance, null);
                        if (value is T)
                        {
                            return (T)((object)value);
                        }
                    }
                    catch
                    {
                    }
                    result = default(T);
                }
            }
            return result;
        }

        // Token: 0x060000CD RID: 205 RVA: 0x00008F70 File Offset: 0x00007170
        private static T GetField<T>(object instance, string fieldName)
        {
            T result;
            if (instance == null)
            {
                result = default(T);
            }
            else
            {
                FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null)
                {
                    result = default(T);
                }
                else
                {
                    result = (T)((object)field.GetValue(instance));
                }
            }
            return result;
        }

        // Token: 0x060000CE RID: 206 RVA: 0x00008FD0 File Offset: 0x000071D0
        private static void SetProperty(object instance, string propertyName, object value)
        {
            if (instance != null)
            {
                PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                {
                    property.SetValue(instance, value, null);
                }
            }
        }

        // Token: 0x060000CF RID: 207 RVA: 0x00009014 File Offset: 0x00007214
        private void InvokeInputEvent(object inputSource, object playerInput)
        {
            if (inputSource != null && playerInput != null)
            {
                if (this._inputSourceEventMethod == null || this._inputSourceEventMethod.DeclaringType != inputSource.GetType())
                {
                    this._inputSourceEventMethod = inputSource.GetType().GetMethod("Event", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                if (!(this._inputSourceEventMethod == null))
                {
                    try
                    {
                        this._inputSourceEventMethod.Invoke(inputSource, new object[]
                        {
                            playerInput
                        });
                    }
                    catch
                    {
                    }
                }
            }
        }

        // Token: 0x04000032 RID: 50
        private Type _playerType;

        // Token: 0x04000033 RID: 51
        private Type _playerControllerType;

        // Token: 0x04000034 RID: 52
        private PropertyInfo _currentPlayerProperty;

        // Token: 0x04000035 RID: 53
        private PropertyInfo _currentPlayerControllerProperty;

        // Token: 0x04000036 RID: 54
        private PropertyInfo _playerTransformProperty;

        // Token: 0x04000037 RID: 55
        private PropertyInfo _leftInputProperty;

        // Token: 0x04000038 RID: 56
        private PropertyInfo _rightInputProperty;

        // Token: 0x04000039 RID: 57
        private MethodInfo _locomotionTranslateMethod;

        // Token: 0x0400003A RID: 58
        private object _locomotionController;

        // Token: 0x0400003B RID: 59
        private Type _playerStateManagerType;

        // Token: 0x0400003C RID: 60
        private PropertyInfo _currentStateTypeProperty;

        // Token: 0x0400003D RID: 61
        private object _stateManagerOwner;

        // Token: 0x0400003E RID: 62
        private Component _stateManagerComponent;

        // Token: 0x0400003F RID: 63
        private GameReflection.PlayerInputPair _looseInputPair = new GameReflection.PlayerInputPair();

        // Token: 0x04000040 RID: 64
        private float _customizationScanTime = -10f;

        // Token: 0x04000041 RID: 65
        private bool _customizationActive;

        // Token: 0x04000042 RID: 66
        private static readonly string[] CustomizationHints = new string[]
        {
            "Customization",
            "Customize",
            "Wardrobe",
            "Dressing",
            "CharacterEditor",
            "Appearance",
            "AvatarEditor",
            "Cosmetic"
        };

        // Token: 0x04000043 RID: 67
        private MethodInfo _inputSourceEventMethod;

        // Token: 0x04000044 RID: 68
        private MethodInfo _startInteractMethod;

        // Token: 0x04000045 RID: 69
        private MethodInfo _startInteractPickupMethod;

        // Token: 0x04000046 RID: 70
        private MethodInfo _stopInteractMethod;

        // Token: 0x04000047 RID: 71
        private MethodInfo _testLeftHandMethod;

        // Token: 0x04000048 RID: 72
        private MethodInfo _testRightHandMethod;

        // Token: 0x04000049 RID: 73
        private MethodInfo _testLeftHandEndMethod;

        // Token: 0x0400004A RID: 74
        private MethodInfo _testRightHandEndMethod;

        // Token: 0x0400004B RID: 75
        private readonly Dictionary<int, GameReflection.BagState> _openBags = new Dictionary<int, GameReflection.BagState>();

        // Token: 0x0400004C RID: 76
        private Transform _cachedLocalBagTransform;

        // Token: 0x02000006 RID: 6
        private sealed class BagState
        {
            // Token: 0x0400004F RID: 79
            public Transform Parent;

            // Token: 0x04000050 RID: 80
            public Vector3 LocalPosition;

            // Token: 0x04000051 RID: 81
            public Quaternion LocalRotation;

            // Token: 0x04000052 RID: 82
            public Vector3 LocalScale;

            // Token: 0x04000053 RID: 83
            public bool IsOpen;
        }

        // Token: 0x02000007 RID: 7
        internal sealed class PlayerInputPair
        {
            // Token: 0x17000038 RID: 56
            // (get) Token: 0x060000D3 RID: 211 RVA: 0x00009158 File Offset: 0x00007358
            public bool HasAny
            {
                get
                {
                    return this.Left != null || this.Right != null;
                }
            }

            // Token: 0x04000054 RID: 84
            public object Left;

            // Token: 0x04000055 RID: 85
            public object Right;
        }
    }
}
