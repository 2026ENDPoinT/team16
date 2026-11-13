using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zzapmagochi.UI
{
    /// <summary>제목 + 본문 + 확인 버튼짜리 단순 팝업. 로그인 실패 알림에 쓴다.</summary>
    public class MessagePopup : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button confirmButton;

        private void Awake()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(Hide);
            // Awake 에서 Hide() 를 부르지 않는다.
            // 이 컴포넌트는 자기가 숨기는 root 위에 올라가 있어서,
            // Show() 로 활성화되는 순간 Awake 가 돌아 다시 숨겨 버린다.
            // 초기 숨김은 씬에서 비활성으로 저장해 두는 것으로 해결한다.
        }

        public void Show(string message, string title = "알림")
        {
            if (titleText != null) titleText.text = title;
            if (messageText != null) messageText.text = message;
            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
