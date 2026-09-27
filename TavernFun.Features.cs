using Alta.Character;
using Alta.Impact;
using Alta.StatSystem;
using HarmonyLib;
using MelonLoader;
using System;
using RootMotion.FinalIK;
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
    internal sealed class ControlMenu
    {
        internal enum Tab
        {
            Player,
            Server,
            Funny,
            Anything
        }
        private bool _voidMenuOpen;
        private Vector2 _voidMenuPosition = new Vector2(600f, 12f);
        private bool _isDraggingVoidMenu;
        private Vector2 _dragOffsetVoidMenu;
        private Rect _lastVoidMenuRect;
        private const int VoidMenuWidth = 220;
        private const int VoidMenuHeight = 216;
        // --- new fields (put alongside the other *MenuOpen fields) ---
        private bool _soundsMenuOpen;
        private Vector2 _soundsMenuPosition = new Vector2(600f, 300f);
        private bool _isDraggingSoundsMenu;
        private bool _handRotMenuOpen;
        private Vector2 _handRotMenuPosition = new Vector2(600f, 300f);
        private bool _isDraggingHandRotMenu;
        private Vector2 _dragOffsetHandRotMenu;
        private Rect _lastHandRotMenuRect;
        private const int HandRotMenuWidth = 260;
        private const int HandRotMenuHeight = 360;
        private readonly HandRotationController _handRotation = new HandRotationController();
        private Vector2 _dragOffsetSoundsMenu;
        // --- Fingers / hand-sign menu ---
        private bool _fingerMenuOpen;
        private Vector2 _fingerMenuPosition = new Vector2(620f, 80f);
        private bool _isDraggingFingerMenu;
        private Vector2 _dragOffsetFingerMenu;
        private Rect _lastFingerMenuRect;
        private Vector2 _fingerPresetScroll;
        private string _fingerPresetName = "My Sign";

        private const int FingerMenuWidth = 300;
        private const int FingerMenuHeight = 520;

        private readonly FingerSignController _fingers = new FingerSignController();

        private Rect _lastSoundsMenuRect;
        private const int SoundsMenuWidth = 220;
        private const int SoundsMenuHeight = 130;
        private const int MenuWidth = 330;
        private bool _weatherMenuOpen;
        private Vector2 _weatherMenuPosition = new Vector2(600f, 300f);
        private bool _isDraggingWeatherMenu;
        private Vector2 _dragOffsetWeatherMenu;
        private Rect _lastWeatherMenuRect;
        private const int WeatherMenuWidth = 220;
        private const int WeatherMenuHeight = 130;
        private readonly WeatherController _weather = new WeatherController();
        private readonly GraphicsController _graphics = new GraphicsController();
        private readonly JeanGreyController _jeanGrey = new JeanGreyController();
        private bool _jeanGreyMenuOpen;
        private Vector2 _jeanGreyMenuPosition = new Vector2(600f, 260f);
        private bool _isDraggingJeanGreyMenu;
        private Vector2 _dragOffsetJeanGreyMenu;
        private Rect _lastJeanGreyMenuRect;
        private Vector2 _jeanGreyScroll;
        private const int JeanGreyMenuWidth = 260;
        private const int JeanGreyMenuHeight = 420;
        private bool _graphicsMenuOpen;
        private Vector2 _graphicsMenuPosition = new Vector2(600f, 440f);
        private bool _isDraggingGraphicsMenu;
        private Vector2 _dragOffsetGraphicsMenu;
        private Rect _lastGraphicsMenuRect;
        private const int GraphicsMenuWidth = 220;
        private const int GraphicsMenuHeight = 130;
        private const int MenuHeight = 600;
        private const int TitleBarHeight = 26;
        private const int OuterFramePadding = 6;
        private const int TabButtonHeight = 22;
        private const int ActionButtonHeight = 20; // non-tab buttons are a bit smaller than tabs
        private const int ButtonSpacing = 5;
        private const int TextureWidth = 64;
        private const int TextureHeight = 32;
        private const int ActionButtonCornerRadius = 6;
        private float _speedMultiplier = 0.5f;
        private string _speedApplyMessage = string.Empty;
        private bool _voidFallDamageEnabled = true;
        // Performance menu (Anything tab) - ported from FastTale: MSAA, Overview Camera,
        // grass and bloom toggles for better frame rates. Opened from the Anything tab.
        private bool _perfMenuOpen;
        private Vector2 _perfMenuPosition = new Vector2(600f, 440f);
        private bool _isDraggingPerfMenu;
        private Vector2 _dragOffsetPerfMenu;
        private Rect _lastPerfMenuRect;
        private const int PerfMenuWidth = 250;
        private const int PerfMenuHeight = 220;
        private readonly PerformanceController _performance = new PerformanceController();
        // Particles menu (Funny tab) - pick a VFX asset and spawn it in front of the player,
        // either as a single burst or as a wall grid of the selected effect.
        private bool _particlesMenuOpen;
        private Vector2 _particlesMenuPosition = new Vector2(640f, 20f);
        private bool _isDraggingParticlesMenu;
        private Vector2 _dragOffsetParticlesMenu;
        private Rect _lastParticlesMenuRect;
        private Vector2 _particlesMenuScroll;
        private const int ParticlesMenuWidth = 260;
        private const int ParticlesMenuHeight = 420;
        private readonly ParticlesController _particles = new ParticlesController();
        private readonly TeleportEffectsController _tpEffects = new TeleportEffectsController();
        private bool _tpEffectsMenuOpen;
        private Vector2 _tpEffectsMenuPosition = new Vector2(640f, 240f);
        private bool _isDraggingTpEffectsMenu;
        private Vector2 _dragOffsetTpEffectsMenu;
        private Rect _lastTpEffectsMenuRect;
        private Vector2 _tpEffectsScroll;
        private const int TpEffectsMenuWidth = 280;
        private const int TpEffectsMenuHeight = 430;
        private readonly SpiderController _spider = new SpiderController();
        private readonly SuperFlyController _superFly = new SuperFlyController();
        private readonly HipMoveController _hipMove = new HipMoveController();
        private bool _fovMenuOpen;
        private Vector2 _fovMenuPosition = new Vector2(360f, 40f);
        private Rect _lastFovMenuRect;
        private bool _isDraggingFovMenu;
        private Vector2 _dragOffsetFovMenu;
        private float _fovValue = 90f;
        private const int FovMenuWidth = 250;
        private const int FovMenuHeight = 145;
        private readonly ReviveOrbController _revive = new ReviveOrbController();
        private bool _voidBurnDamageEnabled = false;
        // PanKake's own panel - bigger, scrollable, opened with a right-click.
        private const int PanKakeWidth = 620;
        private const int PanKakeHeight = 560;
        private bool _grabMenuOpen;
        private Vector2 _grabMenuPosition = new Vector2(600f, 160f);
        private bool _isDraggingGrabMenu;
        private Vector2 _dragOffsetGrabMenu;
        private Rect _lastGrabMenuRect;
        private string _customGrabFilter = "";
        private string _customGrabAmount = "";
        private const int GrabMenuWidth = 260;
        private const int GrabMenuHeight = 420;
        private static readonly Color32 OuterFrameColor = new Color32(198, 166, 130, 255);
        private static readonly Color32 OuterFrameShadow = new Color32(140, 112, 82, 255);
        private static readonly Color32 InnerBackgroundColor = new Color32(36, 24, 16, 255);
        private static readonly Color32 InnerBorderColor = new Color32(20, 12, 8, 255);
        private static readonly Color32 TitleBarTop = new Color32(90, 62, 38, 255);
        private static readonly Color32 TitleBarBottom = new Color32(58, 38, 22, 255);
        private static readonly Color32 GoldTop = new Color32(248, 214, 128, 255);
        private static readonly Color32 GoldBottom = new Color32(164, 122, 36, 255);
        private static readonly Color32 GoldBorder = new Color32(96, 68, 20, 255);
        private static readonly Color32 ShadowColor = new Color32(46, 28, 16, 235);
        private static readonly Color32 TitleTextColor = new Color32(248, 224, 168, 255);

        // Menu starts hidden; M shows/hides it.
        public bool IsVisible { get; private set; }

        // True whenever the mouse is over either window this frame. DesktopInput checks this so
        // clicking a menu button (e.g. right-clicking PanKake) never also registers as an in-game
        // hand grab toggle.
        internal static bool IsPointerOverUI;
        internal static bool IsCursorFree;
        private readonly MenuKeyInput _input = new MenuKeyInput();
        private Tab _currentTab = Tab.Player;

        // Ported-in Flatscreen/PanKake driver. Runs every frame regardless of menu visibility,
        // exactly like it did as its own MelonMod - only the entry point moved.
        private readonly FlatscreenCore _flatscreen = new FlatscreenCore();
        private bool _panKakeEnabled = true;
        private bool _panKakePanelOpen;
        private Vector2 _panKakeScroll;
        private Vector2 _tabScroll;

        // Draggable window positions.
        private Vector2 _windowPosition = new Vector2(18f, 12f);
        private bool _isDraggingMain;
        private Vector2 _dragOffsetMain;
        private Rect _lastMainRect;

        private Vector2 _panKakePosition = new Vector2(360f, 12f);
        private bool _isDraggingPanKake;
        private Vector2 _dragOffsetPanKake;
        private Rect _lastPanKakeRect;

        private Texture2D _outerFrameTexture;
        private Texture2D _outerShadowTexture;
        private Texture2D _innerBackgroundTexture;
        private Texture2D _titleBarTexture;
        private Texture2D _goldButtonTexture;
        private Texture2D _goldButtonDimTexture;
        private Texture2D _actionButtonTexture;
        private Texture2D _actionButtonDimTexture;
        private Texture2D _scrollTrackTexture;
        private Texture2D _scrollThumbTexture;
        private Texture2D _scrollThumbActiveTexture;
        private GUIStyle _shadowLabelStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _actionShadowLabelStyle;
        private GUIStyle _actionLabelStyle;
        private GUIStyle _titleLabelStyle;
        private GUIStyle _titleShadowStyle;
        private GUIStyle _panelHeaderStyle;
        private GUISkin _panKakeSkin;
        private bool _stylesReady;

        internal void Init()
        {
            _flatscreen.Init();
            _weather.Init();
            _graphics.Init();
            _performance.Init();
            _jeanGrey.Init();
            _particles.Init();
            _handRotation.Init();
            _superFly.Init();
        }

        public void Update()
        {
            if (_input.IsMenuTogglePressed) { IsVisible = !IsVisible; }
            IsCursorFree = IsVisible || _panKakePanelOpen || _fovMenuOpen || _tpEffectsMenuOpen;
            _flatscreen.Tick();
            _weather.Tick();
            _graphics.Tick();
            _performance.Tick();
            _spider.Tick();
            _jeanGrey.Tick();
            _handRotation.Tick();
            _superFly.Tick();
            _hipMove.Tick(_flatscreen);
            _tpEffects.Tick();
        }
        public void LateUpdate()
        {
            _flatscreen.LateTick();
            _jeanGrey.LateTick();
            _fingers.LateTick();
            // Runs last so it can re-assert the locked flight rotation after ATT/IK has had
            // its own late-update pass - this is what stops the continuous spin when the
            // body is left flat and unlocked.
            _superFly.LateTick();
            _hipMove.LateTick();
        }

        public void Draw()
        {
            Event evt = Event.current;
            Vector2 mousePos = (evt != null) ? evt.mousePosition : Vector2.zero;

            if (!IsVisible)
            {
                if (_panKakePanelOpen)
                {
                    DrawPanKakePanel();
                }
                if (_voidMenuOpen)
                {
                    DrawVoidMenu();
                }
                if (_grabMenuOpen)
                {
                    DrawGrabMenu();
                }
                if (_fingerMenuOpen)
                {
                    DrawFingerMenu();
                }

                if (_weatherMenuOpen)
                {
                    DrawWeatherMenu();
                }
                if (_graphicsMenuOpen)
                {
                    DrawGraphicsMenu();
                }
                if (_perfMenuOpen)
                {
                    DrawPerfMenu();
                }
                if (_particlesMenuOpen)
                {
                    DrawParticlesMenu();
                }
                if (_tpEffectsMenuOpen)
                {
                    DrawTpEffectsMenu();
                }
                if (_jeanGreyMenuOpen)
                {
                    DrawJeanGreyMenu();
                }
                if (_soundsMenuOpen)
                {
                    DrawSoundsMenu();
                }
                if (_handRotMenuOpen)
                {
                    DrawHandRotMenu();
                }
                if (_fovMenuOpen)
                {
                    DrawFovMenu();
                }
                IsPointerOverUI = (_panKakePanelOpen && _lastPanKakeRect.Contains(mousePos))
                                                || (_voidMenuOpen && _lastVoidMenuRect.Contains(mousePos))
                                                || (_grabMenuOpen && _lastGrabMenuRect.Contains(mousePos))
                                                || (_weatherMenuOpen && _lastWeatherMenuRect.Contains(mousePos))
                                                || (_perfMenuOpen && _lastPerfMenuRect.Contains(mousePos))
                                                || (_handRotMenuOpen && _lastHandRotMenuRect.Contains(mousePos))
                                                || (_particlesMenuOpen && _lastParticlesMenuRect.Contains(mousePos))
                                                || (_tpEffectsMenuOpen && _lastTpEffectsMenuRect.Contains(mousePos))
                                                || (_fingerMenuOpen && _lastFingerMenuRect.Contains(mousePos))
                                                || (_jeanGreyMenuOpen && _lastJeanGreyMenuRect.Contains(mousePos))
                                                || (_soundsMenuOpen && _lastSoundsMenuRect.Contains(mousePos))
                                                || (_fovMenuOpen && _lastFovMenuRect.Contains(mousePos));
                IsCursorFree = IsVisible || _panKakePanelOpen || _voidMenuOpen || _grabMenuOpen || _weatherMenuOpen || _perfMenuOpen || _particlesMenuOpen || _jeanGreyMenuOpen || _fovMenuOpen || _tpEffectsMenuOpen;
                return;
            }
            EnsureStyles();

            var outerRect = new Rect(_windowPosition.x, _windowPosition.y, MenuWidth, MenuHeight);
            _lastMainRect = outerRect;

            var shadowRect = new Rect(outerRect.x + 4f, outerRect.y + 5f, outerRect.width, outerRect.height);
            GUI.DrawTexture(shadowRect, _outerShadowTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(outerRect, _outerFrameTexture, ScaleMode.StretchToFill);

            var titleRect = new Rect(outerRect.x + OuterFramePadding, outerRect.y + OuterFramePadding, outerRect.width - OuterFramePadding * 2f, TitleBarHeight);
            GUI.DrawTexture(titleRect, _titleBarTexture, ScaleMode.StretchToFill);
            var titleShadowRect = new Rect(titleRect.x, titleRect.y + 1f, titleRect.width, titleRect.height);
            GUI.Label(titleShadowRect, "TAVERNFUN", _titleShadowStyle);
            GUI.Label(titleRect, "TAVERNFUN", _titleLabelStyle);

            // Drag the whole window by its title bar.
            HandleDrag(titleRect, ref _windowPosition, ref _isDraggingMain, ref _dragOffsetMain);

            var innerRect = new Rect(
                outerRect.x + OuterFramePadding,
                titleRect.yMax + 4f,
                outerRect.width - OuterFramePadding * 2f,
                outerRect.height - OuterFramePadding - TitleBarHeight - 8f);
            GUI.DrawTexture(innerRect, _innerBackgroundTexture, ScaleMode.StretchToFill);

            GUILayout.BeginArea(innerRect);
            DrawTabs();
            GUILayout.Space(ButtonSpacing);
            _tabScroll = GUILayout.BeginScrollView(_tabScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            switch (_currentTab)
            {
                case Tab.Player:
                    DrawPlayerTab();
                    break;
                case Tab.Server:
                    DrawServerTab();
                    break;
                case Tab.Funny:
                    DrawFunnyTab();
                    break;
                case Tab.Anything:
                    DrawAnythingTab();
                    break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            if (_panKakePanelOpen)
            {
                DrawPanKakePanel();
            }
            if (_voidMenuOpen)
            {
                DrawVoidMenu();
            }
            if (_grabMenuOpen)
            {
                DrawGrabMenu();
            }
            if (_weatherMenuOpen)
            {
                DrawWeatherMenu();
            }
            if (_perfMenuOpen)
            {
                DrawPerfMenu();
            }
            if (_fingerMenuOpen)
            {
                DrawFingerMenu();
            }

            if (_particlesMenuOpen)
            {
                DrawParticlesMenu();
            }
            if (_tpEffectsMenuOpen)
            {
                DrawTpEffectsMenu();
            }
            if (_graphicsMenuOpen)
            {
                DrawGraphicsMenu();
            }
            if (_jeanGreyMenuOpen)
            {
                DrawJeanGreyMenu();
            }
            if (_soundsMenuOpen)
            {
                DrawSoundsMenu();
            }
            if (_handRotMenuOpen)
            {
                DrawHandRotMenu();
            }
            if (_fovMenuOpen)
            {
                DrawFovMenu();
            }
            IsPointerOverUI = _lastMainRect.Contains(mousePos)
                            || (_panKakePanelOpen && _lastPanKakeRect.Contains(mousePos))
                            || (_voidMenuOpen && _lastVoidMenuRect.Contains(mousePos))
                            || (_grabMenuOpen && _lastGrabMenuRect.Contains(mousePos))
                            || (_handRotMenuOpen && _lastHandRotMenuRect.Contains(mousePos))
                            || (_weatherMenuOpen && _lastWeatherMenuRect.Contains(mousePos))
                            || (_fingerMenuOpen && _lastFingerMenuRect.Contains(mousePos))
                            || (_perfMenuOpen && _lastPerfMenuRect.Contains(mousePos))
                            || (_particlesMenuOpen && _lastParticlesMenuRect.Contains(mousePos))
                            || (_tpEffectsMenuOpen && _lastTpEffectsMenuRect.Contains(mousePos))
                            || (_jeanGreyMenuOpen && _lastJeanGreyMenuRect.Contains(mousePos))
                            || (_soundsMenuOpen && _lastSoundsMenuRect.Contains(mousePos))
                            || (_fovMenuOpen && _lastFovMenuRect.Contains(mousePos));
            IsCursorFree = IsVisible || _panKakePanelOpen || _voidMenuOpen || _grabMenuOpen || _weatherMenuOpen || _perfMenuOpen || _particlesMenuOpen || _jeanGreyMenuOpen || _fovMenuOpen || _tpEffectsMenuOpen;
        }

        private void DrawTabs()
        {
            GUILayout.BeginHorizontal();
            DrawTabButton(Tab.Player, "Player");
            DrawTabButton(Tab.Server, "Server");
            DrawTabButton(Tab.Funny, "Funny");
            DrawTabButton(Tab.Anything, "Anything");
            GUILayout.EndHorizontal();
        }

        private void DrawTabButton(Tab tab, string label)
        {
            bool active = _currentTab == tab;
            Rect rect = GUILayoutUtility.GetRect(1f, TabButtonHeight, GUILayout.ExpandWidth(true));
            if (DrawGoldButton(rect, label, active, true))
            {
                _currentTab = tab;
            }
            GUILayout.Space(2f);
        }
        [Serializable]
        internal sealed class FingerSignPreset
        {
            public string Name;
            public bool[] Left;
            public bool[] Right;
        }

        [Serializable]
        internal sealed class FingerSignPresetStore
        {
            public List<FingerSignPreset> Presets =
                new List<FingerSignPreset>();
        }

        internal sealed class FingerSignController
        {
            private const string PreferenceKey =
                "TavernFun.FingerSigns.v1";

            private static readonly string[] FingerNames =
            {
        "Thumb",
        "Index",
        "Middle",
        "Ring",
        "Pinky"
    };

            private readonly GameReflection _game =
                new GameReflection();

            private readonly List<FingerSignPreset> _presets =
                new List<FingerSignPreset>();

            private bool[] _left =
            {
        true,
        true,
        true,
        true,
        true
    };

            private bool[] _right =
            {
        true,
        true,
        true,
        true,
        true
    };

            private HandGrip _leftGrip;
            private HandGrip _rightGrip;
            private Transform _avatarRoot;

            private bool _loaded;
            private string _status = "Select a finger.";

            internal bool Active { get; private set; }

            internal string Status
            {
                get { return _status; }
            }

            private void SetStatus(string value)
            {
                _status = value;
                MelonLogger.Msg("[Fingers] " + value);
            }
            internal int PresetCount
            {
                get
                {
                    EnsureLoaded();
                    return _presets.Count;
                }
            }

            internal void ToggleFinger(int hand, int finger)
            {
                EnsureLoaded();

                if (finger < 0 || finger >= 5)
                {
                    return;
                }

                if (hand == 0)
                {
                    _left[finger] = !_left[finger];
                }
                else
                {
                    _right[finger] = !_right[finger];
                }

                Active = true;
                SetStatus("Finger pose changed.");
            }

            internal bool IsFingerUp(int hand, int finger)
            {
                EnsureLoaded();

                if (finger < 0 || finger >= 5)
                {
                    return true;
                }

                return hand == 0
                    ? _left[finger]
                    : _right[finger];
            }

            internal string GetFingerLabel(int hand, int finger)
            {
                string handName = hand == 0 ? "L" : "R";
                string fingerName =
                    finger >= 0 && finger < FingerNames.Length
                        ? FingerNames[finger]
                        : "Finger";

                return handName
                    + " "
                    + fingerName
                    + ": "
                    + (IsFingerUp(hand, finger) ? "Up" : "Down");
            }

            internal string GetPresetName(int index)
            {
                EnsureLoaded();

                if (index < 0 || index >= _presets.Count)
                {
                    return string.Empty;
                }

                return _presets[index].Name;
            }

            internal void SavePreset(string name)
            {
                EnsureLoaded();

                name = string.IsNullOrWhiteSpace(name)
                    ? "Hand Sign"
                    : name.Trim();

                FingerSignPreset preset = new FingerSignPreset
                {
                    Name = name,
                    Left = CloneState(_left),
                    Right = CloneState(_right)
                };

                int existingIndex = -1;

                for (int i = 0; i < _presets.Count; i++)
                {
                    if (string.Equals(
                            _presets[i].Name,
                            name,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        existingIndex = i;
                        break;
                    }
                }

                if (existingIndex >= 0)
                {
                    _presets[existingIndex] = preset;
                }
                else
                {
                    _presets.Add(preset);
                }

                SavePresets();

                Active = true;
                _status = "Saved hand sign: " + name;
            }

            internal void LoadPreset(int index)
            {
                EnsureLoaded();

                if (index < 0 || index >= _presets.Count)
                {
                    return;
                }

                FingerSignPreset preset = _presets[index];

                _left = NormalizeState(preset.Left);
                _right = NormalizeState(preset.Right);

                Active = true;
                _status = "Loaded hand sign: " + preset.Name;
            }

            internal void DeletePreset(int index)
            {
                EnsureLoaded();

                if (index < 0 || index >= _presets.Count)
                {
                    return;
                }

                string deletedName = _presets[index].Name;

                _presets.RemoveAt(index);
                SavePresets();

                _status = "Deleted hand sign: " + deletedName;
            }

            internal void Reset()
            {
                EnsureLoaded();

                _left = CreateDefaultState();
                _right = CreateDefaultState();

                if (EnsureAvatarRig())
                {
                    float[] zero = new float[] { 0f, 0f, 0f, 0f, 0f };
                    _leftGrip.SkeletalInput(zero);
                    _rightGrip.SkeletalInput(zero);
                }

                Active = false;
                SetStatus("Hands reset.");
            }
            internal void LateTick()
            {
                EnsureLoaded();

                if (!Active)
                {
                    return;
                }

                if (!EnsureAvatarRig())
                {
                    return;
                }

                ApplyHandGrip(_leftGrip, _left);
                ApplyHandGrip(_rightGrip, _right);
            }

            private void EnsureLoaded()
            {
                if (_loaded)
                {
                    return;
                }

                _loaded = true;

                string json = PlayerPrefs.GetString(
                    PreferenceKey,
                    string.Empty);

                if (string.IsNullOrEmpty(json))
                {
                    return;
                }

                try
                {
                    FingerSignPresetStore store =
                        JsonUtility.FromJson<FingerSignPresetStore>(json);

                    if (store == null || store.Presets == null)
                    {
                        return;
                    }

                    for (int i = 0; i < store.Presets.Count; i++)
                    {
                        FingerSignPreset preset = store.Presets[i];

                        if (preset == null
                            || string.IsNullOrWhiteSpace(preset.Name))
                        {
                            continue;
                        }

                        preset.Left = NormalizeState(preset.Left);
                        preset.Right = NormalizeState(preset.Right);

                        _presets.Add(preset);
                    }
                }
                catch (Exception exception)
                {
                    _status = "Could not load hand signs: "
                        + exception.Message;
                }
            }

            private void SavePresets()
            {
                FingerSignPresetStore store =
                    new FingerSignPresetStore
                    {
                        Presets = _presets
                    };

                PlayerPrefs.SetString(
                    PreferenceKey,
                    JsonUtility.ToJson(store));

                PlayerPrefs.Save();
            }
            private RootMotion.FinalIK.FingerRig _leftRig;
            private RootMotion.FinalIK.FingerRig _rightRig;
            private bool _didSceneScan;

            private bool EnsureAvatarRig()
            {
                object player;

                try
                {
                    player = _game.FindLocalPlayer();
                }
                catch
                {
                    SetStatus("Local player is not ready.");
                    return false;
                }

                if (player == null)
                {
                    SetStatus("Local player is not ready.");
                    return false;
                }

                Transform root;

                try
                {
                    root = _game.GetPlayerRootTransform(player);
                }
                catch
                {
                    SetStatus("Could not find the player avatar.");
                    return false;
                }

                if (root == null)
                {
                    SetStatus("Could not find the player avatar.");
                    return false;
                }

                if (root == _avatarRoot && _leftGrip != null && _rightGrip != null)
                {
                    return true;
                }

                Hand[] hands = root.GetComponentsInChildren<Hand>(true);
                HandGrip leftGrip = null;
                HandGrip rightGrip = null;

                for (int i = 0; i < hands.Length; i++)
                {
                    HandGrip grip = hands[i].GripAnimator;
                    if (grip == null)
                    {
                        continue;
                    }
                    if (grip.IsLeftHand)
                    {
                        leftGrip = grip;
                    }
                    else
                    {
                        rightGrip = grip;
                    }
                }

                if (leftGrip == null || rightGrip == null)
                {
                    SetStatus("Could not find HandGrip on both hands.");
                    return false;
                }

                _avatarRoot = root;
                _leftGrip = leftGrip;
                _rightGrip = rightGrip;
                SetStatus("Hand grip applied.");
                return true;
            }

            private static string GetHierarchyPath(Transform t)
            {
                string path = t.name;
                Transform p = t.parent;
                int depth = 0;
                while (p != null && depth < 6)
                {
                    path = p.name + "/" + path;
                    p = p.parent;
                    depth++;
                }
                return path;
            }
            private static FingerChain[] BuildHand(
                Animator animator,
                bool left)
            {
                HumanBodyBones[][] mapping = left
                    ? new[]
                    {
                new[]
                {
                    HumanBodyBones.LeftThumbProximal,
                    HumanBodyBones.LeftThumbIntermediate,
                    HumanBodyBones.LeftThumbDistal
                },
                new[]
                {
                    HumanBodyBones.LeftIndexProximal,
                    HumanBodyBones.LeftIndexIntermediate,
                    HumanBodyBones.LeftIndexDistal
                },
                new[]
                {
                    HumanBodyBones.LeftMiddleProximal,
                    HumanBodyBones.LeftMiddleIntermediate,
                    HumanBodyBones.LeftMiddleDistal
                },
                new[]
                {
                    HumanBodyBones.LeftRingProximal,
                    HumanBodyBones.LeftRingIntermediate,
                    HumanBodyBones.LeftRingDistal
                },
                new[]
                {
                    HumanBodyBones.LeftLittleProximal,
                    HumanBodyBones.LeftLittleIntermediate,
                    HumanBodyBones.LeftLittleDistal
                }
                    }
                    : new[]
                    {
                new[]
                {
                    HumanBodyBones.RightThumbProximal,
                    HumanBodyBones.RightThumbIntermediate,
                    HumanBodyBones.RightThumbDistal
                },
                new[]
                {
                    HumanBodyBones.RightIndexProximal,
                    HumanBodyBones.RightIndexIntermediate,
                    HumanBodyBones.RightIndexDistal
                },
                new[]
                {
                    HumanBodyBones.RightMiddleProximal,
                    HumanBodyBones.RightMiddleIntermediate,
                    HumanBodyBones.RightMiddleDistal
                },
                new[]
                {
                    HumanBodyBones.RightRingProximal,
                    HumanBodyBones.RightRingIntermediate,
                    HumanBodyBones.RightRingDistal
                },
                new[]
                {
                    HumanBodyBones.RightLittleProximal,
                    HumanBodyBones.RightLittleIntermediate,
                    HumanBodyBones.RightLittleDistal
                }
                    };

                FingerChain[] result = new FingerChain[5];

                for (int i = 0; i < mapping.Length; i++)
                {
                    result[i] = BuildFinger(animator, mapping[i]);
                }

                return result;
            }

            private static FingerChain BuildFinger(
                Animator animator,
                HumanBodyBones[] bones)
            {
                Transform[] transforms =
                    new Transform[bones.Length];

                Quaternion[] restRotations =
                    new Quaternion[bones.Length];

                for (int i = 0; i < bones.Length; i++)
                {
                    Transform bone =
                        animator.GetBoneTransform(bones[i]);

                    if (bone == null)
                    {
                        return null;
                    }

                    transforms[i] = bone;
                    restRotations[i] = bone.localRotation;
                }

                return new FingerChain
                {
                    Bones = transforms,
                    RestRotations = restRotations
                };
            }

            private static bool HasAnyFinger(FingerChain[] chains)
            {
                if (chains == null)
                {
                    return false;
                }

                for (int i = 0; i < chains.Length; i++)
                {
                    if (chains[i] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
            private static void ApplyHandGrip(HandGrip grip, bool[] states)
            {
                if (grip == null || states == null)
                {
                    return;
                }

                float[] progress = new float[5];
                for (int i = 0; i < 5 && i < states.Length; i++)
                {
                    progress[i] = states[i] ? 0f : 1f; // Up (true) = open (0), Down (false) = curled (1)
                }

                grip.ForceNextSkeletal();
                grip.SkeletalInput(progress);
            }
            private static void ApplyHandPose(
                FingerChain[] chains,
                bool[] states)
            {
                if (chains == null || states == null)
                {
                    return;
                }

                for (int finger = 0; finger < chains.Length; finger++)
                {
                    FingerChain chain = chains[finger];

                    if (chain == null || finger >= states.Length)
                    {
                        continue;
                    }

                    bool down = !states[finger];

                    for (int bone = 0; bone < chain.Bones.Length; bone++)
                    {
                        float curlAngle =
                            GetCurlAngle(finger, bone);

                        if (!down)
                        {
                            curlAngle = 0f;
                        }

                        // Most Unity humanoid rigs curl around local X.
                        // Change this to +1f if your avatar curls backwards.
                        const float curlDirection = -1f;

                        Quaternion curl =
                            Quaternion.AngleAxis(
                                curlAngle * curlDirection,
                                Vector3.right);

                        chain.Bones[bone].localRotation =
                            chain.RestRotations[bone] * curl;
                    }
                }
            }

            private static float GetCurlAngle(
                int finger,
                int bone)
            {
                if (finger == 0)
                {
                    switch (bone)
                    {
                        case 0:
                            return 22f;
                        case 1:
                            return 30f;
                        default:
                            return 25f;
                    }
                }

                switch (bone)
                {
                    case 0:
                        return 35f;
                    case 1:
                        return 45f;
                    default:
                        return 35f;
                }
            }


            private static bool[] CreateDefaultState()
            {
                return new[]
                {
            true,
            true,
            true,
            true,
            true
        };
            }

            private static bool[] CloneState(bool[] state)
            {
                bool[] copy = CreateDefaultState();

                if (state == null)
                {
                    return copy;
                }

                for (int i = 0; i < copy.Length && i < state.Length; i++)
                {
                    copy[i] = state[i];
                }

                return copy;
            }

            private static bool[] NormalizeState(bool[] state)
            {
                return CloneState(state);
            }

            private sealed class FingerChain
            {
                internal Transform[] Bones;
                internal Quaternion[] RestRotations;
            }
        }
        private void DrawFingerMenu()
        {
            EnsureStyles();

            Rect outerRect = new Rect(
                _fingerMenuPosition.x,
                _fingerMenuPosition.y,
                FingerMenuWidth,
                FingerMenuHeight);

            _lastFingerMenuRect = outerRect;

            Rect shadowRect = new Rect(
                outerRect.x + 4f,
                outerRect.y + 5f,
                outerRect.width,
                outerRect.height);

            GUI.DrawTexture(
                shadowRect,
                _outerShadowTexture,
                ScaleMode.StretchToFill);

            GUI.DrawTexture(
                outerRect,
                _outerFrameTexture,
                ScaleMode.StretchToFill);

            Rect titleRect = new Rect(
                outerRect.x + OuterFramePadding,
                outerRect.y + OuterFramePadding,
                outerRect.width - OuterFramePadding * 2f,
                TitleBarHeight);

            GUI.DrawTexture(
                titleRect,
                _titleBarTexture,
                ScaleMode.StretchToFill);

            GUI.Label(
                new Rect(titleRect.x, titleRect.y + 1f, titleRect.width, titleRect.height),
                "FINGERS",
                _titleShadowStyle);

            GUI.Label(
                titleRect,
                "FINGERS",
                _titleLabelStyle);

            HandleDrag(
                titleRect,
                ref _fingerMenuPosition,
                ref _isDraggingFingerMenu,
                ref _dragOffsetFingerMenu);

            Rect innerRect = new Rect(
                outerRect.x + OuterFramePadding,
                titleRect.yMax + 4f,
                outerRect.width - OuterFramePadding * 2f,
                outerRect.height - OuterFramePadding - TitleBarHeight - 8f);

            GUI.DrawTexture(
                innerRect,
                _innerBackgroundTexture,
                ScaleMode.StretchToFill);

            GUILayout.BeginArea(innerRect);

            GUILayout.BeginHorizontal();

            Rect closeRect = GUILayoutUtility.GetRect(
                60f,
                ActionButtonHeight,
                GUILayout.Width(60f));

            if (DrawGoldButton(closeRect, "Close", false, false))
            {
                _fingerMenuOpen = false;
            }

            GUILayout.FlexibleSpace();

            Rect resetRect = GUILayoutUtility.GetRect(
                70f,
                ActionButtonHeight,
                GUILayout.Width(70f));

            if (DrawGoldButton(resetRect, "Reset", false, false))
            {
                _fingers.Reset();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            GUILayout.Label("Left hand", _panelHeaderStyle);

            for (int i = 0; i < 5; i++)
            {
                Rect fingerRect = GUILayoutUtility.GetRect(
                    1f,
                    ActionButtonHeight,
                    GUILayout.ExpandWidth(true));

                if (DrawGoldButton(
                        fingerRect,
                        _fingers.GetFingerLabel(0, i),
                        _fingers.IsFingerUp(0, i),
                        false))
                {
                    _fingers.ToggleFinger(0, i);
                }

                GUILayout.Space(2f);
            }

            GUILayout.Space(5f);

            GUILayout.Label("Right hand", _panelHeaderStyle);

            for (int i = 0; i < 5; i++)
            {
                Rect fingerRect = GUILayoutUtility.GetRect(
                    1f,
                    ActionButtonHeight,
                    GUILayout.ExpandWidth(true));

                if (DrawGoldButton(
                        fingerRect,
                        _fingers.GetFingerLabel(1, i),
                        _fingers.IsFingerUp(1, i),
                        false))
                {
                    _fingers.ToggleFinger(1, i);
                }

                GUILayout.Space(2f);
            }

            GUILayout.Space(6f);

            GUILayout.Label("Save hand sign", _panelHeaderStyle);

            _fingerPresetName = GUILayout.TextField(
                _fingerPresetName,
                GUILayout.ExpandWidth(true));

            GUILayout.Space(3f);

            Rect saveRect = GUILayoutUtility.GetRect(
                1f,
                ActionButtonHeight,
                GUILayout.ExpandWidth(true));

            if (DrawGoldButton(saveRect, "Save Current Sign", false, false))
            {
                _fingers.SavePreset(_fingerPresetName);
            }

            GUILayout.Space(5f);

            GUILayout.Label("Saved signs", _panelHeaderStyle);

            _fingerPresetScroll = GUILayout.BeginScrollView(
                _fingerPresetScroll,
                GUILayout.Height(100f));

            for (int i = 0; i < _fingers.PresetCount; i++)
            {
                GUILayout.BeginHorizontal();

                Rect loadRect = GUILayoutUtility.GetRect(
                    1f,
                    ActionButtonHeight,
                    GUILayout.ExpandWidth(true));

                if (DrawGoldButton(
                        loadRect,
                        _fingers.GetPresetName(i),
                        false,
                        false))
                {
                    _fingers.LoadPreset(i);
                }

                Rect deleteRect = GUILayoutUtility.GetRect(
                    26f,
                    ActionButtonHeight,
                    GUILayout.Width(26f));

                if (DrawGoldButton(deleteRect, "X", false, false))
                {
                    _fingers.DeletePreset(i);
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(2f);
            }

            GUILayout.EndScrollView();

            GUILayout.Space(4f);

            GUILayout.Label(_fingers.Status, _labelStyle);

            GUILayout.EndArea();
        }

        private void DrawPlayerTab()
        {
            // --- Row 1: PanKake (left) / Void Fall Damage (right) ---
            GUILayout.BeginHorizontal();

            Rect panKakeRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            bool panKakeRightClicked = WasRightClicked(panKakeRect); // must run BEFORE DrawGoldButton
            bool panKakeLeftClicked = DrawGoldButton(panKakeRect, "PanKake", _panKakeEnabled, false);
            if (panKakeRightClicked)
            {
                _panKakePanelOpen = true;
            }
            else if (panKakeLeftClicked)
            {
                TogglePanKake();
            }

            GUILayout.FlexibleSpace();

            Rect fallDamageRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            if (DrawGoldButton(fallDamageRect, _voidFallDamageEnabled ? "Void Fall Dmg: On" : "Void Fall Dmg: Off", _voidFallDamageEnabled, false))
            {
                _voidFallDamageEnabled = !_voidFallDamageEnabled;
                FlatscreenCore.VoidFallDamageEnabled = _voidFallDamageEnabled;
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            // --- Row 2: Speed multiplier (left) / Void Burn Damage (right) ---
            GUILayout.BeginHorizontal();

            Rect speedRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            bool speedRightClicked = WasRightClicked(speedRect); // must run BEFORE DrawGoldButton
            bool speedLeftClicked = DrawGoldButton(speedRect, "Speed Multi: " + _speedMultiplier.ToString("0.0"), true, false);
            if (speedRightClicked)
            {
                if (_speedMultiplier >= 7f - 0.001f)
                {
                    _speedMultiplier = 0.5f;
                }
                else
                {
                    _speedMultiplier = Mathf.Min(7f, _speedMultiplier + 0.5f);
                }
            }
            else if (speedLeftClicked)
            {
                _speedApplyMessage = FlatscreenCore.SetSpeed(_speedMultiplier)
                    ? "Speed set to " + _speedMultiplier.ToString("0.0")
                    : "Speed set failed (no local player / stat)";
            }

            GUILayout.FlexibleSpace();

            Rect burnDamageRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            if (DrawGoldButton(burnDamageRect, _voidBurnDamageEnabled ? "Void Burn Dmg: On" : "Void Burn Dmg: Off", _voidBurnDamageEnabled, false))
            {
                _voidBurnDamageEnabled = !_voidBurnDamageEnabled;
                FlatscreenCore.VoidBurnDamageEnabled = _voidBurnDamageEnabled;
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            // --- Row 2b: Kill Me (left) / Auto Revive (right) ---
            GUILayout.BeginHorizontal();

            Rect killMeRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            if (DrawGoldButton(killMeRect, FlatscreenCore.KillMeBusy ? "Dying..." : "Kill Me", FlatscreenCore.KillMeBusy, false)
                && !FlatscreenCore.KillMeBusy)
            {
                FlatscreenCore.KillMe();
            }

            GUILayout.FlexibleSpace();

            Rect reviveRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            if (DrawGoldButton(reviveRect, _revive.Busy ? "Reviving..." : "Auto Revive", _revive.Busy, false)
                && !_revive.Busy)
            {
                _revive.RunAutoRevive();
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            // --- Grab (own row — a third fixed-width button in Row 2 would overflow the 300px window) ---
            Rect grabRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            bool grabRightClicked = WasRightClicked(grabRect); // must run BEFORE DrawGoldButton
            bool grabLeftClicked = DrawGoldButton(grabRect, "Grab", _grabMenuOpen, false);
            if (grabRightClicked || grabLeftClicked)
            {
                _grabMenuOpen = true;
            }
            GUILayout.Space(6f);

            // --- Full body rotation toggle ---
            Rect bodyRotRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            bool bodyRotEnabled = _flatscreen.FullBodyRotationEnabled;
            if (DrawGoldButton(bodyRotRect, bodyRotEnabled ? "Full Body Rot: On" : "Full Body Rot: Off", bodyRotEnabled, false))
            {
                _flatscreen.ToggleFullBodyRotation();
            }
            GUILayout.Space(6f);

            Rect fingersRect = GUILayoutUtility.GetRect(
                1f,
                ActionButtonHeight,
                GUILayout.Width(140f));

            if (DrawGoldButton(
                    fingersRect,
                    _fingers.Active ? "Fingers client: On" : "Fingers client",
                    _fingerMenuOpen || _fingers.Active,
                    false))
            {
                _fingerMenuOpen = true;
            }
            GUILayout.Space(6f);
            Rect handRotRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            if (DrawGoldButton(handRotRect, "Hand Rotation", _handRotMenuOpen, false))
            {
                _handRotMenuOpen = true;
            }
            GUILayout.Space(6f);
            Rect flyRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            bool flyRightClicked = WasRightClicked(flyRect); // must run BEFORE DrawGoldButton
            bool flyEnabled = _flatscreen.FlyModeEnabled;
            bool flyLeftClicked = DrawGoldButton(flyRect, flyEnabled ? "Fly: On" : "Fly: Off", flyEnabled, false);
            if (flyRightClicked)
            {
                _voidMenuOpen = true;
            }
            else if (flyLeftClicked)
            {
                _flatscreen.ToggleFlyMode();
            }

            GUILayout.Space(8f);

            Rect thirdPersonRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            bool thirdPersonEnabled = _flatscreen.ThirdPersonEnabled;
            if (DrawGoldButton(thirdPersonRect, thirdPersonEnabled ? "3rd Person: On" : "3rd Person: Off", thirdPersonEnabled, false))
            {
                _flatscreen.ToggleThirdPerson();
            }
            GUILayout.Space(6f);

            GUILayout.BeginHorizontal();
            Rect fovRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            if (DrawGoldButton(fovRect, "Change FOV", _fovMenuOpen, false))
            {
                Camera camera = GetLocalPlayerCamera();
                if (camera != null) _fovValue = Mathf.Clamp(camera.fieldOfView, 30f, 120f);
                _fovMenuOpen = !_fovMenuOpen;
            }
            GUILayout.FlexibleSpace();
            Rect hipRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            if (DrawGoldButton(hipRect, _hipMove.Enabled ? "Hip Move: On" : "Hip Move", _hipMove.Enabled, false))
            {
                _hipMove.Toggle(_flatscreen);
            }
            GUILayout.EndHorizontal();
        }

        private static Camera GetLocalPlayerCamera()
        {
            try
            {
                if (PlayerController.Current != null && PlayerController.Current.Camera != null)
                    return PlayerController.Current.Camera;
            }
            catch { }
            return Camera.main;
        }

        private void DrawFovMenu()
        {
            EnsureStyles();
            Rect rect = new Rect(_fovMenuPosition.x, _fovMenuPosition.y, FovMenuWidth, FovMenuHeight);
            _lastFovMenuRect = rect;
            GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 5f, rect.width, rect.height), _outerShadowTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(rect, _outerFrameTexture, ScaleMode.StretchToFill);
            Rect title = new Rect(rect.x + OuterFramePadding, rect.y + OuterFramePadding, rect.width - OuterFramePadding * 2f, TitleBarHeight);
            GUI.DrawTexture(title, _titleBarTexture, ScaleMode.StretchToFill);
            GUI.Label(title, "FIELD OF VIEW", _titleLabelStyle);
            HandleDrag(title, ref _fovMenuPosition, ref _isDraggingFovMenu, ref _dragOffsetFovMenu);
            Rect inner = new Rect(rect.x + OuterFramePadding, title.yMax + 4f, rect.width - OuterFramePadding * 2f, rect.height - TitleBarHeight - 14f);
            GUI.DrawTexture(inner, _innerBackgroundTexture, ScaleMode.StretchToFill);
            GUILayout.BeginArea(inner);
            GUILayout.BeginHorizontal();
            GUILayout.Label("FOV  " + Mathf.RoundToInt(_fovValue), _panelHeaderStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset", GUILayout.Width(54f)))
            {
                _fovValue = 90f;
                Camera resetCamera = GetLocalPlayerCamera();
                if (resetCamera != null) resetCamera.fieldOfView = _fovValue;
            }
            if (GUILayout.Button("Close", GUILayout.Width(54f))) _fovMenuOpen = false;
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            float next = GUILayout.HorizontalSlider(_fovValue, 30f, 120f, GUILayout.Height(24f));
            if (Mathf.Abs(next - _fovValue) > 0.01f)
            {
                _fovValue = next;
                Camera camera = GetLocalPlayerCamera();
                if (camera != null) camera.fieldOfView = _fovValue;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("30", GUILayout.Width(48f))) _fovValue = 30f;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("120", GUILayout.Width(48f))) _fovValue = 120f;
            GUILayout.EndHorizontal();
            Camera currentCamera = GetLocalPlayerCamera();
            if (currentCamera != null) currentCamera.fieldOfView = _fovValue;
            GUILayout.EndArea();
        }

        private void DrawHandRotMenu()
        {
            EnsureStyles();

            var outerRect = new Rect(_handRotMenuPosition.x, _handRotMenuPosition.y, HandRotMenuWidth, HandRotMenuHeight);
            _lastHandRotMenuRect = outerRect;

            var shadowRect = new Rect(outerRect.x + 4f, outerRect.y + 5f, outerRect.width, outerRect.height);
            GUI.DrawTexture(shadowRect, _outerShadowTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(outerRect, _outerFrameTexture, ScaleMode.StretchToFill);

            var titleRect = new Rect(outerRect.x + OuterFramePadding, outerRect.y + OuterFramePadding, outerRect.width - OuterFramePadding * 2f, TitleBarHeight);
            GUI.DrawTexture(titleRect, _titleBarTexture, ScaleMode.StretchToFill);
            GUI.Label(new Rect(titleRect.x, titleRect.y + 1f, titleRect.width, titleRect.height), "HAND ROTATION", _titleShadowStyle);
            GUI.Label(titleRect, "HAND ROTATION", _titleLabelStyle);

            HandleDrag(titleRect, ref _handRotMenuPosition, ref _isDraggingHandRotMenu, ref _dragOffsetHandRotMenu);

            var innerRect = new Rect(
                outerRect.x + OuterFramePadding,
                titleRect.yMax + 4f,
                outerRect.width - OuterFramePadding * 2f,
                outerRect.height - OuterFramePadding - TitleBarHeight - 8f);
            GUI.DrawTexture(innerRect, _innerBackgroundTexture, ScaleMode.StretchToFill);

            GUILayout.BeginArea(innerRect);

            GUILayout.BeginHorizontal();
            Rect closeRect = GUILayoutUtility.GetRect(60f, ActionButtonHeight, GUILayout.Width(60f));
            if (DrawGoldButton(closeRect, "Close", false, false)) { _handRotMenuOpen = false; }
            GUILayout.FlexibleSpace();
            Rect resetRect = GUILayoutUtility.GetRect(70f, ActionButtonHeight, GUILayout.Width(70f));
            if (DrawGoldButton(resetRect, "Reset", false, false)) { _handRotation.ResetOffsets(); }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("Left Hand", _panelHeaderStyle);
            _handRotation.LeftPitch = DrawRotationSlider("Pitch", _handRotation.LeftPitch);
            _handRotation.LeftYaw = DrawRotationSlider("Yaw", _handRotation.LeftYaw);
            _handRotation.LeftRoll = DrawRotationSlider("Roll", _handRotation.LeftRoll);

            GUILayout.Space(6f);
            GUILayout.Label("Right Hand", _panelHeaderStyle);
            _handRotation.RightPitch = DrawRotationSlider("Pitch", _handRotation.RightPitch);
            _handRotation.RightYaw = DrawRotationSlider("Yaw", _handRotation.RightYaw);
            _handRotation.RightRoll = DrawRotationSlider("Roll", _handRotation.RightRoll);

            GUILayout.Space(8f);
            Rect saveRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            if (DrawGoldButton(saveRect, "Save", false, false)) { _handRotation.Save(); }

            GUILayout.Space(4f);
            GUILayout.Label(_handRotation.Status, _labelStyle);

            GUILayout.EndArea();
        }

        private float DrawRotationSlider(string label, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ": " + value.ToString("0"), GUILayout.Width(90f));
            float newValue = GUILayout.HorizontalSlider(value, -180f, 180f, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            return newValue;
        }
        private void DrawGrabMenu()
        {
            EnsureStyles();

            var outerRect = new Rect(_grabMenuPosition.x, _grabMenuPosition.y, GrabMenuWidth, GrabMenuHeight);
            _lastGrabMenuRect = outerRect;

            var shadowRect = new Rect(outerRect.x + 4f, outerRect.y + 5f, outerRect.width, outerRect.height);
            GUI.DrawTexture(shadowRect, _outerShadowTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(outerRect, _outerFrameTexture, ScaleMode.StretchToFill);

            var titleRect = new Rect(
                outerRect.x + OuterFramePadding,
                outerRect.y + OuterFramePadding,
                outerRect.width - OuterFramePadding * 2f,
                TitleBarHeight);

            GUI.DrawTexture(titleRect, _titleBarTexture, ScaleMode.StretchToFill);
            GUI.Label(
                new Rect(titleRect.x, titleRect.y + 1f, titleRect.width, titleRect.height),
                "GRAB",
                _titleShadowStyle);
            GUI.Label(titleRect, "GRAB", _titleLabelStyle);

            HandleDrag(
                titleRect,
                ref _grabMenuPosition,
                ref _isDraggingGrabMenu,
                ref _dragOffsetGrabMenu);

            var innerRect = new Rect(
                outerRect.x + OuterFramePadding,
                titleRect.yMax + 4f,
                outerRect.width - OuterFramePadding * 2f,
                outerRect.height - OuterFramePadding - TitleBarHeight - 8f);

            GUI.DrawTexture(innerRect, _innerBackgroundTexture, ScaleMode.StretchToFill);

            GUILayout.BeginArea(innerRect);

            // Close
            Rect closeRect = GUILayoutUtility.GetRect(
                1f,
                ActionButtonHeight,
                GUILayout.Width(140f));

            if (DrawGoldButton(closeRect, "Close", false, false))
            {
                _grabMenuOpen = false;
            }

            GUILayout.Space(8f);

            // Grab toggles
            GUILayout.BeginHorizontal();

            Rect raycastRect = GUILayoutUtility.GetRect(
                1f,
                ActionButtonHeight,
                GUILayout.Width(140f));

            if (DrawGoldButton(
                raycastRect,
                FlatscreenCore.RaycastGrabEnabled ? "Raycast Grab: On" : "Raycast Grab: Off",
                FlatscreenCore.RaycastGrabEnabled,
                false))
            {
                FlatscreenCore.RaycastGrabEnabled = !FlatscreenCore.RaycastGrabEnabled;
            }

            GUILayout.FlexibleSpace();

            Rect grabbyRect = GUILayoutUtility.GetRect(
                1f,
                ActionButtonHeight,
                GUILayout.Width(140f));

            if (DrawGoldButton(
                grabbyRect,
                FlatscreenCore.GrabbyHandsEnabled ? "Grabby Hands: On" : "Grabby Hands: Off",
                FlatscreenCore.GrabbyHandsEnabled,
                false))
            {
                FlatscreenCore.GrabbyHandsEnabled = !FlatscreenCore.GrabbyHandsEnabled;
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(10f);

            GUILayout.Label("Grab By Name", _panelHeaderStyle);

            GUILayout.Space(4f);

            // Row 1
            GUILayout.BeginHorizontal();

            Rect allRect = GUILayoutUtility.GetRect(
                1f,
                ActionButtonHeight,
                GUILayout.Width(140f));

            if (DrawGoldButton(allRect, "All", false, false))
            {
                _flatscreen.GrabByName(null);
            }

            GUILayout.FlexibleSpace();

            Rect bagsRect = GUILayoutUtility.GetRect(
                1f,
                ActionButtonHeight,
                GUILayout.Width(140f));

            if (DrawGoldButton(bagsRect, "Bags", false, false))
            {
                _flatscreen.GrabByName("bag");
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            // Row 2
            GUILayout.BeginHorizontal();

            Rect coinsRect = GUILayoutUtility.GetRect(
                1f,
                ActionButtonHeight,
                GUILayout.Width(140f));

            if (DrawGoldButton(coinsRect, "Coins", false, false))
            {
                _flatscreen.GrabByName("coin");
            }

            GUILayout.FlexibleSpace();

            Rect ingotsRect = GUILayoutUtility.GetRect(
                1f,
                ActionButtonHeight,
                GUILayout.Width(140f));

            if (DrawGoldButton(ingotsRect, "Ingots", false, false))
            {
                _flatscreen.GrabByName("ingot");
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            // Row 3
            GUILayout.BeginHorizontal();

            Rect toolsRect = GUILayoutUtility.GetRect(
                1f,
                ActionButtonHeight,
                GUILayout.Width(140f));

            if (DrawGoldButton(toolsRect, "Tools", false, false))
            {
                _flatscreen.GrabByName("handle");
            }

            GUILayout.FlexibleSpace();

            Rect mouldsRect = GUILayoutUtility.GetRect(
                1f,
                ActionButtonHeight,
                GUILayout.Width(140f));

            if (DrawGoldButton(mouldsRect, "Moulds", false, false))
            {
                _flatscreen.GrabByName("mould");
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            // Row 4
            Rect potionsRect = GUILayoutUtility.GetRect(
                1f,
                ActionButtonHeight,
                GUILayout.Width(140f));

            if (DrawGoldButton(potionsRect, "Potions", false, false))
            {
                _flatscreen.GrabByName("potion");
            }

            GUILayout.Space(10f);

            // Custom filter
            GUILayout.Label("Custom Filter:", _panelHeaderStyle);

            _customGrabFilter = GUILayout.TextField(
                _customGrabFilter ?? "",
                GUILayout.Height(ActionButtonHeight));

            GUILayout.Space(6f);

            GUILayout.BeginHorizontal();

            GUILayout.Label("Amount:", GUILayout.Width(55f));

            _customGrabAmount = GUILayout.TextField(
                _customGrabAmount ?? "",
                GUILayout.Width(60f),
                GUILayout.Height(ActionButtonHeight));

            GUILayout.Label("(blank = all)");

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            Rect customRect = GUILayoutUtility.GetRect(
                1f,
                ActionButtonHeight,
                GUILayout.Width(140f));

            if (DrawGoldButton(customRect, "Grab Custom", false, false) &&
                !string.IsNullOrWhiteSpace(_customGrabFilter))
            {
                bool hasLimit =
                    int.TryParse(_customGrabAmount.Trim(), out int grabLimit) &&
                    grabLimit > 0;

                _flatscreen.GrabByName(
                    _customGrabFilter.Trim(),
                    hasLimit ? grabLimit : int.MaxValue);
            }

            GUILayout.EndArea();
        }
        private void DrawVoidMenu()
        {
            EnsureStyles();

            var outerRect = new Rect(_voidMenuPosition.x, _voidMenuPosition.y, VoidMenuWidth, VoidMenuHeight);
            _lastVoidMenuRect = outerRect;

            var shadowRect = new Rect(outerRect.x + 4f, outerRect.y + 5f, outerRect.width, outerRect.height);
            GUI.DrawTexture(shadowRect, _outerShadowTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(outerRect, _outerFrameTexture, ScaleMode.StretchToFill);

            var titleRect = new Rect(outerRect.x + OuterFramePadding, outerRect.y + OuterFramePadding, outerRect.width - OuterFramePadding * 2f, TitleBarHeight);
            GUI.DrawTexture(titleRect, _titleBarTexture, ScaleMode.StretchToFill);
            GUI.Label(new Rect(titleRect.x, titleRect.y + 1f, titleRect.width, titleRect.height), "VOID", _titleShadowStyle);
            GUI.Label(titleRect, "VOID", _titleLabelStyle);

            HandleDrag(titleRect, ref _voidMenuPosition, ref _isDraggingVoidMenu, ref _dragOffsetVoidMenu);

            var innerRect = new Rect(
                outerRect.x + OuterFramePadding,
                titleRect.yMax + 4f,
                outerRect.width - OuterFramePadding * 2f,
                outerRect.height - OuterFramePadding - TitleBarHeight - 8f);
            GUI.DrawTexture(innerRect, _innerBackgroundTexture, ScaleMode.StretchToFill);

            GUILayout.BeginArea(innerRect);

            Rect closeRect = GUILayoutUtility.GetRect(60f, ActionButtonHeight, GUILayout.Width(60f));
            if (DrawGoldButton(closeRect, "Close", false, false))
            {
                _voidMenuOpen = false;
            }
            GUILayout.Space(4f);

            Rect noclipRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            if (DrawGoldButton(noclipRect, FlatscreenCore.NoclipEnabled ? "Noclip: On" : "Noclip: Off", FlatscreenCore.NoclipEnabled, false))
            {
                FlatscreenCore.NoclipEnabled = !FlatscreenCore.NoclipEnabled;
            }
            GUILayout.Space(4f);

            Rect noTpRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            if (DrawGoldButton(noTpRect, FlatscreenCore.NoVoidTpEnabled ? "No Void TP: On" : "No Void TP: Off", FlatscreenCore.NoVoidTpEnabled, false))
            {
                FlatscreenCore.NoVoidTpEnabled = !FlatscreenCore.NoVoidTpEnabled;
            }
            GUILayout.Space(4f);

            // --- Super Fly: superhero-style flying mode, own fly option alongside the rest ---
            Rect superFlyRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            if (DrawGoldButton(superFlyRect, _superFly.Enabled ? "Super Fly: On" : "Super Fly: Off", _superFly.Enabled, false))
            {
                _superFly.Toggle();
            }
            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Peak Speed: " + _superFly.MaxSpeed.ToString("0.0"), _labelStyle);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            Rect speedDownRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            if (DrawGoldButton(speedDownRect, "-", false, false))
            {
                _superFly.AdjustMaxSpeed(-1f);
            }
            GUILayout.Space(4f);
            Rect speedUpRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            if (DrawGoldButton(speedUpRect, "+", false, false))
            {
                _superFly.AdjustMaxSpeed(1f);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }
        // --- in DrawServerTab(), replace the placeholder label with a real button ---
        private void DrawServerTab()
        {
            Rect soundsRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            if (DrawGoldButton(soundsRect, "Sounds", _soundsMenuOpen, false))
            {
                _soundsMenuOpen = true;
            }
        }
        // --- new menu, same pattern as DrawWeatherMenu ---
        private void DrawSoundsMenu()
        {
            EnsureStyles();

            var outerRect = new Rect(_soundsMenuPosition.x, _soundsMenuPosition.y, SoundsMenuWidth, SoundsMenuHeight);
            _lastSoundsMenuRect = outerRect;

            var shadowRect = new Rect(outerRect.x + 4f, outerRect.y + 5f, outerRect.width, outerRect.height);
            GUI.DrawTexture(shadowRect, _outerShadowTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(outerRect, _outerFrameTexture, ScaleMode.StretchToFill);

            var titleRect = new Rect(outerRect.x + OuterFramePadding, outerRect.y + OuterFramePadding, outerRect.width - OuterFramePadding * 2f, TitleBarHeight);
            GUI.DrawTexture(titleRect, _titleBarTexture, ScaleMode.StretchToFill);
            GUI.Label(new Rect(titleRect.x, titleRect.y + 1f, titleRect.width, titleRect.height), "SOUNDS", _titleShadowStyle);
            GUI.Label(titleRect, "SOUNDS", _titleLabelStyle);

            HandleDrag(titleRect, ref _soundsMenuPosition, ref _isDraggingSoundsMenu, ref _dragOffsetSoundsMenu);

            var innerRect = new Rect(
                outerRect.x + OuterFramePadding,
                titleRect.yMax + 4f,
                outerRect.width - OuterFramePadding * 2f,
                outerRect.height - OuterFramePadding - TitleBarHeight - 8f);
            GUI.DrawTexture(innerRect, _innerBackgroundTexture, ScaleMode.StretchToFill);

            GUILayout.BeginArea(innerRect);

            Rect closeRect = GUILayoutUtility.GetRect(60f, ActionButtonHeight, GUILayout.Width(60f));
            if (DrawGoldButton(closeRect, "Close", false, false))
            {
                _soundsMenuOpen = false;
            }
            GUILayout.Space(4f);

            Rect muteRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            bool muted = AmbienceSoundPatch.MuteAmbience;
            if (DrawGoldButton(muteRect, muted ? "Ambience Sounds: Muted" : "Ambience Sounds: On", muted, false))
            {
                AmbienceSoundPatch.MuteAmbience = !muted;
            }

            GUILayout.EndArea();
        }
        private void DrawFunnyTab()
        {
            Rect mirrorRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            bool mirrorEnabled = _flatscreen.MirrorEnabled;
            if (DrawGoldButton(mirrorRect, mirrorEnabled ? "Mirror: On" : "Mirror: Off", mirrorEnabled, false))
            {
                _flatscreen.ToggleMirror();
            }

            GUILayout.Space(6f);

            Rect jeanGreyRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            if (DrawGoldButton(jeanGreyRect, "Jean Grey", _jeanGreyMenuOpen, false))
            {
                _jeanGreyMenuOpen = true;
            }
            GUILayout.Space(2f);
            GUILayout.Label(_flatscreen.MirrorStatus, _labelStyle);


            GUILayout.Space(6f);

            Rect spiderRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            bool spiderEnabled = _spider.Enabled;
            if (DrawGoldButton(spiderRect, spiderEnabled ? "Spider: On" : "Spider: Off", spiderEnabled, false))
            {
                _spider.Toggle();
            }
            GUILayout.Space(6f);
            Rect tpEffectsRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(160f));
            if (DrawGoldButton(tpEffectsRect, "TP Effects", _tpEffectsMenuOpen || _tpEffects.IsActive, false))
            {
                _tpEffectsMenuOpen = true;
            }

        }
        private void DrawJeanGreyMenu()
        {
            EnsureStyles();

            var outerRect = new Rect(_jeanGreyMenuPosition.x, _jeanGreyMenuPosition.y, JeanGreyMenuWidth, JeanGreyMenuHeight);
            _lastJeanGreyMenuRect = outerRect;

            var shadowRect = new Rect(outerRect.x + 4f, outerRect.y + 5f, outerRect.width, outerRect.height);
            GUI.DrawTexture(shadowRect, _outerShadowTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(outerRect, _outerFrameTexture, ScaleMode.StretchToFill);

            var titleRect = new Rect(outerRect.x + OuterFramePadding, outerRect.y + OuterFramePadding, outerRect.width - OuterFramePadding * 2f, TitleBarHeight);
            GUI.DrawTexture(titleRect, _titleBarTexture, ScaleMode.StretchToFill);
            GUI.Label(new Rect(titleRect.x, titleRect.y + 1f, titleRect.width, titleRect.height), "JEAN GREY", _titleShadowStyle);
            GUI.Label(titleRect, "JEAN GREY", _titleLabelStyle);

            HandleDrag(titleRect, ref _jeanGreyMenuPosition, ref _isDraggingJeanGreyMenu, ref _dragOffsetJeanGreyMenu);

            var innerRect = new Rect(
                outerRect.x + OuterFramePadding,
                titleRect.yMax + 4f,
                outerRect.width - OuterFramePadding * 2f,
                outerRect.height - OuterFramePadding - TitleBarHeight - 8f);
            GUI.DrawTexture(innerRect, _innerBackgroundTexture, ScaleMode.StretchToFill);

            GUILayout.BeginArea(innerRect);

            GUILayout.BeginHorizontal();
            Rect closeRect = GUILayoutUtility.GetRect(60f, ActionButtonHeight, GUILayout.Width(60f));
            if (DrawGoldButton(closeRect, "Close", false, false))
            {
                _jeanGreyMenuOpen = false;
            }
            GUILayout.FlexibleSpace();
            Rect refreshRect = GUILayoutUtility.GetRect(70f, ActionButtonHeight, GUILayout.Width(70f));
            if (DrawGoldButton(refreshRect, "Refresh", false, false))
            {
                _jeanGrey.RefreshPlayers();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);

            Rect freeCamRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            bool freeCamEnabled = _jeanGrey.FreeCamEnabled;
            if (DrawGoldButton(freeCamRect, freeCamEnabled ? "Free Cam: On" : "Free Cam: Off", freeCamEnabled, false))
            {
                _jeanGrey.ToggleFreeCam();
            }

            GUILayout.Space(4f);

            GUILayout.Label("Spectate:", _panelHeaderStyle);
            _jeanGreyScroll = GUILayout.BeginScrollView(_jeanGreyScroll, GUILayout.ExpandWidth(true), GUILayout.Height(120f));
            int playerCount = _jeanGrey.PlayerCount;
            for (int i = 0; i < playerCount; i++)
            {
                Rect playerRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
                if (DrawGoldButton(playerRect, _jeanGrey.GetPlayerName(i), false, false))
                {
                    _jeanGrey.SpectateIndex(i);
                }
            }
            if (playerCount == 0)
            {
                GUILayout.Label("No players - press Refresh", _labelStyle);
            }
            GUILayout.EndScrollView();

            GUILayout.Space(2f);

            Rect stopRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            if (DrawGoldButton(stopRect, "Stop Spectating", _jeanGrey.IsSpectating, false))
            {
                _jeanGrey.StopSpectating();
            }

            GUILayout.Space(4f);

            Rect tkRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            bool tkEnabled = _jeanGrey.TelekinesisEnabled;
            if (DrawGoldButton(tkRect, tkEnabled ? "Telekinesis: On" : "Telekinesis: Off", tkEnabled, false))
            {
                _jeanGrey.ToggleTelekinesis();
            }

            GUILayout.Label(_jeanGrey.Status, _labelStyle);

            GUILayout.EndArea();
        }
        private void DrawTpEffectsMenu()
        {
            EnsureStyles();
            Rect outer = new Rect(_tpEffectsMenuPosition.x, _tpEffectsMenuPosition.y, TpEffectsMenuWidth, TpEffectsMenuHeight);
            _lastTpEffectsMenuRect = outer;
            GUI.DrawTexture(new Rect(outer.x + 4f, outer.y + 5f, outer.width, outer.height), _outerShadowTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(outer, _outerFrameTexture, ScaleMode.StretchToFill);
            Rect title = new Rect(outer.x + OuterFramePadding, outer.y + OuterFramePadding, outer.width - OuterFramePadding * 2f, TitleBarHeight);
            GUI.DrawTexture(title, _titleBarTexture, ScaleMode.StretchToFill);
            GUI.Label(new Rect(title.x, title.y + 1f, title.width, title.height), "TP EFFECTS", _titleShadowStyle);
            GUI.Label(title, "TP EFFECTS", _titleLabelStyle);
            HandleDrag(title, ref _tpEffectsMenuPosition, ref _isDraggingTpEffectsMenu, ref _dragOffsetTpEffectsMenu);

            Rect inner = new Rect(outer.x + OuterFramePadding, title.yMax + 4f, outer.width - OuterFramePadding * 2f, outer.height - OuterFramePadding - TitleBarHeight - 8f);
            GUI.DrawTexture(inner, _innerBackgroundTexture, ScaleMode.StretchToFill);
            GUILayout.BeginArea(inner);
            GUILayout.BeginHorizontal();
            GUILayout.Label(_tpEffects.Status, _labelStyle);
            GUILayout.FlexibleSpace();
            Rect close = GUILayoutUtility.GetRect(58f, ActionButtonHeight, GUILayout.Width(58f));
            if (DrawGoldButton(close, "Close", false, false)) _tpEffectsMenuOpen = false;
            GUILayout.EndHorizontal();
            GUILayout.Space(3f);
            Rect stopAll = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            if (DrawGoldButton(stopAll, "Stop All TP Effects", false, false)) _tpEffects.StopAll();

            _tpEffectsScroll = GUILayout.BeginScrollView(_tpEffectsScroll, GUILayout.ExpandWidth(true), GUILayout.Height(210f));
            DrawTpEffectToggle("Teleport Sphere", _tpEffects.SphereEnabled, value => _tpEffects.SphereEnabled = value);
            DrawTpEffectToggle("Teleport Cone", _tpEffects.ConeEnabled, value => _tpEffects.ConeEnabled = value);
            DrawTpEffectToggle("Teleport Star", _tpEffects.StarEnabled, value => _tpEffects.StarEnabled = value);
            DrawTpEffectToggle("Teleport Ring", _tpEffects.RingEnabled, value => _tpEffects.RingEnabled = value);
            DrawTpEffectToggle("Teleport Cube", _tpEffects.CubeEnabled, value => _tpEffects.CubeEnabled = value);
            DrawTpEffectToggle("Teleport Helix", _tpEffects.HelixEnabled, value => _tpEffects.HelixEnabled = value);
            DrawTpEffectToggle("Contracting Ring Arena", _tpEffects.RingArenaEnabled, value => _tpEffects.RingArenaEnabled = value);
            DrawTpEffectToggle("Other-player pillars (with sphere)", _tpEffects.PillarsEnabled, value => _tpEffects.PillarsEnabled = value);
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Scale: " + _tpEffects.Scale.ToString("0.0"), _panelHeaderStyle);
            float scale = GUILayout.HorizontalSlider(_tpEffects.Scale, 0.2f, 3f, GUILayout.ExpandWidth(true));
            _tpEffects.Scale = scale;
            GUILayout.EndHorizontal();
            GUILayout.Label("Shapes repeat each second; arena rings animate. Effect count is capped.", _labelStyle);
            GUILayout.EndArea();
        }

        private void DrawTpEffectToggle(string label, bool enabled, Action<bool> setEnabled)
        {
            Rect button = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            if (DrawGoldButton(button, label + (enabled ? ": On" : ": Off"), enabled, false))
                setEnabled(!enabled);
            GUILayout.Space(2f);
        }

        private void DrawParticlesMenu()
        {
            EnsureStyles();

            var outerRect = new Rect(_particlesMenuPosition.x, _particlesMenuPosition.y, ParticlesMenuWidth, ParticlesMenuHeight);
            _lastParticlesMenuRect = outerRect;

            var shadowRect = new Rect(outerRect.x + 4f, outerRect.y + 5f, outerRect.width, outerRect.height);
            GUI.DrawTexture(shadowRect, _outerShadowTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(outerRect, _outerFrameTexture, ScaleMode.StretchToFill);

            var titleRect = new Rect(outerRect.x + OuterFramePadding, outerRect.y + OuterFramePadding, outerRect.width - OuterFramePadding * 2f, TitleBarHeight);
            GUI.DrawTexture(titleRect, _titleBarTexture, ScaleMode.StretchToFill);
            GUI.Label(new Rect(titleRect.x, titleRect.y + 1f, titleRect.width, titleRect.height), "PARTICLES", _titleShadowStyle);
            GUI.Label(titleRect, "PARTICLES", _titleLabelStyle);

            HandleDrag(titleRect, ref _particlesMenuPosition, ref _isDraggingParticlesMenu, ref _dragOffsetParticlesMenu);

            var innerRect = new Rect(
                outerRect.x + OuterFramePadding,
                titleRect.yMax + 4f,
                outerRect.width - OuterFramePadding * 2f,
                outerRect.height - OuterFramePadding - TitleBarHeight - 8f);
            GUI.DrawTexture(innerRect, _innerBackgroundTexture, ScaleMode.StretchToFill);

            GUILayout.BeginArea(innerRect);

            GUILayout.BeginHorizontal();
            Rect closeRect = GUILayoutUtility.GetRect(60f, ActionButtonHeight, GUILayout.Width(60f));
            if (DrawGoldButton(closeRect, "Close", false, false))
            {
                _particlesMenuOpen = false;
            }
            GUILayout.FlexibleSpace();
            Rect refreshRect = GUILayoutUtility.GetRect(70f, ActionButtonHeight, GUILayout.Width(70f));
            if (DrawGoldButton(refreshRect, "Refresh", false, false))
            {
                _particles.Refresh();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);

            GUILayout.Label(_particles.SelectedName, _panelHeaderStyle);
            GUILayout.Label(_particles.Status, _labelStyle);
            GUILayout.Space(4f);

            _particlesMenuScroll = GUILayout.BeginScrollView(_particlesMenuScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            for (int i = 0; i < _particles.Count; i++)
            {
                bool selected = _particles.SelectedIndex == i;
                Rect itemRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
                if (DrawGoldButton(itemRect, _particles.GetName(i), selected, false))
                {
                    _particles.Select(i);
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Space(4f);

            Rect wallRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            if (DrawGoldButton(wallRect, "Spawn Wall", _particles.HasSelection, false))
            {
                _particles.SpawnWall();
            }
            GUILayout.Space(4f);

            Rect singleRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            if (DrawGoldButton(singleRect, "Spawn Single", _particles.HasSelection, false))
            {
                _particles.SpawnSingle();
            }

            GUILayout.EndArea();
        }
        private void DrawAnythingTab()
        {
            Rect weatherRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            bool weatherRightClicked = WasRightClicked(weatherRect); // must run BEFORE DrawGoldButton
            bool weatherLeftClicked = DrawGoldButton(weatherRect, "Weather", _weatherMenuOpen, false);
            if (weatherRightClicked || weatherLeftClicked)
            {
                _weatherMenuOpen = true;
            }
            GUILayout.Space(6f);
            Rect graphicsRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            if (DrawGoldButton(graphicsRect, "Graphics", _graphicsMenuOpen, false))
            {
                _graphicsMenuOpen = true;
            }
            GUILayout.Space(6f);

            Rect perfRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            bool perfRightClicked = WasRightClicked(perfRect); // must run BEFORE DrawGoldButton
            bool perfLeftClicked = DrawGoldButton(perfRect, "Performance", _perfMenuOpen, false);
            if (perfRightClicked || perfLeftClicked)
            {
                _perfMenuOpen = true;
            }
            GUILayout.Space(6f);
        }

        private void DrawGraphicsMenu()
        {
            EnsureStyles();

            var outerRect = new Rect(_graphicsMenuPosition.x, _graphicsMenuPosition.y, GraphicsMenuWidth, GraphicsMenuHeight);
            _lastGraphicsMenuRect = outerRect;

            var shadowRect = new Rect(outerRect.x + 4f, outerRect.y + 5f, outerRect.width, outerRect.height);
            GUI.DrawTexture(shadowRect, _outerShadowTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(outerRect, _outerFrameTexture, ScaleMode.StretchToFill);

            var titleRect = new Rect(outerRect.x + OuterFramePadding, outerRect.y + OuterFramePadding, outerRect.width - OuterFramePadding * 2f, TitleBarHeight);
            GUI.DrawTexture(titleRect, _titleBarTexture, ScaleMode.StretchToFill);
            GUI.Label(new Rect(titleRect.x, titleRect.y + 1f, titleRect.width, titleRect.height), "GRAPHICS", _titleShadowStyle);
            GUI.Label(titleRect, "GRAPHICS", _titleLabelStyle);

            HandleDrag(titleRect, ref _graphicsMenuPosition, ref _isDraggingGraphicsMenu, ref _dragOffsetGraphicsMenu);

            var innerRect = new Rect(
                outerRect.x + OuterFramePadding,
                titleRect.yMax + 4f,
                outerRect.width - OuterFramePadding * 2f,
                outerRect.height - OuterFramePadding - TitleBarHeight - 8f);
            GUI.DrawTexture(innerRect, _innerBackgroundTexture, ScaleMode.StretchToFill);

            GUILayout.BeginArea(innerRect);

            Rect closeRect = GUILayoutUtility.GetRect(60f, ActionButtonHeight, GUILayout.Width(60f));
            if (DrawGoldButton(closeRect, "Close", false, false))
            {
                _graphicsMenuOpen = false;
            }
            GUILayout.Space(4f);

            Rect lightingRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            bool lightingEnabled = _graphics.LightingOverhaulEnabled;
            if (DrawGoldButton(lightingRect, lightingEnabled ? "Lighting Overhaul: On" : "Lighting Overhaul: Off", lightingEnabled, false))
            {
                _graphics.ToggleLightingOverhaul();
            }

            GUILayout.EndArea();
        }
        private void DrawPerfMenu()
        {
            EnsureStyles();

            var outerRect = new Rect(_perfMenuPosition.x, _perfMenuPosition.y, PerfMenuWidth, PerfMenuHeight);
            _lastPerfMenuRect = outerRect;

            var shadowRect = new Rect(outerRect.x + 4f, outerRect.y + 5f, outerRect.width, outerRect.height);
            GUI.DrawTexture(shadowRect, _outerShadowTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(outerRect, _outerFrameTexture, ScaleMode.StretchToFill);

            var titleRect = new Rect(outerRect.x + OuterFramePadding, outerRect.y + OuterFramePadding, outerRect.width - OuterFramePadding * 2f, TitleBarHeight);
            GUI.DrawTexture(titleRect, _titleBarTexture, ScaleMode.StretchToFill);
            GUI.Label(new Rect(titleRect.x, titleRect.y + 1f, titleRect.width, titleRect.height), "PERFORMANCE", _titleShadowStyle);
            GUI.Label(titleRect, "PERFORMANCE", _titleLabelStyle);

            HandleDrag(titleRect, ref _perfMenuPosition, ref _isDraggingPerfMenu, ref _dragOffsetPerfMenu);

            var innerRect = new Rect(
                outerRect.x + OuterFramePadding,
                titleRect.yMax + 4f,
                outerRect.width - OuterFramePadding * 2f,
                outerRect.height - OuterFramePadding - TitleBarHeight - 8f);
            GUI.DrawTexture(innerRect, _innerBackgroundTexture, ScaleMode.StretchToFill);

            GUILayout.BeginArea(innerRect);

            Rect closeRect = GUILayoutUtility.GetRect(60f, ActionButtonHeight, GUILayout.Width(60f));
            if (DrawGoldButton(closeRect, "Close", false, false))
            {
                _perfMenuOpen = false;
            }
            GUILayout.Space(4f);

            Rect msaaRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            bool msaaEnabled = _performance.MsaaEnabled;
            if (DrawGoldButton(msaaRect, msaaEnabled ? "MSAA: ON (2x)" : "MSAA: OFF", msaaEnabled, false))
            {
                _performance.SetMsaaEnabled(!msaaEnabled);
            }
            GUILayout.Space(4f);

            Rect overviewRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            bool overviewEnabled = _performance.OverviewEnabled;
            if (DrawGoldButton(overviewRect, overviewEnabled ? "Overview Cam: ON" : "Overview Cam: OFF", overviewEnabled, false))
            {
                _performance.SetOverviewEnabled(!overviewEnabled);
            }
            GUILayout.Space(4f);

            Rect grassRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            bool grassEnabled = PerformanceController.GrassEnabled;
            if (DrawGoldButton(grassRect, grassEnabled ? "Grass: ON" : "Grass: OFF", grassEnabled, false))
            {
                PerformanceController.SetGrassEnabled(!grassEnabled);
            }
            GUILayout.Space(4f);

            Rect bloomRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            bool bloomEnabled = PerformanceController.BloomEnabled;
            if (DrawGoldButton(bloomRect, bloomEnabled ? "Bloom: ON" : "Bloom: OFF", bloomEnabled, false))
            {
                PerformanceController.SetBloomEnabled(!bloomEnabled);
            }

            GUILayout.EndArea();
        }
        private void DrawWeatherMenu()
        {
            EnsureStyles();

            var outerRect = new Rect(_weatherMenuPosition.x, _weatherMenuPosition.y, WeatherMenuWidth, WeatherMenuHeight);
            _lastWeatherMenuRect = outerRect;

            var shadowRect = new Rect(outerRect.x + 4f, outerRect.y + 5f, outerRect.width, outerRect.height);
            GUI.DrawTexture(shadowRect, _outerShadowTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(outerRect, _outerFrameTexture, ScaleMode.StretchToFill);

            var titleRect = new Rect(outerRect.x + OuterFramePadding, outerRect.y + OuterFramePadding, outerRect.width - OuterFramePadding * 2f, TitleBarHeight);
            GUI.DrawTexture(titleRect, _titleBarTexture, ScaleMode.StretchToFill);
            GUI.Label(new Rect(titleRect.x, titleRect.y + 1f, titleRect.width, titleRect.height), "WEATHER", _titleShadowStyle);
            GUI.Label(titleRect, "WEATHER", _titleLabelStyle);

            HandleDrag(titleRect, ref _weatherMenuPosition, ref _isDraggingWeatherMenu, ref _dragOffsetWeatherMenu);

            var innerRect = new Rect(
                outerRect.x + OuterFramePadding,
                titleRect.yMax + 4f,
                outerRect.width - OuterFramePadding * 2f,
                outerRect.height - OuterFramePadding - TitleBarHeight - 8f);
            GUI.DrawTexture(innerRect, _innerBackgroundTexture, ScaleMode.StretchToFill);

            GUILayout.BeginArea(innerRect);

            Rect closeRect = GUILayoutUtility.GetRect(60f, ActionButtonHeight, GUILayout.Width(60f));
            if (DrawGoldButton(closeRect, "Close", false, false))
            {
                _weatherMenuOpen = false;
            }
            GUILayout.Space(4f);

            Rect rainRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.ExpandWidth(true));
            bool rainEnabled = WeatherController.RainEnabled;
            if (DrawGoldButton(rainRect, rainEnabled ? "Rain: On" : "Rain: Off", rainEnabled, false))
            {
                WeatherController.SetRainEnabled(!rainEnabled);
            }

            GUILayout.EndArea();
        }
        // Full PanKake panel - opened by right-clicking the PanKake button. Hosts everything the
        // old standalone Flatscreen menu could do (status/debug readout + the full server-browser
        // tab set: Servers/Hands/Camera/Actions/System), scrollable since it's a lot of content.
        private void DrawPanKakePanel()
        {
            EnsureStyles();

            var outerRect = new Rect(_panKakePosition.x, _panKakePosition.y, PanKakeWidth, PanKakeHeight);
            _lastPanKakeRect = outerRect;

            var shadowRect = new Rect(outerRect.x + 4f, outerRect.y + 5f, outerRect.width, outerRect.height);
            GUI.DrawTexture(shadowRect, _outerShadowTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(outerRect, _outerFrameTexture, ScaleMode.StretchToFill);

            var titleRect = new Rect(outerRect.x + OuterFramePadding, outerRect.y + OuterFramePadding, outerRect.width - OuterFramePadding * 2f, TitleBarHeight);
            GUI.DrawTexture(titleRect, _titleBarTexture, ScaleMode.StretchToFill);
            GUI.Label(new Rect(titleRect.x, titleRect.y + 1f, titleRect.width, titleRect.height), "PANKAKE", _titleShadowStyle);
            GUI.Label(titleRect, "PANKAKE", _titleLabelStyle);

            // Drag the panel by its title bar.
            HandleDrag(titleRect, ref _panKakePosition, ref _isDraggingPanKake, ref _dragOffsetPanKake);

            var innerRect = new Rect(
                outerRect.x + OuterFramePadding,
                titleRect.yMax + 4f,
                outerRect.width - OuterFramePadding * 2f,
                outerRect.height - OuterFramePadding - TitleBarHeight - 8f);
            GUI.DrawTexture(innerRect, _innerBackgroundTexture, ScaleMode.StretchToFill);

            GUILayout.BeginArea(innerRect);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Full PanKake panel", _panelHeaderStyle, GUILayout.ExpandWidth(true));
            Rect closeRect = GUILayoutUtility.GetRect(70f, ActionButtonHeight, GUILayout.Width(70f));
            if (DrawGoldButton(closeRect, "Close", false, false))
            {
                _panKakePanelOpen = false;
            }
            GUILayout.EndHorizontal();

            Rect enabledRect = GUILayoutUtility.GetRect(1f, ActionButtonHeight, GUILayout.Width(140f));
            if (DrawGoldButton(enabledRect, _panKakeEnabled ? "PanKake: On" : "PanKake: Off", _panKakeEnabled, false))
            {
                TogglePanKake();
            }

            GUILayout.Space(6f);
            _panKakeScroll = GUILayout.BeginScrollView(_panKakeScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // Everything from the old Flatscreen mod (status readout + the full Servers/Hands/
            // Camera/Actions/System tab set) draws through GUILayout.Label/Button/Slider/Toggle
            // with no explicit style of its own, so swapping GUI.skin for the duration reskins
            // all of it into the TavernFun gold/tan look instead of Unity's default grey boxes.
            GUISkin previousSkin = GUI.skin;
            try
            {
                GUI.skin = _panKakeSkin;

                GUILayout.Label("Status", _panelHeaderStyle);
                _flatscreen.DrawDebugContent();

                GUILayout.Space(10f);
                GUILayout.Label("Controls", _panelHeaderStyle);
                _flatscreen.DrawServerBrowserContent();
            }
            finally
            {
                GUI.skin = previousSkin;
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private bool DrawGoldButton(Rect rect, string label, bool bright, bool isTab)
        {
            Texture2D texture = isTab
                ? (bright ? _goldButtonTexture : _goldButtonDimTexture)
                : (bright ? _actionButtonTexture : _actionButtonDimTexture);
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill);
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            var shadowRect = new Rect(rect.x + 1f, rect.y + 2f, rect.width, rect.height);
            GUIStyle shadowStyle = isTab ? _shadowLabelStyle : _actionShadowLabelStyle;
            GUIStyle labelStyle = isTab ? _labelStyle : _actionLabelStyle;
            GUI.Label(shadowRect, label, shadowStyle);
            GUI.Label(rect, label, labelStyle);
            return clicked;
        }


        private static bool WasRightClicked(Rect rect)
        {
            Event current = Event.current;
            if (current != null && current.type == EventType.MouseDown && current.button == 1 && rect.Contains(current.mousePosition))
            {
                current.Use();
                return true;
            }
            return false;
        }


        private static void HandleDrag(Rect dragRect, ref Vector2 position, ref bool isDragging, ref Vector2 dragOffset)
        {
            Event current = Event.current;
            if (current == null)
            {
                return;
            }
            if (!isDragging && current.type == EventType.MouseDown && current.button == 0 && dragRect.Contains(current.mousePosition))
            {
                isDragging = true;
                dragOffset = current.mousePosition - position;
                current.Use();
            }
            else if (isDragging && current.type == EventType.MouseDrag)
            {
                position = current.mousePosition - dragOffset;
                position.x = Mathf.Clamp(position.x, -MenuWidth + 60f, Screen.width - 60f);
                position.y = Mathf.Clamp(position.y, 0f, Screen.height - 40f);
                current.Use();
            }
            else if (isDragging && current.type == EventType.MouseUp)
            {
                isDragging = false;
                current.Use();
            }
        }

        private void TogglePanKake()
        {
            _panKakeEnabled = !_panKakeEnabled;
            FlatscreenCore.Enabled = _panKakeEnabled;
        }

        private void EnsureStyles()
        {
            if (_stylesReady)
            {
                return;
            }
            _outerFrameTexture = CreateSolidTexture(OuterFrameColor);
            _outerShadowTexture = CreateSolidTexture(OuterFrameShadow);
            _innerBackgroundTexture = CreateBorderedTexture(InnerBackgroundColor, InnerBorderColor, 2);
            _titleBarTexture = CreateVerticalGradientTexture(TitleBarTop, TitleBarBottom);
            _goldButtonTexture = CreateGoldButtonTexture(1f);
            _goldButtonDimTexture = CreateGoldButtonTexture(0.6f);
            _actionButtonTexture = CreateRoundedActionButtonTexture(1f, ActionButtonCornerRadius);
            _actionButtonDimTexture = CreateRoundedActionButtonTexture(0.62f, ActionButtonCornerRadius);

            // Scrollbar look: solid dark-brown track (inside) with a solid gold thumb (outside),
            // both fully opaque and square-edged (no rounding, no default Unity transparency).
            _scrollTrackTexture = CreateSolidTexture(InnerBackgroundColor);
            _scrollThumbTexture = CreateSolidTexture(GoldTop);
            _scrollThumbActiveTexture = CreateSolidTexture(GoldBottom);
            ApplyScrollbarStyle(GUI.skin);

            // No custom font asset is bundled here — this uses the default font, bold + shadowed.
            // Drop in a Font via Resources/AssetBundle and assign it to _labelStyle.font /
            // _shadowLabelStyle.font for an actual "Old Town" look.
            _shadowLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };
            _shadowLabelStyle.normal.textColor = ShadowColor;
            _labelStyle = new GUIStyle(_shadowLabelStyle);
            _labelStyle.normal.textColor = Color.white;

            _actionShadowLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 11
            };
            _actionShadowLabelStyle.normal.textColor = ShadowColor;
            _actionLabelStyle = new GUIStyle(_actionShadowLabelStyle);
            _actionLabelStyle.normal.textColor = Color.white;

            _titleShadowStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };
            _titleShadowStyle.normal.textColor = ShadowColor;
            _titleLabelStyle = new GUIStyle(_titleShadowStyle);
            _titleLabelStyle.normal.textColor = TitleTextColor;

            _panelHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                fontSize = 13
            };
            _panelHeaderStyle.normal.textColor = TitleTextColor;

            _panKakeSkin = BuildPanKakeSkin();

            _stylesReady = true;
        }

        // Builds a GUISkin so every default-styled GUILayout.Label/Button/Slider/Toggle call
        // inside the ported-in Flatscreen code (which was never written with TavernFun's custom
        // DrawGoldButton in mind) still comes out looking like the rest of this menu instead of
        // Unity's default grey boxes. Starts from Unity's built-in skin so padding/fonts/etc.
        // stay sane, then only the colors/textures are overridden. Buttons here are treated as
        // "action" buttons (lighter/smaller/rounded), matching everything but the tab strip.
        private GUISkin BuildPanKakeSkin()
        {
            GUISkin skin = ScriptableObject.CreateInstance<GUISkin>();
            skin.button = new GUIStyle(GUI.skin.button);
            skin.label = new GUIStyle(GUI.skin.label);
            skin.box = new GUIStyle(GUI.skin.box);
            skin.toggle = new GUIStyle(GUI.skin.toggle);
            skin.horizontalSlider = new GUIStyle(GUI.skin.horizontalSlider);
            skin.horizontalSliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb);
            skin.verticalScrollbar = new GUIStyle(GUI.skin.verticalScrollbar);
            skin.verticalScrollbarThumb = new GUIStyle(GUI.skin.verticalScrollbarThumb);
            skin.horizontalScrollbar = new GUIStyle(GUI.skin.horizontalScrollbar);
            skin.horizontalScrollbarThumb = new GUIStyle(GUI.skin.horizontalScrollbarThumb);
            skin.font = GUI.skin.font;
            skin.settings.cursorColor = GUI.skin.settings.cursorColor;
            skin.settings.cursorFlashSpeed = GUI.skin.settings.cursorFlashSpeed;
            skin.settings.doubleClickSelectsWord = GUI.skin.settings.doubleClickSelectsWord;
            skin.settings.tripleClickSelectsLine = GUI.skin.settings.tripleClickSelectsLine;

            Texture2D slotBox = CreateBorderedTexture(new Color32(54, 36, 22, 255), InnerBorderColor, 2);
            Texture2D sliderTrack = CreateBorderedTexture(new Color32(24, 15, 9, 255), InnerBorderColor, 1);
            Texture2D sliderThumb = CreateSolidTexture(new Color32(224, 186, 96, 255));
            Texture2D sliderThumbActive = CreateSolidTexture(GoldBottom);

            skin.button.normal.background = _actionButtonTexture;
            skin.button.hover.background = _actionButtonTexture;
            skin.button.active.background = _actionButtonDimTexture;
            skin.button.onNormal.background = _actionButtonDimTexture;
            skin.button.normal.textColor = Color.white;
            skin.button.hover.textColor = Color.white;
            skin.button.active.textColor = Color.white;
            skin.button.onNormal.textColor = Color.white;
            skin.button.fontStyle = FontStyle.Bold;
            skin.button.fontSize = 11;
            skin.button.fixedHeight = ActionButtonHeight;
            skin.button.alignment = TextAnchor.MiddleCenter;

            skin.label.normal.textColor = Color.white;

            skin.box.normal.background = slotBox;
            skin.box.normal.textColor = TitleTextColor;
            skin.box.fontStyle = FontStyle.Bold;

            skin.toggle.normal.textColor = Color.white;
            skin.toggle.onNormal.textColor = Color.white;
            skin.toggle.hover.textColor = Color.white;

            skin.horizontalSlider.normal.background = sliderTrack;
            skin.horizontalSliderThumb.normal.background = sliderThumb;
            skin.horizontalSliderThumb.hover.background = sliderThumb;
            skin.horizontalSliderThumb.active.background = sliderThumbActive;

            skin.verticalScrollbarThumb.normal.background = sliderThumb;
            skin.horizontalScrollbarThumb.normal.background = sliderThumb;

            ApplyScrollbarStyle(skin);

            return skin;
        }

        // Solid dark-brown track / solid gold thumb, square-edged, fully opaque - applied to
        // both GUI.skin (so every plain GUILayout.BeginScrollView in the mod picks it up) and
        // to the dedicated PanKake skin.
        private void ApplyScrollbarStyle(GUISkin skin)
        {
            void Style(GUIStyle track, GUIStyle thumb)
            {
                track.normal.background = _scrollTrackTexture;
                track.hover.background = _scrollTrackTexture;
                track.active.background = _scrollTrackTexture;
                track.focused.background = _scrollTrackTexture;
                track.border = new RectOffset(0, 0, 0, 0);
                track.padding = new RectOffset(0, 0, 0, 0);

                thumb.normal.background = _scrollThumbTexture;
                thumb.hover.background = _scrollThumbTexture;
                thumb.active.background = _scrollThumbActiveTexture;
                thumb.focused.background = _scrollThumbTexture;
                thumb.border = new RectOffset(0, 0, 0, 0);
                thumb.padding = new RectOffset(0, 0, 0, 0);
            }

            Style(skin.verticalScrollbar, skin.verticalScrollbarThumb);
            Style(skin.horizontalScrollbar, skin.horizontalScrollbarThumb);
        }

        private static Texture2D CreateSolidTexture(Color32 color)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private static Texture2D CreateBorderedTexture(Color32 fill, Color32 border, int borderThickness)
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int yy = 0; yy < size; yy++)
            {
                for (int xx = 0; xx < size; xx++)
                {
                    bool isBorder = xx < borderThickness || yy < borderThickness || xx >= size - borderThickness || yy >= size - borderThickness;
                    pixels[yy * size + xx] = isBorder ? border : fill;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        // Flat vertical gradient with no border - used for the new title bar strip.
        private static Texture2D CreateVerticalGradientTexture(Color32 top, Color32 bottom)
        {
            const int size = 16;
            var texture = new Texture2D(1, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size];
            for (int yy = 0; yy < size; yy++)
            {
                float t = (float)yy / (size - 1);
                pixels[yy] = Color32.Lerp(top, bottom, t);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        // Flat vertical gold gradient with a thin dark border and a soft top highlight / bottom
        // shade line - used for the tab strip only.
        private static Texture2D CreateGoldButtonTexture(float brightness)
        {
            var texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false);
            var pixels = new Color32[TextureWidth * TextureHeight];
            for (int yy = 0; yy < TextureHeight; yy++)
            {
                float t = (float)yy / (TextureHeight - 1);
                Color rowColor = Color.Lerp(GoldTop, GoldBottom, t) * brightness;
                rowColor.a = 1f;
                for (int xx = 0; xx < TextureWidth; xx++)
                {
                    bool isBorder = xx == 0 || yy == 0 || xx == TextureWidth - 1 || yy == TextureHeight - 1;
                    Color final = isBorder ? (Color)GoldBorder * brightness : rowColor;
                    final.a = 1f;
                    if (!isBorder && yy == 1)
                    {
                        final = Color.Lerp(final, Color.white, 0.35f);
                    }
                    if (!isBorder && yy == TextureHeight - 2)
                    {
                        final = Color.Lerp(final, Color.black, 0.18f);
                    }
                    pixels[yy * TextureWidth + xx] = final;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        // Lighter/paler gold gradient than the tab texture, with rounded corners baked into the
        // alpha channel, for every non-tab button (PanKake toggle, Close, panel controls, and
        // everything drawn through the ported-in flatscreen skin).
        private static Texture2D CreateRoundedActionButtonTexture(float brightness, int cornerRadius)
        {
            var texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false);
            var pixels = new Color32[TextureWidth * TextureHeight];
            Color lightTop = Color.Lerp(GoldTop, Color.white, 0.30f);
            Color lightBottom = Color.Lerp(GoldBottom, Color.white, 0.20f);
            for (int yy = 0; yy < TextureHeight; yy++)
            {
                float t = (float)yy / (TextureHeight - 1);
                Color rowColor = Color.Lerp(lightTop, lightBottom, t) * brightness;
                rowColor.a = 1f;
                for (int xx = 0; xx < TextureWidth; xx++)
                {
                    bool isBorder = xx == 0 || yy == 0 || xx == TextureWidth - 1 || yy == TextureHeight - 1;
                    Color final = isBorder ? Color.Lerp((Color)GoldBorder, Color.white, 0.15f) * brightness : rowColor;
                    final.a = 1f;
                    if (!isBorder && yy == 1)
                    {
                        final = Color.Lerp(final, Color.white, 0.45f);
                    }
                    if (!isBorder && yy == TextureHeight - 2)
                    {
                        final = Color.Lerp(final, Color.black, 0.10f);
                    }
                    final.a = RoundedCornerAlpha(xx, yy, TextureWidth, TextureHeight, cornerRadius);
                    pixels[yy * TextureWidth + xx] = final;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        // Antialiased alpha mask for rounded corners: 1 inside the rounded rect, 0 outside it,
        // with a 1px soft edge in between so the corner doesn't look jagged.
        private static float RoundedCornerAlpha(int x, int y, int width, int height, int radius)
        {
            if (radius <= 0)
            {
                return 1f;
            }
            int cx, cy;
            if (x < radius && y < radius)
            {
                cx = radius; cy = radius;
            }
            else if (x >= width - radius && y < radius)
            {
                cx = width - radius - 1; cy = radius;
            }
            else if (x < radius && y >= height - radius)
            {
                cx = radius; cy = height - radius - 1;
            }
            else if (x >= width - radius && y >= height - radius)
            {
                cx = width - radius - 1; cy = height - radius - 1;
            }
            else
            {
                return 1f;
            }
            float dx = x - cx;
            float dy = y - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist <= radius - 1f)
            {
                return 1f;
            }
            if (dist >= radius + 1f)
            {
                return 0f;
            }
            return Mathf.Clamp01(radius + 1f - dist);
        }

        private sealed class MenuKeyInput
        {
            public bool IsMenuTogglePressed
            {
                get
                {
                    return WasPressed("mKey");
                }
            }

            private Type _keyboardType;
            private PropertyInfo _keyboardCurrentProperty;

            private bool WasPressed(string keyProperty)
            {
                EnsureTypes();
                object keyboard = (_keyboardCurrentProperty == null) ? null : _keyboardCurrentProperty.GetValue(null, null);
                if (keyboard == null)
                {
                    return false;
                }
                PropertyInfo keyProp = keyboard.GetType().GetProperty(keyProperty, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object key = (keyProp == null) ? null : keyProp.GetValue(keyboard, null);
                if (key == null)
                {
                    return false;
                }
                PropertyInfo pressedProp = key.GetType().GetProperty("wasPressedThisFrame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return pressedProp != null && (bool)pressedProp.GetValue(key, null);
            }

            private void EnsureTypes()
            {
                if (_keyboardType != null)
                {
                    return;
                }
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    _keyboardType = assembly.GetType("UnityEngine.InputSystem.Keyboard");
                    if (_keyboardType != null)
                    {
                        break;
                    }
                }
                _keyboardCurrentProperty = (_keyboardType == null) ? null : _keyboardType.GetProperty("current", BindingFlags.Static | BindingFlags.Public);
            }
        }
    }

    // Funny-tab Jean Grey mode: free cam + spectate any player, plus telekinesis. Spectate targets
    // come from the same Player.AllPlayers roster the minimap mods read (falling back to
    // Players/OnlinePlayers/etc.). Telekinesis force-grabs the item you're looking at and steers
    // the held hand so the game's own held-item networking moves it for everyone to see.
    internal sealed class JeanGreyController
    {
        internal static JeanGreyController Instance { get; private set; }
        internal bool FreeCamEnabled { get; private set; }
        internal bool TelekinesisEnabled { get; private set; }
        internal bool IsSpectating { get { return _spectateTarget != null; } }
        internal string Status { get; private set; } = "Press Refresh to list players";
        internal int PlayerCount { get { return _players.Count; } }

        private readonly GameReflection _game = new GameReflection();
        private readonly List<object> _players = new List<object>();
        private readonly List<string> _playerNames = new List<string>();

        private object _spectateTarget;
        private bool _freeCamInitialized;
        private Vector2 _freeCamLook;
        private Vector3 _freeCamPos;
        private Camera _spectatorCamera;

        private bool _bodyFrozen;
        private Transform _frozenBody;
        private Vector3 _frozenBodyPosition;
        private Quaternion _frozenBodyRotation;

        private bool _tkHolding;
        private float _tkDepth = 2.2f;
        private Vector3 _tkOffset;
        private Vector3 _tkSmooth;

        private Type _keyboardType;
        private PropertyInfo _keyboardCurrentProperty;

        private static readonly string[] PlayerRosterNames = new string[]
        {
            "AllPlayers", "Players", "PlayerList", "OnlinePlayers",
            "ConnectedPlayers", "RemotePlayers", "CurrentPlayers", "ActivePlayers"
        };
        private static readonly string[] PlayerNameMembers = new string[]
        {
            "Username", "UserName", "DisplayName", "PlayerName", "Nickname", "Name"
        };

        internal void Init()
        {
            Instance = this;
        }

        internal void Tick()
        {
            FlatscreenCore.SuppressPlayerMovement = FreeCamEnabled || IsSpectating;
            if (FreeCamEnabled || IsSpectating)
            {
                KeepBodyFrozen();
            }
            if (TelekinesisEnabled)
            {
                TickTelekinesis();
            }
        }

        internal void LateTick()
        {
            if (FreeCamEnabled)
            {
                TickFreeCam();
            }
            else if (_spectateTarget != null)
            {
                TickSpectate();
            }
        }
        internal int RefreshPlayers()
        {
            _players.Clear();
            _playerNames.Clear();
            Type playerType = FindType("Player");
            if (playerType == null)
            {
                Status = "Player type not found";
                return 0;
            }
            object roster = null;
            foreach (string name in PlayerRosterNames)
            {
                roster = ReadStaticMember(playerType, name);
                if (roster != null)
                {
                    break;
                }
            }
            if (roster != null)
            {
                AddPlayersFromRoster(roster);
            }
            object current = ReadStaticMember(playerType, "Current");
            if (current != null)
            {
                AddPlayer(current);
            }
            object local = _game.FindLocalPlayer();
            for (int i = _players.Count - 1; i >= 0; i--)
            {
                if (object.ReferenceEquals(_players[i], local))
                {
                    _players.RemoveAt(i);
                    _playerNames.RemoveAt(i);
                }
            }
            Status = _players.Count + " other player(s) found";
            return _players.Count;
        }

        internal string GetPlayerName(int index)
        {
            return (index >= 0 && index < _playerNames.Count) ? _playerNames[index] : string.Empty;
        }

        internal void SpectateIndex(int index)
        {
            if (index < 0 || index >= _players.Count)
            {
                return;
            }
            FreeCamEnabled = false;
            _spectateTarget = _players[index];
            EnsureSpectatorCamera();
            FreezeBody();
            Status = "Spectating " + _playerNames[index] + " - press Stop to exit";
        }

        internal void StopSpectating()
        {
            _spectateTarget = null;
            StopViewModes();
            Status = "Spectating stopped";
        }

        internal void ToggleFreeCam()
        {
            FreeCamEnabled = !FreeCamEnabled;
            if (FreeCamEnabled)
            {
                _spectateTarget = null;
                _freeCamInitialized = false;
                EnsureSpectatorCamera();
                FreezeBody();
                Status = "Free cam on - WASD moves, mouse looks, Space/Ctrl up/down. Inputs locked.";
            }
            else
            {
                StopViewModes();
                Status = "Free cam off";
            }
        }

        private void StopViewModes()
        {
            UnfreezeBody();
            if (_spectatorCamera != null)
            {
                _spectatorCamera.enabled = false;
            }
        }

        // Freeze the local body so it can't chase the camera or react to input while spectating.
        private void FreezeBody()
        {
            object player = _game.FindLocalPlayer();
            Transform root = _game.GetPlayerRootTransform(player);
            _frozenBody = root;
            if (root != null)
            {
                _frozenBodyPosition = root.position;
                _frozenBodyRotation = root.rotation;
            }
            _bodyFrozen = true;
            if (player != null)
            {
                _game.TrySetDebugMovement(player, true);
            }
        }

        private void UnfreezeBody()
        {
            if (!_bodyFrozen)
            {
                return;
            }
            _bodyFrozen = false;
            _frozenBody = null;
            object player = _game.FindLocalPlayer();
            if (player != null)
            {
                _game.TrySetDebugMovement(player, false);
            }
        }

        private void KeepBodyFrozen()
        {
            if (_frozenBody != null)
            {
                _frozenBody.position = _frozenBodyPosition;
                _frozenBody.rotation = _frozenBodyRotation;
            }
        }

        // A dedicated camera renders OVER the game camera, so we never disturb the player's own
        // camera or body - the game's first-person view just gets covered by the spectate view.
        private Camera EnsureSpectatorCamera()
        {
            if (_spectatorCamera != null)
            {
                _spectatorCamera.enabled = true;
                return _spectatorCamera;
            }
            Camera source = Camera.main;
            GameObject go = new GameObject("Jean Grey Spectator Camera");
            Object.DontDestroyOnLoad(go);
            _spectatorCamera = go.AddComponent<Camera>();
            if (source != null)
            {
                _spectatorCamera.cullingMask = source.cullingMask;
                _spectatorCamera.clearFlags = source.clearFlags;
                _spectatorCamera.backgroundColor = source.backgroundColor;
                _spectatorCamera.nearClipPlane = source.nearClipPlane;
                _spectatorCamera.farClipPlane = source.farClipPlane;
                _spectatorCamera.fieldOfView = source.fieldOfView;
                _spectatorCamera.depth = source.depth + 20f;
            }
            else
            {
                _spectatorCamera.cullingMask = -1;
                _spectatorCamera.clearFlags = CameraClearFlags.Skybox;
                _spectatorCamera.nearClipPlane = 0.05f;
                _spectatorCamera.farClipPlane = 2000f;
                _spectatorCamera.fieldOfView = 90f;
                _spectatorCamera.depth = 30f;
            }
            _spectatorCamera.enabled = true;
            if (go.GetComponent<AudioListener>() == null)
            {
                go.AddComponent<AudioListener>();
            }
            return _spectatorCamera;
        }

        internal void ToggleTelekinesis()
        {
            TelekinesisEnabled = !TelekinesisEnabled;
            if (!TelekinesisEnabled)
            {
                ReleaseTelekinesis();
                Status = "Telekinesis off";
            }
            else
            {
                _tkHolding = false;
                _tkDepth = 2.2f;
                _tkOffset = Vector3.zero;
                Status = "Telekinesis on - look at an item, press G to grab, G again to release. Mouse moves it, scroll zooms.";
            }
        }

        private void TickFreeCam()
        {
            FlatscreenCore core = FlatscreenCore.Instance;
            if (core == null)
            {
                return;
            }
            Camera cam = EnsureSpectatorCamera();
            if (cam == null)
            {
                return;
            }
            if (!_freeCamInitialized)
            {
                Transform playerAnchor = GetPlayerCameraAnchor(); // see below
                if (playerAnchor != null)
                {
                    _freeCamPos = playerAnchor.position;
                    Vector3 pe = playerAnchor.eulerAngles;
                    _freeCamLook = new Vector2(NormalizeAngle(pe.y), NormalizeAngle(pe.x));
                }
                else
                {
                    // fallback: only use cam's own transform if we truly can't find the player
                    _freeCamPos = cam.transform.position;
                    Vector3 e = cam.transform.eulerAngles;
                    _freeCamLook = new Vector2(NormalizeAngle(e.y), NormalizeAngle(e.x));
                }

                cam.transform.position = _freeCamPos;
                cam.transform.rotation = Quaternion.Euler(_freeCamLook.y, _freeCamLook.x, 0f);
                _freeCamInitialized = true;
            }

            Vector2 delta = core.ReadInputMouseDelta();
            float sensitivity = 0.12f * core.LookSensitivity;
            _freeCamLook.x += delta.x * sensitivity;
            _freeCamLook.y = Mathf.Clamp(_freeCamLook.y - delta.y * sensitivity, -85f, 85f);

            Vector3 move = core.ReadInputMoveVector();
            if (core.IsFreeFlyUpPressed)
            {
                move += Vector3.up;
            }
            if (core.IsFreeFlyDownPressed)
            {
                move -= Vector3.up;
            }
            float speed = core.IsRunPressed ? 11f : 4.5f;
            if (move.sqrMagnitude > 0.0001f)
            {
                _freeCamPos += Quaternion.Euler(0f, _freeCamLook.x, 0f) * move * speed * Time.deltaTime;
            }
            cam.transform.position = _freeCamPos;
            cam.transform.rotation = Quaternion.Euler(_freeCamLook.y, _freeCamLook.x, 0f);
            KeepBodyFrozen();
        }
        private Transform GetPlayerCameraAnchor()
        {
            object player = _game.FindLocalPlayer();
            if (player == null)
            {
                return null;
            }

            Transform head = _game.GetPlayerHeadTransform((Player)player);
            return head != null ? head : ((Player)player).transform;
        }
        private void TickSpectate()
        {
            Camera cam = EnsureSpectatorCamera();
            if (cam == null || _spectateTarget == null)
            {
                return;
            }
            Transform head = _game.GetPlayerHeadTransform(_spectateTarget);
            if (head == null)
            {
                head = GetPlayerTransform(_spectateTarget);
            }
            if (head == null)
            {
                return;
            }
            Vector3 headPos = head.position;
            Quaternion headRot = head.rotation;
            Vector3 desired = headPos + headRot * new Vector3(0f, 0.4f, -1.9f);
            cam.transform.position = Vector3.Lerp(cam.transform.position, desired, 1f - Mathf.Exp(-8f * Time.deltaTime));
            Vector3 lookAt = headPos + headRot * Vector3.forward * 1.5f;
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(lookAt - cam.transform.position, Vector3.up), 1f - Mathf.Exp(-10f * Time.deltaTime));
            KeepBodyFrozen();
        }

        private void TickTelekinesis()
        {
            if (IsGKeyPressed())
            {
                if (!_tkHolding)
                {
                    Vector3? itemPos = FlatscreenCore.Instance != null ? FlatscreenCore.Instance.GetTargetedPickupPosition() : null;
                    if (itemPos.HasValue)
                    {
                        Camera grabCam = GetCam();
                        HandEmulator.TelekinesisRightOverride = true;
                        HandEmulator.TelekinesisRightPosition = itemPos.Value;
                        HandEmulator.TelekinesisRightRotation = grabCam != null ? grabCam.transform.rotation : Quaternion.identity;
                        ZeroRightHandVelocity();
                        bool ok = FlatscreenCore.Instance.TryGrabTargetedPickup();
                        if (ok)
                        {
                            _tkHolding = true;
                            _tkSmooth = itemPos.Value;
                            Status = "Telekinesis: holding (G to release)";
                        }
                        else
                        {
                            HandEmulator.TelekinesisRightOverride = false;
                            Status = "Telekinesis: grab failed";
                        }
                    }
                    else
                    {
                        Status = "Telekinesis: no item in sight";
                    }
                }
                else
                {
                    ReleaseTelekinesis();
                    Status = "Telekinesis: released";
                }
            }
            if (!_tkHolding)
            {
                return;
            }

            FlatscreenCore core = FlatscreenCore.Instance;
            Camera cam = GetCam();
            if (core == null || cam == null)
            {
                return;
            }

            float scroll = core.ReadInputScroll();
            if (Mathf.Abs(scroll) > 0.001f)
            {
                _tkDepth = Mathf.Clamp(_tkDepth - scroll * 0.02f, 0.5f, 8f);
            }
            Vector2 delta = core.ReadInputMouseDelta();
            _tkOffset += new Vector3(delta.x, -delta.y, 0f) * 0.012f;
            _tkOffset.x = Mathf.Clamp(_tkOffset.x, -2.5f, 2.5f);
            _tkOffset.y = Mathf.Clamp(_tkOffset.y, -2.5f, 2.5f);

            Vector3 hover = cam.transform.position
                + cam.transform.forward * _tkDepth
                + cam.transform.right * _tkOffset.x
                + cam.transform.up * _tkOffset.y;

            _tkSmooth = Vector3.Lerp(_tkSmooth, hover, 1f - Mathf.Exp(-10f * Time.deltaTime));
            HandEmulator.TelekinesisRightOverride = true;
            HandEmulator.TelekinesisRightPosition = _tkSmooth;
            HandEmulator.TelekinesisRightRotation = cam.transform.rotation;
        }

        private void ReleaseTelekinesis()
        {
            if (_tkHolding && FlatscreenCore.Instance != null)
            {
                FlatscreenCore.Instance.ReleaseAllGrabs();
            }
            _tkHolding = false;
            HandEmulator.TelekinesisRightOverride = false;
        }

        private void ZeroRightHandVelocity()
        {
            object player = _game.FindLocalPlayer();
            object rightInput = _game.GetRightInput(player);
            object raw = _game.GetRawInput(rightInput);
            if (raw == null)
            {
                return;
            }
            _game.SetRawInputVector3(raw, "Velocity", Vector3.zero);
            _game.SetRawInputVector3(raw, "AngularVelocity", Vector3.zero);
        }

        private void AddPlayersFromRoster(object value)
        {
            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    AddPlayer(entry.Value);
                    AddPlayer(entry.Key);
                }
                return;
            }
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                foreach (object item in enumerable)
                {
                    AddPlayer(item);
                }
                return;
            }
            AddPlayer(value);
        }

        private void AddPlayer(object player)
        {
            if (player == null)
            {
                return;
            }
            Vector3? position = GetPlayerWorldPosition(player);
            if (position == null)
            {
                return;
            }
            int hash = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(player);
            for (int i = 0; i < _players.Count; i++)
            {
                if (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_players[i]) == hash)
                {
                    return;
                }
            }
            _players.Add(player);
            _playerNames.Add(GetPlayerDisplayName(player, position.Value));
        }

        private static Vector3? GetPlayerWorldPosition(object player)
        {
            object pos = ReadMember(player, "Position");
            if (pos is Vector3)
            {
                return (Vector3)pos;
            }
            Transform transform = ReadMember(player, "PlayerTransform") as Transform;
            if (transform == null)
            {
                transform = ReadMember(player, "transform") as Transform;
            }
            return transform != null ? (Vector3?)transform.position : null;
        }

        private string GetPlayerDisplayName(object player, Vector3 position)
        {
            foreach (string name in PlayerNameMembers)
            {
                object value = ReadMember(player, name);
                string text = value as string;
                if (IsUsableName(text))
                {
                    return text.Trim();
                }
            }
            string[] nestedNames = new string[] { "UserInfo", "User", "Account", "Profile", "Owner" };
            foreach (string nestedName in nestedNames)
            {
                object nested = ReadMember(player, nestedName);
                if (nested == null || object.ReferenceEquals(nested, player))
                {
                    continue;
                }
                foreach (string name in PlayerNameMembers)
                {
                    object value = ReadMember(nested, name);
                    string text = value as string;
                    if (IsUsableName(text))
                    {
                        return text.Trim();
                    }
                }
            }
            Transform transform = ReadMember(player, "transform") as Transform;
            if (transform != null && IsUsableName(transform.name))
            {
                return transform.name;
            }
            return "Player " + Mathf.FloorToInt(position.x) + "," + Mathf.FloorToInt(position.z);
        }

        private static bool IsUsableName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            string text = value.Trim();
            if (text.Length < 2 || text.Length > 40)
            {
                return false;
            }
            string lower = text.ToLowerInvariant();
            return !lower.Contains("(clone)") && !lower.Contains("controller") && !lower.Contains("camera") && !lower.Contains("input");
        }

        private static Transform GetPlayerTransform(object player)
        {
            object value = ReadMember(player, "PlayerTransform");
            if (value is Transform transform)
            {
                return transform;
            }
            value = ReadMember(player, "transform");
            return value as Transform;
        }

        private static Camera GetCam()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                return cam;
            }
            try
            {
                if (PlayerController.Current != null && PlayerController.Current.Camera != null)
                {
                    return PlayerController.Current.Camera;
                }
            }
            catch { }
            return cam;
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }

        private bool IsGKeyPressed()
        {
            EnsureKeyboard();
            if (_keyboardCurrentProperty == null)
            {
                return false;
            }
            object keyboard;
            try
            {
                keyboard = _keyboardCurrentProperty.GetValue(null, null);
            }
            catch
            {
                return false;
            }
            if (keyboard == null)
            {
                return false;
            }
            PropertyInfo keyProp = keyboard.GetType().GetProperty("gKey", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object key = (keyProp == null) ? null : keyProp.GetValue(keyboard, null);
            if (key == null)
            {
                return false;
            }
            PropertyInfo pressed = key.GetType().GetProperty("wasPressedThisFrame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return pressed != null && (bool)pressed.GetValue(key, null);
        }

        private void EnsureKeyboard()
        {
            if (_keyboardType != null)
            {
                return;
            }
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                _keyboardType = assembly.GetType("UnityEngine.InputSystem.Keyboard");
                if (_keyboardType != null)
                {
                    break;
                }
            }
            _keyboardCurrentProperty = (_keyboardType == null) ? null : _keyboardType.GetProperty("current", BindingFlags.Static | BindingFlags.Public);
        }

        private static object ReadMember(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            try
            {
                PropertyInfo property = instance.GetType().GetProperty(name, flags);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    return property.GetValue(instance, null);
                }
            }
            catch { }
            try
            {
                FieldInfo field = instance.GetType().GetField(name, flags);
                if (field != null)
                {
                    return field.GetValue(instance);
                }
            }
            catch { }
            return null;
        }

        private static object ReadStaticMember(Type type, string name)
        {
            if (type == null)
            {
                return null;
            }
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            try
            {
                PropertyInfo property = type.GetProperty(name, flags);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    return property.GetValue(null, null);
                }
            }
            catch { }
            try
            {
                FieldInfo field = type.GetField(name, flags);
                if (field != null)
                {
                    return field.GetValue(null);
                }
            }
            catch { }
            return null;
        }

        private static Type FindType(string typeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch
                {
                    continue;
                }
                foreach (Type type in types)
                {
                    if (type != null && type.Name == typeName)
                    {
                        return type;
                    }
                }
            }
            return null;
        }
    }
    // to the captured preset - that's the "overhauled" look; turn it off to let the cycle run again.
    internal sealed class GraphicsController
    {
        private const float AmbientBoost = 1.35f;
        private const float EquatorBoost = 1.25f;
        private const float GroundBoost = 1.15f;
        private const float SunBoost = 1.2f;
        private const float SunWhiteLift = 0.08f;
        private const float ShadowSoftness = 0.8f;
        private const float SunRescanInterval = 2f;

        internal static GraphicsController Instance { get; private set; }
        internal bool LightingOverhaulEnabled { get; private set; }

        private Light _sun;
        private float _nextSunScan;
        private float _sunBaseIntensity = -1f;
        private Color _sunBaseColor = Color.white;
        private LightShadows _sunBaseShadows = LightShadows.Soft;
        private float _sunBaseShadowStrength = 1f;

        private AmbientMode _ambientMode;
        private Color _ambientLight;
        private Color _ambientSky;
        private Color _ambientEquator;
        private Color _ambientGround;
        private float _ambientIntensity;
        private float _reflectionIntensity;

        internal void Init()
        {
            Instance = this;
        }

        internal void ToggleLightingOverhaul()
        {
            LightingOverhaulEnabled = !LightingOverhaulEnabled;
            if (LightingOverhaulEnabled)
            {
                CaptureBackup();
                ApplyOverhaul();
            }
            else
            {
                RestoreBackup();
            }
        }

        internal void Tick()
        {
            if (!LightingOverhaulEnabled)
            {
                return;
            }
            // A scene/server change hands us a new sun - recapture so the boost matches it.
            if (Time.time >= _nextSunScan)
            {
                _nextSunScan = Time.time + SunRescanInterval;
                Light currentSun = FindSun();
                if (currentSun != _sun)
                {
                    CaptureBackup();
                }
            }
            // Re-apply every frame so the game's day/night updates can't fade the look out.
            ApplyOverhaul();
        }

        private void CaptureBackup()
        {
            _ambientMode = RenderSettings.ambientMode;
            _ambientLight = RenderSettings.ambientLight;
            _ambientSky = RenderSettings.ambientSkyColor;
            _ambientEquator = RenderSettings.ambientEquatorColor;
            _ambientGround = RenderSettings.ambientGroundColor;
            _ambientIntensity = RenderSettings.ambientIntensity;
            _reflectionIntensity = RenderSettings.reflectionIntensity;

            _sun = FindSun();
            if (_sun != null)
            {
                _sunBaseIntensity = _sun.intensity;
                _sunBaseColor = _sun.color;
                _sunBaseShadows = _sun.shadows;
                _sunBaseShadowStrength = _sun.shadowStrength;
            }
            else
            {
                _sunBaseIntensity = -1f;
            }
        }

        private void ApplyOverhaul()
        {
            // Brighter, slightly warmer ambient trilight so interiors and shadows lift.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.85f) * AmbientBoost;
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.62f, 0.68f) * EquatorBoost;
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.38f, 0.34f) * GroundBoost;
            RenderSettings.ambientIntensity = 1.25f;

            if (_sun != null && _sunBaseIntensity > -1f)
            {
                _sun.intensity = _sunBaseIntensity * SunBoost;
                _sun.color = Color.Lerp(_sunBaseColor, Color.white, SunWhiteLift);
                _sun.shadowStrength = Mathf.Clamp01(_sunBaseShadowStrength * ShadowSoftness);
            }
        }

        private void RestoreBackup()
        {
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientLight = _ambientLight;
            RenderSettings.ambientSkyColor = _ambientSky;
            RenderSettings.ambientEquatorColor = _ambientEquator;
            RenderSettings.ambientGroundColor = _ambientGround;
            RenderSettings.ambientIntensity = _ambientIntensity;
            RenderSettings.reflectionIntensity = _reflectionIntensity;

            if (_sun != null && _sunBaseIntensity > -1f)
            {
                _sun.intensity = _sunBaseIntensity;
                _sun.color = _sunBaseColor;
                _sun.shadows = _sunBaseShadows;
                _sun.shadowStrength = _sunBaseShadowStrength;
            }
            _sun = null;
        }

        private static Light FindSun()
        {
            Light sun = RenderSettings.sun;
            if (sun != null && sun.isActiveAndEnabled && sun.type == LightType.Directional)
            {
                return sun;
            }
            Light[] lights = Object.FindObjectsOfType<Light>();
            Light best = null;
            float bestIntensity = 0f;
            foreach (Light light in lights)
            {
                if (light != null && light.isActiveAndEnabled && light.type == LightType.Directional && light.intensity > bestIntensity)
                {
                    bestIntensity = light.intensity;
                    best = light;
                }
            }
            return best;
        }
    }
    internal sealed class WeatherController
    {
        internal static bool RainEnabled { get; private set; }
        internal static WeatherController Instance { get; private set; }

        internal static void SetRainEnabled(bool enabled)
        {
            RainEnabled = enabled;
            if (Instance != null)
            {
                Instance.ApplyRainState();
            }
        }

        private GameObject _rainRoot;
        private ParticleSystem _rainSystem;
        private ParticleSystem _splashSystem;
        private Material _rainMaterial;
        private Vector3 _rainWind = new Vector3(-3f, 0f, 0f);
        private float _nextWindChange;
        private bool _skyDarkened;
        private Material _skyboxBackup;
        private Color _ambientSkyBackup;
        private Color _ambientEquatorBackup;
        private Color _ambientGroundBackup;
        private bool _fogEnabledBackup;
        private Color _fogColorBackup;
        private float _fogDensityBackup;
        private static readonly Color RainSkyTint = new Color(0.78f, 0.84f, 0.94f, 1f);
        private static readonly Color RainDropColor = new Color(0.55f, 0.72f, 0.92f, 0.55f);
        private static readonly Color RainFogColor = new Color(0.55f, 0.66f, 0.78f, 1f);
        internal void Init()
        {
            Instance = this;
        }

        internal void Tick()
        {
            if (!RainEnabled || _rainRoot == null)
            {
                return;
            }

            Camera cam = Camera.main;

            if (cam != null)
            {
                Vector3 pos = cam.transform.position;
                _rainRoot.transform.position = new Vector3(pos.x, pos.y + 16f, pos.z);
            }

            if (Time.time >= _nextWindChange)
            {
                _nextWindChange = Time.time + UnityEngine.Random.Range(18f, 30f);

                _rainWind = new Vector3(
                    UnityEngine.Random.Range(-4.5f, 4.5f),
                    0f,
                    UnityEngine.Random.Range(-3f, 3f)
                );

                var velocity = _rainSystem.velocityOverLifetime;

                velocity.x = new ParticleSystem.MinMaxCurve(
                    _rainWind.x - 0.5f,
                    _rainWind.x + 0.5f
                );

                velocity.y = new ParticleSystem.MinMaxCurve(-14f, -11f);

                velocity.z = new ParticleSystem.MinMaxCurve(
                    _rainWind.z - 0.5f,
                    _rainWind.z + 0.5f
                );
            }
        }

        private void ApplyRainState()
        {
            if (RainEnabled)
            {
                EnsureRainSystem();
                DarkenSky();
                _rainRoot.SetActive(true);
            }
            else
            {
                if (_rainRoot != null)
                {
                    _rainRoot.SetActive(false);
                }
                RestoreSky();
            }
        }
        private void RestoreRainFog()
        {
            RenderSettings.fog = _fogEnabledBackup;
            RenderSettings.fogColor = _fogColorBackup;
            RenderSettings.fogDensity = _fogDensityBackup;

            if (RenderSettings.skybox != null &&
                RenderSettings.skybox.HasProperty("_Fade"))
            {
                RenderSettings.skybox.SetFloat("_Fade", 0f);
            }
        }
        private void DarkenSky()
        {
            if (_skyDarkened)
            {
                return;
            }
            _skyDarkened = true;
            _skyboxBackup = RenderSettings.skybox;
            _ambientSkyBackup = RenderSettings.ambientSkyColor;
            _ambientEquatorBackup = RenderSettings.ambientEquatorColor;
            _ambientGroundBackup = RenderSettings.ambientGroundColor;
            _fogEnabledBackup = RenderSettings.fog;
            _fogColorBackup = RenderSettings.fogColor;
            _fogDensityBackup = RenderSettings.fogDensity;

            if (RenderSettings.skybox != null)
            {
                Material darkSky = new Material(RenderSettings.skybox);
                darkSky.name = "TavernFun Rain Sky";
                TintIfHasProperty(darkSky, "_Tint", RainSkyTint);
                TintIfHasProperty(darkSky, "_TintColor", RainSkyTint);
                TintIfHasProperty(darkSky, "_SkyTint", RainSkyTint);
                TintIfHasProperty(darkSky, "_GroundColor", RainSkyTint * 0.7f);
                RenderSettings.skybox = darkSky;
            }

            RenderSettings.ambientSkyColor = Color.Lerp(_ambientSkyBackup, new Color(0.62f, 0.72f, 0.86f), 0.35f);
            RenderSettings.ambientEquatorColor = Color.Lerp(_ambientEquatorBackup, new Color(0.58f, 0.68f, 0.80f), 0.30f);
            RenderSettings.ambientGroundColor = Color.Lerp(_ambientGroundBackup, new Color(0.48f, 0.56f, 0.66f), 0.20f);

            RenderSettings.fog = true;
            RenderSettings.fogColor = RainFogColor;
            RenderSettings.fogDensity = 0.0045f;

            if (RenderSettings.skybox != null &&
                RenderSettings.skybox.HasProperty("_Fade"))
            {
                RenderSettings.skybox.SetFloat("_Fade", 0.35f);
            }
        }

        private void RestoreSky()
        {
            if (!_skyDarkened)
            {
                return;
            }
            _skyDarkened = false;
            if (RenderSettings.skybox != null && RenderSettings.skybox.name == "TavernFun Rain Sky")
            {
                Object.Destroy(RenderSettings.skybox);
            }
            RenderSettings.skybox = _skyboxBackup;
            RenderSettings.ambientSkyColor = _ambientSkyBackup;
            RenderSettings.ambientEquatorColor = _ambientEquatorBackup;
            RenderSettings.ambientGroundColor = _ambientGroundBackup;
            RenderSettings.fog = _fogEnabledBackup;
            RenderSettings.fogColor = _fogColorBackup;
            RenderSettings.fogDensity = _fogDensityBackup;
            if (RenderSettings.skybox != null &&
    RenderSettings.skybox.HasProperty("_Fade"))
            {
                RenderSettings.skybox.SetFloat("_Fade", 0f);
            }
        }

        private static void TintIfHasProperty(Material material, string property, Color tint)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, material.GetColor(property) * tint);
            }
        }

        private void EnsureRainSystem()
        {
            if (_rainRoot != null)
            {
                return;
            }

            _rainRoot = new GameObject("TavernFun Rain");
            Object.DontDestroyOnLoad(_rainRoot);

            GameObject splashGo = new GameObject("Rain Splash");
            splashGo.transform.SetParent(_rainRoot.transform, false);
            _splashSystem = splashGo.AddComponent<ParticleSystem>();
            ConfigureSplashSystem();

            GameObject rainGo = new GameObject("Rain Particles");
            rainGo.transform.SetParent(_rainRoot.transform, false);
            _rainSystem = rainGo.AddComponent<ParticleSystem>();
            ConfigureRainSystem();
        }

        private void ConfigureRainSystem()
        {
            var main = _rainSystem.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.0f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.022f);
            main.startColor = RainDropColor;
            main.maxParticles = 1400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            var emission = _rainSystem.emission;
            emission.rateOverTime = 500f;

            var shape = _rainSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(45f, 1f, 45f);

            var velocity = _rainSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-3.5f, -2.5f);
            velocity.y = new ParticleSystem.MinMaxCurve(-14f, -11f);
            velocity.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

            var renderer = _rainSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 2.2f;
            renderer.material = GetRainMaterial();

            var collision = _rainSystem.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.dampen = 1f;
            collision.bounce = 0f;
            collision.lifetimeLoss = 1f;
            collision.collidesWith = ~0;
            collision.sendCollisionMessages = false;

            var subEmitters = _rainSystem.subEmitters;
            subEmitters.enabled = true;
            subEmitters.AddSubEmitter(
                _splashSystem,
                ParticleSystemSubEmitterType.Collision,
                ParticleSystemSubEmitterProperties.InheritNothing
            );
        }

        private void ConfigureSplashSystem()
        {
            var main = _splashSystem.main;
            main.loop = false;
            main.startLifetime = 0.25f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = RainDropColor;
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 2.2f;

            var emission = _splashSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, (short)2, (short)4, 1, 0f)
            });

            var shape = _splashSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.02f;

            var renderer = _splashSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = GetRainMaterial();
        }

        private Material GetRainMaterial()
        {
            if (_rainMaterial != null)
            {
                return _rainMaterial;
            }
            Shader shader = Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Legacy Shaders/Particles/Additive")
                ?? Shader.Find("Sprites/Default");
            _rainMaterial = new Material(shader);
            _rainMaterial.name = "TavernFun Rain Material";
            if (_rainMaterial.HasProperty("_TintColor"))
            {
                _rainMaterial.SetColor("_TintColor", RainDropColor);
            }
            return _rainMaterial;
        }

        private LayerMask GetTerrainLayerMask()
        {
            if (MasterTerrain.Instance != null && MasterTerrain.Instance.Terrain != null)
            {
                return 1 << MasterTerrain.Instance.Terrain.gameObject.layer;
            }
            return ~0;
        }
    }
    // Performance tweaks for the Anything tab, ported from the FastTale mod. Four knobs, all
    // saved through MelonPreferences and applied live: MSAA (2x/off), the stock flat Overview
    // Camera (useless in VR, a pure waste of render time), the small grass tuft meshes and the
    // bloom post-processing effect. Grass and bloom need Harmony hooks (see PerformancePatches)
    // so toggles keep working as new chunks load in / new post-processing volumes enable.
    internal sealed class PerformanceController
    {
        internal static PerformanceController Instance { get; private set; }

        internal static bool GrassEnabled = true;
        internal static bool BloomEnabled = true;

        private const int MsaaOn = 2;
        private const int MsaaOff = 0;
        private const string OverviewName = "Overview Camera";

        private MelonPreferences_Category _cfg;
        private MelonPreferences_Entry<bool> _cfgMsaa;
        private MelonPreferences_Entry<bool> _cfgOverview;
        private MelonPreferences_Entry<bool> _cfgGrass;
        private MelonPreferences_Entry<bool> _cfgBloom;

        internal bool MsaaEnabled { get; private set; }
        internal bool OverviewEnabled { get; private set; }

        private Camera _overviewCam;
        private int _searchCooldown;

        private static readonly HashSet<GameObject> GrassObjects = new HashSet<GameObject>();
        private static readonly List<PostProcessVolume> Volumes = new List<PostProcessVolume>();
        private static readonly Dictionary<PostProcessProfile, bool> BloomOriginal = new Dictionary<PostProcessProfile, bool>();

        internal void Init()
        {
            Instance = this;
            _cfg = MelonPreferences.CreateCategory("TavernFunPerformance");
            _cfgMsaa = _cfg.CreateEntry<bool>("Msaa", false, null, "MSAA on (2x) vs off", false, false, null, null);
            _cfgOverview = _cfg.CreateEntry<bool>("OverviewCamera", false, null, "Render the stock flat Overview Camera (wasted in VR)", false, false, null, null);
            _cfgGrass = _cfg.CreateEntry<bool>("Grass", true, null, "Render the small grass tuft meshes", false, false, null, null);
            _cfgBloom = _cfg.CreateEntry<bool>("Bloom", true, null, "Bloom post processing", false, false, null, null);

            MsaaEnabled = _cfgMsaa.Value;
            OverviewEnabled = _cfgOverview.Value;
            GrassEnabled = _cfgGrass.Value;
            BloomEnabled = _cfgBloom.Value;

            QualitySettings.antiAliasing = MsaaEnabled ? MsaaOn : MsaaOff;
        }

        internal void Tick()
        {
            int num = MsaaEnabled ? MsaaOn : MsaaOff;
            if (QualitySettings.antiAliasing != num)
            {
                QualitySettings.antiAliasing = num;
            }
            ApplyOverview();
        }

        private void ApplyOverview()
        {
            if (_overviewCam == null)
            {
                int i = _searchCooldown - 1;
                _searchCooldown = i;
                if (i > 0)
                {
                    return;
                }
                _searchCooldown = 30;
                foreach (Camera camera in Camera.allCameras)
                {
                    if (camera != null && camera.name == OverviewName && !camera.stereoEnabled)
                    {
                        _overviewCam = camera;
                        break;
                    }
                }
                if (_overviewCam == null)
                {
                    return;
                }
            }
            if (_overviewCam.enabled != OverviewEnabled)
            {
                _overviewCam.enabled = OverviewEnabled;
            }
        }

        internal void SetMsaaEnabled(bool enabled)
        {
            MsaaEnabled = enabled;
            _cfgMsaa.Value = enabled;
            MelonPreferences.Save();
        }

        internal void SetOverviewEnabled(bool enabled)
        {
            OverviewEnabled = enabled;
            _cfgOverview.Value = enabled;
            MelonPreferences.Save();
        }

        internal static void SetGrassEnabled(bool enabled)
        {
            GrassEnabled = enabled;
            if (Instance != null)
            {
                Instance._cfgGrass.Value = enabled;
                MelonPreferences.Save();
            }
            ApplyGrass();
        }

        internal static void SetBloomEnabled(bool enabled)
        {
            BloomEnabled = enabled;
            if (Instance != null)
            {
                Instance._cfgBloom.Value = enabled;
                MelonPreferences.Save();
            }
            ApplyBloom();
        }

        // Harmony postfix on ChunkPrefabPointer.PointerInstance.Spawned: when a chunk loads and
        // grass is disabled, find and deactivate every grass tuft inside it right away.
        internal static void OnPointerSpawned(ChunkPrefabPointer.PointerInstance __instance)
        {
            if (GrassEnabled)
            {
                return;
            }
            GameObject spawned = __instance.Spawned;
            if (spawned == null)
            {
                return;
            }
            CollectGrass(spawned.transform);
        }

        private static void CollectGrass(Transform root)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                string name = transform.name;
                if (name.Contains("Grass LO Group") || name.Contains("_Grass_"))
                {
                    GrassObjects.Add(transform.gameObject);
                    transform.gameObject.SetActive(false);
                }
            }
        }

        private static void ApplyGrass()
        {
            if (GrassEnabled)
            {
                GrassObjects.RemoveWhere((GameObject go) => go == null);
                foreach (GameObject gameObject in GrassObjects)
                {
                    gameObject.SetActive(true);
                }
                GrassObjects.Clear();
                return;
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene sceneAt = SceneManager.GetSceneAt(i);
                if (sceneAt.isLoaded)
                {
                    GameObject[] rootGameObjects = sceneAt.GetRootGameObjects();
                    for (int j = 0; j < rootGameObjects.Length; j++)
                    {
                        CollectGrass(rootGameObjects[j].transform);
                    }
                }
            }
        }

        // Harmony postfix on PostProcessVolume.OnEnable: track every volume that comes alive so
        // the bloom kill-switch can be re-applied to it (new ones spawn as you travel).
        internal static void OnVolumeEnable(PostProcessVolume __instance)
        {
            if (!Volumes.Contains(__instance))
            {
                Volumes.Add(__instance);
            }
            if (!BloomEnabled)
            {
                SetBloom(__instance, false);
            }
        }

        private static void SetBloom(PostProcessVolume volume, bool on)
        {
            PostProcessProfile profile = volume.HasInstantiatedProfile() ? volume.profile : volume.sharedProfile;
            if (profile == null || !profile.TryGetSettings<Bloom>(out Bloom bloom))
            {
                return;
            }
            if (!BloomOriginal.ContainsKey(profile))
            {
                BloomOriginal[profile] = bloom.active;
            }
            bloom.active = on && BloomOriginal[profile];
        }

        private static void ApplyBloom()
        {
            for (int i = Volumes.Count - 1; i >= 0; i--)
            {
                PostProcessVolume postProcessVolume = Volumes[i];
                if (postProcessVolume == null)
                {
                    Volumes.RemoveAt(i);
                }
                else
                {
                    SetBloom(postProcessVolume, BloomEnabled);
                }
            }
        }
    }
    // Directly forces the local player out of the downed state by:
    //   1. Setting IsDowned = false on the PlayerCharacter via reflection
    //   2. Restoring health to full via the health stat
    //   3. Calling ConsumeByDownedPlayer on all revive orbs in the scene (triggers
    //      any game-side revive logic that listens to that event)
    internal sealed class ReviveOrbController
    {
        internal string Status { get; private set; } = "Idle";
        internal bool Busy => _busy;
        private bool _busy;

        internal void RunAutoRevive()
        {
            if (_busy) return;
            MelonCoroutines.Start(DoRevive());
        }

        private System.Collections.IEnumerator DoRevive()
        {
            _busy = true;
            Status = "Reviving...";
            yield return null; // one frame

            var pc = PlayerController.Current as PlayerCharacter;
            if ((object)pc == null)
            {
                Status = "No local player";
                _busy = false;
                yield break;
            }

            bool anySuccess = false;

            // Step 1: Call ConsumeByDownedPlayer on every revive orb in the scene.
            // This fires the game's own revive logic (health restore, downed-state exit, etc.).
            foreach (MonoBehaviour mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (!typeName.Contains("ReviveOrb") && !typeName.Contains("RespawnOrb")) continue;
                Type t = mb.GetType();
                while (t != null)
                {
                    MethodInfo m = t.GetMethod("ConsumeByDownedPlayer",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (m != null)
                    {
                        try
                        {
                            ParameterInfo[] parms = m.GetParameters();
                            object[] args = parms.Length == 1 ? new object[] { pc } : new object[0];
                            m.Invoke(mb, args);
                            anySuccess = true;
                        }
                        catch { }
                        break;
                    }
                    t = t.BaseType;
                }
            }

            // Step 2: Directly clear IsDowned and restore health, regardless of
            // whether orbs were found — this works even if no orbs exist in the scene.
            try
            {
                Type t = pc.GetType();
                while (t != null)
                {
                    PropertyInfo prop = t.GetProperty("IsDowned",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (prop != null && prop.CanWrite) { prop.SetValue(pc, false, null); anySuccess = true; break; }
                    FieldInfo field = t.GetField("isDowned",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (field != null) { field.SetValue(pc, false); anySuccess = true; break; }
                    t = t.BaseType;
                }
            }
            catch { }

            // Step 3: Restore health to full (100).
            try
            {
                if (pc.Health?.StatManager != null)
                {
                    if (pc.Health.StatManager.GetStat("health", out Stat healthStat) && healthStat != null)
                        healthStat.Base = 100f;
                }
            }
            catch { }

            Status = anySuccess ? "Revived!" : "Revive attempted (no orbs found, state forced)";
            MelonLogger.Msg("[Revive] " + Status);
            _busy = false;
        }
    }
    internal sealed class SpiderController
    {
        private const float BodyTilt = 90f;      // degrees - lays the torso parallel to the floor
        private const float HandSpread = 0.55f;  // how far each hand sits out to the side
        private const float BaseReach = 0.8f;    // how far ahead the hands sit on the ground
        private const float StepRange = 0.6f;    // how far each hand swings forward when walking
        private const float GroundOffset = 0.2f; // how high above the terrain the hands hover
        private const float StepSpeed = 1.5f;    // how fast the hands cycle relative to distance

        private readonly GameReflection _game = new GameReflection();
        private Vector3 _lastRootPosition;
        private float _stepPhase;
        private Quaternion _lastCameraRotation = Quaternion.identity;

        internal bool Enabled { get; private set; }

        internal void Init()
        {
        }

        internal void Toggle()
        {
            Enabled = !Enabled;
            if (!Enabled)
            {
                if (FlatscreenCore.Instance != null)
                {
                    FlatscreenCore.Instance.ResetHeightOffset();
                }
                ReleaseHands();
                _stepPhase = 0f;
            }
        }

        internal void Tick()
        {
            if (Enabled)
            {
                Apply();
            }
        }

        private void Apply()
        {
            object player = _game.FindLocalPlayer();
            if (player == null)
            {
                return;
            }

            Transform root = _game.GetPlayerRootTransform(player);
            Transform camera = GetViewCamera(player);
            if (camera != null)
            {
                _lastCameraRotation = camera.rotation;
            }

            // 1. Lowest height - pins the view to floor level.
            if (FlatscreenCore.Instance != null)
            {
                FlatscreenCore.Instance.SetHeightOffset(0f);
            }

            // 2. Body flat against the floor, keeping your facing direction.
            if (root != null)
            {
                float yaw = camera != null ? camera.eulerAngles.y : root.eulerAngles.y;
                root.rotation = Quaternion.Euler(BodyTilt, yaw, 0f);
            }

            // Restore the camera world rotation so the tilt doesn't tip your head over.
            if (camera != null && root != null)
            {
                camera.rotation = _lastCameraRotation;
            }

            // 3. Step phase advances with distance travelled - drives the crawling hand motion.
            if (root != null)
            {
                float moved = (root.position - _lastRootPosition).magnitude;
                _lastRootPosition = root.position;
                if (moved > 0.001f)
                {
                    _stepPhase += moved * StepSpeed;
                }
            }
            else
            {
                _lastRootPosition = Vector3.zero;
            }

            if (camera == null)
            {
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            // Ground contact points out to each side, swung forward alternately when moving.
            Vector3 ground = camera.position + Vector3.down * GroundOffset;
            float leftSwing = Mathf.Sin(_stepPhase) * StepRange;
            float rightSwing = Mathf.Sin(_stepPhase + Mathf.PI) * StepRange;

            Vector3 leftPos = ground + right * HandSpread + forward * (BaseReach + leftSwing);
            Vector3 rightPos = ground - right * HandSpread + forward * (BaseReach + rightSwing);

            SetHand(_game.GetLeftInput(player), leftPos, camera.rotation);
            SetHand(_game.GetRightInput(player), rightPos, camera.rotation);
        }

        private Transform GetViewCamera(object player)
        {
            try
            {
                if (PlayerController.Current != null && PlayerController.Current.Camera != null)
                {
                    return PlayerController.Current.Camera.transform;
                }
            }
            catch { }
            Camera cam = _game.GetPlayerCamera(player);
            return cam != null ? cam.transform : (Camera.main != null ? Camera.main.transform : null);
        }

        // Pins a hand target to the terrain and holds the grab input so it reads as planted.
        private void SetHand(object playerInput, Vector3 position, Quaternion rotation)
        {
            if (playerInput == null)
            {
                return;
            }
            Transform target = _game.GetInputTargetTransform(playerInput);
            if (target != null)
            {
                target.position = position;
                target.rotation = rotation;
            }
            object raw = _game.GetRawInput(playerInput);
            if (raw != null)
            {
                _game.SetRawInputFloat(raw, "GrabAxis", 1f);
                _game.SetRawInputFloat(raw, "GrabStrength", 1f);
                _game.SetButton(playerInput, raw, "Grab", true);
                _game.SetButton(playerInput, raw, "HardGrab", true);
            }
        }

        private void ReleaseHands()
        {
            object player = _game.FindLocalPlayer();
            if (player == null)
            {
                return;
            }
            ReleaseHand(_game.GetLeftInput(player));
            ReleaseHand(_game.GetRightInput(player));
        }

        private void ReleaseHand(object playerInput)
        {
            if (playerInput == null)
            {
                return;
            }
            object raw = _game.GetRawInput(playerInput);
            if (raw != null)
            {
                _game.SetRawInputFloat(raw, "GrabAxis", 0f);
                _game.SetRawInputFloat(raw, "GrabStrength", 0f);
                _game.SetButton(playerInput, raw, "Grab", false);
                _game.SetButton(playerInput, raw, "HardGrab", false);
            }
        }
    }
    // Superhero-style flying mode: flattens the body into a horizontal flying pose, locks its
    // rotation so ATT can't fight it and spin the player, and layers a smooth accelerate/decelerate
    // feel on top of the game's normal fly controls (camera zoom-out while speeding up, a small
    // shake on hitting peak speed, and body banking on turns). Follows the same reflection-based
    // access pattern as SpiderController - GameReflection for the player rig, FlatscreenCore.Instance
    // for the existing fly/third-person state.
    // Superhero-style flying mode: flattens the body into a horizontal flying pose and drives
    // 100% of its own position/rotation while active. FlatscreenCore.SetSpeed isn't reliable for
    // this (doesn't actually change fly velocity), and letting the game's own movement/IK touch
    // the root at the same time as us is what caused the body to "tweak"/spin (ATT and the normal
    // controller fighting our rotation writes). So instead: freeze the player's built-in movement
    // on activation, then every frame compute the one authoritative position+rotation ourselves
    // and re-assert it at the start of Tick, after we move, and again in LateTick - nothing else
    // gets a chance to sneak a change in between our writes.
    internal sealed class SuperFlyController
    {
        // --- Tuning knobs ---
        private const float DefaultMaxSpeed = 10f;    // configurable peak speed, in meters/sec
        private const float MinMaxSpeed = 2f;
        private const float MaxMaxSpeed = 40f;
        private const float Acceleration = 4f;        // m/s^2 while thrusting forward
        private const float Deceleration = 6f;        // m/s^2 while coasting to a stop
        private const float VerticalSpeed = 6f;        // m/s for the up/down fly keys
        private const float FlattenRate = 140f;        // deg/sec the torso pitches toward flat/upright
        private const float MaxBankAngle = 45f;        // deg of sideways body tilt at full turn rate
        private const float BankTurnRateForMaxAngle = 220f; // deg/sec yaw rate that maxes out banking
        private const float BankSmoothing = 6f;         // higher = snappier bank response
        private const float BodyFlatPitch = 90f;        // matches SpiderController's "lying flat" convention
        private const float MaxZoomOutFov = 12f;        // extra FOV added at full speed
        private const float ZoomSmoothing = 1.5f;       // slow, dramatic zoom rather than snappy
        private const float PeakShakeDuration = 0.35f;
        private const float PeakShakeMagnitude = 1.6f;  // degrees of camera shake
        private const float PeakShakeFrequency = 28f;
        private const float HandReachAboveHead = 0.55f;

        private readonly GameReflection _game = new GameReflection();

        internal bool Enabled { get; private set; }
        internal float MaxSpeed { get; private set; } = DefaultMaxSpeed;

        // Runtime flight state - all continuous/interpolated, never snapped, so accelerate,
        // decelerate, re-accelerate, and the return to upright all blend smoothly into each other.
        private float _currentSpeed;
        private float _bodyFlatness;   // 0 = standing upright, 1 = fully flat/horizontal
        private float _bodyBank;       // current sideways tilt in degrees
        private float _previousYaw;
        private bool _havePreviousYaw;

        // The one authoritative pose. Set at the end of every Tick(), and re-stamped onto the
        // root at the top of Tick() and inside LateTick() so nothing else can move/rotate the
        // body except this controller while Super Fly is on.
        private bool _haveFrozenPose;
        private Vector3 _frozenPosition;
        private Quaternion _frozenRotation = Quaternion.identity;
        private bool _debugMovementApplied;
        // FinalIK solvers (VRIK/FullBodyBipedIK/etc. all derive from RootMotion.FinalIK.IK) drive
        // the torso toward the head/hand tracking targets every LateUpdate. Re-stamping the root
        // transform doesn't stop that - it just fights it, which is the "flip"/tweak. Disabling
        // the solver(s) outright while Super Fly is active removes the fight entirely.
        private readonly List<RootMotion.FinalIK.IK> _suspendedSolvers = new List<RootMotion.FinalIK.IK>();

        // What Super Fly changed on activation, so turning it off restores exactly what was there.
        private bool _restoreThirdPerson;
        private bool _restoreFlyMode;
        private bool _appliedThirdPerson;
        private bool _appliedFlyMode;

        // Camera zoom/shake - always computed fresh from the base FOV captured on activation,
        // so nothing can drift or get left behind when Super Fly is switched off.
        private Camera _activeCamera;
        private float _baseFov;
        private float _currentFov;
        private bool _peakShakeArmed = true;
        private float _shakeTimer;

        internal void Init()
        {
        }

        internal void Tick()
        {
            if (!Enabled)
            {
                return;
            }

            object player = _game.FindLocalPlayer();
            Transform root = _game.GetPlayerRootTransform(player);
            if (player == null || root == null)
            {
                return;
            }

            // Re-stamp whatever we last decided was true before doing anything else this frame -
            // this is what stops the "tweak": nobody but us gets to move the body while it's frozen.
            if (_haveFrozenPose)
            {
                root.position = _frozenPosition;
                root.rotation = _frozenRotation;
            }
            else
            {
                _frozenPosition = root.position;
                _frozenRotation = root.rotation;
                _haveFrozenPose = true;
            }

            FlatscreenCore core = FlatscreenCore.Instance;
            float dt = Time.deltaTime;

            // 1. Forward thrust reuses the existing fly-move input direction, but we integrate the
            //    actual displacement ourselves with our own accel/decel curve.
            Vector3 moveDir = core != null ? core.ReadInputMoveVector() : Vector3.zero;
            bool thrusting = moveDir.sqrMagnitude > 0.01f;
            if (thrusting)
            {
                moveDir.Normalize();
            }
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, thrusting ? MaxSpeed : 0f,
                (thrusting ? Acceleration : Deceleration) * dt);
            float speedFraction = MaxSpeed > 0.01f ? Mathf.Clamp01(_currentSpeed / MaxSpeed) : 0f;

            Vector3 verticalMove = Vector3.zero;
            if (core != null && core.IsFreeFlyUpPressed)
            {
                verticalMove += Vector3.up * VerticalSpeed;
            }
            if (core != null && core.IsFreeFlyDownPressed)
            {
                verticalMove -= Vector3.up * VerticalSpeed;
            }

            Vector3 newPosition = _frozenPosition + moveDir * (_currentSpeed * dt) + verticalMove * dt;

            // 2. Torso pitches flat while there's any speed, and eases back upright as it bleeds off.
            float targetFlatness = _currentSpeed > 0.02f ? 1f : 0f;
            _bodyFlatness = Mathf.MoveTowards(_bodyFlatness, targetFlatness, (FlattenRate / 90f) * dt);

            Camera cam = GetViewCamera(player);
            Transform camTransform = cam != null ? cam.transform : null;
            float yaw = camTransform != null ? camTransform.eulerAngles.y : _frozenRotation.eulerAngles.y;

            // 3. Banking: turn rate (yaw change per second) rolls the whole body into the turn.
            if (!_havePreviousYaw)
            {
                _previousYaw = yaw;
                _havePreviousYaw = true;
            }
            float yawRate = Mathf.DeltaAngle(_previousYaw, yaw) / Mathf.Max(dt, 0.0001f);
            _previousYaw = yaw;
            float targetBank = Mathf.Clamp(yawRate / BankTurnRateForMaxAngle, -1f, 1f) * MaxBankAngle * _bodyFlatness;
            _bodyBank = Mathf.Lerp(_bodyBank, targetBank, 1f - Mathf.Exp(-BankSmoothing * dt));

            // Capture the camera's rotation before we touch the root - the camera is a child of
            // the body, so pitching/rolling the root would otherwise tip the view over with it.
            Quaternion preRotationCamRotation = camTransform != null ? camTransform.rotation : Quaternion.identity;

            float pitch = Mathf.LerpAngle(0f, BodyFlatPitch, _bodyFlatness);
            Quaternion newRotation = Quaternion.Euler(pitch, yaw, _bodyBank);

            root.position = newPosition;
            root.rotation = newRotation;
            _frozenPosition = newPosition;
            _frozenRotation = newRotation;

            if (camTransform != null)
            {
                camTransform.rotation = preRotationCamRotation;
            }

            ApplyHandsOverhead(player, root);
            ApplyCameraZoomAndShake(cam, speedFraction, dt);
        }

        // Re-applied after the game's own late-update/IK pass so nothing - ATT included - can
        // move or spin the body between our Tick() and the end of the frame.
        internal void LateTick()
        {
            if (!Enabled || !_haveFrozenPose)
            {
                return;
            }
            object player = _game.FindLocalPlayer();
            Transform root = _game.GetPlayerRootTransform(player);
            if (root != null)
            {
                root.position = _frozenPosition;
                root.rotation = _frozenRotation;
            }
        }

        internal void Toggle()
        {
            Enabled = !Enabled;
            if (Enabled)
            {
                Activate();
            }
            else
            {
                Deactivate();
            }
        }

        internal void AdjustMaxSpeed(float delta)
        {
            MaxSpeed = Mathf.Clamp(MaxSpeed + delta, MinMaxSpeed, MaxMaxSpeed);
        }

        private void Activate()
        {
            _currentSpeed = 0f;
            _bodyFlatness = 0f;
            _bodyBank = 0f;
            _havePreviousYaw = false;
            _haveFrozenPose = false;
            _peakShakeArmed = true;
            _shakeTimer = 0f;

            FlatscreenCore core = FlatscreenCore.Instance;
            if (core != null)
            {
                _restoreFlyMode = core.FlyModeEnabled;
                if (!core.FlyModeEnabled)
                {
                    core.ToggleFlyMode();
                    _appliedFlyMode = true;
                }

                _restoreThirdPerson = core.ThirdPersonEnabled;
                if (!core.ThirdPersonEnabled)
                {
                    core.ToggleThirdPerson();
                    _appliedThirdPerson = true;
                }
            }

            object player = _game.FindLocalPlayer();
            Transform root = _game.GetPlayerRootTransform(player);
            if (player != null)
            {
                // Hands off, game - we're driving position/rotation ourselves from here on.
                _game.TrySetDebugMovement(player, true);
                _debugMovementApplied = true;
            }
            SuspendBodySolvers(root);

            _activeCamera = GetViewCamera(player);
            _baseFov = _activeCamera != null ? _activeCamera.fieldOfView : 60f;
            _currentFov = _baseFov;
        }

        private void Deactivate()
        {
            _haveFrozenPose = false;

            object player = _game.FindLocalPlayer();
            if (_debugMovementApplied && player != null)
            {
                _game.TrySetDebugMovement(player, false);
            }
            _debugMovementApplied = false;
            ResumeBodySolvers();

            FlatscreenCore core = FlatscreenCore.Instance;
            if (core != null)
            {
                if (_appliedThirdPerson && core.ThirdPersonEnabled != _restoreThirdPerson)
                {
                    core.ToggleThirdPerson();
                }
                if (_appliedFlyMode && core.FlyModeEnabled != _restoreFlyMode)
                {
                    core.ToggleFlyMode();
                }
            }
            _appliedThirdPerson = false;
            _appliedFlyMode = false;

            ReleaseHandsOverhead();

            if (_activeCamera != null)
            {
                _activeCamera.fieldOfView = _baseFov;
            }
            _activeCamera = null;

            _currentSpeed = 0f;
            _bodyFlatness = 0f;
            _bodyBank = 0f;
        }

        private void ApplyCameraZoomAndShake(Camera cam, float speedFraction, float dt)
        {
            if (cam == null)
            {
                return;
            }

            float targetFov = _baseFov + MaxZoomOutFov * speedFraction;
            _currentFov = Mathf.Lerp(_currentFov, targetFov, 1f - Mathf.Exp(-ZoomSmoothing * dt));
            cam.fieldOfView = _currentFov;

            // Fire a short shake the moment peak speed is reached, then re-arm once the player
            // has dropped off peak (so slowing down and speeding back up shakes again).
            if (speedFraction >= 0.999f)
            {
                if (_peakShakeArmed)
                {
                    _shakeTimer = PeakShakeDuration;
                    _peakShakeArmed = false;
                }
            }
            else if (speedFraction < 0.95f)
            {
                _peakShakeArmed = true;
            }

            if (_shakeTimer > 0f)
            {
                _shakeTimer -= dt;
                float falloff = Mathf.Clamp01(_shakeTimer / PeakShakeDuration);
                float shakeX = (Mathf.PerlinNoise(Time.time * PeakShakeFrequency, 0.37f) - 0.5f) * 2f;
                float shakeY = (Mathf.PerlinNoise(0.91f, Time.time * PeakShakeFrequency) - 0.5f) * 2f;
                Vector3 shakeEuler = new Vector3(shakeY, shakeX, 0f) * (PeakShakeMagnitude * falloff);
                cam.transform.rotation = cam.transform.rotation * Quaternion.Euler(shakeEuler);
            }
        }

        // Extends both hands straight up above the head for the classic superhero flying pose.
        // Uses the same hand-target pinning technique SpiderController uses for its crawling
        // hands - positions each hand's grab target and holds the grab axis so the IK follows it.
        private void ApplyHandsOverhead(object player, Transform root)
        {
            Vector3 overhead = root.position + root.up * HandReachAboveHead;
            Quaternion handRotation = Quaternion.LookRotation(root.forward, root.up);

            PinHand(_game.GetLeftInput(player), overhead - root.right * 0.08f, handRotation);
            PinHand(_game.GetRightInput(player), overhead + root.right * 0.08f, handRotation);
        }

        private void ReleaseHandsOverhead()
        {
            object player = _game.FindLocalPlayer();
            if (player == null)
            {
                return;
            }
            ReleaseHand(_game.GetLeftInput(player));
            ReleaseHand(_game.GetRightInput(player));
        }

        private void PinHand(object playerInput, Vector3 position, Quaternion rotation)
        {
            if (playerInput == null)
            {
                return;
            }
            Transform target = _game.GetInputTargetTransform(playerInput);
            if (target != null)
            {
                target.position = position;
                target.rotation = rotation;
            }
            object raw = _game.GetRawInput(playerInput);
            if (raw != null)
            {
                _game.SetRawInputFloat(raw, "GrabAxis", 1f);
                _game.SetRawInputFloat(raw, "GrabStrength", 1f);
                _game.SetButton(playerInput, raw, "Grab", true);
            }
        }

        private void ReleaseHand(object playerInput)
        {
            if (playerInput == null)
            {
                return;
            }
            object raw = _game.GetRawInput(playerInput);
            if (raw != null)
            {
                _game.SetRawInputFloat(raw, "GrabAxis", 0f);
                _game.SetRawInputFloat(raw, "GrabStrength", 0f);
                _game.SetButton(playerInput, raw, "Grab", false);
                _game.SetButton(playerInput, raw, "HardGrab", false);
            }
        }

        private void SuspendBodySolvers(Transform root)
        {
            _suspendedSolvers.Clear();
            if (root == null)
            {
                return;
            }
            RootMotion.FinalIK.IK[] solvers = root.GetComponentsInChildren<RootMotion.FinalIK.IK>(true);
            for (int i = 0; i < solvers.Length; i++)
            {
                RootMotion.FinalIK.IK solver = solvers[i];
                if (solver != null && solver.enabled)
                {
                    solver.enabled = false;
                    _suspendedSolvers.Add(solver);
                }
            }
        }

        private void ResumeBodySolvers()
        {
            for (int i = 0; i < _suspendedSolvers.Count; i++)
            {
                RootMotion.FinalIK.IK solver = _suspendedSolvers[i];
                if (solver != null)
                {
                    solver.enabled = true;
                }
            }
            _suspendedSolvers.Clear();
        }

        private Camera GetViewCamera(object player)
        {
            try
            {
                if (PlayerController.Current != null && PlayerController.Current.Camera != null)
                {
                    return PlayerController.Current.Camera;
                }
            }
            catch { }
            Camera cam = _game.GetPlayerCamera(player);
            return cam != null ? cam : Camera.main;
        }
    }
    internal sealed class ParticlesController
    {
        private sealed class Option
        {
            public string Name;
            public object Vfx;
            public object ParticlesToTransform;
        }

        private readonly List<Option> _options = new List<Option>();

        internal int SelectedIndex { get; private set; } = -1;
        internal string Status { get; private set; } = "Open the menu to list VFX";
        internal int Count => _options.Count;
        internal bool HasSelection => SelectedIndex >= 0 && SelectedIndex < _options.Count;
        internal string SelectedName => HasSelection ? _options[SelectedIndex].Name : "No VFX selected";
        internal string GetName(int index) => (index >= 0 && index < _options.Count) ? _options[index].Name : string.Empty;

        private Type _vfxDataType;
        private PropertyInfo _particlesToTransformProperty;
        private PropertyInfo _shouldPlayProperty;
        private MethodInfo _getFromVector3Method;
        private PropertyInfo _pooledTransformProperty;

        internal void Init()
        {
            EnsureTypes();
        }

        // Called whenever the menu opens so the first open populates the list without paying the
        // Resources.FindObjectsOfTypeAll cost at game start.
        internal void EnsureReady()
        {
            EnsureTypes();
            if (_options.Count == 0 && _vfxDataType != null)
            {
                Refresh();
            }
        }

        internal void Select(int index)
        {
            SelectedIndex = (index >= 0 && index < _options.Count) ? index : -1;
        }

        internal void Refresh()
        {
            _options.Clear();
            SelectedIndex = -1;
            EnsureTypes();
            if (_vfxDataType == null)
            {
                Status = "VfxData type not found";
                return;
            }
            Object[] found;
            try
            {
                found = Resources.FindObjectsOfTypeAll(_vfxDataType);
            }
            catch
            {
                Status = "Failed to scan VFX assets";
                return;
            }
            if (found == null || found.Length == 0)
            {
                Status = "No VFX assets in memory - join a server then Refresh";
                return;
            }
            foreach (Object obj in found)
            {
                if (obj == null)
                {
                    continue;
                }
                object particlesToTransform = ReadParticlesToTransform(obj);
                if (particlesToTransform == null || !IsShouldPlay(obj))
                {
                    continue;
                }
                _options.Add(new Option
                {
                    Name = obj.name,
                    Vfx = obj,
                    ParticlesToTransform = particlesToTransform
                });
            }
            Status = _options.Count + " VFX available";
        }

        internal bool SpawnSingle()
        {
            if (!HasSelection)
            {
                Status = "Pick a VFX first";
                return false;
            }
            bool ok = SpawnGrid(1, 1, 0f);
            Status = ok ? "Spawned " + SelectedName : "Spawn failed (no camera?)";
            return ok;
        }

        internal bool SpawnWall()
        {
            if (!HasSelection)
            {
                Status = "Pick a VFX first";
                return false;
            }
            bool ok = SpawnGrid(5, 3, 0.85f);
            Status = ok ? "Wall spawned: " + SelectedName : "Spawn failed (no camera?)";
            return ok;
        }

        private bool SpawnGrid(int columns, int rows, float spacing)
        {
            Camera cam = GetCamera();
            if (cam == null)
            {
                return false;
            }
            Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = cam.transform.forward;
            }
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 0.0001f)
            {
                right = cam.transform.right;
            }
            right.Normalize();
            Vector3 origin = cam.transform.position + forward * 2.2f;
            Quaternion facingPlayer = Quaternion.LookRotation(-forward, Vector3.up);
            Option option = _options[SelectedIndex];
            int spawned = 0;
            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    Vector3 pos = origin
                        + right * ((x - (columns - 1) * 0.5f) * spacing)
                        + Vector3.up * ((y - (rows - 1) * 0.5f) * spacing);
                    if (TrySpawnAt(option, pos, facingPlayer))
                    {
                        spawned++;
                    }
                }
            }
            return spawned > 0;
        }

        private bool TrySpawnAt(Option option, Vector3 position, Quaternion rotation)
        {
            object pooled = GetPooled(option, position);
            if (pooled == null)
            {
                return false;
            }
            Transform transform = GetPooledTransform(pooled);
            if (transform != null)
            {
                transform.position = position;
                transform.rotation = rotation;
            }
            return true;
        }

        // Resolves VfxData.ParticlesToTransform.Get(Vector3) via reflection - that's the pooled
        // prefab reference the game's ParticlesToPlayer uses; it returns an active PooledObject
        // ready to place. The effect plays, fades, and returns to the pool on its own.
        private object GetPooled(Option option, Vector3 position)
        {
            if (option == null || option.ParticlesToTransform == null)
            {
                return null;
            }
            Type referenceType = option.ParticlesToTransform.GetType();
            if (_getFromVector3Method == null || _getFromVector3Method.DeclaringType != referenceType)
            {
                _getFromVector3Method = null;
                foreach (MethodInfo methodInfo in referenceType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (methodInfo.Name != "Get")
                    {
                        continue;
                    }
                    ParameterInfo[] parameters = methodInfo.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Vector3))
                    {
                        _getFromVector3Method = methodInfo;
                        break;
                    }
                }
            }
            if (_getFromVector3Method == null)
            {
                return null;
            }
            try
            {
                return _getFromVector3Method.Invoke(option.ParticlesToTransform, new object[] { position });
            }
            catch
            {
                return null;
            }
        }

        private Transform GetPooledTransform(object pooled)
        {
            if (pooled == null)
            {
                return null;
            }
            Type pooledType = pooled.GetType();
            if (_pooledTransformProperty == null || _pooledTransformProperty.DeclaringType != pooledType)
            {
                _pooledTransformProperty = pooledType.GetProperty("transform", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            if (_pooledTransformProperty == null)
            {
                return null;
            }
            try
            {
                return _pooledTransformProperty.GetValue(pooled) as Transform;
            }
            catch
            {
                return null;
            }
        }

        private object ReadParticlesToTransform(object vfx)
        {
            if (_particlesToTransformProperty == null)
            {
                return null;
            }
            try
            {
                return _particlesToTransformProperty.GetValue(vfx);
            }
            catch
            {
                return null;
            }
        }

        private bool IsShouldPlay(object vfx)
        {
            if (_shouldPlayProperty == null)
            {
                return true;
            }
            try
            {
                return _shouldPlayProperty.GetValue(vfx) is bool shouldPlay && shouldPlay;
            }
            catch
            {
                return true;
            }
        }

        private static Camera GetCamera()
        {
            try
            {
                if (PlayerController.Current != null && PlayerController.Current.Camera != null)
                {
                    return PlayerController.Current.Camera;
                }
            }
            catch { }
            return Camera.main;
        }

        private void EnsureTypes()
        {
            if (_vfxDataType != null)
            {
                return;
            }
            _vfxDataType = FindType("Features.Visual_Effects.VFXData.VfxData") ?? FindType("VfxData");
            if (_vfxDataType == null)
            {
                return;
            }
            _particlesToTransformProperty = _vfxDataType.GetProperty("ParticlesToTransform", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _shouldPlayProperty = _vfxDataType.GetProperty("ShouldPlayVFX", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static Type FindType(string typeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch
                {
                    continue;
                }
                foreach (Type type in types)
                {
                    if (type != null && (type.Name == typeName || type.FullName == typeName))
                    {
                        return type;
                    }
                }
            }
            return null;
        }
    }
    // Token: 0x02000002 RID: 2
    internal sealed class DesktopInput
    {
        // Token: 0x17000001 RID: 1
        // (get) Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
        public bool IsAvailable
        {
            get
            {
                return this._inputSystem.IsAvailable;
            }
        }

        // Token: 0x17000002 RID: 2
        // (get) Token: 0x06000002 RID: 2 RVA: 0x00002070 File Offset: 0x00000270
        public bool IsControlTogglePressed
        {
            get
            {
                return this._inputSystem.WasPressed("capsLockKey");
            }
        }

        // Token: 0x17000003 RID: 3
        // (get) Token: 0x06000003 RID: 3 RVA: 0x00002094 File Offset: 0x00000294
        public bool IsDebugTogglePressed
        {
            get
            {
                return this._inputSystem.WasPressed("f10Key");
            }
        }
        public bool IsQuickAccessTogglePressed
        {
            get
            {
                return this._inputSystem.WasPressed("kKey");
            }
        }

        public bool IsQuickAccessPrevPressed
        {
            get
            {
                return this._inputSystem.WasPressed("commaKey");
            }
        }

        public bool IsQuickAccessNextPressed
        {
            get
            {
                return this._inputSystem.WasPressed("periodKey");
            }
        }

        // Slash (/) — enter the sub-bubble ring of the currently highlighted QAM bubble.
        public bool IsQuickAccessEnterPressed
        {
            get
            {
                return this._inputSystem.WasPressed("slashKey");
            }
        }

        // Backspace — step back out of a sub-bubble ring to the main ring.
        public bool IsQuickAccessBackPressed
        {
            get
            {
                return this._inputSystem.WasPressed("backspaceKey");
            }
        }
        // Token: 0x17000004 RID: 4
        // (get) Token: 0x06000004 RID: 4 RVA: 0x000020B8 File Offset: 0x000002B8
        public bool IsServerBrowserTogglePressed
        {
            get
            {
                return this._inputSystem.WasPressed("f7Key");
            }
        }
        public bool IsLeftSelectTogglePressed
        {
            get
            {
                return this._inputSystem.WasPressed("qKey");
            }
        }

        public bool IsRightSelectTogglePressed
        {
            get
            {
                return this._inputSystem.WasPressed("eKey");
            }
        }
        public bool IsLeftGrabTogglePressed
        {
            get
            {
                return !ControlMenu.IsPointerOverUI && this._inputSystem.WasMousePressed("leftButton");
            }
        }

        public bool IsRightGrabTogglePressed
        {
            get
            {
                return !ControlMenu.IsPointerOverUI && this._inputSystem.WasMousePressed("rightButton");
            }
        }

        public bool IsLeftGrabPressed
        {
            get
            {
                return !ControlMenu.IsPointerOverUI && this._inputSystem.IsMousePressed("leftButton");
            }
        }

        public bool IsRightGrabPressed
        {
            get
            {
                return !ControlMenu.IsPointerOverUI && this._inputSystem.IsMousePressed("rightButton");
            }
        }

        // Token: 0x1700000B RID: 11
        // (get) Token: 0x0600000B RID: 11 RVA: 0x000021B4 File Offset: 0x000003B4
        public bool IsTeleportPressed
        {
            get
            {
                return this._inputSystem.IsPressed("tKey");
            }
        }

        // Token: 0x1700000C RID: 12
        // (get) Token: 0x0600000C RID: 12 RVA: 0x000021D8 File Offset: 0x000003D8
        public bool IsBagTogglePressed
        {
            get
            {
                return this._inputSystem.WasPressed("iKey");
            }
        }

        // Token: 0x1700000D RID: 13
        // (get) Token: 0x0600000D RID: 13 RVA: 0x000021FC File Offset: 0x000003FC
        public bool IsThirdPersonTogglePressed
        {
            get
            {
                return this._inputSystem.WasPressed("vKey");
            }
        }

        // Token: 0x1700000E RID: 14
        // (get) Token: 0x0600000E RID: 14 RVA: 0x00002220 File Offset: 0x00000420
        public bool IsRunPressed
        {
            get
            {
                return this._inputSystem.IsPressed("leftShiftKey") || this._inputSystem.IsPressed("rightShiftKey");
            }
        }

        // Token: 0x1700000F RID: 15
        // (get) Token: 0x0600000F RID: 15 RVA: 0x00002258 File Offset: 0x00000458
        public bool IsHeightUpPressed
        {
            get
            {
                return this._inputSystem.IsPressed("rKey");
            }
        }

        // Token: 0x17000010 RID: 16
        // (get) Token: 0x06000010 RID: 16 RVA: 0x0000227C File Offset: 0x0000047C
        public bool IsHeightDownPressed
        {
            get
            {
                return this._inputSystem.IsPressed("fKey");
            }
        }

        // Token: 0x17000011 RID: 17
        // (get) Token: 0x06000011 RID: 17 RVA: 0x000022A0 File Offset: 0x000004A0
        public bool IsMenuFlyUpPressed
        {
            get
            {
                return this._inputSystem.IsPressed("spaceKey");
            }
        }

        // Token: 0x17000012 RID: 18
        // (get) Token: 0x06000012 RID: 18 RVA: 0x000022C4 File Offset: 0x000004C4
        public bool IsMenuFlyDownPressed
        {
            get
            {
                return this._inputSystem.IsPressed("leftCtrlKey") || this._inputSystem.IsPressed("rightCtrlKey");
            }
        }
        public bool IsCombatModePressed
        {
            get
            {
                if (this.SuppressCombatInput)
                {
                    return false;
                }
                return this._inputSystem.IsPressed("leftCtrlKey") || this._inputSystem.IsPressed("rightCtrlKey");
            }
        }

        // Set by FlatscreenCore while fly mode is active so Ctrl (fly-down) doesn't also
        // register as the combat-mode hand-swipe modifier.
        public bool SuppressCombatInput { get; set; }

        // Token: 0x17000014 RID: 20
        // (get) Token: 0x06000014 RID: 20 RVA: 0x00002334 File Offset: 0x00000534
        public bool IsResetHandsPressed
        {
            get
            {
                return this._inputSystem.WasPressed("homeKey");
            }
        }

        // Token: 0x17000015 RID: 21
        // (get) Token: 0x06000015 RID: 21 RVA: 0x00002358 File Offset: 0x00000558
        public bool IsMetaMenuPressed
        {
            get
            {
                return this._inputSystem.WasPressed("tabKey") || this._inputSystem.WasPressed("escapeKey");
            }
        }

        // Token: 0x17000016 RID: 22
        // (get) Token: 0x06000016 RID: 22 RVA: 0x00002390 File Offset: 0x00000590
        public bool IsLockLeftPressed
        {
            get
            {
                return this._inputSystem.WasPressed("digit1Key");
            }
        }

        // Token: 0x17000017 RID: 23
        // (get) Token: 0x06000017 RID: 23 RVA: 0x000023B4 File Offset: 0x000005B4
        public bool IsLockRightPressed
        {
            get
            {
                return this._inputSystem.WasPressed("digit2Key");
            }
        }

        // Token: 0x17000018 RID: 24
        // (get) Token: 0x06000018 RID: 24 RVA: 0x000023D8 File Offset: 0x000005D8
        public bool IsLockBothPressed
        {
            get
            {
                return this._inputSystem.WasPressed("digit3Key");
            }
        }

        // Token: 0x17000019 RID: 25
        // (get) Token: 0x06000019 RID: 25 RVA: 0x000023FC File Offset: 0x000005FC
        public bool IsUnlockHandsPressed
        {
            get
            {
                return this._inputSystem.WasPressed("digit4Key");
            }
        }

        // Token: 0x1700001A RID: 26
        // (get) Token: 0x0600001A RID: 26 RVA: 0x00002420 File Offset: 0x00000620
        public bool IsLeftFacePressed
        {
            get
            {
                return this._inputSystem.IsPressed("digit6Key");
            }
        }

        // Token: 0x1700001B RID: 27
        // (get) Token: 0x0600001B RID: 27 RVA: 0x00002444 File Offset: 0x00000644
        public bool IsRightFacePressed
        {
            get
            {
                return this._inputSystem.IsPressed("digit7Key");
            }
        }

        // Token: 0x1700001C RID: 28
        // (get) Token: 0x0600001C RID: 28 RVA: 0x00002468 File Offset: 0x00000668
        public bool IsHandAdjustLeftPressed
        {
            get
            {
                return this._inputSystem.IsPressed("leftArrowKey");
            }
        }

        // Token: 0x1700001D RID: 29
        // (get) Token: 0x0600001D RID: 29 RVA: 0x0000248C File Offset: 0x0000068C
        public bool IsHandAdjustRightPressed
        {
            get
            {
                return this._inputSystem.IsPressed("rightArrowKey");
            }
        }

        // Token: 0x1700001E RID: 30
        // (get) Token: 0x0600001E RID: 30 RVA: 0x000024B0 File Offset: 0x000006B0
        public bool IsHandAdjustUpPressed
        {
            get
            {
                return this._inputSystem.IsPressed("upArrowKey");
            }
        }

        // Token: 0x1700001F RID: 31
        // (get) Token: 0x0600001F RID: 31 RVA: 0x000024D4 File Offset: 0x000006D4
        public bool IsHandAdjustDownPressed
        {
            get
            {
                return this._inputSystem.IsPressed("downArrowKey");
            }
        }

        // Token: 0x17000020 RID: 32
        // (get) Token: 0x06000020 RID: 32 RVA: 0x000024F8 File Offset: 0x000006F8
        public bool HasMoveInput
        {
            get
            {
                return this._inputSystem.IsPressed("wKey") || this._inputSystem.IsPressed("aKey") || this._inputSystem.IsPressed("sKey") || this._inputSystem.IsPressed("dKey");
            }
        }

        // Token: 0x17000021 RID: 33
        // (get) Token: 0x06000021 RID: 33 RVA: 0x00002554 File Offset: 0x00000754
        public Quaternion CameraRotation
        {
            get
            {
                return Quaternion.Euler(this._pitch, this._yaw, 0f);
            }
        }

        // Token: 0x17000022 RID: 34
        // (get) Token: 0x06000022 RID: 34 RVA: 0x0000257C File Offset: 0x0000077C
        public Vector3 ForwardOnPlane
        {
            get
            {
                return Quaternion.Euler(0f, this._yaw, 0f) * Vector3.forward;
            }
        }

        // Token: 0x17000023 RID: 35
        // (get) Token: 0x06000023 RID: 35 RVA: 0x000025B0 File Offset: 0x000007B0
        public Vector3 RightOnPlane
        {
            get
            {
                return Quaternion.Euler(0f, this._yaw, 0f) * Vector3.right;
            }
        }

        // Token: 0x17000024 RID: 36
        // (get) Token: 0x06000024 RID: 36 RVA: 0x000025E4 File Offset: 0x000007E4
        // (set) Token: 0x06000025 RID: 37 RVA: 0x000025FC File Offset: 0x000007FC
        public float LookSensitivityMultiplier
        {
            get
            {
                return this._lookSensitivity;
            }
            set
            {
                this._lookSensitivity = Mathf.Clamp(value, 0.25f, 3f);
            }
        }

        // Token: 0x06000026 RID: 38 RVA: 0x00002618 File Offset: 0x00000818
        public void SetInitialRotation(Quaternion rotation)
        {
            Vector3 eulerAngles = rotation.eulerAngles;
            this._yaw = eulerAngles.y;
            this._pitch = DesktopInput.NormalizePitch(eulerAngles.x);
        }

        // Token: 0x06000027 RID: 39 RVA: 0x00002650 File Offset: 0x00000850
        public void UpdateLook(bool cursorLocked)
        {
            if (this.IsAvailable && cursorLocked)
            {
                Vector2 vector = this._inputSystem.ReadMouseVector2("delta");
                float num = 0.12f * this._lookSensitivity;
                this._yaw += vector.x * num;
                this._pitch = Mathf.Clamp(this._pitch - vector.y * num, -85f, 85f);
            }
        }

        // Token: 0x06000028 RID: 40 RVA: 0x000026D0 File Offset: 0x000008D0
        public Vector2 ReadMouseDelta()
        {
            return this.IsAvailable ? this._inputSystem.ReadMouseVector2("delta") : Vector2.zero;
        }

        // Token: 0x06000029 RID: 41 RVA: 0x00002704 File Offset: 0x00000904
        public Vector3 ReadMoveVector()
        {
            Vector3 vector = Vector3.zero;
            if (this._inputSystem.IsPressed("wKey"))
            {
                vector += this.ForwardOnPlane;
            }
            if (this._inputSystem.IsPressed("sKey"))
            {
                vector -= this.ForwardOnPlane;
            }
            if (this._inputSystem.IsPressed("dKey"))
            {
                vector += this.RightOnPlane;
            }
            if (this._inputSystem.IsPressed("aKey"))
            {
                vector -= this.RightOnPlane;
            }
            vector.y = 0f;
            return (vector.sqrMagnitude > 1f) ? vector.normalized : vector;
        }

        // Token: 0x0600002A RID: 42 RVA: 0x000027DC File Offset: 0x000009DC
        public Vector3 ReadClimbHandVector()
        {
            Vector3 vector = Vector3.zero;
            if (this._inputSystem.IsPressed("dKey"))
            {
                vector += Vector3.right;
            }
            if (this._inputSystem.IsPressed("aKey"))
            {
                vector -= Vector3.right;
            }
            return (vector.sqrMagnitude > 1f) ? vector.normalized : vector;
        }

        // Token: 0x0600002B RID: 43 RVA: 0x00002858 File Offset: 0x00000A58
        public float ReadScroll()
        {
            return this.IsAvailable ? this._inputSystem.ReadMouseVector2("scroll").y : 0f;
        }

        // Token: 0x0600002C RID: 44 RVA: 0x00002890 File Offset: 0x00000A90
        public Vector2 ReadMousePosition()
        {
            return this.IsAvailable ? this._inputSystem.ReadMouseVector2("position") : new Vector2((float)Screen.width * 0.5f, (float)Screen.height * 0.5f);
        }

        // Token: 0x0600002D RID: 45 RVA: 0x000028DC File Offset: 0x00000ADC
        private static float NormalizePitch(float pitch)
        {
            return (pitch > 180f) ? (pitch - 360f) : pitch;
        }

        // Token: 0x04000001 RID: 1
        private const float LookSensitivity = 0.12f;

        // Token: 0x04000002 RID: 2
        private readonly DesktopInput.InputSystemReflection _inputSystem = new DesktopInput.InputSystemReflection();

        // Token: 0x04000003 RID: 3
        private float _yaw;

        // Token: 0x04000004 RID: 4
        private float _pitch;

        // Token: 0x04000005 RID: 5
        private float _lookSensitivity = 1f;

        // Token: 0x02000003 RID: 3
        private sealed class InputSystemReflection
        {
            // Token: 0x17000025 RID: 37
            // (get) Token: 0x0600002F RID: 47 RVA: 0x00002920 File Offset: 0x00000B20
            public bool IsAvailable
            {
                get
                {
                    this.EnsureTypes();
                    return this.Keyboard != null && this.Mouse != null;
                }
            }

            // Token: 0x17000026 RID: 38
            // (get) Token: 0x06000030 RID: 48 RVA: 0x00002954 File Offset: 0x00000B54
            private object Keyboard
            {
                get
                {
                    return (this._keyboardCurrentProperty == null) ? null : this._keyboardCurrentProperty.GetValue(null, null);
                }
            }

            // Token: 0x17000027 RID: 39
            // (get) Token: 0x06000031 RID: 49 RVA: 0x00002988 File Offset: 0x00000B88
            private object Mouse
            {
                get
                {
                    return (this._mouseCurrentProperty == null) ? null : this._mouseCurrentProperty.GetValue(null, null);
                }
            }
            // Token: 0x0600003A RID: 58 RVA: 0x00002CB8 File Offset: 0x00000EB8
            private void TryFindTypes()
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly assembly in assemblies)
                {
                    if (this._keyboardType == null)
                    {
                        this._keyboardType = assembly.GetType("UnityEngine.InputSystem.Keyboard");
                    }
                    if (this._mouseType == null)
                    {
                        this._mouseType = assembly.GetType("UnityEngine.InputSystem.Mouse");
                    }
                }
            }
            // Token: 0x06000032 RID: 50 RVA: 0x000029BC File Offset: 0x00000BBC
            public bool IsPressed(string keyProperty)
            {
                return this.ReadBooleanControl(this.Keyboard, keyProperty, "isPressed");
            }

            // Token: 0x06000033 RID: 51 RVA: 0x000029E0 File Offset: 0x00000BE0
            public bool WasPressed(string keyProperty)
            {
                return this.ReadBooleanControl(this.Keyboard, keyProperty, "wasPressedThisFrame");
            }

            // Token: 0x06000034 RID: 52 RVA: 0x00002A04 File Offset: 0x00000C04
            public bool IsMousePressed(string mouseProperty)
            {
                return this.ReadBooleanControl(this.Mouse, mouseProperty, "isPressed");
            }

            // Token: 0x06000035 RID: 53 RVA: 0x00002A28 File Offset: 0x00000C28
            public bool WasMousePressed(string mouseProperty)
            {
                return this.ReadBooleanControl(this.Mouse, mouseProperty, "wasPressedThisFrame");
            }

            // Token: 0x06000036 RID: 54 RVA: 0x00002A4C File Offset: 0x00000C4C
            public Vector2 ReadMouseVector2(string mouseProperty)
            {
                object mouse = this.Mouse;
                Vector2 result;
                if (mouse == null)
                {
                    result = Vector2.zero;
                }
                else
                {
                    object propertyValue = DesktopInput.InputSystemReflection.GetPropertyValue(mouse, mouseProperty);
                    if (propertyValue == null)
                    {
                        result = Vector2.zero;
                    }
                    else
                    {
                        if (this._readVector2Method == null || this._readVector2Method.DeclaringType != propertyValue.GetType())
                        {
                            this._readVector2Method = propertyValue.GetType().GetMethod("ReadValue", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                        }
                        if (this._readVector2Method == null)
                        {
                            result = Vector2.zero;
                        }
                        else
                        {
                            object obj = this._readVector2Method.Invoke(propertyValue, null);
                            result = ((obj is Vector2) ? ((Vector2)obj) : Vector2.zero);
                        }
                    }
                }
                return result;
            }

            // Token: 0x06000037 RID: 55 RVA: 0x00002B34 File Offset: 0x00000D34
            private bool ReadBooleanControl(object device, string controlProperty, string valueProperty)
            {
                object propertyValue = DesktopInput.InputSystemReflection.GetPropertyValue(device, controlProperty);
                bool result;
                if (propertyValue == null)
                {
                    result = false;
                }
                else
                {
                    PropertyInfo property = propertyValue.GetType().GetProperty(valueProperty, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    result = (property != null && (bool)property.GetValue(propertyValue, null));
                }
                return result;
            }

            // Token: 0x06000038 RID: 56 RVA: 0x00002B88 File Offset: 0x00000D88
            private static object GetPropertyValue(object instance, string propertyName)
            {
                object result;
                if (instance == null)
                {
                    result = null;
                }
                else
                {
                    PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    result = ((property == null) ? null : property.GetValue(instance, null));
                }
                return result;
            }

            // Token: 0x06000039 RID: 57 RVA: 0x00002BD0 File Offset: 0x00000DD0
            private void EnsureTypes()
            {
                if (!(this._keyboardType != null) || !(this._mouseType != null))
                {
                    this.TryFindTypes();
                    if (this._keyboardType == null || this._mouseType == null)
                    {
                        try
                        {
                            Assembly.Load("Unity.InputSystem");
                        }
                        catch
                        {
                        }
                        this.TryFindTypes();
                    }
                    this._keyboardCurrentProperty = ((this._keyboardType == null) ? null : this._keyboardType.GetProperty("current", BindingFlags.Static | BindingFlags.Public));
                    this._mouseCurrentProperty = ((this._mouseType == null) ? null : this._mouseType.GetProperty("current", BindingFlags.Static | BindingFlags.Public));
                }
            }


            // Token: 0x04000006 RID: 6
            private Type _keyboardType;

            // Token: 0x04000007 RID: 7
            private Type _mouseType;

            // Token: 0x04000008 RID: 8
            private PropertyInfo _keyboardCurrentProperty;

            // Token: 0x04000009 RID: 9
            private PropertyInfo _mouseCurrentProperty;

            // Token: 0x0400000A RID: 10
            private MethodInfo _readVector2Method;
        }
    }
    internal sealed class HandRotationController
    {
        internal static HandRotationController Instance { get; private set; }

        internal float LeftPitch;
        internal float LeftYaw;
        internal float LeftRoll;
        internal float RightPitch;
        internal float RightYaw;
        internal float RightRoll;

        internal string Status { get; private set; } = "Adjust sliders, then Save.";

        private readonly GameReflection _game = new GameReflection();
        private MelonPreferences_Category _cfg;
        private MelonPreferences_Entry<float> _cfgLeftPitch;
        private MelonPreferences_Entry<float> _cfgLeftYaw;
        private MelonPreferences_Entry<float> _cfgLeftRoll;
        private MelonPreferences_Entry<float> _cfgRightPitch;
        private MelonPreferences_Entry<float> _cfgRightYaw;
        private MelonPreferences_Entry<float> _cfgRightRoll;

        private Transform _avatarRoot;
        private HandGrip _leftGrip;
        private HandGrip _rightGrip;
        private Quaternion _lastLeftOffset = Quaternion.identity;
        private Quaternion _lastRightOffset = Quaternion.identity;

        internal void Init()
        {
            Instance = this;
            _cfg = MelonPreferences.CreateCategory("TavernFunHandRotation");
            _cfgLeftPitch = _cfg.CreateEntry<float>("LeftPitch", 0f, null, "Left hand pitch offset (deg)", false, false, null, null);
            _cfgLeftYaw = _cfg.CreateEntry<float>("LeftYaw", 0f, null, "Left hand yaw offset (deg)", false, false, null, null);
            _cfgLeftRoll = _cfg.CreateEntry<float>("LeftRoll", 0f, null, "Left hand roll offset (deg)", false, false, null, null);
            _cfgRightPitch = _cfg.CreateEntry<float>("RightPitch", 0f, null, "Right hand pitch offset (deg)", false, false, null, null);
            _cfgRightYaw = _cfg.CreateEntry<float>("RightYaw", 0f, null, "Right hand yaw offset (deg)", false, false, null, null);
            _cfgRightRoll = _cfg.CreateEntry<float>("RightRoll", 0f, null, "Right hand roll offset (deg)", false, false, null, null);

            LeftPitch = _cfgLeftPitch.Value;
            LeftYaw = _cfgLeftYaw.Value;
            LeftRoll = _cfgLeftRoll.Value;
            RightPitch = _cfgRightPitch.Value;
            RightYaw = _cfgRightYaw.Value;
            RightRoll = _cfgRightRoll.Value;
        }

        internal void Tick()
        {
            Quaternion leftOffset = Quaternion.Euler(LeftPitch, LeftYaw, LeftRoll);
            Quaternion rightOffset = Quaternion.Euler(RightPitch, RightYaw, RightRoll);
            bool hasOffsets = Mathf.Abs(LeftPitch) > 0.001f || Mathf.Abs(LeftYaw) > 0.001f || Mathf.Abs(LeftRoll) > 0.001f
                || Mathf.Abs(RightPitch) > 0.001f || Mathf.Abs(RightYaw) > 0.001f || Mathf.Abs(RightRoll) > 0.001f;

            // With neutral settings, leave the game's tracked hand rotations entirely alone.
            // Reapplying a startup quaternion every frame freezes the grip pose as a player joins.
            if (!hasOffsets)
            {
                RemoveAppliedOffsets();
                return;
            }
            if (!EnsureRig()) return;

            ApplyOffset(_leftGrip, ref _lastLeftOffset, leftOffset);
            ApplyOffset(_rightGrip, ref _lastRightOffset, rightOffset);
        }

        private void ApplyOffset(HandGrip grip, ref Quaternion previousOffset, Quaternion nextOffset)
        {
            if (grip == null) return;
            // Strip only our previous offset from the latest game-authored pose before applying
            // the current offset, so tracking/animation can keep changing the base rotation.
            Quaternion nativeRotation = grip.transform.localRotation * Quaternion.Inverse(previousOffset);
            grip.transform.localRotation = nativeRotation * nextOffset;
            previousOffset = nextOffset;
        }

        private void RemoveAppliedOffsets()
        {
            RemoveOffset(_leftGrip, ref _lastLeftOffset);
            RemoveOffset(_rightGrip, ref _lastRightOffset);
        }

        private static void RemoveOffset(HandGrip grip, ref Quaternion previousOffset)
        {
            if (grip != null && Quaternion.Angle(previousOffset, Quaternion.identity) > 0.001f)
            {
                grip.transform.localRotation *= Quaternion.Inverse(previousOffset);
            }
            previousOffset = Quaternion.identity;
        }

        internal void Save()
        {
            _cfgLeftPitch.Value = LeftPitch;
            _cfgLeftYaw.Value = LeftYaw;
            _cfgLeftRoll.Value = LeftRoll;
            _cfgRightPitch.Value = RightPitch;
            _cfgRightYaw.Value = RightYaw;
            _cfgRightRoll.Value = RightRoll;
            MelonPreferences.Save();
            Status = "Saved.";
        }

        internal void ResetOffsets()
        {
            LeftPitch = LeftYaw = LeftRoll = 0f;
            RightPitch = RightYaw = RightRoll = 0f;
            Save();
            Status = "Reset to default.";
        }

        private bool EnsureRig()
        {
            object player;
            try { player = _game.FindLocalPlayer(); } catch { return false; }
            if (player == null) return false;

            Transform root;
            try { root = _game.GetPlayerRootTransform(player); } catch { return false; }
            if (root == null) return false;

            if (root == _avatarRoot && _leftGrip != null && _rightGrip != null)
            {
                return true;
            }

            RemoveAppliedOffsets();
            Hand[] hands = root.GetComponentsInChildren<Hand>(true);
            HandGrip leftGrip = null;
            HandGrip rightGrip = null;
            for (int i = 0; i < hands.Length; i++)
            {
                HandGrip grip = hands[i].GripAnimator;
                if (grip == null) continue;
                if (grip.IsLeftHand) leftGrip = grip;
                else rightGrip = grip;
            }

            if (leftGrip == null || rightGrip == null)
            {
                return false;
            }

            _avatarRoot = root;
            _leftGrip = leftGrip;
            _rightGrip = rightGrip;
            return true;
        }
    }
    // Token: 0x0600003A RID: 58 RVA: 0x00002CB8 File Offset: 0x00000EB8


    // Locks the local avatar and view in place, switches to the existing third-person camera,
    // and maps vertical mouse motion onto the game's BodyControllerSettings bend value.
    internal sealed class HipMoveController
    {
        private const float BendMinimum = -35f;
        private const float BendMaximum = 35f;
        private const float MouseSensitivity = 0.16f;
        private readonly GameReflection _game = new GameReflection();
        private readonly List<object> _settings = new List<object>();
        private readonly Dictionary<object, float> _originalBendValues = new Dictionary<object, float>();
        private FieldInfo _bendField;
        private Transform _root;
        private Transform _cameraTransform;
        private Vector3 _lockedPosition;
        private Quaternion _lockedRootRotation;
        private Quaternion _lockedCameraRotation;
        private float _bendValue;
        private bool _restoreThirdPerson;
        private bool _restoreLookLock;
        private bool _restoreMovementLock;

        internal bool Enabled { get; private set; }

        internal void Toggle(FlatscreenCore flatscreen)
        {
            if (Enabled) Stop(flatscreen);
            else Start(flatscreen);
        }

        private void Start(FlatscreenCore flatscreen)
        {
            if (!FlatscreenCore.Enabled)
            {
                MelonLogger.Warning("Hip Move requires PanKake to be enabled for its third-person camera.");
                return;
            }
            object player = _game.FindLocalPlayer();
            _root = _game.GetPlayerRootTransform(player);
            Camera cam = GetLocalPlayerCamera();
            if (player == null || _root == null)
            {
                MelonLogger.Warning("Hip Move: local player is not available.");
                return;
            }
            _cameraTransform = cam != null ? cam.transform : null;
            _lockedPosition = _root.position;
            _lockedRootRotation = _root.rotation;
            _lockedCameraRotation = _cameraTransform != null ? _cameraTransform.rotation : Quaternion.identity;
            _restoreThirdPerson = flatscreen.ThirdPersonEnabled;
            _restoreLookLock = false;
            _restoreMovementLock = FlatscreenCore.SuppressPlayerMovement;
            _originalBendValues.Clear();
            FindBodyBendSettings();
            _bendValue = _bendField != null && _settings.Count > 0 ? ReadBend(_settings[0]) : 0f;
            flatscreen.SetLookInputLocked(true);
            flatscreen.SetThirdPersonEnabled(true);
            FlatscreenCore.SuppressPlayerMovement = true;
            Enabled = true;
        }

        internal void Tick(FlatscreenCore flatscreen)
        {
            if (!Enabled) return;
            if (_root == null)
            {
                Stop(flatscreen);
                return;
            }
            Vector2 delta = (flatscreen != null && !ControlMenu.IsCursorFree) ? flatscreen.ReadInputMouseDelta() : Vector2.zero;
            _bendValue = Mathf.Clamp(_bendValue - delta.y * MouseSensitivity, BendMinimum, BendMaximum);
            ApplyBend(_bendValue);
            LockPose();
        }

        internal void LateTick()
        {
            if (Enabled) LockPose();
        }

        private void LockPose()
        {
            if (_root != null)
            {
                _root.position = _lockedPosition;
                _root.rotation = _lockedRootRotation;
            }
            if (_cameraTransform != null) _cameraTransform.rotation = _lockedCameraRotation;
        }

        private void Stop(FlatscreenCore flatscreen)
        {
            if (!Enabled) return;
            RestoreBend();
            if (flatscreen != null)
            {
                flatscreen.SetLookInputLocked(_restoreLookLock);
                flatscreen.SetThirdPersonEnabled(_restoreThirdPerson);
            }
            FlatscreenCore.SuppressPlayerMovement = _restoreMovementLock;
            Enabled = false;
            _root = null;
            _cameraTransform = null;
            _settings.Clear();
            _originalBendValues.Clear();
            _bendField = null;
        }

        private void FindBodyBendSettings()
        {
            Type settingsType = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length && settingsType == null; i++)
            {
                try
                {
                    settingsType = assemblies[i].GetType("BodyControllerSettings", false);
                    if (settingsType == null)
                    {
                        Type[] types = assemblies[i].GetTypes();
                        for (int j = 0; j < types.Length; j++)
                            if (types[j] != null && types[j].Name == "BodyControllerSettings") { settingsType = types[j]; break; }
                    }
                }
                catch { }
            }
            if (settingsType == null) return;

            FieldInfo[] fields = settingsType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.FieldType == typeof(float) && field.Name.IndexOf("bend", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _bendField = field;
                    if (field.Name.IndexOf("multiplier", StringComparison.OrdinalIgnoreCase) >= 0) break;
                }
            }
            if (_bendField == null) return;
            try
            {
                Array found = Resources.FindObjectsOfTypeAll(settingsType);
                foreach (object item in found)
                {
                    if (item == null) continue;
                    _settings.Add(item);
                    _originalBendValues[item] = ReadBend(item);
                }
            }
            catch { }
        }

        private float ReadBend(object settings)
        {
            try { return (float)_bendField.GetValue(settings); }
            catch { return 0f; }
        }

        private void ApplyBend(float value)
        {
            if (_bendField == null) return;
            for (int i = 0; i < _settings.Count; i++)
            {
                try { _bendField.SetValue(_settings[i], value); }
                catch { }
            }
        }

        private void RestoreBend()
        {
            if (_bendField == null) return;
            foreach (KeyValuePair<object, float> pair in _originalBendValues)
            {
                try { _bendField.SetValue(pair.Key, pair.Value); }
                catch { }
            }
        }

        private static Camera GetLocalPlayerCamera()
        {
            try
            {
                if (PlayerController.Current != null && PlayerController.Current.Camera != null)
                    return PlayerController.Current.Camera;
            }
            catch { }
            return Camera.main;
        }
    }


    // Reproduces the bundled teleport-effect shapes through PlayerEffectController's
    // remoteTeleportEffect RPC. A bounded number of effect events are emitted each cycle.
    internal sealed class TeleportEffectsController
    {
        private const int MaxEffectsPerBurst = 180;
        private const int MaxArenaPoints = 48;
        private bool _centerSet;
        private Vector3 _center;
        private float _nextCenterScan;
        private float _nextBurst;
        private float _nextArenaTick;
        private float _arenaRadius;
        private float _arenaLastTime;
        private int _nextWarningTime;
        private float _nextApiResolveTime;
        private PlayerEffectController _effectController;
        private FieldInfo _remoteEffectField;
        private Type _teleportInfoType;
        private FieldInfo _teleportPositionField;
        private FieldInfo _teleportTypeField;
        private MethodInfo _sendToChunksMethod;

        internal bool SphereEnabled { get; set; }
        internal bool ConeEnabled { get; set; }
        internal bool StarEnabled { get; set; }
        internal bool RingEnabled { get; set; }
        internal bool CubeEnabled { get; set; }
        internal bool HelixEnabled { get; set; }
        internal bool RingArenaEnabled { get; set; }
        internal bool PillarsEnabled { get; set; }
        internal float Scale { get; set; } = 1f;
        internal bool IsActive { get { return AnyEnabled(); } }
        internal string Status { get { return IsActive ? "Teleport effects running" : "Choose one or more effects"; } }

        internal void StopAll()
        {
            SphereEnabled = ConeEnabled = StarEnabled = RingEnabled = CubeEnabled = HelixEnabled = false;
            RingArenaEnabled = PillarsEnabled = false;
        }

        internal void Tick()
        {
            if (!AnyEnabled())
            {
                _centerSet = false;
                _arenaRadius = 0f;
                return;
            }
            if (!_centerSet)
            {
                if (Time.time < _nextCenterScan) return;
                if (!TryGetCenter(out _center))
                {
                    _nextCenterScan = Time.time + 1f;
                    return;
                }
                _centerSet = true;
                _nextBurst = Time.time;
            }

            if (Time.time >= _nextBurst)
            {
                TickShapes();
                _nextBurst = Time.time + 1f;
            }
            if (RingArenaEnabled && Time.time >= _nextArenaTick)
                TickRingArena();
        }

        private bool AnyEnabled()
        {
            return SphereEnabled || ConeEnabled || StarEnabled || RingEnabled || CubeEnabled || HelixEnabled || RingArenaEnabled;
        }

        private void TickShapes()
        {
            List<List<Vector3>> shapes = new List<List<Vector3>>();
            if (SphereEnabled) shapes.Add(BuildSphere());
            if (ConeEnabled) shapes.Add(BuildCone());
            if (StarEnabled) shapes.Add(BuildStar());
            if (RingEnabled) shapes.Add(BuildRing());
            if (CubeEnabled) shapes.Add(BuildCube());
            if (HelixEnabled) shapes.Add(BuildHelix());
            int perShape = shapes.Count > 0 ? Mathf.Max(1, MaxEffectsPerBurst / shapes.Count) : 0;
            for (int i = 0; i < shapes.Count; i++) SpawnSampled(shapes[i], perShape, Scale);
            if (SphereEnabled && PillarsEnabled) SpawnOtherPlayerPillars();
        }

        private void TickRingArena()
        {
            float scale = Mathf.Clamp(Scale, 0.2f, 3f);
            float maxRadius = 12f * scale;
            if (_arenaRadius <= 0f || _arenaRadius > maxRadius) _arenaRadius = maxRadius;
            float spacing = Mathf.Max(0.2f, 0.6f * scale);
            int count = Mathf.Max(12, Mathf.CeilToInt(2f * Mathf.PI * _arenaRadius / spacing));
            List<Vector3> points = new List<Vector3>();
            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                Vector3 point = new Vector3(Mathf.Cos(angle) * _arenaRadius, 0f, Mathf.Sin(angle) * _arenaRadius);
                for (float y = -0.5f * scale; y <= 0.5f * scale + 0.001f; y += 0.5f * scale)
                    points.Add(point + Vector3.up * y);
            }
            SpawnSampled(points, MaxArenaPoints, 1f);
            float now = Time.time;
            float elapsed = _arenaLastTime > 0f ? now - _arenaLastTime : 0.12f;
            _arenaLastTime = now;
            _arenaRadius = Mathf.Max(Mathf.Max(0.05f, maxRadius * 0.08f), _arenaRadius - 0.5f * scale * Mathf.Max(0.001f, elapsed));
            _nextArenaTick = now + 0.12f;
        }

        private void SpawnOtherPlayerPillars()
        {
            HashSet<IPlayer> allPlayers = Player.AllPlayers;
            if (allPlayers == null) return;
            int playerCount = 0;
            foreach (IPlayer other in allPlayers)
            {
                if (other == null || other.IsLocalPlayer || other.PlayerController == null) continue;
                if ((other.PlayerController.PlayerFeetPosition - _center).sqrMagnitude > 400f) continue;
                Vector3 feet = other.PlayerController.PlayerFeetPosition;
                for (float y = 0f; y <= 4f; y += 0.5f) SpawnEffect(feet + Vector3.up * y);
                if (++playerCount >= 12) break;
            }
        }

        private void SpawnSampled(List<Vector3> offsets, int limit, float scale)
        {
            if (offsets == null || offsets.Count == 0 || limit <= 0) return;
            int stride = Mathf.Max(1, Mathf.CeilToInt(offsets.Count / (float)limit));
            int emitted = 0;
            for (int i = 0; i < offsets.Count && emitted < limit; i += stride, emitted++)
                SpawnEffect(_center + offsets[i] * scale);
        }

        private static List<Vector3> BuildSphere()
        {
            List<Vector3> offsets = new List<Vector3>(5000);
            const float outerRadius = 20f;
            const float innerRadius = 19.5f;
            float innerSquared = innerRadius * innerRadius;
            float outerSquared = outerRadius * outerRadius;
            for (float x = -outerRadius; x <= outerRadius; x += 1f)
            for (float y = -outerRadius; y <= outerRadius; y += 1f)
            for (float z = -outerRadius; z <= outerRadius; z += 1f)
            {
                Vector3 point = new Vector3(x, y, z);
                float sqr = point.sqrMagnitude;
                if (sqr >= innerSquared && sqr <= outerSquared) offsets.Add(point);
            }
            return offsets;
        }

        private static List<Vector3> BuildCone()
        {
            List<Vector3> offsets = new List<Vector3>();
            for (int y = 0; y <= 18; y++)
            {
                float radius = 10f * y / 18f;
                if (radius <= 0.001f) { offsets.Add(Vector3.zero); continue; }
                int segments = Mathf.Max(6, Mathf.CeilToInt(2f * Mathf.PI * radius));
                for (int i = 0; i < segments; i++)
                {
                    float angle = i / (float)segments * Mathf.PI * 2f;
                    offsets.Add(new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius));
                }
            }
            return offsets;
        }

        private static List<Vector3> BuildStar()
        {
            List<Vector3> offsets = new List<Vector3>();
            Vector3[] vertices = new Vector3[10];
            for (int i = 0; i < vertices.Length; i++)
            {
                float angle = i / 10f * Mathf.PI * 2f;
                float radius = i % 2 == 0 ? 12f : 6f;
                vertices[i] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }
            for (int edge = 0; edge < vertices.Length; edge++)
            {
                Vector3 a = vertices[edge];
                Vector3 b = vertices[(edge + 1) % vertices.Length];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b)));
                for (int i = 0; i <= steps; i++)
                {
                    Vector3 point = Vector3.Lerp(a, b, i / (float)steps);
                    for (float y = -0.5f; y <= 0.5f; y += 0.5f) offsets.Add(point + Vector3.up * y);
                }
            }
            return offsets;
        }

        private static List<Vector3> BuildRing()
        {
            List<Vector3> offsets = new List<Vector3>();
            const float radius = 12f;
            int count = Mathf.Max(12, Mathf.CeilToInt(2f * Mathf.PI * radius / 0.6f));
            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                Vector3 point = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                for (float y = -0.5f; y <= 0.5f; y += 0.5f) offsets.Add(point + Vector3.up * y);
            }
            return offsets;
        }

        private static List<Vector3> BuildCube()
        {
            List<Vector3> offsets = new List<Vector3>();
            Vector3[] corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = new Vector3((i & 1) == 0 ? -8f : 8f, (i & 2) == 0 ? -8f : 8f, (i & 4) == 0 ? -8f : 8f);
            for (int i = 0; i < 8; i++)
            for (int axis = 0; axis < 3; axis++)
            {
                int other = i ^ (1 << axis);
                if (i >= other) continue;
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(corners[i], corners[other])));
                for (int j = 0; j <= steps; j++) offsets.Add(Vector3.Lerp(corners[i], corners[other], j / (float)steps));
            }
            return offsets;
        }

        private static List<Vector3> BuildHelix()
        {
            List<Vector3> offsets = new List<Vector3>();
            for (int i = 0; i <= 32; i++)
            {
                float t = i / 32f;
                float angle = t * Mathf.PI * 2f * 3f;
                offsets.Add(new Vector3(Mathf.Cos(angle) * 6f, t * 16f, Mathf.Sin(angle) * 6f));
            }
            return offsets;
        }

        private bool TryGetCenter(out Vector3 center)
        {
            if (TryGetFriendlyOrbCenter(out center)) return true;
            PlayerController player = PlayerController.Current;
            if (player != null)
            {
                center = player.PlayerFeetPosition;
                return true;
            }
            center = Vector3.zero;
            return false;
        }

        private bool TryGetFriendlyOrbCenter(out Vector3 center)
        {
            center = Vector3.zero;
            try
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    Type orbType = null;
                    try
                    {
                        Type[] types = assemblies[i].GetTypes();
                        for (int j = 0; j < types.Length; j++)
                            if (types[j] != null && types[j].Name == "FriendlyOrbBehavior2") { orbType = types[j]; break; }
                    }
                    catch { }
                    if (orbType == null) continue;
                    FieldInfo bodyField = AccessTools.Field(orbType, "tp_rb");
                    PropertyInfo bodyProperty = orbType.GetProperty("tp_rb", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    UnityEngine.Object[] orbs = Resources.FindObjectsOfTypeAll(orbType);
                    for (int j = 0; j < orbs.Length; j++)
                    {
                        Component orb = orbs[j] as Component;
                        if (orb == null || !orb.gameObject.activeInHierarchy) continue;
                        object body = bodyField != null ? bodyField.GetValue(orb) : (bodyProperty != null ? bodyProperty.GetValue(orb, null) : null);
                        Rigidbody rb = body as Rigidbody;
                        if (rb != null) { center = rb.position; return true; }
                        Component bodyComponent = body as Component;
                        if (bodyComponent != null) { center = bodyComponent.transform.position; return true; }
                    }
                }
            }
            catch { }
            return false;
        }

        private void SpawnEffect(Vector3 position)
        {
            try
            {
                PlayerController player = PlayerController.Current;
                if (player == null) return;
                PlayerEffectController controller = player.transform.GetComponent<PlayerEffectController>();
                if (controller == null) return;
                if (!object.ReferenceEquals(_effectController, controller)
                    || (_sendToChunksMethod == null && Time.time >= _nextApiResolveTime))
                {
                    ResolveEffectApi(controller);
                    _nextApiResolveTime = Time.time + 1f;
                }
                if (_remoteEffectField == null || _teleportInfoType == null || _sendToChunksMethod == null) return;
                object remote = _remoteEffectField.GetValue(controller);
                if (remote == null) return;
                object info = Activator.CreateInstance(_teleportInfoType);
                if (_teleportPositionField != null) _teleportPositionField.SetValue(info, position);
                if (_teleportTypeField != null && _teleportTypeField.FieldType.IsEnum)
                {
                    try { _teleportTypeField.SetValue(info, Enum.Parse(_teleportTypeField.FieldType, "Unknown")); }
                    catch { }
                }
                ParameterInfo[] parameters = _sendToChunksMethod.GetParameters();
                object recipients = parameters[1].ParameterType.IsArray
                    ? Array.CreateInstance(parameters[1].ParameterType.GetElementType(), 0)
                    : null;
                _sendToChunksMethod.Invoke(remote, new object[] { info, recipients });
            }
            catch (Exception ex)
            {
                if (Time.frameCount > _nextWarningTime)
                {
                    _nextWarningTime = Time.frameCount + 300;
                    MelonLogger.Warning("TP Effects: " + ex.GetBaseException().Message);
                }
            }
        }

        private void ResolveEffectApi(PlayerEffectController controller)
        {
            _effectController = controller;
            Type componentType = controller.GetType();
            _remoteEffectField = AccessTools.Field(componentType, "remoteTeleportEffect");
            _teleportInfoType = null;
            Type[] nested = componentType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < nested.Length; i++)
                if (nested[i].Name == "PlayerEffectTeleportInformation") { _teleportInfoType = nested[i]; break; }
            _teleportPositionField = _teleportInfoType != null ? AccessTools.Field(_teleportInfoType, "teleportEffectPosition") : null;
            _teleportTypeField = _teleportInfoType != null ? AccessTools.Field(_teleportInfoType, "teleportType") : null;
            _sendToChunksMethod = null;
            object remote = _remoteEffectField != null ? _remoteEffectField.GetValue(controller) : null;
            if (remote != null)
            {
                MethodInfo[] methods = remote.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    ParameterInfo[] parameters = methods[i].GetParameters();
                    if (methods[i].Name == "SendToChunks" && parameters.Length == 2)
                    {
                        _sendToChunksMethod = methods[i];
                        break;
                    }
                }
            }
        }
    }

}
