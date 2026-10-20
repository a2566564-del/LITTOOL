using System.Collections.Generic;

namespace Littool
{
    // Texts of the Bone Scale tab (Korean, English, Japanese).
    internal static partial class L
    {
        static void Add(Dictionary<string, string[]> table, string key, string ko, string en, string ja)
        {
            table[key] = new[] { ko, en, ja };
        }

        static void AddBoneScaleTexts(Dictionary<string, string[]> t)
        {
            Add(t, "bs.playMode",
                "플레이 모드에서는 본 스케일을 바꿀 수 없습니다. 플레이 모드를 끝낸 뒤 사용하세요.",
                "Bone scales cannot be changed in Play Mode. Exit Play Mode first.",
                "プレイモード中はボーンスケールを変更できません。プレイモードを終了してから使ってください。");
            Add(t, "bs.undoHint", " (Ctrl+Z로 되돌릴 수 있음)", " (Undo with Ctrl+Z)", " (Ctrl+Zで元に戻せます)");
            Add(t, "bs.missing", "없음", "missing", "なし");

            // ---- Avatar -------------------------------------------------------------------------------
            Add(t, "bs.avatar.header", "아바타", "Avatar", "アバター");
            Add(t, "bs.avatar.tooltip",
                "씬에 있는 아바타의 루트 오브젝트 (Animator가 있는 휴머노이드)",
                "The avatar's root object in the scene (a humanoid with an Animator)",
                "シーン内のアバターのルートオブジェクト(Animator付きのヒューマノイド)");
            Add(t, "bs.avatar.none",
                "씬에 있는 아바타를 넣으세요. 휴머노이드 본과 Breast 본을 자동으로 찾습니다.",
                "Assign the avatar in the scene. Humanoid bones and Breast bones are found automatically.",
                "シーン内のアバターを指定してください。ヒューマノイドのボーンとBreastボーンを自動で探します。");
            Add(t, "bs.avatar.prefabAsset",
                "프로젝트 창의 프리팹 파일은 쓸 수 없습니다. 씬(하이어라키)에 있는 아바타를 넣으세요.",
                "Prefab files from the Project window cannot be used. Assign the avatar in the scene (Hierarchy).",
                "プロジェクトウィンドウのプレハブファイルは使えません。シーン(ヒエラルキー)にあるアバターを指定してください。");
            Add(t, "bs.avatar.noAnimator",
                "이 오브젝트에 Animator가 없습니다. 아바타의 루트 오브젝트를 넣으세요.",
                "This object has no Animator. Assign the avatar's root object.",
                "このオブジェクトにはAnimatorがありません。アバターのルートオブジェクトを指定してください。");
            Add(t, "bs.avatar.notHumanoid",
                "휴머노이드 아바타가 아닙니다. 모델의 Rig 설정이 Humanoid인지 확인하세요.",
                "This is not a humanoid avatar. Check that the model's Rig is set to Humanoid.",
                "ヒューマノイドのアバターではありません。モデルのRig設定がHumanoidか確認してください。");
            Add(t, "bs.avatar.info", "휴머노이드 · Breast 본 찾음 ({0})", "Humanoid · Breast bones found ({0})", "ヒューマノイド · Breastボーンを検出 ({0})");
            Add(t, "bs.avatar.infoManual", "휴머노이드 · Breast 본 직접 지정 ({0})", "Humanoid · Breast bones assigned ({0})", "ヒューマノイド · Breastボーンを手動指定 ({0})");
            Add(t, "bs.avatar.infoNoBreast",
                "휴머노이드 · Breast 본을 찾지 못함 (본 목록의 Breast에서 직접 지정)",
                "Humanoid · Breast bones not found (assign them under Breast in the bone list)",
                "ヒューマノイド · Breastボーンが見つかりません(ボーン一覧のBreastで手動指定)");

            // ---- Outfits ------------------------------------------------------------------------------
            Add(t, "bs.outfit.header", "의상", "Outfits", "衣装");
            Add(t, "bs.outfit.autoMatch", "새 의상 자동 맞추기", "Auto-match new outfits", "新しい衣装を自動で合わせる");
            Add(t, "bs.outfit.autoMatchTooltip",
                "창이 열려 있는 동안 새로 입힌 의상(MA Merge Armature)을 지금 체형에 바로 맞춥니다.",
                "While this window is open, newly dressed outfits (MA Merge Armature) are matched to the current body shape at once.",
                "このウィンドウが開いている間、新しく着せた衣装(MA Merge Armature)を今の体型にすぐ合わせます。");
            Add(t, "bs.outfit.noMa",
                "Modular Avatar가 설치되어 있지 않아 의상을 찾지 않습니다. 아바타 본만 바뀝니다.",
                "Modular Avatar is not installed, so outfits are not detected. Only the avatar's bones change.",
                "Modular Avatarがインストールされていないため、衣装は検出しません。アバターのボーンだけが変わります。");
            Add(t, "bs.outfit.none",
                "아바타 안에서 MA Merge Armature가 있는 의상을 찾지 못했습니다. 아바타 본만 바뀝니다.",
                "No outfits with MA Merge Armature were found under the avatar. Only the avatar's bones change.",
                "アバター内にMA Merge Armatureのある衣装が見つかりません。アバターのボーンだけが変わります。");
            Add(t, "bs.outfit.checkNote",
                "체크 = 씬에서 의상 켜기/끄기 (Ctrl+Z 가능) · 꺼진 의상도 체형은 계속 맞춰요",
                "Checkbox = switch the outfit on/off in the scene (Ctrl+Z works) · outfits that are off still follow the body shape",
                "チェック = シーンで衣装をオン/オフ (Ctrl+Z可) · オフの衣装も体型は合わせ続けます");
            Add(t, "bs.outfit.autoOn",
                "켜짐: 창이 열려 있는 동안 새로 입힌 의상은 지금 체형에 바로 맞춥니다. 원래 있던 의상은 다르면 표시만 합니다.",
                "On: outfits dressed while this window is open are matched to the current body shape at once. Existing outfits are only marked when they differ.",
                "オン: ウィンドウが開いている間に着せた衣装は今の体型にすぐ合わせます。元からある衣装は違う場合に表示だけします。");
            Add(t, "bs.outfit.autoOff",
                "꺼짐: 새 의상도 체형이 다르면 표시만 하고, [맞추기]를 눌러야 맞춥니다.",
                "Off: new outfits are only marked when they differ; press [Match] to match them.",
                "オフ: 新しい衣装も体型が違えば表示だけし、[合わせる]を押すと合わせます。");
            Add(t, "bs.outfit.toggleTooltip",
                "씬에서 이 의상을 켜거나 끕니다 (인스펙터의 체크와 같음)",
                "Switches this outfit on or off in the scene (same as the Inspector checkbox)",
                "シーンでこの衣装をオン/オフします(インスペクターのチェックと同じ)");
            Add(t, "bs.outfit.tagLocked", "자동 · MA 잠금", "Auto · MA lock", "自動 · MAロック");
            Add(t, "bs.outfit.tagUnlocked", "값 넣음 · 잠금 꺼짐", "Written · lock off", "値を書き込み · ロックなし");
            Add(t, "bs.outfit.tagOffLocked", "꺼짐 · MA 잠금", "Off · MA lock", "オフ · MAロック");
            Add(t, "bs.outfit.tagOffUnlocked", "꺼짐 · 잠금 꺼짐", "Off · lock off", "オフ · ロックなし");
            Add(t, "bs.outfit.tagMatched", "방금 맞춤 · 본 {0}개", "Just matched · {0} bones", "合わせました · ボーン{0}個");
            Add(t, "bs.outfit.tagMismatch", "체형 다름 · {0} {1} → {2}", "Differs · {0} {1} → {2}", "体型が違う · {0} {1} → {2}");
            Add(t, "bs.outfit.fix", "맞추기", "Match", "合わせる");
            Add(t, "bs.outfit.fixTooltip",
                "이 의상의 본에 아바타의 지금 값을 넣습니다 (Ctrl+Z 가능)",
                "Copies the avatar's current values to this outfit's bones (Ctrl+Z works)",
                "この衣装のボーンにアバターの今の値を入れます(Ctrl+Z可)");
            Add(t, "bs.part.floorRef", "바닥 기준", "floor reference", "床の基準");
            Add(t, "bs.part.noBones", "목록의 본 안 씀", "uses no listed bones", "一覧のボーンは不使用");
            Add(t, "bs.fix.done",
                "‘{0}’의 본 {1}개를 지금 체형에 맞췄습니다.",
                "Matched {1} bones of ‘{0}’ to the current body shape.",
                "「{0}」のボーン{1}個を今の体型に合わせました。");
            Add(t, "bs.auto.done",
                "새 의상 {0}을(를) 지금 체형에 맞췄습니다 (본 {1}개).",
                "Matched the new outfit {0} to the current body shape ({1} bones).",
                "新しい衣装 {0} を今の体型に合わせました(ボーン{1}個)。");

            // ---- Bones --------------------------------------------------------------------------------
            Add(t, "bs.bones.header", "본", "Bones", "ボーン");
            Add(t, "bs.bones.symmetry", "좌우 대칭", "Symmetry", "左右対称");
            Add(t, "bs.bones.symmetryTooltip",
                "켜면 왼쪽과 오른쪽 본이 함께 바뀝니다.",
                "When on, the left and right bones change together.",
                "オンにすると左右のボーンが一緒に変わります。");
            Add(t, "bs.bones.resetAll", "전체 1로", "All to 1", "すべて1に");
            Add(t, "bs.bones.resetAllTooltip",
                "목록의 모든 본을 Scale 1로 되돌립니다 (Ctrl+Z 가능)",
                "Sets every listed bone back to scale 1 (Ctrl+Z works)",
                "一覧のすべてのボーンをScale 1に戻します(Ctrl+Z可)");
            Add(t, "bs.bones.resetAllDone", "목록의 본 {0}개를 1로 되돌렸습니다.", "Set {0} listed bones back to 1.", "一覧のボーン{0}個を1に戻しました。");
            Add(t, "bs.bones.resetAllNothing", "목록의 본이 모두 이미 1입니다.", "Every listed bone is already 1.", "一覧のボーンはすべてすでに1です。");
            Add(t, "bs.bones.hint",
                "파란 점 = 값을 바꾼 본 · 점이나 목록을 눌러 선택",
                "Blue dot = changed bone · click a dot or a row to select",
                "青い点 = 値を変えたボーン · 点か一覧をクリックして選択");
            Add(t, "bs.bones.front", "앞에서 본 모습", "Front view", "正面から見た姿");
            Add(t, "bs.bones.none", "목록이 비어 있습니다", "The list is empty", "一覧は空です");
            Add(t, "bs.kind.humanoid", "휴머노이드", "Humanoid", "ヒューマノイド");
            Add(t, "bs.kind.byName", "이름으로 찾음", "Found by name", "名前で検出");
            Add(t, "bs.kind.manual", "직접 지정", "Assigned", "手動指定");
            Add(t, "bs.kind.armature", "Armature", "Armature", "Armature");
            Add(t, "bs.panel.empty", "본 목록에서 본을 고르세요.", "Select a bone in the list.", "一覧からボーンを選んでください。");
            Add(t, "bs.panel.symNote", "좌우 대칭: {0} · {1} 함께 바뀜", "Symmetry: {0} and {1} change together", "左右対称: {0} · {1} が一緒に変わります");
            Add(t, "bs.panel.split", "X·Y·Z 따로", "Separate X·Y·Z", "X·Y·Zを個別に");
            Add(t, "bs.panel.splitTooltip", "켜면 X, Y, Z 값을 따로 정합니다.", "When on, X, Y and Z are set separately.", "オンにするとX・Y・Zを個別に設定します。");
            Add(t, "bs.panel.reset", "1로 초기화", "Reset to 1", "1にリセット");
            Add(t, "bs.panel.resetTooltip",
                "이 본(대칭이면 양쪽)을 Scale 1로 되돌립니다 (Ctrl+Z 가능)",
                "Sets this bone (both sides with symmetry) back to scale 1 (Ctrl+Z works)",
                "このボーン(対称なら両側)をScale 1に戻します(Ctrl+Z可)");
            Add(t, "bs.panel.outfits", "의상의 같은 본", "Same bone in outfits", "衣装の同じボーン");
            Add(t, "bs.panel.noOutfit", "이 본을 쓰는 의상 없음", "No outfit uses this bone", "このボーンを使う衣装はありません");
            Add(t, "bs.panel.noMa", "Modular Avatar가 없어 의상은 바뀌지 않음", "Modular Avatar is missing; outfits are not changed", "Modular Avatarがないため衣装は変わりません");
            Add(t, "bs.panel.missing", "이 아바타에서 이 본을 찾지 못했습니다.", "This bone was not found on this avatar.", "このアバターではこのボーンが見つかりません。");
            Add(t, "bs.panel.breastMissing",
                "Breast 본을 이름으로 찾지 못했습니다. 위 칸에 왼쪽(L)과 오른쪽(R) 본을 넣으세요.",
                "Breast bones were not found by name. Assign the left (L) and right (R) bones above.",
                "Breastボーンが名前で見つかりません。上の欄に左(L)と右(R)のボーンを指定してください。");
            Add(t, "bs.panel.breastTooltip",
                "아바타 안의 Breast 본. 비우면 이름으로 찾은 본을 씁니다.",
                "The breast bone inside the avatar. Leave empty to use the one found by name.",
                "アバター内のBreastボーン。空にすると名前で見つけたボーンを使います。");
            Add(t, "bs.panel.breastOutside", "아바타 안에 있는 본만 지정할 수 있습니다.", "Only bones inside the avatar can be assigned.", "アバター内のボーンだけを指定できます。");
            Add(t, "bs.line.locked", "MA가 자동으로 따라감", "MA follows automatically", "MAが自動で追従");
            Add(t, "bs.line.lockedOff", "꺼져 있어도 MA가 따라감", "off, MA still follows", "オフでもMAが追従");
            Add(t, "bs.line.ratio", "같은 비율로 {0}", "same ratio, {0}", "同じ比率で{0}");
            Add(t, "bs.line.ratioOff", "꺼져 있어도 같은 비율로 {0}", "off, still same ratio, {0}", "オフでも同じ比率で{0}");
            Add(t, "bs.line.mismatch", "체형 다름 · 위 [맞추기]로 맞춰요", "differs · use [Match] above", "体型が違う · 上の[合わせる]で合わせます");
            Add(t, "bs.add.title", "본 추가", "Add Bone", "ボーンを追加");
            Add(t, "bs.add.allListed", "모두 목록에 있음", "All are listed", "すべて一覧にあります");
            Add(t, "bs.add.pick", "Armature에서 다른 본 고르기…", "Pick another bone from the Armature…", "Armatureから他のボーンを選ぶ…");
            Add(t, "bs.pick.this", "{0} (이 본)", "{0} (this bone)", "{0} (このボーン)");

            // ---- Body shapes --------------------------------------------------------------------------
            Add(t, "bs.preset.header", "체형", "Body Shapes", "体型");
            Add(t, "bs.preset.empty", "저장된 체형 없음", "No saved shapes", "保存された体型なし");
            Add(t, "bs.preset.emptyHint",
                "아직 저장된 체형이 없어요. 값을 맞춘 뒤 [현재 값 저장]을 누르면 여기에 쌓여요.",
                "No body shapes yet. Set the values, then press [Save current] to keep them here.",
                "まだ保存された体型はありません。値を合わせてから[今の値を保存]を押すとここに保存されます。");
            Add(t, "bs.preset.apply", "적용", "Apply", "適用");
            Add(t, "bs.preset.applyTooltip",
                "고른 체형의 값을 아바타에 넣습니다 (Ctrl+Z 가능)",
                "Writes the selected shape's values to the avatar (Ctrl+Z works)",
                "選んだ体型の値をアバターに入れます(Ctrl+Z可)");
            Add(t, "bs.preset.save", "현재 값 저장", "Save current", "今の値を保存");
            Add(t, "bs.preset.saveTooltip",
                "목록에 있는 본의 지금 값을 이름을 붙여 저장합니다. 이 프로젝트의 다른 아바타에서도 불러올 수 있어요.",
                "Saves the current values of the listed bones under a name. Other avatars in this project can load it too.",
                "一覧のボーンの今の値を名前を付けて保存します。このプロジェクトの他のアバターでも読み込めます。");
            Add(t, "bs.preset.rename", "이름 바꾸기", "Rename", "名前を変更");
            Add(t, "bs.preset.delete", "삭제", "Delete", "削除");
            Add(t, "bs.preset.name", "이름", "Name", "名前");
            Add(t, "bs.preset.saveOk", "저장", "Save", "保存");
            Add(t, "bs.preset.overwrite", "덮어쓰기", "Overwrite", "上書き");
            Add(t, "bs.preset.renameOk", "바꾸기", "Rename", "変更");
            Add(t, "bs.preset.nameEmpty", "이름을 입력해 주세요.", "Enter a name.", "名前を入力してください。");
            Add(t, "bs.preset.nameTaken",
                "같은 이름의 체형이 이미 있어요. 다른 이름을 써 주세요.",
                "A shape with this name already exists. Use another name.",
                "同じ名前の体型がすでにあります。別の名前にしてください。");
            Add(t, "bs.preset.overwriteNote",
                "같은 이름의 체형이 있어요. 저장하면 그 체형의 값을 덮어써요.",
                "A shape with this name exists. Saving overwrites its values.",
                "同じ名前の体型があります。保存するとその体型の値を上書きします。");
            Add(t, "bs.preset.saveNote",
                "지금 목록에 있는 본 {0}개의 값을 이 이름으로 저장해요.",
                "Saves the values of the {0} listed bones under this name.",
                "今一覧にあるボーン{0}個の値をこの名前で保存します。");
            Add(t, "bs.preset.renameNote",
                "‘{0}’의 이름만 바뀌고 저장된 값은 그대로예요.",
                "Only the name of ‘{0}’ changes; its saved values stay.",
                "「{0}」の名前だけが変わり、保存された値はそのままです。");
            Add(t, "bs.preset.defaultName", "새 체형 {0}", "New shape {0}", "新しい体型 {0}");
            Add(t, "bs.preset.saved",
                "현재 값을 ‘{0}’(으)로 저장했습니다 (본 {1}개). 이 프로젝트의 다른 아바타에서도 불러올 수 있어요.",
                "Saved the current values as ‘{0}’ ({1} bones). Other avatars in this project can load it too.",
                "今の値を「{0}」として保存しました(ボーン{1}個)。このプロジェクトの他のアバターでも読み込めます。");
            Add(t, "bs.preset.overwritten",
                "‘{0}’을(를) 현재 값으로 덮어썼습니다 (본 {1}개).",
                "Overwrote ‘{0}’ with the current values ({1} bones).",
                "「{0}」を今の値で上書きしました(ボーン{1}個)。");
            Add(t, "bs.preset.renamed", "‘{0}’의 이름을 ‘{1}’(으)로 바꿨습니다.", "Renamed ‘{0}’ to ‘{1}’.", "「{0}」の名前を「{1}」に変更しました。");
            Add(t, "bs.preset.deleteTitle", "체형 삭제", "Delete body shape", "体型を削除");
            Add(t, "bs.preset.deleteBody",
                "‘{0}’ 체형을 삭제할까요? 삭제한 체형은 되돌릴 수 없습니다. (아바타의 본 값은 바뀌지 않습니다.)",
                "Delete the body shape ‘{0}’? This cannot be undone. (The avatar's bone values do not change.)",
                "体型「{0}」を削除しますか？削除した体型は元に戻せません。(アバターのボーンの値は変わりません。)");
            Add(t, "bs.preset.deleteOk", "삭제", "Delete", "削除");
            Add(t, "bs.preset.deleted", "‘{0}’ 체형을 삭제했습니다.", "Deleted the body shape ‘{0}’.", "体型「{0}」を削除しました。");
            Add(t, "bs.preset.nothing",
                "‘{0}’에 저장된 본을 이 아바타에서 찾지 못했습니다.",
                "None of the bones saved in ‘{0}’ were found on this avatar.",
                "「{0}」に保存されたボーンがこのアバターに見つかりません。");
            Add(t, "bs.preset.applied", "‘{0}’ 적용: 본 {1}개", "Applied ‘{0}’: {1} bones", "「{0}」を適用: ボーン{1}個");
            Add(t, "bs.preset.appliedWritten", " · 의상 {0}에도 같은 비율로 넣음", " · written to {0} at the same ratio", " · 衣装 {0} にも同じ比率で書き込み");
            Add(t, "bs.preset.appliedFollow", " · {0}은(는) MA가 따라감", " · MA keeps {0} following", " · {0} はMAが追従");
            Add(t, "bs.preset.appliedSkipped", " · 이 아바타에 없는 본 {0}개는 건너뜀", " · {0} bones missing on this avatar were skipped", " · このアバターにないボーン{0}個はスキップ");

            // ---- Floor ---------------------------------------------------------------------------------
            Add(t, "bs.floor.header", "바닥 맞추기", "Floor", "床合わせ");
            Add(t, "bs.floor.basis", "켜진 의상의 신발 밑창 기준 · MA Floor Adjuster", "Soles of the outfits that are on · MA Floor Adjuster", "オンの衣装の靴底基準 · MA Floor Adjuster");
            Add(t, "bs.floor.refresh", "다시 재기", "Measure again", "測り直す");
            Add(t, "bs.floor.measuring", "바닥 높이를 재는 중…", "Measuring the floor height…", "床の高さを測定中…");
            Add(t, "bs.floor.nothing",
                "켜져 있는 렌더러가 없어 바닥 높이를 잴 수 없습니다.",
                "No visible renderers to measure the floor height from.",
                "表示中のレンダラーがないため床の高さを測れません。");
            Add(t, "bs.floor.okNoAdjuster",
                "밑창이 바닥과 맞아요. Floor Adjuster가 필요 없어요.",
                "The soles are on the floor. No Floor Adjuster is needed.",
                "靴底が床に合っています。Floor Adjusterは不要です。");
            Add(t, "bs.floor.below",
                "밑창({0})이 바닥보다 {1} cm 아래 · Floor Adjuster 없음",
                "Soles ({0}) are {1} cm below the floor · no Floor Adjuster",
                "靴底({0})が床より{1} cm下 · Floor Adjusterなし");
            Add(t, "bs.floor.above",
                "밑창({0})이 바닥보다 {1} cm 위에 떠 있음 · Floor Adjuster 없음",
                "Soles ({0}) float {1} cm above the floor · no Floor Adjuster",
                "靴底({0})が床より{1} cm浮いています · Floor Adjusterなし");
            Add(t, "bs.floor.okAdjusterBelow",
                "Floor Adjuster가 밑창 높이에 있어요 (바닥보다 {0} cm 아래).",
                "The Floor Adjuster is at sole height ({0} cm below the floor).",
                "Floor Adjusterが靴底の高さにあります(床より{0} cm下)。");
            Add(t, "bs.floor.okAdjusterAbove",
                "Floor Adjuster가 밑창 높이에 있어요 (바닥보다 {0} cm 위).",
                "The Floor Adjuster is at sole height ({0} cm above the floor).",
                "Floor Adjusterが靴底の高さにあります(床より{0} cm上)。");
            Add(t, "bs.floor.off",
                "넣은 Floor Adjuster와 밑창({0}) 높이가 {1} cm 달라요.",
                "The Floor Adjuster and the soles ({0}) are {1} cm apart.",
                "Floor Adjusterと靴底({0})の高さが{1} cm違います。");
            Add(t, "bs.floor.multiple",
                "Floor Adjuster가 {0}개 켜져 있습니다. 여러 개가 켜져 있으면 MA는 바닥을 맞추지 않습니다. 하나만 남기세요.",
                "{0} Floor Adjusters are active. MA does not adjust the floor when more than one is active; keep only one.",
                "Floor Adjusterが{0}個オンです。複数オンのときMAは床を合わせません。1つだけにしてください。");
            Add(t, "bs.floor.place", "Floor Adjuster 넣기", "Add Floor Adjuster", "Floor Adjusterを追加");
            Add(t, "bs.floor.move", "위치 다시 맞추기", "Move to the soles", "位置を合わせ直す");
            Add(t, "bs.floor.notePlace",
                "누르면 아바타 안에 ‘LITTOOL Floor’를 만들고 밑창 높이에 놓아요 (MA 1.17.0 이상, Ctrl+Z 가능).",
                "Creates ‘LITTOOL Floor’ inside the avatar at sole height (MA 1.17.0 or later, Ctrl+Z works).",
                "アバター内に「LITTOOL Floor」を作り、靴底の高さに置きます(MA 1.17.0以上、Ctrl+Z可)。");
            Add(t, "bs.floor.noteMove",
                "아바타 안의 ‘{0}’에 Floor Adjuster가 있어요. 업로드할 때 MA가 이 높이를 바닥으로 맞춰요.",
                "‘{0}’ inside the avatar has the Floor Adjuster. MA uses its height as the floor when uploading.",
                "アバター内の「{0}」にFloor Adjusterがあります。アップロード時にMAがこの高さを床に合わせます。");
            Add(t, "bs.floor.noMa",
                "Modular Avatar 1.17.0 이상이 필요해요. Modular Avatar가 설치되어 있지 않습니다.",
                "Requires Modular Avatar 1.17.0 or later. Modular Avatar is not installed.",
                "Modular Avatar 1.17.0以上が必要です。Modular Avatarがインストールされていません。");
            Add(t, "bs.floor.oldMa",
                "설치된 Modular Avatar {0}에는 Floor Adjuster가 없어요. 1.17.0 이상으로 올려 주세요.",
                "The installed Modular Avatar {0} has no Floor Adjuster. Update to 1.17.0 or later.",
                "インストールされているModular Avatar {0}にはFloor Adjusterがありません。1.17.0以上に更新してください。");
            Add(t, "bs.floor.placed",
                "‘{0}’을(를) 만들어 밑창 높이(바닥보다 {1} cm 아래)에 놓았습니다.",
                "Created ‘{0}’ at sole height ({1} cm below the floor).",
                "「{0}」を作り、靴底の高さ(床より{1} cm下)に置きました。");
            Add(t, "bs.floor.placedAbove",
                "‘{0}’을(를) 만들어 밑창 높이(바닥보다 {1} cm 위)에 놓았습니다.",
                "Created ‘{0}’ at sole height ({1} cm above the floor).",
                "「{0}」を作り、靴底の高さ(床より{1} cm上)に置きました。");
            Add(t, "bs.floor.moved",
                "‘{0}’을(를) 밑창 높이(바닥보다 {1} cm 아래)로 옮겼습니다.",
                "Moved ‘{0}’ to sole height ({1} cm below the floor).",
                "「{0}」を靴底の高さ(床より{1} cm下)に移動しました。");
            Add(t, "bs.floor.movedAbove",
                "‘{0}’을(를) 밑창 높이(바닥보다 {1} cm 위)로 옮겼습니다.",
                "Moved ‘{0}’ to sole height ({1} cm above the floor).",
                "「{0}」を靴底の高さ(床より{1} cm上)に移動しました。");
            Add(t, "bs.floor.failed",
                "Floor Adjuster를 넣지 못했습니다. 콘솔을 확인하세요.",
                "Could not add the Floor Adjuster. See the Console.",
                "Floor Adjusterを追加できませんでした。コンソールを確認してください。");
        }
    }
}
