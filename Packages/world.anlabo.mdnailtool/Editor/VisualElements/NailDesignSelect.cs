using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Language;
using world.anlabo.mdnailtool.Editor.Model;
using world.anlabo.mdnailtool.Editor.NailDesigns;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.VisualElements {
	public class NailDesignSelect : VisualElement, ILocalizedElement {

		public event Action<string>? OnSelectNail;
		public event Action? OnSearchButtonClicked;
		public string? FirstDesignName { get; private set; }

		private readonly VisualElement _listView;
		private readonly VisualElement _clip;
		private readonly VisualElement _list;
		private readonly Button _leftButton;
		private readonly Button _rightButton;
		private readonly Button _searchButton;
		private Label? _searchButtonLabel;
		private Label? _pageLabel;

		private int _pageIndex;
		private int _onePageCount;
		private int _maxPageCount;
		private int _elementSize;

		public NailDesignSelect() {
			this._listView = new VisualElement {
				style = {
					width = new Length(100, LengthUnit.Percent),
				}
			};
			this.Add(this._listView);

			this._clip = new VisualElement {
				style = {
					overflow = Overflow.Hidden
				}
			};
			this._listView.Add(this._clip);

			this._list = new VisualElement {
				style = {
					flexDirection = FlexDirection.Row,
					marginTop = new Length(5, LengthUnit.Pixel),
					justifyContent = Justify.SpaceAround,
				}
			};

			this._clip.Add(this._list);

			VisualElement footer = new() {
				style = {
					flexDirection = FlexDirection.Row,
					justifyContent = Justify.Center,
					alignItems = Align.Center,
					marginTop = new Length(5, LengthUnit.Pixel),
					height = new Length(36, LengthUnit.Pixel),
					position = Position.Relative
				}
			};
			this.Add(footer);

			var arrowsContainer = new VisualElement {
				style = {
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					justifyContent = Justify.Center
				}
			};
			footer.Add(arrowsContainer);

			
			this._leftButton = new Button {
				text = "◀",
				style = {
					flexShrink = 0,
					width = new Length(38, LengthUnit.Pixel),
					height = new Length(38, LengthUnit.Pixel),
					fontSize = 16,
					unityFontStyleAndWeight = FontStyle.Normal,
					backgroundColor = new Color(0.25f, 0.25f, 0.25f),
					borderTopWidth = 0, borderBottomWidth = 0,
					borderLeftWidth = 0, borderRightWidth = 0,
					borderTopLeftRadius = 4, borderTopRightRadius = 4,
					borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
				}
			};
			this._leftButton.clicked += this.OnLeftButton;
			arrowsContainer.Add(this._leftButton);

			this._pageLabel = new Label("- / -") {
				style = {
					width = new Length(60, LengthUnit.Pixel),
					unityTextAlign = TextAnchor.MiddleCenter,
					fontSize = 11,
				}
			};
			arrowsContainer.Add(this._pageLabel);

			this._rightButton = new Button {
				text = "▶",
				style = {
					flexShrink = 0,
					width = new Length(38, LengthUnit.Pixel),
					height = new Length(38, LengthUnit.Pixel),
					fontSize = 16,
					unityFontStyleAndWeight = FontStyle.Normal,
					backgroundColor = new Color(0.25f, 0.25f, 0.25f),
					borderTopWidth = 0, borderBottomWidth = 0,
					borderLeftWidth = 0, borderRightWidth = 0,
					borderTopLeftRadius = 4, borderTopRightRadius = 4,
					borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
				}
			};
			this._rightButton.clicked += this.OnRightButton;
			arrowsContainer.Add(this._rightButton);


			this._searchButton = new Button {
				style = {
					height = new Length(30, LengthUnit.Pixel),
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					justifyContent = Justify.Center,
					paddingLeft = 10, paddingRight = 10,
					borderTopLeftRadius = 15, borderBottomLeftRadius = 15,
					borderTopRightRadius = 15, borderBottomRightRadius = 15,
					position = Position.Absolute,
					right = 0 
				}
			};

				var searchIcon = new Image {
				image = EditorGUIUtility.Load("d_Search Icon") as Texture2D, 
				style = { width = 16, height = 16, marginRight = 5 }
			};

			searchIcon.tintColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : Color.black;
			this._searchButton.Add(searchIcon);

			this._searchButtonLabel = new Label(LanguageManager.S("window.search_nail") ?? "Search Nail") {
				style = {
					unityTextAlign = TextAnchor.MiddleCenter,
					paddingTop = 0, paddingBottom = 0,
				}
			};
			this._searchButton.Add(this._searchButtonLabel);

			this._searchButton.clicked += () => OnSearchButtonClicked?.Invoke();
			footer.Add(this._searchButton);

			this.Init();
		}

		public void UpdateLanguage() {
			if (this._searchButtonLabel != null)
				this._searchButtonLabel.text = LanguageManager.S("window.search_nail") ?? "Search Nail";
		}

		public void Init() {
			foreach (VisualElement visualElement in this._list.Children().ToArray()) {
				this._list.Remove(visualElement);
			}
			using DBNailDesign dbNailDesign = new();
			Action<EventBase> selectNailAction = SelectNail;
			IReadOnlyDictionary<string, DateTime> lastUsedTime = GlobalSetting.DesignLastUsedTimes;
IReadOnlyDictionary<string, int> useCounts = GlobalSetting.DesignUseCount;
string langKey = LanguageManager.CurrentLanguageData.language;

foreach (NailDesign nailDesign in dbNailDesign.collection
         .OrderByDescending(design => INailProcessor.IsInstalledDesign(design.DesignName)) 
         .ThenByDescending(design => useCounts.GetValueOrDefault(design.DesignName, 0)) 
         .ThenByDescending(design => lastUsedTime.GetValueOrDefault(design.DesignName, DateTime.MinValue))
         .ThenByDescending(design => design.Id)) 
{
{
				VisualElement nailElement = new() {
					style = {
						marginTop = new Length(5, LengthUnit.Pixel),
						marginRight = new Length(3, LengthUnit.Pixel),
						marginLeft = new Length(3, LengthUnit.Pixel),
					}
				};
				Button thumbnailButton = new() {
					style = {
						width = new Length(60, LengthUnit.Pixel),
						height = new Length(60, LengthUnit.Pixel),
						paddingTop = new Length(0, LengthUnit.Pixel),
						paddingRight = new Length(0, LengthUnit.Pixel),
						paddingBottom = new Length(0, LengthUnit.Pixel),
						paddingLeft = new Length(0, LengthUnit.Pixel),
						borderTopRightRadius = 0,
						borderBottomRightRadius = 0,
						borderBottomLeftRadius = 0,
						borderTopLeftRadius = 0,
						backgroundColor = new Color(1, 0, 1),
						marginTop = new Length(0, LengthUnit.Pixel),
						marginRight = new Length(0, LengthUnit.Pixel),
						marginBottom = new Length(0, LengthUnit.Pixel),
						marginLeft =	new Length(0, LengthUnit.Pixel),
						flexShrink = 0
					},
					name = nailDesign.DesignName
				};
				nailElement.Add(thumbnailButton);
				string displayName = nailDesign.DisplayNames.GetValueOrDefault(langKey, nailDesign.DesignName);
				Label nailTitle = new(displayName) {
					style = {
						width = new Length(60, LengthUnit.Pixel),
						whiteSpace = WhiteSpace.Normal,
						textOverflow = TextOverflow.Ellipsis,
						overflow = Overflow.Hidden,
					}
				};
				nailTitle.AddToClassList("main-window-nail-design-name");
				nailElement.Add(nailTitle);

				this._list.Add(nailElement);
				bool isInstalled = INailProcessor.IsInstalledDesign(nailDesign.DesignName);

				if (isInstalled) {
					thumbnailButton.name = nailDesign.DesignName;
					thumbnailButton.clickable.clickedWithEventInfo += selectNailAction;
					this.FirstDesignName ??= nailDesign.DesignName;
				} else {
					thumbnailButton.SetEnabled(false);
					thumbnailButton.style.borderTopWidth = 0;
					thumbnailButton.style.borderRightWidth = 0;
					thumbnailButton.style.borderBottomWidth = 0;
					thumbnailButton.style.borderLeftWidth = 0;
				}
				
				if (string.IsNullOrEmpty(nailDesign.ThumbnailGUID)) continue;

				string thumbnailPath = AssetDatabase.GUIDToAssetPath(nailDesign.ThumbnailGUID);
				if (string.IsNullOrEmpty(thumbnailPath)) continue;

				Texture2D? thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(thumbnailPath);
				if (thumbnail == null) continue;

				if (isInstalled) {
					thumbnailButton.Add(new Image {
						image = thumbnail,
						pickingMode = PickingMode.Ignore
					});
				} else {
					thumbnailButton.Add(new GrayImage(thumbnail) {
						style = {
							width = new Length(60, LengthUnit.Pixel),
							height = new Length(60, LengthUnit.Pixel)
						},
						pickingMode = PickingMode.Ignore
					});
				}
			}

			this.CalculateListWidth();
		}
		}

		private void SelectNail(EventBase evt) {
			if (evt.target is not VisualElement element) return;
			string designName = element.name;
			if (string.IsNullOrEmpty(designName)) return;
			
			ResourceAutoExtractor.EnsureDesignExtracted(designName);
			
			this.OnSelectNail?.Invoke(designName);
		}

		private void OnLeftButton() {
			this._pageIndex--;
			if (this._pageIndex < 0) {
				this._pageIndex = this._maxPageCount;
			}

			this.CalculatePageOffset();
		}

		private void OnRightButton() {
			this._pageIndex++;

			if (this._maxPageCount < this._pageIndex) {
				this._pageIndex = 0;
			}

			this.CalculatePageOffset();
		}

		private void CalculateListWidth() {
			int count = this._list.childCount;	
			int width = (int)this._listView.contentRect.width;
			const int elementSize = 60 + 10;

			if (width < elementSize) return;

			this._onePageCount = width / elementSize;

			if (this._onePageCount == 0) return;
			
			this._maxPageCount = Mathf.Max(Mathf.CeilToInt(count / (float)this._onePageCount) - 1, 0);

			int surplus = width - this._onePageCount * elementSize;
			int margin = surplus / this._onePageCount;
			this._elementSize = elementSize + margin;

			this._list.style.width = new Length(count * this._elementSize, LengthUnit.Pixel);
			this._clip.style.width = new Length(this._onePageCount * this._elementSize, LengthUnit.Pixel);
			if (this._maxPageCount < this._pageIndex) {
				this._pageIndex = this._maxPageCount;
			}
			this.UpdatePageLabel();
		}

		private void CalculatePageOffset() {
			int offset = this._elementSize * this._pageIndex * this._onePageCount;
			this._list.style.left = new Length(-offset, LengthUnit.Pixel);
			this.UpdatePageLabel();
		}

		private void UpdatePageLabel() {
			if (this._pageLabel == null) return;
			this._pageLabel.text = $"{this._pageIndex + 1} / {this._maxPageCount + 1}";
		}

		protected override void ExecuteDefaultAction(EventBase evt) {
			base.ExecuteDefaultAction(evt);
			switch (evt) {
				case GeometryChangedEvent:
					this.CalculateListWidth();
					this.CalculatePageOffset();
					break;
			}
		}
		

		internal new class UxmlFactory : UxmlFactory<NailDesignSelect, UxmlTraits> { }
		
		internal new class UxmlTraits : VisualElement.UxmlTraits { }


		private class GrayImage : IMGUIContainer {
			private static Material? _material;

			private readonly Texture _texture;

			public GrayImage(Texture texture) {
				this._texture = texture;
				this.onGUIHandler = this.OnGUI;
				this.cullingEnabled = true;
			}

			private void OnGUI() {
				Event e = Event.current;
				if (e.type == EventType.Layout) return;
				Rect rect = this.contentRect;
				if (_material == null) {
					string shaderPath = AssetDatabase.GUIDToAssetPath(MDNailToolDefines.GRAY_SHADER_GUID);
					Shader grayShader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
					_material = new Material(grayShader) {
						enableInstancing = true
					};
				}

				Graphics.DrawTexture(rect, this._texture, _material);
			}
		}
	}
}