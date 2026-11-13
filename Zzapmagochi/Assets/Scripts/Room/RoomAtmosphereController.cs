using System.Collections.Generic;
using UnityEngine;

namespace Zzapmagochi.Room
{
    /// <summary>
    /// 자취방(scene_room)의 벽/바닥 두께(3D 단면) 자동 생성·색상 동기화 및
    /// 에디터 실시간 조명 색상 · 구석탱이 음영(Corner AO) 조절 컨트롤러.
    ///
    /// [ExecuteAlways]가 붙어 있어 Play 버튼을 누르지 않고 에디터 Inspector에서
    /// 조명 색이나 슬라이더를 만지기만 해도 Scene / Game 뷰에 즉시 반영됩니다.
    /// </summary>
    [ExecuteAlways]
    public class RoomAtmosphereController : MonoBehaviour
    {
        [Header("1. 에디터 실시간 조명 설정 (Inspector에서 바로 조절)")]
        [Tooltip("자취방 전체에 비치는 메인 조명 색상 (따뜻한 백열등, 노을빛, 새벽 푸른빛 등)")]
        [SerializeField] private Color roomLightColor = new Color(1.0f, 0.94f, 0.84f, 1f);

        [Tooltip("메인 조명 밝기")]
        [SerializeField, Range(0.2f, 2.2f)] private float roomLightIntensity = 1.05f;

        [Tooltip("빛을 덜 받는 면의 기본 환경광(Ambient) 톤")]
        [SerializeField] private Color ambientTint = new Color(0.58f, 0.53f, 0.66f, 1f);

        [Tooltip("조명이 비추는 방향 (벽면/바닥면 사이의 자연스러운 명암차 결정)")]
        [SerializeField] private Vector3 lightDirection = new Vector3(-0.35f, 0.8f, -0.45f);

        [Header("2. 구석탱이 어두움 (Corner Ambient Occlusion) 설정")]
        [Tooltip("벽과 벽, 벽과 바닥이 만나는 구석 모서리가 얼마나 어두워질지 강도")]
        [SerializeField, Range(0f, 1f)] private float cornerDarkness = 0.48f;

        [Tooltip("구석 어두움이 퍼지는 반경 (유닛 단위)")]
        [SerializeField, Range(0.3f, 3.0f)] private float cornerRadius = 1.4f;

        [Tooltip("구석 그림자에 섞일 어두운 그림자 색")]
        [SerializeField] private Color cornerShadowColor = new Color(0.24f, 0.19f, 0.32f, 1f);

        [Header("3. 벽지 / 바닥재 컬러 & 두께 설정")]
        [Tooltip("벽지 전체에 입힐 색상 틴트 (흰색이면 원본 스프라이트 색 그대로 사용)")]
        [SerializeField] private Color wallpaperTint = Color.white;

        [Tooltip("바닥재 전체에 입힐 색상 틴트 (흰색이면 원본 스프라이트 색 그대로 사용)")]
        [SerializeField] private Color flooringTint = Color.white;

        [Tooltip("벽 두께 (유닛 단위)")]
        [SerializeField, Range(0.05f, 0.6f)] private float wallThickness = 0.22f;

        [Tooltip("바닥 두께 (유닛 단위)")]
        [SerializeField, Range(0.05f, 0.6f)] private float floorThickness = 0.22f;

        [Tooltip("벽지/바닥재 본체 색 대비 두께 단면의 명도 비율 (1.0이면 완전 동일, 0.88이면 단면이 아주 살짝 차분해져 입체감이 살아남)")]
        [SerializeField, Range(0.65f, 1.05f)] private float thicknessToneRatio = 0.88f;

        [Header("4. 방 크기 기준값 (현재 방 규격: 5 x 4 x 5)")]
        [SerializeField] private Vector3 roomMinCorner = new Vector3(-2.5f, 0f, -2.5f);
        [SerializeField] private Vector3 roomMaxCorner = new Vector3(2.5f, 4f, 2.5f);

        [Header("5. 참조 (비워두면 씬에서 자동 탐색)")]
        [SerializeField] private Transform roomRoot;
        [SerializeField] private Light sceneDirectionalLight;
        [SerializeField] private Material roomSurfaceMaterial;

        private const string ShellObjectName = "__RoomThicknessShell";
        private static readonly Color SpriteBaseGreyTone = new Color(0.82f, 0.82f, 0.82f, 1f); // hous_wall_0 / hous_ground_0 평균 밝기

        private MaterialPropertyBlock _mpb;

        private static readonly int BaseColorId        = Shader.PropertyToID("_BaseColor");
        private static readonly int RoomLightColorId   = Shader.PropertyToID("_RoomLightColor");
        private static readonly int RoomLightIntId     = Shader.PropertyToID("_RoomLightIntensity");
        private static readonly int RoomAmbientColorId = Shader.PropertyToID("_RoomAmbientColor");
        private static readonly int RoomLightDirId     = Shader.PropertyToID("_RoomLightDir");
        private static readonly int RoomCornerMinId    = Shader.PropertyToID("_RoomCornerMin");
        private static readonly int RoomCornerMaxId    = Shader.PropertyToID("_RoomCornerMax");
        private static readonly int CornerDarknessId   = Shader.PropertyToID("_CornerDarkness");
        private static readonly int CornerRadiusId     = Shader.PropertyToID("_CornerRadius");
        private static readonly int ShadowColorId      = Shader.PropertyToID("_ShadowColor");
        private static readonly int SurfaceBrightId    = Shader.PropertyToID("_SurfaceBrightness");
        private static readonly int ZWriteId           = Shader.PropertyToID("_ZWrite");

        private void OnEnable()
        {
            RefreshAll();
        }

        private void OnValidate()
        {
            RefreshAll();
        }

        private void Update()
        {
            if (!Application.isPlaying)
                ApplyLightingAndColors();
        }

        /// <summary>
        /// 두께 메쉬 지오메트리와 조명·머티리얼 속성을 모두 새로고침합니다.
        /// </summary>
        [ContextMenu("Refresh Room Thickness & Lighting")]
        public void RefreshAll()
        {
            EnsureReferences();
            EnsureThicknessGeometry();
            ApplyLightingAndColors();
        }

        private void EnsureReferences()
        {
            if (roomRoot == null)
            {
                var found = GameObject.Find("room");
                if (found != null) roomRoot = found.transform;
            }

            if (sceneDirectionalLight == null)
            {
                var dirGo = GameObject.Find("Directional Light");
                if (dirGo != null) sceneDirectionalLight = dirGo.GetComponent<Light>();
                if (sceneDirectionalLight == null)
                    sceneDirectionalLight = FindFirstObjectByType<Light>();
            }
        }

        /// <summary>
        /// room 오브젝트 아래에 왼쪽 벽 두께, 뒷벽 두께, 바닥 두께 3D 큐브들을 생성·배치합니다.
        /// </summary>
        public void EnsureThicknessGeometry()
        {
            if (roomRoot == null) return;

            var shellTf = roomRoot.Find(ShellObjectName);
            if (shellTf == null)
            {
                var shellGo = new GameObject(ShellObjectName);
                shellGo.transform.SetParent(roomRoot, false);
                shellTf = shellGo.transform;
            }

            float widthX  = roomMaxCorner.x - roomMinCorner.x; // 5
            float heightY = roomMaxCorner.y - roomMinCorner.y; // 4
            float depthZ  = roomMaxCorner.z - roomMinCorner.z; // 5
            float wt = Mathf.Max(0.02f, wallThickness);
            float ft = Mathf.Max(0.02f, floorThickness);

            // 스프라이트 면(x=-2.5, z=+2.5, y=0)과 Z-Fighting이 일어나지 않도록 바깥쪽으로 미세 오프셋(0.004)
            const float eps = 0.004f;

            // 1. 왼쪽 벽(-X) 두께 블럭 (모서리 기둥까지 자연스럽게 감싸도록 Z축으로 wt만큼 연장)
            var leftWallBox = GetOrCreateBlock(shellTf, "WallThickness_Left");
            leftWallBox.localPosition = new Vector3(
                roomMinCorner.x - wt * 0.5f - eps,
                roomMinCorner.y + heightY * 0.5f,
                (roomMinCorner.z + roomMaxCorner.z + wt) * 0.5f);
            leftWallBox.localRotation = Quaternion.identity;
            leftWallBox.localScale = new Vector3(wt, heightY, depthZ + wt);

            // 2. 오른쪽 뒷벽(+Z) 두께 블럭
            var backWallBox = GetOrCreateBlock(shellTf, "WallThickness_Back");
            backWallBox.localPosition = new Vector3(
                (roomMinCorner.x + roomMaxCorner.x) * 0.5f,
                roomMinCorner.y + heightY * 0.5f,
                roomMaxCorner.z + wt * 0.5f + eps);
            backWallBox.localRotation = Quaternion.identity;
            backWallBox.localScale = new Vector3(widthX, heightY, wt);

            // 3. 바닥(-Y) 두께 블럭 (벽 두께 아래까지 빈틈없이 받쳐주는 구조)
            var floorBox = GetOrCreateBlock(shellTf, "FloorThickness_Slab");
            floorBox.localPosition = new Vector3(
                (roomMinCorner.x - wt + roomMaxCorner.x) * 0.5f,
                roomMinCorner.y - ft * 0.5f - eps,
                (roomMinCorner.z + roomMaxCorner.z + wt) * 0.5f);
            floorBox.localRotation = Quaternion.identity;
            floorBox.localScale = new Vector3(widthX + wt, ft, depthZ + wt);
        }

        private Transform GetOrCreateBlock(Transform parent, string blockName)
        {
            var child = parent.Find(blockName);
            if (child == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = blockName;
                go.transform.SetParent(parent, false);

                // 가구 배치 레이캐스트를 방해하지 않도록 콜라이더는 제거/비활성화
                var col = go.GetComponent<Collider>();
                if (col != null) col.enabled = false;

                child = go.transform;
            }

            if (roomSurfaceMaterial != null)
            {
                var mr = child.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != roomSurfaceMaterial)
                    mr.sharedMaterial = roomSurfaceMaterial;
            }

            return child;
        }

        public void ApplyLightingAndColors()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            // 1. 씬의 3D Directional Light 동기화 (배치된 3D 가구들도 똑같은 조명 색과 세기를 받도록 함)
            if (sceneDirectionalLight != null)
            {
                sceneDirectionalLight.color = roomLightColor;
                sceneDirectionalLight.intensity = roomLightIntensity;
                sceneDirectionalLight.shadows = LightShadows.Soft;
            }

            if (roomRoot == null) return;

            // 2. 방 내부 벽/바닥 SpriteRenderer 적용
            // (캐릭터(sortingOrder 10~120) 및 가구 프리뷰(30000)가 잘리지 않도록 sortingOrder=-10, ZWrite=0 유지)
            var spriteRenderers = roomRoot.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in spriteRenderers)
            {
                if (sr == null) continue;

                if (roomSurfaceMaterial != null && sr.sharedMaterial != roomSurfaceMaterial)
                    sr.sharedMaterial = roomSurfaceMaterial;

                sr.sortingOrder = -10;

                bool isFloor = sr.name.Contains("ground") || sr.name.Contains("floor");
                Color surfaceTint = isFloor ? flooringTint : wallpaperTint;
                float faceBrightness = isFloor ? 0.98f : (Mathf.Abs(sr.transform.eulerAngles.y) > 45f ? 0.94f : 1.0f);

                sr.GetPropertyBlock(_mpb);
                ApplyCommonShaderProps(_mpb, surfaceTint, faceBrightness, 0f);
                sr.SetPropertyBlock(_mpb);
            }

            // 3. 벽/바닥 두께(3D Cube) MeshRenderer 적용 (벽지·바닥재 색과 자연스럽게 이어지도록 동기화)
            var shellTf = roomRoot.Find(ShellObjectName);
            if (shellTf != null)
            {
                Color matchedWallColor = SpriteBaseGreyTone * wallpaperTint * thicknessToneRatio;
                matchedWallColor.a = 1f;

                Color matchedFloorColor = SpriteBaseGreyTone * flooringTint * (thicknessToneRatio * 0.96f);
                matchedFloorColor.a = 1f;

                var meshRenderers = shellTf.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var mr in meshRenderers)
                {
                    if (mr == null) continue;

                    if (roomSurfaceMaterial != null && mr.sharedMaterial != roomSurfaceMaterial)
                        mr.sharedMaterial = roomSurfaceMaterial;

                    mr.sortingOrder = -20;

                    bool isFloorSlab = mr.name.Contains("Floor");
                    Color slabColor = isFloorSlab ? matchedFloorColor : matchedWallColor;

                    mr.GetPropertyBlock(_mpb);
                    ApplyCommonShaderProps(_mpb, slabColor, 1.0f, 1f);
                    mr.SetPropertyBlock(_mpb);
                }
            }
        }

        private void ApplyCommonShaderProps(MaterialPropertyBlock block, Color baseColor, float faceBrightness, float zWrite)
        {
            block.SetColor(BaseColorId, baseColor);
            block.SetColor(RoomLightColorId, roomLightColor);
            block.SetFloat(RoomLightIntId, roomLightIntensity);
            block.SetColor(RoomAmbientColorId, ambientTint);
            block.SetVector(RoomLightDirId, new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0f));
            block.SetVector(RoomCornerMinId, new Vector4(roomMinCorner.x, roomMinCorner.y, roomMinCorner.z, 0f));
            block.SetVector(RoomCornerMaxId, new Vector4(roomMaxCorner.x, roomMaxCorner.y, roomMaxCorner.z, 0f));
            block.SetFloat(CornerDarknessId, cornerDarkness);
            block.SetFloat(CornerRadiusId, cornerRadius);
            block.SetColor(ShadowColorId, cornerShadowColor);
            block.SetFloat(SurfaceBrightId, faceBrightness);
            block.SetFloat(ZWriteId, zWrite);
        }
    }
}
