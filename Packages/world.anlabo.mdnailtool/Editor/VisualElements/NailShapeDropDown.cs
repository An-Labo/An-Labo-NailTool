using System.Collections.Generic;
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

		public Mesh?[]? GetSelectedShapeMeshes() {
			string? shapeName = this.value;
			if (string.IsNullOrEmpty(shapeName)) return null;
			
			using DBNailShape dbNailShape = new();
			NailShape? nailShape = dbNailShape.FindNailShapeByName(shapeName);
			if (nailShape == null) return null;

			string guid = nailShape.FbxFolderGUID;
			if (string.IsNullOrEmpty(guid)) return null;

			string? path = AssetDatabase.GUIDToAssetPath(guid);
			if (string.IsNullOrEmpty(path)) return null;

			return MDNailToolDefines.HANDS_NAIL_OBJECT_NAME_LIST
				.Select(objectName => $"{path}/{nailShape.FbxNamePrefix}{objectName}.fbx")
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