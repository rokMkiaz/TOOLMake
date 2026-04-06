@echo off
chcp 65001 > nul
REM =====================================================
REM  금화조사 자동화 - Windows 작업 스케줄러 등록
REM  매주 월요일 오전 9시 자동 실행
REM  ※ 반드시 "관리자 권한으로 실행" 하세요!
REM =====================================================

echo 작업 스케줄러 등록 중 (PowerShell 방식)...

powershell -NoProfile -ExecutionPolicy Bypass -Command "$action = New-ScheduledTaskAction -Execute 'python' -Argument 'D:\Work_1003bm\금화조사\monday_automation.py'; $trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Monday -At '09:00AM'; $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Hours 2); $result = Register-ScheduledTask -TaskName '금화조사_주간자동화' -Action $action -Trigger $trigger -Settings $settings -RunLevel Highest -Force; if ($result) { Write-Host 'SUCCESS' } else { Write-Host 'FAIL'; exit 1 }"

if %ERRORLEVEL% == 0 (
    echo.
    echo =====================================================
    echo  등록 완료!
    echo  작업명  : 금화조사_주간자동화
    echo  실행시간: 매주 월요일 오전 09:00
    echo  확인방법: 작업 스케줄러 열기 ^> 작업 스케줄러 라이브러리
    echo =====================================================
) else (
    echo.
    echo [오류] 작업 등록에 실패했습니다.
    echo  마우스 우클릭 -^> "관리자 권한으로 실행" 으로 다시 시도하세요.
)

pause