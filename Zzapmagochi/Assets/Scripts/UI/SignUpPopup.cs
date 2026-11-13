using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zzapmagochi.Core;
using Zzapmagochi.Net;

namespace Zzapmagochi.UI
{
    /// <summary>
    /// 회원가입 팝업.
    /// 가입 버튼은 (아이디 중복확인 통과) + (비밀번호 비어있지 않음) + (재입력 일치) 일 때만 활성화된다.
    /// 아이디를 수정하면 중복확인 결과는 무효가 된다.
    /// </summary>
    public class SignUpPopup : MonoBehaviour
    {
        [SerializeField] private GameObject root;

        [Header("아이디")]
        [SerializeField] private TMP_InputField idInput;
        [SerializeField] private Button checkIdButton;
        [SerializeField] private TMP_Text idStatusText;

        [Header("비밀번호")]
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private TMP_InputField passwordConfirmInput;
        [SerializeField] private TMP_Text passwordStatusText;

        [Header("동작")]
        [SerializeField] private Button submitButton;
        [SerializeField] private Button cancelButton;

        private static readonly Color Ok = new Color(0.18f, 0.65f, 0.33f);
        private static readonly Color Error = new Color(0.82f, 0.24f, 0.24f);

        private bool _idChecked;
        private string _checkedId;
        private bool _busy;

        /// <summary>가입 성공 시 호출된다. 인자는 가입한 학번.</summary>
        public event Action<string> SignedUp;

        /// <summary>가입 실패를 사용자에게 알려야 할 때 호출된다.</summary>
        public event Action<string> Failed;

        private void Awake()
        {
            if (checkIdButton != null) checkIdButton.onClick.AddListener(OnCheckIdClicked);
            if (submitButton != null) submitButton.onClick.AddListener(OnSubmitClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(Hide);

            if (idInput != null) idInput.onValueChanged.AddListener(_ => OnIdChanged());
            if (passwordInput != null) passwordInput.onValueChanged.AddListener(_ => RefreshPasswordStatus());
            if (passwordConfirmInput != null) passwordConfirmInput.onValueChanged.AddListener(_ => RefreshPasswordStatus());
            // Awake 에서 Hide() 를 부르지 않는다.
            // 이 컴포넌트는 자기가 숨기는 root 위에 올라가 있어서,
            // Show() 로 활성화되는 순간 Awake 가 돌아 다시 숨겨 버린다.
            // 초기 숨김은 씬에서 비활성으로 저장해 두는 것으로 해결한다.
        }

        public void Show()
        {
            ResetFields();
            if (root != null) root.SetActive(true);
            if (idInput != null) idInput.Select();
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        private void ResetFields()
        {
            if (idInput != null) idInput.text = string.Empty;
            if (passwordInput != null) passwordInput.text = string.Empty;
            if (passwordConfirmInput != null) passwordConfirmInput.text = string.Empty;

            _idChecked = false;
            _checkedId = null;
            _busy = false;

            SetStatus(idStatusText, string.Empty, Ok);
            SetStatus(passwordStatusText, string.Empty, Error);
            RefreshSubmitState();
        }

        private void OnIdChanged()
        {
            // 중복확인 후 아이디를 바꾸면 확인 결과를 버린다.
            if (_idChecked && idInput.text != _checkedId)
            {
                _idChecked = false;
                SetStatus(idStatusText, "아이디를 다시 확인해 주세요.", Error);
            }

            RefreshSubmitState();
        }

        private async void OnCheckIdClicked()
        {
            if (_busy) return;

            var id = idInput != null ? idInput.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(id))
            {
                SetStatus(idStatusText, "아이디를 입력해 주세요.", Error);
                return;
            }

            SetBusy(true);
            SetStatus(idStatusText, "확인 중...", Ok);

            try
            {
                var result = await Api.Current.CheckStudentId(id);

                if (result.isAvailable)
                {
                    _idChecked = true;
                    _checkedId = id;
                    SetStatus(idStatusText, "사용 가능한 아이디입니다.", Ok);
                }
                else
                {
                    _idChecked = false;
                    SetStatus(idStatusText, "이미 사용 중인 아이디입니다.", Error);
                }
            }
            catch (ApiException e)
            {
                _idChecked = false;
                SetStatus(idStatusText, e.Message, Error);
            }
            catch (Exception)
            {
                _idChecked = false;
                SetStatus(idStatusText, "서버에 연결할 수 없습니다.", Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void RefreshPasswordStatus()
        {
            var pw = passwordInput != null ? passwordInput.text : string.Empty;
            var confirm = passwordConfirmInput != null ? passwordConfirmInput.text : string.Empty;

            if (string.IsNullOrEmpty(confirm))
                SetStatus(passwordStatusText, string.Empty, Error);
            else if (pw != confirm)
                SetStatus(passwordStatusText, "비밀번호가 일치하지 않습니다.", Error);
            else
                SetStatus(passwordStatusText, "비밀번호가 일치합니다.", Ok);

            RefreshSubmitState();
        }

        private bool IsFormValid()
        {
            var pw = passwordInput != null ? passwordInput.text : string.Empty;
            var confirm = passwordConfirmInput != null ? passwordConfirmInput.text : string.Empty;

            return _idChecked
                   && !string.IsNullOrEmpty(pw)
                   && pw == confirm;
        }

        private void RefreshSubmitState()
        {
            if (submitButton != null)
                submitButton.interactable = !_busy && IsFormValid();
        }

        private async void OnSubmitClicked()
        {
            if (_busy || !IsFormValid()) return;

            var id = idInput.text.Trim();
            var pw = passwordInput.text;

            SetBusy(true);

            try
            {
                var auth = await Api.Current.SignUp(id, pw);
                Session.Set(id, auth);
                SignedUp?.Invoke(id);
            }
            catch (ApiException e)
            {
                SetBusy(false);
                Failed?.Invoke(e.Message);
            }
            catch (Exception)
            {
                SetBusy(false);
                Failed?.Invoke("서버에 연결할 수 없습니다. 잠시 후 다시 시도해 주세요.");
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;

            if (checkIdButton != null) checkIdButton.interactable = !busy;
            if (cancelButton != null) cancelButton.interactable = !busy;
            if (idInput != null) idInput.interactable = !busy;
            if (passwordInput != null) passwordInput.interactable = !busy;
            if (passwordConfirmInput != null) passwordConfirmInput.interactable = !busy;

            RefreshSubmitState();
        }

        private static void SetStatus(TMP_Text target, string message, Color color)
        {
            if (target == null) return;
            target.text = message;
            target.color = color;
        }
    }
}
