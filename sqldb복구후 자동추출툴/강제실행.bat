@echo off
chcp 65001 > nul
echo =====================================================
echo  금화조사 자동화 - 테스트 실행
echo =====================================================
echo.

REM Python 설치 확인
echo [1] Python 설치 확인...
python --version
if %ERRORLEVEL% neq 0 (
    echo.
    echo [오류] Python이 설치되지 않았거나 PATH에 등록되지 않았습니다.
    echo  https://www.python.org 에서 Python 설치 후 재시도하세요.
    echo  설치 시 "Add Python to PATH" 반드시 체크!
    goto END
)

REM pyodbc 설치 확인
echo.
echo [2] pyodbc 패키지 확인...
python -c "import pyodbc; print('pyodbc OK:', pyodbc.version)"
if %ERRORLEVEL% neq 0 (
    echo.
    echo [오류] pyodbc 미설치. 지금 설치합니다...
    pip install pyodbc
)

REM config.ini 존재 확인
echo.
echo [3] config.ini 확인...
if not exist "D:\Work_1003bm\금화조사\config.ini" (
    echo [오류] config.ini 파일이 없습니다.
    echo  D:\Work_1003bm\금화조사\ 폴더에 config.ini 를 넣어주세요.
    goto END
) else (
    echo config.ini 발견!
)

REM 스크립트 실행
echo.
echo [4] 자동화 스크립트 실행 중...
echo ─────────────────────────────────────────────────
python "D:\Work_1003bm\금화조사\monday_automation.py"
echo ─────────────────────────────────────────────────

if %ERRORLEVEL% == 0 (
    echo.
    echo 실행 완료! 결과 확인: D:\Work_1003bm\금화조사\결과\
) else (
    echo.
    echo [오류] 스크립트 실행 중 오류가 발생했습니다.
    echo  로그 파일 확인: D:\Work_1003bm\금화조사\logs\
)

:END
echo.
pause