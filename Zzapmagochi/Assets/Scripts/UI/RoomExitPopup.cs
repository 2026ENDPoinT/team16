using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zzapmagochi.UI
{
    /// <summary>
    /// 출입문을 클릭하면 뜨는 팝업.
    ///
    ///   버튼 1: 친구 자취방 방문 → 친구 목록 팝업을 연다
    ///   버튼 2: 무작위 자취방 방문 → 아직 기능 없음 (이벤트만 남겨 둔다)
    /// </summary>
    public class RoomExitPopup : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;

        [Header("버튼")]
        [SerializeField] private Button friendVisitButton;
        [SerializeField] private Button randomVisitButton;
        [SerializeField] private Button closeButton;

        [Header("연결")]
        [SerializeField] private FriendListPopup friendListPopup;

        [Tooltip("아직 구현되지 않은 기능을 눌렀을 때 띄울 문구")]
        [SerializeField] private TMP_Text noticeText;

        /// <summary>
        /// 무작위 방문을 눌렀을 때 알린다.
        /// 구현이 생기면 여기에 붙이면 되고, 그때 아래 안내 문구는 지운다.
        /// GET /v1/room/random (API문서.md 5.6) 에 해당한다.
        /// </summary>
        public event Action RandomVisitRequested;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            if (friendVisitButton != null) friendVisitButton.onClick.AddListener(OnFriendVisitClicked);
            if (randomVisitButton != null) randomVisitButton.onClick.AddListener(OnRandomVisitClicked);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            // Awake 에서 Hide() 를 부르지 않는다.
            // 이 컴포넌트는 자기가 숨기는 root 위에 올라가 있어서,
            // Show() 로 활성화되는 순간 Awake 가 돌아 다시 숨겨 버린다.
            // 초기 숨김은 씬에서 비활성으로 저장해 두는 것으로 해결한다.
        }

        public void Show()
        {
            if (noticeText != null) noticeText.text = string.Empty;
            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Hide();
            else Show();
        }

        private void OnFriendVisitClicked()
        {
            if (friendListPopup == null)
            {
                SetNotice("친구 목록을 열 수 없습니다.");
                return;
            }

            // 출입문 팝업은 닫고 친구 목록으로 넘긴다.
            Hide();
            friendListPopup.Show();
        }

        private void OnRandomVisitClicked()
        {
            RandomVisitRequested?.Invoke();

            // 붙은 구현이 없으면 아무 일도 안 일어난 것처럼 보이므로 상태를 알려 준다.
            if (RandomVisitRequested == null)
                SetNotice("무작위 방문은 준비 중입니다.");
        }

        private void SetNotice(string message)
        {
            if (noticeText != null) noticeText.text = message;
            else Debug.Log($"[RoomExitPopup] {message}");
        }
    }
}
