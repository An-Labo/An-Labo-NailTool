#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace world.anlabo.mdnailtool.Editor.Core
{
	public static class ShaderPresetApplier
	{
		// 値転送許可リスト. テクスチャは型動的判定なので列挙不要.
		private static readonly HashSet<string> NonTextureWhitelist = new() {
			"_Color",
			"_MainTexHSVG",

			"_BumpScale",
			"_UseBumpMap",

			"_Cutoff",
			"_Cull",
			"_ZWrite",

			"_MatCapBumpScale",
			"_MatCapLod",
			"_MatCap2ndBumpScale",
			"_MatCap2ndLod",

			"_Metallic",
			"_UseEmission",
			"_EmissionColor",
			"_EmissionMainStrength",
			"_EmissionBlend",

			"_UseEmission2nd",
			"_Emission2ndColor",
			"_Emission2ndMainStrength",
			"_Emission2ndBlend",

			"_GlitterColor",
			"_GlitterParams1",
			"_GlitterParams2",
			"_GlitterMainStrength",
			"_GlitterVRParallaxStrength",
			"_GlitterSensitivity",

			"_RimColor",

			"_ShadowStrength",
			"_ShadowColor",
			"_ShadowBorder",
			"_ShadowBlur",
		};

		// matcap テクスチャ系: preset 側に値が入っていれば nail で上書きしない (preset 優先).
		private static readonly HashSet<string> PresetPriorityTextures = new() {
			// lilToon
			"_MatCapTex",
			"_MatCapBumpMap",
			"_MatCapBlendMask",
			"_MatCap2ndTex",
			"_MatCap2ndBumpMap",
			"_MatCap2ndBlendMask",
			// Poiyomi
			"_Matcap",
			"_Matcap0NormalMap",
			"_MatcapMask",
			"_Matcap2",
			"_Matcap1NormalMap",
			"_Matcap2Mask",
		};

		// matcap 1st 関連の非テクスチャプロパティ. preset 側に 1st テクスチャがあれば preset 維持, なければ nail から転送.
		private static readonly HashSet<string> Matcap1stRelatedProperties = new() {
			// lilToon
			"_UseMatCap", "_MatCapColor", "_MatCapBlend", "_MatCapBlendMode",
			"_MatCapEnableLighting", "_MatCapShadowMask", "_MatCapBackfaceMask",
			"_MatCapApplyTransparency", "_MatCapPerspective", "_MatCapMainStrength",
			"_MatCapVRParallaxStrength", "_MatCapZRotCancel", "_MatCapCustomNormal",
			"_MatCapNormalStrength", "_MatCapBumpScale", "_MatCapLod",
			// Poiyomi
			"_MatcapEnable", "_MatcapColor", "_MatcapIntensity",
			"_MatcapNormal", "_Matcap0NormalMapScale",
		};

		// matcap 2nd 関連の非テクスチャプロパティ.
		private static readonly HashSet<string> Matcap2ndRelatedProperties = new() {
			// lilToon
			"_UseMatCap2nd", "_MatCap2ndColor", "_MatCap2ndBlend", "_MatCap2ndBlendMode",
			"_MatCap2ndEnableLighting", "_MatCap2ndShadowMask", "_MatCap2ndBackfaceMask",
			"_MatCap2ndApplyTransparency", "_MatCap2ndPerspective", "_MatCap2ndMainStrength",
			"_MatCap2ndVRParallaxStrength", "_MatCap2ndZRotCancel", "_MatCap2ndCustomNormal",
			"_MatCap2ndNormalStrength", "_MatCap2ndBumpScale", "_MatCap2ndLod",
			// Poiyomi
			"_Matcap2Enable", "_Matcap2Color", "_Matcap2Intensity",
			"_Matcap2Normal", "_Matcap1NormalMapScale",
		};

		// preset 側で matcap (1st/2nd) テクスチャ有無を判定する対象プロパティ.
		private static readonly string[] Matcap1stTextureProps = { "_MatCapTex", "_Matcap" };
		private static readonly string[] Matcap2ndTextureProps = { "_MatCap2ndTex", "_Matcap2" };

		// Poiyomi は一部の lilToon 同名プロパティも持つため、Emission は実際に使われる別名を優先する。
		private static readonly HashSet<string> PresetAliasPriority = new() {
			"_UseEmission", "_EmissionMainStrength", "_EmissionBlendMask",
			"_UseEmission2nd", "_Emission2ndColor", "_Emission2ndMap",
			"_Emission2ndMainStrength", "_Emission2ndBlendMask",
		};

		// lilToon → Poiyomi 別名解決. 同名 (_BumpMap/_Color/_Metallic/_EmissionColor/_RimColor/_ShadowColor 等) は同名経路で処理.
		private static readonly Dictionary<string, string> LilToPoiMap = new() {
			{ "_UseMatCap", "_MatcapEnable" },
			{ "_MatCapColor", "_MatcapColor" },
			{ "_MatCapTex", "_Matcap" },
			{ "_MatCapBlend", "_MatcapIntensity" },
			{ "_MatCapBlendMask", "_MatcapMask" },
			{ "_MatCapNormalStrength", "_MatcapNormal" },
			{ "_MatCapBumpMap", "_Matcap0NormalMap" },
			{ "_MatCapBumpScale", "_Matcap0NormalMapScale" },

			{ "_UseMatCap2nd", "_Matcap2Enable" },
			{ "_MatCap2ndColor", "_Matcap2Color" },
			{ "_MatCap2ndTex", "_Matcap2" },
			{ "_MatCap2ndBlend", "_Matcap2Intensity" },
			{ "_MatCap2ndBlendMask", "_Matcap2Mask" },
			{ "_MatCap2ndNormalStrength", "_Matcap2Normal" },
			{ "_MatCap2ndBumpMap", "_Matcap1NormalMap" },
			{ "_MatCap2ndBumpScale", "_Matcap1NormalMapScale" },

			{ "_UseBump2ndMap", "_DetailEnabled" },
			{ "_Bump2ndMap", "_DetailNormalMap" },
			{ "_Bump2ndScale", "_DetailNormalMapScale" },

			{ "_UseReflection", "_MochieBRDF" },
			{ "_Smoothness", "_Glossiness" },

			{ "_UseEmission", "_EnableEmission" },
			{ "_EmissionMainStrength", "_EmissionBaseColorAsMap" },
			{ "_EmissionBlendMask", "_EmissionMask" },

			{ "_UseEmission2nd", "_EnableEmission1" },
			{ "_Emission2ndColor", "_EmissionColor1" },
			{ "_Emission2ndMap", "_EmissionMap1" },
			{ "_Emission2ndMainStrength", "_EmissionBaseColorAsMap1" },
			{ "_Emission2ndBlendMask", "_EmissionMask1" },

			{ "_UseGlitter", "_GlitterEnable" },
			{ "_GlitterColorTex", "_GlitterColorMap" },

			{ "_UseRim", "_EnableRimLighting" },
			{ "_UseShadow", "_ShadingEnabled" },
		};

		public static void OverrideFromNail(Material presetBase, Material nailSource)
		{
			if (presetBase == null || nailSource == null) return;

			Shader nailShader = nailSource.shader;
			Shader presetShader = presetBase.shader;
			int count = nailShader.GetPropertyCount();

			bool presetHasMatcap1st = HasPresetTexture(presetBase, Matcap1stTextureProps);
			bool presetHasMatcap2nd = HasPresetTexture(presetBase, Matcap2ndTextureProps);

			for (int i = 0; i < count; i++) {
				string nailProp = nailShader.GetPropertyName(i);
				ShaderPropertyType nailType = nailShader.GetPropertyType(i);

				string? presetProp = ResolvePresetProperty(presetShader, nailProp);
				if (presetProp == null) continue;

				int presetIdx = presetShader.FindPropertyIndex(presetProp);
				if (presetIdx < 0) continue;

				ShaderPropertyType presetType = presetShader.GetPropertyType(presetIdx);
				if (nailType != presetType
					&& !(IsNumericShaderProperty(nailType) && IsNumericShaderProperty(presetType))) continue;

				switch (nailType) {
					case ShaderPropertyType.Texture: {
						if (PresetPriorityTextures.Contains(presetProp) && presetBase.GetTexture(presetProp) != null) {
							break;
						}
						Texture? tex = nailSource.GetTexture(nailProp);
						if (tex != null) presetBase.SetTexture(presetProp, tex);
						break;
					}
					case ShaderPropertyType.Float:
					case ShaderPropertyType.Range:
					case ShaderPropertyType.Int:
						if (IsValueTransferAllowedForNail(nailProp, presetHasMatcap1st, presetHasMatcap2nd)) {
							float value = nailType == ShaderPropertyType.Int
								? nailSource.GetInt(nailProp)
								: nailSource.GetFloat(nailProp);
							if (presetType == ShaderPropertyType.Int) presetBase.SetInt(presetProp, Mathf.RoundToInt(value));
							else presetBase.SetFloat(presetProp, value);
						}
						break;
					case ShaderPropertyType.Color:
						if (IsValueTransferAllowedForNail(nailProp, presetHasMatcap1st, presetHasMatcap2nd)) {
							Color value = nailSource.GetColor(nailProp);
							presetBase.SetColor(presetProp, value);
							// Poiyomi uses the lilToon emission color alpha as emission intensity.
							if (nailProp == "_EmissionColor" && presetBase.HasProperty("_EmissionStrength")) {
								presetBase.SetFloat("_EmissionStrength", value.a);
							}
							else if (nailProp == "_Emission2ndColor" && presetBase.HasProperty("_EmissionStrength1")) {
								presetBase.SetFloat("_EmissionStrength1", value.a);
							}
						}
						break;
					case ShaderPropertyType.Vector:
						if (IsValueTransferAllowedForNail(nailProp, presetHasMatcap1st, presetHasMatcap2nd)) {
							presetBase.SetVector(presetProp, nailSource.GetVector(nailProp));
						}
						break;
				}
			}

			NormalizePoiyomiMatcapBlend(presetBase, presetShader);
		}

		private static bool HasPresetTexture(Material presetBase, string[] textureProps)
		{
			foreach (string p in textureProps) {
				if (presetBase.HasProperty(p) && presetBase.GetTexture(p) != null) return true;
			}
			return false;
		}

		private static bool IsNumericShaderProperty(ShaderPropertyType type) =>
			type == ShaderPropertyType.Float || type == ShaderPropertyType.Range || type == ShaderPropertyType.Int;

		private static bool IsValueTransferAllowedForNail(string nailProp, bool presetHasMatcap1st, bool presetHasMatcap2nd)
		{
			if (Matcap1stRelatedProperties.Contains(nailProp)) return !presetHasMatcap1st;
			if (Matcap2ndRelatedProperties.Contains(nailProp)) return !presetHasMatcap2nd;
			return IsValueTransferAllowed(nailProp);
		}

		// lilToon の matcap 見た目を Poiyomi で再現するため, Add+Screen+UnlitAdd 合算に固定 (Replace は OFF).
		private static void NormalizePoiyomiMatcapBlend(Material presetBase, Shader presetShader)
		{
			if (presetShader.FindPropertyIndex("_MatcapAdd") < 0) return;
			presetBase.SetFloat("_MatcapReplace", 0f);
			presetBase.SetFloat("_MatcapAdd", 1f);
			presetBase.SetFloat("_MatcapScreen", 1f);
			presetBase.SetFloat("_MatcapAddToLight", 1f);
		}

		private static string? ResolvePresetProperty(Shader presetShader, string nailProp)
		{
			if (PresetAliasPriority.Contains(nailProp)
				&& LilToPoiMap.TryGetValue(nailProp, out string? preferredAlias)
				&& presetShader.FindPropertyIndex(preferredAlias) >= 0) {
				return preferredAlias;
			}
			if (presetShader.FindPropertyIndex(nailProp) >= 0) return nailProp;
			if (LilToPoiMap.TryGetValue(nailProp, out string? poiName)
				&& presetShader.FindPropertyIndex(poiName) >= 0) {
				return poiName;
			}
			return null;
		}

		private static bool IsValueTransferAllowed(string nailProp)
		{
			return NonTextureWhitelist.Contains(nailProp);
		}
	}
}
