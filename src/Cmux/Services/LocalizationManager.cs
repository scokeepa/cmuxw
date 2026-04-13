using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Cmux.Services;

public sealed class LocalizationManager : INotifyPropertyChanged
{
    private static readonly StringComparer Cmp = StringComparer.OrdinalIgnoreCase;

    public static LocalizationManager Instance { get; } = new();

    private readonly Dictionary<string, Dictionary<string, string>> _translations = new(Cmp)
    {
        ["ko"] = new Dictionary<string, string>(Cmp)
        {
            ["Settings"] = "설정",
            ["Workspace"] = "워크스페이스",
            ["Surface"] = "서피스",
            ["Pane"] = "패널",
            ["View"] = "보기",
            ["App"] = "앱",
            ["Layout"] = "레이아웃",
            ["Appearance"] = "모양",
            ["Terminal"] = "터미널",
            ["Behavior"] = "동작",
            ["Keyboard"] = "단축키",
            ["Agent"] = "에이전트",
            ["New Workspace"] = "새 워크스페이스",
            ["New Surface"] = "새 서피스",
            ["Close Surface"] = "서피스 닫기",
            ["Close Workspace"] = "워크스페이스 닫기",
            ["Split Right"] = "오른쪽 분할",
            ["Split Down"] = "아래 분할",
            ["Toggle Sidebar"] = "사이드바 토글",
            ["Notifications"] = "알림",
            ["Test Notification"] = "테스트 알림",
            ["Open Command Logs"] = "명령 로그 열기",
            ["Open Session Vault"] = "세션 보관함 열기",
            ["Open Command History"] = "명령 기록 열기",
            ["Insert Last Command"] = "최근 명령 삽입",
            ["Search"] = "검색",
            ["Toggle Agent Chat"] = "에이전트 채팅 토글",
            ["Zoom Pane"] = "패널 확대",
            ["Focus Next Pane"] = "다음 패널 포커스",
            ["Focus Previous Pane"] = "이전 패널 포커스",
            ["Next Surface"] = "다음 서피스",
            ["Previous Surface"] = "이전 서피스",
            ["Equalize Panes"] = "패널 균등 분할",
            ["Restore"] = "복원",
            ["Maximize"] = "최대화",
            ["Unzoom Pane"] = "패널 확대 해제",
            ["System Theme"] = "시스템 테마",
            ["Light"] = "라이트",
            ["Dark"] = "다크",
            ["Restore Session"] = "세션 복원",
            ["Restore previous session on startup"] = "시작 시 이전 세션 복원",
            ["Confirm Close"] = "닫기 확인",
            ["Ask before closing window"] = "창 닫기 전 확인",
            ["Auto Copy"] = "자동 복사",
            ["Copy to clipboard on text selection"] = "텍스트 선택 시 클립보드로 복사",
            ["Ctrl+Click URLs"] = "Ctrl+클릭 URL",
            ["Open URLs with Ctrl+Click"] = "Ctrl+클릭으로 URL 열기",
            ["Auto Save (sec)"] = "자동 저장(초)",
            ["Log Retention (days)"] = "로그 보관 기간(일)",
            ["Capture On Close"] = "닫기 시 캡처",
            ["Capture On Clear"] = "지우기 전 캡처",
            ["Capture Retention"] = "캡처 보관 기간",
            ["Keyboard Shortcuts"] = "키보드 단축키",
            ["Cancel"] = "취소",
            ["Save"] = "저장",
            ["Delete log files older than this many days. Use 0 to keep logs forever."] = "지정 일수보다 오래된 로그 파일을 삭제합니다. 0이면 영구 보관합니다.",
            ["Delete terminal captures older than this many days. Use 0 to keep forever."] = "지정 일수보다 오래된 터미널 캡처를 삭제합니다. 0이면 영구 보관합니다.",
            ["Save terminal transcript when pane/surface/workspace/app closes"] = "패널/서피스/워크스페이스/앱 종료 시 터미널 기록 저장",
            ["Save terminal transcript before Clear Terminal"] = "터미널 지우기 전 기록 저장",
            ["Command Palette"] = "명령 팔레트",
            ["Session Vault"] = "세션 보관함",
            ["Command Logs"] = "명령 로그",
            ["User message sent"] = "사용자 메시지 전송됨",
            ["Streaming response..."] = "응답 생성 중...",
            ["Response completed"] = "응답 완료",
            ["Idle"] = "대기",
            ["Error"] = "오류",
            ["Fatal Error"] = "치명적 오류",
            ["Unexpected error"] = "예상치 못한 오류",
            ["Daemon"] = "데몬",
            ["Local"] = "로컬",
            ["Connected to cmux-daemon — sessions persist across restarts"] = "cmux-daemon 연결됨 — 재시작 후에도 세션 유지",
            ["Running locally — sessions will not persist"] = "로컬 실행 중 — 세션이 유지되지 않음",
            ["Ctrl+Shift+P: Commands"] = "Ctrl+Shift+P: 명령",
            ["Language"] = "언어",
            ["English"] = "영어",
            ["Korean"] = "한국어",
            ["Chinese (Simplified)"] = "중국어(간체)",
            ["Logs"] = "로그",
            ["History"] = "히스토리",
            ["Command History"] = "명령 기록",
            ["No command history found yet for this pane."] = "이 패널의 명령 기록이 아직 없습니다.",
            ["New thread created"] = "새 스레드가 생성되었습니다.",
            ["No active pane selected"] = "활성 패널이 선택되지 않았습니다.",
            ["Agent did not accept the prompt"] = "에이전트가 프롬프트를 수락하지 않았습니다.",
            ["Prompt sent"] = "프롬프트 전송됨",
            ["About cmux"] = "cmux 정보",
            ["cmux for Windows\nA terminal multiplexer optimized for modern workflows."] = "Windows용 cmux\n현대적인 워크플로우에 최적화된 터미널 멀티플렉서입니다.",
            ["Settings saved."] = "설정이 저장되었습니다."
            ,["_File"] = "파일(_F)"
            ,["_Window"] = "창(_W)"
            ,["_Help"] = "도움말(_H)"
            ,["_New Workspace"] = "새 워크스페이스(_N)"
            ,["New _Surface"] = "새 서피스(_S)"
            ,["Open Command _Logs"] = "명령 로그 열기(_L)"
            ,["Open Session _Vault"] = "세션 보관함 열기(_V)"
            ,["_Settings"] = "설정(_S)"
            ,["E_xit"] = "종료(_X)"
            ,["Split _Right"] = "오른쪽 분할(_R)"
            ,["Split _Down"] = "아래 분할(_D)"
            ,["_Zoom Pane"] = "패널 확대(_Z)"
            ,["_Equalize Panes"] = "패널 균등 분할(_E)"
            ,["_Search"] = "검색(_S)"
            ,["Command _Palette"] = "명령 팔레트(_P)"
            ,["_Snippets"] = "스니펫(_S)"
            ,["Toggle Agent _Chat"] = "에이전트 채팅 토글(_C)"
            ,["Toggle _Sidebar"] = "사이드바 토글(_S)"
            ,["Keyboard Shortcuts"] = "키보드 단축키"
            ,["About"] = "정보"
            ,["Minimize"] = "최소화"
            ,["Close"] = "닫기"
            ,["Workspaces"] = "워크스페이스"
            ,["Manage sessions and environments"] = "세션과 환경 관리"
            ,["New Workspace (Ctrl+N)"] = "새 워크스페이스 (Ctrl+N)"
            ,["Session Vault (Ctrl+Shift+V)"] = "세션 보관함 (Ctrl+Shift+V)"
            ,["Command Logs (Ctrl+Shift+L)"] = "명령 로그 (Ctrl+Shift+L)"
            ,["Split Right (Ctrl+D)"] = "오른쪽 분할 (Ctrl+D)"
            ,["Split Down (Ctrl+Shift+D)"] = "아래 분할 (Ctrl+Shift+D)"
            ,["Zoom Pane (Ctrl+Shift+Z)"] = "패널 확대 (Ctrl+Shift+Z)"
            ,["Unzoom Pane (Ctrl+Shift+Z)"] = "패널 확대 해제 (Ctrl+Shift+Z)"
            ,["Filter workspaces by name, branch, or directory"] = "이름/브랜치/디렉터리로 워크스페이스 필터"
            ,["Open pane with shell..."] = "셸로 패널 열기..."
            ,["Layout: 2 Columns"] = "레이아웃: 2열"
            ,["Layout: Grid 2x2"] = "레이아웃: 2x2 그리드"
            ,["Layout: Main + Stack"] = "레이아웃: 메인 + 스택"
            ,["Toggle Agent Chat (Ctrl+Shift+A)"] = "에이전트 채팅 토글 (Ctrl+Shift+A)"
            ,["1 pane"] = "패널 1개"
            ,["Agent Chat"] = "에이전트 채팅"
            ,["Persistent threads per pane"] = "패널별 스레드 유지"
            ,["Hide Agent Chat"] = "에이전트 채팅 숨기기"
            ,["Search threads"] = "스레드 검색"
            ,["New thread"] = "새 스레드"
            ,["Refresh threads"] = "스레드 새로고침"
            ,["Usage: -"] = "사용량: -"
            ,["Context: -"] = "컨텍스트: -"
            ,["Search within selected thread"] = "선택된 스레드 내 검색"
            ,["Send"] = "전송"
            ,["Commands"] = "명령"
            ,["No matching commands"] = "일치하는 명령이 없습니다"
            ,["No notifications yet"] = "아직 알림이 없습니다"
            ,["Mark all read"] = "모두 읽음 처리"
            ,["Snippets"] = "스니펫"
            ,["New snippet (Ctrl+N)"] = "새 스니펫 (Ctrl+N)"
            ,["No snippets found"] = "스니펫이 없습니다"
            ,["Name"] = "이름"
            ,["Category"] = "카테고리"
            ,["Command"] = "명령"
            ,["Custom"] = "사용자 정의"
            ,["Save Snippet"] = "스니펫 저장"
            ,["Update Snippet"] = "스니펫 업데이트"
            ,["Edit snippet"] = "스니펫 편집"
            ,["New snippet"] = "새 스니펫"
            ,["Delete Snippet"] = "스니펫 삭제"
            ,["Delete snippet '{0}'?"] = "'{0}' 스니펫을 삭제할까요?"
            ,["Toggle favorite"] = "즐겨찾기 토글"
            ,["Delete snippet"] = "스니펫 삭제"
            ,["Snippet"] = "스니펫"
            ,["User snippet"] = "사용자 스니펫"
            ,["Snippet command/content cannot be empty."] = "스니펫 명령/내용은 비워둘 수 없습니다."
            ,["Input"] = "입력"
            ,["Press Enter to confirm or Esc to cancel."] = "Enter로 확인, Esc로 취소하세요."
            ,["OK"] = "확인"
            ,["Accent Color"] = "강조 색상"
            ,["Workspace Accent Color"] = "워크스페이스 강조 색상"
            ,["Preview"] = "미리보기"
            ,["Hex"] = "헥스"
            ,["Apply"] = "적용"
            ,["Font Family"] = "글꼴"
            ,["Font Size"] = "글자 크기"
            ,["Opacity"] = "투명도"
            ,["Cursor Style"] = "커서 스타일"
            ,["Cursor Blink"] = "커서 깜박임"
            ,[" (detected)"] = " (감지됨)"
            ,["Color Preset"] = "색상 프리셋"
            ,["Custom Colors"] = "사용자 색상"
            ,["Override preset colors"] = "프리셋 색상 덮어쓰기"
            ,["Pick"] = "선택"
            ,["Reset"] = "초기화"
            ,["Default Shell"] = "기본 셸"
            ,["Shell Arguments"] = "셸 인수"
            ,["Scrollback Lines"] = "스크롤백 줄 수"
            ,["New workspace"] = "새 워크스페이스"
            ,["Jump to workspace"] = "워크스페이스 이동"
            ,["Close workspace"] = "워크스페이스 닫기"
            ,["New surface"] = "새 서피스"
            ,["Close surface"] = "서피스 닫기"
            ,["Next surface"] = "다음 서피스"
            ,["Previous surface"] = "이전 서피스"
            ,["Split right"] = "오른쪽 분할"
            ,["Split down"] = "아래 분할"
            ,["Focus pane directionally"] = "방향키로 패널 포커스 이동"
            ,["Zoom pane toggle"] = "패널 확대 토글"
            ,["Delete previous word"] = "이전 단어 삭제"
            ,["Notification panel"] = "알림 패널"
            ,["Jump to latest unread"] = "최신 미읽음으로 이동"
            ,["Search in terminal"] = "터미널 검색"
            ,["Command palette"] = "명령 팔레트"
            ,["Toggle agent chat"] = "에이전트 채팅 토글"
            ,["Open command logs"] = "명령 로그 열기"
            ,["Open command history picker"] = "명령 기록 선택기 열기"
            ,["Open session vault"] = "세션 보관함 열기"
            ,["Enable Agent"] = "에이전트 활성화"
            ,["Enable pane handler commands"] = "패널 핸들러 명령 활성화"
            ,["Agent Name"] = "에이전트 이름"
            ,["Primary Handler"] = "기본 핸들러"
            ,["Extra Handlers"] = "추가 핸들러"
            ,["Active Provider"] = "활성 제공자"
            ,["System Prompt"] = "시스템 프롬프트"
            ,["OpenAI-Compatible"] = "OpenAI 호환"
            ,["Base URL"] = "기본 URL"
            ,["Model"] = "모델"
            ,["API Key"] = "API 키"
            ,["Clear"] = "지우기"
            ,["Anthropic-Compatible"] = "Anthropic 호환"
            ,["Tools"] = "도구"
            ,["Bash Tool"] = "Bash 도구"
            ,["Bash Timeout"] = "Bash 제한시간"
            ,["Web Search"] = "웹 검색"
            ,["Exa Base URL"] = "Exa 기본 URL"
            ,["Exa API Key"] = "Exa API 키"
            ,["Pane Submit"] = "패널 전송"
            ,["Default Submit Key"] = "기본 전송 키"
            ,["Enable Auto Fallback"] = "자동 대체 활성화"
            ,["Fallback Wait (ms)"] = "대체 대기(ms)"
            ,["Fallback Order"] = "대체 순서"
            ,["Date"] = "날짜"
            ,["Refresh"] = "새로고침"
            ,["Open Logs Folder"] = "로그 폴더 열기"
            ,["Open Terminal Captures"] = "터미널 캡처 열기"
            ,["Clear Filters"] = "필터 초기화"
            ,["Time"] = "시간"
            ,["Working Dir"] = "작업 디렉터리"
            ,["Exit"] = "종료 코드"
            ,["Duration"] = "소요 시간"
            ,["Copy Command"] = "명령 복사"
            ,["Insert in Focused Pane"] = "포커스 패널에 삽입"
            ,["Run in Focused Pane"] = "포커스 패널에서 실행"
            ,["All workspaces"] = "모든 워크스페이스"
            ,["All surfaces"] = "모든 서피스"
            ,["All panes"] = "모든 패널"
            ,["1 entry"] = "항목 1개"
            ,["{0} entries"] = "항목 {0}개"
            ,["{0} / {1} entries"] = "{0} / {1} 항목"
            ,["No focused pane available."] = "포커스된 패널이 없습니다."
            ,["Logs folder"] = "로그 폴더"
            ,["Terminal captures folder"] = "터미널 캡처 폴더"
            ,["1 command"] = "명령 1개"
            ,["{0} commands"] = "명령 {0}개"
            ,["Enter = run, Shift+Enter = insert"] = "Enter = 실행, Shift+Enter = 삽입"
            ,["Copy"] = "복사"
            ,["Insert"] = "삽입"
            ,["Run"] = "실행"
            ,["Open Folder"] = "폴더 열기"
            ,["Select a capture"] = "캡처를 선택하세요"
            ,["Copy All"] = "전체 복사"
            ,["Open File"] = "파일 열기"
            ,["1 capture"] = "캡처 1개"
            ,["{0} captures"] = "캡처 {0}개"
            ,["reason"] = "사유"
            ,["cwd"] = "작업경로"
            ,["Session vault folder"] = "세션 보관함 폴더"
            ,["Rename Pane"] = "패널 이름 변경"
            ,["Set a custom name for this pane."] = "이 패널의 사용자 지정 이름을 입력하세요."
            ,["Reset Pane Name"] = "패널 이름 초기화"
            ,["Close pane"] = "패널 닫기"
            ,["Close Pane"] = "패널 닫기"
            ,["Clear Terminal"] = "터미널 지우기"
            ,["Select All"] = "전체 선택"
            ,["Paste"] = "붙여넣기"
            ,["Workspace Icon"] = "워크스페이스 아이콘"
            ,["Enter a single icon (emoji/symbol) or a glyph code like E8A5, U+E8A5, 0xE8A5."] = "단일 아이콘(이모지/기호) 또는 E8A5, U+E8A5, 0xE8A5 형식의 글리프 코드를 입력하세요."
            ,["SVG is not supported in workspace icon yet. Use emoji/symbol or MDL2 hex code."] = "워크스페이스 아이콘에서 SVG는 아직 지원되지 않습니다. 이모지/기호 또는 MDL2 16진 코드를 사용하세요."
            ,["Rename"] = "이름 변경"
            ,["Duplicate"] = "복제"
            ,["Set Workspace Icon"] = "워크스페이스 아이콘 설정"
            ,["Accent: Indigo"] = "강조: 인디고"
            ,["Accent: Green"] = "강조: 그린"
            ,["Accent: Amber"] = "강조: 앰버"
            ,["Accent: Red"] = "강조: 레드"
            ,["Accent: Cyan"] = "강조: 시안"
            ,["Accent: Purple"] = "강조: 퍼플"
            ,["Accent: Slate"] = "강조: 슬레이트"
            ,["Accent: Pink"] = "강조: 핑크"
            ,["Accent: Custom..."] = "강조: 사용자 지정..."
            ,["Move Up"] = "위로 이동"
            ,["Move Down"] = "아래로 이동"
            ,["Usage: in {0} · out {1} · total {2}"] = "사용량: 입력 {0} · 출력 {1} · 합계 {2}"
            ,["Context: {0}/{1} tokens{2}"] = "컨텍스트: {0}/{1} 토큰{2}"
            ,["Context: {0}/{1} tokens{2}{3}"] = "컨텍스트: {0}/{1} 토큰{2}{3}"
            ,[" (near limit)"] = " (한계 근접)"
            ,[" · compacted"] = " · 압축됨"
            ,["assistant"] = "어시스턴트"
            ,["streaming..."] = "생성 중..."
            ,["cmux test"] = "cmux 테스트"
            ,["Notification check"] = "알림 확인"
            ,["If you see this in panel/toast, notifications are working."] = "패널/토스트에 이 메시지가 보이면 알림이 정상 동작합니다."
        },
        ["zh-CN"] = new Dictionary<string, string>(Cmp)
        {
            ["Settings"] = "设置",
            ["Workspace"] = "工作区",
            ["Surface"] = "标签页",
            ["Pane"] = "面板",
            ["View"] = "视图",
            ["App"] = "应用",
            ["Layout"] = "布局",
            ["Appearance"] = "外观",
            ["Terminal"] = "终端",
            ["Behavior"] = "行为",
            ["Keyboard"] = "快捷键",
            ["Agent"] = "代理",
            ["New Workspace"] = "新工作区",
            ["New Surface"] = "新标签页",
            ["Close Surface"] = "关闭标签页",
            ["Close Workspace"] = "关闭工作区",
            ["Split Right"] = "向右分割",
            ["Split Down"] = "向下分割",
            ["Toggle Sidebar"] = "切换侧边栏",
            ["Notifications"] = "通知",
            ["Test Notification"] = "测试通知",
            ["Open Command Logs"] = "打开命令日志",
            ["Open Session Vault"] = "打开会话库",
            ["Open Command History"] = "打开命令历史",
            ["Insert Last Command"] = "插入上一条命令",
            ["Search"] = "搜索",
            ["Toggle Agent Chat"] = "切换代理聊天",
            ["Zoom Pane"] = "放大面板",
            ["Focus Next Pane"] = "聚焦下一个面板",
            ["Focus Previous Pane"] = "聚焦上一个面板",
            ["Next Surface"] = "下一个标签页",
            ["Previous Surface"] = "上一个标签页",
            ["Equalize Panes"] = "均分面板",
            ["Restore"] = "还原",
            ["Maximize"] = "最大化",
            ["Unzoom Pane"] = "取消放大面板",
            ["System Theme"] = "系统主题",
            ["Light"] = "浅色",
            ["Dark"] = "深色",
            ["Restore Session"] = "恢复会话",
            ["Restore previous session on startup"] = "启动时恢复上次会话",
            ["Confirm Close"] = "关闭确认",
            ["Ask before closing window"] = "关闭窗口前确认",
            ["Auto Copy"] = "自动复制",
            ["Copy to clipboard on text selection"] = "选择文本时复制到剪贴板",
            ["Ctrl+Click URLs"] = "Ctrl+点击 URL",
            ["Open URLs with Ctrl+Click"] = "Ctrl+点击打开 URL",
            ["Auto Save (sec)"] = "自动保存(秒)",
            ["Log Retention (days)"] = "日志保留(天)",
            ["Capture On Close"] = "关闭时捕获",
            ["Capture On Clear"] = "清空前捕获",
            ["Capture Retention"] = "捕获保留",
            ["Keyboard Shortcuts"] = "键盘快捷键",
            ["Cancel"] = "取消",
            ["Save"] = "保存",
            ["Delete log files older than this many days. Use 0 to keep logs forever."] = "删除超过指定天数的日志文件。设为 0 则永久保留。",
            ["Delete terminal captures older than this many days. Use 0 to keep forever."] = "删除超过指定天数的终端捕获。设为 0 则永久保留。",
            ["Save terminal transcript when pane/surface/workspace/app closes"] = "关闭面板/标签页/工作区/应用时保存终端记录",
            ["Save terminal transcript before Clear Terminal"] = "清空终端前保存记录",
            ["Command Palette"] = "命令面板",
            ["Session Vault"] = "会话库",
            ["Command Logs"] = "命令日志",
            ["User message sent"] = "用户消息已发送",
            ["Streaming response..."] = "正在生成回复...",
            ["Response completed"] = "回复完成",
            ["Idle"] = "空闲",
            ["Error"] = "错误",
            ["Fatal Error"] = "致命错误",
            ["Unexpected error"] = "意外错误",
            ["Daemon"] = "守护进程",
            ["Local"] = "本地",
            ["Connected to cmux-daemon — sessions persist across restarts"] = "已连接 cmux-daemon — 重启后会话可恢复",
            ["Running locally — sessions will not persist"] = "本地运行 — 会话不会持久化",
            ["Ctrl+Shift+P: Commands"] = "Ctrl+Shift+P：命令",
            ["Language"] = "语言",
            ["English"] = "英语",
            ["Korean"] = "韩语",
            ["Chinese (Simplified)"] = "中文（简体）",
            ["Logs"] = "日志",
            ["History"] = "历史",
            ["Command History"] = "命令历史",
            ["No command history found yet for this pane."] = "此面板暂无命令历史。",
            ["New thread created"] = "已创建新线程",
            ["No active pane selected"] = "未选择活动面板",
            ["Agent did not accept the prompt"] = "代理未接受该提示",
            ["Prompt sent"] = "提示已发送",
            ["About cmux"] = "关于 cmux",
            ["cmux for Windows\nA terminal multiplexer optimized for modern workflows."] = "Windows 版 cmux\n面向现代工作流优化的终端复用器。",
            ["Settings saved."] = "设置已保存。",
            ["_File"] = "文件(_F)",
            ["_Window"] = "窗口(_W)",
            ["_Help"] = "帮助(_H)",
            ["_New Workspace"] = "新建工作区(_N)",
            ["New _Surface"] = "新建标签页(_S)",
            ["Open Command _Logs"] = "打开命令日志(_L)",
            ["Open Session _Vault"] = "打开会话库(_V)",
            ["_Settings"] = "设置(_S)",
            ["E_xit"] = "退出(_X)",
            ["Split _Right"] = "向右分割(_R)",
            ["Split _Down"] = "向下分割(_D)",
            ["_Zoom Pane"] = "放大面板(_Z)",
            ["_Equalize Panes"] = "均分面板(_E)",
            ["_Search"] = "搜索(_S)",
            ["Command _Palette"] = "命令面板(_P)",
            ["_Snippets"] = "片段(_S)",
            ["Toggle Agent _Chat"] = "切换代理聊天(_C)",
            ["Toggle _Sidebar"] = "切换侧边栏(_S)",
            ["Keyboard Shortcuts"] = "键盘快捷键",
            ["About"] = "关于",
            ["Minimize"] = "最小化",
            ["Close"] = "关闭",
            ["Workspaces"] = "工作区",
            ["Manage sessions and environments"] = "管理会话与环境",
            ["New Workspace (Ctrl+N)"] = "新建工作区 (Ctrl+N)",
            ["Session Vault (Ctrl+Shift+V)"] = "会话库 (Ctrl+Shift+V)",
            ["Command Logs (Ctrl+Shift+L)"] = "命令日志 (Ctrl+Shift+L)",
            ["Split Right (Ctrl+D)"] = "向右分割 (Ctrl+D)",
            ["Split Down (Ctrl+Shift+D)"] = "向下分割 (Ctrl+Shift+D)",
            ["Zoom Pane (Ctrl+Shift+Z)"] = "放大面板 (Ctrl+Shift+Z)",
            ["Unzoom Pane (Ctrl+Shift+Z)"] = "取消放大面板 (Ctrl+Shift+Z)",
            ["Filter workspaces by name, branch, or directory"] = "按名称、分支或目录过滤工作区",
            ["Open pane with shell..."] = "用 Shell 打开面板...",
            ["Layout: 2 Columns"] = "布局：2 列",
            ["Layout: Grid 2x2"] = "布局：2x2 网格",
            ["Layout: Main + Stack"] = "布局：主面板 + 堆叠",
            ["Toggle Agent Chat (Ctrl+Shift+A)"] = "切换代理聊天 (Ctrl+Shift+A)",
            ["1 pane"] = "1 个面板",
            ["Agent Chat"] = "代理聊天",
            ["Persistent threads per pane"] = "每个面板保留线程",
            ["Hide Agent Chat"] = "隐藏代理聊天",
            ["Search threads"] = "搜索线程",
            ["New thread"] = "新线程",
            ["Refresh threads"] = "刷新线程",
            ["Usage: -"] = "用量：-",
            ["Context: -"] = "上下文：-",
            ["Search within selected thread"] = "在线程内搜索",
            ["Send"] = "发送",
            ["Commands"] = "命令",
            ["No matching commands"] = "没有匹配的命令",
            ["No notifications yet"] = "暂无通知",
            ["Mark all read"] = "全部标记已读",
            ["Snippets"] = "代码片段",
            ["New snippet (Ctrl+N)"] = "新建片段 (Ctrl+N)",
            ["No snippets found"] = "未找到代码片段",
            ["Name"] = "名称",
            ["Category"] = "分类",
            ["Command"] = "命令",
            ["Custom"] = "自定义",
            ["Save Snippet"] = "保存片段",
            ["Update Snippet"] = "更新片段",
            ["Edit snippet"] = "编辑片段",
            ["New snippet"] = "新建片段",
            ["Delete Snippet"] = "删除片段",
            ["Delete snippet '{0}'?"] = "要删除片段“{0}”吗？",
            ["Toggle favorite"] = "切换收藏",
            ["Delete snippet"] = "删除片段",
            ["Snippet"] = "片段",
            ["User snippet"] = "用户片段",
            ["Snippet command/content cannot be empty."] = "片段命令/内容不能为空。",
            ["Input"] = "输入",
            ["Press Enter to confirm or Esc to cancel."] = "按 Enter 确认，按 Esc 取消。",
            ["OK"] = "确定",
            ["Accent Color"] = "强调色",
            ["Workspace Accent Color"] = "工作区强调色",
            ["Preview"] = "预览",
            ["Hex"] = "Hex",
            ["Apply"] = "应用",
            ["Font Family"] = "字体",
            ["Font Size"] = "字号",
            ["Opacity"] = "不透明度",
            ["Cursor Style"] = "光标样式",
            ["Cursor Blink"] = "光标闪烁",
            [" (detected)"] = "（已检测）",
            ["Color Preset"] = "颜色预设",
            ["Custom Colors"] = "自定义颜色",
            ["Override preset colors"] = "覆盖预设颜色",
            ["Pick"] = "选择",
            ["Reset"] = "重置",
            ["Default Shell"] = "默认 Shell",
            ["Shell Arguments"] = "Shell 参数",
            ["Scrollback Lines"] = "回滚行数",
            ["New workspace"] = "新建工作区",
            ["Jump to workspace"] = "切换到工作区",
            ["Close workspace"] = "关闭工作区",
            ["New surface"] = "新建标签页",
            ["Close surface"] = "关闭标签页",
            ["Next surface"] = "下一个标签页",
            ["Previous surface"] = "上一个标签页",
            ["Split right"] = "向右分割",
            ["Split down"] = "向下分割",
            ["Focus pane directionally"] = "按方向聚焦面板",
            ["Zoom pane toggle"] = "切换面板放大",
            ["Delete previous word"] = "删除上一个单词",
            ["Notification panel"] = "通知面板",
            ["Jump to latest unread"] = "跳转到最新未读",
            ["Search in terminal"] = "在终端中搜索",
            ["Command palette"] = "命令面板",
            ["Toggle agent chat"] = "切换代理聊天",
            ["Open command logs"] = "打开命令日志",
            ["Open command history picker"] = "打开命令历史选择器",
            ["Open session vault"] = "打开会话库",
            ["Enable Agent"] = "启用代理",
            ["Enable pane handler commands"] = "启用面板处理命令",
            ["Agent Name"] = "代理名称",
            ["Primary Handler"] = "主处理器",
            ["Extra Handlers"] = "额外处理器",
            ["Active Provider"] = "当前提供方",
            ["System Prompt"] = "系统提示词",
            ["OpenAI-Compatible"] = "OpenAI 兼容",
            ["Base URL"] = "基础 URL",
            ["Model"] = "模型",
            ["API Key"] = "API Key",
            ["Clear"] = "清除",
            ["Anthropic-Compatible"] = "Anthropic 兼容",
            ["Tools"] = "工具",
            ["Bash Tool"] = "Bash 工具",
            ["Bash Timeout"] = "Bash 超时",
            ["Web Search"] = "网页搜索",
            ["Exa Base URL"] = "Exa 基础 URL",
            ["Exa API Key"] = "Exa API Key",
            ["Pane Submit"] = "面板提交",
            ["Default Submit Key"] = "默认提交键",
            ["Enable Auto Fallback"] = "启用自动回退",
            ["Fallback Wait (ms)"] = "回退等待(ms)",
            ["Fallback Order"] = "回退顺序",
            ["Date"] = "日期",
            ["Refresh"] = "刷新",
            ["Open Logs Folder"] = "打开日志目录",
            ["Open Terminal Captures"] = "打开终端捕获",
            ["Clear Filters"] = "清除筛选",
            ["Time"] = "时间",
            ["Working Dir"] = "工作目录",
            ["Exit"] = "退出码",
            ["Duration"] = "耗时",
            ["Copy Command"] = "复制命令",
            ["Insert in Focused Pane"] = "插入到当前面板",
            ["Run in Focused Pane"] = "在当前面板运行",
            ["All workspaces"] = "全部工作区",
            ["All surfaces"] = "全部标签页",
            ["All panes"] = "全部面板",
            ["1 entry"] = "1 条记录",
            ["{0} entries"] = "{0} 条记录",
            ["{0} / {1} entries"] = "{0} / {1} 条记录",
            ["No focused pane available."] = "没有可用的聚焦面板。",
            ["Logs folder"] = "日志目录",
            ["Terminal captures folder"] = "终端捕获目录",
            ["1 command"] = "1 条命令",
            ["{0} commands"] = "{0} 条命令",
            ["Enter = run, Shift+Enter = insert"] = "Enter = 运行，Shift+Enter = 插入",
            ["Copy"] = "复制",
            ["Insert"] = "插入",
            ["Run"] = "运行",
            ["Open Folder"] = "打开文件夹",
            ["Select a capture"] = "请选择一个捕获",
            ["Copy All"] = "复制全部",
            ["Open File"] = "打开文件",
            ["1 capture"] = "1 条捕获",
            ["{0} captures"] = "{0} 条捕获",
            ["reason"] = "原因",
            ["cwd"] = "工作目录",
            ["Session vault folder"] = "会话库目录",
            ["Rename Pane"] = "重命名面板",
            ["Set a custom name for this pane."] = "为此面板设置自定义名称。",
            ["Reset Pane Name"] = "重置面板名称",
            ["Close pane"] = "关闭面板",
            ["Close Pane"] = "关闭面板",
            ["Clear Terminal"] = "清空终端",
            ["Select All"] = "全选",
            ["Paste"] = "粘贴",
            ["Workspace Icon"] = "工作区图标",
            ["Enter a single icon (emoji/symbol) or a glyph code like E8A5, U+E8A5, 0xE8A5."] = "输入单个图标（表情/符号）或如 E8A5、U+E8A5、0xE8A5 的字形代码。",
            ["SVG is not supported in workspace icon yet. Use emoji/symbol or MDL2 hex code."] = "工作区图标暂不支持 SVG。请使用表情/符号或 MDL2 十六进制代码。",
            ["Rename"] = "重命名",
            ["Duplicate"] = "复制",
            ["Set Workspace Icon"] = "设置工作区图标",
            ["Accent: Indigo"] = "强调色：靛蓝",
            ["Accent: Green"] = "强调色：绿色",
            ["Accent: Amber"] = "强调色：琥珀",
            ["Accent: Red"] = "强调色：红色",
            ["Accent: Cyan"] = "强调色：青色",
            ["Accent: Purple"] = "强调色：紫色",
            ["Accent: Slate"] = "强调色：石板",
            ["Accent: Pink"] = "强调色：粉色",
            ["Accent: Custom..."] = "强调色：自定义...",
            ["Move Up"] = "上移",
            ["Move Down"] = "下移",
            ["Usage: in {0} · out {1} · total {2}"] = "用量：输入 {0} · 输出 {1} · 总计 {2}",
            ["Context: {0}/{1} tokens{2}"] = "上下文：{0}/{1} tokens{2}",
            ["Context: {0}/{1} tokens{2}{3}"] = "上下文：{0}/{1} tokens{2}{3}",
            [" (near limit)"] = "（接近上限）",
            [" · compacted"] = " · 已压缩",
            ["assistant"] = "助手",
            ["streaming..."] = "生成中...",
            ["cmux test"] = "cmux 测试",
            ["Notification check"] = "通知检查",
            ["If you see this in panel/toast, notifications are working."] = "如果你在面板/通知中看到此消息，说明通知功能正常。"
        }
    };

    private readonly Dictionary<string, string> _keyByAnyText = new(Cmp);
    private string _language = "en";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? LanguageChanged;

    public string Language => _language;

    private LocalizationManager()
    {
        BuildLookup();
    }

    public void SetLanguage(string? language)
    {
        var normalized = NormalizeLanguage(language);
        if (string.Equals(_language, normalized, StringComparison.OrdinalIgnoreCase))
            return;

        _language = normalized;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke();
    }

    public string this[string key] => Translate(key);

    public string Translate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text ?? "";

        var key = ResolveKey(text);
        if (_language.Equals("en", StringComparison.OrdinalIgnoreCase))
            return key;

        if (_translations.TryGetValue(_language, out var map) && map.TryGetValue(key, out var translated))
            return translated;

        return key;
    }

    public void ApplyToVisualTree(DependencyObject root)
    {
        LocalizeElement(root);
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
            ApplyToVisualTree(VisualTreeHelper.GetChild(root, i));
    }

    private void LocalizeElement(DependencyObject d)
    {
        switch (d)
        {
            case Window w:
                w.Title = Translate(w.Title);
                break;
            case TextBlock tb:
                tb.Text = Translate(tb.Text);
                break;
            case Run run:
                run.Text = Translate(run.Text);
                break;
            case HeaderedContentControl hcc when hcc.Header is string s:
                hcc.Header = Translate(s);
                break;
            case HeaderedItemsControl hic when hic.Header is string hs:
                hic.Header = Translate(hs);
                break;
            case ContentControl cc when cc.Content is string cs:
                cc.Content = Translate(cs);
                break;
        }

        if (d is FrameworkElement fe)
        {
            if (fe.ToolTip is string tt)
                fe.ToolTip = Translate(tt);

            if (fe is ListView lv && lv.View is GridView gv)
            {
                foreach (var col in gv.Columns)
                {
                    if (col.Header is string header)
                        col.Header = Translate(header);
                }
            }
        }
    }

    private string ResolveKey(string text)
    {
        if (_keyByAnyText.TryGetValue(text, out var key))
            return key;

        return text;
    }

    private static string NormalizeLanguage(string? language)
    {
        var raw = (language ?? "").Trim();
        if (raw.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
            return "ko";
        if (raw.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            return "zh-CN";
        return "en";
    }

    private void BuildLookup()
    {
        _keyByAnyText.Clear();

        foreach (var (_, map) in _translations)
        {
            foreach (var (key, value) in map)
            {
                _keyByAnyText.TryAdd(key, key);
                if (!string.IsNullOrWhiteSpace(value))
                    _keyByAnyText.TryAdd(value, key);
            }
        }
    }
}

public static class L
{
    public static string T(string text) => LocalizationManager.Instance.Translate(text);
}
