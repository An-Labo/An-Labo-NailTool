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

		private readonly ScrollView _scroll;
		private readonly VisualElement _list;

		public NailDesignSelect() {
			this._scroll = new ScrollView(ScrollViewMode.Horizontal) {
				horizontalScrollerVisibility = ScrollerVisibility.Auto,
				verticalScrollerVisibility = ScrollerVisibility.Hidden,
				style = {
					flexGrow = 1,
					marginTop = new Length(5, LengthUnit.Pixel),
				}
			};
			this.Add(this._scroll);

			this._list = new VisualElement {
				style = {
					flexDirection = FlexDirection.Row,
				}
			};
			this._scroll.Add(this._list);

			this.Init();
		}

		public void UpdateLanguage() {
			// 旧 search button label 廃止. ヘッダ側で管理.
		}

		public void TriggerSearch() => this.OnSearchButtonClicked?.Invoke();

		public void ToggleSortMode() {
			// AvatarDropDowns と同じく GenericMenu でドロップダウン表示.
			var menu = new UnityEditor.GenericMenu();
			var current = GlobalSetting.NailDesignSort;
			void AddItem(GlobalSetting.NailDesignSortMode mode, string langKey, string fallback) {
				string label = LanguageManager.S(langKey) ?? fallback;
				menu.AddItem(new GUIContent(label), current == mode, () => {
					GlobalSetting.NailDesignSort = mode;
					this.Init();
				});
			}
			AddItem(GlobalSetting.NailDesignSortMode.UseCount, "sort_order.use_count", "着用数順");
			AddItem(GlobalSetting.NailDesignSortMode.Newest, "sort_order.newer_asc", "新着順");
			menu.ShowAsContext();
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
			bool sortByNewest = GlobalSetting.NailDesignSort == GlobalSetting.NailDesignSortMode.Newest;

			IOrderedEnumerable<NailDesign> ordered = dbNailDesign.collection
				.Where(design => string.IsNullOrEmpty(design.ParentVariant))
				.OrderByDescending(design => INailProcessor.IsInstalledDesign(design.DesignName));
			ordered = sortByNewest
				? ordered.ThenByDescending(d => d.Id)
				         .ThenByDescending(d => useCounts.GetValueOrDefault(d.DesignName, 0))
				         .ThenByDescending(d => lastUsedTime.GetValueOrDefault(d.DesignName, DateTime.MinValue))
				: ordered.ThenByDescending(d => useCounts.GetValueOrDefault(d.DesignName, 0))
				         .ThenByDescending(d => lastUsedTime.GetValueOrDefault(d.DesignName, DateTime.MinValue))
				         .ThenByDescending(d => d.Id);

			foreach (NailDesign nailDesign in ordered) {
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
						marginLeft = new Length(0, LengthUnit.Pixel),
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

				Texture2D? thumbnail = MDNailToolAssetLoader.LoadThumbnail(nailDesign.ThumbnailGUID, nailDesign.DesignName);
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
		}

		private void SelectNail(EventBase evt) {
			if (evt.target is not VisualElement element) return;
			string designName = element.name;
			if (string.IsNullOrEmpty(designName)) return;
			ResourceAutoExtractor.EnsureDesignExtracted(designName);
			this.OnSelectNail?.Invoke(designName);
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
					Shader grayShader = MDNailToolAssetLoader.LoadShader(MDNailToolDefines.GRAY_SHADER_GUID)!;
					_material = new Material(grayShader) {
						enableInstancing = true
					};
				}

				Graphics.DrawTexture(rect, this._texture, _material);
			}
		}
	}
}
