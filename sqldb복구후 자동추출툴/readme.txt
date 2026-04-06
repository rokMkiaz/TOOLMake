# 금화조사 주간 자동화 사용방법

## 📁 파일 구성
```
D:\Work_1003bm\금화조사\
├── monday_automation.py   ← 메인 자동화 스크립트
├── config.ini             ← FTP/DB 접속 설정 (★ 반드시 수정)
├── 패키지_설치.bat          ← 처음 한 번만 실행
├── 작업스케줄러_등록.bat    ← 매주 자동실행 등록
├── *.sql                  ← 기존 쿼리 파일 (그대로 유지)
├── 결과\                  ← CSV 결과 파일 저장 위치
└── logs\                  ← 실행 로그 저장 위치
```

---

## 🚀 최초 설정 (처음 한 번만)

### 1단계: 파일 복사
위 파일들을 `D:\Work_1003bm\금화조사\` 폴더에 복사하세요.

### 2단계: config.ini 수정
`config.ini` 파일을 메모장으로 열어 FTP/DB 정보를 입력하세요.

```ini
[FTP]
host     = 192.168.1.100    ← FTP 서버 주소로 변경
port     = 21
user     = ftpuser          ← FTP 아이디로 변경
password = ftppass          ← FTP 비밀번호로 변경
base_dir =                  ← FTP 기본 경로 (루트면 비워두기)

[DATABASE]
server   = localhost        ← SQL Server 주소로 변경
database = csb_db           ← 데이터베이스명으로 변경
```

### 3단계: Python 패키지 설치
`패키지_설치.bat` 파일을 더블클릭하여 실행하세요.
(Python이 설치되어 있어야 합니다)

### 4단계: 작업 스케줄러 등록
`작업스케줄러_등록.bat` 파일을 **마우스 우클릭 → 관리자 권한으로 실행** 하세요.
→ 매주 월요일 오전 9시에 자동 실행됩니다.

---

## ✅ 동작 순서 (매주 월요일 자동 실행)

1. **이번 주 월요일 날짜** 계산 (예: 20260323)
2. **FTP 접속** → `20260323/csb_cash.bak` 다운로드
   → `D:\Work_1003bm\금화조사\csb_cash_20260323.bak` 에 저장
3. **SQL 파일의 날짜** 자동 교체 (`'20XXXXXX'` → `'20260323'`)
4. **SQL 쿼리 실행** (Windows 인증으로 DB 접속)
5. **결과 저장** → `D:\Work_1003bm\금화조사\결과\파일명_20260323.csv`
6. **로그 기록** → `D:\Work_1003bm\금화조사\logs\automation_20260323.log`

---

## 🔧 수동 실행 (테스트할 때)

```
python D:\Work_1003bm\금화조사\monday_automation.py
```

또는 해당 폴더에서 명령 프롬프트 열고:
```
python monday_automation.py
```

---

## ❓ 자주 묻는 문제

| 오류 메시지 | 해결 방법 |
|---|---|
| `설정 파일이 없습니다` | config.ini 파일이 같은 폴더에 있는지 확인 |
| `FTP 접속 실패` | config.ini의 host/user/password 확인 |
| `pyodbc 오류` | 패키지_설치.bat 실행 후 재시도 |
| `ODBC Driver 17 없음` | Microsoft ODBC Driver 17 for SQL Server 설치 필요 |
| `.sql 파일이 없습니다` | 금화조사 폴더에 .sql 파일이 있는지 확인 |