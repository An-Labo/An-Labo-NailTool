using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.Model;
using world.anlabo.mdnailtool.Editor.NailDesigns;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Language;

namespace world.anlabo.mdnailtool.Editor.Window {
    public class SearchNailDesignWindow : EditorWindow {
        private MDNailToolWindow _parentWindow;

        private VisualElement _nailGrid;
        private Label _pageLabel;
        private ToolbarSearchField _searchField;
        private DropdownField _sortDropdown;

        private List<Toggle> _allToggles = new List<Toggle>();
        private List<Button> _colorButtons = new List<Button>();
        private Toggle _favToggle;
        private Toggle _importedToggle;
        private Toggle _notImportedToggle;

        private List<NailDesign> _allDesigns = new();
        private List<NailDesign> _filteredDesigns = new();

        private string _searchText = "";
        private bool _favPriority = false;
        private bool _showImported = true;
        private bool _showNotImported = true;
        private readonly HashSet<string> _activeTags = new();
        private readonly HashSet<string> _activeColors = new();

        private const string PrefFavPriority = "MDNail_Search_FavPriority";

        private int _pageIndex = 0;
        private const int ItemsPerPage = 12;

        private static readonly Vector2 MinWindowSize = new Vector2(700, 600);

        private Color _highlightColor;
        private Color _borderColor;

        private readonly Dictionary<string, string> _tagMap = new() {
            {"tag.cute", "cute"}, {"tag.cool", "cool"},
            {"tag.nuance", "nuance"}, {"tag.pop", "pop"},
            {"tag.simple", "simple"}, {"tag.flashy", "flashy"},
            {"tag.elegance", "elegance"}
        };

        private readonly string[] _monoColors = { "white", "black", "gray" };
        private readonly string[] _colorsRow2 = { "brown", "green", "blue", "purple" };
        private readonly string[] _colorsRow3 = { "red", "yellow", "pink", "orange" };

        private readonly Dictionary<string, Color> _colorValueMap = new() {
            {"white", Color.white}, {"black", Color.black}, {"gray", Color.gray},
            {"brown", new Color(0.6f, 0.4f, 0.2f)}, {"green", Color.green},
            {"blue", Color.blue}, {"purple", new Color(0.5f, 0, 0.5f)},
            {"red", Color.red}, {"yellow", Color.yellow}, {"pink", new Color(1f, 0.4f, 0.7f)},
            {"orange", new Color(1f, 0.5f, 0f)}
        };

        public static void ShowWindow(MDNailToolWindow parentWindow) {
            SearchNailDesignWindow window = CreateInstance<SearchNailDesignWindow>();
            string title = LanguageManager.S("window.search_nail") ?? "Search Nail";
            window.titleContent = new GUIContent(title);
            window._parentWindow = parentWindow;
            window.minSize = MinWindowSize;
            window.ShowAuxWindow();
        }

        private void CreateGUI() {
            _allToggles.Clear();
            _colorButtons.Clear();
            _favPriority = EditorPrefs.GetBool(PrefFavPriority, false);
            _showImported = true;
            _showNotImported = true;

            bool isDark = EditorGUIUtility.isProSkin;
            _highlightColor = isDark ? new Color(1f, 0.85f, 0.3f) : new Color(0.2f, 0.5f, 0.9f);
            _borderColor    = isDark ? new Color(0.15f, 0.15f, 0.15f) : Color.gray;

            // 共有USSを読み込む
            var uss = MDNailToolAssetLoader.LoadByGuid<StyleSheet>(MDNailToolGuids.WindowUss);
            if (uss != null) rootVisualElement.styleSheets.Add(uss);

            var root = rootVisualElement;
            root.AddToClassList("mdn-search-root");

            // ---- タイトル行 ----
            var titleBar = new VisualElement();
            titleBar.style.flexDirection = FlexDirection.Row;
            titleBar.style.alignItems = Align.Center;
            titleBar.style.marginBottom = 6;
            titleBar.style.paddingBottom = 6;
            titleBar.style.borderBottomWidth = 1;
            titleBar.style.borderBottomColor = new Color(0.27f, 0.27f, 0.27f);

            var titleLbl = new Label("Nail Design Search");
            titleLbl.style.fontSize = 14;
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLbl.style.color = isDark ? new Color(0.93f, 0.93f, 0.93f) : new Color(0.1f, 0.1f, 0.1f);
            titleLbl.style.flexGrow = 1;
            titleBar.Add(titleLbl);

            root.Add(titleBar);

            // ---- トップバー ----
            var topBar = new VisualElement();
            topBar.AddToClassList("mdn-search-topbar");

            _searchField = new ToolbarSearchField();
            _searchField.AddToClassList("mdn-search-field");
            _searchField.RegisterValueChangedCallback(evt => { _searchText = evt.newValue; UpdateFilter(); });
            topBar.Add(_searchField);

            string sortNewest = LanguageManager.S("window.sort.newest") ?? "Newest";
            string sortName   = LanguageManager.S("window.sort.name")   ?? "Name";
            string sortUsage  = LanguageManager.S("window.sort.usage")  ?? "Usage Count";
            _sortDropdown = new DropdownField(new List<string> { sortNewest, sortName, sortUsage }, 0);
            _sortDropdown.AddToClassList("mdn-search-sort");
            _sortDropdown.RegisterValueChangedCallback(_ => UpdateFilter());
            topBar.Add(_sortDropdown);

            var resetBtn = new Button(ResetFilters) { text = "Reset" };
            resetBtn.AddToClassList("mdn-search-reset-btn");
            topBar.Add(resetBtn);
            root.Add(topBar);

            // ---- フィルターパネル ----
            var filterPanel = new VisualElement();
            filterPanel.AddToClassList("mdn-filter-panel");
            root.Add(filterPanel);

            var textArea = new VisualElement();
            textArea.style.flexDirection = FlexDirection.Row;
            textArea.style.flexGrow = 1;
            textArea.style.marginRight = 10;
            filterPanel.Add(textArea);

            var col1 = CreateFilterCol();
            string favText = LanguageManager.S("window.favorite_priority") ?? "Fav Priority";
            _favToggle = AddFilterCheck(col1, favText, v => {
                _favPriority = v;
                EditorPrefs.SetBool(PrefFavPriority, v);
            });
            _favToggle.value = _favPriority;
            AddTagToColumn(col1, "tag.cute");
            AddTagToColumn(col1, "tag.cool");
            textArea.Add(col1);

            var col2 = CreateFilterCol();
            string importedText = LanguageManager.S("window.show_imported") ?? "Imported";
            _importedToggle = AddFilterCheck(col2, importedText, v => _showImported = v);
            _importedToggle.value = _showImported;
            AddTagToColumn(col2, "tag.nuance");
            AddTagToColumn(col2, "tag.pop");
            textArea.Add(col2);

            var col3 = CreateFilterCol();
            string notImportedText = LanguageManager.S("window.show_not_imported") ?? "Not Imported";
            _notImportedToggle = AddFilterCheck(col3, notImportedText, v => _showNotImported = v);
            _notImportedToggle.value = _showNotImported;
            AddTagToColumn(col3, "tag.simple");
            AddTagToColumn(col3, "tag.flashy");
            textArea.Add(col3);

            var col4 = CreateFilterCol();
            col4.Add(new VisualElement { style = { height = 20, marginBottom = 2 } });
            AddTagToColumn(col4, "tag.elegance");
            textArea.Add(col4);

            var divider = new VisualElement();
            divider.AddToClassList("mdn-filter-divider");
            filterPanel.Add(divider);

            var colorArea = new VisualElement();
            colorArea.AddToClassList("mdn-filter-color-area");
            filterPanel.Add(colorArea);

            var row1 = new VisualElement(); row1.AddToClassList("mdn-filter-color-row");
            foreach (var c in _monoColors) AddColorButton(row1, c);
            colorArea.Add(row1);

            var row2 = new VisualElement(); row2.AddToClassList("mdn-filter-color-row");
            foreach (var c in _colorsRow2) AddColorButton(row2, c);
            colorArea.Add(row2);

            var row3 = new VisualElement(); row3.AddToClassList("mdn-filter-color-row");
            foreach (var c in _colorsRow3) AddColorButton(row3, c);
            colorArea.Add(row3);

            // ---- カードグリッド ----
            var scroll = new ScrollView(); scroll.style.flexGrow = 1;
            var gridOuter = new VisualElement();
            gridOuter.AddToClassList("mdn-nail-grid-outer");
            _nailGrid = new VisualElement();
            _nailGrid.AddToClassList("mdn-nail-grid");
            gridOuter.Add(_nailGrid);
            scroll.Add(gridOuter);
            root.Add(scroll);

            // ---- ページネーション ----
            var footer = new VisualElement();
            footer.AddToClassList("mdn-search-footer");

            var prevBtn = new Button(() => ChangePage(-1)) { text = "◀" };
            prevBtn.AddToClassList("mdn-page-btn");

            _pageLabel = new Label("1 / 1");
            _pageLabel.AddToClassList("mdn-page-label");

            var nextBtn = new Button(() => ChangePage(1)) { text = "▶" };
            nextBtn.AddToClassList("mdn-page-btn");

            footer.Add(prevBtn);
            footer.Add(_pageLabel);
            footer.Add(nextBtn);
            root.Add(footer);

            LoadData();
        }

        // -------------------------------------------------------

        private static VisualElement CreateFilterCol() {
            var col = new VisualElement();
            col.AddToClassList("mdn-filter-col");
            return col;
        }

        private void AddTagToColumn(VisualElement column, string key) {
            if (!_tagMap.TryGetValue(key, out string jsonTag)) return;
            string displayName = LanguageManager.S(key) ?? key.Replace("tag.", "");
            var t = AddFilterCheck(column, displayName, val => {
                if (val) _activeTags.Add(jsonTag); else _activeTags.Remove(jsonTag);
            });
            _allToggles.Add(t);
        }

        private Toggle AddFilterCheck(VisualElement parent, string labelText, Action<bool> onToggle) {
            var row = new VisualElement();
            row.AddToClassList("mdn-filter-check-row");

            var toggle = new Toggle();
            toggle.style.width = 18; toggle.style.height = 18;
            toggle.style.marginRight = 0; toggle.style.marginLeft = 0;
            toggle.style.marginTop = 0; toggle.style.marginBottom = 0;

            var label = new Label(labelText);
            label.AddToClassList("mdn-filter-check-label");

            toggle.RegisterValueChangedCallback(evt => { onToggle(evt.newValue); UpdateFilter(); });
            row.RegisterCallback<ClickEvent>(evt => { if (evt.target != toggle) toggle.value = !toggle.value; });

            row.Add(toggle);
            row.Add(label);
            parent.Add(row);
            return toggle;
        }

        private void AddColorButton(VisualElement parent, string colorKey) {
            if (!_colorValueMap.TryGetValue(colorKey, out Color colorValue)) return;
            var btn = new Button {
                style = {
                    width = 26, height = 26,
                    borderTopLeftRadius = 13, borderTopRightRadius = 13,
                    borderBottomLeftRadius = 13, borderBottomRightRadius = 13,
                    backgroundColor = colorValue,
                    marginRight = 4, marginBottom = 0,
                    borderTopWidth = 2, borderBottomWidth = 2,
                    borderLeftWidth = 2, borderRightWidth = 2,
                    borderTopColor = _borderColor, borderBottomColor = _borderColor,
                    borderLeftColor = _borderColor, borderRightColor = _borderColor
                }
            };
            btn.clicked += () => {
                if (_activeColors.Contains(colorKey)) {
                    _activeColors.Remove(colorKey);
                    SetBorderWidth(btn, 2); SetBorderColor(btn, _borderColor);
                    btn.transform.scale = Vector3.one;
                } else {
                    _activeColors.Add(colorKey);
                    SetBorderWidth(btn, 4); SetBorderColor(btn, _highlightColor);
                    btn.transform.scale = new Vector3(1.15f, 1.15f, 1);
                }
                UpdateFilter();
            };
            _colorButtons.Add(btn);
            parent.Add(btn);
        }

        private void ResetFilters() {
            _searchText = ""; _searchField.value = "";
            _showImported = true; _showNotImported = true;
            _activeTags.Clear(); _activeColors.Clear();
            if (_favToggle != null) _favToggle.SetValueWithoutNotify(false);
            if (_importedToggle != null) _importedToggle.SetValueWithoutNotify(true);
            if (_notImportedToggle != null) _notImportedToggle.SetValueWithoutNotify(true);
            foreach (var t in _allToggles) t.SetValueWithoutNotify(false);
            foreach (var b in _colorButtons) {
                SetBorderWidth(b, 2); SetBorderColor(b, _borderColor);
                b.transform.scale = Vector3.one;
            }
            UpdateFilter();
        }

        private static void SetBorderWidth(VisualElement e, float w) {
            e.style.borderTopWidth = w; e.style.borderBottomWidth = w;
            e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
        }
        private static void SetBorderColor(VisualElement e, Color c) {
            e.style.borderTopColor = c; e.style.borderBottomColor = c;
            e.style.borderLeftColor = c; e.style.borderRightColor = c;
        }

        private void LoadData() {
            using DBNailDesign db = new DBNailDesign();
            _allDesigns = db.collection.OrderByDescending(d => d.Id).ToList();
            UpdateFilter();
        }

        private void UpdateFilter() {
            _pageIndex = 0;
            _filteredDesigns = _allDesigns.Where(d => {
                if (!string.IsNullOrEmpty(_searchText)) {
                    bool match = d.DisplayNames.Values.Any(n => n.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!match) return false;
                }
                bool isInstalled = INailProcessor.IsInstalledDesign(d.DesignName);
                if (!_showImported && isInstalled) return false;
                if (!_showNotImported && !isInstalled) return false;
                if (_activeTags.Count > 0) {
                    if (d.Tag == null) return false;
                    if (_activeTags.Any(t => !d.Tag.Contains(t))) return false;
                }
                if (_activeColors.Count > 0) {
                    if (d.TagColor == null) return false;
                    if (_activeColors.Any(c => !d.TagColor.Contains(c))) return false;
                }
                return true;
            }).ToList();

            _filteredDesigns.Sort((a, b) => {
                if (_favPriority) {
                    bool favA = IsFavorite(a.DesignName), favB = IsFavorite(b.DesignName);
                    if (favA != favB) return favB.CompareTo(favA);
                }
                if (_sortDropdown.index == 2) {
                    int cA = GlobalSetting.DesignUseCount.GetValueOrDefault(a.DesignName, 0);
                    int cB = GlobalSetting.DesignUseCount.GetValueOrDefault(b.DesignName, 0);
                    if (cA != cB) return cB.CompareTo(cA);
                }
                if (_sortDropdown.index == 0) return b.Id.CompareTo(a.Id);
                return string.Compare(a.DesignName, b.DesignName, StringComparison.Ordinal);
            });

            RebuildGrid();
        }

        private void ChangePage(int delta) {
            int maxPage = Mathf.Max(Mathf.CeilToInt((float)_filteredDesigns.Count / ItemsPerPage), 1);
            int newIndex = _pageIndex + delta;
            if (newIndex >= 0 && newIndex < maxPage) { _pageIndex = newIndex; RebuildGrid(); }
        }

        private void RebuildGrid() {
            _nailGrid.Clear();
            int total   = _filteredDesigns.Count;
            int maxPage = Mathf.Max(Mathf.CeilToInt((float)total / ItemsPerPage), 1);
            _pageLabel.text = $"{_pageIndex + 1} / {maxPage}";
            int start = _pageIndex * ItemsPerPage;
            int end   = Mathf.Min(start + ItemsPerPage, total);
            for (int i = start; i < end; i++) _nailGrid.Add(CreateCard(_filteredDesigns[i]));
        }

        private VisualElement CreateCard(NailDesign design) {
            bool isInstalled = INailProcessor.IsInstalledDesign(design.DesignName);

            var card = new VisualElement();
            card.AddToClassList("mdn-nail-card");
            if (!isInstalled) card.AddToClassList("mdn-nail-card--disabled");
            card.style.position = Position.Relative;

            var thumb = new Image();
            thumb.AddToClassList("mdn-nail-thumb");
            if (!string.IsNullOrEmpty(design.ThumbnailGUID)) {
                string path = AssetDatabase.GUIDToAssetPath(design.ThumbnailGUID);
                thumb.image = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            if (isInstalled) thumb.RegisterCallback<ClickEvent>(_ => SelectDesign(design.DesignName));
            card.Add(thumb);

            var nameRow = new VisualElement();
            nameRow.AddToClassList("mdn-nail-name-row");

            string currentLang = LanguageManager.CurrentLanguageData?.language ?? "jp";
            string displayName = design.DisplayNames.GetValueOrDefault(currentLang, design.DesignName);

            var nameLbl = new Label(displayName);
            nameLbl.AddToClassList("mdn-nail-name");
            nameRow.Add(nameLbl);

            var favLbl = new Label(IsFavorite(design.DesignName) ? "♥" : "♡");
            favLbl.AddToClassList("mdn-nail-fav");
            favLbl.RegisterCallback<ClickEvent>(_ => {
                ToggleFavorite(design.DesignName);
                favLbl.text = IsFavorite(design.DesignName) ? "♥" : "♡";
                if (_favPriority) UpdateFilter();
            });
            nameRow.Add(favLbl);
            card.Add(nameRow);

            string linkText = LanguageManager.S("window.to_booth_page") ?? "Booth Page";
            var linkLbl = new Label(linkText);
            linkLbl.AddToClassList("mdn-nail-link");
            linkLbl.RegisterCallback<ClickEvent>(_ => {
                if (!string.IsNullOrEmpty(design.Url)) Application.OpenURL(design.Url);
            });
            card.Add(linkLbl);

            int count = GlobalSetting.DesignUseCount.GetValueOrDefault(design.DesignName, 0);
            var badge = new Label($"{count}");
            badge.AddToClassList("mdn-nail-badge");
            card.Add(badge);

            return card;
        }

        private void SelectDesign(string designName) => _parentWindow?.SelectNailFromSearch(designName);

        private string FavKey(string name) => $"MDNail_Fav_{name}";
        private bool IsFavorite(string name) => EditorPrefs.GetBool(FavKey(name), false);
        private void ToggleFavorite(string name) => EditorPrefs.SetBool(FavKey(name), !IsFavorite(name));
    }
}
