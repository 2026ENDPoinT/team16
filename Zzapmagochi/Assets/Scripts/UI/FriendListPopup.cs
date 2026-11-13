using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zzapmagochi.UI
{
    /// <summary>
    /// 친구 목록 팝업.
    ///
    /// 아직 친구 기능이 없어 목록은 비어 있다.
    /// 항목이 들어갈 자리(itemContainer)와 안내 문구만 만들어 두고,
    /// 서버의 GET /v1/friends 가 붙으면 여기에 채우면 된다 (API문서.md 8.1).
    /// </summary>
    public class FriendListPopup : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;

        [Tooltip("친구 항목이 들어갈 자리. 지금은 비어 있다.")]
        [SerializeField] private Transform itemContainer;

        [Tooltip("목록이 비었을 때 보여줄 문구")]
        [SerializeField] private GameObject emptyNotice;

        [SerializeField] private Button closeButton;

        public Transform ItemContainer => itemContainer;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            // Awake 에서 Hide() 를 부르지 않는다.
            // 이 컴포넌트는 자기가 숨기는 root 위에 올라가 있어서,
            // Show() 로 활성화되는 순간 Awake 가 돌아 다시 숨겨 버린다.
            // 초기 숨김은 씬에서 비활성으로 저장해 두는 것으로 해결한다.
        }

        public void Show()
        {
            if (root != null) root.SetActive(true);
            RefreshEmptyNotice();
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        /// <summary>항목을 채운 뒤 호출해 "비어 있음" 문구를 갱신한다.</summary>
        public void RefreshEmptyNotice()
        {
            if (emptyNotice == null) return;

            var hasItems = itemContainer != null && itemContainer.childCount > 0;
            emptyNotice.SetActive(!hasItems);
        }
    }
}
