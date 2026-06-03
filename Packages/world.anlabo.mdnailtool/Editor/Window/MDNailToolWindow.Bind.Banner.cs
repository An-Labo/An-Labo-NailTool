#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using world.anlabo.mdnailtool.Editor.Core;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Model;
using world.anlabo.mdnailtool.Editor.NailDesigns;
using world.anlabo.mdnailtool.Editor.VisualElements;
using static world.anlabo.mdnailtool.Editor.Language.LanguageManager;
using Avatar = world.anlabo.mdnailtool.Editor.Entity.Avatar;
using Object = UnityEngine.Object;
using Newtonsoft.Json;
using world.anlabo.mdnailtool.Editor;
using world.anlabo.mdnailtool.Editor.Window.Domain;
using world.anlabo.mdnailtool.Editor.Window.Controllers;

namespace world.anlabo.mdnailtool.Editor.Window
{
	public partial class MDNailToolWindow
	{
		private void BindErrorBanner()
		{
			this._errorBanner = this.rootVisualElement.Q<VisualElement>("error-banner");
			this._errorMessage = this.rootVisualElement.Q<Label>("error-message");
			this._errorDetailToggle = this.rootVisualElement.Q<Label>("error-detail-toggle");
			this._errorDetailArea = this.rootVisualElement.Q<VisualElement>("error-detail-area");
			this._errorDetailText = this.rootVisualElement.Q<Label>("error-detail-text");

			this.rootVisualElement.Q<Button>("error-close")
				?.RegisterCallback<ClickEvent>(_ => {
					this.HideErrorBanner();
					this.ClearHandFootError();
					this.ClearAvatarFieldError();
				});

			this._errorDetailToggle?.RegisterCallback<ClickEvent>(_ => this.ToggleErrorDetail());

			var copyBtn = this.rootVisualElement.Q<Button>("error-copy");
			if (copyBtn != null)
			{
				copyBtn.text = S("error.copy") ?? "Copy to Clipboard";
				copyBtn.RegisterCallback<ClickEvent>(_ =>
					GUIUtility.systemCopyBuffer = this._errorDetailText?.text ?? "");
			}

			var contactBtn = this.rootVisualElement.Q<Button>("error-contact");
			if (contactBtn != null)
			{
				contactBtn.text = S("window.contact") ?? "Contact";
				contactBtn.RegisterCallback<ClickEvent>(_ => Application.OpenURL(S("link.contact")));
			}
		}

		private void ShowErrorBanner(string? userMessage, Exception? ex = null)
		{
			if (this._errorBanner == null) return;
			this._errorBanner.style.display = DisplayStyle.Flex;
			if (this._errorMessage != null) this._errorMessage.text = userMessage ?? "";
			if (this._errorDetailText != null) this._errorDetailText.text = ex?.ToString() ?? "";
			this._errorDetailExpanded = false;
			if (this._errorDetailArea != null) this._errorDetailArea.style.display = DisplayStyle.None;
			if (this._errorDetailToggle != null)
				this._errorDetailToggle.text = S("error.show_detail") ?? "▶ Show Details";
			// Only show the detail toggle when there's exception detail to show
			if (this._errorDetailToggle != null)
				this._errorDetailToggle.style.display = ex != null ? DisplayStyle.Flex : DisplayStyle.None;
			// ScrollView内でエラーバナーが見えるようにスクロール
			var scrollView = this.rootVisualElement.Q<ScrollView>("Root");
			if (scrollView != null && this._errorBanner != null)
				this._errorBanner.schedule.Execute(() => scrollView.ScrollTo(this._errorBanner));
		}

		private void HideErrorBanner()
		{
			if (this._errorBanner != null) this._errorBanner.style.display = DisplayStyle.None;
			if (this._contactLinksArea != null) this._contactLinksArea.style.display = DisplayStyle.None;
		}

		private void ShowContactLinks(string errorText)
		{
			if (this._errorBanner == null) return;

			// 既存のcontactLinksAreaがあれば削除
			if (this._contactLinksArea != null) {
				this._contactLinksArea.RemoveFromHierarchy();
			}

			this._contactLinksArea = new VisualElement();
			this._contactLinksArea.style.marginTop = 8;

			// 問い合わせ案内テキスト
			var contactLabel = new Label(S("error.execute.contact_prompt"));
			contactLabel.style.marginBottom = 4;
			contactLabel.style.whiteSpace = WhiteSpace.Normal;
			this._contactLinksArea.Add(contactLabel);

			// ボタン行
			var buttonRow = new VisualElement();
			buttonRow.style.flexDirection = FlexDirection.Row;
			buttonRow.style.flexWrap = Wrap.Wrap;

			// エラーコピーボタン
			var copyButton = new Button(() => {
				GUIUtility.systemCopyBuffer = errorText;
			});
			copyButton.text = S("error.execute.copy_error");
			copyButton.style.marginRight = 4;
			copyButton.style.marginBottom = 4;
			copyButton.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
			copyButton.style.color = Color.white;
			buttonRow.Add(copyButton);

			// Discord (紫)
			var discordButton = new Button(() => {
				Application.OpenURL("https://discord.gg/anlabo");
			});
			discordButton.text = "Discord";
			discordButton.style.marginRight = 4;
			discordButton.style.marginBottom = 4;
			discordButton.style.backgroundColor = new Color(0.34f, 0.40f, 0.95f);
			discordButton.style.color = Color.white;
			buttonRow.Add(discordButton);

			// BOOTH (オレンジ)
			var boothButton = new Button(() => {
				Application.OpenURL("https://accounts.booth.pm/conversations/5331544/messages");
			});
			boothButton.text = "BOOTH";
			boothButton.style.marginRight = 4;
			boothButton.style.marginBottom = 4;
			boothButton.style.backgroundColor = new Color(0.82f, 0.17f, 0.20f);
			boothButton.style.color = Color.white;
			buttonRow.Add(boothButton);

			// X (黒)
			var xButton = new Button(() => {
				Application.OpenURL("https://x.com/an_labo_virtual");
			});
			xButton.text = "X";
			xButton.style.marginBottom = 4;
			xButton.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
			xButton.style.color = Color.white;
			buttonRow.Add(xButton);

			this._contactLinksArea.Add(buttonRow);
			this._errorBanner.Add(this._contactLinksArea);
		}

		private void ToggleErrorDetail()
		{
			this._errorDetailExpanded = !this._errorDetailExpanded;
			if (this._errorDetailArea != null)
				this._errorDetailArea.style.display = this._errorDetailExpanded ? DisplayStyle.Flex : DisplayStyle.None;
			if (this._errorDetailToggle != null)
				this._errorDetailToggle.text = this._errorDetailExpanded
					? (S("error.hide_detail") ?? "▼ Hide Details")
					: (S("error.show_detail") ?? "▶ Show Details");
		}

		private void BindWarningBanner()
		{
			this._warningBanner = this.rootVisualElement.Q<VisualElement>("warning-banner");
			this._warningMessage = this.rootVisualElement.Q<Label>("warning-message");
			this._warningDetailToggle = this.rootVisualElement.Q<Label>("warning-detail-toggle");
			this._warningDetailArea = this.rootVisualElement.Q<VisualElement>("warning-detail-area");
			this._warningDetailText = this.rootVisualElement.Q<Label>("warning-detail-text");
			var closeBtn = this.rootVisualElement.Q<Button>("warning-close");
			if (closeBtn != null) closeBtn.clicked += this.HideWarningBanner;
			if (this._warningDetailToggle != null)
				this._warningDetailToggle.RegisterCallback<ClickEvent>(_ => this.ToggleWarningDetail());
			var copyBtn = this.rootVisualElement.Q<Button>("warning-copy");
			if (copyBtn != null)
			{
				copyBtn.text = S("error.copy") ?? "Copy to Clipboard";
				copyBtn.RegisterCallback<ClickEvent>(_ =>
					GUIUtility.systemCopyBuffer = this._warningDetailText?.text ?? "");
			}
		}

		private void ShowWarningBanner(string summary, IReadOnlyList<string> details)
		{
			if (this._warningBanner == null) return;
			this._warningBanner.style.display = DisplayStyle.Flex;
			if (this._warningMessage != null) this._warningMessage.text = summary;
			this._warningDetailExpanded = false;
			if (this._warningDetailArea != null) this._warningDetailArea.style.display = DisplayStyle.None;
			if (this._warningDetailText != null) this._warningDetailText.text = string.Join("\n", details);
			if (this._warningDetailToggle != null)
			{
				this._warningDetailToggle.style.display = details.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
				this._warningDetailToggle.text = S("error.show_detail") ?? "▶ Show Details";
			}
			var scrollView = this.rootVisualElement.Q<ScrollView>("Root");
			if (scrollView != null && this._warningBanner != null)
				this._warningBanner.schedule.Execute(() => scrollView.ScrollTo(this._warningBanner));
		}

		private void HideWarningBanner()
		{
			if (this._warningBanner != null) this._warningBanner.style.display = DisplayStyle.None;
		}

		private void ToggleWarningDetail()
		{
			this._warningDetailExpanded = !this._warningDetailExpanded;
			if (this._warningDetailArea != null)
				this._warningDetailArea.style.display = this._warningDetailExpanded ? DisplayStyle.Flex : DisplayStyle.None;
			if (this._warningDetailToggle != null)
				this._warningDetailToggle.text = this._warningDetailExpanded
					? (S("error.hide_detail") ?? "▼ Hide Details")
					: (S("error.show_detail") ?? "▶ Show Details");
		}

		private void ShowAvatarFieldError()
		{
			this._avatarObjectField?.AddToClassList("mdn-field-error");
		}

		private void ClearAvatarFieldError()
		{
			this._avatarObjectField?.RemoveFromClassList("mdn-field-error");
		}

		private void ShowHandFootError()
		{
			this._handSectionHeader?.AddToClassList("mdn-field-error");
			this._footSectionHeader?.AddToClassList("mdn-field-error");
		}

		private void ClearHandFootError()
		{
			this._handSectionHeader?.RemoveFromClassList("mdn-field-error");
			this._footSectionHeader?.RemoveFromClassList("mdn-field-error");
		}

	}
}
