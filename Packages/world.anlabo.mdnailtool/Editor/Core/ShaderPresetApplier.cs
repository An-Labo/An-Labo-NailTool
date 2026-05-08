#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace world.anlabo.mdnailtool.Editor.Core
{
	public static class ShaderPresetApplier
	{
		// 非テクスチャ系プロパティのうち, ネイル側の値で preset を上書きするホワイトリスト.
		// テクスチャ型 (TexEnv) は型ベースで動的判定するため列挙不要 (ネイル側に値が入っていれば常に上書き).
		private static readonly HashSet<string> NonTextureWhitelist = new() {
			"_Color",
			"_MainTexHSVG",

			"_BumpScale",
			"_UseBumpMap",
			"_UseBump2ndMap",
			"_Bump2ndScale",

			"_Cutoff",
			"_Cull",
			"_ZWrite",

			"_UseMatCap",
			"_MatCapColor",
			"_MatCapBlend",
			"_MatCapNormalStrength",
			"_MatCapBumpScale",
			"_MatCapLod",

			"_UseEmission",
			"_EmissionColor",
			"_EmissionMainStrength",
			"_EmissionBlend",

			"_UseEmission2nd",
			"_Emission2ndColor",
			"_Emission2ndMainStrength",
			"_Emission2ndBlend",

			"_UseGlitter",
			"_GlitterColor",
			"_GlitterParams1",
			"_GlitterParams2",
			"_GlitterMainStrength",
			"_GlitterVRParallaxStrength",
			"_GlitterSensitivity",
		};

		public static void OverrideFromNail(Material presetBase, Material nailSource)
		{
			if (presetBase == null || nailSource == null) return;

			Shader nailShader = nailSource.shader;
			int count = nailShader.GetPropertyCount();

			for (int i = 0; i < count; i++) {
				string propName = nailShader.GetPropertyName(i);
				if (!presetBase.HasProperty(propName)) continue;

				int presetIdx = presetBase.shader.FindPropertyIndex(propName);
				if (presetIdx < 0) continue;

				ShaderPropertyType nailType = nailShader.GetPropertyType(i);
				ShaderPropertyType presetType = presetBase.shader.GetPropertyType(presetIdx);
				if (nailType != presetType) continue;

				switch (nailType) {
					case ShaderPropertyType.Texture: {
						Texture? tex = nailSource.GetTexture(propName);
						if (tex != null) presetBase.SetTexture(propName, tex);
						break;
					}
					case ShaderPropertyType.Float:
					case ShaderPropertyType.Range:
						if (NonTextureWhitelist.Contains(propName)) {
							presetBase.SetFloat(propName, nailSource.GetFloat(propName));
						}
						break;
					case ShaderPropertyType.Color:
						if (NonTextureWhitelist.Contains(propName)) {
							presetBase.SetColor(propName, nailSource.GetColor(propName));
						}
						break;
					case ShaderPropertyType.Vector:
						if (NonTextureWhitelist.Contains(propName)) {
							presetBase.SetVector(propName, nailSource.GetVector(propName));
						}
						break;
				}
			}
		}
	}
}
