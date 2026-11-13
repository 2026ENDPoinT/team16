mergeInto(LibraryManager.library, {

  ZzapDesktopMate_IsSupported: function () {
    if (typeof window === "undefined") return 0;
    if ("documentPictureInPicture" in window) return 2; // Document PiP 지원
    if (document.pictureInPictureEnabled) return 1;      // Video PiP 폴백 지원
    return 0;
  },

  ZzapDesktopMate_OpenPiP: function (configJsonPtr, idleDataUrlPtr, walkDataUrlPtr) {
    var configStr = UTF8ToString(configJsonPtr);
    var idleUrl = UTF8ToString(idleDataUrlPtr);
    var walkUrl = UTF8ToString(walkDataUrlPtr);

    var config = {};
    try {
      config = JSON.parse(configStr);
    } catch (e) {
      console.warn("[DesktopMatePiP] JSON parse error:", e);
    }

    var nickname = config.nickname || "짭이";
    var intervalSec = Math.max(5, config.intervalSeconds || 60);
    var lines = (config.lines && config.lines.length > 0)
      ? config.lines
      : [
          "오늘 할 일은 다 챙겼어? 하나씩 끝내보자!",
          "어깨 한 번 쭉 펴고 물 한 모금 마셔!",
          "꾸준히 하는 게 제일 멋져. 내가 옆에서 지켜보고 있다구!",
          "과제 마감 시간 놓치지 않게 조심해!"
        ];

    // 이미 열린 Document PiP 창이 있으면 닫기(토글)
    if (window.__zzapPipWindow && !window.__zzapPipWindow.closed) {
      window.__zzapPipWindow.close();
      window.__zzapPipWindow = null;
      return;
    }

    // 1순위: Document Picture-in-Picture API (Chrome / Edge 116+)
    if ("documentPictureInPicture" in window) {
      window.documentPictureInPicture.requestWindow({
        width: 320,
        height: 220
      }).then(function (pipWin) {
        window.__zzapPipWindow = pipWin;
        var doc = pipWin.document;

        doc.head.innerHTML = [
          "<meta charset='utf-8'>",
          "<title>" + nickname + "</title>",
          "<style>",
          "  * { box-sizing: border-box; user-select: none; -webkit-user-select: none; margin: 0; padding: 0; }",
          "  body {",
          "    width: 100vw; height: 100vh; overflow: hidden;",
          "    font-family: 'Pretendard', 'Noto Sans KR', 'Malgun Gothic', sans-serif;",
          "    background: #231c33;",
          "    position: relative;",
          "  }",
          "  .bubble {",
          "    position: absolute; bottom: 138px; left: 50%; transform: translateX(-50%) scale(0.85);",
          "    min-width: 170px; max-width: 280px;",
          "    background: #ffffff; color: #1e222b;",
          "    font-size: 12.5px; font-weight: 700; line-height: 1.42; text-align: center;",
          "    padding: 9px 13px; border-radius: 14px;",
          "    box-shadow: 0 6px 18px rgba(0,0,0,0.35);",
          "    opacity: 0; pointer-events: none;",
          "    transition: opacity 0.2s ease, transform 0.2s cubic-bezier(0.34, 1.56, 0.64, 1);",
          "    z-index: 20; word-break: keep-all;",
          "  }",
          "  .bubble.visible {",
          "    opacity: 1; transform: translateX(-50%) scale(1);",
          "  }",
          "  .bubble::after {",
          "    content: ''; position: absolute; bottom: -7px; left: 50%; transform: translateX(-50%);",
          "    border-width: 7px 7px 0; border-style: solid;",
          "    border-color: #ffffff transparent transparent transparent;",
          "  }",
          "  .char-wrap {",
          "    position: absolute; bottom: 8px; left: 110px;",
          "    width: 104px; height: 132px; cursor: pointer;",
          "    display: flex; align-items: flex-end; justify-content: center;",
          "  }",
          "  .char-img {",
          "    width: 104px; height: 132px; object-fit: contain;",
          "    transform-origin: bottom center;",
          "  }",
          "  .char-img.bounce {",
          "    animation: hop 0.3s ease;",
          "  }",
          "  @keyframes hop {",
          "    0% { transform: scale(1, 1) translateY(0); }",
          "    35% { transform: scale(1.08, 0.92) translateY(0); }",
          "    65% { transform: scale(0.95, 1.06) translateY(-10px); }",
          "    100% { transform: scale(1, 1) translateY(0); }",
          "  }",
          "</style>"
        ].join("\n");

        doc.body.innerHTML = [
          "<div class='bubble' id='bubble'></div>",
          "<div class='char-wrap' id='charWrap'>",
          "  <img class='char-img' id='charImg' alt='character' />",
          "</div>"
        ].join("\n");

        var bubbleEl = doc.getElementById("bubble");
        var charWrap = doc.getElementById("charWrap");
        var charImg = doc.getElementById("charImg");

        charImg.src = idleUrl;

        var posX = 110;
        var targetX = 180;
        var isWalking = true;
        var facingRight = true;
        var walkFrameToggle = false;
        var frameTimer = 0;
        var stateTimer = 2.0;
        var speechTimer = intervalSec;
        var bubbleHideTimeout = null;
        var lastLineIdx = -1;

        function showRandomLine() {
          if (!lines || lines.length === 0) return;
          var idx = Math.floor(Math.random() * lines.length);
          if (lines.length > 1 && idx === lastLineIdx) {
            idx = (idx + 1) % lines.length;
          }
          lastLineIdx = idx;

          bubbleEl.textContent = lines[idx];
          bubbleEl.classList.add("visible");

          charImg.classList.remove("bounce");
          void charImg.offsetWidth;
          charImg.classList.add("bounce");

          if (bubbleHideTimeout) pipWin.clearTimeout(bubbleHideTimeout);
          bubbleHideTimeout = pipWin.setTimeout(function () {
            bubbleEl.classList.remove("visible");
          }, 5000);
        }

        charWrap.addEventListener("click", function () {
          speechTimer = intervalSec;
          showRandomLine();
        });

        var dt = 1 / 30;
        var loopId = pipWin.setInterval(function () {
          var maxX = Math.max(40, (pipWin.innerWidth || 320) - 114);
          var minX = 10;

          speechTimer -= dt;
          if (speechTimer <= 0) {
            speechTimer = intervalSec;
            showRandomLine();
          }

          stateTimer -= dt;
          if (isWalking) {
            var dir = targetX > posX ? 1 : -1;
            facingRight = dir > 0;
            posX += dir * 42 * dt;

            frameTimer += dt;
            if (frameTimer >= 0.35) {
              frameTimer = 0;
              walkFrameToggle = !walkFrameToggle;
              charImg.src = walkFrameToggle ? walkUrl : idleUrl;
            }

            if (Math.abs(targetX - posX) < 3 || stateTimer <= 0) {
              isWalking = false;
              stateTimer = 1.5 + Math.random() * 2.5;
              charImg.src = idleUrl;
            }
          } else {
            if (stateTimer <= 0) {
              isWalking = true;
              stateTimer = 2.0 + Math.random() * 3.5;
              targetX = minX + Math.random() * (maxX - minX);
              frameTimer = 0;
            }
          }

          charWrap.style.left = Math.round(posX) + "px";
          charImg.style.transform = facingRight ? "scaleX(-1)" : "scaleX(1)";
        }, 33);

        pipWin.addEventListener("pagehide", function () {
          pipWin.clearInterval(loopId);
          window.__zzapPipWindow = null;
        });
      }).catch(function (err) {
        console.warn("[DesktopMatePiP] Document PiP 실패:", err);
      });
    }
  }
});
