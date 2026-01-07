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

        // UI Elements
        private VisualElement _nailGrid;
        private Label _pageLabel;
        private ToolbarSearchField _searchField;
        private DropdownField _sortDropdown;
        
        // リセット用に保持
        private List<Toggle> _allToggles = new List<Toggle>();
        private List<Button> _colorButtons = new List<Button>();
        private Toggle _favToggle;
        private Toggle _importedToggle;
        private Toggle _notImportedToggle;

        // データ
        private List<NailDesign> _allDesigns = new();
        private List<NailDesign> _filteredDesigns = new();
        
        // フィルタ状態
        private string _searchText = "";
        private bool _favOnly = false;
        private bool _importedOnly = false;
        private bool _notImportedOnly = false;
        private readonly HashSet<string> _activeTags = new();
        private readonly HashSet<string> _activeColors = new();

        // ページネーション (4列x3行)
        private int _pageIndex = 0;
        private const int ItemsPerPage = 12;

        // ウィンドウサイズ固定
        private static readonly Vector2 FixedWindowSize = new Vector2(740, 920);

        // カラー設定
        private Color _windowBgColor;
        private Color _panelBgColor; 
        private Color _cardBgColor;
        private Color _textColor;
        private Color _linkColor;
        private Color _borderColor;

        // タグ定義
        private readonly Dictionary<string, string> _tagMap = new() {
            {"tag.cute", "cute"}, {"tag.cool", "cool"},
            {"tag.nuance", "nuance"}, {"tag.pop", "pop"},
            {"tag.simple", "simple"}, {"tag.flashy", "flashy"},
            {"tag.elegance", "elegance"}
        };

        // カラー定義
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
            window.minSize = FixedWindowSize;
            window.maxSize = FixedWindowSize;
            window.ShowAuxWindow();
        }

        private void CreateGUI() {
            _allToggles.Clear();
            _colorButtons.Clear();

            bool isDark = EditorGUIUtility.isProSkin;
            if (isDark) {
                _windowBgColor = new Color(0.22f, 0.22f, 0.22f);
                _panelBgColor = new Color(0.26f, 0.26f, 0.26f);
                _cardBgColor = new Color(0.28f, 0.28f, 0.28f);
                _textColor = new Color(0.9f, 0.9f, 0.9f);
                _linkColor = new Color(0.4f, 0.7f, 1f);
                _borderColor = new Color(0.15f, 0.15f, 0.15f);
            } else {
                _windowBgColor = new Color(0.76f, 0.76f, 0.76f);
                _panelBgColor = new Color(0.65f, 0.65f, 0.65f);
                _cardBgColor = new Color(0.9f, 0.9f, 0.9f);
                _textColor = Color.black;
                _linkColor = new Color(0.1f, 0.1f, 0.8f);
                _borderColor = Color.gray;
            }

            var root = rootVisualElement;
            root.style.backgroundColor = _windowBgColor;
            root.style.paddingTop = 8; root.style.paddingBottom = 8;
            root.style.paddingLeft = 8; root.style.paddingRight = 8;

            var topBar = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 0, height = 20 } };
            
            _searchField = new ToolbarSearchField { style = { flexGrow = 1, marginRight = 0 } };
            _searchField.RegisterValueChangedCallback(evt => { _searchText = evt.newValue; UpdateFilter(); });
            topBar.Add(_searchField);

            string sortNewest = LanguageManager.S("window.sort.newest") ?? "Newest";
            string sortName = LanguageManager.S("window.sort.name") ?? "Name";
            _sortDropdown = new DropdownField(new List<string> { sortNewest, sortName }, 0);
            _sortDropdown.style.width = 80;
            _sortDropdown.style.marginLeft = 0;
            _sortDropdown.RegisterValueChangedCallback(_ => UpdateFilter());
            topBar.Add(_sortDropdown);

            var resetBtn = new Button(ResetFilters) { text = "Reset", style = { width = 50, marginLeft = 2 } };
            topBar.Add(resetBtn);
            root.Add(topBar);


            var filterPanel = new VisualElement { 
                style = { 
                    flexDirection = FlexDirection.Row,
                    backgroundColor = _panelBgColor,
                    paddingTop = 8, paddingBottom = 8, paddingLeft = 8, paddingRight = 8,
                    borderBottomLeftRadius = 5, borderBottomRightRadius = 5,
                    marginBottom = 10
                }
            };
            root.Add(filterPanel);

            var textFilterContainer = new VisualElement { 
                style = { 
                    flexDirection = FlexDirection.Row, 
                    flexGrow = 1, 
                    marginRight = 10 
                } 
            };
            filterPanel.Add(textFilterContainer);

            var col1 = CreateColumn();
            string favText = LanguageManager.S("window.favorite_only") ?? "Favorite";
            _favToggle = AddCustomCheckbox(col1, favText, v => _favOnly = v);
            
            AddTagToColumn(col1, "tag.cute");
            AddTagToColumn(col1, "tag.cool");
            textFilterContainer.Add(col1);

            var col2 = CreateColumn();
            string importedText = LanguageManager.S("window.imported_only") ?? "Imported";
            _importedToggle = AddCustomCheckbox(col2, importedText, v => _importedOnly = v);

            AddTagToColumn(col2, "tag.nuance");
            AddTagToColumn(col2, "tag.pop");
            textFilterContainer.Add(col2);

            var col3 = CreateColumn();
            string notImportedText = LanguageManager.S("window.not_imported") ?? "Not Imported";
            _notImportedToggle = AddCustomCheckbox(col3, notImportedText, v => _notImportedOnly = v);

            AddTagToColumn(col3, "tag.simple");
            AddTagToColumn(col3, "tag.flashy");
            textFilterContainer.Add(col3);

            var col4 = CreateColumn();
            col4.Add(new VisualElement { style = { height = 20, marginBottom = 2 } }); 
            AddTagToColumn(col4, "tag.elegance");
            textFilterContainer.Add(col4);

            filterPanel.Add(new VisualElement { style = { width = 1, backgroundColor = Color.gray, marginRight = 10 } });

            var colorContainer = new VisualElement { style = { width = 100 } }; 
            filterPanel.Add(colorContainer);

            var rowColor1 = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
            foreach (var colorKey in _monoColors) AddColorButton(rowColor1, colorKey);
            colorContainer.Add(rowColor1);

            var rowColor2 = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
            foreach (var colorKey in _colorsRow2) AddColorButton(rowColor2, colorKey);
            colorContainer.Add(rowColor2);

            var rowColor3 = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 0 } };
            foreach (var colorKey in _colorsRow3) AddColorButton(rowColor3, colorKey);
            colorContainer.Add(rowColor3);

            var scroll = new ScrollView { style = { flexGrow = 1 } };
            _nailGrid = new VisualElement { 
                style = { 
                    flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, justifyContent = Justify.FlexStart, width = 700
                } 
            };
            scroll.Add(_nailGrid);
            root.Add(scroll);

            var footer = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.Center, marginTop = 10, marginBottom = 5 } };
var prevBtn = new Button(() => ChangePage(-1)) { 
                text = "◀", 
                style = { width = 36, height = 36, paddingLeft = 1, paddingRight = 1 } 
            };
            
            var nextBtn = new Button(() => ChangePage(1)) { 
                text = "▶", 
                style = { width = 36, height = 36, paddingLeft = 1, paddingRight = 1 } 
            };

            _pageLabel = new Label("1 / 1") { 
                style = { alignSelf = Align.Center, marginLeft = 10, marginRight = 10, color = _textColor } 
            };
            footer.Add(prevBtn);
            footer.Add(_pageLabel);
            footer.Add(nextBtn);
            root.Add(footer);

            LoadData();
        }

        private VisualElement CreateColumn() {
            return new VisualElement { 
                style = { 
                    flexDirection = FlexDirection.Column, 
                    marginRight = 10, 
                    width = 110 
                } 
            };
        }

        private void AddTagToColumn(VisualElement column, string key) {
            if (!_tagMap.TryGetValue(key, out string jsonTag)) return;
            string displayName = LanguageManager.S(key) ?? key.Replace("tag.", "");
            
            var t = AddCustomCheckbox(column, displayName, (val) => {
                if (val) _activeTags.Add(jsonTag); else _activeTags.Remove(jsonTag);
            });
            _allToggles.Add(t);
        }

        private void AddColorButton(VisualElement parent, string colorKey) {
            if (!_colorValueMap.TryGetValue(colorKey, out Color colorValue)) return;

            var btn = new Button {
                style = {
                    width = 20, height = 20,
                    borderTopLeftRadius = 10, borderTopRightRadius = 10, borderBottomLeftRadius = 10, borderBottomRightRadius = 10,
                    backgroundColor = colorValue, 
                    marginRight = 4, marginBottom = 0,
                    borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = _borderColor, borderBottomColor = _borderColor, borderLeftColor = _borderColor, borderRightColor = _borderColor
                }
            };
            btn.clicked += () => {
                if (_activeColors.Contains(colorKey)) {
                    _activeColors.Remove(colorKey);
                    SetBorderWidth(btn, 1);
                    SetBorderColor(btn, _borderColor);
                    btn.transform.scale = Vector3.one;
                } else {
                    _activeColors.Add(colorKey);
                    SetBorderWidth(btn, 3);
                    SetBorderColor(btn, Color.cyan);
                    btn.transform.scale = new Vector3(1.2f, 1.2f, 1);
                }
                UpdateFilter();
            };
            _colorButtons.Add(btn);
            parent.Add(btn);
        }

        private Toggle AddCustomCheckbox(VisualElement parent, string labelText, Action<bool> onToggle) {
            var container = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 2,
                }
            };

            var toggle = new Toggle {
                style = {
                    width = 18, height = 18,
                    marginRight = 0, marginLeft = 0, marginTop = 0, marginBottom = 0
                }
            };

            var label = new Label(labelText) {
                style = {
                    marginLeft = 2,
                    paddingLeft = 0,
                    color = new Color(0.9f, 0.9f, 0.9f)
                }
            };

            toggle.RegisterValueChangedCallback(evt => {
                onToggle(evt.newValue);
                UpdateFilter();
            });

            container.RegisterCallback<ClickEvent>(evt => {
                if (evt.target != toggle) {
                    toggle.value = !toggle.value;
                }
            });

            container.Add(toggle);
            container.Add(label);
            parent.Add(container);

            return toggle;
        }

        private void ResetFilters() {
            _searchText = "";
            _searchField.value = "";
            _favOnly = false;
            _importedOnly = false;
            _notImportedOnly = false;
            _activeTags.Clear();
            _activeColors.Clear();

            if (_favToggle != null) _favToggle.SetValueWithoutNotify(false);
            if (_importedToggle != null) _importedToggle.SetValueWithoutNotify(false);
            if (_notImportedToggle != null) _notImportedToggle.SetValueWithoutNotify(false);

            foreach (var t in _allToggles) t.SetValueWithoutNotify(false);
            foreach (var btn in _colorButtons) {
                SetBorderWidth(btn, 1);
                SetBorderColor(btn, _borderColor);
                btn.transform.scale = Vector3.one;
            }
            UpdateFilter();
        }

        private void SetBorderWidth(VisualElement element, float width) {
            element.style.borderTopWidth = width; element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width; element.style.borderRightWidth = width;
        }
        private void SetBorderColor(VisualElement element, Color color) {
            element.style.borderTopColor = color; element.style.borderBottomColor = color;
            element.style.borderLeftColor = color; element.style.borderRightColor = color;
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
                if (_favOnly && !IsFavorite(d.DesignName)) return false;

                bool isInstalled = INailProcessor.IsInstalledDesign(d.DesignName);
                if (_importedOnly && !isInstalled) return false;
                if (_notImportedOnly && isInstalled) return false;
                
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

            if (_sortDropdown.index == 0) _filteredDesigns.Sort((a, b) => b.Id.CompareTo(a.Id));
            else _filteredDesigns.Sort((a, b) => string.Compare(a.DesignName, b.DesignName, StringComparison.Ordinal));
            RebuildGrid();
        }

        private void ChangePage(int delta) {
            int maxPage = Mathf.CeilToInt((float)_filteredDesigns.Count / ItemsPerPage);
            if (maxPage == 0) maxPage = 1;
            int newIndex = _pageIndex + delta;
            if (newIndex >= 0 && newIndex < maxPage) {
                _pageIndex = newIndex;
                RebuildGrid();
            }
        }

        private void RebuildGrid() {
            _nailGrid.Clear();
            int total = _filteredDesigns.Count;
            int maxPage = Mathf.CeilToInt((float)total / ItemsPerPage);
            if (maxPage == 0) maxPage = 1;
            _pageLabel.text = $"{_pageIndex + 1} / {maxPage}";
            int start = _pageIndex * ItemsPerPage;
            int end = Mathf.Min(start + ItemsPerPage, total);
            for (int i = start; i < end; i++) {
                _nailGrid.Add(CreateCard(_filteredDesigns[i]));
            }
        }

        private VisualElement CreateCard(NailDesign design) {
            bool isInstalled = INailProcessor.IsInstalledDesign(design.DesignName);

            var card = new VisualElement {
                style = {
                    width = 160, height = 210, 
                    marginTop = 5, marginBottom = 5, marginLeft = 5, marginRight = 5,
                    paddingTop = 5, paddingBottom = 5, paddingLeft = 5, paddingRight = 5,
                    backgroundColor = _cardBgColor,
                    borderTopLeftRadius = 5, borderTopRightRadius = 5, borderBottomLeftRadius = 5, borderBottomRightRadius = 5,
                    opacity = isInstalled ? 1.0f : 0.5f 
                }
            };

            var thumb = new Image { style = { width = 150, height = 150, alignSelf = Align.Center, marginBottom = 5 } };
            if (!string.IsNullOrEmpty(design.ThumbnailGUID)) {
                string path = AssetDatabase.GUIDToAssetPath(design.ThumbnailGUID);
                thumb.image = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            } else {
                thumb.style.backgroundColor = Color.gray;
            }

            if (isInstalled) {
                thumb.RegisterCallback<ClickEvent>(_ => SelectDesign(design.DesignName));
            }
            card.Add(thumb);

            var rowName = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
            string currentLang = LanguageManager.CurrentLanguageData?.language ?? "jp";
            string displayName = design.DisplayNames.GetValueOrDefault(currentLang, design.DesignName);
            
            var lbl = new Label(displayName) {
                style = { 
                    fontSize = 12, unityFontStyleAndWeight = FontStyle.Bold, 
                    whiteSpace = WhiteSpace.Normal, width = 120, overflow = Overflow.Hidden,
                    height = 30, 
                    color = _textColor
                }
            };
            rowName.Add(lbl);

            var favBtn = new Label(IsFavorite(design.DesignName) ? "♥" : "♡") {
                style = { fontSize = 16, color = Color.red }
            };
            favBtn.RegisterCallback<ClickEvent>(_ => {
                ToggleFavorite(design.DesignName);
                favBtn.text = IsFavorite(design.DesignName) ? "♥" : "♡";
                if (_favOnly) UpdateFilter();
            });
            rowName.Add(favBtn);
            card.Add(rowName);

            string linkText = LanguageManager.S("window.to_booth_page") ?? "Booth Page";
            var linkLabel = new Label(linkText) {
                style = { fontSize = 11, color = _linkColor, marginTop = 2 }
            };
            linkLabel.RegisterCallback<ClickEvent>(_ => {
                if (!string.IsNullOrEmpty(design.Url)) Application.OpenURL(design.Url);
            });
            card.Add(linkLabel);

            return card;
        }

        private void SelectDesign(string designName) {
            _parentWindow?.SelectNailFromSearch(designName);
        }

        private string FavKey(string name) => $"MDNail_Fav_{name}";
        private bool IsFavorite(string name) => EditorPrefs.GetBool(FavKey(name), false);
        private void ToggleFavorite(string name) => EditorPrefs.SetBool(FavKey(name), !IsFavorite(name));
    }
}