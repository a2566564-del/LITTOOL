using System;
using System.Collections.Generic;
using UnityEditor;

namespace Littool
{
    internal enum Lang
    {
        Ko = 0,
        En = 1,
        Ja = 2,
    }

    /// <summary>
    /// UI text in Korean, English and Japanese. The language is a per-user preference (EditorPrefs),
    /// so each person sharing a project can use their own language. Until the user picks one, the computer's
    /// language decides (Korean → 한국어, Japanese → 日本語, anything else → English).
    /// lilToon section names are not here: they come from lilToon's translations (see LilToonCatalog).
    /// Feature texts live in other parts of this class (Localization.BoneScale.cs, Localization.ErrorFinder.cs).
    /// </summary>
    internal static partial class L
    {
        const string PrefKey = "LITTOOL.Language";

        public static readonly string[] LanguageNames = { "한국어", "English", "日本語" };

        static Lang? s_Current;

        static L()
        {
            AddBoneScaleTexts(s_Table);
            AddErrorFinderTexts(s_Table);
        }

        public static Lang Current
        {
            get
            {
                if (!s_Current.HasValue)
                {
                    int value = EditorPrefs.HasKey(PrefKey) ? EditorPrefs.GetInt(PrefKey, (int)Lang.Ko) : (int)FromSystem();
                    if (value < 0 || value >= LanguageNames.Length) value = (int)Lang.Ko;
                    s_Current = (Lang)value;
                }
                return s_Current.Value;
            }
            set
            {
                s_Current = value;
                EditorPrefs.SetInt(PrefKey, (int)value);
            }
        }

        static Lang FromSystem()
        {
            switch (UnityEngine.Application.systemLanguage)
            {
                case UnityEngine.SystemLanguage.Korean: return Lang.Ko;
                case UnityEngine.SystemLanguage.Japanese: return Lang.Ja;
                default: return Lang.En;
            }
        }

        /// <summary>Returns the text for <paramref name="key"/> in the current language.</summary>
        public static string T(string key)
        {
            if (s_Table.TryGetValue(key, out var texts))
            {
                int index = (int)Current;
                if (index >= 0 && index < texts.Length && !string.IsNullOrEmpty(texts[index])) return texts[index];
                return texts[0];
            }
            return key;
        }

        /// <summary>string.Format on the localized text. Never throws on a bad format string.</summary>
        public static string F(string key, params object[] args)
        {
            string format = T(key);
            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        static readonly Dictionary<string, string[]> s_Table = new Dictionary<string, string[]>
        {
            // ---- Common --------------------------------------------------------------------------------
            { "common.ok", new[] { "확인", "OK", "OK" } },
            { "common.cancel", new[] { "취소", "Cancel", "キャンセル" } },
            { "common.more", new[] { " 외 {0}개", " and {0} more", " 他{0}個" } },

            // ---- Tabs ----------------------------------------------------------------------------------
            { "tab.materialBatch", new[] { "마테리얼 일괄 적용", "Material Batch", "マテリアル一括適用" } },
            { "tab.boneScale", new[] { "본 스케일", "Bone Scale", "ボーンスケール" } },

            // ---- lilToon ---------------------------------------------------------------------------------
            { "lil.notInstalled", new[]
            {
                "lilToon이 설치되어 있지 않습니다. lilToon을 먼저 설치하세요.",
                "lilToon is not installed. Please install lilToon first.",
                "lilToonがインストールされていません。先にlilToonをインストールしてください。",
            } },

            // ---- Reference (material or preset) ------------------------------------------------------
            { "ref.header", new[] { "기준", "Reference", "基準" } },
            { "ref.modeMaterial", new[] { "마테리얼", "Material", "マテリアル" } },
            { "ref.modePreset", new[] { "프리셋", "Preset", "プリセット" } },
            { "ref.none", new[]
            {
                "기준 마테리얼을 지정하세요. (예: 아바타 Body 마테리얼)",
                "Assign a reference material (e.g. the avatar's Body material).",
                "基準マテリアルを指定してください。(例: アバターのBodyマテリアル)",
            } },
            { "ref.shaderMissing", new[]
            {
                "기준 마테리얼의 셰이더가 깨져 있습니다(분홍색). lilToon이 설치되어 있는지 확인하세요.",
                "The reference material's shader is broken (pink). Check that lilToon is installed.",
                "基準マテリアルのシェーダーが壊れています(ピンク色)。lilToonがインストールされているか確認してください。",
            } },
            { "ref.notLil", new[]
            {
                "lilToon 마테리얼만 기준으로 쓸 수 있습니다. (현재 셰이더: {0})",
                "Only lilToon materials can be used as the reference. (Current shader: {0})",
                "基準にできるのはlilToonマテリアルのみです。(現在のシェーダー: {0})",
            } },
            { "ref.shader", new[] { "Shader: {0}", "Shader: {0}", "Shader: {0}" } },
            { "ref.missingProps", new[]
            {
                "목록의 항목 중 {0}개는 기준 마테리얼에 없어서 건너뜁니다. (lilToon Lite 등)",
                "{0} listed properties do not exist on the reference material and will be skipped (e.g. lilToon Lite).",
                "リストのうち{0}個の項目は基準マテリアルに存在しないためスキップされます。(lilToon Liteなど)",
            } },

            // ---- Copy without textures -----------------------------------------------------------------
            { "copy.button", new[] { "텍스처 뺀 복사본 만들기", "Copy without textures", "テクスチャなしで複製" } },
            { "copy.tooltip", new[]
            {
                "기준 마테리얼을 같은 폴더에 ‘이름_Ref’로 복사하고, 텍스처·노멀맵 등 이미지 슬롯을 모두 비웁니다. 원본은 바뀌지 않습니다.",
                "Copies the reference material into the same folder as ‘name_Ref’ with every texture slot emptied. The original is not changed.",
                "基準マテリアルを同じフォルダーに「名前_Ref」として複製し、テクスチャ・ノーマルマップなどの画像スロットをすべて空にします。元のマテリアルは変更しません。",
            } },
            { "copy.confirmTitle", new[] { "텍스처 뺀 복사본 만들기", "Copy without textures", "テクスチャなしで複製" } },
            { "copy.confirmBody", new[]
            {
                "‘{0}’의 복사본을 같은 폴더에 ‘{1}’(으)로 만들고, 이미지가 들어 있는 슬롯 {2}개(메인 텍스처, 노멀맵, 마스크 등)를 모두 비웁니다. 수치 값은 그대로 두고, 원본은 바뀌지 않습니다.\n\n만든 복사본을 기준 마테리얼로 지정합니다.",
                "A copy of ‘{0}’ named ‘{1}’ will be created in the same folder, and its {2} texture slots in use (main texture, normal map, masks...) will be emptied. Values are kept and the original is not changed.\n\nThe copy then becomes the reference material.",
                "「{0}」の複製を同じフォルダーに「{1}」として作成し、画像が入っている{2}個のスロット(メインテクスチャ、ノーマルマップ、マスクなど)をすべて空にします。数値はそのまま残し、元のマテリアルは変更しません。\n\n作成した複製を基準マテリアルに設定します。",
            } },
            { "copy.ok", new[] { "만들기", "Create", "作成" } },
            { "copy.pickTitle", new[] { "복사본 저장 위치", "Save the copy", "複製の保存先" } },
            { "copy.pickMessage", new[]
            {
                "원본 폴더에는 저장할 수 없어서 저장할 위치를 골라 주세요.",
                "The original folder is read-only. Choose where to save the copy.",
                "元のフォルダーには保存できないため、保存先を選んでください。",
            } },
            { "copy.done", new[]
            {
                "{0}을(를) 만들고 기준 마테리얼로 지정했습니다. 이미지 슬롯 {1}개를 비웠고, 원본 ‘{2}’은(는) 그대로입니다.",
                "Created {0} and set it as the reference material. {1} texture slots were emptied; the original ‘{2}’ is unchanged.",
                "{0} を作成し、基準マテリアルに設定しました。画像スロット{1}個を空にしました。元の「{2}」は変更していません。",
            } },
            { "copy.failed", new[]
            {
                "복사본을 만들지 못했습니다. 콘솔을 확인하세요.",
                "Could not create the copy. See the Console.",
                "複製を作成できませんでした。コンソールを確認してください。",
            } },

            // ---- Preset as the reference ---------------------------------------------------------------
            { "preset.select", new[] { "프리셋을 고르세요", "Select a preset", "プリセットを選択" } },
            { "preset.dropdownTitle", new[] { "프리셋 선택", "Select Preset", "プリセットを選択" } },
            { "preset.builtIn", new[] { "기본", "Built-in", "標準" } },
            { "preset.user", new[] { "사용자", "Custom", "ユーザー" } },
            { "preset.builtInLong", new[] { "lilToon 기본 프리셋", "lilToon built-in preset", "lilToon標準プリセット" } },
            { "preset.userLong", new[] { "사용자 프리셋", "Custom preset", "ユーザープリセット" } },
            { "preset.current", new[] { "현재", "current", "選択中" } },
            { "preset.none", new[] { "프리셋이 없습니다", "No presets", "プリセットがありません" } },
            { "preset.noneInProject", new[]
            {
                "프로젝트에서 lilToon 프리셋을 찾지 못했습니다. lilToon이 설치되어 있는지 확인하세요.",
                "No lilToon presets were found in the project. Check that lilToon is installed.",
                "プロジェクト内にlilToonプリセットが見つかりません。lilToonがインストールされているか確認してください。",
            } },
            { "preset.pick", new[]
            {
                "기준으로 쓸 lilToon 프리셋을 고르세요. 프로젝트 창에서 프리셋 파일을 끌어다 놓아도 됩니다.",
                "Choose the lilToon preset to use as the reference, or drag a preset file from the Project window.",
                "基準にするlilToonプリセットを選んでください。プロジェクトウィンドウからプリセットファイルをドラッグすることもできます。",
            } },
            { "preset.info", new[] { "{0}.asset · 값 {1}개 · {2}", "{0}.asset · {1} values · {2}", "{0}.asset · 値{1}個 · {2}" } },
            { "preset.infoTooltip", new[] { "클릭: 프로젝트에서 위치 표시", "Click: ping in Project", "クリック: プロジェクトで表示" } },
            { "preset.missing", new[]
            {
                "적용 항목 중 이 프리셋에 없는 값 {0}개는 건너뛰고, 대상 마테리얼의 원래 값을 유지합니다.",
                "{0} listed values are not stored in this preset; they are skipped and the target materials keep their own values.",
                "適用項目のうちこのプリセットにない値{0}個はスキップし、対象マテリアルの元の値を維持します。",
            } },
            { "preset.itemsInfo", new[]
            {
                "목록에 있는 항목 중 프리셋에 저장된 수치 값만 복사합니다. 프리셋의 텍스처, 윤곽선 켜기/끄기, 렌더링 설정은 적용하지 않습니다.",
                "Only the listed values that the preset stores are copied. The preset's textures, outline on/off and rendering settings are not applied.",
                "リストにある項目のうち、プリセットに保存された数値のみをコピーします。プリセットのテクスチャ、輪郭線のオン/オフ、描画設定は適用しません。",
            } },
            { "cov.none", new[] { "프리셋에 없음", "Not in preset", "プリセットになし" } },
            { "cov.has", new[] { "있음", "In preset", "あり" } },
            { "cov.count", new[] { "{0}/{1}", "{0}/{1}", "{0}/{1}" } },

            // ---- Properties to apply -----------------------------------------------------------------
            { "items.header", new[] { "적용 항목", "Properties to Apply", "適用項目" } },
            { "items.reset", new[] { "기본값", "Reset", "初期化" } },
            { "items.resetTitle", new[] { "적용 항목 초기화", "Reset properties", "適用項目の初期化" } },
            { "items.resetBody", new[]
            {
                "적용 항목을 기본값({0})으로 되돌릴까요?",
                "Reset the list to the default ({0})?",
                "適用項目を初期値({0})に戻しますか？",
            } },
            { "items.none", new[] { "목록이 비어 있습니다", "The list is empty", "リストは空です" } },
            { "items.sectionTooltip", new[] { "섹션 전체 · 프로퍼티 {0}개", "Whole section · {0} properties", "セクション全体 · プロパティ{0}個" } },
            { "items.info", new[]
            {
                "목록에 있는 항목의 수치 값만 기준 마테리얼에서 복사합니다. 텍스처, 노멀맵, 마스크는 대상 마테리얼의 값을 그대로 유지합니다.",
                "Only the values of the listed properties are copied from the reference material. Textures, normal maps and masks on the target materials are kept as they are.",
                "リストにある項目の数値のみを基準マテリアルからコピーします。テクスチャ・ノーマルマップ・マスクは対象マテリアルのものをそのまま維持します。",
            } },
            { "items.empty", new[]
            {
                "+ 버튼으로 적용할 항목을 추가하세요.",
                "Add properties with the + button.",
                "＋ボタンで適用する項目を追加してください。",
            } },
            { "items.colorWarn", new[]
            {
                "메인 컬러(_Color)를 복사하면 의상의 기본 색이 기준 마테리얼의 색으로 바뀝니다.",
                "Copying Main Color (_Color) changes the outfit's base tint to the reference material's color.",
                "メインカラー(_Color)をコピーすると、衣装の基本色が基準マテリアルの色に変わります。",
            } },

            // ---- Add dropdown ------------------------------------------------------------------------
            { "dropdown.title", new[] { "항목 추가", "Add Property", "項目を追加" } },
            { "dropdown.addAll", new[] { "섹션 전체 추가 ({0})", "Add whole section ({0})", "セクション全体を追加 ({0})" } },
            { "dropdown.empty", new[] { "추가할 항목이 없습니다", "Nothing left to add", "追加できる項目がありません" } },

            // ---- Target ------------------------------------------------------------------------------
            { "target.header", new[] { "적용 대상", "Target", "適用対象" } },
            { "target.none", new[]
            {
                "의상·헤어·액세서리의 루트 오브젝트를 넣으세요. 하위 파츠의 마테리얼을 모두 찾습니다.",
                "Assign the root object of an outfit, hair or accessory. Materials of every child part are detected.",
                "衣装・髪・アクセサリーのルートオブジェクトを指定してください。子パーツのマテリアルをすべて検出します。",
            } },
            { "target.noRenderers", new[]
            {
                "이 오브젝트 아래에서 마테리얼이 있는 렌더러를 찾지 못했습니다.",
                "No renderers with materials were found under this object.",
                "このオブジェクトの下にマテリアルを持つレンダラーが見つかりませんでした。",
            } },
            { "target.rescan", new[] { "다시 검색", "Rescan", "再検索" } },
            { "target.detected", new[] { "감지된 마테리얼", "Detected Materials", "検出されたマテリアル" } },
            { "target.summary", new[]
            {
                "파츠 {0}개 중 {1}개 선택 · 적용 {2} · 건너뜀 {3}",
                "{1} of {0} parts selected · {2} to apply · {3} skipped",
                "パーツ{0}個中{1}個選択 · 適用{2} · スキップ{3}",
            } },
            { "target.selectAll", new[] { "전체 선택", "Select all", "すべて選択" } },
            { "target.selectNone", new[] { "전체 해제", "Select none", "すべて解除" } },
            { "target.matCount", new[] { "마테리얼 {0}", "{0} materials", "マテリアル{0}" } },
            { "target.rowTooltip", new[]
            {
                "클릭: 프로젝트에서 위치 표시 · 더블클릭: 선택",
                "Click: ping in Project · Double-click: select",
                "クリック: プロジェクトで表示 · ダブルクリック: 選択",
            } },

            // ---- Tags on material rows ---------------------------------------------------------------
            { "tag.shared", new[] { "공유", "Shared", "共有" } },
            { "tag.reference", new[] { "기준 · 변경 안 함", "Reference · unchanged", "基準 · 変更なし" } },
            { "tag.ShaderMissing", new[] { "오류 · 셰이더 없음", "Error · Missing shader", "エラー · シェーダーなし" } },
            { "tag.ReadOnly", new[] { "오류 · 수정 불가", "Error · Read-only", "エラー · 編集不可" } },
            { "tag.NotLilToon", new[] { "경고 · lilToon 아님", "Warning · Not lilToon", "警告 · lilToon以外" } },
            { "tag.Partial", new[] { "경고 · 일부 미지원", "Warning · Partial", "警告 · 一部未対応" } },
            { "tag.NoneApplicable", new[] { "경고 · 적용 항목 없음", "Warning · Nothing to apply", "警告 · 適用項目なし" } },
            { "tag.Locked", new[] { "경고 · 일부 잠김", "Warning · Partly locked", "警告 · 一部ロック" } },
            { "tag.Checkout", new[] { "경고 · 체크아웃 필요", "Warning · Checkout needed", "警告 · チェックアウト必要" } },
            { "tag.SharedChange", new[] { "경고 · 공유라 함께 바뀜", "Warning · Changes (shared)", "警告 · 共有のため変更" } },
            { "tag.excluded", new[] { "제외", "Excluded", "除外" } },

            // ---- Issue list ----------------------------------------------------------------------------
            { "issues.header", new[] { "문제 {0}건", "{0} issues", "問題 {0}件" } },
            { "issues.errors", new[] { "오류 {0}", "Errors {0}", "エラー {0}" } },
            { "issues.warnings", new[] { "경고 {0}", "Warnings {0}", "警告 {0}" } },
            { "issues.hint", new[]
            {
                "항목을 클릭하면 하이어라키에서 해당 오브젝트를 선택합니다",
                "Click an item to select its object in the Hierarchy",
                "項目をクリックするとヒエラルキーで該当オブジェクトを選択します",
            } },
            { "level.error", new[] { "오류", "Error", "エラー" } },
            { "level.warning", new[] { "경고", "Warning", "警告" } },
            { "issue.ShaderMissing", new[]
            {
                "셰이더 없음 – 셰이더가 깨진 마테리얼 (분홍색)",
                "Missing shader – the material's shader is broken (pink)",
                "シェーダーなし – シェーダーが壊れたマテリアル(ピンク色)",
            } },
            { "issue.ReadOnlyImported", new[]
            {
                "수정 불가 – FBX 등 모델 파일에 포함된 읽기 전용 마테리얼",
                "Read-only – embedded in a model file such as FBX",
                "編集不可 – FBXなどのモデルファイルに含まれる読み取り専用マテリアル",
            } },
            { "issue.ReadOnlyPackage", new[]
            {
                "수정 불가 – 읽기 전용 패키지 안의 마테리얼",
                "Read-only – inside an immutable package",
                "編集不可 – 読み取り専用パッケージ内のマテリアル",
            } },
            { "issue.ReadOnlyFile", new[]
            {
                "수정 불가 – 파일이 잠겨 있거나 읽기 전용",
                "Read-only – the file is locked or read-only",
                "編集不可 – ファイルがロックされているか読み取り専用です",
            } },
            { "issue.NotLilToon", new[]
            {
                "lilToon 아님 – {0} 셰이더, 건너뜀",
                "Not lilToon – {0} shader, skipped",
                "lilToonではありません – {0}シェーダー、スキップ",
            } },
            { "issue.Partial", new[]
            {
                "일부 미지원 – {0}개 항목 건너뜀 (나머지는 적용)",
                "Partially supported – {0} properties skipped (the rest are applied)",
                "一部未対応 – {0}個の項目をスキップ(残りは適用)",
            } },
            { "issue.NoneApplicable", new[]
            {
                "적용할 수 있는 항목 없음 – 목록의 항목이 {0} 셰이더에 없어 건너뜀",
                "Nothing to apply – none of the listed properties exist on the {0} shader, skipped",
                "適用できる項目なし – リストの項目が{0}シェーダーにないためスキップ",
            } },
            { "issue.Locked", new[]
            {
                "일부 잠김 – 상위 마테리얼(Material Variant)에서 잠긴 항목 {0}개 건너뜀",
                "Partly locked – {0} properties locked by a parent Material Variant are skipped",
                "一部ロック – 親マテリアル(Material Variant)でロックされた{0}個の項目をスキップ",
            } },
            { "issue.Checkout", new[]
            {
                "체크아웃 필요 – 적용할 때 편집 가능 상태로 전환을 시도합니다",
                "Checkout needed – LITTOOL will try to make the file editable when applying",
                "チェックアウトが必要 – 適用時に編集可能な状態への切り替えを試みます",
            } },
            { "issue.SharedChange", new[]
            {
                "공유 마테리얼 – 선택한 다른 파츠에서도 쓰고 있어 여기서도 함께 바뀜",
                "Shared material – a selected part also uses it, so it changes here too",
                "共有マテリアル – 選択した他のパーツでも使われているため、ここでも変更されます",
            } },

            // ---- Apply -----------------------------------------------------------------------------------
            { "apply.button", new[] { "{0}개 마테리얼에 적용", "Apply to {0} materials", "{0}個のマテリアルに適用" } },
            { "apply.playMode", new[]
            {
                "플레이 모드에서는 적용할 수 없습니다. 플레이 모드를 끝낸 뒤 적용하세요.",
                "Cannot apply in Play Mode. Exit Play Mode first.",
                "プレイモード中は適用できません。プレイモードを終了してから適用してください。",
            } },
            { "apply.confirmTitle", new[] { "원본 마테리얼 수정", "Modify original materials", "元のマテリアルを変更" } },
            { "apply.confirmBody", new[]
            {
                "{0}개 마테리얼의 원본 파일을 {1} 값으로 직접 수정합니다.{2}\n\n같은 마테리얼을 쓰는 다른 아바타·의상에도 함께 반영됩니다. 계속할까요?",
                "This will modify the original files of {0} materials with the values of {1}.{2}\n\nOther avatars and outfits that use the same materials will change too. Continue?",
                "{0}個のマテリアルの元ファイルを{1}の値で直接変更します。{2}\n\n同じマテリアルを使う他のアバター・衣装にも反映されます。続行しますか？",
            } },
            { "apply.srcMaterial", new[] { "기준 마테리얼 ‘{0}’의", "the reference material ‘{0}’", "基準マテリアル「{0}」" } },
            { "apply.srcPreset", new[] { "프리셋 ‘{0}’의", "the preset ‘{0}’", "プリセット「{0}」" } },
            { "apply.labelMaterial", new[] { "기준 마테리얼 {0}", "reference material {0}", "基準マテリアル {0}" } },
            { "apply.labelPreset", new[] { "프리셋 {0}", "preset {0}", "プリセット {0}" } },
            { "apply.confirmSkip", new[]
            {
                " 문제가 있는 {0}개는 건너뜁니다.",
                " {0} materials with issues will be skipped.",
                "問題のある{0}個はスキップします。",
            } },
            { "apply.ok", new[] { "적용", "Apply", "適用" } },
            { "apply.progressTitle", new[] { "LITTOOL", "LITTOOL", "LITTOOL" } },
            { "apply.progress", new[] { "마테리얼 적용 중... ({0}/{1})", "Applying materials... ({0}/{1})", "マテリアルを適用中... ({0}/{1})" } },
            { "apply.done", new[]
            {
                "적용 완료: {0}개 마테리얼, {1} ({3}){2} (Ctrl+Z로 되돌릴 수 있음)",
                "Applied to {0} materials: {1} ({3}){2} (Undo with Ctrl+Z)",
                "適用完了: {0}個のマテリアル、{1} ({3}){2} (Ctrl+Zで元に戻せます)",
            } },
            { "apply.doneSkip", new[] { " · 건너뜀 {0}개", " · {0} skipped", " · スキップ{0}個" } },
            { "apply.failed", new[]
            {
                "{0}개 마테리얼은 수정하지 못했습니다(체크아웃 실패 등). 콘솔을 확인하세요.",
                "{0} materials could not be modified (e.g. checkout failed). See the Console.",
                "{0}個のマテリアルは変更できませんでした(チェックアウト失敗など)。コンソールを確認してください。",
            } },
            { "apply.error", new[]
            {
                "적용 중 오류가 발생했습니다. 콘솔을 확인하세요. 이미 바뀐 값은 Ctrl+Z로 되돌릴 수 있습니다.",
                "An error occurred while applying. See the Console. Values already changed can be undone with Ctrl+Z.",
                "適用中にエラーが発生しました。コンソールを確認してください。変更済みの値はCtrl+Zで元に戻せます。",
            } },
            { "apply.nothing", new[] { "적용할 마테리얼이 없습니다.", "There are no materials to apply.", "適用できるマテリアルがありません。" } },
        };
    }
}
