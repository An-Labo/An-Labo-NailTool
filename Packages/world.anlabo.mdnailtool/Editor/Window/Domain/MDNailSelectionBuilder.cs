using System;
using System.Collections.Generic;
using System.Linq;
using world.anlabo.mdnailtool.Editor.NailDesigns;
using world.anlabo.mdnailtool.Editor.VisualElements;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Window.Domain
{
	internal static class MDNailSelectionBuilder
	{
		internal static (INailProcessor, string, string)[] Build(
			NailDesignDropDowns[] nailDesignDropDowns,
			bool isHandActive,
			bool isHandDetail,
			bool isFootActive,
			bool isFootDetail
		)
		{
			(string d, string m, string c)[] allSelections = nailDesignDropDowns
				.Select(dropDowns => dropDowns.GetSelectedDesignAndVariationName())
				.ToArray();

			List<(string d, string m, string c)> finalSelectionList = new();
			var emptyDummy = ("", "", "");
			// HandDetail/FootDetail=false 時の全指共通デザインは ON状態の最初の指から取る。
			// 単純に [0] を使うと指0番OFFで全指消滅する。
			var globalMasterSource = allSelections.FirstOrDefault(s => !string.IsNullOrEmpty(s.d));
			if (string.IsNullOrEmpty(globalMasterSource.d)) globalMasterSource = emptyDummy;

			if (isHandActive)
			{
				for (int i = MDNailSlot.HandStartIndex; i < MDNailSlot.HandStartIndex + MDNailSlot.HandCount; i++)
				{
					finalSelectionList.Add(isHandDetail ? allSelections[i] : globalMasterSource);
				}
			}
			else
			{
				for (int i = 0; i < MDNailSlot.HandCount; i++) finalSelectionList.Add(emptyDummy);
			}

			if (isFootActive)
			{
				for (int i = MDNailSlot.FootStartIndex; i < MDNailSlot.TotalCount; i++)
				{
					finalSelectionList.Add(isFootDetail ? allSelections[i] : globalMasterSource);
				}
			}
			else
			{
				for (int i = 0; i < MDNailSlot.FootCount; i++) finalSelectionList.Add(emptyDummy);
			}

			Dictionary<string, INailProcessor> designDictionary = new();
			return finalSelectionList.Select(tuple =>
			{
				if (string.IsNullOrEmpty(tuple.d)) return (null!, "", "");
				if (designDictionary.TryGetValue(tuple.d, out INailProcessor nailDesign)) return (nailDesign, tuple.m, tuple.c);

				INailProcessor newProcessor = INailProcessor.CreateNailDesign(tuple.d);
				designDictionary.Add(tuple.d, newProcessor);
				return (newProcessor, tuple.m, tuple.c);
			}).ToArray();
		}

		internal static string?[] BuildAdditionalMaterialSources(
			NailDesignDropDowns[] nailDesignDropDowns,
			bool isHandActive,
			bool isHandDetail,
			bool isFootActive,
			bool isFootDetail,
			string? globalSource) =>
			BuildAdditionalSources(nailDesignDropDowns, dd => dd.GetSelectedAdditionalMaterialSource(),
				isHandActive, isHandDetail, isFootActive, isFootDetail, globalSource);

		// 追加オブジェクトは手のみ対応(TargetFingerは0-9)。足側 result[10..19] は呼び出し側で読まれないが配列構造の対称性を保つ。
		internal static string?[] BuildAdditionalObjectSources(
			NailDesignDropDowns[] nailDesignDropDowns,
			bool isHandActive,
			bool isHandDetail,
			bool isFootActive,
			bool isFootDetail,
			string? globalSource) =>
			BuildAdditionalSources(nailDesignDropDowns, dd => dd.GetSelectedAdditionalObjectSource(),
				isHandActive, isHandDetail, isFootActive, isFootDetail, globalSource);

		private static string?[] BuildAdditionalSources(
			NailDesignDropDowns[] nailDesignDropDowns,
			Func<NailDesignDropDowns, string?> selector,
			bool isHandActive,
			bool isHandDetail,
			bool isFootActive,
			bool isFootDetail,
			string? globalSource)
		{
			string?[] result = new string?[MDNailSlot.TotalCount];

			string?[] perFingerSources = nailDesignDropDowns
				.Select(selector)
				.ToArray();

			if (isHandActive)
			{
				string? handSource = isHandDetail ? null : perFingerSources.Take(MDNailSlot.HandCount).FirstOrDefault(s => !string.IsNullOrEmpty(s));
				for (int i = MDNailSlot.HandStartIndex; i < MDNailSlot.HandStartIndex + MDNailSlot.HandCount; i++)
				{
					result[i] = isHandDetail ? perFingerSources[i] : (handSource ?? globalSource);
				}
			}

			if (isFootActive)
			{
				for (int i = MDNailSlot.FootStartIndex; i < MDNailSlot.TotalCount; i++)
				{
					result[i] = isFootDetail ? perFingerSources[i] : globalSource;
				}
			}

			return result;
		}
	}
}
