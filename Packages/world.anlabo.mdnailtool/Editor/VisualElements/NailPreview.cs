using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.VisualElements {
	public class NailPreview : VisualElement, IDisposable {
		private View? _view;

		private Color _backgroundColor;
		private Color BackgroundColor {
			set {
				this._backgroundColor = value;
				if (this._view != null) {
					this._view.BackgroundColor = value;
				}
			}
		}

		public GameObject? NailObj => this._view?.NailObj;

		public NailPreview() {
			this.Add(new IMGUIContainer(this.OnGUI));
		}

		private void OnGUI() {
			this._view?.OnGUI(this.contentRect);
		}

		protected override void ExecuteDefaultActionAtTarget(EventBase evt) {
			switch (evt) {
				case AttachToPanelEvent:
					this._view = new View();
					this._view.BackgroundColor = this._backgroundColor;
					break;
				case DetachFromPanelEvent:
					this._view?.Dispose();
					this._view = null;
					break;
			}
		}

		public void Dispose() {
			this._view?.Dispose();
			this._view = null;
		}


		internal new class UxmlFactory : UxmlFactory<NailPreview, UxmlTraits> { }

		internal new class UxmlTraits : VisualElement.UxmlTraits {
			private readonly UxmlColorAttributeDescription _backgroundColor = new() {
				name = "background-color",
				defaultValue = Color.black
			};

			public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc) {
				base.Init(ve, bag, cc);
				if (ve is not NailPreview preview) return;
				preview.BackgroundColor = this._backgroundColor.GetValueFromBag(bag, cc);
			}
		}

		private class View : IDisposable {
			private readonly Scene _scene;
			private RenderTexture? _renderTexture;
			private Material _material;

			private readonly GameObject _cameraObj;
			private readonly GameObject _lightObj;
			public GameObject NailObj { get; }

			private readonly Camera _camera;

			private int _height = 32;
			private int _width = 32;

			public Color BackgroundColor {
				set => this._camera.backgroundColor = value;
			}


			public View() {
				this._scene = EditorSceneManager.NewPreviewScene();
				this._material = CreateMaterial();

				this._cameraObj = this.CreateCameraObject(out Camera camera);
				this._camera = camera;
				this.AddGameObject(this._cameraObj);

				this._lightObj = CreateLightObj();
				this.AddGameObject(this._lightObj);

				this.NailObj = CreateNailObj();
				this.AddGameObject(this.NailObj);
			}

			public void OnGUI(Rect rect) {
				Event e = Event.current;
				if (e.type == EventType.Layout) return;

				int intWidth = Mathf.CeilToInt(rect.width);
				int intHeight = Mathf.CeilToInt(rect.height);
				if (intWidth == 0 || intHeight == 0) return;
				if (this._width != intWidth || this._height != intHeight || this._renderTexture == null) {
					this.ResizeMonitor(intWidth, intHeight);
				}

				bool oldAllowPipes = Unsupported.useScriptableRenderPipeline;
				Unsupported.useScriptableRenderPipeline = false;
				this._camera.Render();
				Unsupported.useScriptableRenderPipeline = oldAllowPipes;

				if (this._material == null) {
					this._material = CreateMaterial();
				}

				Graphics.DrawTexture(rect, this._renderTexture, this._material);
			}

			private void ResizeMonitor(int width, int height) {
				if (width <= 0 || height <= 0) return;
				this._renderTexture = new RenderTexture(width, height, 32);
				this._camera.targetTexture = this._renderTexture;
				this._width = width;
				this._height = height;
			}

			private GameObject CreateCameraObject(out Camera camera) {
				GameObject cameraObj = new("Camera", typeof(Camera)) {
					transform = {
						position = new Vector3(0, 0.5f, 0.007f),
						rotation = Quaternion.Euler(90, 0, 0)
					}
				};
				camera = cameraObj.GetComponent<Camera>();
				camera.cameraType = CameraType.Preview;
				camera.orthographic = true;
				camera.orthographicSize = 0.025f;
				camera.forceIntoRenderTexture = true;
				camera.scene = this._scene;
				camera.enabled = false;
				camera.nearClipPlane = 0.01f;
				camera.clearFlags = CameraClearFlags.SolidColor;
				return cameraObj;
			}

			private void AddGameObject(GameObject obj) {
				SceneManager.MoveGameObjectToScene(obj, this._scene);
			}

			public void Dispose() {
				if (this._camera != null) this._camera.targetTexture = null;
				if (this._renderTexture != null) Object.DestroyImmediate(this._renderTexture);
				if (this.NailObj != null) Object.DestroyImmediate(this.NailObj);
				if (this._lightObj != null) Object.DestroyImmediate(this._lightObj);
				if (this._cameraObj != null) Object.DestroyImmediate(this._cameraObj);
				EditorSceneManager.ClosePreviewScene(this._scene);
			}

			private static GameObject CreateLightObj() {
				GameObject lightObj = new("Directional Light", typeof(Light)) {
					transform = {
						rotation = Quaternion.Euler(50, -30, 0)
					}
				};

				Light light = lightObj.GetComponent<Light>();
				light.type = LightType.Directional;
				return lightObj;
			}

			private static GameObject CreateNailObj() {
				string prefabPath = AssetDatabase.GUIDToAssetPath(MDNailToolDefines.PREVIEW_PREFAB_GUID);
				GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
				GameObject cloned = Object.Instantiate(prefab);
				cloned.name = prefab.name;
				cloned.transform.position = Vector3.zero;
				cloned.transform.rotation = Quaternion.identity;
				return cloned;
			}

			private static Material CreateMaterial() {
				string shaderPath = AssetDatabase.GUIDToAssetPath(MDNailToolDefines.PREVIEW_SHADER_GUID);
				Shader previewShader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
				return new Material(previewShader);
			}
		}
	}
}