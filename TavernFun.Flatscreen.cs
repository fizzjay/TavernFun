using Alta.Character;
using Alta.Impact;
using Alta.StatSystem;
using HarmonyLib;
using MelonLoader;
using System;
using System.Collections;
using Alta.Chunks;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;


namespace TavernFun
{
    // FlatscreenCore used to be its own standalone MelonMod (FlatscreenATTMod). It is now a plain
    // driver class owned and pumped by TavernFunMod/ControlMenu, so PanKake no longer needs a
    // second mod DLL to function. All original behaviour (VR-hand emulation, camera control,
    // server-session handling, debug overlay, server browser) is unchanged - only the entry point
    // changed. Init()/Tick()/LateTick()/DrawDebugContent() replace the old MelonMod overrides.
    //
    // Token: 0x02000004 RID: 4
    internal sealed class FlatscreenCore
    {
        // Master on/off switch, driven by the PanKake button on the TavernFun menu. When false,
        // all update/draw logic is skipped and the Harmony prefixes stop suppressing the game's
        // own OpenXR/camera update methods, handing control back to the game as if this mod were
        // never loaded.
        internal static bool Enabled = true;

        // Lightweight logger shim so the code below (ported unmodified from the old MelonMod)
        // can keep calling "LoggerInstance.Msg/Warning" without every call site needing edits.
        private static readonly MelonLogger.Instance LoggerInstance = new MelonLogger.Instance("PanKake");

        // Token: 0x17000028 RID: 40
        // (get) Token: 0x0600003C RID: 60 RVA: 0x00002D48 File Offset: 0x00000F48
        // (set) Token: 0x0600003D RID: 61 RVA: 0x00002D5E File Offset: 0x00000F5E
        internal static FlatscreenCore Instance { get; private set; }
        internal static bool NoclipEnabled = false;
        internal static bool SuppressPlayerMovement = false;
        internal static bool NoVoidTpEnabled = false;

        private int _noclipPlayerLayer = -1;
        private int _noclipTerrainLayer = -1;
        private bool _noclipActive;
        internal static bool RaycastGrabEnabled = false;
        internal static bool GrabbyHandsEnabled = false;

        internal int GrabByName(string nameFilter, int limit = int.MaxValue)
        {
            return this._game.GrabByName(nameFilter, limit);
        }

        internal bool TryGrabTargetedPickup()
        {
            object player = this._game.FindLocalPlayer();
            object rightInput = this._game.GetRightInput(player);
            if (rightInput == null)
            {
                return false;
            }
            return this._hands.TryGrabTargetedPickup(this._game, rightInput, true);
        }

        internal void ReleaseAllGrabs()
        {
            this._hands.ReleaseAllGrabs();
        }

        internal Vector3? GetTargetedPickupPosition()
        {
            return this._hands.GetTargetedPickupPosition();
        }


        internal Vector3 ReadInputMoveVector()
        {
            return this._input.ReadMoveVector();
        }

        internal Vector2 ReadInputMouseDelta()
        {
            return this._input.ReadMouseDelta();
        }

        internal float ReadInputScroll()
        {
            return this._input.ReadScroll();
        }

        internal bool IsRunPressed
        {
            get { return this._input.IsRunPressed; }
        }

        internal bool IsFreeFlyUpPressed
        {
            get { return this._input.IsMenuFlyUpPressed; }
        }

        internal bool IsFreeFlyDownPressed
        {
            get { return this._input.IsMenuFlyDownPressed; }
        }
        private void ApplyNoclip()
        {
            if (!NoclipEnabled)
            {
                if (_noclipActive && _noclipPlayerLayer != -1 && _noclipTerrainLayer != -1)
                {
                    Physics.IgnoreLayerCollision(_noclipPlayerLayer, _noclipTerrainLayer, false);
                }
                _noclipActive = false;
                return;
            }

            if (_noclipTerrainLayer == -1 && MasterTerrain.Instance != null && MasterTerrain.Instance.Terrain != null)
            {
                _noclipTerrainLayer = MasterTerrain.Instance.Terrain.gameObject.layer;
            }

            object player = this._game.FindLocalPlayer();
            CharacterController cc = this._game.FindLocalCharacterController(player);
            if (cc != null)
            {
                _noclipPlayerLayer = cc.gameObject.layer;
            }

            if (_noclipPlayerLayer != -1 && _noclipTerrainLayer != -1 && !_noclipActive)
            {
                Physics.IgnoreLayerCollision(_noclipPlayerLayer, _noclipTerrainLayer, true);
                _noclipActive = true;
            }
        }
        // Token: 0x17000029 RID: 41
        // (get) Token: 0x0600003E RID: 62 RVA: 0x00002D68 File Offset: 0x00000F68
        internal float HeightOffset
        {
            get
            {
                return this._heightOffset;
            }
        }
        private bool _fullBodyRotationEnabled;
        private bool _quickAccessMenuOpen;
        private bool _quickAccessPendingOpen;
        internal bool FullBodyRotationEnabled
        {
            get { return this._fullBodyRotationEnabled; }
        }
        private bool _flyModeEnabled;

        internal bool FlyModeEnabled
        {
            get { return this._flyModeEnabled; }
        }

        internal void ToggleFlyMode()
        {
            this._flyModeEnabled = !this._flyModeEnabled;
            object player = this._game.FindLocalPlayer();
            if (player != null && !this._game.TrySetDebugMovement(player, this._flyModeEnabled))
            {
                LoggerInstance.Warning("Fly mode: could not toggle SmoothLocomotion.IsDebugMovement (no local player locomotion found yet).");
            }
        }
        private bool _mirrorEnabled;
        private GameObject _mirrorQuad;
        private Material _mirrorMaterial;
        private Camera _mirrorCamera;
        private RenderTexture _mirrorRenderTexture;
        private RenderTexture _lastMirrorTexture;
        private string _mirrorStatus = "Mirror off";
        private bool _handCameraSummoned;
        private float _lastSelfieFixTime = -10f;
        private float _lastResummonTime = -10f;

        // Reflection into the game's Alta.ScreenshotCamera.HandCamera / HandyCameraManager.
        private Type _handCameraType;
        private Type _handyCameraManagerType;
        private MethodInfo _updateCameraStatusMethod;
        private PropertyInfo _handCameraCurrentProperty;
        private FieldInfo _handCameraRenderTextureField;
        private PropertyInfo _handCameraIsSelfieProperty;
        private PropertyInfo _handCameraIsStreamingProperty;
        private MethodInfo _handCameraFlipMethod;

        private const float MirrorDistance = 2.2f;
        private const float MirrorHeight = 1.2f;
        private const float MirrorWidth = 2.4f;
        private const float MirrorTall = 3.6f;
        private const float MirrorCameraOffset = 0.6f;
        private const int MirrorRenderWidth = 480;
        private const int MirrorRenderHeight = 720;

        internal bool MirrorEnabled
        {
            get { return _mirrorEnabled; }
        }

        internal string MirrorStatus
        {
            get { return _mirrorStatus; }
        }

        internal void ToggleMirror()
        {
            _mirrorEnabled = !_mirrorEnabled;
            if (_mirrorEnabled)
            {
                EnsureMirrorQuad();
                EnsureMirrorCamera();
                SummonHandCamera();
                _mirrorStatus = "Mirror on - waiting for feed";
            }
            else
            {
                if (_mirrorQuad != null)
                {
                    _mirrorQuad.SetActive(false);
                }
                StopHandCameraStreaming(GetHandCamera());
                ReleaseMirrorCamera();
                DesummonHandCamera();
                _mirrorStatus = "Mirror off";
            }
        }

        // Summons the game's actual screenshot camera for the local player (same call the
        // "Summon Camera" quick-access action runs). It's a toggle: call it again to despawn.
        private void SummonHandCamera()
        {
            _handCameraSummoned = false;
            _lastSelfieFixTime = -10f;
            EnsureHandCameraReflection();
            if (_updateCameraStatusMethod == null)
            {
                _mirrorStatus = "Mirror: camera manager not found";
                return;
            }
            try
            {
                _updateCameraStatusMethod.Invoke(null, null);
                _handCameraSummoned = true;
                _mirrorStatus = "Mirror: camera summon requested...";
            }
            catch (Exception ex)
            {
                _mirrorStatus = "Mirror: summon failed (" + ex.GetType().Name + ")";
            }
        }

        private void DesummonHandCamera()
        {
            if (!_handCameraSummoned)
            {
                return;
            }
            _handCameraSummoned = false;
            EnsureHandCameraReflection();
            if (_updateCameraStatusMethod == null)
            {
                return;
            }
            // Only toggle it off if a camera actually spawned - otherwise the toggle would
            // spawn one instead of removing it.
            if (GetHandCamera() == null)
            {
                return;
            }
            try
            {
                _updateCameraStatusMethod.Invoke(null, null);
            }
            catch { }
        }

        private void EnsureMirrorQuad()
        {
            if (_mirrorQuad == null)
            {
                _mirrorQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _mirrorQuad.name = "TavernFun Mirror Screen";
                Collider collider = _mirrorQuad.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }
                Object.DontDestroyOnLoad(_mirrorQuad);
                Shader shader = Shader.Find("Unlit/Texture");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }
                _mirrorMaterial = new Material(shader);
                _mirrorQuad.GetComponent<MeshRenderer>().material = _mirrorMaterial;
                _mirrorQuad.transform.localScale = new Vector3(MirrorWidth, MirrorTall, 1f);
            }
            _mirrorQuad.SetActive(true);
        }
        private void EnsureHandCameraReflection()
        {
            if (_handCameraType != null && _handyCameraManagerType != null)
            {
                return;
            }
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (_handCameraType == null)
                {
                    _handCameraType = assembly.GetType("Alta.ScreenshotCamera.HandCamera");
                    if (_handCameraType != null)
                    {
                        _handCameraCurrentProperty = _handCameraType.GetProperty("Current", BindingFlags.Static | BindingFlags.Public);
                        _handCameraRenderTextureField = _handCameraType.GetField("renderTexture", BindingFlags.Instance | BindingFlags.NonPublic);
                        _handCameraIsSelfieProperty = _handCameraType.GetProperty("IsUsingSelfieCamera", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        _handCameraIsStreamingProperty = _handCameraType.GetProperty("IsStreaming", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        _handCameraFlipMethod = _handCameraType.GetMethod("FlipCamera", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    }
                }
                if (_handyCameraManagerType == null)
                {
                    _handyCameraManagerType = assembly.GetType("Alta.ScreenshotCamera.HandyCameraManager");
                    if (_handyCameraManagerType != null)
                    {
                        _updateCameraStatusMethod = _handyCameraManagerType.GetMethod("UpdateCameraStatusForLocalPlayer", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    }
                }
            }
        }

        private object GetHandCamera()
        {
            EnsureHandCameraReflection();
            if (_handCameraCurrentProperty == null)
            {
                return null;
            }
            try
            {
                return _handCameraCurrentProperty.GetValue(null, null);
            }
            catch
            {
                return null;
            }
        }

        private RenderTexture GetHandCameraRenderTexture(object handCamera)
        {
            if (handCamera == null || _handCameraRenderTextureField == null)
            {
                return null;
            }
            try
            {
                return _handCameraRenderTextureField.GetValue(handCamera) as RenderTexture;
            }
            catch
            {
                return null;
            }
        }

        // We rotate the whole camera to face the player, so the FRONT lens is the one that
        // points at them - just make sure selfie mode (whose lens would point away after the
        // rotation) never sneaks in. Throttled because FlipCamera is a no-op until the camera
        // is visible, so we keep retrying until it actually flips.
        private void EnsureHandCameraFront(object handCamera)
        {
            if (handCamera == null || _handCameraIsSelfieProperty == null || _handCameraFlipMethod == null)
            {
                return;
            }
            if (Time.unscaledTime < _lastSelfieFixTime + 0.5f)
            {
                return;
            }
            try
            {
                bool isSelfie = _handCameraIsSelfieProperty.GetValue(handCamera, null) is bool selfie && selfie;
                if (isSelfie)
                {
                    _lastSelfieFixTime = Time.unscaledTime;
                    _handCameraFlipMethod.Invoke(handCamera, null);
                }
            }
            catch { }
        }
        private void UpdateMirror()
        {
            if (!_mirrorEnabled || _mirrorQuad == null)
            {
                return;
            }
            object player = this._game.FindLocalPlayer();
            Transform playerRoot = this._game.GetPlayerRootTransform(player);
            if (playerRoot == null)
            {
                if (_mirrorQuad.activeSelf)
                {
                    _mirrorQuad.SetActive(false);
                }
                _mirrorStatus = "Mirror: no local player yet";
                return;
            }
            if (!_mirrorQuad.activeSelf)
            {
                _mirrorQuad.SetActive(true);
            }

            Vector3 forwardFlat = Vector3.ProjectOnPlane(playerRoot.forward, Vector3.up);
            if (forwardFlat.sqrMagnitude < 0.0001f)
            {
                forwardFlat = playerRoot.forward;
            }
            forwardFlat.Normalize();

            Vector3 mirrorPosition = playerRoot.position + Vector3.up * MirrorHeight + forwardFlat * MirrorDistance;
            _mirrorQuad.transform.position = mirrorPosition;
            _mirrorQuad.transform.rotation = Quaternion.LookRotation(playerRoot.position - mirrorPosition, Vector3.up);

            // Preferred feed: the summoned game camera's viewfinder, flipped to selfie mode so
            // its lens faces back at the player - that is a real mirror view of you.
            object handCamera = GetHandCamera();
            if (handCamera != null)
            {
                RenderTexture feed = GetHandCameraRenderTexture(handCamera);
                Component camComponent = handCamera as Component;
                Transform camTransform = (camComponent != null) ? camComponent.transform : null;
                if (feed != null && camTransform != null)
                {
                    // Keep the front lens active, and force the camera to keep streaming even
                    // though it now sits hidden behind the big display.
                    EnsureHandCameraFront(handCamera);
                    KeepHandCameraStreaming(handCamera);

                    // Park the camera BEHIND the display so it never blocks the screen, still
                    // pointing back at the player. The display's back face is culled, so the
                    // lens sees straight through to you.
                    Vector3 eye = playerRoot.position + Vector3.up * MirrorHeight;
                    Vector3 camPosition = mirrorPosition - forwardFlat * MirrorCameraOffset;
                    camTransform.position = camPosition;
                    camTransform.rotation = Quaternion.LookRotation(eye - camPosition, Vector3.up);

                    if (!ReferenceEquals(_mirrorMaterial.mainTexture, feed))
                    {
                        _mirrorMaterial.mainTexture = feed;
                        _lastMirrorTexture = feed;
                    }
                    _mirrorStatus = "Mirror: big display active";
                    return;
                }
                _mirrorStatus = (feed == null)
                    ? "Mirror: game camera not rendering yet"
                    : "Mirror: game camera transform missing";
            }
            else if (_handCameraSummoned)
            {
                // The game despawns the summoned camera once the player walks out of its range -
                // just summon it again so the mirror keeps following you.
                if (Time.unscaledTime >= _lastResummonTime + 2f)
                {
                    _lastResummonTime = Time.unscaledTime;
                    try
                    {
                        _updateCameraStatusMethod.Invoke(null, null);
                    }
                    catch { }
                }
                _mirrorStatus = "Mirror: waiting for camera to spawn...";
            }

            // Fallback: our own dedicated camera at the quad, looking back at the player.
            if (_mirrorCamera == null || _mirrorRenderTexture == null)
            {
                EnsureMirrorCamera();
            }
            if (_mirrorCamera != null && _mirrorRenderTexture != null)
            {
                Vector3 eye = playerRoot.position + Vector3.up * MirrorHeight;
                _mirrorCamera.transform.position = mirrorPosition;
                _mirrorCamera.transform.rotation = Quaternion.LookRotation(eye - mirrorPosition, Vector3.up);
                _mirrorCamera.aspect = (float)MirrorRenderWidth / MirrorRenderHeight;
                _mirrorCamera.fieldOfView = 60f;
                try
                {
                    _mirrorCamera.Render();
                }
                catch
                {
                    _mirrorStatus = "Mirror: render failed";
                    return;
                }
                if (!ReferenceEquals(_mirrorMaterial.mainTexture, _mirrorRenderTexture))
                {
                    _mirrorMaterial.mainTexture = _mirrorRenderTexture;
                }
                _mirrorStatus = "Mirror: local render";
            }
        }
        // The game's HandCamera only renders its viewfinder while its object is on-screen. We
        // hide it behind the display, so force IsStreaming on - that keeps the feed alive even
        // when the physical camera isn't visible to the main camera.
        private void KeepHandCameraStreaming(object handCamera)
        {
            if (_handCameraIsStreamingProperty == null || handCamera == null)
            {
                return;
            }
            try
            {
                if (!(_handCameraIsStreamingProperty.GetValue(handCamera, null) is bool streaming && streaming))
                {
                    _handCameraIsStreamingProperty.SetValue(handCamera, true, null);
                }
            }
            catch { }
        }

        private void StopHandCameraStreaming(object handCamera)
        {
            if (_handCameraIsStreamingProperty == null || handCamera == null)
            {
                return;
            }
            try
            {
                _handCameraIsStreamingProperty.SetValue(handCamera, false, null);
            }
            catch { }
        }
        private void EnsureMirrorCamera()
        {
            if (_mirrorCamera != null)
            {
                return;
            }
            GameObject gameObject = new GameObject("TavernFun Mirror Camera");
            Object.DontDestroyOnLoad(gameObject);
            _mirrorCamera = gameObject.AddComponent<Camera>();
            _mirrorCamera.enabled = false; // driven by UpdateMirror -> Render()

            Camera source = (_camera != null) ? _camera : Camera.main;
            if (source != null)
            {
                _mirrorCamera.cullingMask = source.cullingMask;
                _mirrorCamera.clearFlags = source.clearFlags;
                _mirrorCamera.backgroundColor = source.backgroundColor;
                _mirrorCamera.nearClipPlane = source.nearClipPlane;
                _mirrorCamera.farClipPlane = source.farClipPlane;
            }
            else
            {
                _mirrorCamera.cullingMask = -1;
                _mirrorCamera.clearFlags = CameraClearFlags.Skybox;
                _mirrorCamera.nearClipPlane = 0.05f;
                _mirrorCamera.farClipPlane = 100f;
            }
            _mirrorCamera.fieldOfView = 60f;
            _mirrorCamera.depth = -10f;

            _mirrorRenderTexture = new RenderTexture(MirrorRenderWidth, MirrorRenderHeight, 24, RenderTextureFormat.ARGB32);
            _mirrorRenderTexture.name = "TavernFun Mirror RenderTexture";
            _mirrorRenderTexture.Create();
            _mirrorCamera.targetTexture = _mirrorRenderTexture;

            if (_mirrorMaterial != null)
            {
                _mirrorMaterial.mainTexture = _mirrorRenderTexture;
            }
        }

        private void ReleaseMirrorCamera()
        {
            if (_mirrorRenderTexture != null)
            {
                _mirrorRenderTexture.Release();
                _mirrorRenderTexture = null;
            }
            if (_mirrorCamera != null)
            {
                Object.Destroy(_mirrorCamera.gameObject);
                _mirrorCamera = null;
            }
        }
        internal void ToggleFullBodyRotation()
        {
            this._fullBodyRotationEnabled = !this._fullBodyRotationEnabled;
        }

        private void BeginQuickAccessMenuOpen()
        {
            this._quickAccessPendingOpen = true;
            this._hands.QuickAccessModeActive = true;
            this._hands.ResetQuickAccessHandPose();
        }

        private void CloseQuickAccessMenu()
        {
            this._quickAccessPendingOpen = false;
            if (this._quickAccessMenuOpen)
            {
                this._game.TryCloseQuickAccessMenu();
            }
            this._quickAccessMenuOpen = false;
            this._hands.QuickAccessModeActive = false;
            this._hands.ResetQuickAccessHandPose();
            this._game.ResetSubBubbleState();
        }

        private void TryCompletePendingQuickAccessOpen()
        {
            if (!this._quickAccessPendingOpen || this._quickAccessMenuOpen)
            {
                return;
            }
            if (!this._hands.IsQuickAccessHandCentered())
            {
                return;
            }
            if (this._game.TryOpenQuickAccessMenu())
            {
                this._quickAccessPendingOpen = false;
                this._quickAccessMenuOpen = true;
                this._game.TrySelectQuickAccessBubble(0);
            }
        }
        // Token: 0x1700002A RID: 42
        // (get) Token: 0x0600003F RID: 63 RVA: 0x00002D80 File Offset: 0x00000F80
        internal float CameraFieldOfView
        {
            get
            {
                Camera camera = (this._camera != null) ? this._camera : Camera.main;
                return (camera != null) ? camera.fieldOfView : this._targetFieldOfView;
            }
        }

        // Token: 0x1700002B RID: 43
        // (get) Token: 0x06000040 RID: 64 RVA: 0x00002DC8 File Offset: 0x00000FC8
        internal float LookSensitivity
        {
            get
            {
                return this._input.LookSensitivityMultiplier;
            }
        }

        // Token: 0x1700002C RID: 44
        // (get) Token: 0x06000041 RID: 65 RVA: 0x00002DE8 File Offset: 0x00000FE8
        internal bool ThirdPersonEnabled
        {
            get
            {
                return this._thirdPersonEnabled;
            }
        }

        // Token: 0x1700002D RID: 45
        // (get) Token: 0x06000042 RID: 66 RVA: 0x00002E00 File Offset: 0x00001000
        internal float ThirdPersonDistance
        {
            get
            {
                return this._thirdPersonDistance;
            }
        }

        // Token: 0x06000043 RID: 67 RVA: 0x00002E18 File Offset: 0x00001018
        internal void Init()
        {
            FlatscreenCore.Instance = this;
            new HarmonyLib.Harmony("TavernFun.PanKake").PatchAll(typeof(OpenXRInputPatches).Assembly);
            LoggerInstance.Msg("Loaded. Right-click PanKake on the TavernFun menu for the full panel. F7 opens the legacy control menu, Caps Lock toggles control lock, F10 toggles debug overlay.");
        }

        // Token: 0x06000044 RID: 68 RVA: 0x00002E54 File Offset: 0x00001054
        internal void Tick()
        {
            UpdateMirror();
            ApplyNoclip();
            if (!FlatscreenCore.Enabled)
            {
                return;
            }
            if (!this._input.IsAvailable)
            {
                this._lastStatus = "waiting for keyboard/mouse from Unity InputSystem";
                if (!this._loggedMissingInput)
                {
                    this._loggedMissingInput = true;
                    LoggerInstance.Warning("Unity InputSystem keyboard/mouse devices are not available yet.");
                }

            }
            else
            {
                if (this._input.IsControlTogglePressed)
                {
                    this._cursorLocked = !this._cursorLocked;
                    LoggerInstance.Msg("Control lock " + (this._cursorLocked ? "enabled" : "disabled") + ".");
                }
                if (this._input.IsDebugTogglePressed)
                {
                    this._showDebug = !this._showDebug;
                    LoggerInstance.Msg("Debug overlay " + (this._showDebug ? "enabled" : "disabled") + ".");
                }
                if (this._input.IsQuickAccessTogglePressed)
                {
                    if (this._quickAccessMenuOpen || this._quickAccessPendingOpen)
                    {
                        this.CloseQuickAccessMenu();
                    }
                    else
                    {
                        this.BeginQuickAccessMenuOpen();
                    }
                }
                if (this._quickAccessMenuOpen)
                {
                    if (this._input.IsQuickAccessPrevPressed)
                    {
                        this._game.TryCycleQuickAccessSelection(-1);
                    }
                    else if (this._input.IsQuickAccessNextPressed)
                    {
                        this._game.TryCycleQuickAccessSelection(1);
                    }
                }
                if (this._input.IsServerBrowserTogglePressed)
                {
                    this._showServerBrowser = !this._showServerBrowser;
                    LoggerInstance.Msg("Server browser " + (this._showServerBrowser ? "enabled" : "disabled") + ".");
                }
                if (this._input.IsBagTogglePressed)
                {
                    this._hands.TriggerBagAssist(this._game, this.GetViewTransform());
                    LoggerInstance.Msg("Bag assist triggered.");
                }
                if (this._input.IsThirdPersonTogglePressed)
                {
                    this._thirdPersonEnabled = !this._thirdPersonEnabled;
                    LoggerInstance.Msg("Third person " + (this._thirdPersonEnabled ? "enabled" : "disabled") + ".");
                }

                this.ApplyCursorState();
            }
        }

        internal void LateTick()
        {
            if (!FlatscreenCore.Enabled)
            {
                FlatscreenCore.SuppressOpenXRInputUpdates = false;
                return;
            }
            FlatscreenCore.SuppressOpenXRInputUpdates = true;
            this._input.SuppressCombatInput = this._flyModeEnabled;
            FlatscreenCore.SuppressOpenXRInputUpdates = true;
            if (!this._input.IsAvailable)
            {
                FlatscreenCore.SuppressOpenXRInputUpdates = false;
            }
            else if (this._pendingReturnToMenu)
            {
                this._lastStatus = "returning to menu";
                this._lastMode = "server";
                this._camera = null;
                this._game.TryReturnToMainMenu();
                object obj = this._game.FindLocalPlayer();
                if (obj == null)
                {
                    this._pendingReturnToMenu = false;
                    this._serverSessionActive = false;
                    this._lastPlayerTransform = null;
                    this._lastMode = "menu";
                }
            }
            else
            {
                object obj2 = this._lastPlayer = this._game.FindLocalPlayer();
                if (obj2 != null)
                {
                    this._serverSessionActive = true;
                    this.UpdateServerPlayer(obj2);
                }
                else if (!this.HoldMissingServerPlayer())
                {
                    this.UpdateMainMenu();
                }
            }
        }

        // Status readout, drawn with no BeginArea/EndArea of its own so it can be dropped
        // straight into the TavernFun-styled PanKake panel's scroll view.
        // Token: 0x06000046 RID: 70 RVA: 0x00003128 File Offset: 0x00001328
        internal void DrawDebugContent()
        {
            GUILayout.Label("pankake settings", new GUILayoutOption[0]);
            GUILayout.Label("Status: " + this._lastStatus, new GUILayoutOption[0]);
            GUILayout.Label("Mode: " + this._lastMode, new GUILayoutOption[0]);
            GUILayout.Label("Input available: " + this._input.IsAvailable, new GUILayoutOption[0]);
            GUILayout.Label("Cursor locked: " + this._cursorLocked, new GUILayoutOption[0]);
            GUILayout.Label("Player type: " + this._game.PlayerTypeName, new GUILayoutOption[0]);
            GUILayout.Label("Controller type: " + this._game.PlayerControllerTypeName, new GUILayoutOption[0]);
            GUILayout.Label("Has Player.Current: " + this._game.HasCurrentPlayerProperty, new GUILayoutOption[0]);
            GUILayout.Label("Has PlayerController.Current: " + this._game.HasCurrentPlayerControllerProperty, new GUILayoutOption[0]);
            GUILayout.Label("Player object: " + ((this._lastPlayer == null) ? "null" : this._lastPlayer.GetType().FullName), new GUILayoutOption[0]);
            GUILayout.Label("Player transform: " + ((this._lastPlayerTransform == null) ? "null" : this._lastPlayerTransform.name), new GUILayoutOption[0]);
            GUILayout.Label("Left input: " + ((this._lastLeftInput == null) ? "null" : this._lastLeftInput.GetType().FullName), new GUILayoutOption[0]);
            GUILayout.Label("Right input: " + ((this._lastRightInput == null) ? "null" : this._lastRightInput.GetType().FullName), new GUILayoutOption[0]);
            GUILayout.Label("Locomotion found: " + this._game.HasLocomotionController, new GUILayoutOption[0]);
            GUILayout.Label("Height offset: " + this._heightOffset.ToString("0.00"), new GUILayoutOption[0]);
            GUILayout.Label(string.Concat(new object[]
            {
                "Climbing mode: ",
                this._hands.ClimbingModeEnabled ? "enabled" : "disabled",
                " / active: ",
                this._hands.IsClimbingGrabActive
            }), new GUILayoutOption[0]);
            GUILayout.Label("Target collider: " + this._hands.TargetColliderName, new GUILayoutOption[0]);
            GUILayout.Label("Target pickup: " + this._hands.HasTargetPickup, new GUILayoutOption[0]);
            GUILayout.Label(string.Concat(new object[]
            {
                "Target interactable: ",
                this._hands.TargetInteractableName,
                " / ",
                this._hands.HasTargetInteractable
            }), new GUILayoutOption[0]);
            GUILayout.Label("Target menu: " + this._hands.TargetMenuName, new GUILayoutOption[0]);
            GUILayout.Label("Target components: " + this._hands.TargetParentComponents, new GUILayoutOption[0]);
            GUILayout.Label("Interactor: " + this._game.LastInteractorName, new GUILayoutOption[0]);
            GUILayout.Label("Interact result: " + this._game.LastInteractResult, new GUILayoutOption[0]);
            GUILayout.Label(string.Concat(new object[]
            {
                "Control menu: ",
                this._showServerBrowser,
                " / ",
                this._serverBrowser.Count,
                " / ",
                this._serverBrowser.Status
            }), new GUILayoutOption[0]);
            GUILayout.Label(string.Concat(new object[]
            {
                "Third person: ",
                this._thirdPersonEnabled,
                " Distance: ",
                this._thirdPersonDistance.ToString("0.00")
            }), new GUILayoutOption[0]);
        }

        // height offset, bag toggle, drop items, return to menu) inline with no floating window
        // of its own, so it can sit inside the scrollable PanKake panel too.
        internal void DrawServerBrowserContent()
        {
            this._serverBrowser.DrawInline(this._game, this._hands);
        }

        // Toggles the panel uses to flip the same switches the F7/F10/Caps Lock/V hotkeys do.
        internal bool ShowDebug
        {
            get { return this._showDebug; }
            set { this._showDebug = value; }
        }

        internal bool ShowServerBrowser
        {
            get { return this._showServerBrowser; }
            set { this._showServerBrowser = value; }
        }

        internal string LastStatus
        {
            get { return this._lastStatus; }
        }

        internal string LastMode
        {
            get { return this._lastMode; }
        }

        // Token: 0x06000047 RID: 71 RVA: 0x0000361C File Offset: 0x0000181C
        private void UpdateServerPlayer(object player)
        {
            object obj = this._game.FindLocalController();
            Transform controllerTransform = this._game.GetControllerTransform(obj);
            Camera controllerCamera = this._game.GetControllerCamera(obj);
            Transform playerTransform = this._game.GetPlayerTransform(player);
            Transform transform = (controllerTransform != null) ? controllerTransform : this._game.GetPlayerRootTransform(player);
            Camera camera = (controllerCamera != null) ? controllerCamera : this._game.GetPlayerCamera(player);
            bool movementBlocked = this.GetMovementBlocked(player, obj);
            this._lastPlayerTransform = ((transform != null) ? transform : playerTransform);
            if (transform == null)
            {
                this._lastStatus = "server player found, but no root transform";
                this._lastMode = "server";
            }
            else
            {
                if (camera != null)
                {
                    this._camera = camera;
                    this._camera.enabled = true;
                    this._camera.tag = "MainCamera";
                    this.ApplyDefaultFieldOfView(this._camera);
                    this.UpdateServerCamera(transform, this._camera.transform);
                }
                else
                {
                    this.EnsureCamera(false);
                    if (this._camera != null)
                    {
                        this.UpdateServerCamera(transform, this._camera.transform);
                    }
                }
                Camera camera2 = this.GetViewCamera();
                if (this._thirdPersonEnabled)
                {
                    this.UpdateThirdPersonCamera(transform, this._camera);
                    camera2 = ((this._thirdPersonCamera != null) ? this._thirdPersonCamera : this._camera);
                }
                else
                {
                    this.DisableThirdPersonCamera();
                    camera2 = this._camera;
                }
                this.AdjustHeight();
                bool flag = this._game.IsCustomizationObject(player) || this._game.IsCustomizationObject(obj) || this._game.IsCustomizationTransform(transform) || (movementBlocked && this._game.IsCustomizationActive());
                if (flag)
                {
                    this._wasCustomizationMode = true;
                    this.UpdateCustomizationArea(player, transform, camera);
                }
                else
                {
                    if (this._wasCustomizationMode)
                    {
                        this.RestorePlayerCameraAfterCustomization(camera);
                    }
                    bool flag2 = !movementBlocked || flag;
                    this._customizationCameraInitialized = false;
                    this._wasCustomizationMode = false;
                    if (movementBlocked)
                    {
                        if (!this.ShouldPreserveBlockedHandInteraction())
                        {
                            this._game.TryReleaseHeldHands(player);
                        }
                        if (!this._wasMovementBlocked)
                        {
                            this._hands.ResetHands();
                        }
                    }
                    else if (this._wasMovementBlocked)
                    {
                        this._game.TryReleaseHeldHands(player);
                    }
                    this._wasMovementBlocked = movementBlocked;
                    if (!this._lookInputLocked)
                    {
                        this._input.UpdateLook(this._cursorLocked && !this._input.IsCombatModePressed);
                    }

                    // Reassert IsDebugMovement every frame regardless of fly state.
                    // When fly is ON this keeps flight alive across scene transitions.
                    // When fly is OFF this restores gravity if a fresh SmoothLocomotion
                    // instance spawned with debug movement still active.
                    this._game.TrySetDebugMovement(player, this._flyModeEnabled);

                    if (this._cursorLocked && flag2)
                    {
                        this.MovePlayer(player, obj, transform);
                    }
                    this._lastLeftInput = this._game.GetLeftInput(player);
                    this._lastRightInput = this._game.GetRightInput(player);
                    if (this._lastLeftInput == null || this._lastRightInput == null)
                    {
                        GameReflection.PlayerInputPair playerInputPair = this._game.FindLoosePlayerInputs();
                        if (this._lastLeftInput == null)
                        {
                            this._lastLeftInput = playerInputPair.Left;
                        }
                        if (this._lastRightInput == null)
                        {
                            this._lastRightInput = playerInputPair.Right;
                        }
                    }
                    Camera camera3 = (this._camera != null) ? this._camera : camera2;
                    Transform transform2 = (camera3 != null) ? camera3.transform : null;
                    if (camera3 != null && transform2 != null)
                    {
                        this._hands.UpdateInputs(this._lastLeftInput, this._lastRightInput, this._game, this._input, camera3, transform2, this._cursorLocked);
                        this.TryCompletePendingQuickAccessOpen();
                        if (this._quickAccessMenuOpen)
                        {
                            this._game.TryMaintainQuickAccessSelection();
                        }
                    }
                    if (this._cursorLocked && flag2)
                    {
                        this.ApplyClimbTranslation(player, obj, transform);
                    }
                    this._lastStatus = (flag ? "controlling customization area" : (movementBlocked ? "server movement blocked" : (this._hands.IsClimbingGrabActive ? "climbing server player" : (this._cursorLocked ? "controlling server player" : "server controls unlocked"))));
                    this._lastMode = (flag ? "customization" : "server");
                }
            }
        }

        // Token: 0x06000048 RID: 72 RVA: 0x00003ABC File Offset: 0x00001CBC
        private bool HoldMissingServerPlayer()
        {
            bool result;
            if (!this._serverSessionActive && (this._lastPlayerTransform == null || (this._lastMode != "server" && this._lastMode != "customization")))
            {
                result = false;
            }
            else
            {
                this._hands.ReleaseAllGrabs();
                this._wasMovementBlocked = true;
                this._lastStatus = "server player unavailable, movement blocked";
                this._lastMode = "server";
                result = true;
            }
            return result;
        }

        // Token: 0x06000049 RID: 73 RVA: 0x00003B48 File Offset: 0x00001D48
        private bool GetMovementBlocked(object player, object controller)
        {
            if (!object.ReferenceEquals(this._movementBlockPlayer, player) || !object.ReferenceEquals(this._movementBlockController, controller) || Time.unscaledTime >= this._nextMovementBlockScanTime)
            {
                this._movementBlockPlayer = player;
                this._movementBlockController = controller;
                this._cachedMovementBlocked = this._game.IsMovementBlocked(player, controller);
                this._nextMovementBlockScanTime = Time.unscaledTime + 0.5f;
            }
            return this._cachedMovementBlocked;
        }

        // Token: 0x0600004A RID: 74 RVA: 0x00003BC8 File Offset: 0x00001DC8
        private void RestorePlayerCameraAfterCustomization(Camera playerCamera)
        {
            this.DisableThirdPersonCamera();
            if (this._camera != null && this._camera.name == "Flatscreen Desktop Camera")
            {
                this._camera.enabled = false;
                this._camera.tag = "Untagged";
                this._camera = null;
            }
            if (playerCamera != null)
            {
                playerCamera.enabled = true;
                playerCamera.tag = "MainCamera";
                this._camera = playerCamera;
                this.ApplyDefaultFieldOfView(this._camera);
                this._input.SetInitialRotation(this._camera.transform.rotation);
            }
            this._customizationCameraInitialized = false;
            this._menuCameraInitialized = false;
        }

        // Token: 0x0600004B RID: 75 RVA: 0x00003C94 File Offset: 0x00001E94
        private void UpdateCustomizationArea(object player, Transform playerRoot, Camera sourceCamera)
        {
            this._wasMovementBlocked = false;
            this.DisableThirdPersonCamera();
            this._lastLeftInput = this._game.GetLeftInput(player);
            this._lastRightInput = this._game.GetRightInput(player);
            if (this._lastLeftInput == null || this._lastRightInput == null)
            {
                GameReflection.PlayerInputPair playerInputPair = this._game.FindLoosePlayerInputs();
                if (this._lastLeftInput == null)
                {
                    this._lastLeftInput = playerInputPair.Left;
                }
                if (this._lastRightInput == null)
                {
                    this._lastRightInput = playerInputPair.Right;
                }
            }
            this.EnsureCamera(true);
            if (this._camera == null)
            {
                this._lastStatus = "customization: waiting for camera";
                this._lastMode = "customization";
            }
            else
            {
                if (!this._customizationCameraInitialized)
                {
                    Transform playerHeadTransform = this._game.GetPlayerHeadTransform(player);
                    if (playerHeadTransform != null)
                    {
                        this._menuCameraPosition = playerHeadTransform.position;
                        this._input.SetInitialRotation(playerHeadTransform.rotation);
                    }
                    else if (playerRoot != null)
                    {
                        this._menuCameraPosition = playerRoot.position + Vector3.up * this._heightOffset;
                        this._input.SetInitialRotation(Quaternion.Euler(0f, playerRoot.rotation.eulerAngles.y, 0f));
                    }
                    else if (sourceCamera != null)
                    {
                        this._menuCameraPosition = sourceCamera.transform.position;
                        this._input.SetInitialRotation(sourceCamera.transform.rotation);
                    }
                    else
                    {
                        this._menuCameraPosition = this._camera.transform.position;
                    }
                    this._customizationCameraInitialized = true;
                }
                if (this._cursorLocked)
                {
                    this.MoveMenuCamera();
                }
                this._camera.transform.position = this._menuCameraPosition;
                this._camera.transform.rotation = this._input.CameraRotation;
                this._hands.UpdateInputs(this._lastLeftInput, this._lastRightInput, this._game, this._input, this._camera, this._camera.transform, this._cursorLocked);
                this._lastStatus = (this._cursorLocked ? "controlling customization camera" : "customization controls unlocked");
                this._lastMode = "customization";
            }
        }

        // Token: 0x0600004C RID: 76 RVA: 0x00003F24 File Offset: 0x00002124
        private void UpdateMainMenu()
        {
            this._lastPlayerTransform = null;
            GameReflection.PlayerInputPair playerInputPair = this._game.FindLoosePlayerInputs();
            this._lastLeftInput = playerInputPair.Left;
            this._lastRightInput = playerInputPair.Right;
            this._wasMovementBlocked = false;
            if (!playerInputPair.HasAny)
            {
                this._lastStatus = "main menu: waiting for PlayerInput hands";
                this._lastMode = "menu";
            }
            else
            {
                this.EnsureMenuCamera();
                this.AdjustHeight();
                if (!this._lookInputLocked)
                {
                    this._input.UpdateLook(this._cursorLocked);
                }
                if (this._cursorLocked)
                {
                    this.MoveMenuCamera();
                }
                this._camera.transform.position = this._menuCameraPosition;
                this._camera.transform.rotation = this._input.CameraRotation;
                this._hands.UpdateInputs(playerInputPair.Left, playerInputPair.Right, this._game, this._input, this._camera, this._camera.transform, this._cursorLocked);
                this._lastStatus = (this._cursorLocked ? "controlling main menu hands" : "menu controls unlocked");
                this._lastMode = "menu";
            }
        }

        // Token: 0x0600004D RID: 77 RVA: 0x00004054 File Offset: 0x00002254
        private void EnsureCamera(bool forceDedicatedCamera = false)
        {
            if (!(this._camera != null) || (forceDedicatedCamera && !(this._camera.name == "Flatscreen Desktop Camera")))
            {
                Camera camera = null;
                if (!forceDedicatedCamera)
                {
                    camera = Camera.main;
                    if (camera != null)
                    {
                        this._camera = camera;
                        this._camera.enabled = true;
                        this._camera.tag = "MainCamera";
                        this.ApplyDefaultFieldOfView(this._camera);
                        return;
                    }
                }
                if (forceDedicatedCamera && this._camera != null && this._camera.name != "Flatscreen Desktop Camera")
                {
                    this._camera.enabled = false;
                    camera = this._camera;
                    this._camera = null;
                }
                if (this._camera == null)
                {
                    if (camera == null)
                    {
                        camera = Camera.main;
                    }
                    GameObject gameObject = new GameObject("Flatscreen Desktop Camera");
                    Object.DontDestroyOnLoad(gameObject);
                    this._camera = gameObject.AddComponent<Camera>();
                    this._camera.nearClipPlane = 0.03f;
                    this._camera.farClipPlane = 2000f;
                    this._camera.fieldOfView = this._targetFieldOfView;
                    if (camera != null)
                    {
                        this._camera.clearFlags = camera.clearFlags;
                        this._camera.backgroundColor = camera.backgroundColor;
                        this._camera.cullingMask = camera.cullingMask;
                        this._camera.depth = camera.depth + 1f;
                    }
                    if (forceDedicatedCamera)
                    {
                        Camera[] array = Object.FindObjectsOfType<Camera>();
                        foreach (Camera camera2 in array)
                        {
                            if (camera2 != null && camera2 != this._camera)
                            {
                                camera2.enabled = false;
                            }
                        }
                    }
                }
                this._camera.enabled = true;
                this._camera.tag = "MainCamera";
                this.ApplyDefaultFieldOfView(this._camera);
            }
        }

        // Token: 0x0600004E RID: 78 RVA: 0x000042BC File Offset: 0x000024BC
        private void EnsureMenuCamera()
        {
            this.EnsureCamera(false);
            this._menuCamera = this._camera;
            if (!this._menuCameraInitialized)
            {
                this._menuCameraPosition = this._camera.transform.position;
                this._input.SetInitialRotation(this._camera.transform.rotation);
                this._menuCameraInitialized = true;
            }
        }

        // Token: 0x0600004F RID: 79 RVA: 0x00004324 File Offset: 0x00002524
        private void UpdatePlayerCamera(Transform playerTransform)
        {
            if (this._camera != null)
            {
                this.UpdateServerCamera(playerTransform, this._camera.transform);
            }
        }

        // Token: 0x06000050 RID: 80 RVA: 0x0000435A File Offset: 0x0000255A
        internal void RequestReturnToMenu()
        {
            this._pendingReturnToMenu = true;
        }

        // Token: 0x06000051 RID: 81 RVA: 0x00004364 File Offset: 0x00002564
        internal bool TryToggleBag()
        {
            this._hands.TriggerBagAssist(this._game, this.GetViewTransform());
            return true;
        }

        // Token: 0x06000052 RID: 82 RVA: 0x00004390 File Offset: 0x00002590
        internal bool DropAllHeldItems()
        {
            object obj = this._game.FindLocalPlayer();
            this._hands.ReleaseAllGrabs();
            bool result;
            if (obj == null)
            {
                this._lastStatus = "drop all: no local player";
                result = false;
            }
            else
            {
                bool flag = this._game.TryReleaseHeldHands(obj);
                this._lastStatus = (flag ? "dropped held items" : "no held items to drop");
                result = flag;
            }
            return result;
        }

        // Token: 0x06000053 RID: 83 RVA: 0x000043FC File Offset: 0x000025FC
        internal bool ToggleTerrainRefresh()
        {
            GameObject gameObject = GameObject.Find("MasterTerrain");
            bool result;
            if (gameObject == null)
            {
                this._lastStatus = "MasterTerrain not found";
                result = false;
            }
            else
            {
                if (!this._terrainBasePositionSet)
                {
                    this._terrainBasePosition = gameObject.transform.position;
                    this._terrainBasePositionSet = true;
                }
                if (this._terrainDown)
                {
                    gameObject.transform.position = this._terrainBasePosition;
                    this._terrainDown = false;
                    this._lastStatus = "terrain restored";
                }
                else
                {
                    gameObject.transform.position = this._terrainBasePosition + Vector3.down * 50f;
                    this._terrainDown = true;
                    this._lastStatus = "terrain lowered";
                }
                result = true;
            }
            return result;
        }
        private void UpdateServerCamera(Transform playerRoot, Transform cameraTransform)
        {
            if (cameraTransform == null)
            {
                return;
            }
            cameraTransform.localPosition = new Vector3(0f, this._heightOffset, 0f);
            Vector3 eulerAngles = this._input.CameraRotation.eulerAngles;
            float pitch = (eulerAngles.x > 180f) ? (eulerAngles.x - 360f) : eulerAngles.x;
            float yaw = eulerAngles.y;

            if (this._fullBodyRotationEnabled && playerRoot != null)
            {
                // Full body rotation: both yaw and pitch go onto the body, camera stays neutral
                // relative to it, instead of the body only yawing while the camera pitches alone.
                cameraTransform.localEulerAngles = Vector3.zero;
                playerRoot.localEulerAngles = new Vector3(pitch, yaw, 0f);
                return;
            }

            if (playerRoot != null)
            {
                playerRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            else
            {
                cameraTransform.rotation = this._input.CameraRotation;
            }
        }

        // Token: 0x06000055 RID: 85 RVA: 0x000045A4 File Offset: 0x000027A4
        private void UpdateThirdPersonCamera(Transform playerRoot, Camera sourceCamera)
        {
            if (playerRoot == null || sourceCamera == null)
            {
                this.DisableThirdPersonCamera();
            }
            else
            {
                this.EnsureThirdPersonCamera(sourceCamera);
                if (!(this._thirdPersonCamera == null))
                {
                    FlatscreenCore.SyncThirdPersonCameraSettings(sourceCamera, this._thirdPersonCamera);
                    // The first-person camera commonly excludes the local avatar's render
                    // layers. The third-person view must include them so the body is visible.
                    this._thirdPersonCamera.cullingMask |= GetAvatarRenderLayers(playerRoot);
                    Vector3 vector = playerRoot.position + Vector3.up * (this._heightOffset + 0.1f);
                    Quaternion cameraRotation = this._input.CameraRotation;
                    Vector3 vector2 = cameraRotation * new Vector3(0f, this._thirdPersonHeight, 0f - this._thirdPersonDistance);
                    this._thirdPersonCamera.transform.position = vector + vector2;
                    this._thirdPersonCamera.transform.rotation = Quaternion.LookRotation(vector - this._thirdPersonCamera.transform.position, Vector3.up);
                    this._thirdPersonCamera.enabled = true;
                    this._thirdPersonCamera.tag = "MainCamera";
                }
            }
        }

        private int GetAvatarRenderLayers(Transform playerRoot)
        {
            if (playerRoot == null) return 0;
            if (playerRoot != _thirdPersonAvatarRoot)
            {
                _thirdPersonAvatarRoot = playerRoot;
                _thirdPersonAvatarLayers = 1 << playerRoot.gameObject.layer;
                Renderer[] renderers = playerRoot.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                        _thirdPersonAvatarLayers |= 1 << renderers[i].gameObject.layer;
                }
            }
            return _thirdPersonAvatarLayers;
        }

        // Token: 0x06000056 RID: 86 RVA: 0x000046BC File Offset: 0x000028BC
        private void EnsureThirdPersonCamera(Camera sourceCamera)
        {
            if (!(this._thirdPersonCamera != null))
            {
                GameObject gameObject = new GameObject("Flatscreen Third Person Camera");
                Object.DontDestroyOnLoad(gameObject);
                this._thirdPersonCamera = gameObject.AddComponent<Camera>();
                FlatscreenCore.SyncThirdPersonCameraSettings(sourceCamera, this._thirdPersonCamera);
                this._thirdPersonCamera.depth = sourceCamera.depth + 10f;
                this._thirdPersonCamera.enabled = true;
            }
        }

        // Token: 0x06000057 RID: 87 RVA: 0x0000472C File Offset: 0x0000292C
        private static void SyncThirdPersonCameraSettings(Camera sourceCamera, Camera targetCamera)
        {
            if (!(sourceCamera == null) && !(targetCamera == null))
            {
                targetCamera.clearFlags = sourceCamera.clearFlags;
                targetCamera.backgroundColor = sourceCamera.backgroundColor;
                targetCamera.cullingMask = sourceCamera.cullingMask;
                targetCamera.nearClipPlane = sourceCamera.nearClipPlane;
                targetCamera.farClipPlane = sourceCamera.farClipPlane;
                targetCamera.fieldOfView = sourceCamera.fieldOfView;
                targetCamera.orthographic = sourceCamera.orthographic;
                targetCamera.orthographicSize = sourceCamera.orthographicSize;
                targetCamera.allowHDR = sourceCamera.allowHDR;
                targetCamera.allowMSAA = sourceCamera.allowMSAA;
            }
        }

        // Token: 0x06000058 RID: 88 RVA: 0x000047DC File Offset: 0x000029DC
        private void DisableThirdPersonCamera()
        {
            if (this._thirdPersonCamera != null)
            {
                this._thirdPersonCamera.enabled = false;
            }
        }

        // Fly mode uses its own smoothed velocity so direction changes/stops ease in and out
        // instead of snapping instantly like normal ground movement does.
        private Vector3 _flyVelocity;
        private const float FlySpeed = 5.5f;
        private const float FlyRunSpeed = 11f;
        private const float FlySmoothing = 10f;

        private void MovePlayer(object player, object controller, Transform playerTransform)
        {
            if (FlatscreenCore.SuppressPlayerMovement)
            {
                return;
            }
            Vector3 vector = this._input.ReadMoveVector();

            if (this._flyModeEnabled)
            {
                if (this._input.IsMenuFlyUpPressed)
                {
                    vector += Vector3.up;
                }
                if (this._input.IsMenuFlyDownPressed)
                {
                    vector -= Vector3.up;
                }
            }
            else
            {
                // Drop any residual smoothed velocity so the next fly toggle starts clean.
                this._flyVelocity = Vector3.zero;
            }

            Vector3 vector2;
            if (this._flyModeEnabled)
            {
                float flySpeed = this._input.IsRunPressed ? FlyRunSpeed : FlySpeed;
                Vector3 targetVelocity = (vector.sqrMagnitude > 1f) ? vector.normalized * flySpeed : vector * flySpeed;
                this._flyVelocity = Vector3.Lerp(this._flyVelocity, targetVelocity, 1f - Mathf.Exp(-FlySmoothing * Time.deltaTime));
                vector2 = this._flyVelocity * Time.deltaTime;
            }
            else
            {
                if (vector.sqrMagnitude <= 0.0001f)
                {
                    return;
                }
                float num = this._input.IsRunPressed ? 6f : 3.2f;
                vector2 = vector * num * Time.deltaTime;
                vector2.y = 0f;
            }

            if (vector2.sqrMagnitude > 1E-08f)
            {
                if (playerTransform != null)
                {
                    Vector3 position = playerTransform.position + vector2;
                    if (!this._flyModeEnabled)
                    {
                        position.y = playerTransform.position.y;
                    }
                    playerTransform.position = position;
                    this._lastStatus = "server movement direct";
                }
                else if (!this._game.TryTranslateWithLocomotion(player, vector2) && !this._game.TryTranslateWithLocomotion(controller, vector2) && !this._game.TryTranslateWithController(controller, vector2) && !this._game.TryTranslateWithController(player, vector2))
                {
                    this._lastStatus = "server movement unavailable";
                }
            }
        }
        // Token: 0x0600005A RID: 90 RVA: 0x00004914 File Offset: 0x00002B14
        private void ApplyClimbTranslation(object player, object controller, Transform playerTransform)
        {
            Vector3 vector = this._hands.ConsumeClimbBodyTranslation();
            if (vector.sqrMagnitude > 1E-06f)
            {
                vector = Vector3.ClampMagnitude(vector, 0.2f);
                if (playerTransform != null)
                {
                    playerTransform.position += vector;
                }
                else if (!this._game.TryTranslateWithLocomotion(player, vector) && !this._game.TryTranslateWithLocomotion(controller, vector) && !this._game.TryTranslateWithController(controller, vector) && !this._game.TryTranslateWithController(player, vector))
                {
                    this._lastStatus = "climb movement unavailable";
                }
            }
        }

        // Token: 0x0600005B RID: 91 RVA: 0x000049C8 File Offset: 0x00002BC8
        private void MoveMenuCamera()
        {
            Vector3 vector = this._input.ReadMoveVector();
            if (vector.sqrMagnitude > 0.0001f)
            {
                float num = this._input.IsRunPressed ? 6f : 3.2f;
                if (this._input.IsMenuFlyUpPressed)
                {
                    vector += Vector3.up;
                }
                if (this._input.IsMenuFlyDownPressed)
                {
                    vector -= Vector3.up;
                }
                this._menuCameraPosition += vector * num * Time.deltaTime;
            }
        }

        // Token: 0x0600005C RID: 92 RVA: 0x00004A78 File Offset: 0x00002C78
        private void AdjustHeight()
        {
            float num = 0f;
            if (this._input.IsHeightUpPressed)
            {
                num += 1f;
            }
            if (this._input.IsHeightDownPressed)
            {
                num -= 1f;
            }
            if (Mathf.Abs(num) > 0.001f)
            {
                this._heightOffset = Mathf.Clamp(this._heightOffset + num * Time.deltaTime * 0.8f, 0f, 2f);
            }
        }

        // Token: 0x0600005D RID: 93 RVA: 0x00004B03 File Offset: 0x00002D03
        internal void SetHeightOffset(float value)
        {
            this._heightOffset = Mathf.Clamp(value, 0f, 2f);
        }

        // Token: 0x0600005E RID: 94 RVA: 0x00004B1C File Offset: 0x00002D1C
        internal void ResetHeightOffset()
        {
            this._heightOffset = 1.45f;
        }
        internal void SetLookSensitivity(float value)
        {
            this._input.LookSensitivityMultiplier = value;
        }
        // Token: 0x0600005F RID: 95 RVA: 0x00004B2C File Offset: 0x00002D2C
        internal void SetCameraFieldOfView(float value)
        {
            float num = Mathf.Clamp(value, 45f, 110f);
            this._targetFieldOfView = num;
            this._fieldOfViewInitialized = true;
            if (this._camera != null)
            {
                this._camera.fieldOfView = num;
            }
            if (Camera.main != null && Camera.main != this._camera)
            {
                Camera.main.fieldOfView = num;
            }
        }

        // Token: 0x06000060 RID: 96 RVA: 0x00004BB4 File Offset: 0x00002DB4
        private void ApplyDefaultFieldOfView(Camera camera)
        {
            if (!this._fieldOfViewInitialized && !(camera == null))
            {
                camera.fieldOfView = this._targetFieldOfView;
                if (Camera.main != null && Camera.main != camera)
                {
                    Camera.main.fieldOfView = this._targetFieldOfView;
                }
                this._fieldOfViewInitialized = true;
            }
        }
        private static bool _killMeBusy;
        internal static bool KillMeBusy => _killMeBusy;

        internal static void KillMe()
        {
            if (_killMeBusy) return;
            MelonCoroutines.Start(KillMeCoroutine());
        }

        private static IEnumerator KillMeCoroutine()
        {
            _killMeBusy = true;
            yield return null; // one frame so button state updates

            var pc = PlayerController.Current as PlayerCharacter;
            if ((object)pc == null)
            {
                LoggerInstance.Warning("[KillMe] No local PlayerCharacter found.");
                _killMeBusy = false;
                yield break;
            }

            // 1. Temporarily lift the fall-damage block so our hit gets through,
            //    then deal massive damage using a source our own patches don't filter.
            bool prevFallDmg = VoidFallDamageEnabled;
            VoidFallDamageEnabled = false;
            try { pc.Health?.ReceiveDamage(999999f, 999999f, new DamageData(DamageSource.FallDamage)); }
            catch { }
            VoidFallDamageEnabled = prevFallDmg;

            // 2. Also zero the health stat and force IsDowned in case ReceiveDamage
            //    gets swallowed by an invuln window or the stat clamps to 1.
            if (pc.Health != null)
            {
                StatManager sm = pc.Health.StatManager;
                if (sm != null && sm.GetStat("health", out Stat healthStat) && healthStat != null)
                    healthStat.Base = 0f;
            }

            try
            {
                Type t = pc.GetType();
                while (t != null)
                {
                    PropertyInfo prop = t.GetProperty("IsDowned",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (prop != null && prop.CanWrite) { prop.SetValue(pc, true, null); break; }
                    FieldInfo field = t.GetField("isDowned",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (field != null) { field.SetValue(pc, true); break; }
                    t = t.BaseType;
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("[KillMe] Could not set IsDowned: " + ex.Message);
            }

            LoggerInstance.Msg($"[KillMe] Done. IsDowned={pc.IsDowned}");
            _killMeBusy = false;
        }
        internal static bool SetSpeed(float multiplier)
        {
            // Find SmoothLocomotion and set SpeedMultiplier directly — the health stat
            // approach doesn't work because locomotion speed lives on SmoothLocomotion,
            // not the character's health stat manager.
            MonoBehaviour[] all = Object.FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour mb in all)
            {
                if (mb == null || mb.GetType().Name != "SmoothLocomotion") continue;
                Type t = mb.GetType();
                // Try property first
                PropertyInfo prop = null;
                Type walk = t;
                while (walk != null)
                {
                    prop = walk.GetProperty("SpeedMultiplier",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (prop != null && prop.CanWrite) break;
                    prop = null;
                    walk = walk.BaseType;
                }
                if (prop != null)
                {
                    try { prop.SetValue(mb, multiplier, null); return true; }
                    catch { }
                }
                // Fallback: field
                walk = t;
                while (walk != null)
                {
                    FieldInfo field = walk.GetField("speedMultiplier",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (field != null)
                    {
                        try { field.SetValue(mb, multiplier); return true; }
                        catch { }
                    }
                    walk = walk.BaseType;
                }
            }
            return false;
        }

        // Reflection-based equivalents of GetStat(string, out Stat) / stat.Base = value, so we
        // never need to name the game's "Stat" type at compile time - only StatManager resolved
        // directly; Stat apparently doesn't (nested type, different namespace, etc.), so this
        // avoids that entirely, same approach GameReflection uses throughout this file.
        private static MethodInfo _getStatMethod;
        private static Type _statManagerType;

        private static bool TryGetStat(object statManager, string name, out object stat)
        {
            stat = null;
            if (statManager == null)
            {
                return false;
            }
            Type type = statManager.GetType();
            if (_getStatMethod == null || _statManagerType != type)
            {
                _statManagerType = type;
                _getStatMethod = null;
                foreach (MethodInfo mi in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (mi.Name != "GetStat")
                    {
                        continue;
                    }
                    ParameterInfo[] parms = mi.GetParameters();
                    if (parms.Length == 2 && parms[0].ParameterType == typeof(string) && parms[1].ParameterType.IsByRef)
                    {
                        _getStatMethod = mi;
                        break;
                    }
                }
            }
            if (_getStatMethod == null)
            {
                return false;
            }
            object[] args = new object[] { name, null };
            object result;
            try
            {
                result = _getStatMethod.Invoke(statManager, args);
            }
            catch
            {
                return false;
            }
            if (!(result is bool ok) || !ok)
            {
                return false;
            }
            stat = args[1];
            return stat != null;
        }

        private static bool TrySetStatBase(object stat, float value)
        {
            if (stat == null)
            {
                return false;
            }
            Type type = stat.GetType();
            while (type != null)
            {
                PropertyInfo prop = FindDeclaredProperty(type, "Base");
                if (prop != null && prop.CanWrite)
                {
                    try
                    {
                        prop.SetValue(stat, value, null);
                        return true;
                    }
                    catch { }
                }
                FieldInfo field = null;
                try
                {
                    field = type.GetField("Base", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                }
                catch { }
                if (field != null)
                {
                    try
                    {
                        field.SetValue(stat, value);
                        return true;
                    }
                    catch { }
                }
                type = type.BaseType;
            }
            return false;
        }

        private static object ReadMember(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }
            Type type = instance.GetType();
            while (type != null)
            {
                PropertyInfo prop = FindDeclaredProperty(type, name);
                if (prop != null)
                {
                    try
                    {
                        return prop.GetValue(instance, null);
                    }
                    catch { }
                }

                FieldInfo field = null;
                try
                {
                    field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                }
                catch { }
                if (field != null)
                {
                    try
                    {
                        return field.GetValue(instance);
                    }
                    catch { }
                }

                type = type.BaseType;
            }
            return null;
        }

        // Type.GetProperty(name, flags) throws AmbiguousMatchException if a base type declares a
        // property and a derived type redeclares one with the same name via "new" (common in these
        // game codebases). Walking one type at a time with DeclaredOnly and taking the first match
        // found (most-derived first) avoids that entirely.
        private static PropertyInfo FindDeclaredProperty(Type type, string name)
        {
            try
            {
                return type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            }
            catch (AmbiguousMatchException)
            {
                // Even a single type can declare two properties of the same name (rare, but
                // possible with explicit interface implementations) - just take the first.
                foreach (PropertyInfo candidate in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (candidate.Name == name)
                    {
                        return candidate;
                    }
                }
                return null;
            }
        }
        // Token: 0x06000062 RID: 98 RVA: 0x00004C39 File Offset: 0x00002E39
        internal void SetThirdPersonEnabled(bool value)
        {
            this._thirdPersonEnabled = value;
        }

        // Hip-move temporarily freezes the desktop look state so mouse deltas can drive the
        // body pose without moving the camera. The current look angles are left untouched.
        internal void SetLookInputLocked(bool value)
        {
            this._lookInputLocked = value;
        }

        // Token: 0x06000063 RID: 99 RVA: 0x00004C44 File Offset: 0x00002E44
        internal void ToggleThirdPerson()
        {
            this._thirdPersonEnabled = !this._thirdPersonEnabled;
            if (!this._thirdPersonEnabled)
            {
                this.DisableThirdPersonCamera();
            }
        }

        // Token: 0x06000064 RID: 100 RVA: 0x00004C74 File Offset: 0x00002E74
        internal void SetThirdPersonDistance(float value)
        {
            this._thirdPersonDistance = Mathf.Clamp(value, 0.8f, 4.5f);
        }

        private void ApplyCursorState()
        {
            if (ControlMenu.IsCursorFree)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }
            Cursor.lockState = this._cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !this._cursorLocked;
        }

        // Token: 0x06000066 RID: 102 RVA: 0x00004CD0 File Offset: 0x00002ED0
        private Camera GetViewCamera()
        {
            Camera result;
            if (this._thirdPersonEnabled && this._thirdPersonCamera != null)
            {
                result = this._thirdPersonCamera;
            }
            else
            {
                result = this._camera;
            }
            return result;
        }

        // Token: 0x06000067 RID: 103 RVA: 0x00004D14 File Offset: 0x00002F14
        private Transform GetViewTransform()
        {
            Camera viewCamera = this.GetViewCamera();
            return (viewCamera != null) ? viewCamera.transform : null;
        }

        // Token: 0x06000068 RID: 104 RVA: 0x00004D40 File Offset: 0x00002F40
        private bool ShouldPreserveBlockedHandInteraction()
        {
            string targetInteractableName = this._hands.TargetInteractableName;
            if (!string.IsNullOrEmpty(targetInteractableName))
            {
                string text = targetInteractableName.ToLowerInvariant();
                if (text.Contains("revive") || text.Contains("respawn") || text.Contains("death orb"))
                {
                    return true;
                }
            }
            string targetParentComponents = this._hands.TargetParentComponents;
            if (!string.IsNullOrEmpty(targetParentComponents))
            {
                string text = targetParentComponents.ToLowerInvariant();
                if (text.Contains("reviveorb") || text.Contains("respawnorb") || text.Contains("deathorb"))
                {
                    return true;
                }
            }
            return false;
        }


        // Token: 0x0400000B RID: 11
        internal static bool SuppressOpenXRInputUpdates;
        internal static bool VoidFallDamageEnabled = true;
        internal static bool VoidBurnDamageEnabled = false;
        // Token: 0x0400000C RID: 12
        private readonly DesktopInput _input = new DesktopInput();

        // Token: 0x0400000D RID: 13
        private readonly GameReflection _game = new GameReflection();

        // Token: 0x0400000E RID: 14
        private readonly HandEmulator _hands = new HandEmulator();

        // Token: 0x0400000F RID: 15
        private readonly ServerBrowserOverlay _serverBrowser = new ServerBrowserOverlay();

        // Token: 0x04000010 RID: 16
        private Camera _camera;

        // Token: 0x04000011 RID: 17
        private Camera _menuCamera;

        // Token: 0x04000012 RID: 18
        private Camera _thirdPersonCamera;
        private Transform _thirdPersonAvatarRoot;
        private int _thirdPersonAvatarLayers;

        // Token: 0x04000013 RID: 19
        private bool _cursorLocked = true;

        // Token: 0x04000014 RID: 20
        private bool _showDebug;

        // Token: 0x04000015 RID: 21
        private bool _showServerBrowser = true;

        // Token: 0x04000016 RID: 22
        private bool _menuCameraInitialized;

        // Token: 0x04000017 RID: 23
        private bool _customizationCameraInitialized;

        // Token: 0x04000018 RID: 24
        private bool _wasCustomizationMode;

        // Token: 0x04000019 RID: 25
        private bool _loggedMissingInput;

        // Token: 0x0400001A RID: 26
        private bool _pendingReturnToMenu;

        // Token: 0x0400001B RID: 27
        private bool _serverSessionActive;

        // Token: 0x0400001C RID: 28
        private string _lastStatus = "starting";

        // Token: 0x0400001D RID: 29
        private string _lastMode = "none";

        // Token: 0x0400001E RID: 30
        private object _lastPlayer;

        // Token: 0x0400001F RID: 31
        private Transform _lastPlayerTransform;

        // Token: 0x04000020 RID: 32
        private object _lastLeftInput;

        // Token: 0x04000021 RID: 33
        private object _lastRightInput;

        // Token: 0x04000022 RID: 34
        private bool _wasMovementBlocked;

        // Token: 0x04000023 RID: 35
        private object _movementBlockPlayer;

        // Token: 0x04000024 RID: 36
        private object _movementBlockController;

        // Token: 0x04000025 RID: 37
        private bool _cachedMovementBlocked;

        // Token: 0x04000026 RID: 38
        private float _nextMovementBlockScanTime;

        // Token: 0x04000027 RID: 39
        private Vector3 _menuCameraPosition;

        // Token: 0x04000028 RID: 40
        private float _heightOffset = 1.45f;

        // Token: 0x04000029 RID: 41
        private float _targetFieldOfView = 90f;

        // Token: 0x0400002A RID: 42
        private bool _fieldOfViewInitialized;

        // Token: 0x0400002B RID: 43
        private bool _thirdPersonEnabled;
        private bool _lookInputLocked;

        // Token: 0x0400002C RID: 44
        private float _thirdPersonDistance = 2.35f;

        // Token: 0x0400002D RID: 45
        private float _thirdPersonHeight = 0.55f;

        // Token: 0x0400002E RID: 46
        private Vector3 _terrainBasePosition;

        // Token: 0x0400002F RID: 47
        private bool _terrainBasePositionSet;

        // Token: 0x04000030 RID: 48
        private bool _terrainDown;

    }

    // Token: 0x02000008 RID: 8
    internal sealed class HandEmulator
    {
        // Token: 0x17000039 RID: 57
        // (get) Token: 0x060000D5 RID: 213 RVA: 0x0000918C File Offset: 0x0000738C
        public string TargetColliderName
        {
            get
            {
                return this._targeter.CurrentColliderName;
            }
        }

        // Token: 0x1700003A RID: 58
        // (get) Token: 0x060000D6 RID: 214 RVA: 0x000091AC File Offset: 0x000073AC
        public string TargetInteractableName
        {
            get
            {
                return this._targeter.CurrentInteractableName;
            }
        }

        public bool IsQuickAccessHandCentered()
        {
            return true;
        }
        // add near the other hand fields
        private bool _skipNextQuickAccessDelta;

        public void ResetQuickAccessHandPose()
        {
            this._right.ResetQuickAccess();
            this._right.ResetRestPose();
            this._skipNextQuickAccessDelta = true; // ignore the delta from the frame we opened on
        }

        private void UpdateQuickAccess(DesktopInput input)
        {
            // hands never move in quick access mode — offset stays at zero
            return;
        }
        internal bool TryGrabTargetedPickup(GameReflection game, object rightInput, bool allowAnyInteractable)
        {
            object target = this._targeter.CurrentPickup;
            if (target == null && allowAnyInteractable)
            {
                target = this._targeter.CurrentInteractable;
            }
            if (target == null)
            {
                return false;
            }
            object interactor = game.FindInteractorForInput(rightInput);
            if (interactor == null)
            {
                return false;
            }
            return game.TryPickupGrab(interactor, target);
        }

        internal Vector3? GetTargetedPickupPosition()
        {
            object pickup = this._targeter.CurrentPickup;
            Component component = pickup as Component;
            return component != null ? (Vector3?)component.transform.position : null;
        }
        // Token: 0x1700003B RID: 59
        // (get) Token: 0x060000D7 RID: 215 RVA: 0x000091CC File Offset: 0x000073CC
        public string TargetMenuName
        {
            get
            {
                return this._targeter.CurrentMenuTargetName;
            }
        }

        // Token: 0x1700003C RID: 60
        // (get) Token: 0x060000D8 RID: 216 RVA: 0x000091EC File Offset: 0x000073EC
        public string TargetParentComponents
        {
            get
            {
                return this._targeter.CurrentParentComponents;
            }
        }

        // Token: 0x1700003D RID: 61
        // (get) Token: 0x060000D9 RID: 217 RVA: 0x0000920C File Offset: 0x0000740C
        public bool HasTargetPickup
        {
            get
            {
                return this._targeter.CurrentPickup != null;
            }
        }

        // Token: 0x1700003E RID: 62
        // (get) Token: 0x060000DA RID: 218 RVA: 0x00009230 File Offset: 0x00007430
        public bool HasTargetInteractable
        {
            get
            {
                return this._targeter.CurrentInteractable != null;
            }
        }

        // Token: 0x1700003F RID: 63
        // (get) Token: 0x060000DB RID: 219 RVA: 0x00009254 File Offset: 0x00007454
        public float LeftHorizontalAngle
        {
            get
            {
                return this._left.HorizontalAngle;
            }
        }

        // Token: 0x17000040 RID: 64
        // (get) Token: 0x060000DC RID: 220 RVA: 0x00009274 File Offset: 0x00007474
        public float LeftVerticalAngle
        {
            get
            {
                return this._left.VerticalAngle;
            }
        }

        // Token: 0x17000041 RID: 65
        // (get) Token: 0x060000DD RID: 221 RVA: 0x00009294 File Offset: 0x00007494
        public float LeftRollAngle
        {
            get
            {
                return this._left.RollAngle;
            }
        }

        // Token: 0x17000042 RID: 66
        // (get) Token: 0x060000DE RID: 222 RVA: 0x000092B4 File Offset: 0x000074B4
        public float RightHorizontalAngle
        {
            get
            {
                return this._right.HorizontalAngle;
            }
        }

        // Token: 0x17000043 RID: 67
        // (get) Token: 0x060000DF RID: 223 RVA: 0x000092D4 File Offset: 0x000074D4
        public float RightVerticalAngle
        {
            get
            {
                return this._right.VerticalAngle;
            }
        }

        // Token: 0x17000044 RID: 68
        // (get) Token: 0x060000E0 RID: 224 RVA: 0x000092F4 File Offset: 0x000074F4
        public float RightRollAngle
        {
            get
            {
                return this._right.RollAngle;
            }
        }

        // Token: 0x17000045 RID: 69
        // (get) Token: 0x060000E1 RID: 225 RVA: 0x00009314 File Offset: 0x00007514
        public float LeftDepth
        {
            get
            {
                return this._left.Depth;
            }
        }

        // Token: 0x17000046 RID: 70
        // (get) Token: 0x060000E2 RID: 226 RVA: 0x00009334 File Offset: 0x00007534
        public float RightDepth
        {
            get
            {
                return this._right.Depth;
            }
        }

        // Token: 0x17000047 RID: 71
        // (get) Token: 0x060000E3 RID: 227 RVA: 0x00009354 File Offset: 0x00007554
        // (set) Token: 0x060000E4 RID: 228 RVA: 0x0000936B File Offset: 0x0000756B
        public bool ClimbingModeEnabled { get; set; }
        public bool QuickAccessModeActive { get; set; }
        // Token: 0x17000048 RID: 72
        // (get) Token: 0x060000E5 RID: 229 RVA: 0x00009374 File Offset: 0x00007574
        public bool IsClimbingGrabActive
        {
            get
            {
                return this.ClimbingModeEnabled && (this.IsLeftClimbingGrabActive || this.IsRightClimbingGrabActive);
            }
        }

        // Token: 0x17000049 RID: 73
        // (get) Token: 0x060000E6 RID: 230 RVA: 0x000093A4 File Offset: 0x000075A4
        public bool IsLeftClimbingGrabActive
        {
            get
            {
                return this._left.IsClimbingGrabActive;
            }
        }

        // Token: 0x1700004A RID: 74
        // (get) Token: 0x060000E7 RID: 231 RVA: 0x000093C4 File Offset: 0x000075C4
        public bool IsRightClimbingGrabActive
        {
            get
            {
                return this._right.IsClimbingGrabActive;
            }
        }

        // Token: 0x1700004B RID: 75
        // (get) Token: 0x060000E8 RID: 232 RVA: 0x000093E4 File Offset: 0x000075E4
        // (set) Token: 0x060000E9 RID: 233 RVA: 0x000093FC File Offset: 0x000075FC
        public float ClimbSensitivity
        {
            get
            {
                return this._climbSensitivity;
            }
            set
            {
                this._climbSensitivity = Mathf.Clamp(value, 0.25f, 3f);
            }
        }

        // Token: 0x1700004C RID: 76
        // (get) Token: 0x060000EA RID: 234 RVA: 0x00009418 File Offset: 0x00007618
        // (set) Token: 0x060000EB RID: 235 RVA: 0x00009430 File Offset: 0x00007630
        public float CombatSensitivity
        {
            get
            {
                return this._combatSensitivity;
            }
            set
            {
                this._combatSensitivity = Mathf.Clamp(value, 0.25f, 3f);
            }
        }

        // Token: 0x060000EC RID: 236 RVA: 0x0000944C File Offset: 0x0000764C
        public void ResetHands()
        {
            this._left.Reset();
            this._right.Reset();
            this._bagAssistActive = false;
            this._leftGrabToggle = false;
            this._rightGrabToggle = false;
            this._climbBodyTranslation = Vector3.zero;
            this._left.ResetCombat();
            this._right.ResetCombat();
            this._right.ResetQuickAccess();
        }

        // Token: 0x060000ED RID: 237 RVA: 0x000094AC File Offset: 0x000076AC
        public void ReleaseAllGrabs()
        {
            this._leftGrabToggle = false;
            this._rightGrabToggle = false;
            this._left.ResetClimb();
            this._right.ResetClimb();
            this._left.ResetCombat();
            this._right.ResetCombat();
        }

        // Token: 0x060000EE RID: 238 RVA: 0x000094F8 File Offset: 0x000076F8
        public void TriggerBagAssist(GameReflection game, Transform cameraTransform)
        {
            if (this._bagAssistActive)
            {
                this._bagAssistActive = false;
                this._left.ResetRestPose();
            }
            else
            {
                object player = game.FindLocalPlayer();
                Transform playerRootTransform = game.GetPlayerRootTransform(player);
                Transform transform = (playerRootTransform != null) ? playerRootTransform : cameraTransform;
                if (!(transform == null))
                {
                    Vector3 vector = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                    if (vector.sqrMagnitude < 0.0001f)
                    {
                        vector = transform.forward;
                    }
                    vector.Normalize();
                    Vector3 vector2 = Vector3.Cross(Vector3.up, vector);
                    if (vector2.sqrMagnitude < 0.0001f)
                    {
                        vector2 = transform.right;
                    }
                    vector2.Normalize();
                    Transform transform2 = (cameraTransform != null) ? cameraTransform : transform;
                    this._bagAssistPosition = new Vector3(transform.position.x - vector.x * 0.28f - vector2.x * 0.18f, transform2.position.y - 0.34f, transform.position.z - vector.z * 0.28f - vector2.z * 0.18f);
                    this._bagAssistRotation = Quaternion.LookRotation(-vector, Vector3.up) * Quaternion.Euler(0f, -90f, 90f);
                    this._bagAssistActive = true;
                    this._leftSelectedToggle = false;
                }
            }
        }

        // Token: 0x060000EF RID: 239 RVA: 0x00009685 File Offset: 0x00007885
        public void ResetLeftPose()
        {
            this._left.ResetPose();
        }

        // Token: 0x060000F0 RID: 240 RVA: 0x00009694 File Offset: 0x00007894
        public void ResetRightPose()
        {
            this._right.ResetPose();
        }

        // Token: 0x060000F1 RID: 241 RVA: 0x000096A3 File Offset: 0x000078A3
        public void SetLeftHorizontalAngle(float value)
        {
            this._left.HorizontalAngle = Mathf.Clamp(value, -180f, 180f);
        }

        // Token: 0x060000F2 RID: 242 RVA: 0x000096C1 File Offset: 0x000078C1
        public void SetLeftVerticalAngle(float value)
        {
            this._left.VerticalAngle = Mathf.Clamp(value, -180f, 180f);
        }

        // Token: 0x060000F3 RID: 243 RVA: 0x000096DF File Offset: 0x000078DF
        public void SetLeftRollAngle(float value)
        {
            this._left.RollAngle = Mathf.Clamp(value, -180f, 180f);
        }

        // Token: 0x060000F4 RID: 244 RVA: 0x000096FD File Offset: 0x000078FD
        public void SetRightHorizontalAngle(float value)
        {
            this._right.HorizontalAngle = Mathf.Clamp(value, -180f, 180f);
        }

        // Token: 0x060000F5 RID: 245 RVA: 0x0000971B File Offset: 0x0000791B
        public void SetRightVerticalAngle(float value)
        {
            this._right.VerticalAngle = Mathf.Clamp(value, -180f, 180f);
        }

        // Token: 0x060000F6 RID: 246 RVA: 0x00009739 File Offset: 0x00007939
        public void SetRightRollAngle(float value)
        {
            this._right.RollAngle = Mathf.Clamp(value, -180f, 180f);
        }

        // Token: 0x060000F7 RID: 247 RVA: 0x00009757 File Offset: 0x00007957
        public void SetLeftDepth(float value)
        {
            this._left.Depth = Mathf.Clamp(value, -0.15f, 1.6f);
        }

        // Token: 0x060000F8 RID: 248 RVA: 0x00009775 File Offset: 0x00007975
        public void SetRightDepth(float value)
        {
            this._right.Depth = Mathf.Clamp(value, -0.15f, 1.6f);
        }

        // Token: 0x060000F9 RID: 249 RVA: 0x00009793 File Offset: 0x00007993
        public void SetLeftPalmDownPose()
        {
            HandEmulator.SetPalmDownPose(this._left, true);
        }

        // Token: 0x060000FA RID: 250 RVA: 0x000097A3 File Offset: 0x000079A3
        public void SetRightPalmDownPose()
        {
            HandEmulator.SetPalmDownPose(this._right, false);
        }

        // Token: 0x060000FB RID: 251 RVA: 0x000097B3 File Offset: 0x000079B3
        public void SetBothPalmDownPose()
        {
            HandEmulator.SetPalmDownPose(this._left, true);
            HandEmulator.SetPalmDownPose(this._right, false);
        }

        // Token: 0x060000FC RID: 252 RVA: 0x000097D0 File Offset: 0x000079D0
        public void SetBothHandshakePose()
        {
            HandEmulator.SetHandshakePose(this._left, true);
            HandEmulator.SetHandshakePose(this._right, false);
        }

        // Token: 0x060000FD RID: 253 RVA: 0x000097ED File Offset: 0x000079ED
        public void SetBothFingertipsDownInPose()
        {
            HandEmulator.SetFingertipsDownInPose(this._left, true);
            HandEmulator.SetFingertipsDownInPose(this._right, false);
        }

        // Token: 0x060000FE RID: 254 RVA: 0x0000980C File Offset: 0x00007A0C
        private static void SetPalmDownPose(HandEmulator.HandState state, bool isLeft)
        {
            state.HorizontalAngle = (isLeft ? -105f : 105f);
            state.VerticalAngle = -110f;
            state.RollAngle = (isLeft ? 122f : -122f);
            state.Clamp();
        }

        // Token: 0x060000FF RID: 255 RVA: 0x00009858 File Offset: 0x00007A58
        private static void SetHandshakePose(HandEmulator.HandState state, bool isLeft)
        {
            state.HorizontalAngle = (isLeft ? -14f : 14f);
            state.VerticalAngle = -44f;
            state.RollAngle = 0f;
            state.Depth = 0.26f;
            state.Clamp();
        }

        // Token: 0x06000100 RID: 256 RVA: 0x000098A4 File Offset: 0x00007AA4
        private static void SetFingertipsDownInPose(HandEmulator.HandState state, bool isLeft)
        {
            state.HorizontalAngle = (isLeft ? -16f : 16f);
            state.VerticalAngle = 57f;
            state.RollAngle = (isLeft ? -5f : 5f);
            state.Depth = 0.26f;
            state.Clamp();
        }

        // Token: 0x06000101 RID: 257 RVA: 0x000098FB File Offset: 0x00007AFB
        public void Update(object player, GameReflection game, DesktopInput input, Camera camera, Transform cameraTransform, bool cursorLocked)
        {
            this.UpdateInputs(game.GetLeftInput(player), game.GetRightInput(player), game, input, camera, cameraTransform, cursorLocked);
        }

        // Token: 0x06000102 RID: 258 RVA: 0x0000991C File Offset: 0x00007B1C
        public void UpdateInputs(object leftInput, object rightInput, GameReflection game, DesktopInput input, Camera camera, Transform cameraTransform, bool cursorLocked)
        {
            this._climbBodyTranslation = Vector3.zero;
            if (!object.ReferenceEquals(this._lastLeftInput, leftInput))
            {
                this._left.IsLocked = false;
                this._left.ResetRestPose();
                this._left.ResetClimb();
                this._lastLeftInput = leftInput;
            }
            if (!object.ReferenceEquals(this._lastRightInput, rightInput))
            {
                this._right.IsLocked = false;
                this._right.ResetRestPose();
                this._right.ResetClimb();
                this._lastRightInput = rightInput;
            }
            if (input.IsResetHandsPressed)
            {
                this.ResetHands();
            }
            if (input.IsLockLeftPressed)
            {
                this._left.IsLocked = !this._left.IsLocked;
            }
            if (input.IsLockRightPressed)
            {
                this._right.IsLocked = !this._right.IsLocked;
            }
            if (input.IsLockBothPressed)
            {
                bool isLocked = !this._left.IsLocked || !this._right.IsLocked;
                this._left.IsLocked = isLocked;
                this._right.IsLocked = isLocked;
            }
            if (input.IsUnlockHandsPressed)
            {
                this._left.IsLocked = false;
                this._right.IsLocked = false;
            }
            if (input.IsLeftSelectTogglePressed)
            {
                this._leftSelectedToggle = !this._leftSelectedToggle;
                if (this._leftSelectedToggle)
                {
                    this._left.IsLocked = false;
                }
                else
                {
                    this._left.ResetPositionOnly();
                }
            }
            if (input.IsRightSelectTogglePressed)
            {
                this._rightSelectedToggle = !this._rightSelectedToggle;
                if (this._rightSelectedToggle)
                {
                    this._right.IsLocked = false;
                }
                else
                {
                    this._right.ResetPositionOnly();
                }
            }
            float num = input.ReadScroll() * 0.0012f;
            if (Mathf.Abs(num) > 0.0001f)
            {
                if (this._leftSelectedToggle)
                {
                    this._left.Depth += num;
                }
                if (this._rightSelectedToggle)
                {
                    this._right.Depth += num;
                }
                this._left.Clamp();
                this._right.Clamp();
            }
            float num2 = 0f;
            float num3 = 0f;
            if (input.IsHandAdjustLeftPressed)
            {
                num2 -= 0.55f * Time.deltaTime;
            }
            if (input.IsHandAdjustRightPressed)
            {
                num2 += 0.55f * Time.deltaTime;
            }
            if (input.IsHandAdjustUpPressed)
            {
                num3 += 0.55f * Time.deltaTime;
            }
            if (input.IsHandAdjustDownPressed)
            {
                num3 -= 0.55f * Time.deltaTime;
            }
            if (Mathf.Abs(num2) > 0.0001f || Mathf.Abs(num3) > 0.0001f)
            {
                if (this._leftSelectedToggle)
                {
                    this._left.OffsetX += num2;
                    this._left.OffsetY += num3;
                    this._left.Clamp();
                }
                if (this._rightSelectedToggle)
                {
                    this._right.OffsetX += num2;
                    this._right.OffsetY += num3;
                    this._right.Clamp();
                }
            }
            // Clamp the fallback depth so the star never ends up behind or inside the
            // player's head when hand depth is near zero or negative.
            float lookFallback = Mathf.Max(this._left.Depth, this._right.Depth);
            lookFallback = Mathf.Max(lookFallback, 1.2f);
            Vector3 lookPoint = this._targeter.Update(camera, cameraTransform, input.ReadMousePosition(), cursorLocked, lookFallback);

            bool enhancedGrabActive = FlatscreenCore.RaycastGrabEnabled || FlatscreenCore.GrabbyHandsEnabled;
            bool grabClickedThisFrame = input.IsLeftGrabTogglePressed || input.IsRightGrabTogglePressed;
            bool enhancedGrabConsumedClick = enhancedGrabActive && grabClickedThisFrame
                && this.TryGrabTargetedPickup(game, rightInput, FlatscreenCore.GrabbyHandsEnabled);

            if (!enhancedGrabConsumedClick)
            {
                if (!this.ClimbingModeEnabled && input.IsLeftGrabTogglePressed)
                {
                    this._leftGrabToggle = !this._leftGrabToggle;
                }
                if (!this.ClimbingModeEnabled && input.IsRightGrabTogglePressed)
                {
                    this._rightGrabToggle = !this._rightGrabToggle;
                }
            }
            this.UpdateClimbing(input, cameraTransform);
            bool isTeleporting = input.IsTeleportPressed && !input.HasMoveInput && !input.IsRunPressed;
            bool flag = this.ClimbingModeEnabled ? input.IsLeftGrabPressed : this._leftGrabToggle;
            bool flag2 = this.ClimbingModeEnabled ? input.IsRightGrabPressed : this._rightGrabToggle;
            if (HandEmulator.TelekinesisRightOverride)
            {
                flag2 = true;
            }
            this.UpdateCombat(input, flag, flag2);
            this.UpdateQuickAccess(input);
            HandEmulator.ApplyHand(game, leftInput, this._left, this._leftSelectedToggle, input.IsLeftFacePressed, this._bagAssistActive, this._bagAssistPosition, this._bagAssistRotation, flag, isTeleporting, lookPoint, input, cameraTransform, true, false);
            HandEmulator.ApplyHand(game, rightInput, this._right, this._rightSelectedToggle, input.IsRightFacePressed, false, Vector3.zero, Quaternion.identity, flag2, isTeleporting, lookPoint, input, cameraTransform, false, this.QuickAccessModeActive);
        }

        private static void ApplyHand(GameReflection game, object playerInput, HandEmulator.HandState state, bool selected, bool facePose, bool bagPose, Vector3 bagPosePosition, Quaternion bagPoseRotation, bool isGrabbing, bool isTeleporting, Vector3 lookPoint, DesktopInput input, Transform cameraTransform, bool isLeft, bool quickAccessPose)
        {

            if (playerInput != null)
            {
                if (HandEmulator.TelekinesisRightOverride && !isLeft)
                {
                    state.Position = HandEmulator.TelekinesisRightPosition;
                    state.Rotation = HandEmulator.TelekinesisRightRotation;
                }
                else if (bagPose)
                {
                    state.Position = bagPosePosition;
                    state.Rotation = bagPoseRotation;
                }
                else if (facePose || selected || !state.IsLocked || state.IsClimbingGrabActive)
                {
                    Vector3 vector = state.BaseOffset;
                    vector.z = state.Depth;
                    vector += state.ClimbPoseOffset;
                    vector += state.CombatPoseOffset;
                    Quaternion quaternion = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
                    float num = state.VerticalAngle - state.DefaultVerticalAngle;
                    if (facePose)
                    {
                        vector.y += 0.58f;
                        vector.z += 0.08f;
                        vector.x += (isLeft ? 0.01f : -0.01f);
                        vector.x += state.OffsetX;
                        vector.y += state.OffsetY;
                        state.Position = cameraTransform.TransformPoint(vector);
                        state.Rotation = cameraTransform.rotation * Quaternion.Euler(-8f + num, state.HorizontalAngle, state.RollAngle);
                    }
                    else if (selected)
                    {
                        vector.y += 0.52f;
                        vector.z += 0.24f;
                        vector.x += state.OffsetX;
                        vector.y += state.OffsetY;
                        state.Position = cameraTransform.TransformPoint(vector);
                        state.Rotation = cameraTransform.rotation * Quaternion.Euler(-30f + num, state.HorizontalAngle, state.RollAngle);
                    }
                    else
                    {
                        state.Position = cameraTransform.position + quaternion * vector;
                        state.Rotation = quaternion * Quaternion.Euler(state.VerticalAngle, state.HorizontalAngle, state.RollAngle);
                    }
                }
                Transform inputTargetTransform = game.GetInputTargetTransform(playerInput);
                if (inputTargetTransform != null)
                {
                    Vector3 position = inputTargetTransform.position;
                    inputTargetTransform.position = state.Position;
                    inputTargetTransform.rotation = state.Rotation;
                    object rawInput = game.GetRawInput(playerInput);
                    game.SetRawInputVector3(rawInput, "Velocity", (inputTargetTransform.position - position) / Mathf.Max(Time.deltaTime, 0.0001f));
                    game.SetRawInputVector3(rawInput, "AngularVelocity", Vector3.zero);
                    game.SetRawInputFloat(rawInput, "GrabAxis", isGrabbing ? 1f : 0f);
                    game.SetRawInputFloat(rawInput, "GrabStrength", isGrabbing ? 1f : 0f);
                    game.SetRawInputFloat(rawInput, "SecondaryAxis", 0f);
                    game.SetButton(playerInput, rawInput, "Grab", isGrabbing);
                    game.SetButton(playerInput, rawInput, "HardGrab", isGrabbing);
                    game.SetButton(playerInput, rawInput, "Teleport", isTeleporting);
                    game.SetButton(playerInput, rawInput, "MetaMenu", input.IsMetaMenuPressed);
                    game.SetButton(playerInput, rawInput, "ToggleMetaMenu", input.IsMetaMenuPressed);
                }
            }
        }

        // Token: 0x06000104 RID: 260 RVA: 0x0000A1E8 File Offset: 0x000083E8
        public Vector3 ConsumeClimbBodyTranslation()
        {
            Vector3 climbBodyTranslation = this._climbBodyTranslation;
            this._climbBodyTranslation = Vector3.zero;
            return climbBodyTranslation;
        }

        // Token: 0x06000105 RID: 261 RVA: 0x0000A210 File Offset: 0x00008410
        private void UpdateClimbing(DesktopInput input, Transform cameraTransform)
        {
            if (!this.ClimbingModeEnabled || cameraTransform == null)
            {
                this._left.ResetClimb();
                this._right.ResetClimb();
            }
            else
            {
                Vector3 vector = input.ReadClimbHandVector();
                Vector3 handDelta = vector * 1.55f * this._climbSensitivity * Time.deltaTime;
                int num = 0;
                Vector3 vector2 = Vector3.zero;
                if (input.IsLeftGrabPressed)
                {
                    num++;
                    vector2 -= HandEmulator.TransformClimbDelta(cameraTransform, HandEmulator.UpdateClimbHand(this._left, handDelta));
                }
                else
                {
                    this._left.ResetClimb();
                }
                if (input.IsRightGrabPressed)
                {
                    num++;
                    vector2 -= HandEmulator.TransformClimbDelta(cameraTransform, HandEmulator.UpdateClimbHand(this._right, handDelta));
                }
                else
                {
                    this._right.ResetClimb();
                }
                if (num > 1)
                {
                    vector2 /= (float)num;
                }
                this._climbBodyTranslation = vector2 * 1f;
            }
        }

        // Token: 0x06000106 RID: 262 RVA: 0x0000A330 File Offset: 0x00008530
        private void UpdateCombat(DesktopInput input, bool leftGrab, bool rightGrab)
        {
            if (!input.IsCombatModePressed)
            {
                this._left.ResetCombat();
                this._right.ResetCombat();
            }
            else
            {
                Vector2 vector = input.ReadMouseDelta();
                Vector3 vector2 = new Vector3(vector.x, vector.y, Mathf.Abs(vector.y) * 0.2f) * 0.0035f * this._combatSensitivity;
                bool flag = this._leftSelectedToggle || (!this._rightSelectedToggle && leftGrab);
                bool flag2 = this._rightSelectedToggle || (!this._leftSelectedToggle && rightGrab);
                if (!flag && !flag2)
                {
                    flag2 = true;
                }
                if (flag)
                {
                    this._left.CombatPoseOffset += vector2;
                    this._left.ClampCombat();
                }
                else
                {
                    this._left.ResetCombat();
                }
                if (flag2)
                {
                    this._right.CombatPoseOffset += vector2;
                    this._right.ClampCombat();
                }
                else
                {
                    this._right.ResetCombat();
                }
            }
        }

        // Token: 0x06000107 RID: 263 RVA: 0x0000A470 File Offset: 0x00008670
        private static Vector3 UpdateClimbHand(HandEmulator.HandState state, Vector3 handDelta)
        {
            state.IsClimbingGrabActive = true;
            Vector3 result;
            if (handDelta.sqrMagnitude <= 1E-06f)
            {
                result = Vector3.zero;
            }
            else
            {
                Vector3 climbPoseOffset = state.ClimbPoseOffset;
                state.ClimbPoseOffset += handDelta;
                state.ClampClimb();
                result = state.ClimbPoseOffset - climbPoseOffset;
            }
            return result;
        }

        // Token: 0x06000108 RID: 264 RVA: 0x0000A4D0 File Offset: 0x000086D0
        private static Vector3 TransformClimbDelta(Transform cameraTransform, Vector3 localDelta)
        {
            Quaternion quaternion = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
            return quaternion * localDelta;
        }

        // Token: 0x04000056 RID: 86
        private const float SelectedRaiseOffset = 0.52f;

        // Token: 0x04000057 RID: 87
        private const float SelectedForwardOffset = 0.24f;

        // Token: 0x04000058 RID: 88
        private const float SelectedForwardPitch = -30f;

        // Token: 0x04000059 RID: 89
        private const float FaceRaiseOffset = 0.58f;

        // Token: 0x0400005A RID: 90
        private const float FaceForwardOffset = 0.08f;

        // Token: 0x0400005B RID: 91
        private const float FacePitch = -8f;

        // Token: 0x0400005C RID: 92
        private const float ArrowAdjustSpeed = 0.55f;

        // Token: 0x0400005D RID: 93
        private const float ClimbHandSpeed = 1.55f;

        // Token: 0x0400005E RID: 94
        private const float ClimbBodyMultiplier = 1f;

        // Token: 0x0400005F RID: 95
        private const float CombatMouseSpeed = 0.0035f;

        // Token: 0x04000060 RID: 96
        private const float BagBackOffset = 0.28f;

        // Token: 0x04000061 RID: 97
        private const float BagSideOffset = 0.18f;

        // Token: 0x04000062 RID: 98
        private const float BagHeightOffset = 0.34f;
        // Jean Grey telekinesis override: when set, the right hand target is driven to this
        // world pose every frame instead of the emulator's normal pose (with grab held).
        internal static bool TelekinesisRightOverride;
        internal static Vector3 TelekinesisRightPosition;
        internal static Quaternion TelekinesisRightRotation;
        // Token: 0x04000063 RID: 99
        private readonly HandEmulator.HandState _left = new HandEmulator.HandState(new Vector3(-0.22f, -0.6f, 0.1f), -14f, 18f);

        // Token: 0x04000064 RID: 100
        private readonly HandEmulator.HandState _right = new HandEmulator.HandState(new Vector3(0.22f, -0.6f, 0.1f), 14f, 18f);

        // Token: 0x04000065 RID: 101
        private readonly LookTargeter _targeter = new LookTargeter();

        // Token: 0x04000066 RID: 102
        private object _lastLeftInput;

        // Token: 0x04000067 RID: 103
        private object _lastRightInput;

        // Token: 0x04000068 RID: 104
        private bool _leftSelectedToggle;

        // Token: 0x04000069 RID: 105
        private bool _rightSelectedToggle;

        // Token: 0x0400006A RID: 106
        private bool _leftGrabToggle;

        // Token: 0x0400006B RID: 107
        private bool _rightGrabToggle;

        // Token: 0x0400006C RID: 108
        private bool _bagAssistActive;

        // Token: 0x0400006D RID: 109
        private Vector3 _bagAssistPosition;

        // Token: 0x0400006E RID: 110
        private Quaternion _bagAssistRotation;

        // Token: 0x0400006F RID: 111
        private Vector3 _climbBodyTranslation;

        // Token: 0x04000070 RID: 112
        private float _climbSensitivity = 1f;

        // Token: 0x04000071 RID: 113
        private float _combatSensitivity = 1f;

        // Token: 0x02000009 RID: 9
        private sealed class HandState
        {
            // Token: 0x0600010A RID: 266 RVA: 0x0000A58B File Offset: 0x0000878B
            public HandState(Vector3 baseOffset, float horizontalAngle, float verticalAngle)
            {
                this.BaseOffset = baseOffset;
                this.DefaultHorizontalAngle = horizontalAngle;
                this.DefaultVerticalAngle = verticalAngle;
                this.Reset();
            }
            public Vector3 QuickAccessPoseOffset;

            public void ResetQuickAccess()
            {
                QuickAccessPoseOffset = Vector3.zero;
            }

            public void ClampQuickAccess()
            {
                QuickAccessPoseOffset.x = Mathf.Clamp(QuickAccessPoseOffset.x, -0.5f, 0.5f);
                QuickAccessPoseOffset.y = Mathf.Clamp(QuickAccessPoseOffset.y, -0.4f, 0.4f);
            }
            // Token: 0x0600010B RID: 267 RVA: 0x0000A5B2 File Offset: 0x000087B2
            public void Reset()
            {
                this.IsLocked = false;
                this.ResetClimb();
                this.ResetRestPose();
            }

            // Token: 0x0600010C RID: 268 RVA: 0x0000A5CA File Offset: 0x000087CA
            public void ResetPose()
            {
                this.HorizontalAngle = this.DefaultHorizontalAngle;
                this.VerticalAngle = this.DefaultVerticalAngle;
                this.RollAngle = 0f;
                this.OffsetX = 0f;
                this.OffsetY = 0f;
            }

            // Token: 0x0600010D RID: 269 RVA: 0x0000A606 File Offset: 0x00008806
            public void ResetRestPose()
            {
                this.Depth = this.BaseOffset.z;
                this.ResetPose();
            }

            // Token: 0x0600010E RID: 270 RVA: 0x0000A621 File Offset: 0x00008821
            public void ResetPositionOnly()
            {
                this.Depth = this.BaseOffset.z;
                this.OffsetX = 0f;
                this.OffsetY = 0f;
                this.ResetClimb();
            }

            // Token: 0x0600010F RID: 271 RVA: 0x0000A654 File Offset: 0x00008854
            public void Clamp()
            {
                this.Depth = Mathf.Clamp(this.Depth, -0.15f, 1.6f);
                this.OffsetX = Mathf.Clamp(this.OffsetX, -0.45f, 0.45f);
                this.OffsetY = Mathf.Clamp(this.OffsetY, -0.35f, 0.55f);
                this.HorizontalAngle = Mathf.Clamp(this.HorizontalAngle, -180f, 180f);
                this.VerticalAngle = Mathf.Clamp(this.VerticalAngle, -180f, 180f);
                this.RollAngle = Mathf.Clamp(this.RollAngle, -180f, 180f);
            }

            // Token: 0x06000110 RID: 272 RVA: 0x0000A704 File Offset: 0x00008904
            public void ResetClimb()
            {
                this.ClimbPoseOffset = Vector3.zero;
                this.IsClimbingGrabActive = false;
            }

            // Token: 0x06000111 RID: 273 RVA: 0x0000A719 File Offset: 0x00008919
            public void ResetCombat()
            {
                this.CombatPoseOffset = Vector3.zero;
            }

            // Token: 0x06000112 RID: 274 RVA: 0x0000A728 File Offset: 0x00008928
            public void ClampClimb()
            {
                this.ClimbPoseOffset.x = Mathf.Clamp(this.ClimbPoseOffset.x, -0.85f, 0.85f);
                this.ClimbPoseOffset.y = Mathf.Clamp(this.ClimbPoseOffset.y, -1.25f, 1.25f);
                this.ClimbPoseOffset.z = Mathf.Clamp(this.ClimbPoseOffset.z, -0.35f, 0.35f);
            }

            // Token: 0x06000113 RID: 275 RVA: 0x0000A7A8 File Offset: 0x000089A8
            public void ClampCombat()
            {
                this.CombatPoseOffset.x = Mathf.Clamp(this.CombatPoseOffset.x, -0.75f, 0.75f);
                this.CombatPoseOffset.y = Mathf.Clamp(this.CombatPoseOffset.y, -0.55f, 0.65f);
                this.CombatPoseOffset.z = Mathf.Clamp(this.CombatPoseOffset.z, -0.25f, 0.35f);
            }

            // Token: 0x04000073 RID: 115
            public readonly Vector3 BaseOffset;

            // Token: 0x04000074 RID: 116
            public readonly float DefaultHorizontalAngle;

            // Token: 0x04000075 RID: 117
            public readonly float DefaultVerticalAngle;

            // Token: 0x04000076 RID: 118
            public Vector3 Position;

            // Token: 0x04000077 RID: 119
            public Quaternion Rotation;

            // Token: 0x04000078 RID: 120
            public bool IsLocked;

            // Token: 0x04000079 RID: 121
            public float Depth;

            // Token: 0x0400007A RID: 122
            public float HorizontalAngle;

            // Token: 0x0400007B RID: 123
            public float OffsetX;

            // Token: 0x0400007C RID: 124
            public float OffsetY;

            // Token: 0x0400007D RID: 125
            public float VerticalAngle;

            // Token: 0x0400007E RID: 126
            public float RollAngle;

            // Token: 0x0400007F RID: 127
            public Vector3 ClimbPoseOffset;

            // Token: 0x04000080 RID: 128
            public Vector3 CombatPoseOffset;

            // Token: 0x04000081 RID: 129
            public bool IsClimbingGrabActive;
        }
    }

    // Token: 0x0200000A RID: 10
    internal sealed class LookTargeter
    {
        // Token: 0x1700004D RID: 77
        // (get) Token: 0x06000114 RID: 276 RVA: 0x0000A828 File Offset: 0x00008A28
        // (set) Token: 0x06000115 RID: 277 RVA: 0x0000A83F File Offset: 0x00008A3F
        public Vector3 CurrentPoint { get; private set; }

        // Token: 0x1700004E RID: 78
        // (get) Token: 0x06000116 RID: 278 RVA: 0x0000A848 File Offset: 0x00008A48
        // (set) Token: 0x06000117 RID: 279 RVA: 0x0000A85F File Offset: 0x00008A5F
        public bool HasTarget { get; private set; }

        // Token: 0x1700004F RID: 79
        // (get) Token: 0x06000118 RID: 280 RVA: 0x0000A868 File Offset: 0x00008A68
        // (set) Token: 0x06000119 RID: 281 RVA: 0x0000A87F File Offset: 0x00008A7F
        public object CurrentPickup { get; private set; }

        // Token: 0x17000050 RID: 80
        // (get) Token: 0x0600011A RID: 282 RVA: 0x0000A888 File Offset: 0x00008A88
        // (set) Token: 0x0600011B RID: 283 RVA: 0x0000A89F File Offset: 0x00008A9F
        public object CurrentInteractable { get; private set; }

        // Token: 0x17000051 RID: 81
        // (get) Token: 0x0600011C RID: 284 RVA: 0x0000A8A8 File Offset: 0x00008AA8
        // (set) Token: 0x0600011D RID: 285 RVA: 0x0000A8BF File Offset: 0x00008ABF
        public object CurrentMenuTarget { get; private set; }

        // Token: 0x17000052 RID: 82
        // (get) Token: 0x0600011E RID: 286 RVA: 0x0000A8C8 File Offset: 0x00008AC8
        // (set) Token: 0x0600011F RID: 287 RVA: 0x0000A8DF File Offset: 0x00008ADF
        public string CurrentColliderName { get; private set; }

        // Token: 0x17000053 RID: 83
        // (get) Token: 0x06000120 RID: 288 RVA: 0x0000A8E8 File Offset: 0x00008AE8
        // (set) Token: 0x06000121 RID: 289 RVA: 0x0000A8FF File Offset: 0x00008AFF
        public string CurrentInteractableName { get; private set; }

        // Token: 0x17000054 RID: 84
        // (get) Token: 0x06000122 RID: 290 RVA: 0x0000A908 File Offset: 0x00008B08
        // (set) Token: 0x06000123 RID: 291 RVA: 0x0000A91F File Offset: 0x00008B1F
        public string CurrentMenuTargetName { get; private set; }

        // Token: 0x17000055 RID: 85
        // (get) Token: 0x06000124 RID: 292 RVA: 0x0000A928 File Offset: 0x00008B28
        // (set) Token: 0x06000125 RID: 293 RVA: 0x0000A93F File Offset: 0x00008B3F
        public string CurrentParentComponents { get; private set; }

        // Token: 0x06000126 RID: 294 RVA: 0x0000A948 File Offset: 0x00008B48
        public Vector3 Update(Camera camera, Transform cameraTransform, Vector2 pointerPosition, bool cursorLocked, float fallbackDistance)
        {
            // Never place the star closer than 1.2 m — prevents it ending up inside the head
            // when hand depth is zero or negative (e.g. walking backwards, QAM open, etc.).
            fallbackDistance = Mathf.Max(fallbackDistance, 1.2f);
            Vector3 vector = cameraTransform.position + cameraTransform.forward * fallbackDistance;
            this.HasTarget = false;
            this.CurrentPickup = null;
            this.CurrentInteractable = null;
            this.CurrentMenuTarget = null;
            this.CurrentColliderName = "none";
            this.CurrentInteractableName = "none";
            this.CurrentMenuTargetName = "none";
            this.CurrentParentComponents = "none";
            this.CurrentPoint = vector;
            this.EnsurePhysics();
            Vector3 result;
            if (this._raycastMethod == null || this._raycastHitType == null)
            {
                this.SetMarker(false, vector);
                result = vector;
            }
            else
            {
                Ray ray = (camera != null) ? camera.ScreenPointToRay(cursorLocked ? new Vector2((float)Screen.width * 0.5f, (float)Screen.height * 0.5f) : pointerPosition) : new Ray(cameraTransform.position, cameraTransform.forward);
                this.FindPickupFromRaycastAll(ray);
                object obj = Activator.CreateInstance(this._raycastHitType);
                // Exclude the player's own character layers (8, 9, 10) so the ray never
                // hits the local player's body and places the star inside their head.
                int layerMask = ~((1 << 8) | (1 << 9) | (1 << 10));
                object[] array = new object[]
                {
                    ray,
                    obj,
                    20f,
                    layerMask
                };
                try
                {
                    if ((bool)this._raycastMethod.Invoke(null, array))
                    {
                        object obj2 = (this._hitPointProperty == null) ? null : this._hitPointProperty.GetValue(array[1], null);
                        if (obj2 is Vector3)
                        {
                            this.HasTarget = true;
                            this.CurrentPoint = (Vector3)obj2;
                            this.FindTargets(array[1]);
                        }
                    }
                }
                catch
                {
                    this.HasTarget = false;
                    this.CurrentPoint = vector;
                }
                this.SetMarker(this.HasTarget, this.CurrentPoint);
                result = this.CurrentPoint;
            }
            return result;
        }

        private Renderer _markerRenderer;
        private static readonly Color StarBlack = new Color(0.02f, 0.02f, 0.02f, 1f);
        private static readonly Color StarYellow = new Color(1f, 0.85f, 0.1f, 1f);
        private static readonly Color StarYellowEmission = StarYellow * 0.35f; // "a tad" of light, not blown out
        private static Mesh _starMesh;

        private void SetMarker(bool visible, Vector3 position)
        {
            if (this._marker == null)
            {
                this._marker = new GameObject("Flatscreen Look Target");
                this._marker.layer = 2;
                Object.DontDestroyOnLoad(this._marker);

                if (_starMesh == null)
                    _starMesh = BuildPuffyStarMesh(outerRadius: 0.5f, innerRadius: 0.22f, points: 5, depth: 0.55f, ringSlices: 6);

                var meshFilter = this._marker.AddComponent<MeshFilter>();
                meshFilter.mesh = _starMesh;

                var meshRenderer = this._marker.AddComponent<MeshRenderer>();
                var shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
                var mat = new Material(shader);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", Color.black);
                }
                mat.color = StarBlack;
                meshRenderer.material = mat;

                _markerRenderer = meshRenderer;
            }

            this._marker.SetActive(visible);
            if (visible)
            {
                this._marker.transform.position = position;
                this._marker.transform.localScale = Vector3.one * 0.11f; // doubled from 0.055f

                if (_markerRenderer != null)
                {
                    bool highlighted = this.CurrentPickup != null;
                    var mat = _markerRenderer.material;
                    mat.color = highlighted ? StarYellow : StarBlack;
                    if (mat.HasProperty("_EmissionColor"))
                        mat.SetColor("_EmissionColor", highlighted ? StarYellowEmission : Color.black);
                }
            }
        }

        // Builds a rounded/"puffy" star: interior cross-section rings taper from a point
        // at the front, bulge to full size in the middle, and taper back to a point at
        // the back — giving a lens-like plump shape instead of a flat extruded prism.
        private static Mesh BuildPuffyStarMesh(float outerRadius, float innerRadius, int points, float depth, int ringSlices = 6)
        {
            int vertCount2D = points * 2;
            Vector3[] dir2D = new Vector3[vertCount2D];
            bool[] isOuter = new bool[vertCount2D];
            float angleStep = Mathf.PI * 2f / vertCount2D;

            for (int i = 0; i < vertCount2D; i++)
            {
                float angle = i * angleStep - Mathf.PI / 2f; // start pointing up
                dir2D[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                isOuter[i] = (i % 2 == 0);
            }

            float halfDepth = depth * 0.5f;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();

            // front apex (tip point)
            int frontApexIndex = vertices.Count;
            vertices.Add(new Vector3(0, 0, -halfDepth));

            // interior rings, tapering out then back in (sine bulge profile = rounded, not flat)
            int[][] ringIndices = new int[ringSlices][];
            for (int s = 0; s < ringSlices; s++)
            {
                float u = (s + 1f) / (ringSlices + 1f); // 0 < u < 1, excludes the apex points
                float z = Mathf.Lerp(-halfDepth, halfDepth, u);
                float taper = Mathf.Sin(u * Mathf.PI); // 0 at ends, 1 at middle = rounded bulge

                ringIndices[s] = new int[vertCount2D];
                for (int i = 0; i < vertCount2D; i++)
                {
                    float r = (isOuter[i] ? outerRadius : innerRadius) * taper;
                    ringIndices[s][i] = vertices.Count;
                    vertices.Add(new Vector3(dir2D[i].x * r, dir2D[i].y * r, z));
                }
            }

            // back apex (tip point)
            int backApexIndex = vertices.Count;
            vertices.Add(new Vector3(0, 0, halfDepth));

            // front apex fan -> first ring
            for (int i = 0; i < vertCount2D; i++)
            {
                int a = ringIndices[0][i];
                int b = ringIndices[0][(i + 1) % vertCount2D];
                triangles.Add(frontApexIndex); triangles.Add(b); triangles.Add(a);
            }

            // connect adjacent rings with quads
            for (int s = 0; s < ringSlices - 1; s++)
            {
                for (int i = 0; i < vertCount2D; i++)
                {
                    int a = ringIndices[s][i];
                    int b = ringIndices[s][(i + 1) % vertCount2D];
                    int c = ringIndices[s + 1][i];
                    int d = ringIndices[s + 1][(i + 1) % vertCount2D];

                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
            }

            // last ring -> back apex fan
            int last = ringSlices - 1;
            for (int i = 0; i < vertCount2D; i++)
            {
                int a = ringIndices[last][i];
                int b = ringIndices[last][(i + 1) % vertCount2D];
                triangles.Add(backApexIndex); triangles.Add(a); triangles.Add(b);
            }

            var mesh = new Mesh { name = "PuffyStarMesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
        // Token: 0x06000128 RID: 296 RVA: 0x0000AC4C File Offset: 0x00008E4C
        private void EnsurePhysics()
        {
            if (!(this._raycastMethod != null) && !(this._physicsType != null))
            {
                this.TryFindPhysicsTypes();
                if (this._physicsType == null || this._raycastHitType == null)
                {
                    try
                    {
                        Assembly.Load("UnityEngine.PhysicsModule");
                    }
                    catch
                    {
                    }
                    this.TryFindPhysicsTypes();
                }
                if (!(this._physicsType == null) && !(this._raycastHitType == null))
                {
                    this._hitPointProperty = this._raycastHitType.GetProperty("point", BindingFlags.Instance | BindingFlags.Public);
                    this._hitColliderProperty = this._raycastHitType.GetProperty("collider", BindingFlags.Instance | BindingFlags.Public);
                    MethodInfo[] methods = this._physicsType.GetMethods(BindingFlags.Static | BindingFlags.Public);
                    foreach (MethodInfo methodInfo in methods)
                    {
                        ParameterInfo[] parameters = methodInfo.GetParameters();
                        // Prefer the 4-param overload Raycast(Ray, out RaycastHit, float, int)
                        // so we can pass a layer mask and skip the player's own body colliders.
                        if (methodInfo.Name == "Raycast" && parameters.Length == 4
                            && parameters[0].ParameterType == typeof(Ray)
                            && parameters[1].ParameterType.IsByRef
                            && parameters[1].ParameterType.GetElementType() == this._raycastHitType
                            && parameters[2].ParameterType == typeof(float)
                            && parameters[3].ParameterType == typeof(int))
                        {
                            this._raycastMethod = methodInfo;
                        }
                        if (methodInfo.Name == "RaycastAll" && parameters.Length == 3 && parameters[0].ParameterType == typeof(Ray) && parameters[1].ParameterType == typeof(float) && parameters[2].ParameterType == typeof(int))
                        {
                            this._raycastAllMethod = methodInfo;
                        }
                    }
                }
            }
        }

        // Token: 0x06000129 RID: 297 RVA: 0x0000AE74 File Offset: 0x00009074
        private void FindPickupFromRaycastAll(Ray ray)
        {
            if (!(this._raycastAllMethod == null))
            {
                try
                {
                    Array array = this._raycastAllMethod.Invoke(null, new object[]
                    {
                        ray,
                        10f,
                        -1
                    }) as Array;
                    if (array != null)
                    {
                        foreach (object hit in array)
                        {
                            object obj = this.FindPickup(hit);
                            if (obj != null && !LookTargeter.IsHeldObject(obj))
                            {
                                this.CurrentPickup = obj;
                                Component component = obj as Component;
                                if (component != null)
                                {
                                    this.CurrentInteractableName = component.name + " / Pickup";
                                }
                                break;
                            }
                        }
                    }
                }
                catch
                {
                }
            }
        }

        // Token: 0x0600012A RID: 298 RVA: 0x0000AFB4 File Offset: 0x000091B4
        private object FindPickup(object hit)
        {
            Component component = (this._hitColliderProperty == null) ? null : (this._hitColliderProperty.GetValue(hit, null) as Component);
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
                    if (monoBehaviour != null && LookTargeter.IsTypeOrBaseNamed(monoBehaviour.GetType(), "Pickup") && !LookTargeter.IsHeldObject(monoBehaviour))
                    {
                        return monoBehaviour;
                    }
                }
                result = null;
            }
            return result;
        }

        // Token: 0x0600012B RID: 299 RVA: 0x0000B060 File Offset: 0x00009260
        private void FindTargets(object hit)
        {
            Component component = (this._hitColliderProperty == null) ? null : (this._hitColliderProperty.GetValue(hit, null) as Component);
            if (!(component == null))
            {
                this.CurrentColliderName = component.name;
                this.CurrentParentComponents = string.Empty;
                MonoBehaviour[] componentsInParent = component.GetComponentsInParent<MonoBehaviour>(true);
                foreach (MonoBehaviour monoBehaviour in componentsInParent)
                {
                    if (!(monoBehaviour == null))
                    {
                        if (this.CurrentParentComponents.Length < 180)
                        {
                            if (this.CurrentParentComponents.Length > 0)
                            {
                                this.CurrentParentComponents += " > ";
                            }
                            this.CurrentParentComponents += monoBehaviour.GetType().Name;
                        }
                        if (monoBehaviour.GetType().Name == "Interactable" && !LookTargeter.IsHeldObject(monoBehaviour))
                        {
                            this.CurrentInteractableName = monoBehaviour.name;
                            this.CurrentInteractable = monoBehaviour;
                            break;
                        }
                        if (this.CurrentPickup == null && LookTargeter.IsTypeOrBaseNamed(monoBehaviour.GetType(), "Pickup") && !LookTargeter.IsHeldObject(monoBehaviour))
                        {
                            this.CurrentPickup = monoBehaviour;
                            this.CurrentInteractableName = monoBehaviour.name + " / Pickup";
                            this.CurrentInteractable = monoBehaviour;
                            break;
                        }
                        if (LookTargeter.IsMenuTarget(monoBehaviour))
                        {
                            this.CurrentMenuTarget = monoBehaviour;
                            this.CurrentMenuTargetName = monoBehaviour.name + " / " + monoBehaviour.GetType().Name;
                        }
                        object obj = LookTargeter.FindReferencedInteractable(monoBehaviour);
                        if (obj != null && !LookTargeter.IsHeldObject(obj))
                        {
                            Component component2 = obj as Component;
                            this.CurrentInteractableName = ((component2 == null) ? obj.GetType().Name : component2.name);
                            this.CurrentInteractable = obj;
                            break;
                        }
                    }
                }
            }
        }

        // Token: 0x0600012C RID: 300 RVA: 0x0000B29C File Offset: 0x0000949C
        private static bool IsMenuTarget(MonoBehaviour behaviour)
        {
            string name = behaviour.GetType().Name;
            string text = name;
            int num;
            if (text == null || (!(text == "CaptainsWheel") && !(text == "WheelGrab") && !(text == "ServerSelectionMenu")))
            {
                num = ((name == "VrMainMenu") ? 1 : 0);
            }
            else
            {
                num = 1;
            }
            return (byte)num != 0;
        }

        // Token: 0x0600012D RID: 301 RVA: 0x0000B30C File Offset: 0x0000950C
        private static object FindReferencedInteractable(object component)
        {
            Type type = component.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (FieldInfo fieldInfo in fields)
            {
                if (LookTargeter.CouldBeInteractableReference(fieldInfo.Name, fieldInfo.FieldType))
                {
                    object value = LookTargeter.SafeGetField(fieldInfo, component);
                    object obj = LookTargeter.ResolveInteractable(value);
                    if (obj != null)
                    {
                        return obj;
                    }
                }
            }
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (PropertyInfo propertyInfo in properties)
            {
                if (propertyInfo.CanRead && propertyInfo.GetIndexParameters().Length == 0 && LookTargeter.CouldBeInteractableReference(propertyInfo.Name, propertyInfo.PropertyType))
                {
                    object value = LookTargeter.SafeGetProperty(propertyInfo, component);
                    object obj = LookTargeter.ResolveInteractable(value);
                    if (obj != null)
                    {
                        return obj;
                    }
                }
            }
            return null;
        }

        // Token: 0x0600012E RID: 302 RVA: 0x0000B424 File Offset: 0x00009624
        private static bool CouldBeInteractableReference(string memberName, Type memberType)
        {
            string text = (memberName == null) ? string.Empty : memberName.ToLowerInvariant();
            int num;
            if (!LookTargeter.IsInteractableLikeType(memberType) && !text.Contains("interactable"))
            {
                string text2 = text;
                if (text2 != null)
                {
                    if (text2 == "pickup" || text2 == "handle" || text2 == "targetpickup")
                    {
                        goto IL_BA;
                    }
                }
                if (!text.Contains("slot") && !text.Contains("bag") && !text.Contains("pouch") && !text.Contains("container"))
                {
                    num = (text.Contains("inventory") ? 1 : 0);
                    goto IL_BE;
                }
            }
        IL_BA:
            num = 1;
        IL_BE:
            return (byte)num != 0;
        }

        // Token: 0x0600012F RID: 303 RVA: 0x0000B4FC File Offset: 0x000096FC
        private static object ResolveInteractable(object value)
        {
            object result;
            if (value == null)
            {
                result = null;
            }
            else if (LookTargeter.IsInteractableLikeType(value.GetType()))
            {
                result = value;
            }
            else
            {
                Component component = value as Component;
                if (component != null)
                {
                    MonoBehaviour[] componentsInParent = component.GetComponentsInParent<MonoBehaviour>(true);
                    foreach (MonoBehaviour monoBehaviour in componentsInParent)
                    {
                        if (monoBehaviour != null && LookTargeter.IsInteractableLikeType(monoBehaviour.GetType()))
                        {
                            return monoBehaviour;
                        }
                    }
                }
                result = null;
            }
            return result;
        }

        // Token: 0x06000130 RID: 304 RVA: 0x0000B5AC File Offset: 0x000097AC
        private static bool IsInteractableLikeType(Type type)
        {
            return LookTargeter.IsTypeOrBaseNamed(type, "Interactable") || LookTargeter.TypeNameContains(type, "slot") || LookTargeter.TypeNameContains(type, "bag") || LookTargeter.TypeNameContains(type, "pouch") || LookTargeter.TypeNameContains(type, "container") || LookTargeter.TypeNameContains(type, "inventory");
        }

        // Token: 0x06000131 RID: 305 RVA: 0x0000B610 File Offset: 0x00009810
        private static bool IsTypeOrBaseNamed(Type type, string name)
        {
            while (type != null)
            {
                if (type.Name == name)
                {
                    return true;
                }
                type = type.BaseType;
            }
            return false;
        }

        // Token: 0x06000132 RID: 306 RVA: 0x0000B654 File Offset: 0x00009854
        private static bool TypeNameContains(Type type, string text)
        {
            while (type != null)
            {
                string name = type.Name;
                if (!string.IsNullOrEmpty(name) && name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
                type = type.BaseType;
            }
            return false;
        }

        // Token: 0x06000133 RID: 307 RVA: 0x0000B6A8 File Offset: 0x000098A8
        private static bool IsHeldObject(object target)
        {
            bool result;
            if (target == null)
            {
                result = false;
            }
            else
            {
                string[] array = new string[]
                {
                    "IsHeld",
                    "Held",
                    "IsGrabbed",
                    "Grabbed",
                    "InHand",
                    "IsInteracting"
                };
                for (int i = 0; i < array.Length; i++)
                {
                    bool? flag = LookTargeter.ReadBoolMember(target, array[i]);
                    if (flag != null && flag.Value)
                    {
                        return true;
                    }
                }
                string[] array2 = new string[]
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
                for (int i = 0; i < array2.Length; i++)
                {
                    object obj = LookTargeter.ReadObjectMember(target, array2[i]);
                    if (obj != null)
                    {
                        string[] array3 = new string[]
                        {
                            "IsLocal",
                            "IsLocalPlayer",
                            "IsOwner",
                            "IsOwned",
                            "HasAuthority",
                            "IsMine"
                        };
                        bool flag2 = false;
                        for (int j = 0; j < array3.Length; j++)
                        {
                            bool? flag3 = LookTargeter.ReadBoolMember(obj, array3[j]);
                            if (flag3 != null)
                            {
                                flag2 = true;
                                if (!flag3.Value)
                                {
                                    return true;
                                }
                            }
                        }
                        if (flag2)
                        {
                            return false;
                        }
                    }
                }
                result = false;
            }
            return result;
        }

        // Token: 0x06000134 RID: 308 RVA: 0x0000B890 File Offset: 0x00009A90
        private static bool? ReadBoolMember(object instance, string name)
        {
            bool? result;
            if (instance == null)
            {
                result = null;
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
                            return new bool?((bool)value);
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
                            return new bool?((bool)value);
                        }
                    }
                    catch
                    {
                    }
                }
                result = null;
            }
            return result;
        }

        // Token: 0x06000135 RID: 309 RVA: 0x0000B994 File Offset: 0x00009B94
        private static object ReadObjectMember(object instance, string name)
        {
            object result;
            if (instance == null)
            {
                result = null;
            }
            else
            {
                Type type = instance.GetType();
                PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.CanRead)
                {
                    try
                    {
                        return property.GetValue(instance, null);
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
                        return field.GetValue(instance);
                    }
                    catch
                    {
                    }
                }
                result = null;
            }
            return result;
        }

        // Token: 0x06000136 RID: 310 RVA: 0x0000BA44 File Offset: 0x00009C44
        private static object SafeGetField(FieldInfo field, object instance)
        {
            object result;
            try
            {
                result = field.GetValue(instance);
            }
            catch
            {
                result = null;
            }
            return result;
        }

        // Token: 0x06000137 RID: 311 RVA: 0x0000BA78 File Offset: 0x00009C78
        private static object SafeGetProperty(PropertyInfo property, object instance)
        {
            object result;
            try
            {
                result = property.GetValue(instance, null);
            }
            catch
            {
                result = null;
            }
            return result;
        }

        // Token: 0x06000138 RID: 312 RVA: 0x0000BAAC File Offset: 0x00009CAC
        private void TryFindPhysicsTypes()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                if (this._physicsType == null)
                {
                    this._physicsType = assembly.GetType("UnityEngine.Physics");
                }
                if (this._raycastHitType == null)
                {
                    this._raycastHitType = assembly.GetType("UnityEngine.RaycastHit");
                }
            }
        }

        // Token: 0x04000082 RID: 130
        private const float MaxDistance = 4f;

        // Token: 0x04000083 RID: 131
        private Type _physicsType;

        // Token: 0x04000084 RID: 132
        private Type _raycastHitType;

        // Token: 0x04000085 RID: 133
        private MethodInfo _raycastMethod;

        // Token: 0x04000086 RID: 134
        private MethodInfo _raycastAllMethod;

        // Token: 0x04000087 RID: 135
        private PropertyInfo _hitPointProperty;

        // Token: 0x04000088 RID: 136
        private PropertyInfo _hitColliderProperty;

        // Token: 0x04000089 RID: 137
        private GameObject _marker;
    }

    // Token: 0x0200000D RID: 13
    internal sealed class ServerBrowserOverlay
    {
        // Token: 0x17000056 RID: 86
        // (get) Token: 0x06000141 RID: 321 RVA: 0x0000BE94 File Offset: 0x0000A094
        public string Status
        {
            get
            {
                return this._status;
            }
        }

        // Token: 0x17000057 RID: 87
        // (get) Token: 0x06000142 RID: 322 RVA: 0x0000BEAC File Offset: 0x0000A0AC
        public int Count
        {
            get
            {
                return this._servers.Count;
            }
        }

        // Token: 0x06000143 RID: 323 RVA: 0x0000BECC File Offset: 0x0000A0CC
        public void Draw(GameReflection game, HandEmulator hands)
        {
            GUILayout.BeginArea(new Rect(18f, 12f, 900f, 720f), GUI.skin.box);
            this.DrawInline(game, hands);
            GUILayout.EndArea();
        }

        // Same tabs and controls as Draw() above (Servers/Hands/Camera/Actions/System - FOV,
        // sensitivity, third person, height offset, bag toggle, drop items, return to menu,
        // server list/join/refresh), but with no BeginArea/EndArea of its own so it can be
        // embedded inside another layout - e.g. a GUILayout.BeginScrollView on the TavernFun
        // PanKake panel.
        public void DrawInline(GameReflection game, HandEmulator hands)
        {
            GUILayout.BeginHorizontal(new GUILayoutOption[0]);
            if (ServerBrowserOverlay.TabButton("Servers", this._menuTab == ServerBrowserOverlay.MenuTab.Servers, 132f))
            {
                this._menuTab = ServerBrowserOverlay.MenuTab.Servers;
            }
            if (ServerBrowserOverlay.TabButton("Hands", this._menuTab == ServerBrowserOverlay.MenuTab.Hands, 132f))
            {
                this._menuTab = ServerBrowserOverlay.MenuTab.Hands;
            }
            if (ServerBrowserOverlay.TabButton("Camera", this._menuTab == ServerBrowserOverlay.MenuTab.Camera, 132f))
            {
                this._menuTab = ServerBrowserOverlay.MenuTab.Camera;
            }
            if (ServerBrowserOverlay.TabButton("Actions", this._menuTab == ServerBrowserOverlay.MenuTab.Actions, 132f))
            {
                this._menuTab = ServerBrowserOverlay.MenuTab.Actions;
            }
            if (ServerBrowserOverlay.TabButton("System", this._menuTab == ServerBrowserOverlay.MenuTab.System, 132f))
            {
                this._menuTab = ServerBrowserOverlay.MenuTab.System;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            switch (this._menuTab)
            {
                case ServerBrowserOverlay.MenuTab.Servers:
                    this.DrawServers();
                    break;
                case ServerBrowserOverlay.MenuTab.Hands:
                    this.DrawHands(hands);
                    break;
                case ServerBrowserOverlay.MenuTab.Camera:
                    this.DrawMenu(game);
                    break;
                case ServerBrowserOverlay.MenuTab.Actions:
                    this.DrawQuickAccess(game);
                    break;
                case ServerBrowserOverlay.MenuTab.System:
                    this.DrawSystem(game);
                    break;
            }
        }

        // Token: 0x06000144 RID: 324 RVA: 0x0000C08C File Offset: 0x0000A28C
        public void Refresh()
        {
            this._servers.Clear();
            this._selectedIndex = -1;
            MonoBehaviour monoBehaviour = ServerBrowserOverlay.FindMonoBehaviour("ServerSelectionMenu");
            if (monoBehaviour == null)
            {
                this._status = "ServerSelectionMenu not found";
            }
            else
            {
                ServerBrowserOverlay.InvokeNoThrow(monoBehaviour, "RefreshServersList");
                IEnumerable enumerable = ServerBrowserOverlay.GetFieldOrProperty(monoBehaviour, "boards") as IEnumerable;
                if (enumerable != null)
                {
                    foreach (object board in enumerable)
                    {
                        this.AddServersFromBoard(board);
                    }
                }
                object fieldOrProperty = ServerBrowserOverlay.GetFieldOrProperty(monoBehaviour, "CurrentBoard");
                this.AddServersFromBoard(fieldOrProperty);
                this._status = ((this._servers.Count == 0) ? "No servers found yet. Wait a moment and refresh." : ("Loaded " + this._servers.Count + " servers"));
            }
        }

        // Token: 0x06000145 RID: 325 RVA: 0x0000C1AC File Offset: 0x0000A3AC
        private void DrawServers()
        {
            GUILayout.BeginHorizontal(new GUILayoutOption[0]);
            if (GUILayout.Button("Refresh", new GUILayoutOption[]
            {
                GUILayout.Width(120f)
            }))
            {
                this.Refresh();
            }
            if (GUILayout.Button("Join Selected", new GUILayoutOption[]
            {
                GUILayout.Width(140f)
            }))
            {
                this.JoinSelected();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Servers: " + this._servers.Count, new GUILayoutOption[0]);
            this._serverScroll = GUILayout.BeginScrollView(this._serverScroll, new GUILayoutOption[]
            {
                GUILayout.Height(520f)
            });
            for (int i = 0; i < this._servers.Count; i++)
            {
                object instance = this._servers[i];
                string text = ((i == this._selectedIndex) ? "> " : "  ") + ServerBrowserOverlay.GetText(instance, "Name", "Unnamed Server");
                GUILayout.BeginVertical(GUI.skin.box, new GUILayoutOption[0]);
                GUILayout.BeginHorizontal(new GUILayoutOption[0]);
                if (GUILayout.Button(text, new GUILayoutOption[]
                {
                    GUILayout.Width(420f)
                }))
                {
                    this._selectedIndex = i;
                }
                if (GUILayout.Button("Join", new GUILayoutOption[]
                {
                    GUILayout.Width(90f)
                }))
                {
                    this._selectedIndex = i;
                    this.JoinSelected();
                }
                GUILayout.EndHorizontal();
                string text2 = ServerBrowserOverlay.GetText(instance, "Description", string.Empty);
                if (!string.IsNullOrEmpty(text2))
                {
                    GUILayout.Label(text2, new GUILayoutOption[0]);
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
        }

        // Token: 0x06000146 RID: 326 RVA: 0x0000C3AC File Offset: 0x0000A5AC
        private void DrawHands(HandEmulator hands)
        {
            if (hands == null)
            {
                GUILayout.Label("Hand controller not available.", new GUILayoutOption[0]);
            }
            else
            {
                GUILayout.BeginHorizontal(new GUILayoutOption[0]);
                if (ServerBrowserOverlay.TabButton("Left Hand", this._handTab == ServerBrowserOverlay.HandTab.Left))
                {
                    this._handTab = ServerBrowserOverlay.HandTab.Left;
                }
                if (ServerBrowserOverlay.TabButton("Right Hand", this._handTab == ServerBrowserOverlay.HandTab.Right))
                {
                    this._handTab = ServerBrowserOverlay.HandTab.Right;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(6f);
                hands.ClimbingModeEnabled = GUILayout.Toggle(hands.ClimbingModeEnabled, "Climbing Mode", new GUILayoutOption[0]);
                GUILayout.Label("Climb sensitivity: " + hands.ClimbSensitivity.ToString("0.00"), new GUILayoutOption[0]);
                hands.ClimbSensitivity = GUILayout.HorizontalSlider(hands.ClimbSensitivity, 0.25f, 3f, new GUILayoutOption[]
                {
                    GUILayout.Width(500f)
                });
                GUILayout.Label("Climb: hold left/right click, then A/D moves the held hand sideways. W/S still walks forward/back.", new GUILayoutOption[0]);
                GUILayout.Label("Combat sensitivity: " + hands.CombatSensitivity.ToString("0.00"), new GUILayoutOption[0]);
                hands.CombatSensitivity = GUILayout.HorizontalSlider(hands.CombatSensitivity, 0.25f, 3f, new GUILayoutOption[]
                {
                    GUILayout.Width(500f)
                });
                GUILayout.Label("Combat: hold Ctrl and move the mouse to swipe the selected or grabbed hand.", new GUILayoutOption[0]);
                GUILayout.Space(6f);
                GUILayout.Label((this._handTab == ServerBrowserOverlay.HandTab.Left) ? "Left hand" : "Right hand", new GUILayoutOption[0]);
                GUILayout.Label(new GUIContent("Q / E toggles hands", "Q toggles left hand. E toggles right hand. Both can stay active."), new GUILayoutOption[0]);
                GUILayout.Label(new GUIContent("Left click / Right click", "Grab with the selected hand."), new GUILayoutOption[0]);
                GUILayout.Label(new GUIContent("Mouse wheel", "Move the selected hand forward or back."), new GUILayoutOption[0]);
                float num = (this._handTab == ServerBrowserOverlay.HandTab.Left) ? hands.LeftHorizontalAngle : hands.RightHorizontalAngle;
                float num2 = (this._handTab == ServerBrowserOverlay.HandTab.Left) ? hands.LeftVerticalAngle : hands.RightVerticalAngle;
                float num3 = (this._handTab == ServerBrowserOverlay.HandTab.Left) ? hands.LeftDepth : hands.RightDepth;
                float num4 = (this._handTab == ServerBrowserOverlay.HandTab.Left) ? hands.LeftRollAngle : hands.RightRollAngle;
                GUILayout.Label("Horizontal", new GUILayoutOption[0]);
                num = GUILayout.HorizontalSlider(num, -180f, 180f, new GUILayoutOption[]
                {
                    GUILayout.Width(500f)
                });
                GUILayout.Label("Vertical", new GUILayoutOption[0]);
                num2 = GUILayout.HorizontalSlider(num2, -180f, 180f, new GUILayoutOption[]
                {
                    GUILayout.Width(500f)
                });
                GUILayout.Label("Roll", new GUILayoutOption[0]);
                num4 = GUILayout.HorizontalSlider(num4, -180f, 180f, new GUILayoutOption[]
                {
                    GUILayout.Width(500f)
                });
                GUILayout.Label("Depth", new GUILayoutOption[0]);
                num3 = GUILayout.HorizontalSlider(num3, 0.2f, 1.6f, new GUILayoutOption[]
                {
                    GUILayout.Width(500f)
                });
                if (this._handTab == ServerBrowserOverlay.HandTab.Left)
                {
                    hands.SetLeftHorizontalAngle(num);
                    hands.SetLeftVerticalAngle(num2);
                    hands.SetLeftRollAngle(num4);
                    hands.SetLeftDepth(num3);
                }
                else
                {
                    hands.SetRightHorizontalAngle(num);
                    hands.SetRightVerticalAngle(num2);
                    hands.SetRightRollAngle(num4);
                    hands.SetRightDepth(num3);
                }
                GUILayout.BeginHorizontal(new GUILayoutOption[0]);
                if (GUILayout.Button("Palm Down", new GUILayoutOption[]
                {
                    GUILayout.Width(120f)
                }))
                {
                    if (this._handTab == ServerBrowserOverlay.HandTab.Left)
                    {
                        hands.SetLeftPalmDownPose();
                    }
                    else
                    {
                        hands.SetRightPalmDownPose();
                    }
                }
                if (GUILayout.Button("Both Palm Down", new GUILayoutOption[]
                {
                    GUILayout.Width(140f)
                }))
                {
                    hands.SetBothPalmDownPose();
                }
                if (GUILayout.Button("Handshake", new GUILayoutOption[]
                {
                    GUILayout.Width(120f)
                }))
                {
                    hands.SetBothHandshakePose();
                }
                if (GUILayout.Button("Tips Down In", new GUILayoutOption[]
                {
                    GUILayout.Width(130f)
                }))
                {
                    hands.SetBothFingertipsDownInPose();
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal(new GUILayoutOption[0]);
                if (GUILayout.Button("Reset Selected Pose", new GUILayoutOption[]
                {
                    GUILayout.Width(160f)
                }))
                {
                    if (this._handTab == ServerBrowserOverlay.HandTab.Left)
                    {
                        hands.ResetLeftPose();
                    }
                    else
                    {
                        hands.ResetRightPose();
                    }
                }
                if (GUILayout.Button("Reset All Hands", new GUILayoutOption[]
                {
                    GUILayout.Width(150f)
                }))
                {
                    hands.ResetHands();
                }
                GUILayout.EndHorizontal();
            }
        }

        // Token: 0x06000147 RID: 327 RVA: 0x0000C8F8 File Offset: 0x0000AAF8
        private void DrawSystem(GameReflection game)
        {
            GUILayout.Label("System actions", new GUILayoutOption[0]);
            GUILayout.BeginHorizontal(new GUILayoutOption[0]);
            if (GUILayout.Button("Disconnect", new GUILayoutOption[]
            {
                GUILayout.Width(140f)
            }))
            {
                FlatscreenCore.SuppressOpenXRInputUpdates = false;
                this._status = "returning to menu";
                if (FlatscreenCore.Instance != null)
                {
                    FlatscreenCore.Instance.RequestReturnToMenu();
                }
            }
            if (GUILayout.Button("Drop All Held", new GUILayoutOption[]
            {
                GUILayout.Width(130f)
            }))
            {
                if (FlatscreenCore.Instance == null || !FlatscreenCore.Instance.DropAllHeldItems())
                {
                    this._status = ((FlatscreenCore.Instance == null) ? "mod unavailable" : "no held items to drop");
                }
                else
                {
                    this._status = "dropped held items";
                }
            }
            if (GUILayout.Button("Escape Customization", new GUILayoutOption[]
            {
                GUILayout.Width(180f)
            }))
            {
                if (game != null && game.TryEscapeCustomization())
                {
                    this._status = game.LastInteractResult;
                }
                else
                {
                    this._status = ((game == null) ? "game reflection unavailable" : game.LastInteractResult);
                }
            }
            if (GUILayout.Button("Quit Game", new GUILayoutOption[]
            {
                GUILayout.Width(120f)
            }))
            {
                Application.Quit();
                this._status = "Quit requested";
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            if (GUILayout.Button("Refresh Terrain", new GUILayoutOption[]
            {
                GUILayout.Width(160f)
            }) && FlatscreenCore.Instance != null && !FlatscreenCore.Instance.ToggleTerrainRefresh())
            {
                this._status = "MasterTerrain not found";
            }
            if (GUILayout.Button("Refresh Server List", new GUILayoutOption[]
            {
                GUILayout.Width(180f)
            }))
            {
                this.Refresh();
            }
        }

        // Token: 0x06000148 RID: 328 RVA: 0x0000CB08 File Offset: 0x0000AD08
        private void DrawMenu(GameReflection game)
        {
            GUILayout.BeginHorizontal(new GUILayoutOption[0]);
            if (ServerBrowserOverlay.TabButton("Camera", this._menuSubTab == ServerBrowserOverlay.MenuSubTab.Camera))
            {
                this._menuSubTab = ServerBrowserOverlay.MenuSubTab.Camera;
            }
            if (ServerBrowserOverlay.TabButton("Names", this._menuSubTab == ServerBrowserOverlay.MenuSubTab.Names))
            {
                this._menuSubTab = ServerBrowserOverlay.MenuSubTab.Names;
            }
            if (ServerBrowserOverlay.TabButton("Wheel", this._menuSubTab == ServerBrowserOverlay.MenuSubTab.Wheel))
            {
                this._menuSubTab = ServerBrowserOverlay.MenuSubTab.Wheel;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            switch (this._menuSubTab)
            {
                case ServerBrowserOverlay.MenuSubTab.Camera:
                    this.DrawCameraMenu(game);
                    break;
                case ServerBrowserOverlay.MenuSubTab.Names:
                    this.DrawNameMenu(game);
                    break;
                case ServerBrowserOverlay.MenuSubTab.Wheel:
                    this.DrawWheelMenu(game);
                    break;
            }
        }

        // Token: 0x06000149 RID: 329 RVA: 0x0000CBD8 File Offset: 0x0000ADD8
        private void DrawCameraMenu(GameReflection game)
        {
            GUILayout.Label("Camera", new GUILayoutOption[0]);
            if (FlatscreenCore.Instance != null)
            {
                float cameraFieldOfView = FlatscreenCore.Instance.CameraFieldOfView;
                GUILayout.Label("FOV", new GUILayoutOption[0]);
                float num = GUILayout.HorizontalSlider(cameraFieldOfView, 45f, 110f, new GUILayoutOption[]
                {
                    GUILayout.Width(320f)
                });
                if (Mathf.Abs(num - cameraFieldOfView) > 0.01f)
                {
                    FlatscreenCore.Instance.SetCameraFieldOfView(num);
                    this._status = "FOV " + num.ToString("0");
                }
                float lookSensitivity = FlatscreenCore.Instance.LookSensitivity;
                GUILayout.Label("Look sensitivity: " + lookSensitivity.ToString("0.00"), new GUILayoutOption[0]);
                float num2 = GUILayout.HorizontalSlider(lookSensitivity, 0.25f, 3f, new GUILayoutOption[]
                {
                    GUILayout.Width(320f)
                });
                if (Mathf.Abs(num2 - lookSensitivity) > 0.01f)
                {
                    FlatscreenCore.Instance.SetLookSensitivity(num2);
                    this._status = "Look sensitivity " + num2.ToString("0.00");
                }
            }
            GUILayout.BeginHorizontal(new GUILayoutOption[0]);
            if (GUILayout.Button("Screenshot Camera", new GUILayoutOption[]
            {
                GUILayout.Width(150f)
            }))
            {
                if (!game.TryRunQuickAccessAction(new string[]
                {
                    "SummonCameraQuickAccess",
                    "ScreenshotCameraQuickAccess",
                    "CameraQuickAccess"
                }, new string[]
                {
                    "Summon Camera Action",
                    "Screenshot Camera",
                    "Screenshot"
                }, new string[]
                {
                    "Open"
                }, new string[]
                {
                    "Run"
                }))
                {
                    this._status = game.LastInteractResult;
                }
                else
                {
                    this._status = game.LastInteractResult;
                }
            }
            if (GUILayout.Button("Enable Filters", new GUILayoutOption[]
            {
                GUILayout.Width(130f)
            }))
            {
                if (!game.TryRunQuickAccessAction(new string[]
                {
                    "CameraFilter",
                    "ScreenshotCameraQuickAccess",
                    "PhotoFilter",
                    "Filter"
                }, new string[]
                {
                    "Enable Filters",
                    "Filters",
                    "Filter"
                }, new string[]
                {
                    "Open",
                    "Activate"
                }, new string[]
                {
                    "Run",
                    "Apply",
                    "Enable",
                    "Toggle"
                }))
                {
                    this._status = game.LastInteractResult;
                }
                else
                {
                    this._status = game.LastInteractResult;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            GUILayout.Label("View", new GUILayoutOption[0]);
            GUILayout.BeginHorizontal(new GUILayoutOption[0]);
            if (GUILayout.Button((FlatscreenCore.Instance != null && FlatscreenCore.Instance.ThirdPersonEnabled) ? "Third Person On" : "Third Person Off", new GUILayoutOption[]
            {
                GUILayout.Width(150f)
            }) && FlatscreenCore.Instance != null)
            {
                FlatscreenCore.Instance.ToggleThirdPerson();
                this._status = (FlatscreenCore.Instance.ThirdPersonEnabled ? "Third person enabled" : "Third person disabled");
            }
            if (GUILayout.Button("Open Bag", new GUILayoutOption[]
            {
                GUILayout.Width(110f)
            }))
            {
                if (FlatscreenCore.Instance == null || !FlatscreenCore.Instance.TryToggleBag())
                {
                    this._status = game.LastInteractResult;
                }
                else
                {
                    this._status = "bag assist triggered";
                }
            }
            GUILayout.EndHorizontal();
            if (FlatscreenCore.Instance != null)
            {
                float thirdPersonDistance = FlatscreenCore.Instance.ThirdPersonDistance;
                GUILayout.Label(new GUIContent("Third-person distance", "V toggles third person. I opens the bag."), new GUILayoutOption[0]);
                float num3 = GUILayout.HorizontalSlider(thirdPersonDistance, 0.8f, 4.5f, new GUILayoutOption[]
                {
                    GUILayout.Width(320f)
                });
                if (Mathf.Abs(num3 - thirdPersonDistance) > 0.01f)
                {
                    FlatscreenCore.Instance.SetThirdPersonDistance(num3);
                    this._status = "Third-person distance " + num3.ToString("0.0");
                }
            }
            GUILayout.BeginHorizontal(new GUILayoutOption[0]);
            if (GUILayout.Button("Reset Height", new GUILayoutOption[]
            {
                GUILayout.Width(120f)
            }) && FlatscreenCore.Instance != null)
            {
                FlatscreenCore.Instance.ResetHeightOffset();
                this._status = "Height reset";
            }
            if (GUILayout.Button("Lower", new GUILayoutOption[]
            {
                GUILayout.Width(80f)
            }) && FlatscreenCore.Instance != null)
            {
                FlatscreenCore.Instance.SetHeightOffset(FlatscreenCore.Instance.HeightOffset - 0.15f);
                this._status = "Height lowered";
            }
            if (GUILayout.Button("Raise", new GUILayoutOption[]
            {
                GUILayout.Width(80f)
            }) && FlatscreenCore.Instance != null)
            {
                FlatscreenCore.Instance.SetHeightOffset(FlatscreenCore.Instance.HeightOffset + 0.15f);
                this._status = "Height raised";
            }
            GUILayout.EndHorizontal();
        }

        // Token: 0x0600014A RID: 330 RVA: 0x0000D1D4 File Offset: 0x0000B3D4
        private void DrawQuickAccess(GameReflection game)
        {
            GUILayout.Label("Actions", new GUILayoutOption[0]);
            GUILayout.BeginHorizontal(new GUILayoutOption[0]);
            if (GUILayout.Button(this._showNamesState ? "Hide Names" : "Show Names", new GUILayoutOption[]
            {
                GUILayout.Width(120f)
            }))
            {
                this._showNamesState = !this._showNamesState;
                if (!game.TrySetShowNames(this._showNamesState))
                {
                    this._status = game.LastInteractResult;
                }
                else
                {
                    this._status = game.LastInteractResult;
                }
            }
            GUILayout.EndHorizontal();
        }

        // Token: 0x0600014B RID: 331 RVA: 0x0000D27C File Offset: 0x0000B47C
        private void DrawNameMenu(GameReflection game)
        {
            GUILayout.BeginHorizontal(new GUILayoutOption[0]);
            if (GUILayout.Button("Toggle Usernames", new GUILayoutOption[]
            {
                GUILayout.Width(150f)
            }))
            {
                if (!game.TryToggleSceneBoolean(new string[]
                {
                    "Nameplate",
                    "NameTag",
                    "PlayerName",
                    "PlayerNameplate",
                    "NameDisplay"
                }, new string[]
                {
                    "ShowNames",
                    "ShowUsernames",
                    "ShowNameplates",
                    "DisplayNames",
                    "DisplayUsernames"
                }))
                {
                    this._status = game.LastInteractResult;
                }
                else
                {
                    this._status = game.LastInteractResult;
                }
            }
            if (GUILayout.Button("Toggle Name UI", new GUILayoutOption[]
            {
                GUILayout.Width(130f)
            }))
            {
                if (!game.TryInvokeSceneMethod(new string[]
                {
                    "Nameplate",
                    "NameTag",
                    "PlayerName",
                    "PlayerNameplate",
                    "NameDisplay"
                }, new string[]
                {
                    "Toggle",
                    "Show",
                    "Hide",
                    "SetVisible"
                }))
                {
                    this._status = game.LastInteractResult;
                }
                else
                {
                    this._status = game.LastInteractResult;
                }
            }
            GUILayout.EndHorizontal();
        }

        // Token: 0x0600014C RID: 332 RVA: 0x0000D3FC File Offset: 0x0000B5FC
        private void DrawWheelMenu(GameReflection game)
        {
            GUILayout.BeginHorizontal(new GUILayoutOption[0]);
            if (GUILayout.Button("Wheel Left", new GUILayoutOption[]
            {
                GUILayout.Width(100f)
            }) && !this._TryWheelScroll(game, -1f))
            {
                this._status = game.LastInteractResult;
            }
            if (GUILayout.Button("Wheel Right", new GUILayoutOption[]
            {
                GUILayout.Width(100f)
            }) && !this._TryWheelScroll(game, 1f))
            {
                this._status = game.LastInteractResult;
            }
            if (GUILayout.Button("Reset Wheel", new GUILayoutOption[]
            {
                GUILayout.Width(120f)
            }))
            {
                if (!game.TryInvokeSceneMethod(new string[]
                {
                    "CaptainsWheel",
                    "WheelGrab",
                    "ServerSelectionMenu",
                    "VrMainMenu"
                }, new string[]
                {
                    "Reset",
                    "Recenter",
                    "ResetWheel",
                    "ResetPosition",
                    "Home"
                }))
                {
                    this._status = game.LastInteractResult;
                }
                else
                {
                    this._status = game.LastInteractResult;
                }
            }
            GUILayout.EndHorizontal();
        }

        // Token: 0x0600014D RID: 333 RVA: 0x0000D550 File Offset: 0x0000B750
        private bool _TryWheelScroll(GameReflection game, float direction)
        {
            MonoBehaviour monoBehaviour = ServerBrowserOverlay.FindMonoBehaviour("CaptainsWheel");
            bool result;
            if (monoBehaviour != null && game.TryAdjustMenuWheel(monoBehaviour, direction))
            {
                this._status = game.LastInteractResult;
                result = true;
            }
            else
            {
                monoBehaviour = ServerBrowserOverlay.FindMonoBehaviour("WheelGrab");
                if (monoBehaviour != null && game.TryAdjustMenuWheel(monoBehaviour, direction))
                {
                    this._status = game.LastInteractResult;
                    result = true;
                }
                else
                {
                    monoBehaviour = ServerBrowserOverlay.FindMonoBehaviour("ServerSelectionMenu");
                    if (monoBehaviour != null && game.TryAdjustMenuWheel(monoBehaviour, direction))
                    {
                        this._status = game.LastInteractResult;
                        result = true;
                    }
                    else
                    {
                        this._status = "wheel target not found";
                        result = false;
                    }
                }
            }
            return result;
        }

        // Token: 0x0600014E RID: 334 RVA: 0x0000D618 File Offset: 0x0000B818
        private void JoinSelected()
        {
            if (this._selectedIndex < 0 || this._selectedIndex >= this._servers.Count)
            {
                this._status = "Select a server first";
            }
            else
            {
                MonoBehaviour monoBehaviour = ServerBrowserOverlay.FindMonoBehaviour("VrMainMenu");
                if (monoBehaviour == null)
                {
                    this._status = "VrMainMenu not found";
                }
                else
                {
                    MethodInfo method = monoBehaviour.GetType().GetMethod("JoinServer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method == null)
                    {
                        this._status = "JoinServer method not found";
                    }
                    else
                    {
                        try
                        {
                            method.Invoke(monoBehaviour, new object[]
                            {
                                this._servers[this._selectedIndex]
                            });
                            this._status = "Joining " + ServerBrowserOverlay.GetText(this._servers[this._selectedIndex], "Name", "server");
                        }
                        catch (Exception ex)
                        {
                            this._status = "Join failed: " + ex.GetType().Name;
                        }
                    }
                }
            }
        }

        // Token: 0x0600014F RID: 335 RVA: 0x0000D740 File Offset: 0x0000B940
        private void AddServersFromBoard(object board)
        {
            if (board != null)
            {
                this.AddServerEnumerable(ServerBrowserOverlay.GetFieldOrProperty(board, "lastFilteredServers") as IEnumerable);
                this.AddServerEnumerable(ServerBrowserOverlay.GetFieldOrProperty(board, "lastReceivedServers") as IEnumerable);
            }
        }

        // Token: 0x06000150 RID: 336 RVA: 0x0000D788 File Offset: 0x0000B988
        private void AddServerEnumerable(IEnumerable enumerable)
        {
            if (enumerable != null)
            {
                foreach (object obj in enumerable)
                {
                    if (obj != null && !this._servers.Contains(obj))
                    {
                        this._servers.Add(obj);
                    }
                }
            }
        }

        // Token: 0x06000151 RID: 337 RVA: 0x0000D814 File Offset: 0x0000BA14
        private static bool TabButton(string label, bool active)
        {
            return ServerBrowserOverlay.TabButton(label, active, 120f);
        }

        // Token: 0x06000152 RID: 338 RVA: 0x0000D834 File Offset: 0x0000BA34
        private static bool TabButton(string label, bool active, float width)
        {
            GUILayoutOption[] array = active ? new GUILayoutOption[]
            {
                GUILayout.Width(width + 10f)
            } : new GUILayoutOption[]
            {
                GUILayout.Width(width)
            };
            return GUILayout.Button(active ? ("[ " + label + " ]") : label, array);
        }

        // Token: 0x06000153 RID: 339 RVA: 0x0000D894 File Offset: 0x0000BA94
        private static MonoBehaviour FindMonoBehaviour(string typeName)
        {
            MonoBehaviour[] array = Object.FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour monoBehaviour in array)
            {
                if (monoBehaviour != null && monoBehaviour.GetType().Name == typeName)
                {
                    return monoBehaviour;
                }
            }
            return null;
        }

        // Token: 0x06000154 RID: 340 RVA: 0x0000D8FC File Offset: 0x0000BAFC
        private static object GetFieldOrProperty(object instance, string name)
        {
            object result;
            if (instance == null)
            {
                result = null;
            }
            else
            {
                Type type = instance.GetType();
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    try
                    {
                        return field.GetValue(instance);
                    }
                    catch
                    {
                    }
                }
                PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                {
                    try
                    {
                        return property.GetValue(instance, null);
                    }
                    catch
                    {
                    }
                }
                result = null;
            }
            return result;
        }

        // Token: 0x06000155 RID: 341 RVA: 0x0000D9BC File Offset: 0x0000BBBC
        private static string GetText(object instance, string memberName, string fallback)
        {
            object fieldOrProperty = ServerBrowserOverlay.GetFieldOrProperty(instance, memberName);
            return (fieldOrProperty == null) ? fallback : fieldOrProperty.ToString();
        }

        // Token: 0x06000156 RID: 342 RVA: 0x0000D9E4 File Offset: 0x0000BBE4
        private static void InvokeNoThrow(object instance, string methodName)
        {
            if (instance != null)
            {
                MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (!(method == null) && method.GetParameters().Length == 0)
                {
                    try
                    {
                        method.Invoke(instance, null);
                    }
                    catch
                    {
                    }
                }
            }
        }

        // Token: 0x04000093 RID: 147
        private readonly List<object> _servers = new List<object>();

        // Token: 0x04000094 RID: 148
        private Vector2 _serverScroll;

        // Token: 0x04000095 RID: 149
        private ServerBrowserOverlay.MenuTab _menuTab = ServerBrowserOverlay.MenuTab.Servers;

        // Token: 0x04000096 RID: 150
        private ServerBrowserOverlay.MenuSubTab _menuSubTab = ServerBrowserOverlay.MenuSubTab.Camera;

        // Token: 0x04000097 RID: 151
        private ServerBrowserOverlay.HandTab _handTab = ServerBrowserOverlay.HandTab.Left;

        // Token: 0x04000098 RID: 152
        private int _selectedIndex = -1;

        // Token: 0x04000099 RID: 153
        private bool _showNamesState = true;

        // Token: 0x0400009A RID: 154
        private string _status = "Not refreshed yet";

        // Token: 0x0200000E RID: 14
        private enum MenuTab
        {
            // Token: 0x0400009C RID: 156
            Servers,
            // Token: 0x0400009D RID: 157
            Hands,
            // Token: 0x0400009E RID: 158
            Camera,
            // Token: 0x0400009F RID: 159
            Actions,
            // Token: 0x040000A0 RID: 160
            System
        }

        // Token: 0x0200000F RID: 15
        private enum MenuSubTab
        {
            // Token: 0x040000A2 RID: 162
            Camera,
            // Token: 0x040000A3 RID: 163
            Names,
            // Token: 0x040000A4 RID: 164
            Wheel
        }

        // Token: 0x02000010 RID: 16
        private enum HandTab
        {
            // Token: 0x040000A6 RID: 166
            Left,
            // Token: 0x040000A7 RID: 167
            Right
        }
    }
    // NOT a [HarmonyPatch] attribute class — applied manually at runtime so a missing
    // target method never crashes PatchAll and kills every other patch in the mod.
    internal static class AmbienceSoundPatch
    {
        internal static bool MuteAmbience = false;

        private static readonly HashSet<string> BlockedNames = new HashSet<string>
        {
            "DayAmbience", "NightAmbience", "CaveAmbience", "ForestAmbience"
        };

        internal static void TryApply(HarmonyLib.Harmony harmony)
        {
            try
            {
                // Find the sound-manager Play method that takes a single enum/key argument.
                MethodInfo target = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var t in TryGetTypes(asm))
                    {
                        if (t.Name != "AmbienceSoundManager" && t.Name != "SFXManager" && t.Name != "AmbientSoundPlayer") continue;
                        foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            var p = m.GetParameters();
                            if ((m.Name == "Play" || m.Name == "PlayAmbience" || m.Name == "PlaySound") && p.Length == 1)
                            {
                                target = m;
                                break;
                            }
                        }
                        if (target != null) break;
                    }
                    if (target != null) break;
                }
                if (target == null) return;

                var prefix = typeof(AmbienceSoundPatch).GetMethod(nameof(Prefix),
                    BindingFlags.Static | BindingFlags.NonPublic);
                harmony.Patch(target, prefix: new HarmonyLib.HarmonyMethod(prefix));
            }
            catch { }
        }

        private static IEnumerable<Type> TryGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch { return new Type[0]; }
        }

        private static bool Prefix(object key)
        {
            if (!MuteAmbience) return true;
            return !BlockedNames.Contains(key?.ToString() ?? "");
        }
    }
    // Token: 0x0200000C RID: 12
    [HarmonyPatch]
    internal static class CameraStabilizerPatches
    {
        // Token: 0x0600013E RID: 318 RVA: 0x0000BDF8 File Offset: 0x00009FF8
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type type = CameraStabilizerPatches.FindType("CameraStabelizer") ?? CameraStabilizerPatches.FindType("CameraStabilizer");
            if (!(type == null))
            {
                string[] methodNames = new string[]
                {
                    "StabelizeCamera",
                    "StabilizeCamera",
                    "LateUpdate",
                    "Update"
                };
                for (int i = 0; i < methodNames.Length; i++)
                {
                    MethodInfo method = type.GetMethod(methodNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method != null)
                    {
                        yield return method;
                    }
                }
            }
            yield break;
        }
        [HarmonyPatch(typeof(SmoothLocomotion), "MoveToSafety")]
        internal static class NoVoidTpPatch
        {
            private static bool Prefix()
            {
                return !FlatscreenCore.NoVoidTpEnabled;
            }
        }
        // Token: 0x0600013F RID: 319 RVA: 0x0000BE14 File Offset: 0x0000A014
        private static bool Prefix()
        {
            return !FlatscreenCore.SuppressOpenXRInputUpdates;
        }

        // Token: 0x06000140 RID: 320 RVA: 0x0000BE30 File Offset: 0x0000A030
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
    }

    // Token: 0x0200000B RID: 11
    [HarmonyPatch]
    internal static class OpenXRInputPatches
    {
        // Token: 0x0600013A RID: 314 RVA: 0x0000BB3C File Offset: 0x00009D3C
        private static MethodBase TargetMethod()
        {
            Type type = OpenXRInputPatches.FindType("OpenXRInputController");
            MethodBase result;
            if (type == null)
            {
                result = null;
            }
            else
            {
                result = type.GetMethod("UpdatePosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            return result;
        }

        // SmoothLocomotion namespace may need a using directive depending on where the interop stub
        // puts it (check dnSpy if this doesn't compile - it's probably Alta.Character or similar).
        [HarmonyPatch(typeof(SmoothLocomotion), "TryApplyFallDamage")]
        internal static class VoidFallDamagePatch
        {
            private static bool Prefix()
            {
                return !FlatscreenCore.VoidFallDamageEnabled;
            }
        }
        [HarmonyPatch(typeof(HealthObject), "ReceiveDamage", typeof(float), typeof(float), typeof(DamageData))]
        internal static class VoidDamagePatch
        {
            private static bool Prefix(DamageData impact)
            {
                if (FlatscreenCore.VoidFallDamageEnabled && impact.Source == DamageSource.FallDamage)
                {
                    return false;
                }
                if (FlatscreenCore.VoidBurnDamageEnabled && impact.Source == DamageSource.Fire)
                {
                    return false;
                }
                return true;
            }
        }
        // Harmony patches for the Performance menu (auto-applied by MelonLoader like the other
        // [HarmonyPatch] classes in this file): catch freshly spawned chunks for grass culling and
        // track post-processing volumes so bloom can be force-disabled everywhere.
        [HarmonyPatch]
        internal static class PerformancePatches
        {
            [HarmonyPatch(typeof(ChunkPrefabPointer.PointerInstance), "Spawned", MethodType.Setter)]
            internal static class ChunkSpawnedPatch
            {
                private static void Postfix(ChunkPrefabPointer.PointerInstance __instance)
                {
                    PerformanceController.OnPointerSpawned(__instance);
                }
            }

            [HarmonyPatch(typeof(PostProcessVolume), "OnEnable")]
            internal static class PostProcessVolumePatch
            {
                private static void Postfix(PostProcessVolume __instance)
                {
                    PerformanceController.OnVolumeEnable(__instance);
                }
            }
        }
        // Token: 0x0600013B RID: 315 RVA: 0x0000BB7C File Offset: 0x00009D7C
        private static bool Prepare()
        {
            return OpenXRInputPatches.TargetMethod() != null;
        }

        // Token: 0x0600013C RID: 316 RVA: 0x0000BB9C File Offset: 0x00009D9C
        private static bool Prefix()
        {
            return !FlatscreenCore.SuppressOpenXRInputUpdates;
        }

        // Token: 0x0600013D RID: 317 RVA: 0x0000BBB8 File Offset: 0x00009DB8
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
    }
    // Harmony patches for the Performance menu (auto-applied by MelonLoader like the other
    // [HarmonyPatch] classes in this file): catch freshly spawned chunks for grass culling and
    // track post-processing volumes so bloom can be force-disabled everywhere.
    [HarmonyPatch]
    internal static class PerformancePatches
    {
        [HarmonyPatch(typeof(ChunkPrefabPointer.PointerInstance), "Spawned", MethodType.Setter)]
        internal static class ChunkSpawnedPatch
        {
            private static void Postfix(ChunkPrefabPointer.PointerInstance __instance)
            {
                PerformanceController.OnPointerSpawned(__instance);
            }
        }

        [HarmonyPatch(typeof(PostProcessVolume), "OnEnable")]
        internal static class PostProcessVolumePatch
        {
            private static void Postfix(PostProcessVolume __instance)
            {
                PerformanceController.OnVolumeEnable(__instance);
            }
        }
    }
}