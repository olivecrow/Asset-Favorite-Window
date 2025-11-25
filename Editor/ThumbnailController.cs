using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoF.AssetFavoriteWindow.Editor
{
    public static class ThumbnailController
    {
        const string PATH_THUMBNAILS = "Assets/Editor/AFW Thumbnails";
        public static Texture2D TakePrefabThumbnail(GameObject prefab, ThumbnailSettings settings)
        {
            if (prefab == null)
            {
                Debug.LogError("Prefab is null.");
                return null;
            }

            // Create a temporary scene to take the thumbnail
            var scene = EditorSceneManager.NewPreviewScene();
            var sceneCamera = new GameObject("ThumbnailCamera", typeof(Camera)).GetComponent<Camera>();
            sceneCamera.scene = scene;
            sceneCamera.cameraType = CameraType.Preview;
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            sceneCamera.fieldOfView = settings.CameraFOV;
            sceneCamera.farClipPlane = 1000;
            sceneCamera.nearClipPlane = 0.1f;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (instance == null)
            {
                Debug.LogError("Failed to instantiate prefab.");
                return null;
            }

            instance.transform.rotation = Quaternion.Euler(settings.ObjectRotation);

            // Center the camera on the object
            Bounds bounds = GetBounds(instance);
            float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float distance = maxDim / (2f * Mathf.Tan(sceneCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            sceneCamera.transform.position = bounds.center - new Vector3(0, -bounds.size.y * 0.5f, distance * 1.166f);
            sceneCamera.transform.LookAt(bounds.center);


            // Add a light to the scene
            var light = new GameObject("ThumbnailLight", typeof(Light)).GetComponent<Light>();
            SceneManager.MoveGameObjectToScene(light.gameObject, scene);
            light.type = LightType.Directional;
            light.intensity = settings.LightIntensity;
            light.transform.rotation = Quaternion.Euler(settings.LightRotation);
            
            var reflectionLight = new GameObject("ThumbnailLight", typeof(Light)).GetComponent<Light>();
            SceneManager.MoveGameObjectToScene(reflectionLight.gameObject, scene);
            reflectionLight.type = LightType.Directional;
            reflectionLight.intensity = settings.ReflectionIntensity;
            var mainLightRot = light.transform.eulerAngles;
            reflectionLight.transform.rotation = Quaternion.Euler(mainLightRot.x, mainLightRot.y - 90, mainLightRot.z);

            // Render the thumbnail
            RenderTexture renderTexture = new RenderTexture(settings.ThumbnailSize.x, settings.ThumbnailSize.y, 24);
            sceneCamera.targetTexture = renderTexture;
            sceneCamera.Render();

            Texture2D thumbnail = new Texture2D(settings.ThumbnailSize.x, settings.ThumbnailSize.y, TextureFormat.RGB24, false);
            RenderTexture.active = renderTexture;
            thumbnail.ReadPixels(new Rect(0, 0, settings.ThumbnailSize.x, settings.ThumbnailSize.y), 0, 0);
            thumbnail.Apply();

            RenderTexture.active = null;
            sceneCamera.targetTexture = null;

            // Cleanup
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(sceneCamera.gameObject);
            Object.DestroyImmediate(light.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            Object.DestroyImmediate(renderTexture);

            return thumbnail;
        }

        private static Bounds GetBounds(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
                return bounds;
            }
            return new Bounds(obj.transform.position, Vector3.zero);
        }

        public static void SaveThumbnail(Object asset, Texture2D thumbnail)
        {
            if(AFW_Data.instance.TryGetDetail(asset, out var detail))
            {
                if(detail.thumbnail) AssetDatabase.RemoveObjectFromAsset(detail.thumbnail);
            }
            else
            {
                detail = new AFW_AssetDetail
                {
                    guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset))
                };
                AFW_Data.instance.AppendDetail(asset, detail);
            }
            EnsureFolderExists(PATH_THUMBNAILS);
            thumbnail.name = $"{asset.name}_{detail.guid}_Thumbnail";
            var path = Path.Combine(PATH_THUMBNAILS, thumbnail.name + ".asset");
            detail.thumbnail = thumbnail;
            
            AssetDatabase.CreateAsset(thumbnail, path);
            AFW_Data.instance.Save();
        }
        
        /// <summary>
        /// 경로의 모든 상위 폴더가 존재하는지 확인하고, 없으면 생성합니다.
        /// 예: "Assets/Editor/AFW Thumbnails"
        /// </summary>
        public static void EnsureFolderExists(string targetFolderAssetPath)
        {
            // 1. 이미 유니티 상에서 유효한 폴더인지 확인 (최적화)
            if (AssetDatabase.IsValidFolder(targetFolderAssetPath)) return;

            // 2. "Assets/..." 형태의 유니티 경로를 윈도우/맥의 실제 전체 경로로 변환
            // Application.dataPath는 ".../Assets"까지를 반환하므로 그 상위 폴더(프로젝트 루트)를 구함
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            string fullSystemPath = Path.Combine(projectRoot, targetFolderAssetPath);

            // 3. System.IO 기능을 사용하여 없는 폴더를 계층 구조에 맞춰 한 번에 전부 생성
            // (중간 폴더가 없으면 알아서 다 만들어줍니다)
            Directory.CreateDirectory(fullSystemPath);

            // 4. 유니티 에디터가 새로 생긴 폴더를 인식하고 .meta 파일을 생성하도록 갱신
            AssetDatabase.Refresh();
        }
    }
}
