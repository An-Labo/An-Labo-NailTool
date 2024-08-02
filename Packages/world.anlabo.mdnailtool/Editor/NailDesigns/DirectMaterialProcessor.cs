using UnityEngine;
using world.anlabo.mdnailtool.Editor.JsonData;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.NailDesigns {
	public class DirectMaterialProcessor : NailProcessorBase {

		private readonly Material _originMaterial;

		public DirectMaterialProcessor(Material originMaterial) : base(string.Empty, new DesignData()) {
			this._originMaterial = originMaterial;
		}
		
		
		protected override Material GetBaseMaterial(string materialName, string nailShapeName) {
			return this._originMaterial;
		}

		protected override void ProcessMaterial(Material targetMaterial, string materialName, string colorName, string nailShapeName) {}
		

		protected override string GetMaterialKey(string materialName, string variationName, string nailShapeName, bool isPreview) {
			string materialKey = $"direct.{this._originMaterial.name}";
			if (isPreview) {
				materialKey += ".preview";
			}

			return materialKey;
		}

		public override bool IsInstalledMaterialVariation(string materialName) {
			throw new System.NotImplementedException();
		}

		public override bool IsInstalledColorVariation(string materialName, string colorName) {
			throw new System.NotImplementedException();
		}

		public override bool IsSupportedNailShape(string shapeName) {
			throw new System.NotImplementedException();
		}
	}
}