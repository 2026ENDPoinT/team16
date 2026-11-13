using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zzapmagochi.Core;
using Zzapmagochi.Room;

namespace Zzapmagochi.UI
{
    /// <summary>
    /// scene_survey 화면 컨트롤러.
    /// 1단계(인트로): "작은 자취방에서 대학생활을 시작해봐요 / 룸메이트는 어떤 성격일까요?"
    /// 2단계(8문항 설문): 대학생활/자취방 질문 8개를 통해 4가지 성격(MatePersonaType) 중 하나로 판정
    /// 3단계(결과 & 프로필 설정): 판정된 성격 확인 + 이름 지정 + 말버릇(옵션) 지정 후 scene_room 으로 이동
    /// </summary>
    public class SurveyController : MonoBehaviour
    {
        [Serializable]
        public struct SurveyOption
        {
            public string text;
            public MatePersonaType targetPersona;
        }

        [Serializable]
        public struct SurveyQuestion
        {
            public string questionText;
            public SurveyOption[] options;
        }

        [Header("1단계: 인트로 패널")]
        [SerializeField] private GameObject introPanel;
        [SerializeField] private Button startSurveyButton;

        [Header("2단계: 8문항 질문 패널")]
        [SerializeField] private GameObject questionPanel;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private Button[] optionButtons;
        [SerializeField] private TMP_Text[] optionLabels;

        [Header("3단계: 성격 판정 결과 & 이름/말버릇 지정 패널")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultPersonaTitleText;
        [SerializeField] private TMP_Text resultPersonaDescText;
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private TMP_InputField catchphraseInput;
        [SerializeField] private Button enterRoomButton;

        private SurveyQuestion[] _questions;
        private readonly int[] _scores = new int[4];
        private int _currentQuestionIndex;
        private MatePersonaType _judgedPersona = MatePersonaType.Diligent;

        private void Awake()
        {
            _questions = BuildDefaultQuestions();

            if (startSurveyButton != null)
                startSurveyButton.onClick.AddListener(OnStartSurveyClicked);

            if (optionButtons != null)
            {
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    int capturedIndex = i;
                    if (optionButtons[i] != null)
                        optionButtons[i].onClick.AddListener(() => OnOptionClicked(capturedIndex));
                }
            }

            if (enterRoomButton != null)
                enterRoomButton.onClick.AddListener(OnEnterRoomClicked);
        }

        private void Start()
        {
            ShowIntroStep();
        }

        private void ShowIntroStep()
        {
            if (introPanel != null) introPanel.SetActive(true);
            if (questionPanel != null) questionPanel.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(false);
        }

        private void OnStartSurveyClicked()
        {
            Array.Clear(_scores, 0, _scores.Length);
            _currentQuestionIndex = 0;

            if (introPanel != null) introPanel.SetActive(false);
            if (questionPanel != null) questionPanel.SetActive(true);
            if (resultPanel != null) resultPanel.SetActive(false);

            RefreshQuestionUI();
        }

        private void RefreshQuestionUI()
        {
            if (_currentQuestionIndex < 0 || _currentQuestionIndex >= _questions.Length)
                return;

            var q = _questions[_currentQuestionIndex];

            if (progressText != null)
                progressText.text = $"질문 {_currentQuestionIndex + 1} / {_questions.Length}";

            if (questionText != null)
                questionText.text = q.questionText;

            if (optionLabels != null && q.options != null)
            {
                for (int i = 0; i < optionLabels.Length && i < q.options.Length; i++)
                {
                    if (optionLabels[i] != null)
                        optionLabels[i].text = q.options[i].text;
                }
            }
        }

        private void OnOptionClicked(int optionIndex)
        {
            if (_currentQuestionIndex < 0 || _currentQuestionIndex >= _questions.Length)
                return;

            var q = _questions[_currentQuestionIndex];
            if (q.options != null && optionIndex >= 0 && optionIndex < q.options.Length)
            {
                var persona = q.options[optionIndex].targetPersona;
                _scores[(int)persona] += 2;

                // 마지막 8번째 질문은 타이브레이커(동점 방지)를 위해 +1 가중치 부여
                if (_currentQuestionIndex == _questions.Length - 1)
                    _scores[(int)persona] += 1;
            }

            _currentQuestionIndex++;

            if (_currentQuestionIndex < _questions.Length)
            {
                RefreshQuestionUI();
            }
            else
            {
                ShowResultStep();
            }
        }

        private void ShowResultStep()
        {
            _judgedPersona = EvaluateWinningPersona();

            if (introPanel != null) introPanel.SetActive(false);
            if (questionPanel != null) questionPanel.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(true);

            if (resultPersonaTitleText != null)
                resultPersonaTitleText.text = GetPersonaTitle(_judgedPersona);

            if (resultPersonaDescText != null)
                resultPersonaDescText.text = GetPersonaDescription(_judgedPersona);

            if (nicknameInput != null && string.IsNullOrWhiteSpace(nicknameInput.text))
                nicknameInput.text = "짭이";

            if (catchphraseInput != null)
                catchphraseInput.text = string.Empty;
        }

        private MatePersonaType EvaluateWinningPersona()
        {
            int bestIndex = 0;
            int bestScore = _scores[0];

            for (int i = 1; i < _scores.Length; i++)
            {
                if (_scores[i] > bestScore)
                {
                    bestScore = _scores[i];
                    bestIndex = i;
                }
            }

            return (MatePersonaType)bestIndex;
        }

        private void OnEnterRoomClicked()
        {
            string nickname = nicknameInput != null ? nicknameInput.text.Trim() : "짭이";
            if (string.IsNullOrEmpty(nickname)) nickname = "짭이";

            string catchphrase = catchphraseInput != null ? catchphraseInput.text.Trim() : string.Empty;

            MateProfileStore.SaveSurveyResult(nickname, _judgedPersona, catchphrase);
            SceneManager.LoadScene(SceneNames.Room);
        }

        private static string GetPersonaTitle(MatePersonaType persona) => persona switch
        {
            MatePersonaType.Diligent    => "📋 성실형 메이트",
            MatePersonaType.Cheerleader => "📣 응원단장 메이트",
            MatePersonaType.Tsundere    => "😼 츤데레 메이트",
            MatePersonaType.NightCoder  => "💻 새벽코딩 메이트",
            _                           => "📋 성실형 메이트"
        };

        private static string GetPersonaDescription(MatePersonaType persona) => persona switch
        {
            MatePersonaType.Diligent =>
                "계획표와 과제 마감을 꼼꼼하게 챙겨주는 든든한 모범생 룸메이트예요!\n" +
                "네가 지치지 않고 끝까지 완주할 수 있도록 옆에서 페이스를 맞춰줄 거예요.",
            MatePersonaType.Cheerleader =>
                "언제나 하이텐션! 사소한 할 일 하나만 끝내도 박수 백 번 쳐주는 에너자이저 룸메이트예요!\n" +
                "우울하거나 지칠 틈 없이 자취방 분위기를 밝게 만들어 줄 거예요.",
            MatePersonaType.Tsundere =>
                "말로는 틱틱대고 귀찮다면서 뒤에서는 네 과제와 컨디션을 제일 신경 쓰는 룸메이트예요!\n" +
                "딴짓할 땐 따끔하게 잔소리하고, 힘들 땐 슬쩍 위로를 건네줄 거예요.",
            MatePersonaType.NightCoder =>
                "새벽 공기와 모니터 불빛을 사랑하는 올빼미 개발자 룸메이트예요!\n" +
                "깃허브 스트릭과 밤샘 과제도 외롭지 않게 끝까지 옆자리를 지켜줄 거예요.",
            _ => ""
        };

        private static SurveyQuestion[] BuildDefaultQuestions()
        {
            return new[]
            {
                new SurveyQuestion
                {
                    questionText = "Q1. 새 학기 첫날 아침! 알람이 울릴 때 룸메이트의 모습은?",
                    options = new[]
                    {
                        new SurveyOption { text = "알람 울리기 10분 전에 이미 일어나 오늘 시간표를 점검하고 있다.", targetPersona = MatePersonaType.Diligent },
                        new SurveyOption { text = "\"좋은 아침!! 오늘도 완전 럭키한 하루가 될 거야!\" 하며 커튼을 활짝 연다.", targetPersona = MatePersonaType.Cheerleader },
                        new SurveyOption { text = "\"야, 알람 시끄러워. 빨리 안 일어나면 지각한다?\" 하며 깨워준다.", targetPersona = MatePersonaType.Tsundere },
                        new SurveyOption { text = "\"...어제 새벽 5시에 푸시하고 자서 아직 부팅 중이야...\" 하며 눈을 비빈다.", targetPersona = MatePersonaType.NightCoder }
                    }
                },
                new SurveyQuestion
                {
                    questionText = "Q2. eclass에 갑자기 어려운 과제가 올라왔을 때 룸메이트의 반응은?",
                    options = new[]
                    {
                        new SurveyOption { text = "마감일과 배점을 캘린더에 적고 오늘 해야 할 분량을 차근차근 나눈다.", targetPersona = MatePersonaType.Diligent },
                        new SurveyOption { text = "\"우리 둘이 힘 합치면 금방 끝내지! 다 하고 맛있는 거 먹으러 가자!\"", targetPersona = MatePersonaType.Cheerleader },
                        new SurveyOption { text = "\"하, 진짜 귀찮게 하네... 야, 너 막히는 부분 있으면 가져와 봐. 봐줄게.\"", targetPersona = MatePersonaType.Tsundere },
                        new SurveyOption { text = "조용히 헤드셋을 끼고 에너지 드링크를 따며 밤샘 디버깅 준비를 한다.", targetPersona = MatePersonaType.NightCoder }
                    }
                },
                new SurveyQuestion
                {
                    questionText = "Q3. 작은 자취방 안, 룸메이트의 책상 위는 어떤 느낌일까?",
                    options = new[]
                    {
                        new SurveyOption { text = "할 일 체크리스트와 형광펜, 전공책이 각 잡혀 깔끔하게 정리되어 있다.", targetPersona = MatePersonaType.Diligent },
                        new SurveyOption { text = "귀여운 마스코트 인형들과 응원 문구 포스트잇이 알록달록 붙어 있다.", targetPersona = MatePersonaType.Cheerleader },
                        new SurveyOption { text = "심플하고 무심해 보이는데 서랍 속에 네가 좋아하는 간식이 챙겨져 있다.", targetPersona = MatePersonaType.Tsundere },
                        new SurveyOption { text = "모니터 불빛 아래 기계식 키보드와 깃허브 잔디 위젯이 돌아가고 있다.", targetPersona = MatePersonaType.NightCoder }
                    }
                },
                new SurveyQuestion
                {
                    questionText = "Q4. 시험 기간, 늦은 밤까지 공부하다 지친 나를 본 룸메이트는?",
                    options = new[]
                    {
                        new SurveyOption { text = "\"지금은 컨디션 조절이 더 중요해. 오늘 목표는 채웠으니 푹 자자.\"", targetPersona = MatePersonaType.Diligent },
                        new SurveyOption { text = "\"오늘 진짜 고생 많았어!! 이 정도면 무조건 A+ 각이야, 최고최고!\"", targetPersona = MatePersonaType.Cheerleader },
                        new SurveyOption { text = "\"누가 그렇게 무리하래? ...여기 따뜻한 음료 타 왔으니까 마시고 자라.\"", targetPersona = MatePersonaType.Tsundere },
                        new SurveyOption { text = "\"나도 아직 안 자고 코딩 중이야. 새벽 감성으로 조금만 더 같이 달릴래?\"", targetPersona = MatePersonaType.NightCoder }
                    }
                },
                new SurveyQuestion
                {
                    questionText = "Q5. 자취방을 꾸밀 때 룸메이트가 가장 먼저 사고 싶어 하는 가구는?",
                    options = new[]
                    {
                        new SurveyOption { text = "학사 일정과 할 일을 한눈에 적어둘 수 있는 큼직한 캘린더 보드", targetPersona = MatePersonaType.Diligent },
                        new SurveyOption { text = "방 분위기를 확 살려주는 폭신하고 화사한 컬러 소파", targetPersona = MatePersonaType.Cheerleader },
                        new SurveyOption { text = "구석에서 편하게 쉴 수 있는 푹신한 침대 (근데 네 자리도 은근 비워둠)", targetPersona = MatePersonaType.Tsundere },
                        new SurveyOption { text = "어두운 방에서도 네온 초록빛으로 빛나는 깃허브 스트릭 전광판 액자", targetPersona = MatePersonaType.NightCoder }
                    }
                },
                new SurveyQuestion
                {
                    questionText = "Q6. 내가 과제를 미루고 스마트폰만 보고 있을 때 룸메이트의 한마디는?",
                    options = new[]
                    {
                        new SurveyOption { text = "\"오늘 마감인 할 일이 남아있어! 지금 시작하면 여유 있게 끝낼 수 있어.\"", targetPersona = MatePersonaType.Diligent },
                        new SurveyOption { text = "\"잠깐 에너지 충전 중이구나! 딱 10분만 더 쉬고 나랑 같이 시작하자!\"", targetPersona = MatePersonaType.Cheerleader },
                        new SurveyOption { text = "\"야, 너 또 딴짓하냐? 이러다 마감 5분 전에 울면서 제출하지 마라.\"", targetPersona = MatePersonaType.Tsundere },
                        new SurveyOption { text = "\"오늘 깃허브 잔디 아직 빈칸이던데... 자기 전에 1커밋은 심어야지?\"", targetPersona = MatePersonaType.NightCoder }
                    }
                },
                new SurveyQuestion
                {
                    questionText = "Q7. 주말에 오랜만에 여유 시간이 생겼다! 룸메이트가 제안하는 일은?",
                    options = new[]
                    {
                        new SurveyOption { text = "밀린 방 청소를 상쾌하게 끝내고 다음 주 일정을 미리 정리해 두기", targetPersona = MatePersonaType.Diligent },
                        new SurveyOption { text = "날씨도 좋은데 기분 전환 겸 학교 앞 맛집 탐방하러 나가기", targetPersona = MatePersonaType.Cheerleader },
                        new SurveyOption { text = "\"딱히 너랑 가고 싶은 건 아닌데, 마트 1+1 행사한다니까 같이 가든가.\"", targetPersona = MatePersonaType.Tsundere },
                        new SurveyOption { text = "재밌는 토이 프로젝트 아이디어를 이야기하며 해커톤 나가자고 꼬시기", targetPersona = MatePersonaType.NightCoder }
                    }
                },
                new SurveyQuestion
                {
                    questionText = "Q8. 마지막으로, 내가 가장 함께하고 싶은 단짝 룸메이트의 스타일은?",
                    options = new[]
                    {
                        new SurveyOption { text = "흔들리지 않게 옆에서 학습 습관을 잡아주는 성실한 페이스메이커", targetPersona = MatePersonaType.Diligent },
                        new SurveyOption { text = "언제나 내 편에서 아낌없는 칭찬과 응원을 쏟아주는 해피 바이러스", targetPersona = MatePersonaType.Cheerleader },
                        new SurveyOption { text = "겉으론 툴툴대도 뒤에서 살뜰히 챙겨주는 매력 만점 츤데레", targetPersona = MatePersonaType.Tsundere },
                        new SurveyOption { text = "늦은 밤에도 함께 모니터 앞을 지키며 공감해주는 코딩 메이트", targetPersona = MatePersonaType.NightCoder }
                    }
                }
            };
        }
    }
}
