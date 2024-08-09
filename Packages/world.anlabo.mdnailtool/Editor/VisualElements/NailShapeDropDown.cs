using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Model;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.VisualElements {
	public class NailShapeDropDown : LocalizedDropDown {
		public NailShapeDropDown() {
			this.Init();
		}

		private void Init() {
			using DBNailShape dbNailShape = new();
			List<string> shapeNames = dbNailShape.collection.Select(shape => shape.ShapeName).ToList();
			this.choices = shapeNames;
			this.value = shapeNames[0];
		}

		public void SetFilter(Func<string, bool> filter) {
			using DBNailShape dbNailShape = new();
			List<string> shapeNames = dbNailShape.collection
				.Select(shape => shape.ShapeName)
				.Where(filter)
				.ToList();
			this.choices = shapeNames;
			this.SetValueWithoutNotify(shapeNames.Contains(GlobalSetting.LastUseShapeName) ? GlobalSetting.LastUseShapeName : shapeNames[0]);
		}

		public Mesh?[]? GetSelectedShapeMeshes() {
			string? shapeName = this.value;
			if (string.IsNullOrEmpty(shapeName)) return null;
			
			using DBNailShape dbNailShape = new();
			NailShape? nailShape = dbNailShape.FindNailShapeByName(shapeName);
			if (nailShape == null) return null;

			string? path = null;
			foreach (string guid in nailShape.FbxFolderGUID) {
				if (string.IsNullOrEmpty(guid)) continue;
				path = AssetDatabase.GUIDToAssetPath(guid);
				if (string.IsNullOrEmpty(path)) continue;
				if (Directory.Exists(path)) break;
			}

			if (string.IsNullOrEmpty(path)) return null;
			if (!Directory.Exists(path)) return null;

			if (File.Exists($"{path}/{nailShape.FbxNamePrefix}{MDNailToolDefines.HANDS_NAIL_OBJECT_NAME_LIST[0]}.fbx")) {
				return MDNailToolDefines.HANDS_NAIL_OBJECT_NAME_LIST
					.Select(objectName => $"{path}/{nailShape.FbxNamePrefix}{objectName}.fbx")
					.Select(AssetDatabase.LoadAssetAtPath<Mesh>)
					.ToArray();
			}

			return MDNailToolDefines.HANDS_NAIL_OBJECT_NAME_LIST
				.Select(objectName => $"{path}/{nailShape.FbxNamePrefix}{objectName.Replace('.', '_')}.fbx")
				.Select(AssetDatabase.LoadAssetAtPath<Mesh>)
				.ToArray();
		}

		public void SetNailShape(string shapeName) {
			if (!this.choices.Contains(shapeName)) return;
			this.SetValueWithoutNotify(shapeName);
		}


		internal new class UxmlFactory : UxmlFactory<NailShapeDropDown, UxmlTraits> {}
		
		internal new class UxmlTraits : LocalizedDropDown.UxmlTraits {}
		
	}
}