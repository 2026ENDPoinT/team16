using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zzapmagochi.Core;
using Zzapmagochi.Net;

namespace Zzapmagochi.UI
{
    /// <summary>
    /// scene_login 의 화면 로직.
    /// 로그인 또는 회원가입이 성공하면 scene_room 으로 이동한다.
    /// </summary>
    public class LoginController : MonoBehaviour
    {
        [Header("입력")]
        [SerializeField] private TMP_InputField idInput;
        [SerializeField] private TMP_InputField passwordInput;

        [Header("버튼")]
        [SerializeField] private Button loginButton;
        [SerializeField] private Button signUpButton;

        [Header("팝업")]
        [SerializeField] private MessagePopup failPopup;
        [SerializeField] private SignUpPopup signUpPopup;

        private bool _busy;

        private void Awake()
        {
            if (loginButton != null) loginButton.onClick.AddListener(OnLoginClicked);
            if (signUpButton != null) signUpButton.onClick.AddListener(OnSignUpClicked);

            if (signUpPopup != null)
            {
                signUpPopup.SignedUp += OnSignedUp;
                signUpPopup.Failed += ShowFailure;
            }

            if (passwordInput != null)
                passwordInput.onSubmit.AddListener(_ => OnLoginClicked());
        }

        private void OnDestroy()
        {
            if (signUpPopup != null)
            {
                signUpPopup.SignedUp -= OnSignedUp;
                signUpPopup.Failed -= ShowFailure;
            }
        }

        private async void OnLoginClicked()
        {
            if (_busy) return;

            var id = idInput != null ? idInput.text.Trim() : string.Empty;
            var pw = passwordInput != null ? passwordInput.text : string.Empty;

            SetBusy(true);

            try
            {
                var auth = await Api.Current.Login(id, pw);
                Session.Set(id, auth);
                GoToRoom();
            }
            catch (ApiException e)
            {
                SetBusy(false);
                ShowFailure(e.Message);
            }
            catch (Exception)
            {
                SetBusy(false);
                ShowFailure("서버에 연결할 수 없습니다. 잠시 후 다시 시도해 주세요.");
            }
        }

        private void OnSignUpClicked()
        {
            if (_busy) return;
            if (signUpPopup != null) signUpPopup.Show();
        }

        private void OnSignedUp(string studentId)
        {
            GoToRoom();
        }

        private void ShowFailure(string message)
        {
            if (failPopup != null)
                failPopup.Show(message, "로그인 실패");
            else
                Debug.LogWarning($"[Login] {message}");
        }

        private void GoToRoom()
        {
            SceneManager.LoadScene(SceneNames.Room);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;

            if (loginButton != null) loginButton.interactable = !busy;
            if (signUpButton != null) signUpButton.interactable = !busy;
            if (idInput != null) idInput.interactable = !busy;
            if (passwordInput != null) passwordInput.interactable = !busy;
        }
    }
}
