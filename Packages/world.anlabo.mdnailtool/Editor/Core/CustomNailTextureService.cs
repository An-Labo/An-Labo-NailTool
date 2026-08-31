#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace world.anlabo.mdnailtool.Editor.Core
{
	internal static class CustomNailTextureService
	{
		private const string StandardNailMatCapGuid = "81aa5acab4400e54486f930dda82be43";

		internal static void EnsureFolder()
		{
			bool changed = false;
			if (!Directory.Exists(MDNailToolDefines.CUSTOM_NAIL_TEXTURE_PATH))
			{
				Directory.CreateDirectory(MDNailToolDefines.CUSTOM_NAIL_TEXTURE_PATH);
				changed = true;
			}
			if (!Directory.Exists(MDNailToolDefines.CUSTOM_NAIL_GENERATED_PATH))
			{
				Directory.CreateDirectory(MDNailToolDefines.CUSTOM_NAIL_GENERATED_PATH);
				changed = true;
			}
			if (changed) AssetDatabase.Refresh();
		}

		internal static List<string> FindTexturePaths()
		{
			EnsureFolder();
			return AssetDatabase.FindAssets("t:Texture2D", new[] { MDNailToolDefines.CUSTOM_NAIL_TEXTURE_PATH })
				.Select(AssetDatabase.GUIDToAssetPath)
				.Where(path => !string.IsNullOrEmpty(path))
				.OrderBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		internal static Material? BuildMaterial(string texturePath)
		{
			Texture2D? texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
			if (texture == null) return null;

			string? presetName = GlobalSetting.SelectedShaderPreset;
			Material? preset = string.IsNullOrEmpty(presetName)
				? ShaderPresetScanner.FindPresetByName("lilToon Default")
				: ShaderPresetScanner.FindPresetByName(presetName!);
			preset ??= ShaderPresetScanner.ScanAllPresetNames()
				.Select(ShaderPresetScanner.FindPresetByName)
				.FirstOrDefault(material => material != null);
			if (preset == null) return null;

			Material generated = new(preset) { name = $"CustomNail_{texture.name}" };
			SetTextureIfAvailable(generated, "_MainTex", texture);
			SetTextureIfAvailable(generated, "_BaseMap", texture);
			SetTextureIfAvailable(generated, "_BaseColorMap", texture);
			SetStandardMatCapIfAvailable(generated);

			EnsureFolder();
			string folder = MDNailToolDefines.CUSTOM_NAIL_GENERATED_PATH;
			string safeName = string.Concat(texture.name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
			string assetPath = $"{folder}{safeName}.mat";
			Material? existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
			if (existing == null)
			{
				AssetDatabase.CreateAsset(generated, assetPath);
				existing = generated;
			}
			else
			{
				EditorUtility.CopySerialized(generated, existing);
				UnityEngine.Object.DestroyImmediate(generated);
				EditorUtility.SetDirty(existing);
			}
			AssetDatabase.SaveAssets();
			return existing;
		}

		private static void SetTextureIfAvailable(Material material, string propertyName, Texture texture)
		{
			if (material.HasProperty(propertyName)) material.SetTexture(propertyName, texture);
		}

		private static void SetStandardMatCapIfAvailable(Material material)
		{
			const string propertyName = "_MatCapTex";
			if (!material.HasProperty(propertyName) || material.GetTexture(propertyName) != null) return;

			string matCapPath = AssetDatabase.GUIDToAssetPath(StandardNailMatCapGuid);
			if (string.IsNullOrEmpty(matCapPath))
			{
				DisableMatCap(material);
				return;
			}

			Texture2D? matCap = AssetDatabase.LoadAssetAtPath<Texture2D>(matCapPath);
			if (matCap != null)
			{
				material.SetTexture(propertyName, matCap);
				return;
			}

			DisableMatCap(material);
		}

		private static void DisableMatCap(Material material)
		{
			const string useMatCapPropertyName = "_UseMatCap";
			if (material.HasProperty(useMatCapPropertyName)) material.SetFloat(useMatCapPropertyName, 0f);
		}
	}
}
