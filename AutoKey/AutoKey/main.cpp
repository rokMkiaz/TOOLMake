#include <windows.h>
#include <tchar.h>
#include <string>
#include <iostream>

// 타이틀에 substr이 포함된 첫 번째 창을 반환
HWND FindWindowByTitlePart(const std::wstring& part) {
    struct Data { const std::wstring* p; HWND h; };
    Data data{ &part, nullptr };

    EnumWindows([](HWND hwnd, LPARAM lp) -> BOOL {
        Data* d = reinterpret_cast<Data*>(lp);
        if (!IsWindowVisible(hwnd))
            return TRUE;

        WCHAR buf[512] = { 0 };
        GetWindowTextW(hwnd, buf, _countof(buf));
        if (wcsstr(buf, d->p->c_str())) {
            d->h = hwnd;
            return FALSE;  // 찾았으니 중단
        }
        return TRUE;       // 계속
        }, reinterpret_cast<LPARAM>(&data));

    return data.h;
}

// 찾은 창을 앞으로 가져오고, Unicode 텍스트를 SendInput으로 전송
void SendUnicodeText(HWND hTarget, const std::wstring& text) {
    // 최소화 복원 & 포어그라운드
    ShowWindow(hTarget, SW_RESTORE);
    SetForegroundWindow(hTarget);
    Sleep(100);  // 창 전환 대기

    for (wchar_t ch : text) {
        INPUT inp = {};
        inp.type = INPUT_KEYBOARD;
        inp.ki.wScan = ch;                  // 유니코드 스캔 코드
        inp.ki.dwFlags = KEYEVENTF_UNICODE; // 키다운
        SendInput(1, &inp, sizeof(inp));

        inp.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP; // 키업
        SendInput(1, &inp, sizeof(inp));
    }

    INPUT ip = {};
    ip.type = INPUT_KEYBOARD;
    ip.ki.wVk = VK_RETURN;          // 가상 키 코드
    ip.ki.dwFlags = 0;              // 키다운
    SendInput(1, &ip, sizeof(ip));

    // Enter 키 뗌
    ip.ki.dwFlags = KEYEVENTF_KEYUP;
    SendInput(1, &ip, sizeof(ip));
}

int main() {
    // 1) “Notepad”가 포함된 창 찾기
    while (true)
    {
        int delay = std::rand() % 1001;

        HWND h = FindWindowByTitlePart(L"Notepad");
        if (!h) {
            std::wcout << L"[Error] 창을 찾을 수 없습니다: Notepad\n";
            return 1;
        }

        // 2) 자동으로 보낼 텍스트
        int r = std::rand() % (90 - 65 + 1) + 65;  // 0~25 + 65 → 65~90
   
        char character = static_cast<char>(r);

        // 1) 와이드 문자를 직접 생성
        wchar_t wc = static_cast<wchar_t>(character);
        std::wstring Key(1, wc);   // L"A" ~ L"Z"
        
        //std::wstring toSend = L"Hello from ChatGPT!";

        // 3) 입력 수행
        SendUnicodeText(h, Key);
        //SendUnicodeText(h, delay1);
        std::wcout << L"[Done] 텍스트 전송 완료.\n";

        Sleep(delay);
    }
    return 0;
}
