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

            Hide();
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
