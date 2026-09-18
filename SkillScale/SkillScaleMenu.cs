using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace SkillScale
{
    /// <summary>
    /// Single-page employment dial. Default backslash (\ / | key). Global + death always visible; skills scroll below.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    internal class SkillScaleMenu : MonoBehaviour
    {
        private const string ConfigManagerGuid = "com.bepis.bepinex.configurationmanager";
        private const float ReferenceHeight = 1080f;
        private const float PanelWidth = 500f;
        private const float RowHeight = 26f;
        private const float DragStep = 0.25f;
        private const float TypeStep = 0.01f;
        private const float IconSize = 22f;
        private const float CheckSize = 22f;

        internal static bool IsOpen { get; private set; }

        private bool _open;
        private bool _centerNextOpen = true;
        private Rect _window = new Rect(0f, 0f, PanelWidth, 520f);
        private Vector2 _bodyScroll;
        private string _globalText;
        private string _deathText;
        private float _uiScale = 1f;
        private int _lastScreenW = -1;
        private int _lastScreenH = -1;
        private bool _stylesReady;
        private string _activeSliderId;
        private string _prevRateFocus;
        private float _checkSpinDegrees = 180f;
        private float _checkMarkScale = 0.78f;
        private readonly Dictionary<string, float> _draftRates = new Dictionary<string, float>();

        private GUIStyle _windowStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _rowBoxStyle;
        private GUIStyle _stepStyle;
        private GUIStyle _fieldStyle;
        private GUIStyle _sliderStyle;
        private GUIStyle _thumbStyle;
        private GUIStyle _closeStyle;
        private GUIStyle _scrollStyle;
        private GUIStyle _ghostButtonStyle;
        private GUIStyle _iconButtonStyle;
        private GUIStyle _vScrollStyle;
        private GUIStyle _vScrollThumbStyle;
        private Texture2D _dimTexture;
        private Texture2D _trackTex;
        private Texture2D _thumbTex;
        private Texture2D _checkOff;
        private Texture2D _checkOffHot;
        private Texture2D _checkMark;
        private Texture2D _checkMarkHot;
        private Texture2D _spunCheckMark;
        private Texture2D _spunCheckMarkHot;

        private readonly GUIStyle[] _presetStyles = new GUIStyle[5];
        private readonly GUIStyle[] _presetOnStyles = new GUIStyle[5];

        private readonly Dictionary<Skills.SkillType, string> _skillTexts =
            new Dictionary<Skills.SkillType, string>();
        private readonly Dictionary<Skills.SkillType, Sprite> _skillIcons =
            new Dictionary<Skills.SkillType, Sprite>();

        private static readonly string[] PresetShort =
        {
            "0.75×",
            "1×",
            "1.5×",
            "2.5×",
            "Custom"
        };

        private static readonly string[] PresetNames =
        {
            "Giga Unemployed",
            "Unemployed",
            "Part-time",
            "Full-time",
            "Custom"
        };

        // Cool = slower grind, warm = faster grind.
        private static readonly Color[] PresetColors =
        {
            new Color(0.35f, 0.48f, 0.62f), // cool blue
            new Color(0.42f, 0.45f, 0.48f), // neutral
            new Color(0.62f, 0.52f, 0.28f), // warm amber
            new Color(0.78f, 0.52f, 0.18f), // hot orange
            new Color(0.48f, 0.40f, 0.55f)  // custom violet
        };

        private void OnDisable()
        {
            if (_open || IsOpen)
            {
                _open = false;
                IsOpen = false;
                Patches.MenuInputPatches.OnMenuClosed();
            }
        }

        private void OnDestroy()
        {
            if (IsOpen)
            {
                IsOpen = false;
                Patches.MenuInputPatches.OnMenuClosed();
            }
        }

        private void Update()
        {
            if (!ModConfig.EnableInGameMenu.Value)
            {
                CloseMenu();
                return;
            }

            if (_open && HasBlockingGameUi())
            {
                CloseMenu();
                return;
            }

            if (ShouldIgnoreHotkey())
            {
                return;
            }

            if (ModConfig.MenuKey.Value.IsDown())
            {
                if (_open)
                {
                    CloseMenu();
                }
                else if (!HasBlockingGameUi())
                {
                    OpenMenu();
                }
            }
            else if (_open && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseMenu();
            }
        }

        private void LateUpdate()
        {
            if (_open)
            {
                Patches.MenuInputPatches.ForceFreeCursor();
            }
        }

        private void OnGUI()
        {
            if (!_open || !ModConfig.EnableInGameMenu.Value)
            {
                return;
            }

            EnsureStyles();

            // Height-led scale (Valheim-like), with a width floor so narrow/Steam Deck layouts still fit.
            float heightScale = Screen.height / ReferenceHeight;
            float widthScale = Screen.width / 1280f;
            _uiScale = Mathf.Clamp(Mathf.Min(heightScale, widthScale * 1.15f), 0.65f, 1.4f);

            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_uiScale, _uiScale, 1f));

            try
            {
                float vw = Screen.width / _uiScale;
                float vh = Screen.height / _uiScale;
                const float margin = 10f;
                float panelW = Mathf.Clamp(PanelWidth, 300f, Mathf.Max(300f, vw - margin * 2f));
                float maxH = Mathf.Clamp(Mathf.Min(620f, vh * 0.9f), 260f, Mathf.Max(260f, vh - margin * 2f));

                DrawBackdrop(vw, vh);

                // Keep wheel for our scroll view; do not let it reach the game camera.
                if (Event.current.type == EventType.ScrollWheel)
                {
                    // Only eat scroll when the pointer is outside our panel; inside, IMGUI scroll uses it.
                    if (!_window.Contains(Event.current.mousePosition))
                    {
                        Event.current.Use();
                    }
                }

                _window.width = panelW;
                _window.height = maxH;

                bool resolutionChanged = Screen.width != _lastScreenW || Screen.height != _lastScreenH;
                if (_centerNextOpen || resolutionChanged)
                {
                    _window.x = (vw - _window.width) * 0.5f;
                    _window.y = (vh - _window.height) * 0.5f;
                    _centerNextOpen = false;
                    _lastScreenW = Screen.width;
                    _lastScreenH = Screen.height;
                }

                _window.x = Mathf.Clamp(_window.x, margin, Mathf.Max(margin, vw - _window.width - margin));
                _window.y = Mathf.Clamp(_window.y, margin, Mathf.Max(margin, vh - _window.height - margin));

                _window = GUI.ModalWindow(
                    0x5C1115CA,
                    _window,
                    DrawWindow,
                    string.Empty,
                    _windowStyle);

                // Keep the panel on-screen; content scrolls inside instead of growing the window.
                _window.width = panelW;
                _window.height = maxH;
                _window.x = Mathf.Clamp(_window.x, margin, Mathf.Max(margin, vw - _window.width - margin));
                _window.y = Mathf.Clamp(_window.y, margin, Mathf.Max(margin, vh - _window.height - margin));
            }
            finally
            {
                GUI.matrix = previous;
            }
        }

        private void OpenMenu()
        {
            _open = true;
            IsOpen = true;
            _centerNextOpen = true;
            _draftRates.Clear();
            _skillTexts.Clear();
            _globalText = null;
            _deathText = null;
            _prevRateFocus = null;
            RefreshSkillIcons();
            Patches.MenuInputPatches.OnMenuOpened();
        }

        private void CloseMenu()
        {
            bool wasOpen = _open;
            _open = false;
            IsOpen = false;
            if (wasOpen)
            {
                Patches.MenuInputPatches.OnMenuClosed();
            }
        }

        private void DrawBackdrop(float width, float height)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), _dimTexture);
            GUI.color = previous;

            if (Event.current.type == EventType.MouseDown && !_window.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
            }
        }

        private void DrawWindow(int id)
        {
            bool locked = IsConfigLockedForLocalPlayer();

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            GUILayout.BeginHorizontal();
            GUILayout.Label("SkillScale", _titleStyle, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("✕", _stepStyle, GUILayout.Width(RowHeight), GUILayout.Height(RowHeight)))
            {
                CloseMenu();
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Scale the grind to how employed you are.", _hintStyle);

            // One body scroll so global, death, and every skill stay reachable on short screens.
            float bodyH = Mathf.Max(160f, _window.height - _windowStyle.padding.vertical - 78f);
            GUIStyle previousThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbarThumb = _vScrollThumbStyle;
            _bodyScroll = GUILayout.BeginScrollView(
                _bodyScroll,
                false,
                true,
                GUIStyle.none,
                _vScrollStyle,
                _scrollStyle,
                GUILayout.Height(bodyH),
                GUILayout.ExpandWidth(true));

            GUI.enabled = !locked;
            if (locked)
            {
                GUILayout.Label("Locked by the server host.", _hintStyle);
            }

            DrawEnableToggle();
            GUILayout.Space(4f);
            DrawXpToastToggle();

            GUILayout.Space(6f);
            DrawPresets();

            GUILayout.Space(6f);
            DrawBoundRateRow(
                "global",
                "Global XP rate",
                "Type freely (e.g. 1.37), then Enter or click away. +/- and the slider use 0.25 steps. Mark shows 1.0.",
                ModConfig.GlobalMultiplier,
                ModConfig.MinRate,
                ModConfig.MaxXpRate,
                ref _globalText);

            GUILayout.Space(4f);
            DrawBoundRateRow(
                "death",
                "Death skill loss",
                "0 = keep skills · 1 = normal · 2 = twice as harsh · max 5",
                ModConfig.DeathLossMultiplier,
                ModConfig.MinRate,
                ModConfig.MaxDeathRate,
                ref _deathText);

            GUILayout.Space(6f);
            DrawSkillsSection(locked, ModConfig.GlobalMultiplier.Value);

            GUILayout.Space(8f);
            GUILayout.EndScrollView();
            GUI.skin.verticalScrollbarThumb = previousThumb;

            GUI.enabled = true;
            GUILayout.Space(2f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Esc closes · \\ / | opens", _hintStyle, GUILayout.ExpandWidth(true));
            if (HasConfigurationManager())
            {
                GUILayout.Label("F1 = Config Manager", _hintStyle);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            if (Event.current.type == EventType.Repaint)
            {
                _prevRateFocus = GUI.GetNameOfFocusedControl();
            }

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private void DrawEnableToggle()
        {
            bool enabled = ModConfig.EnableSkillScaling.Value;
            GUILayout.BeginHorizontal(GUILayout.Height(CheckSize));
            Rect box = GUILayoutUtility.GetRect(CheckSize, CheckSize, GUILayout.Width(CheckSize), GUILayout.Height(CheckSize));
            bool hover = box.Contains(Event.current.mousePosition);
            Texture2D frame = hover ? _checkOffHot : _checkOff;
            Texture2D mark = hover
                ? (_spunCheckMarkHot != null ? _spunCheckMarkHot : _checkMarkHot)
                : (_spunCheckMark != null ? _spunCheckMark : _checkMark);

            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(box, frame, ScaleMode.StretchToFill, true);
                if (enabled)
                {
                    // Spun + randomly sized mark, always centered inside the box.
                    float size = box.width * Mathf.Clamp(_checkMarkScale, 0.5f, 0.92f);
                    float x = box.x + (box.width - size) * 0.5f;
                    float y = box.y + (box.height - size) * 0.5f;
                    GUI.DrawTexture(new Rect(x, y, size, size), mark, ScaleMode.StretchToFill, true);
                }
            }

            if (GUI.Button(box, GUIContent.none, _iconButtonStyle))
            {
                bool turnOn = !enabled;
                if (turnOn)
                {
                    float degrees = UnityEngine.Random.Range(0f, 360f);
                    _checkSpinDegrees = degrees;
                    _checkMarkScale = UnityEngine.Random.Range(0.52f, 0.92f);
                    DestroySpunMarks();
                    _spunCheckMark = BakeRotatedMark(_checkMark, degrees);
                    _spunCheckMarkHot = BakeRotatedMark(_checkMarkHot, degrees);
                }

                ModConfig.EnableSkillScaling.Value = turnOn;
            }

            GUILayout.Space(10f);
            GUILayout.Label("Enable skill scaling", _bodyStyle, GUILayout.Height(CheckSize));
            GUILayout.EndHorizontal();
        }

        private void DrawXpToastToggle()
        {
            bool on = ModConfig.SwingXpNotifications.Value;
            GUILayout.BeginHorizontal(GUILayout.Height(CheckSize));
            Rect box = GUILayoutUtility.GetRect(CheckSize, CheckSize, GUILayout.Width(CheckSize), GUILayout.Height(CheckSize));
            bool hover = box.Contains(Event.current.mousePosition);
            Texture2D frame = hover ? _checkOffHot : _checkOff;
            Texture2D mark = hover ? _checkMarkHot : _checkMark;

            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(box, frame, ScaleMode.StretchToFill, true);
                if (on)
                {
                    float pad = 4f;
                    GUI.DrawTexture(
                        new Rect(box.x + pad, box.y + pad, box.width - pad * 2f, box.height - pad * 2f),
                        mark,
                        ScaleMode.StretchToFill,
                        true);
                }
            }

            if (GUI.Button(box, GUIContent.none, _iconButtonStyle))
            {
                ModConfig.SwingXpNotifications.Value = !on;
            }

            GUILayout.Space(10f);
            GUILayout.BeginVertical();
            GUILayout.Label("Show XP toasts", _bodyStyle, GUILayout.Height(CheckSize));
            GUILayout.Label(
                "Modest top-left progress (skips Run). Also confirms your rate is applying.",
                _hintStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawPresets()
        {
            GUILayout.Label("Employment preset", _sectionStyle);
            int selected = (int)ModConfig.Preset.Value;
            GUILayout.BeginHorizontal();
            for (int i = 0; i < PresetShort.Length; i++)
            {
                bool isOn = i == selected;
                GUIStyle style = isOn ? _presetOnStyles[i] : _presetStyles[i];
                string label = isOn ? $"[{PresetShort[i]}]" : PresetShort[i];
                if (GUILayout.Button(label, style, GUILayout.Height(32f), GUILayout.ExpandWidth(true)))
                {
                    ModConfig.Preset.Value = (EmploymentPreset)i;
                    _draftRates.Clear();
                    _globalText = null;
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Label(
                $"Selected: {PresetNames[selected]} ({PresetShort[selected]} XP rate)",
                _hintStyle);
        }

        private void DrawSkillsSection(bool locked, float global)
        {
            GUILayout.Label("Per skill", _sectionStyle);
            GUILayout.Label("Reset appears when a skill is off the global rate.", _hintStyle);

            // Compare against the rate shown for global (includes in-progress typing/draft).
            float globalDisplay = GetDisplayRate("global", global);

            List<KeyValuePair<Skills.SkillType, ConfigEntry<float>>> skills =
                ModConfig.SkillMultipliers
                    .OrderBy(pair => ModConfig.GetDisplayName(pair.Key), StringComparer.OrdinalIgnoreCase)
                    .ToList();

            GUILayout.BeginHorizontal();
            GUILayout.Label(
                skills.Count > 0 ? $"{skills.Count} skills" : "Load a character to fill this list",
                _bodyStyle,
                GUILayout.ExpandWidth(true));
            GUI.enabled = !locked && skills.Count > 0;
            if (GUILayout.Button("Reset all to global", _ghostButtonStyle, GUILayout.Width(140f), GUILayout.Height(24f)))
            {
                ModConfig.SetAllSkillRates(globalDisplay);
                _skillTexts.Clear();
                ClearSkillDrafts();
            }

            GUI.enabled = !locked;
            GUILayout.EndHorizontal();

            if (skills.Count == 0)
            {
                return;
            }

            RefreshSkillIcons();

            foreach (KeyValuePair<Skills.SkillType, ConfigEntry<float>> pair in skills)
            {
                DrawSkillRow(pair.Key, pair.Value, globalDisplay, locked);
            }
        }

        private void DrawSkillRow(Skills.SkillType skill, ConfigEntry<float> entry, float global, bool locked)
        {
            string name = ModConfig.GetDisplayName(skill);
            string focus = "skill_" + (int)skill;
            if (!_skillTexts.TryGetValue(skill, out string text))
            {
                text = null;
            }

            float display = GetDisplayRate(focus, entry.Value);
            // Per-skill default is "follow global".
            bool offDefault = RatesDiffer(display, global);

            GUILayout.BeginVertical(_rowBoxStyle);

            GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            DrawSkillIcon(skill);
            GUILayout.Space(4f);
            GUILayout.Label(name, _bodyStyle, GUILayout.ExpandWidth(true), GUILayout.Height(RowHeight));

            if (offDefault)
            {
                GUI.enabled = !locked;
                if (GUILayout.Button("Reset", _ghostButtonStyle, GUILayout.Width(52f), GUILayout.Height(22f)))
                {
                    entry.Value = global;
                    _skillTexts[skill] = Format(global);
                    _draftRates.Remove(focus);
                    display = global;
                    text = Format(global);
                }
            }

            GUI.enabled = !locked;
            GUILayout.EndHorizontal();

            float next = DrawRateControls(focus, display, ModConfig.MinRate, ModConfig.MaxXpRate, ref text, out bool commit);
            _skillTexts[skill] = text;
            if (commit)
            {
                entry.Value = next;
                _draftRates.Remove(focus);
            }
            else if (RatesDiffer(next, display))
            {
                _draftRates[focus] = next;
            }

            GUILayout.EndVertical();
        }

        private void DrawBoundRateRow(
            string focus,
            string title,
            string hint,
            ConfigEntry<float> entry,
            float min,
            float max,
            ref string text)
        {
            float def = EntryDefault(entry);
            float display = GetDisplayRate(focus, entry.Value);
            bool offDefault = RatesDiffer(display, def);

            GUILayout.BeginHorizontal();
            GUILayout.Label(title, _sectionStyle, GUILayout.ExpandWidth(true));
            if (offDefault)
            {
                if (GUILayout.Button("Reset", _ghostButtonStyle, GUILayout.Width(52f), GUILayout.Height(22f)))
                {
                    entry.Value = def;
                    text = Format(def);
                    _draftRates.Remove(focus);
                    display = def;
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Label(hint, _hintStyle);
            float next = DrawRateControls(focus, display, min, max, ref text, out bool commit);
            if (commit)
            {
                if (RatesDiffer(entry.Value, next))
                {
                    entry.Value = next;
                }

                _draftRates.Remove(focus);
                if (focus == "global")
                {
                    // Global SettingChanged updates linked skills; refresh their field text.
                    _skillTexts.Clear();
                    ClearSkillDrafts();
                }
            }
            else if (RatesDiffer(next, display))
            {
                _draftRates[focus] = next;
            }
        }

        private static float EntryDefault(ConfigEntry<float> entry)
        {
            try
            {
                return Convert.ToSingle(entry.DefaultValue, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 1f;
            }
        }

        private static bool RatesDiffer(float a, float b)
        {
            return Mathf.Abs(a - b) > 0.005f;
        }

        private float GetDisplayRate(string focus, float committed)
        {
            return _draftRates.TryGetValue(focus, out float draft) ? draft : committed;
        }

        private void ClearSkillDrafts()
        {
            List<string> keys = _draftRates.Keys.Where(k => k.StartsWith("skill_", StringComparison.Ordinal)).ToList();
            foreach (string key in keys)
            {
                _draftRates.Remove(key);
            }
        }

        private float DrawRateControls(
            string focus,
            float value,
            float min,
            float max,
            ref string text,
            out bool commit)
        {
            commit = false;
            GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));

            Rect sliderRect = GUILayoutUtility.GetRect(
                10f,
                RowHeight,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(RowHeight));
            float sliderValue = DrawCenteredSlider(focus, sliderRect, value, min, max, out bool sliderCommit);
            // Snap in 0.25 steps while dragging so the thumb and number jump together.
            float snapped = Clamp(SnapDrag(sliderValue), min, max);
            bool moved = Mathf.Abs(snapped - value) > 0.0005f;
            if (moved)
            {
                value = snapped;
            }

            if (sliderCommit)
            {
                commit = true;
                value = snapped;
            }

            GUILayout.Space(4f);
            // +/- jump to the next/previous 0.25 notch (1.37 + → 1.5, 1.37 - → 1.25).
            if (GUILayout.Button("-", _stepStyle, GUILayout.Width(RowHeight), GUILayout.Height(RowHeight)))
            {
                value = Clamp(StepDragDown(value), min, max);
                commit = true;
                moved = true;
                text = Format(value);
            }

            bool focused = GUI.GetNameOfFocusedControl() == focus;
            bool lostFocus = !commit &&
                             !string.IsNullOrEmpty(_prevRateFocus) &&
                             _prevRateFocus == focus &&
                             !focused;

            if (text == null || moved || (!focused && !lostFocus))
            {
                text = Format(SnapType(value));
            }

            GUI.SetNextControlName(focus);
            string typed = GUILayout.TextField(text, _fieldStyle, GUILayout.Width(58f), GUILayout.Height(RowHeight));
            text = typed;

            if (GUILayout.Button("+", _stepStyle, GUILayout.Width(RowHeight), GUILayout.Height(RowHeight)))
            {
                value = Clamp(StepDragUp(value), min, max);
                commit = true;
                moved = true;
                text = Format(value);
            }

            GUILayout.EndHorizontal();

            bool enterPressed = Event.current.type == EventType.KeyDown &&
                                (Event.current.keyCode == KeyCode.Return ||
                                 Event.current.keyCode == KeyCode.KeypadEnter);

            // Free typing while focused (keeps values like 1.37). Commit only on Enter or leaving the field.
            if (!commit && focused && enterPressed)
            {
                if (TryParseRate(typed, out float parsed))
                {
                    parsed = Clamp(SnapType(parsed), min, max);
                    value = parsed;
                    text = Format(parsed);
                    commit = true;
                }
                else
                {
                    text = Format(SnapType(value));
                }

                Event.current.Use();
                GUI.FocusControl(null);
                _prevRateFocus = null;
            }
            else if (!commit && lostFocus)
            {
                if (TryParseRate(typed, out float parsed))
                {
                    parsed = Clamp(SnapType(parsed), min, max);
                    value = parsed;
                    text = Format(parsed);
                    commit = true;
                }
                else
                {
                    text = Format(SnapType(value));
                }
            }

            return value;
        }

        private float DrawCenteredSlider(
            string id,
            Rect area,
            float value,
            float min,
            float max,
            out bool commit)
        {
            commit = false;
            const float trackH = 8f;
            const float thumbW = 14f;
            const float thumbH = 14f;

            if (area.width < 8f || area.height < 8f)
            {
                return value;
            }

            Rect track = new Rect(
                area.x,
                area.y + (area.height - trackH) * 0.5f,
                area.width,
                trackH);

            Event e = Event.current;
            Vector2 mouse = e.mousePosition;

            if (e.type == EventType.MouseDown && e.button == 0 && area.Contains(mouse))
            {
                _activeSliderId = id;
                value = SnapDrag(ValueFromPointer(track, mouse.x, min, max, thumbW));
                e.Use();
            }

            if (_activeSliderId == id)
            {
                if (Input.GetMouseButton(0))
                {
                    value = SnapDrag(ValueFromPointer(track, mouse.x, min, max, thumbW));
                    if (e.type == EventType.MouseDrag)
                    {
                        e.Use();
                    }
                }

                if (e.type == EventType.MouseUp && e.button == 0)
                {
                    value = SnapDrag(ValueFromPointer(track, mouse.x, min, max, thumbW));
                    _activeSliderId = null;
                    commit = true;
                    e.Use();
                }
            }

            if (e.type == EventType.Repaint)
            {
                GUI.DrawTexture(track, _trackTex);

                // Vanilla 1.0 tick so "how employed" reads at a glance.
                if (min < 1f && max > 1f)
                {
                    float oneT = Mathf.InverseLerp(min, max, 1f);
                    float oneX = track.x + oneT * track.width;
                    Rect tick = new Rect(oneX - 1f, track.y - 3f, 2f, trackH + 6f);
                    GUI.DrawTexture(tick, _thumbTex);
                }

                float t = Mathf.InverseLerp(min, max, value);
                float thumbX = track.x + t * Mathf.Max(0f, track.width - thumbW);
                float midY = track.y + track.height * 0.5f;
                Rect thumb = new Rect(thumbX, midY - thumbH * 0.5f, thumbW, thumbH);
                GUI.DrawTexture(thumb, _thumbTex);
            }

            return Clamp(value, min, max);
        }

        private static float ValueFromPointer(Rect track, float pointerX, float min, float max, float thumbW)
        {
            float start = track.x + thumbW * 0.5f;
            float end = track.xMax - thumbW * 0.5f;
            float t = Mathf.InverseLerp(start, end, pointerX);
            return Mathf.Lerp(min, max, Mathf.Clamp01(t));
        }

        private void DrawSkillIcon(Skills.SkillType skill)
        {
            Rect rect = GUILayoutUtility.GetRect(IconSize, IconSize, GUILayout.Width(IconSize), GUILayout.Height(IconSize));
            if (_skillIcons.TryGetValue(skill, out Sprite sprite) && sprite != null)
            {
                DrawSprite(rect, sprite);
            }
            else if (ModConfig.SkillIcons.TryGetValue(skill, out Sprite cached) && cached != null)
            {
                DrawSprite(rect, cached);
            }
        }

        private void RefreshSkillIcons()
        {
            try
            {
                AbsorbIconsFrom(Player.m_localPlayer?.GetSkills());

                if (Player.m_localPlayer == null && ZNetScene.instance != null)
                {
                    GameObject prefab = ZNetScene.instance.GetPrefab("Player");
                    if (prefab != null)
                    {
                        AbsorbIconsFrom(prefab.GetComponent<Skills>());
                        AbsorbIconsFrom(prefab.GetComponentInChildren<Skills>(true));
                    }
                }

                foreach (KeyValuePair<Skills.SkillType, Sprite> pair in ModConfig.SkillIcons)
                {
                    if (pair.Value != null)
                    {
                        _skillIcons[pair.Key] = pair.Value;
                    }
                }
            }
            catch
            {
                // Optional.
            }
        }

        private void AbsorbIconsFrom(Skills skills)
        {
            if (skills?.m_skills == null)
            {
                return;
            }

            foreach (Skills.SkillDef def in skills.m_skills)
            {
                if (def?.m_icon == null)
                {
                    continue;
                }

                _skillIcons[def.m_skill] = def.m_icon;
                ModConfig.SkillIcons[def.m_skill] = def.m_icon;
            }
        }

        private static void DrawSprite(Rect screenRect, Sprite sprite)
        {
            Texture2D texture = sprite.texture;
            if (texture == null)
            {
                return;
            }

            Rect texRect = sprite.textureRect;
            Rect uv = new Rect(
                texRect.x / texture.width,
                texRect.y / texture.height,
                texRect.width / texture.width,
                texRect.height / texture.height);
            GUI.DrawTextureWithTexCoords(screenRect, texture, uv, true);
        }

        private void EnsureStyles()
        {
            if (_stylesReady && _windowStyle != null)
            {
                return;
            }

            Color bg = Hex(0x12161C, 0.98f);
            Color row = Hex(0x1C222B, 1f);
            Color raised = Hex(0x2A313C, 1f);
            Color raisedHot = Hex(0x3A4452, 1f);
            Color accent = Hex(0xE8C468, 1f);
            Color accentDeep = Hex(0xC49A3C, 1f);
            Color text = Hex(0xF4EEE0, 1f);
            Color hint = Hex(0xA79B86, 1f);
            Color field = Hex(0x0B0E12, 1f);
            Color track = Hex(0x0B0E12, 1f);

            Texture2D bgTex = Solid(bg);
            Texture2D rowTex = Solid(row);
            Texture2D raisedTex = Solid(raised);
            Texture2D raisedHotTex = Solid(raisedHot);
            Texture2D accentTex = Solid(accent);
            Texture2D accentDeepTex = Solid(accentDeep);
            Texture2D fieldTex = Solid(field);
            Texture2D trackTex = Solid(track);
            Texture2D thumbTex = Solid(accent);
            _dimTexture = Solid(Color.black);
            _trackTex = trackTex;
            _thumbTex = thumbTex;

            _checkOff = MakeCheckboxFrame(false);
            _checkOffHot = MakeCheckboxFrame(true);
            _checkMark = MakeCheckMark(false);
            _checkMarkHot = MakeCheckMark(true);

            _windowStyle = Base(GUI.skin.window, bgTex, text, 13);
            _windowStyle.padding = new RectOffset(14, 14, 12, 18);
            _windowStyle.border = new RectOffset(10, 10, 10, 10);

            _titleStyle = Label(22, accent, FontStyle.Bold);
            _bodyStyle = Label(13, text, FontStyle.Normal);
            _bodyStyle.alignment = TextAnchor.MiddleLeft;
            _hintStyle = Label(11, hint, FontStyle.Normal);
            _hintStyle.wordWrap = true;
            _hintStyle.alignment = TextAnchor.MiddleLeft;
            _sectionStyle = Label(13, accent, FontStyle.Bold);

            _rowBoxStyle = Base(GUI.skin.box, rowTex, text, 12);
            _rowBoxStyle.padding = new RectOffset(6, 6, 4, 4);
            _rowBoxStyle.margin = new RectOffset(0, 0, 2, 2);

            _scrollStyle = Base(GUI.skin.box, rowTex, text, 12);
            _scrollStyle.padding = new RectOffset(6, 12, 6, 12);

            Texture2D scrollBg = Solid(Hex(0x0B0E12, 1f));
            Texture2D scrollThumb = Solid(Hex(0x6A6356, 1f));
            Texture2D scrollThumbHot = Solid(Hex(0xC49A3C, 1f));
            _vScrollStyle = new GUIStyle(GUI.skin.verticalScrollbar)
            {
                fixedWidth = 10f,
                normal = { background = scrollBg },
                hover = { background = scrollBg },
                active = { background = scrollBg }
            };
            _vScrollThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb)
            {
                fixedWidth = 10f,
                normal = { background = scrollThumb },
                hover = { background = scrollThumbHot },
                active = { background = scrollThumbHot }
            };

            for (int i = 0; i < PresetColors.Length; i++)
            {
                Color c = PresetColors[i];
                Color idle = Color.Lerp(c, raised, 0.45f);
                Color hot = Color.Lerp(c, Color.white, 0.18f);
                Color on = Color.Lerp(c, accent, 0.25f);
                _presetStyles[i] = Button(Solid(idle), Solid(hot), Solid(on), text, 12);
                _presetStyles[i].alignment = TextAnchor.MiddleCenter;
                _presetStyles[i].fontStyle = FontStyle.Bold;
                _presetStyles[i].margin = new RectOffset(0, 4, 0, 0);
                // Selected: bright fill + thick gold rim so active preset is obvious.
                _presetOnStyles[i] = Button(
                    Bordered(on, accent, 5),
                    Bordered(hot, accent, 5),
                    Bordered(on, Hex(0xFFF0C0, 1f), 5),
                    Hex(0x1A1408, 1f),
                    13);
                _presetOnStyles[i].alignment = TextAnchor.MiddleCenter;
                _presetOnStyles[i].fontStyle = FontStyle.Bold;
                _presetOnStyles[i].margin = new RectOffset(0, 4, 0, 0);
            }

            _stepStyle = Button(raisedTex, raisedHotTex, accentDeepTex, text, 14);
            _stepStyle.alignment = TextAnchor.MiddleCenter;
            _stepStyle.fontStyle = FontStyle.Bold;
            _stepStyle.margin = new RectOffset(2, 0, 0, 0);
            _stepStyle.padding = new RectOffset(0, 0, 0, 0);
            _stepStyle.fixedHeight = RowHeight;
            _stepStyle.fixedWidth = RowHeight;

            _fieldStyle = Base(GUI.skin.textField, fieldTex, text, 13);
            _fieldStyle.alignment = TextAnchor.MiddleCenter;
            _fieldStyle.padding = new RectOffset(4, 4, 0, 0);
            _fieldStyle.margin = new RectOffset(2, 2, 0, 0);
            _fieldStyle.fixedHeight = RowHeight;
            PaintAllBg(_fieldStyle, fieldTex, raisedHotTex);
            PaintText(_fieldStyle, text);

            _ghostButtonStyle = Button(raisedTex, raisedHotTex, accentDeepTex, text, 11);
            _ghostButtonStyle.alignment = TextAnchor.MiddleCenter;
            _ghostButtonStyle.padding = new RectOffset(6, 6, 2, 2);

            _iconButtonStyle = new GUIStyle(GUIStyle.none)
            {
                alignment = TextAnchor.MiddleCenter,
                stretchWidth = false,
                stretchHeight = false,
                fixedWidth = CheckSize,
                fixedHeight = CheckSize
            };

            _closeStyle = Button(accentDeepTex, accentTex, accentTex, Hex(0x1A1408, 1f), 14);
            _closeStyle.fontStyle = FontStyle.Bold;
            _closeStyle.alignment = TextAnchor.MiddleCenter;

            _sliderStyle = new GUIStyle
            {
                normal = { background = trackTex },
                hover = { background = trackTex },
                active = { background = trackTex },
                focused = { background = trackTex },
                fixedHeight = 10f
            };

            _thumbStyle = new GUIStyle
            {
                normal = { background = accentTex },
                hover = { background = accentTex },
                active = { background = accentDeepTex },
                focused = { background = accentTex },
                fixedWidth = 12f,
                fixedHeight = 18f
            };

            _stylesReady = true;
        }

        private static Texture2D MakeCheckboxFrame(bool hover)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color border = hover ? Hex(0xE8C468, 1f) : Hex(0xC49A3C, 1f);
            Color fill = Hex(0x1C222B, 1f);
            Color clear = new Color(0f, 0f, 0f, 0f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x < 5 || y < 5 || x >= size - 5 || y >= size - 5;
                    pixels[y * size + x] = edge ? border : fill;
                }
            }

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3 - i; j++)
                {
                    pixels[i * size + j] = clear;
                    pixels[i * size + (size - 1 - j)] = clear;
                    pixels[(size - 1 - i) * size + j] = clear;
                    pixels[(size - 1 - i) * size + (size - 1 - j)] = clear;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static Texture2D MakeCheckMark(bool hover)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color check = hover ? Hex(0xFFF0C0, 1f) : Hex(0xE8C468, 1f);
            Color clear = new Color(0f, 0f, 0f, 0f);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            // Upright check in Unity texture space (y=0 at bottom). Rotation handles the comedy.
            DrawCheckLine(pixels, size, 16, 30, 28, 18, check, 5);
            DrawCheckLine(pixels, size, 28, 18, 50, 44, check, 5);

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private void DestroySpunMarks()
        {
            if (_spunCheckMark != null)
            {
                UnityEngine.Object.Destroy(_spunCheckMark);
                _spunCheckMark = null;
            }

            if (_spunCheckMarkHot != null)
            {
                UnityEngine.Object.Destroy(_spunCheckMarkHot);
                _spunCheckMarkHot = null;
            }
        }

        /// <summary>
        /// Bake a rotated check so the easter-egg spin stays inside the box (no GUI matrix tricks).
        /// </summary>
        private static Texture2D BakeRotatedMark(Texture2D source, float degrees)
        {
            int size = source.width;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color clear = new Color(0f, 0f, 0f, 0f);
            var pixels = new Color[size * size];
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            float mid = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - mid;
                    float dy = y - mid;
                    float sx = (dx * cos) + (dy * sin) + mid;
                    float sy = (-dx * sin) + (dy * cos) + mid;
                    if (sx < 0f || sy < 0f || sx >= size - 1 || sy >= size - 1)
                    {
                        pixels[y * size + x] = clear;
                        continue;
                    }

                    pixels[y * size + x] = source.GetPixelBilinear(sx / (size - 1f), sy / (size - 1f));
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static void DrawCheckLine(Color[] pixels, int size, int x0, int y0, int x1, int y1, Color color, int thickness)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * 2;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int cx = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int cy = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int oy = -thickness; oy <= thickness; oy++)
                {
                    for (int ox = -thickness; ox <= thickness; ox++)
                    {
                        if ((ox * ox) + (oy * oy) > thickness * thickness)
                        {
                            continue;
                        }

                        int x = cx + ox;
                        int y = cy + oy;
                        if (x < 0 || y < 0 || x >= size || y >= size)
                        {
                            continue;
                        }

                        pixels[y * size + x] = color;
                    }
                }
            }
        }

        private static GUIStyle Label(int size, Color color, FontStyle fontStyle)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = fontStyle,
                wordWrap = false,
                richText = false
            };
            PaintText(style, color);
            return style;
        }

        private static GUIStyle Base(GUIStyle proto, Texture2D bg, Color text, int size)
        {
            var style = new GUIStyle(proto)
            {
                fontSize = size,
                richText = false
            };
            PaintAllBg(style, bg, bg);
            PaintText(style, text);
            return style;
        }

        private static GUIStyle Button(Texture2D normal, Texture2D hover, Texture2D active, Color text, int size)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = size,
                richText = false,
                border = new RectOffset(4, 4, 4, 4)
            };
            PaintAllBg(style, normal, hover, active);
            PaintText(style, text);
            return style;
        }

        private static void PaintAllBg(GUIStyle style, Texture2D normal, Texture2D hover)
        {
            PaintAllBg(style, normal, hover, hover);
        }

        private static void PaintAllBg(GUIStyle style, Texture2D normal, Texture2D hover, Texture2D active)
        {
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = active;
            style.focused.background = hover;
            style.onNormal.background = normal;
            style.onHover.background = hover;
            style.onActive.background = active;
            style.onFocused.background = hover;
        }

        private static void PaintText(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        private static Color Hex(int rgb, float alpha)
        {
            float r = ((rgb >> 16) & 0xFF) / 255f;
            float g = ((rgb >> 8) & 0xFF) / 255f;
            float b = (rgb & 0xFF) / 255f;
            return new Color(r, g, b, alpha);
        }

        private static Texture2D Bordered(Color fill, Color border, int borderPx)
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x < borderPx || y < borderPx || x >= size - borderPx || y >= size - borderPx;
                    pixels[y * size + x] = edge ? border : fill;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false);
            return tex;
        }

        private static Texture2D Solid(Color color)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixels(new[] { color, color, color, color });
            tex.Apply(false);
            return tex;
        }

        private static bool IsConfigLockedForLocalPlayer()
        {
            return Plugin.ConfigSync != null &&
                   Plugin.ConfigSync.IsLocked &&
                   !Plugin.ConfigSync.IsAdmin;
        }

        private static bool HasBlockingGameUi()
        {
            try
            {
                if (InventoryGui.IsVisible() || Menu.IsVisible() || TextInput.IsVisible())
                {
                    return true;
                }

                if (Chat.instance != null && Chat.instance.HasFocus())
                {
                    return true;
                }

                if (StoreGui.IsVisible())
                {
                    return true;
                }

                if (TextViewer.instance != null && TextViewer.instance.IsVisible())
                {
                    return true;
                }
            }
            catch
            {
                // UI not ready.
            }

            return false;
        }

        private static bool TryParseRate(string text, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            text = text.Trim().TrimEnd('x', 'X', '×');
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            // Allow comma decimals (1,25) when the OS locale uses them.
            text = text.Replace(',', '.');
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string Format(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static float Clamp(float value, float min, float max)
        {
            return Mathf.Clamp(value, min, max);
        }

        private static float SnapDrag(float value)
        {
            return (float)Math.Round(value / DragStep) * DragStep;
        }

        private static float StepDragUp(float value)
        {
            return (Mathf.Floor((value + 0.0001f) / DragStep) + 1f) * DragStep;
        }

        private static float StepDragDown(float value)
        {
            return (Mathf.Ceil((value - 0.0001f) / DragStep) - 1f) * DragStep;
        }

        private static float SnapType(float value)
        {
            return (float)Math.Round(value / TypeStep) * TypeStep;
        }

        private static bool HasConfigurationManager()
        {
            return Chainloader.PluginInfos != null &&
                   Chainloader.PluginInfos.ContainsKey(ConfigManagerGuid);
        }

        private static bool ShouldIgnoreHotkey()
        {
            try
            {
                // Match Valheim's "input blocked" idea so \ can be typed in chat safely.
                if (Console.IsVisible() || TextInput.IsVisible())
                {
                    return true;
                }

                if (Chat.instance != null && Chat.instance.HasFocus())
                {
                    return true;
                }

                if (Minimap.IsOpen())
                {
                    return true;
                }

                if (ZNet.instance != null && ZNet.instance.InPasswordDialog())
                {
                    return true;
                }

                if (Menu.IsVisible() || InventoryGui.IsVisible() || StoreGui.IsVisible())
                {
                    return true;
                }

                if (TextViewer.instance != null && TextViewer.instance.IsVisible())
                {
                    return true;
                }
            }
            catch
            {
                // Not ready.
            }

            return false;
        }
    }
}
