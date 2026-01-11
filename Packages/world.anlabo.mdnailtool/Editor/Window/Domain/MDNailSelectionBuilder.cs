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
			var globalMasterSource = allSelections.Length > 0 ? allSelections[0] : emptyDummy;

			if (isHandActive)
			{
				var handSource = allSelections.Length > 0 ? allSelections[0] : emptyDummy;
				for (int i = 0; i < 10; i++)
				{
					finalSelectionList.Add(isHandDetail ? allSelections[i] : handSource);
				}
			}
			else
			{
				for (int i = 0; i < 10; i++) finalSelectionList.Add(emptyDummy);
			}

			if (isFootActive)
			{
				for (int i = 10; i < 20; i++)
				{
					finalSelectionList.Add(isFootDetail ? allSelections[i] : globalMasterSource);
				}
			}
			else
			{
				for (int i = 0; i < 10; i++) finalSelectionList.Add(emptyDummy);
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
	}
}
