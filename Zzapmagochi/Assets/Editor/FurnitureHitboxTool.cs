using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zzapmagochi.Room;

namespace Zzapmagochi.EditorTools
{
    /// <summary>
    /// 가구 프리팹에 클릭용 투명 히트박스를 넣는다.
    ///
    /// 가구가 메시에서 스프라이트로 바뀌면서 콜라이더가 사라져 제거 모드의 클릭이 먹히지 않는다.
    /// 스프라이트는 렌더러만 있고 충돌체가 없어 Physics.Raycast 에 걸리지 않기 때문이다.
    ///
    ///   Unity.exe -batchmode -quit -projectPath &lt;경로&gt; \
    ///     -executeMethod Zzapmagochi.EditorTools.FurnitureHitboxTool.AddHitboxesToAll
    ///
    /// 크기는 FurnitureDefinition.size 가 아니라 <b>프리팹의 실제 렌더러 바운즈</b>에서 뽑는다.
    /// 프리팹마다 루트 피벗이 달라서(침대는 루트가 앞쪽 모서리에 있다)
    /// 그리드 크기로 계산하면 실제 그림과 어긋난다.
    /// </summary>
    public static class FurnitureHitboxTool
    {
        private const string HitboxName = "Hitbox";
        private const string DataDir = "Assets/Data/Furniture";

        /// <summary>너무 얇은 가구(액자 등)도 집을 수 있도록 보장하는 최소 두께(유닛).</summary>
        private const float MinThickness = 0.2f;

        [MenuItem("Zzapmagochi/Furniture/Add Click Hitboxes")]
        public static void AddHitboxesToAll()
        {
            var guids = AssetDatabase.FindAssets("t:FurnitureDefinition", new[] { DataDir });
            var done = 0;

            foreach (var guid in guids)
            {
                var definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));

                if (definition == null || definition.prefab == null)
                {
                    Debug.LogWarning($"[Hitbox] 프리팹이 없는 가구: {definition?.id}");
                    continue;
                }

                if (AddHitbox(definition)) done++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Hitbox] 히트박스 적용 완료: {done}개");
        }

        private static bool AddHitbox(FurnitureDefinition definition)
        {
            var path = AssetDatabase.GetAssetPath(definition.prefab);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"[Hitbox] 프리팹 경로를 찾지 못함: {definition.id}");
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                if (!TryGetLocalBounds(root, out var bounds))
                {
                    Debug.LogWarning($"[Hitbox] 렌더러가 없어 건너뜀: {definition.id}");
                    return false;
                }

                var hitboxTransform = root.transform.Find(HitboxName);
                var hitbox = hitboxTransform != null
                    ? hitboxTransform.gameObject
                    : new GameObject(HitboxName);

                hitbox.transform.SetParent(root.transform, false);
                hitbox.transform.localPosition = Vector3.zero;
                hitbox.transform.localRotation = Quaternion.identity;
                hitbox.transform.localScale = Vector3.one;

                // ?? 를 쓰면 안 된다. 유니티 오브젝트는 "파괴됨/없음"을 가짜 null 로 돌려주는데
                // ?? 는 진짜 참조 null 만 보므로 없는 컴포넌트를 그대로 통과시킨다.
                var collider = hitbox.GetComponent<BoxCollider>();
                if (collider == null) collider = hitbox.AddComponent<BoxCollider>();

                // 얇은 축이 있으면 클릭이 거의 불가능하므로 최소 두께를 보장한다.
                var size = bounds.size;
                size.x = Mathf.Max(size.x, MinThickness);
                size.y = Mathf.Max(size.y, MinThickness);
                size.z = Mathf.Max(size.z, MinThickness);

                collider.center = bounds.center;
                collider.size = size;

                // 트리거로 둔다. 캐릭터 이동이나 물리에 끼어들면 안 되고,
                // 클릭 판정은 QueryTriggerInteraction.Collide 로 트리거까지 훑는다.
                collider.isTrigger = true;

                PrefabUtility.SaveAsPrefabAsset(root, path);

                Debug.Log($"[Hitbox] {definition.id}: center={collider.center} size={collider.size}");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>프리팹 루트 기준 로컬 공간의 렌더러 합산 바운즈.</summary>
        private static bool TryGetLocalBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;

            var renderers = new List<Renderer>(root.GetComponentsInChildren<Renderer>(true));
            if (renderers.Count == 0) return false;

            var found = false;
            var rootInverse = root.transform.worldToLocalMatrix;

            foreach (var renderer in renderers)
            {
                // 히트박스 자신은 렌더러가 없지만, 혹시 모를 경우를 위해 걸러 둔다.
                if (renderer.transform.name == HitboxName) continue;

                var world = renderer.bounds;
                if (world.size == Vector3.zero) continue;

                // 월드 바운즈의 여덟 꼭짓점을 루트 로컬로 옮겨 다시 감싼다.
                var min = world.min;
                var max = world.max;

                for (var i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? min.x : max.x,
                        (i & 2) == 0 ? min.y : max.y,
                        (i & 4) == 0 ? min.z : max.z);

                    var local = rootInverse.MultiplyPoint3x4(corner);

                    if (!found)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        found = true;
                    }
                    else
                    {
                        bounds.Encapsulate(local);
                    }
                }
            }

            return found;
        }
    }
}
