using System;
using UnityEngine;
///절차적 텍스처 & 이미지 스트림 변환기)

///스트릭 데이터를 받아 Texture2D, Sprite, byte[] (PNG 스트림)으로 변환합니다.

///좌표계 보정: 웹(GitHub)은 좌상단 (0, 0)이 일요일이지만, 유니티 Texture2D의 UV 좌표계는 좌하단 (0, 0)이 기준이므로 Y축 반전(6 - dayOfWeek)을 수행하여 상하 반전을 방지합니다.

namespace DormTamagotchi.GitHub
{
    [Serializable]
    public struct GrassThemePalette
    {
        public Color backgroundColor;
        public Color level0Color;
        public Color level1Color;
        public Color level2Color;
        public Color level3Color;
        public Color level4Color;

        // 깃허브 다크모드 기본 팔레트 (스케치의 검은 배경 잔디판과 동일한 톤)
        public static GrassThemePalette GitHubDark => new GrassThemePalette
        {
            backgroundColor = new Color32(13, 17, 23, 255),   // #0d1117
            level0Color     = new Color32(22, 27, 34, 255),   // #161b22
            level1Color     = new Color32(14, 68, 41, 255),   // #0e4429
            level2Color     = new Color32(0, 109, 50, 255),   // #006d32
            level3Color     = new Color32(38, 166, 65, 255),  // #26a641
            level4Color     = new Color32(57, 211, 83, 255)   // #39d353
        };

        public Color GetColor(ContributionLevel level) => level switch
        {
            ContributionLevel.FirstQuarter  => level1Color,
            ContributionLevel.SecondQuarter => level2Color,
            ContributionLevel.ThirdQuarter  => level3Color,
            ContributionLevel.FourthQuarter => level4Color,
            _                               => level0Color
        };
    }

    public static class GitHubStreakRenderer
    {
        /// <summary>
        /// 스트릭 데이터로부터 도트/픽셀 스타일 Texture2D, Sprite, PNG 바이트 스트림을 생성합니다.
        /// </summary>
        /// <param name="payload">파싱된 스트릭 데이터</param>
        /// <param name="cellSize">잔디 한 칸의 픽셀 크기 (기본 4px)</param>
        /// <param name="gap">칸 사이 여백 픽셀 (기본 1px)</param>
        /// <param name="padding">외곽 테두리 여백 픽셀 (기본 4px)</param>
        /// <param name="pixelsPerUnit">유니티 월드 스케일 대응 PPU</param>
        public static GitHubStreakBundle CreateBundle(
            GitHubStreakPayload payload,
            GrassThemePalette palette,
            int cellSize = 4,
            int gap = 1,
            int padding = 4,
            float pixelsPerUnit = 32f)
        {
            if (payload == null || payload.days == null || payload.days.Count == 0)
                throw new ArgumentException("유효한 GitHubStreakPayload 데이터가 없습니다.");

            // 첫 날짜의 요일 오프셋 계산 (Sunday = 0 ~ Saturday = 6)
            int firstDayOffset = 0;
            if (DateTime.TryParse(payload.days[0].date, out DateTime firstDate))
            {
                firstDayOffset = (int)firstDate.DayOfWeek;
            }

            int totalCells = firstDayOffset + payload.days.Count;
            int totalWeeks = Mathf.CeilToInt(totalCells / 7f); // 보통 52~53주
            int daysPerWeek = 7;

            int width = padding * 2 + totalWeeks * cellSize + Mathf.Max(0, totalWeeks - 1) * gap;
            int height = padding * 2 + daysPerWeek * cellSize + (daysPerWeek - 1) * gap;

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                // 2D 도트/아이소메트릭 뷰에서 뭉개짐 방지를 위해 Point 필터 강제
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            // 1. 배경색으로 전체 버퍼 초기화
            Color[] pixels = new Color[width * height];
            Array.Fill(pixels, palette.backgroundColor);

            // 2. 각 날짜별 잔디 셀 렌더링
            for (int i = 0; i < payload.days.Count; i++)
            {
                int gridIndex = firstDayOffset + i;
                int weekIndex = gridIndex / 7;      // X축 (0 ~ 52)
                int dayOfWeek = gridIndex % 7;      // Y축 (0: 일요일 ~ 6: 토요일)

                // 유니티 Texture2D는 좌하단이 (0,0)이므로 일요일(0)이 맨 위로 가도록 Y축 반전
                int invertedY = (daysPerWeek - 1) - dayOfWeek;

                int startX = padding + weekIndex * (cellSize + gap);
                int startY = padding + invertedY * (cellSize + gap);

                Color cellColor = palette.GetColor(payload.days[i].level);

                for (int y = 0; y < cellSize; y++)
                {
                    int rowOffset = (startY + y) * width;
                    for (int x = 0; x < cellSize; x++)
                    {
                        pixels[rowOffset + startX + x] = cellColor;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            // 3. 가구 배치용 Sprite 및 파일/네트워크 전송용 PNG 바이트 스트림 추출
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit
            );

            byte[] pngStream = texture.EncodeToPNG();

            return new GitHubStreakBundle(payload, texture, sprite, pngStream);
        }
    }
}