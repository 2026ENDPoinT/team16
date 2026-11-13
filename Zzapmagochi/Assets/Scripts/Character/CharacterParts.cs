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

    [Header("동작")]
    [Tooltip("켜면 걷기/대기 포즈를 자동으로 번갈아 보여준다. 꾸미기 미리보기에서는 끈다.")]
    [SerializeField] private bool autoWalkLoop = true;

    [Tooltip("걷기/대기 전환 간격(초)")]
    [SerializeField] private float walkLoopInterval = 0.75f;

    [Tooltip("이동 중 걸음 사이클 간격(초). 걷기 포즈와 대기 포즈를 이 간격으로 번갈아 보여준다.")]
    [SerializeField] private float walkCycleInterval = 0.22f;

    /// <summary>생성된 파츠. 키는 CharacterBody 의 필드 이름(body, head, hair_front ...).</summary>
    private readonly Dictionary<string, SpriteRenderer> _renderers =
        new Dictionary<string, SpriteRenderer>();

    /// <summary>_walk / _idle 로 끝나는 파츠는 한 번에 하나만 보여야 한다.</summary>
    private const string WalkSuffix = "_walk";
    private const string IdleSuffix = "_idle";

    private bool _walking;
    private Coroutine _walkRoutine;

    /// <summary>이동 중 걸음 사이클. 제자리에 서면 멈춘다.</summary>
    private Coroutine _cycleRoutine;

    public IReadOnlyDictionary<string, SpriteRenderer> Renderers => _renderers;
    public bool IsWalking => _walking;

    private void Start()
    {
        BuildParts();

        if (autoWalkLoop)
            _walkRoutine = StartCoroutine(Walk());
        else
            SetWalking(false);
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

            // 스프라이트가 비어 있어도 오브젝트는 만들어 둔다.
            // 꾸미기에서 나중에 장착할 수 있어야 하므로, 비어 있으면 렌더러만 꺼 둔다.
            CreatePart(field.Name, part);
        }

        SetWalking(_walking);
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
        ApplyVisibility(partName, renderer);
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
        _walking = walking;

        foreach (var pair in _renderers)
            ApplyVisibility(pair.Key, pair.Value);
    }

    /// <summary>
    /// 파츠가 보여야 하는지 한 곳에서 정한다.
    /// 기준은 둘이다 — 스프라이트가 있는가, 그리고 지금 포즈에 맞는 파츠인가.
    /// </summary>
    private void ApplyVisibility(string partName, SpriteRenderer renderer)
    {
        if (renderer == null) return;

        if (renderer.sprite == null)
        {
            renderer.enabled = false;
            return;
        }

        if (partName.EndsWith(WalkSuffix, StringComparison.Ordinal))
            renderer.enabled = _walking;
        else if (partName.EndsWith(IdleSuffix, StringComparison.Ordinal))
            renderer.enabled = !_walking;
        else
            renderer.enabled = true;
    }

    /// <summary>
    /// 파츠 하나의 스프라이트를 교체한다. null 을 넣으면 그 파츠를 벗는다.
    /// 꾸미기 시스템이 이 메서드로 장착/해제를 처리한다.
    /// </summary>
    public void SetSprite(string partName, Sprite sprite)
    {
        if (!_renderers.TryGetValue(partName, out var renderer) || renderer == null) return;

        renderer.sprite = sprite;
        ApplyVisibility(partName, renderer);
    }

    /// <summary>현재 그 파츠에 들어가 있는 스프라이트. 없으면 null.</summary>
    public Sprite GetSprite(string partName)
    {
        return _renderers.TryGetValue(partName, out var renderer) && renderer != null
            ? renderer.sprite
            : null;
    }

    /// <summary>파츠 하나의 SpriteRenderer 를 돌려준다. 없으면 null.</summary>
    public SpriteRenderer GetPart(string partName)
    {
        return _renderers.TryGetValue(partName, out var renderer) ? renderer : null;
    }

    /// <summary>걷기 루프를 켜고 끈다. 꾸미기 미리보기에서는 꺼서 대기 포즈로 고정한다.</summary>
    public void SetAutoWalkLoop(bool enabled)
    {
        autoWalkLoop = enabled;

        if (!enabled)
        {
            if (_walkRoutine != null)
            {
                StopCoroutine(_walkRoutine);
                _walkRoutine = null;
            }

            SetWalking(false);
            return;
        }

        if (_walkRoutine == null && isActiveAndEnabled)
            _walkRoutine = StartCoroutine(Walk());
    }

    /// <summary>
    /// 이동 중 걸음 사이클을 시작한다.
    /// 걷기 포즈 한 장으로 고정하면 미끄러지듯 보이므로,
    /// shoe_walk/shoe_idle · cloth_bottom_walk/cloth_bottom_idle · leg_walk/leg_idle 을
    /// 일정 간격으로 번갈아 보여 준다.
    /// </summary>
    public void StartWalkCycle()
    {
        if (_cycleRoutine != null || !isActiveAndEnabled) return;
        _cycleRoutine = StartCoroutine(WalkCycle());
    }

    /// <summary>걸음 사이클을 멈추고 대기 포즈로 되돌린다.</summary>
    public void StopWalkCycle()
    {
        if (_cycleRoutine != null)
        {
            StopCoroutine(_cycleRoutine);
            _cycleRoutine = null;
        }

        SetWalking(false);
    }

    private IEnumerator WalkCycle()
    {
        var interval = Mathf.Max(0.02f, walkCycleInterval);

        while (true)
        {
            SetWalking(true);
            yield return new WaitForSeconds(interval);
            SetWalking(false);
            yield return new WaitForSeconds(interval);
        }
    }

    private void OnDisable()
    {
        // 비활성화되면 유니티가 코루틴을 멈추므로 핸들을 비워 둔다.
        // 안 그러면 다시 켰을 때 StartWalkCycle 이 이미 돈다고 착각한다.
        _cycleRoutine = null;
        _walkRoutine = null;
    }

    private IEnumerator Walk()
    {
        while (true)
        {
            SetWalking(true);
            yield return new WaitForSeconds(walkLoopInterval);
            SetWalking(false);
            yield return new WaitForSeconds(walkLoopInterval);
        }
    }
}
