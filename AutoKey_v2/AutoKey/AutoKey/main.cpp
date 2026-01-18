#include <windows.h>
#include <uiautomation.h>
#include <iostream>
#include <string>
#include <vector>

#pragma comment(lib, "UIAutomationCore.lib")
#pragma comment(lib, "ole32.lib")

static void SafeRelease(IUnknown* p) { if (p) p->Release(); }

// -------------------- Low-level click fallbacks --------------------
static bool ClickElementViaMouse(IUIAutomationElement* el) {
    if (!el) return false;

    POINT pt{};
    BOOL b;
    HRESULT hr = el->GetClickablePoint(&pt,&b);
    if (FAILED(hr)) return false;

    const int sw = GetSystemMetrics(SM_CXSCREEN);
    const int sh = GetSystemMetrics(SM_CYSCREEN);
    if (sw <= 1 || sh <= 1) return false;

    INPUT in[3]{};
    in[0].type = INPUT_MOUSE;
    in[0].mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE;
    in[0].mi.dx = (LONG)(pt.x * 65535LL / (sw - 1));
    in[0].mi.dy = (LONG)(pt.y * 65535LL / (sh - 1));

    in[1].type = INPUT_MOUSE;
    in[1].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;

    in[2].type = INPUT_MOUSE;
    in[2].mi.dwFlags = MOUSEEVENTF_LEFTUP;

    return SendInput(3, in, sizeof(INPUT)) == 3;
}

static bool PressKeyElement(IUIAutomationElement* el) {
    if (!el) return false;

    // 1) Invoke
    {
        IUIAutomationInvokePattern* inv = nullptr;
        HRESULT hr = el->GetCurrentPattern(UIA_InvokePatternId, (IUnknown**)&inv);
        if (SUCCEEDED(hr) && inv) {
            hr = inv->Invoke();
            inv->Release();
            if (SUCCEEDED(hr)) return true;
        }
    }

    // 2) Legacy IAccessible DoDefaultAction
    {
        IUIAutomationLegacyIAccessiblePattern* leg = nullptr;
        HRESULT hr = el->GetCurrentPattern(UIA_LegacyIAccessiblePatternId, (IUnknown**)&leg);
        if (SUCCEEDED(hr) && leg) {
            hr = leg->DoDefaultAction();
            leg->Release();
            if (SUCCEEDED(hr)) return true;
        }
    }

    // 3) Mouse click at clickable point
    return ClickElementViaMouse(el);
}

// -------------------- Conditions & search helpers --------------------
static IUIAutomationCondition* MakeNameCondition(IUIAutomation* automation, const std::wstring& name) {
    if (!automation) return nullptr;

    VARIANT v; VariantInit(&v);
    v.vt = VT_BSTR;
    v.bstrVal = SysAllocString(name.c_str());

    IUIAutomationCondition* cond = nullptr;
    automation->CreatePropertyCondition(UIA_NamePropertyId, v, &cond);

    VariantClear(&v);
    return cond;
}

static IUIAutomationCondition* MakeAutomationIdCondition(IUIAutomation* automation, const std::wstring& aid) {
    if (!automation) return nullptr;

    VARIANT v; VariantInit(&v);
    v.vt = VT_BSTR;
    v.bstrVal = SysAllocString(aid.c_str());

    IUIAutomationCondition* cond = nullptr;
    automation->CreatePropertyCondition(UIA_AutomationIdPropertyId, v, &cond);

    VariantClear(&v);
    return cond;
}

static IUIAutomationCondition* MakeControlTypeButtonCondition(IUIAutomation* automation) {
    if (!automation) return nullptr;

    VARIANT v; VariantInit(&v);
    v.vt = VT_I4;
    v.lVal = UIA_ButtonControlTypeId;

    IUIAutomationCondition* cond = nullptr;
    automation->CreatePropertyCondition(UIA_ControlTypePropertyId, v, &cond);

    VariantClear(&v);
    return cond;
}

static void BuildCandidates(const wchar_t* primary, const std::vector<std::wstring>& fallbacks,
    std::vector<std::wstring>& outNames) {
    outNames.clear();
    if (primary && primary[0]) outNames.emplace_back(primary);
    for (const auto& f : fallbacks) outNames.emplace_back(f);
}

static bool ClickKeyStrictButtonByName(
    IUIAutomation* automation,
    IUIAutomationElement* osk,
    const std::wstring& keyName
) {
    if (!automation || !osk) return false;

    // Name == keyName
    IUIAutomationCondition* condName = MakeNameCondition(automation, keyName);
    // ControlType == Button
    IUIAutomationCondition* condBtn = MakeControlTypeButtonCondition(automation);

    if (!condName || !condBtn) {
        SafeRelease(condName);
        SafeRelease(condBtn);
        return false;
    }

    // AND(Name, Button)
    IUIAutomationCondition* condAnd = nullptr;
    automation->CreateAndCondition(condName, condBtn, &condAnd);
    SafeRelease(condName);
    SafeRelease(condBtn);

    if (!condAnd) return false;

    IUIAutomationElementArray* arr = nullptr;
    osk->FindAll(TreeScope_Descendants, condAnd, &arr);
    SafeRelease(condAnd);

    if (!arr) return false;

    int length = 0;
    arr->get_Length(&length);

    bool pressed = false;
    for (int i = 0; i < length; i++) {
        IUIAutomationElement* el = nullptr;
        if (SUCCEEDED(arr->GetElement(i, &el)) && el) {
            if (PressKeyElement(el)) {
                pressed = true;
                el->Release();
                break;
            }
            el->Release();
        }
    }

    arr->Release();
    return pressed;
}

static bool ClickKeyStrictButtonByAutomationId(
    IUIAutomation* automation,
    IUIAutomationElement* osk,
    const std::wstring& automationId
) {
    if (!automation || !osk) return false;

    // AutomationId == automationId
    IUIAutomationCondition* condId = MakeAutomationIdCondition(automation, automationId);
    // ControlType == Button
    IUIAutomationCondition* condBtn = MakeControlTypeButtonCondition(automation);

    if (!condId || !condBtn) {
        SafeRelease(condId);
        SafeRelease(condBtn);
        return false;
    }

    // AND(AutomationId, Button)
    IUIAutomationCondition* condAnd = nullptr;
    automation->CreateAndCondition(condId, condBtn, &condAnd);
    SafeRelease(condId);
    SafeRelease(condBtn);

    if (!condAnd) return false;

    IUIAutomationElementArray* arr = nullptr;
    osk->FindAll(TreeScope_Descendants, condAnd, &arr);
    SafeRelease(condAnd);

    if (!arr) return false;

    int length = 0;
    arr->get_Length(&length);

    bool pressed = false;
    for (int i = 0; i < length; i++) {
        IUIAutomationElement* el = nullptr;
        if (SUCCEEDED(arr->GetElement(i, &el)) && el) {
            if (PressKeyElement(el)) {
                pressed = true;
                el->Release();
                break;
            }
            el->Release();
        }
    }

    arr->Release();
    return pressed;
}

// Tries name candidates (Name==X && Button), then AutomationId candidates (AutomationId==X && Button)
static bool ClickKeyRobust(
    IUIAutomation* automation,
    IUIAutomationElement* osk,
    const wchar_t* primaryName,
    const std::vector<std::wstring>& nameFallbacks
) {
    std::vector<std::wstring> names;
    BuildCandidates(primaryName, nameFallbacks, names);

    // 1) Try by Name (strict Button)
    for (const auto& n : names) {
        if (ClickKeyStrictButtonByName(automation, osk, n)) {
            std::wcout << L"[성공] Key(Name): " << n << std::endl;
            return true;
        }
    }

    // 2) Try by AutomationId (also strict Button)
    //    In many OSK builds, AutomationId for letter keys can differ.
    //    If you know exact IDs, add them in fallbacks and this will try them too.
    for (const auto& n : names) {
        if (ClickKeyStrictButtonByAutomationId(automation, osk, n)) {
            std::wcout << L"[성공] Key(AutomationId): " << n << std::endl;
            return true;
        }
    }

    std::wcout << L"[실패] Key not found/pressed: " << (primaryName ? primaryName : L"(null)") << std::endl;
    return false;
}

// -------------------- OSK finder --------------------
static IUIAutomationElement* FindOSKWindow(IUIAutomation* automation) {
    if (!automation) return nullptr;

    IUIAutomationElement* root = nullptr;
    automation->GetRootElement(&root);
    if (!root) return nullptr;

    // ClassName == "OSKMainClass"
    VARIANT v; VariantInit(&v);
    v.vt = VT_BSTR;
    v.bstrVal = SysAllocString(L"OSKMainClass");

    IUIAutomationCondition* cond = nullptr;
    automation->CreatePropertyCondition(UIA_ClassNamePropertyId, v, &cond);
    VariantClear(&v);

    IUIAutomationElement* osk = nullptr;
    if (cond) {
        // OSK might not be direct child => Descendants
        root->FindFirst(TreeScope_Descendants, cond, &osk);
        cond->Release();
    }

    root->Release();
    return osk; // caller releases
}

int main() {
    setlocale(LC_ALL, "");

    HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    if (FAILED(hr)) {
        std::wcout << L"[오류] CoInitializeEx 실패\n";
        return 1;
    }

    IUIAutomation* automation = nullptr;
    hr = CoCreateInstance(CLSID_CUIAutomation, nullptr, CLSCTX_INPROC_SERVER,
        IID_IUIAutomation, (void**)&automation);
    if (FAILED(hr) || !automation) {
        std::wcout << L"[오류] UIAutomation 생성 실패\n";
        CoUninitialize();
        return 1;
    }

    std::wcout << L"프로그램 시작 - OSK(화상 키보드) 키 클릭 시도\n";
    std::wcout << L"※ 메모장 커서를 원하는 위치에 두고, OSK를 켜 둔 상태에서 테스트하세요.\n\n";

    while (true) {
        IUIAutomationElement* osk = FindOSKWindow(automation);
        if (!osk) {
            std::wcout << L"화상 키보드(OSK) 실행 대기 중...\n";
            Sleep(1000);
            continue;
        }

        // --- Tab ---
        // Name 후보: "Tab", "탭"
        ClickKeyRobust(automation, osk, L"Tab", { L"탭" });
        Sleep(200);

        // --- F ---
        // 가장 흔한 표시: "F"
        // 한글 모드(두벌식)에서는 'ㄹ'이 될 수 있음
        // 소문자 f 라벨이 있는 경우도 있어 fallback에 포함
        ClickKeyRobust(automation, osk, L"F", { L"f", L"ㄹ" });

        osk->Release();

        Sleep(1000);
    }

    automation->Release();
    CoUninitialize();
    return 0;
}
