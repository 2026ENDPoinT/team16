목적: 기존 Server의 app/main.py 및 기타 API를 보존하면서 테스트 계정 로그인 기능 추가

1. zip 파일을 기존 Server 폴더의 상위 폴더에서 압축 해제하여 병합
2. Server 폴더 터미널: python auth_install.py
3. python -m pip install -r requirements.txt
4. python -m uvicorn app.main:app --reload --host 127.0.0.1 --port 8000
5. http://127.0.0.1:8000/docs 의 POST /v1/auth/login에서 테스트

학생 ID: 20241234
비밀번호: Test1234!

더 자세한 설명: 로그인_적용가이드.md
※ 이클래스 계정과 전혀 무관한 개발용 테스트 계정
