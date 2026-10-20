using System.Collections.Generic;

namespace Littool
{
    // Texts of the Error Finder tab (Korean, English, Japanese). The guidance on the cards (what to do) comes from the
    // error list and is Korean only (decided for v1.3); the tab's titles, buttons and states are in three languages.
    internal static partial class L
    {
        static void AddErrorFinderTexts(Dictionary<string, string[]> t)
        {
            Add(t, "tab.errorFinder", "오류 찾기", "Error Finder", "エラー診断");

            // ---- status line ---------------------------------------------------------------------------------
            Add(t, "ef.title", "오류", "Errors", "エラー");
            Add(t, "ef.autoCollect", "자동으로 모으기", "Collect automatically", "自動で集める");
            Add(t, "ef.autoCollectTip",
                "켜면 에디터가 켜질 때부터 플레이·업로드·컴파일 오류를 모아요(창을 닫아도). 끄면 [다시 검사]를 누를 때만 읽어요.",
                "On: errors of Play Mode, uploads and compilation are collected from the moment the editor starts (even with this window closed). Off: they are read only when you press [Check again].",
                "オン: エディター起動時からプレイ・アップロード・コンパイルのエラーを集めます(ウィンドウを閉じていても)。オフ: [再チェック]を押したときだけ読みます。");
            Add(t, "ef.btn.rescan", "다시 검사", "Check again", "再チェック");
            Add(t, "ef.rescanTip",
                "지금 확인할 수 있는 오류(컴파일 오류, SDK 창의 검사 목록, 작업 중에 난 오류)를 다시 읽어요. 자동으로 모으기가 꺼져 있으면 콘솔·NDMF·SDK·컴파일을 한꺼번에 읽어요.",
                "Reads what can be checked now (compile errors, the SDK window's Validations, errors raised while editing). With automatic collection off, it reads the console, NDMF, the SDK and compilation at once.",
                "今確認できるエラー(コンパイルエラー、SDKウィンドウのValidations、作業中に出たエラー)を読み直します。自動で集めるがオフのときは、コンソール・NDMF・SDK・コンパイルをまとめて読みます。");
            Add(t, "ef.toast.rescanNone", "새 오류는 없었어요", "No new errors", "新しいエラーはありません");
            Add(t, "ef.catalogError",
                "오류 목록(ErrorCatalog.json)을 읽지 못해 원인을 찾을 수 없어요. LITTOOL을 다시 설치해 보세요. ({0})",
                "The error list (ErrorCatalog.json) could not be read, so causes cannot be found. Try reinstalling LITTOOL. ({0})",
                "エラーリスト(ErrorCatalog.json)を読めないため、原因を探せません。LITTOOLを入れ直してください。({0})");
            Add(t, "ef.problem", "오류 찾기 내부 문제: {0}", "Error Finder internal problem: {0}", "エラー診断の内部の問題: {0}");

            // ---- summary -------------------------------------------------------------------------------------
            Add(t, "ef.sum.found", "{0} · 원인 {1}개를 찾았어요", "{0} · {1} causes found", "{0} · 原因を{1}個見つけました");
            Add(t, "ef.sum.noCause", "{0} · 원인을 찾지 못했어요", "{0} · no cause found", "{0} · 原因が見つかりませんでした");
            Add(t, "ef.kind.playErrors", "플레이 오류", "Play Mode errors", "プレイ中のエラー");
            Add(t, "ef.kind.uploadFailed", "업로드 실패", "Upload failed", "アップロード失敗");
            Add(t, "ef.kind.uploadErrors", "업로드 중 오류", "Errors during upload", "アップロード中のエラー");
            Add(t, "ef.kind.testFailed", "Build & Test 실패", "Build & Test failed", "Build & Test失敗");
            Add(t, "ef.kind.testErrors", "Build & Test 중 오류", "Errors during Build & Test", "Build & Test中のエラー");
            Add(t, "ef.kind.compileErrors", "스크립트 오류", "Script errors", "スクリプトエラー");
            Add(t, "ef.kind.rescan", "다시 검사", "Check again", "再チェック");
            Add(t, "ef.when.play", "플레이 중", "During Play Mode", "プレイ中");
            Add(t, "ef.when.upload", "SDK 업로드 중", "During SDK upload", "SDKアップロード中");
            Add(t, "ef.when.test", "Build & Test 중", "During Build & Test", "Build & Test中");
            Add(t, "ef.when.compile", "스크립트 컴파일", "Script compilation", "スクリプトのコンパイル");
            Add(t, "ef.when.rescan", "다시 검사", "Check again", "再チェック");
            Add(t, "ef.ch.console", "콘솔", "Console", "コンソール");
            Add(t, "ef.ch.sdk", "SDK 업로드 결과", "SDK upload result", "SDKのアップロード結果");
            Add(t, "ef.ch.validation", "SDK 검사 목록", "SDK Validations", "SDKのValidations");
            Add(t, "ef.ch.ndmf", "NDMF Console", "NDMF Console", "NDMF Console");
            Add(t, "ef.ch.compile", "스크립트 컴파일", "Script compilation", "スクリプトのコンパイル");
            Add(t, "ef.read", "읽은 곳: {0}", "Read from: {0}", "読んだ場所: {0}");
            Add(t, "ef.readFold", "원인이 먼저, 결과 메시지는 아래에 접어 둬요", "causes first, result messages folded below", "原因を先に、結果メッセージは下にまとめています");
            Add(t, "ef.ignored", "무해한 메시지 {0}개는 건너뛰었어요", "{0} harmless messages skipped", "無害なメッセージ{0}個は省きました");
            Add(t, "ef.notice.rescanNothingNew",
                "다시 검사에서 새 오류는 없었어요. 플레이·업로드 중에만 나는 오류는 다시 플레이하거나 업로드해야 확인돼요.",
                "Checking again found no new errors. Errors that only happen in Play Mode or during an upload are confirmed by playing or uploading again.",
                "再チェックで新しいエラーはありませんでした。プレイ中・アップロード中だけに出るエラーは、もう一度プレイかアップロードすると確認できます。");
            Add(t, "ef.noCauseHint",
                "LITTOOL이 원인이 될 만한 오류 문장을 받지 못했어요. 콘솔(Window › General › Console)에 빨간 오류가 있는지 확인해 보세요.",
                "LITTOOL did not receive an error message that could be the cause. Check the Console (Window › General › Console) for red errors.",
                "LITTOOLは原因になりそうなエラー文を受け取れませんでした。コンソール(Window › General › Console)に赤いエラーがないか確認してください。");

            // ---- severity filter -----------------------------------------------------------------------------
            Add(t, "ef.filter.all", "전체 {0}", "All {0}", "すべて {0}");
            Add(t, "ef.filter.upload", "업로드 차단 {0}", "Blocks upload {0}", "アップロード不可 {0}");
            Add(t, "ef.filter.play", "플레이 차단 {0}", "Blocks Play Mode {0}", "プレイ不可 {0}");
            Add(t, "ef.filter.broken", "기능 깨짐 {0}", "Breaks a feature {0}", "機能が壊れる {0}");
            Add(t, "ef.filter.unknown", "모름 {0}", "Unknown {0}", "不明 {0}");
            Add(t, "ef.filter.project", "프로젝트 차단 {0}", "Blocks the project {0}", "プロジェクトが止まる {0}");
            Add(t, "ef.filterEmpty", "이 심각도의 오류는 없어요", "No errors of this severity", "この重大度のエラーはありません");

            // ---- cards ---------------------------------------------------------------------------------------
            Add(t, "ef.card.notListed", "목록에 없는 오류예요", "Not in the error list", "リストにないエラーです");
            Add(t, "ef.card.count", "{0}번", "×{0}", "{0}回");
            Add(t, "ef.sev.upload", "업로드 차단", "Blocks upload", "アップロード不可");
            Add(t, "ef.sev.play", "플레이 차단", "Blocks Play Mode", "プレイ不可");
            Add(t, "ef.sev.both", "플레이·업로드 차단", "Blocks play & upload", "プレイ・アップロード不可");
            Add(t, "ef.sev.broken", "기능 깨짐", "Breaks a feature", "機能が壊れる");
            Add(t, "ef.sev.project", "프로젝트 차단", "Blocks the project", "プロジェクトが止まる");
            Add(t, "ef.sev.unknown", "심각도 모름", "Severity unknown", "重大度不明");
            Add(t, "ef.tool.guess", "{0} 추정", "Probably {0}", "{0}と推定");
            Add(t, "ef.match.code", "확정 · {0}", "Confirmed · {0}", "確定 · {0}");
            Add(t, "ef.match.row", "확정 · 목록 {0}번", "Confirmed · list #{0}", "確定 · リスト{0}番");
            Add(t, "ef.match.similar", "비슷한 오류 · {0}%", "Similar error · {0}%", "似たエラー · {0}%");
            Add(t, "ef.match.none", "비슷한 오류 없음", "No similar error", "似たエラーなし");
            Add(t, "ef.found", "발견: {0} · {1}", "Found: {0} · {1}", "発見: {0} · {1}");
            Add(t, "ef.sec.where", "어디서 났나요", "Where it happened", "どこで出たか");
            Add(t, "ef.sec.cause", "원인", "Cause", "原因");
            Add(t, "ef.moreTargets", "같은 종류가 {0}개 더 있어요.", "{0} more of the same kind.", "同じ種類があと{0}個あります。");
            Add(t, "ef.sec.message", "메시지 내용", "What the message says", "メッセージの内容");
            Add(t, "ef.btn.papago", "파파고로 번역", "Translate (Papago)", "Papagoで翻訳");
            Add(t, "ef.btn.google", "구글 번역", "Google Translate", "Google翻訳");
            Add(t, "ef.translate.tip",
                "이 메시지를 번역 사이트에서 열어요. 이름·경로·ID는 가리고 메시지만 보내요(누를 때만).",
                "Opens this message on a translation site. Names, paths and IDs are masked; only the message is sent (only when you press it).",
                "このメッセージを翻訳サイトで開きます。名前・パス・IDは伏せ、メッセージだけを送ります(押したときだけ)。");
            Add(t, "ef.translate.offline", "인터넷에 연결되어 있지 않아요.", "No internet connection.", "インターネットに接続されていません。");
            Add(t, "ef.sec.steps", "해결 순서", "How to fix", "直す手順");
            Add(t, "ef.sec.first", "먼저 해 볼 것", "Try first", "まず試すこと");
            Add(t, "ef.sec.ifNot", "안 될 때", "If it does not help", "直らないとき");
            Add(t, "ef.sec.why", "왜 비슷한가요", "Why it is similar", "なぜ似ているか");
            Add(t, "ef.sec.received", "받은 오류", "The error you got", "受け取ったエラー");
            Add(t, "ef.sec.listed", "목록의 오류", "The error in the list", "リストのエラー");
            Add(t, "ef.sec.raw", "원문 보기 ({0})", "Original message ({0})", "原文を見る ({0})");
            Add(t, "ef.sec.source", "출처", "Source", "出典");
            Add(t, "ef.btn.select", "선택", "Select", "選択");
            Add(t, "ef.btn.folder", "폴더 보기", "Show folder", "フォルダーを表示");
            Add(t, "ef.btn.sdk", "SDK 창 열기", "Open SDK window", "SDKウィンドウを開く");
            Add(t, "ef.btn.ndmf", "NDMF Console 열기", "Open NDMF Console", "NDMF Consoleを開く");
            Add(t, "ef.btn.more", "나머지 {0}개 더 보기", "Show {0} more", "残り{0}個を表示");
            Add(t, "ef.notFound", "오브젝트를 찾을 수 없어요", "The object cannot be found", "オブジェクトが見つかりません");
            Add(t, "ef.noPipeline",
                "SDK 창에서 고른 아바타의 Pipeline Manager를 찾지 못했어요. SDK 창을 열고 아바타를 고른 뒤 다시 보세요.",
                "The Pipeline Manager of the avatar chosen in the SDK window was not found. Open the SDK window, choose the avatar, then look again.",
                "SDKウィンドウで選んだアバターのPipeline Managerが見つかりません。SDKウィンドウを開いてアバターを選んでから、もう一度見てください。");
            Add(t, "ef.results", "결과 메시지 {0}개", "{0} result messages", "結果メッセージ {0}個");
            Add(t, "ef.resultsHint", "위 원인을 고치면 같이 사라져요", "they go away with the causes above", "上の原因を直すと一緒に消えます");
            Add(t, "ef.footer",
                "LITTOOL은 아바타나 설정을 직접 바꾸지 않아요. [선택]은 위치만 보여주고, 고치는 건 안내를 보고 직접 해요.",
                "LITTOOL never changes your avatar or settings. [Select] only shows where; you make the fix yourself by following the guide.",
                "LITTOOLはアバターや設定を直接変えません。[選択]は場所を示すだけで、修正は案内を見ながらご自身で行います。");

            // ---- notification (scene view, when the window is closed) -----------------------------------------
            Add(t, "ef.notify",
                "LITTOOL · {0}, 원인 {1}개를 찾았어요\nTools › LITTOOL에서 확인하세요",
                "LITTOOL · {0}, {1} causes found\nSee Tools › LITTOOL",
                "LITTOOL · {0}、原因を{1}個見つけました\nTools › LITTOOLで確認してください");
            Add(t, "ef.notifyNoCause",
                "LITTOOL · {0}\nTools › LITTOOL에서 확인하세요",
                "LITTOOL · {0}\nSee Tools › LITTOOL",
                "LITTOOL · {0}\nTools › LITTOOLで確認してください");

            // ---- no errors -----------------------------------------------------------------------------------
            Add(t, "ef.idle.title", "지금은 찾은 오류가 없어요", "No errors found right now", "今は見つかったエラーはありません");
            Add(t, "ef.idle.desc",
                "Gesture Manager로 플레이하거나 SDK로 업로드할 때 오류가 나면, 여기에 원인과 해결 순서가 나와요.",
                "When playing with Gesture Manager or uploading with the SDK ends in errors, the cause and how to fix it appear here.",
                "Gesture Managerでのプレイ中やSDKでのアップロードでエラーが出ると、ここに原因と直す手順が表示されます。");
            Add(t, "ef.idle.descOff",
                "자동으로 모으기가 꺼져 있어요. 오류가 나면 [다시 검사]를 눌러 확인해요.",
                "Automatic collection is off. When something fails, press [Check again].",
                "自動で集めるがオフです。エラーが出たら[再チェック]を押して確認します。");
            Add(t, "ef.idle.last", "마지막 검사 {0} · {1}", "Last check {0} · {1}", "最後のチェック {0} · {1}");
            Add(t, "ef.idle.never", "이번 Unity 실행에서는 아직 검사하지 않았어요", "Nothing checked yet since Unity started", "今回のUnity起動ではまだチェックしていません");
            Add(t, "ef.idle.loose",
                "작업 중에 난 오류 {0}개가 있어요. [다시 검사]를 누르면 확인해요.",
                "{0} errors happened while editing. Press [Check again] to see them.",
                "作業中に出たエラーが{0}個あります。[再チェック]を押すと確認できます。");
            Add(t, "ef.short.play", "플레이", "Play Mode", "プレイ");
            Add(t, "ef.short.upload", "업로드", "Upload", "アップロード");
            Add(t, "ef.short.test", "Build & Test", "Build & Test", "Build & Test");
            Add(t, "ef.short.compile", "스크립트 컴파일", "Script compilation", "スクリプトのコンパイル");
            Add(t, "ef.short.rescan", "다시 검사", "Check again", "再チェック");
            Add(t, "ef.watch", "지켜보는 곳", "Watching", "見ている場所");
            Add(t, "ef.watch.capOn", "창을 닫아 두어도 지켜봐요", "even with this window closed", "ウィンドウを閉じていても見ています");
            Add(t, "ef.watch.capOff", "[다시 검사]를 누를 때만 읽어요", "read only on [Check again]", "[再チェック]を押したときだけ読みます");
            Add(t, "ef.watch.console", "콘솔", "Console", "コンソール");
            Add(t, "ef.watch.consoleDesc", "Gesture Manager 플레이 중 오류 포함", "including errors while playing with Gesture Manager", "Gesture Managerでのプレイ中のエラーも含む");
            Add(t, "ef.watch.sdk", "VRChat SDK 업로드 결과", "VRChat SDK upload result", "VRChat SDKのアップロード結果");
            Add(t, "ef.watch.sdkDesc", "빌드·업로드 실패 메시지 · 콘솔에 안 남는 것까지", "build / upload failures, also those not in the console", "ビルド・アップロードの失敗メッセージ · コンソールに残らないものも");
            Add(t, "ef.watch.ndmf", "NDMF Console", "NDMF Console", "NDMF Console");
            Add(t, "ef.watch.ndmfDesc", "Modular Avatar·AAO 같은 툴이 보고한 오류", "errors reported by tools such as Modular Avatar and AAO", "Modular Avatar・AAOなどのツールが報告したエラー");
            Add(t, "ef.watch.compile", "스크립트 컴파일", "Script compilation", "スクリプトのコンパイル");
            Add(t, "ef.watch.compileDesc", "툴끼리 충돌, 빠진 패키지", "tools clashing, missing packages", "ツール同士の衝突、足りないパッケージ");
            Add(t, "ef.st.on", "켜짐", "On", "オン");
            Add(t, "ef.st.scan", "검사할 때만", "On check only", "チェック時のみ");
            Add(t, "ef.st.missing", "없음", "Not installed", "なし");
            Add(t, "ef.st.unreadable", "읽을 수 없음", "Cannot read", "読めません");
            Add(t, "ef.st.unreadableVer", "읽을 수 없음 · 버전 {0}", "Cannot read · version {0}", "読めません · バージョン{0}");
            Add(t, "ef.know", "알아두기", "Good to know", "知っておくこと");
            Add(t, "ef.know.noChange",
                "LITTOOL은 아바타나 설정을 직접 바꾸지 않아요. 고치는 방법을 안내만 해요.",
                "LITTOOL never changes your avatar or settings. It only shows how to fix things.",
                "LITTOOLはアバターや設定を直接変えません。直し方を案内するだけです。");
            Add(t, "ef.know.safeMode",
                "Unity가 Safe Mode로 열리면 LITTOOL도 실행되지 않아요. 그때는 콘솔 맨 위의 빨간 줄(error CS…)부터 확인해요.",
                "When Unity opens in Safe Mode, LITTOOL does not run either. Then check the first red line in the Console (error CS…).",
                "UnityがSafe Modeで開くとLITTOOLも動きません。そのときはコンソールの一番上の赤い行(error CS…)から確認します。");
            Add(t, "ef.know.list",
                "오류 목록 {0}개({1}) · {2} 기준. 목록에 없는 오류는 오류가 난 위치나 비슷한 오류로 추정해서 알려줘요.",
                "Error list: {0} errors ({1}), as of {2}. Errors not in the list are estimated from where they happened or from similar errors.",
                "エラーリスト{0}個({1}) · {2}時点。リストにないエラーは、出た場所や似たエラーから推定して案内します。");
            Add(t, "ef.know.log",
                "목록과 똑같지 않은 오류는 이 PC의 문서 › LITTOOL › 오류 기록에 남겨요. 이름·ID는 가려서 적고, 어디로도 보내지 않아요.",
                "Errors that do not exactly match the list are kept on this PC in Documents › LITTOOL › 오류 기록. Names and IDs are masked, and nothing is sent anywhere.",
                "リストと完全に一致しないエラーは、このPCのドキュメント › LITTOOL › 오류 기록に残します。名前・IDは伏せて書き、どこにも送りません。");
            Add(t, "ef.btn.logFolder", "기록 폴더 열기", "Open log folder", "記録フォルダーを開く");
            Add(t, "ef.toast.logFail", "기록 폴더를 열지 못했어요: {0}", "Could not open the log folder: {0}", "記録フォルダーを開けませんでした: {0}");
        }
    }
}
