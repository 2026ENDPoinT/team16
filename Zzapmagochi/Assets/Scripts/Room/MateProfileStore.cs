using UnityEngine;
using UnityEngine.SceneManagement;
using Zzapmagochi.Core;

namespace Zzapmagochi.Room
{
    /// <summary>
    /// 설문조사(scene_survey)에서 결정된 룸메이트의 성격, 이름, 말버릇(옵션)을 저장하고
    /// scene_room 의 DesktopMateController 에 전달하는 저장소.
    ///
    /// 회원가입 관련 스크립트(LoginController, SignUpPopup 등)를 절대 수정하지 않고도
    /// 회원가입 직후(Session.IsNewUser == true)에만 자동으로 scene_survey 로 분기하도록 처리합니다.
    /// </summary>
    public static class MateProfileStore
    {
        public const string SurveySceneName = "scene_survey";

        public static bool HasCustomProfile { get; private set; }
        public static string Nickname { get; private set; } = "짭이";
        public static MatePersonaType Persona { get; private set; } = MatePersonaType.Diligent;
        public static string Catchphrase { get; private set; } = string.Empty;

        private static bool _surveyCompletedForCurrentSession;
        private static string _lastStudentId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneRedirectHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 로그인 화면으로 돌아오면 새 세션을 위해 설문 완료 플래그 초기화
            if (scene.name == SceneNames.Login)
            {
                _surveyCompletedForCurrentSession = false;
                return;
            }

            // 다른 학번으로 새로 가입/로그인한 경우 플래그 초기화
            if (Session.StudentId != _lastStudentId)
            {
                _lastStudentId = Session.StudentId;
                _surveyCompletedForCurrentSession = false;
            }

            // 회원가입(Session.IsNewUser == true)으로 Room에 진입했을 때 아직 설문을 안 했다면
            // 회원가입/로그인 코드를 건드리지 않고 즉시 scene_survey로 분기!
            // (일반 로그인은 Session.IsNewUser == false 이므로 바로 scene_room 에 머무름)
            if (scene.name == SceneNames.Room &&
                Session.IsNewUser &&
                !_surveyCompletedForCurrentSession &&
                Application.CanStreamedLevelBeLoaded(SurveySceneName))
            {
                SceneManager.LoadScene(SurveySceneName);
            }
        }

        public static void SaveSurveyResult(string nickname, MatePersonaType persona, string catchphrase)
        {
            Nickname = string.IsNullOrWhiteSpace(nickname) ? "짭이" : nickname.Trim();
            Persona = persona;
            Catchphrase = string.IsNullOrWhiteSpace(catchphrase) ? string.Empty : catchphrase.Trim();
            HasCustomProfile = true;
            _surveyCompletedForCurrentSession = true;
        }

        public static void MarkSurveySkippedOrDone()
        {
            _surveyCompletedForCurrentSession = true;
        }

        /// <summary>
        /// 대사 끝에 말버릇(옵션)을 자연스럽게 붙여줍니다.
        /// </summary>
        public static string ApplyCatchphrase(string line)
        {
            if (string.IsNullOrWhiteSpace(Catchphrase) || string.IsNullOrWhiteSpace(line))
                return line;

            return $"{line} {Catchphrase}";
        }
    }
}
