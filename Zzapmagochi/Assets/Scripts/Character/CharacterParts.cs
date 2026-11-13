using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[Serializable]
public struct CharacterPart
{
    public int order;
    public Sprite parts;
}

[Serializable]
public struct CharacterBody
{
    public CharacterPart outer;
    public CharacterPart accessories;
    public CharacterPart shoe_walk;
    public CharacterPart shoe_idle;
    public CharacterPart cloth_top;
    public CharacterPart cloth_bottom_walk;
    public CharacterPart cloth_bottom_idle;
    public CharacterPart hair_front;
    public CharacterPart hair_back;
    public CharacterPart mouth;
    public CharacterPart eye;
    public CharacterPart leg_walk;
    public CharacterPart leg_idle;
    public CharacterPart arm;
    public CharacterPart head;
    public CharacterPart body;
}

public class CharacterParts : MonoBehaviour
{
    [SerializeField]
    public CharacterBody characterBody;

    [Header("정렬")]
    [Tooltip("생성되는 모든 파츠가 쓸 Sorting Layer 이름")]
    [SerializeField] private string sortingLayerName = "Default";

    [Tooltip("각 파츠의 order 에 더해지는 값. 캐릭터 전체를 다른 오브젝트 앞뒤로 밀 때 쓴다.")]
    [SerializeField] private int sortingOrderOffset = 0;

    /// <summary>생성된 파츠. 키는 CharacterBody 의 필드 이름(body, head, hair_front ...).</summary>
    private readonly Dictionary<string, SpriteRenderer> _renderers =
        new Dictionary<string, SpriteRenderer>();

    /// <summary>_walk / _idle 로 끝나는 파츠는 한 번에 하나만 보여야 한다.</summary>
    private const string WalkSuffix = "_walk";
    private const string IdleSuffix = "_idle";

    public IReadOnlyDictionary<string, SpriteRenderer> Renderers => _renderers;

    private void Start()
    {
        BuildParts();
        StartCoroutine(Walk());
    }

    /// <summary>
    /// characterBody 의 각 필드마다 자식 GameObject 를 만들고
    /// SpriteRenderer 에 스프라이트와 sorting order 를 적용한다.
    /// </summary>
    public void BuildParts()
    {
        ClearParts();

        // 필드를 직접 나열하지 않고 리플렉션으로 순회한다.
        // CharacterBody 에 파츠를 추가해도 이 코드는 고치지 않아도 된다.
        var fields = typeof(CharacterBody).GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            if (field.FieldType != typeof(CharacterPart)) continue;

            var part = (CharacterPart)field.GetValue(characterBody);

            // 스프라이트가 비어 있는 파츠는 건너뛴다.
            // (액세서리를 끼지 않은 캐릭터처럼 비어 있는 것이 정상인 경우가 있다)
            if (part.parts == null) continue;

            CreatePart(field.Name, part);
        }
    }

    private void CreatePart(string partName, CharacterPart part)
    {
        var go = new GameObject(partName);
        go.transform.SetParent(transform, false);

        // 부모의 회전(아이소메트릭 각도)을 그대로 따르도록 로컬 값은 기본값으로 둔다.
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = part.parts;
        renderer.sortingOrder = part.order + sortingOrderOffset;

        if (!string.IsNullOrEmpty(sortingLayerName))
            renderer.sortingLayerName = sortingLayerName;

        _renderers[partName] = renderer;
    }

    private void ClearParts()
    {
        foreach (var pair in _renderers)
        {
            if (pair.Value == null) continue;

            if (Application.isPlaying)
                Destroy(pair.Value.gameObject);
            else
                DestroyImmediate(pair.Value.gameObject);
        }

        _renderers.Clear();
    }

    /// <summary>
    /// 걷기 / 대기 파츠를 전환한다.
    /// shoe_walk 와 shoe_idle 처럼 쌍으로 된 파츠는 동시에 보이면 겹쳐서 깨져 보인다.
    /// </summary>
    public void SetWalking(bool walking)
    {
        foreach (var pair in _renderers)
        {
            if (pair.Value == null) continue;

            if (pair.Key.EndsWith(WalkSuffix, StringComparison.Ordinal))
                pair.Value.enabled = walking;
            else if (pair.Key.EndsWith(IdleSuffix, StringComparison.Ordinal))
                pair.Value.enabled = !walking;
        }
    }

    /// <summary>파츠 하나의 스프라이트를 교체한다. 꾸미기 기능에서 쓴다.</summary>
    public void SetSprite(string partName, Sprite sprite)
    {
        if (_renderers.TryGetValue(partName, out var renderer) && renderer != null)
            renderer.sprite = sprite;
    }

    /// <summary>파츠 하나의 SpriteRenderer 를 돌려준다. 없으면 null.</summary>
    public SpriteRenderer GetPart(string partName)
    {
        return _renderers.TryGetValue(partName, out var renderer) ? renderer : null;
    }

    private void Update() {
    }

    IEnumerator Walk() {
        while (true) {
            SetWalking(true);
            yield return new WaitForSeconds(0.75f);
            SetWalking(false);
            yield return new WaitForSeconds(0.75f);
        }
    }
}
