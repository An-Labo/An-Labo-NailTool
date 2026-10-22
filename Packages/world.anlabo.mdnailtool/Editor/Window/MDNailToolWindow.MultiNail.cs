#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using world.anlabo.mdnailtool.Editor.Core;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Language;
using world.anlabo.mdnailtool.Editor.Model;
using world.anlabo.mdnailtool.Editor.NailDesigns;
using world.anlabo.mdnailtool.Editor.Window.Domain;
using static world.anlabo.mdnailtool.Editor.Language.LanguageManager;

namespace world.anlabo.mdnailtool.Editor.Window
{
	public partial class MDNailToolWindow
	{
		// 他ギミックと衝突しないよう An-Labo 固有の名前に固定する。
		internal const string MultiNailParameter = "AnLabo/NailSwitch";

		// 1デザイン内で選べる種類。バリアント (月別等) と素材違いを1つの選択肢にまとめる。
		private sealed class MultiNailVariant
		{
			internal string DesignName = "";
			internal string MaterialName = "";
			internal string Label = "";
			internal List<string> Colors = new();
		}

		// 選んだ色1つ = VRChatのメニュー1項目。
		private sealed class MultiNailPick
		{
			internal int Id;
			internal int VariantIndex;
			internal string ColorName = "";
		}

		// 1サムネイル = 1行。行の中で色を複数選べる。
		private sealed class MultiNailRow
		{
			internal int Id;
			internal string GroupName = "";
			internal string GroupDisplayName = "";
			internal List<MultiNailVariant> Variants = new();
			internal int VariantIndex;
			internal readonly List<MultiNailPick> Picks = new();
			internal MultiNailVariant Variant => this.Variants[this.VariantIndex];
		}

		// Id は全行共通の連番なので、Id 順 = 行をまたいで押した順になる。
		private IEnumerable<(MultiNailRow row, MultiNailPick pick)> MultiNailEntries =>
			this._multiNailRows.SelectMany(r => r.Picks.Select(p => (r, p))).OrderBy(e => e.p.Id);

		private Toggle? _multiNailToggle;
		private VisualElement? _multiNailRow;
		private VisualElement? _multiNailPanel;
		private VisualElement? _multiNailList;
		private DropdownField? _multiNailStart;
		private readonly List<MultiNailRow> _multiNailRows = new();
		private int _multiNailNextId = 1;
		private int _multiNailStartId;
		private int _multiNailPreviewPickId;

		private bool IsMultiNailMode =>
			GlobalSetting.EnableBetaFeatures && this._multiNailToggle?.value == true;

		private void BindMultiNailUI()
		{
			VisualElement? anchor = this._nailDesignSelect;
			VisualElement? body = anchor?.parent;
			if (anchor == null || body == null || this._multiNailRow != null) return;

			this._multiNailRow = new VisualElement { name = "multi-nail-row" };

			VisualElement toggleRow = new();
			toggleRow.AddToClassList("mdn-option-row");
			Toggle multiToggle = new() { name = "enable-multi-nail" };
			this._multiNailToggle = multiToggle;
			multiToggle.AddToClassList("mdn-toggle");
			Label toggleLabel = new(S("window.multi_nail.enable") ?? "Register multiple nails as a switch (beta)");
			toggleLabel.AddToClassList("mdn-option-label");
			toggleLabel.AddToClassList("mdn-option-clickable");
			toggleLabel.RegisterCallback<ClickEvent>(_ => multiToggle.value = !multiToggle.value);
			toggleRow.Add(multiToggle);
			toggleRow.Add(toggleLabel);
			this._multiNailRow.Add(toggleRow);

			this._multiNailPanel = new VisualElement();
			this._multiNailPanel.style.marginLeft = 4;
			this._multiNailPanel.style.marginTop = 4;
			this._multiNailPanel.style.flexDirection = FlexDirection.Row;

			VisualElement left = new();
			left.style.flexGrow = 1;
			left.style.flexShrink = 1;
			this._multiNailPanel.Add(left);

			Label hint = new(S("window.multi_nail.hint") ?? "Select the thumbnails of the nails you want to wear.");
			hint.style.whiteSpace = WhiteSpace.Normal;
			hint.style.opacity = 0.8f;
			left.Add(hint);

			this._multiNailList = new VisualElement();
			this._multiNailList.style.marginTop = 4;
			this._multiNailList.style.marginBottom = 4;
			left.Add(this._multiNailList);

			DropdownField startField = new(S("window.multi_nail.start") ?? "Nail worn from the start");
			this._multiNailStart = startField;
			startField.RegisterValueChangedCallback(_ =>
			{
				int index = startField.index;
				var entries = this.MultiNailEntries.ToList();
				this._multiNailStartId = index <= 0 || index > entries.Count ? 0 : entries[index - 1].pick.Id;
				this.RefreshMultiNailMenuPreview();
			});
			left.Add(startField);

			this._multiNailPanel.Add(this.CreateMultiNailMenuPreview());

			this._multiNailRow.Add(this._multiNailPanel);
			body.Insert(body.IndexOf(anchor) + 1, this._multiNailRow);

			multiToggle.RegisterValueChangedCallback(evt =>
			{
#if MD_NAIL_FOR_MA
				if (evt.newValue) this.ForceMultiNailMenuSettings();
#endif
				this.UpdateMultiNailVisibility();
				// 戻した時は残っている選択の最新を映し直す。
				this._multiNailPreviewPickId = 0;
				this.ShowLatestMultiNailPick();
				// 直接指定マテリアルの使用有無も切り替わるので、選択が無くても描き直す。
				this.UpdatePreview();
				this.RequestScenePreviewUpdate();
			});
			this.UpdateMultiNailVisibility();
		}

		// 切替はMAのメニュー項目で動くため、メニュー生成なしでは全ネイルが常時表示になる。
		private void ForceMultiNailMenuSettings()
		{
			if (this._forModularAvatar != null) this._forModularAvatar.value = true;
			if (this._generateExpressionMenu != null) this._generateExpressionMenu.value = true;
		}

		private const int MultiNailMenuPreviewSize = 220;
		// VRChatの1ページに置ける項目数 (VRCExpressionsMenu.MAX_CONTROLS)。超えると MA が More に分けるため、選択数の上限にする。
		private const int MultiNailMenuPageSize = 8;

		private VisualElement? _multiNailMenuWheel;
		private List<string> _multiNailMenuItems = new();
		private int _multiNailMenuStartIndex = -1;
		private List<bool> _multiNailMenuRegistered = new();

		private VisualElement CreateMultiNailMenuPreview()
		{
			VisualElement box = new();
			box.style.width = MultiNailMenuPreviewSize;
			box.style.flexShrink = 0;
			box.style.marginLeft = 12;
			box.style.alignItems = Align.Center;

			Label caption = new(S("window.multi_nail.preview") ?? "How it looks in VRChat (image only)");
			caption.style.opacity = 0.8f;
			caption.style.marginBottom = 4;
			box.Add(caption);

			this._multiNailMenuWheel = new VisualElement();
			this._multiNailMenuWheel.style.width = MultiNailMenuPreviewSize;
			this._multiNailMenuWheel.style.height = MultiNailMenuPreviewSize;
			this._multiNailMenuWheel.generateVisualContent += this.DrawMultiNailMenuWheel;
			box.Add(this._multiNailMenuWheel);
			return box;
		}

		// 戻る(上中央) + 項目。VRChatの見た目に寄せた簡易表示。
		private void RefreshMultiNailMenuPreview()
		{
			if (this._multiNailMenuWheel == null) return;
			List<(string label, bool registered, int pickId)> controls = this.BuildMultiNailMenuControls();
			// 1ページを超えた時は、MA と同じく7個 + More で見せる。
			if (controls.Count > MultiNailMenuPageSize)
			{
				controls = controls.Take(MultiNailMenuPageSize - 1).ToList();
				controls.Add((S("window.multi_nail.preview_more") ?? "More", false, 0));
			}

			var items = new List<string> { S("window.multi_nail.preview_back") ?? "Back" };
			items.AddRange(controls.Select(c => c.label));
			this._multiNailMenuItems = items;
			this._multiNailMenuRegistered = new List<bool> { false };
			this._multiNailMenuRegistered.AddRange(controls.Select(c => c.registered));
			int startControl = this._multiNailStartId == 0 ? -1 : controls.FindIndex(c => c.pickId == this._multiNailStartId);
			this._multiNailMenuStartIndex = startControl >= 0 ? startControl + 1 : -1;

			this._multiNailMenuWheel.Clear();
			float center = MultiNailMenuPreviewSize / 2f;
			float labelRadius = center * 0.68f;
			for (int i = 0; i < items.Count; i++)
			{
				float angle = (-90f + 360f * i / items.Count) * Mathf.Deg2Rad;
				Label label = new(items[i]);
				label.pickingMode = PickingMode.Ignore;
				label.style.position = Position.Absolute;
				label.style.width = 64;
				label.style.left = center + Mathf.Cos(angle) * labelRadius - 32;
				label.style.top = center + Mathf.Sin(angle) * labelRadius - 12;
				label.style.fontSize = 9;
				label.style.whiteSpace = WhiteSpace.Normal;
				label.style.unityTextAlign = TextAnchor.MiddleCenter;
				// 登録済みの分は今回の選択と区別できるよう薄く出す。
				bool registered = this._multiNailMenuRegistered[i];
				label.style.color = registered ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
				if (i == this._multiNailMenuStartIndex) label.style.unityFontStyleAndWeight = FontStyle.Bold;
				this._multiNailMenuWheel.Add(label);
			}
			this._multiNailMenuWheel.MarkDirtyRepaint();
		}

		// 着用後の An-Labo サブメニューを並び順どおりに組み立てる。登録処理と同じく、
		// 「現在のネイルを削除」で消える分を除き、同名のネイルはその位置で置き換え、残りの新規は末尾に足す。
		private List<(string label, bool registered, int pickId)> BuildMultiNailMenuControls()
		{
			var entries = this.MultiNailEntries.ToList();
			var selections = this.GetMultiNailSelections();
			var pending = new Dictionary<string, int>();
			for (int i = 0; i < selections.Count; i++)
			{
				var s = selections[i];
				string name = BuildMultiNailLabel(selections, i)
					?? (string.IsNullOrEmpty(s.colorName) ? s.designName : $"{s.designName}_{s.colorName}");
				pending[$"[An-Labo]{name}"] = i;
			}
			string PickText(int i)
			{
				string detail = MultiNailPickLabel(entries[i].row, entries[i].pick);
				return string.IsNullOrEmpty(detail) ? entries[i].row.GroupDisplayName : $"{entries[i].row.GroupDisplayName}\n{detail}";
			}

			var controls = new List<(string label, bool registered, int pickId)>();
			var placed = new HashSet<int>();
#if MD_NAIL_FOR_MA
			if (this._avatarObjectField?.value is VRCAvatarDescriptor avatar)
			{
				bool removeCurrent = this._removeCurrentNail?.value == true;
				foreach (var e in NailSetupProcessor.FindAnLaboMenuEntries(avatar))
				{
					if (removeCurrent && e.removable) continue;
					if (e.replaceable && pending.TryGetValue(e.rootName, out int index) && placed.Add(index))
						controls.Add((PickText(index), false, entries[index].pick.Id));
					else
						controls.Add((e.label, true, 0));
				}
			}
#endif
			for (int i = 0; i < entries.Count; i++)
			{
				if (!placed.Contains(i)) controls.Add((PickText(i), false, entries[i].pick.Id));
			}
			return controls;
		}

		private void DrawMultiNailMenuWheel(MeshGenerationContext context)
		{
			Painter2D painter = context.painter2D;
			float c = MultiNailMenuPreviewSize / 2f;
			Vector2 center = new(c, c);
			float outer = c - 2;
			float inner = c * 0.32f;
			int count = System.Math.Max(1, this._multiNailMenuItems.Count);
			float step = 360f / count;

			for (int i = 0; i < count; i++)
			{
				float from = -90f + step * i - step / 2f;
				float to = from + step;
				painter.fillColor = i == this._multiNailMenuStartIndex
					? new Color(0.24f, 0.62f, 1f, 0.85f)
					: i == 0 ? new Color(0.18f, 0.18f, 0.2f, 0.95f) : new Color(0.1f, 0.1f, 0.12f, 0.9f);
				painter.BeginPath();
				painter.Arc(center, outer, Angle.Degrees(from), Angle.Degrees(to));
				painter.Arc(center, inner, Angle.Degrees(to), Angle.Degrees(from), ArcDirection.CounterClockwise);
				painter.ClosePath();
				painter.Fill();
			}

			painter.strokeColor = new Color(0.45f, 0.45f, 0.5f, 1f);
			painter.lineWidth = 1.5f;
			if (count > 1)
			{
				for (int i = 0; i < count; i++)
				{
					float a = (-90f + step * i - step / 2f) * Mathf.Deg2Rad;
					painter.BeginPath();
					painter.MoveTo(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * inner);
					painter.LineTo(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * outer);
					painter.Stroke();
				}
			}
			painter.BeginPath();
			painter.Arc(center, outer, Angle.Degrees(0), Angle.Degrees(360));
			painter.Stroke();
			painter.BeginPath();
			painter.Arc(center, inner, Angle.Degrees(0), Angle.Degrees(360));
			painter.Stroke();
		}

		private void UpdateMultiNailVisibility()
		{
			if (this._multiNailRow == null) return;
			this._multiNailRow.style.display = GlobalSetting.EnableBetaFeatures ? DisplayStyle.Flex : DisplayStyle.None;
			if (this._multiNailPanel != null)
				this._multiNailPanel.style.display = this.IsMultiNailMode ? DisplayStyle.Flex : DisplayStyle.None;
			this.RefreshMultiNailRows();
		}

		// サムネイルのクリック先。まとめて登録中は着用・プレビューを変えず、リストへの追加/解除だけを切り替える。
		// 1サムネイルにつき1行。もう一度押すと外れる。
		private bool TryAddMultiNail(string designName)
		{
			if (!this.IsMultiNailMode) return false;

			using DBNailDesign db = new();
			NailDesign? design = db.FindNailDesignByDesignName(designName);
			if (design == null) return true;
			string groupName = string.IsNullOrEmpty(design.ParentVariant) ? design.DesignName : design.ParentVariant!;
			NailDesign? group = db.FindNailDesignByDesignName(groupName) ?? design;

			MultiNailRow? existing = this._multiNailRows.FirstOrDefault(r => r.GroupName == group.DesignName);
			if (existing != null)
			{
				this.RemoveMultiNailRow(existing.Id);
				return true;
			}

			string lang = LanguageManager.CurrentLanguageData.language;
			string DisplayOf(NailDesign d) => d.DisplayNames?.GetValueOrDefault(lang, d.DesignName) ?? d.DesignName;

			var members = new List<NailDesign> { group };
			members.AddRange(db.FindChildVariants(group.DesignName));
			bool hasChildren = members.Count > 1;

			var variants = new List<MultiNailVariant>();
			foreach (NailDesign member in members.Where(m => INailProcessor.IsInstalledDesign(m.DesignName)))
			{
				ResourceAutoExtractor.EnsureDesignExtracted(member.DesignName);
				INailProcessor processor = INailProcessor.CreateNailDesign(member.DesignName);
				List<string> materials = member.MaterialVariation == null
					? new List<string> { "" }
					: member.MaterialVariation.Values
						.Select(m => m.MaterialName)
						.Where(processor.IsInstalledMaterialVariation)
						.ToList();
				foreach (string material in materials)
				{
					List<string> colors = member.ColorVariation.Values
						.Select(c => c.ColorName)
						.Where(c => processor.IsInstalledColorVariation(material, c))
						.ToList();
					if (colors.Count == 0) continue;
					string label = hasChildren ? DisplayOf(member) : material;
					if (hasChildren && materials.Count > 1 && !string.IsNullOrEmpty(material)) label += $" / {material}";
					variants.Add(new MultiNailVariant { DesignName = member.DesignName, MaterialName = material, Label = label, Colors = colors });
				}
			}
			if (variants.Count == 0) return true;

			var row = new MultiNailRow
			{
				Id = this._multiNailNextId++,
				GroupName = group.DesignName,
				GroupDisplayName = DisplayOf(group),
				Variants = variants,
				VariantIndex = System.Math.Max(0, variants.FindIndex(v => v.DesignName == design.DesignName)),
			};
			this._multiNailRows.Add(row);
			this.RefreshMultiNailRows();
			return true;
		}

		private void RemoveMultiNailRow(int id)
		{
			MultiNailRow? row = this._multiNailRows.FirstOrDefault(r => r.Id == id);
			if (row == null) return;
			if (row.Picks.Any(p => p.Id == this._multiNailStartId)) this._multiNailStartId = 0;
			this._multiNailRows.Remove(row);
			this.RefreshMultiNailRows();
			this.ShowLatestMultiNailPick();
		}

		// 押すたびに選択/解除を切り替える。選んだ順にメニューへ並ぶ。
		private void ToggleMultiNailColor(MultiNailRow row, int variantIndex, string colorName)
		{
			MultiNailPick? pick = row.Picks.FirstOrDefault(p => p.VariantIndex == variantIndex && p.ColorName == colorName);
			if (pick != null)
			{
				row.Picks.Remove(pick);
				if (this._multiNailStartId == pick.Id) this._multiNailStartId = 0;
			}
			else
			{
				if (this.MultiNailEntries.Count() >= MultiNailMenuPageSize) return;
				row.Picks.Add(new MultiNailPick { Id = this._multiNailNextId++, VariantIndex = variantIndex, ColorName = colorName });
			}
			this.RefreshMultiNailRows();
			this.ShowLatestMultiNailPick();
		}

		// プレビューは最後に選んだカラーを映す。解除で最新が消えたら、残りの中で最後に選んだものへ戻す。
		private void ShowLatestMultiNailPick()
		{
			(MultiNailRow row, MultiNailPick pick) latest = this.IsMultiNailMode ? this.MultiNailEntries.LastOrDefault() : default;
			if (latest.pick == null || latest.pick.Id == this._multiNailPreviewPickId) return;
			this._multiNailPreviewPickId = latest.pick.Id;
			MultiNailVariant variant = latest.row.Variants[latest.pick.VariantIndex];
			this.ShowNailDesign(variant.DesignName, variant.MaterialName, latest.pick.ColorName);
		}

		private void RefreshMultiNailRows()
		{
			if (this._multiNailList == null) return;
			this._multiNailList.Clear();

			if (this._multiNailRows.Count == 0)
			{
				Label empty = new(S("window.multi_nail.empty") ?? "No nails added yet.");
				empty.style.opacity = 0.7f;
				this._multiNailList.Add(empty);
			}

			var order = this.MultiNailEntries.Select((e, i) => (e.pick.Id, i + 1)).ToDictionary(x => x.Id, x => x.Item2);
			// 1回にまとめて登録する数をVRChatの1ページ分で止める。登録済みの分と合わせて超えた時は MA が More に分ける。
			bool full = order.Count >= MultiNailMenuPageSize;
			if (full)
			{
				Label limit = new(string.Format(S("window.multi_nail.limit") ?? "You can select up to {0}.", MultiNailMenuPageSize));
				limit.style.color = new Color(1f, 0.75f, 0.3f);
				limit.style.marginBottom = 4;
				this._multiNailList.Add(limit);
			}
			using DBNailDesign db = new();
			foreach (MultiNailRow row in this._multiNailRows)
			{
				VisualElement block = new();
				block.style.marginBottom = 6;

				VisualElement line = new();
				line.style.flexDirection = FlexDirection.Row;
				line.style.alignItems = Align.Center;

				NailDesign? design = db.FindNailDesignByDesignName(row.Variant.DesignName);
				Texture2D? thumbnail = design == null ? null : MDNailToolAssetLoader.LoadThumbnail(design.ThumbnailGUID, design.DesignName);
				Image image = new() { image = thumbnail };
				image.style.width = 40;
				image.style.height = 40;
				image.style.flexShrink = 0;
				image.style.marginRight = 6;
				line.Add(image);

				Label name = new(row.GroupDisplayName);
				name.style.flexGrow = 1;
				name.style.whiteSpace = WhiteSpace.Normal;
				line.Add(name);

				int id = row.Id;
				Button remove = new(() => this.RemoveMultiNailRow(id)) { text = "×" };
				remove.tooltip = S("window.multi_nail.remove") ?? "Remove";
				line.Add(remove);
				block.Add(line);

				// DailyNail のように種類ごとに色が1つだけのネイルは、種類名そのものをボタンにして1列に並べる。
				bool flat = row.Variants.Count == 1 || row.Variants.All(v => v.Colors.Count == 1);
				VisualElement? buttons = null;
				for (int v = 0; v < row.Variants.Count; v++)
				{
					MultiNailVariant variant = row.Variants[v];
					if (buttons == null || !flat)
					{
						if (!flat && !string.IsNullOrEmpty(variant.Label))
						{
							Label header = new(variant.Label);
							header.style.marginLeft = 46;
							header.style.marginTop = 3;
							header.style.opacity = 0.8f;
							block.Add(header);
						}
						buttons = new VisualElement();
						buttons.style.flexDirection = FlexDirection.Row;
						buttons.style.flexWrap = Wrap.Wrap;
						buttons.style.marginLeft = 46;
						block.Add(buttons);
					}

					foreach (string colorName in variant.Colors)
					{
						int variantIndex = v;
						string c = colorName;
						MultiNailPick? pick = row.Picks.FirstOrDefault(p => p.VariantIndex == variantIndex && p.ColorName == c);
						string text = flat && row.Variants.Count > 1 ? JoinMultiNailLabel(variant.Label, colorName) : colorName;
						if (string.IsNullOrEmpty(text)) text = row.GroupDisplayName;
						Button colorButton = new(() => this.ToggleMultiNailColor(row, variantIndex, c))
						{
							text = pick == null ? text : $"{order[pick.Id]}. {text}",
						};
						colorButton.style.marginTop = 2;
						if (full && pick == null) colorButton.SetEnabled(false);
						if (pick != null)
						{
							SetMultiNailHighlight(colorButton, 2);
							colorButton.style.unityFontStyleAndWeight = FontStyle.Bold;
						}
						buttons.Add(colorButton);
					}
				}

				this._multiNailList.Add(block);
			}

			this._nailDesignSelect?.SetHighlightedDesigns(this.IsMultiNailMode
				? this._multiNailRows.Select(r => r.GroupName).ToList()
				: null);

			this.RefreshMultiNailStart();
			this.RefreshMultiNailMenuPreview();
		}

		private void RefreshMultiNailStart()
		{
			if (this._multiNailStart == null) return;
			var choices = new List<string> { S("window.multi_nail.start_none") ?? "None (start without nails)" };
			var entries = this.MultiNailEntries.ToList();
			choices.AddRange(entries.Select((e, i) =>
				$"{i + 1}. {JoinMultiNailLabel(e.row.GroupDisplayName, MultiNailPickLabel(e.row, e.pick))}"));
			this._multiNailStart.choices = choices;
			int startIndex = entries.FindIndex(e => e.pick.Id == this._multiNailStartId);
			this._multiNailStart.SetValueWithoutNotify(choices[startIndex + 1]);
		}

		private List<(string designName, string materialName, string colorName, bool isStart)> GetMultiNailSelections()
		{
			return this.MultiNailEntries
				.Select(e =>
				{
					MultiNailVariant v = e.row.Variants[e.pick.VariantIndex];
					return (v.DesignName, v.MaterialName, e.pick.ColorName, e.pick.Id == this._multiNailStartId);
				})
				.ToList();
		}

		private static string JoinMultiNailLabel(params string[] parts) =>
			string.Join(" / ", parts.Where(p => !string.IsNullOrEmpty(p)));

		// 種類が1つしかないネイルは種類名を出さない。色名が空のネイル (DailyNail等) は種類名だけになる。
		private static string MultiNailPickLabel(MultiNailRow row, MultiNailPick pick) =>
			JoinMultiNailLabel(row.Variants.Count > 1 ? row.Variants[pick.VariantIndex].Label : "", pick.ColorName);

		internal static void SetMultiNailHighlight(VisualElement element, float width)
		{
			Color color = new(0.24f, 0.62f, 1f);
			element.style.borderTopWidth = width;
			element.style.borderRightWidth = width;
			element.style.borderBottomWidth = width;
			element.style.borderLeftWidth = width;
			element.style.borderTopColor = color;
			element.style.borderRightColor = color;
			element.style.borderBottomColor = color;
			element.style.borderLeftColor = color;
		}

		// 通常は従来どおり「デザイン名_色名」。素材だけ違う同色が並ぶ時だけ素材名を挟んで別オブジェクトにする。
		private static string? BuildMultiNailLabel(List<(string designName, string materialName, string colorName, bool isStart)> selections, int index)
		{
			var s = selections[index];
			bool collides = selections.Where((o, i) => i != index)
				.Any(o => o.designName == s.designName && o.colorName == s.colorName);
			if (!collides || string.IsNullOrEmpty(s.materialName)) return null;
			return string.IsNullOrEmpty(s.colorName)
				? $"{s.designName}_{s.materialName}"
				: $"{s.designName}_{s.materialName}_{s.colorName}";
		}

		// 全指に同じデザインを入れる。指ごとの詳細指定はまとめて登録では使わない。
		private static (INailProcessor, string, string)[] BuildUniformNailSelection(
			INailProcessor processor, string materialName, string colorName, bool isHandActive, bool isFootActive)
		{
			var result = new (INailProcessor, string, string)[MDNailSlot.TotalCount];
			for (int i = 0; i < MDNailSlot.TotalCount; i++)
			{
				bool active = i < MDNailSlot.FootStartIndex ? isHandActive : isFootActive;
				result[i] = active ? (processor, materialName, colorName) : (null!, "", "");
			}
			return result;
		}
	}
}
