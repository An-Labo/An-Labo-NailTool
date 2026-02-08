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

        private static readonly Vector2 FixedWindowSize = new Vector2(850, 950);

        private Color _windowBgColor;
        private Color _panelBgColor; 
        private Color _cardBgColor;
        private Color _textColor;
        private Color _linkColor;
        private Color _borderColor;
        private Color _highlightColor;

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
            window.minSize = FixedWindowSize;
            window.maxSize = FixedWindowSize;
            window.ShowAuxWindow();
        }

        private void CreateGUI() {
            _allToggles.Clear();
            _colorButtons.Clear();
            _favPriority = EditorPrefs.GetBool(PrefFavPriority, false);
            _showImported = true;
            _showNotImported = true;

            bool isDark = EditorGUIUtility.isProSkin;
            if (isDark) {
                _windowBgColor = new Color(0.22f, 0.22f, 0.22f);
                _panelBgColor = new Color(0.26f, 0.26f, 0.26f);
                _cardBgColor = new Color(0.28f, 0.28f, 0.28f);
                _textColor = new Color(0.9f, 0.9f, 0.9f);
                _linkColor = new Color(0.4f, 0.7f, 1f);
                _borderColor = new Color(0.15f, 0.15f, 0.15f);
                _highlightColor = new Color(1f, 0.85f, 0.3f); // Gold
            } else {
                _windowBgColor = new Color(0.76f, 0.76f, 0.76f);
                _panelBgColor = new Color(0.65f, 0.65f, 0.65f);
                _cardBgColor = new Color(0.9f, 0.9f, 0.9f);
                _textColor = Color.black;
                _linkColor = new Color(0.1f, 0.1f, 0.8f);
                _borderColor = Color.gray;
                _highlightColor = new Color(0.2f, 0.5f, 0.9f); // Blue
            }

            var root = rootVisualElement;
            root.style.backgroundColor = _windowBgColor;
            root.style.paddingTop = 8; root.style.paddingBottom = 8;
            root.style.paddingLeft = 8; root.style.paddingRight = 8;

            var topBar = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4, height = 22 } };
            
            _searchField = new ToolbarSearchField { style = { flexGrow = 1, marginRight = 4 } };
            _searchField.RegisterValueChangedCallback(evt => { _searchText = evt.newValue; UpdateFilter(); });
            topBar.Add(_searchField);

            string sortNewest = LanguageManager.S("window.sort.newest") ?? "Newest";
            string sortName = LanguageManager.S("window.sort.name") ?? "Name";
            string sortUsage = LanguageManager.S("window.sort.usage") ?? "Usage Count";
            _sortDropdown = new DropdownField(new List<string> { sortNewest, sortName, sortUsage }, 0);
            _sortDropdown.style.width = 85;
            _sortDropdown.style.marginLeft = 0;
            _sortDropdown.RegisterValueChangedCallback(_ => UpdateFilter());
            topBar.Add(_sortDropdown);

            var resetBtn = new Button(ResetFilters) { text = "Reset", style = { width = 55, marginLeft = 4 } };
            topBar.Add(resetBtn);
            root.Add(topBar);


            var filterPanel = new VisualElement { 
                style = { 
                    flexDirection = FlexDirection.Row,
                    backgroundColor = _panelBgColor,
                    paddingTop = 10, paddingBottom = 10, paddingLeft = 10, paddingRight = 10,
                    borderBottomLeftRadius = 6, borderBottomRightRadius = 6,
                    marginBottom = 12
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
            string favText = LanguageManager.S("window.favorite_priority") ?? "Fav Priority";
            _favToggle = AddCustomCheckbox(col1, favText, v => {
                _favPriority = v;
                EditorPrefs.SetBool(PrefFavPriority, v);
            });
            _favToggle.value = _favPriority;
            
            AddTagToColumn(col1, "tag.cute");
            AddTagToColumn(col1, "tag.cool");
            textFilterContainer.Add(col1);

            var col2 = CreateColumn();
            string importedText = LanguageManager.S("window.show_imported") ?? "Imported";
            _importedToggle = AddCustomCheckbox(col2, importedText, v => _showImported = v);
            _importedToggle.value = _showImported;

            AddTagToColumn(col2, "tag.nuance");
            AddTagToColumn(col2, "tag.pop");
            textFilterContainer.Add(col2);

            var col3 = CreateColumn();
            string notImportedText = LanguageManager.S("window.show_not_imported") ?? "Not Imported";
            _notImportedToggle = AddCustomCheckbox(col3, notImportedText, v => _showNotImported = v);
            _notImportedToggle.value = _showNotImported;

            AddTagToColumn(col3, "tag.simple");
            AddTagToColumn(col3, "tag.flashy");
            textFilterContainer.Add(col3);

            var col4 = CreateColumn();
            col4.Add(new VisualElement { style = { height = 20, marginBottom = 2 } }); 
            AddTagToColumn(col4, "tag.elegance");
            textFilterContainer.Add(col4);

            filterPanel.Add(new VisualElement { style = { width = 1, backgroundColor = Color.gray, marginRight = 10 } });

            var colorContainer = new VisualElement { style = { width = 130 } }; 
            filterPanel.Add(colorContainer);

            var rowColor1 = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 5 } };
            foreach (var colorKey in _monoColors) AddColorButton(rowColor1, colorKey);
            colorContainer.Add(rowColor1);

            var rowColor2 = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 5 } };
            foreach (var colorKey in _colorsRow2) AddColorButton(rowColor2, colorKey);
            colorContainer.Add(rowColor2);

            var rowColor3 = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 0 } };
            foreach (var colorKey in _colorsRow3) AddColorButton(rowColor3, colorKey);
            colorContainer.Add(rowColor3);

            var scroll = new ScrollView { style = { flexGrow = 1 } };
            _nailGrid = new VisualElement { 
                style = { 
                    flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, justifyContent = Justify.Center
                } 
            };
            scroll.Add(_nailGrid);
            root.Add(scroll);

            var footer = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.Center, marginTop = 12, marginBottom = 6 } };
            var prevBtn = new Button(() => ChangePage(-1)) { 
                text = "◀", 
                style = { width = 40, height = 40, paddingLeft = 1, paddingRight = 1, fontSize = 16 } 
            };
            
            var nextBtn = new Button(() => ChangePage(1)) { 
                text = "▶", 
                style = { width = 40, height = 40, paddingLeft = 1, paddingRight = 1, fontSize = 16 } 
            };

            _pageLabel = new Label("1 / 1") { 
                style = { alignSelf = Align.Center, marginLeft = 12, marginRight = 12, color = _textColor, fontSize = 14 } 
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
                    width = 26, height = 26,
                    borderTopLeftRadius = 13, borderTopRightRadius = 13, borderBottomLeftRadius = 13, borderBottomRightRadius = 13,
                    backgroundColor = colorValue, 
                    marginRight = 4, marginBottom = 0,
                    borderTopWidth = 2, borderBottomWidth = 2, borderLeftWidth = 2, borderRightWidth = 2,
                    borderTopColor = _borderColor, borderBottomColor = _borderColor, borderLeftColor = _borderColor, borderRightColor = _borderColor
                }
            };
            btn.clicked += () => {
                if (_activeColors.Contains(colorKey)) {
                    _activeColors.Remove(colorKey);
                    SetBorderWidth(btn, 2);
                    SetBorderColor(btn, _borderColor);
                    btn.transform.scale = Vector3.one;
                } else {
                    _activeColors.Add(colorKey);
                    SetBorderWidth(btn, 4);
                    SetBorderColor(btn, _highlightColor);
                    btn.transform.scale = new Vector3(1.15f, 1.15f, 1);
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
            _showImported = true;
            _showNotImported = true;
            _activeTags.Clear();
            _activeColors.Clear();

            if (_favToggle != null) _favToggle.SetValueWithoutNotify(false);
            if (_importedToggle != null) _importedToggle.SetValueWithoutNotify(true);
            if (_notImportedToggle != null) _notImportedToggle.SetValueWithoutNotify(true);

            foreach (var t in _allToggles) t.SetValueWithoutNotify(false);
            foreach (var btn in _colorButtons) {
                SetBorderWidth(btn, 2);
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
                    bool favA = IsFavorite(a.DesignName);
                    bool favB = IsFavorite(b.DesignName);
                    if (favA != favB) return favB.CompareTo(favA);
                }

                if (_sortDropdown.index == 2) {
                    int countA = GlobalSetting.DesignUseCount.GetValueOrDefault(a.DesignName, 0);
                    int countB = GlobalSetting.DesignUseCount.GetValueOrDefault(b.DesignName, 0);
                    if (countA != countB) return countB.CompareTo(countA);
                }

                if (_sortDropdown.index == 0) return b.Id.CompareTo(a.Id);
                return string.Compare(a.DesignName, b.DesignName, StringComparison.Ordinal);
            });

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
                    width = 165, height = 220, 
                    marginTop = 8, marginBottom = 8, marginLeft = 4, marginRight = 4,
                    paddingTop = 8, paddingBottom = 8, paddingLeft = 8, paddingRight = 8,
                    backgroundColor = _cardBgColor,
                    borderTopLeftRadius = 8, borderTopRightRadius = 8, borderBottomLeftRadius = 8, borderBottomRightRadius = 8,
                    opacity = isInstalled ? 1.0f : 0.5f 
                }
            };
            SetBorderWidth(card, 1);
            SetBorderColor(card, new Color(_borderColor.r, _borderColor.g, _borderColor.b, 0.3f));

            var thumb = new Image { 
                style = { 
                    width = 150, height = 150, alignSelf = Align.Center, marginBottom = 6,
                    borderTopLeftRadius = 6, borderTopRightRadius = 6, borderBottomLeftRadius = 6, borderBottomRightRadius = 6
                } 
            };
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
                if (_favPriority) UpdateFilter();
            });
            rowName.Add(favBtn);
            card.Add(rowName);

            int count = GlobalSetting.DesignUseCount.GetValueOrDefault(design.DesignName, 0);
            var badge = new Label($"{count}") {
                style = {
                    position = Position.Absolute, 
                    bottom = 2,
                    right = 2,
                    fontSize = 10, 
                    backgroundColor = new Color(0, 0, 0, 0.6f),
                    color = Color.white, 
                    paddingLeft = 4, 
                    paddingRight = 4,
                    borderTopLeftRadius = 4, 
                    borderBottomRightRadius = 0 
                }
            };
            card.Add(badge);    

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