using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zzapmagochi.Room;

namespace Zzapmagochi.EditorTools
{
    /// <summary>
    /// 현재 열린 자취방 씬(scene_room)에 벽/바닥 3D 두께 메쉬, 구석탱이 음영(Corner AO) 전용 머티리얼,
    /// 그리고 에디터에서 실시간으로 조명 색을 조절할 수 있는 RoomAtmosphereController를 한 번에 세팅한다.
    /// </summary>
    public static class RoomAtmosphereSetup
    {
        private const string MaterialDir = "Assets/Material";
        private const string SurfaceMatPath = MaterialDir + "/M_ZzapRoomSurface.mat";

        [MenuItem("Zzapmagochi/Build Scenes/Apply Room Thickness & Lighting to Active Scene")]
        public static void ApplyToActiveScene()
        {
            if (!Directory.Exists(MaterialDir))
                AssetDatabase.CreateFolder("Assets", "Material");

            var shader = Shader.Find("Zzapmagochi/Room/LitRoomSurfaceWithCornerAO");
            if (shader == null)
            {
                Debug.LogError("[RoomAtmosphereSetup] 'Zzapmagochi/Room/LitRoomSurfaceWithCornerAO' 셰이더를 찾지 못했습니다.");
                return;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(SurfaceMatPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "M_ZzapRoomSurface" };
                AssetDatabase.CreateAsset(mat, SurfaceMatPath);
            }
            else
            {
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
            }

            var roomGo = GameObject.Find("room");
            if (roomGo == null)
            {
                Debug.LogError("[RoomAtmosphereSetup] 현재 씬에서 'room' 오브젝트를 찾지 못했습니다. scene_room 씬을 열고 실행해 주세요.");
                return;
            }

            var ctrl = roomGo.GetComponent<RoomAtmosphereController>();
            if (ctrl == null)
                ctrl = roomGo.AddComponent<RoomAtmosphereController>();

            Light dirLight = null;
            var dirGo = GameObject.Find("Directional Light");
            if (dirGo != null) dirLight = dirGo.GetComponent<Light>();
            if (dirLight == null) dirLight = Object.FindFirstObjectByType<Light>();

            var so = new SerializedObject(ctrl);
            so.FindProperty("roomRoot").objectReferenceValue = roomGo.transform;
            so.FindProperty("sceneDirectionalLight").objectReferenceValue = dirLight;
            so.FindProperty("roomSurfaceMaterial").objectReferenceValue = mat;
            so.ApplyModifiedPropertiesWithoutUndo();

            ctrl.RefreshAll();

            EditorSceneManager.MarkSceneDirty(roomGo.scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = roomGo;

            Debug.Log("[RoomAtmosphereSetup] 자취방 벽/바닥 두께 및 실시간 조명·구석 음영 설정 완료! 'room' 오브젝트의 Inspector에서 조명 색을 조절해 보세요.");
        }
    }
}
