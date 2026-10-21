# LITTOOL v1.3.4

[한국어](#한국어) · [English](#english) · [日本語](#日本語)

---

## 한국어

LITTOOL은 VRChat 아바타 제작을 돕는 Unity 에디터 툴 모음입니다.
첫 번째 기능 **마테리얼 일괄 적용**은 기준 마테리얼이나 lilToon 프리셋의 설정값(그림자, 림 라이트 등)을
의상·헤어·액세서리 안의 lilToon 마테리얼에 한 번에 복사합니다.
텍스처, 노멀맵, 마스크는 각 마테리얼의 것을 그대로 유지합니다.
두 번째 기능 **본 스케일**은 아바타의 본 Scale 값(머리, 목, 가슴, 팔, 다리 등)을 한 화면에서 바꾸고,
Modular Avatar로 입힌 의상과 바닥 높이까지 함께 맞춥니다.
세 번째 기능 **오류 찾기**는 Gesture Manager 플레이나 SDK 업로드가 오류로 막혔을 때, 무엇이 문제인지와 고치는 순서를 보여 줍니다.

### 동작 환경
- Unity 2022.3.22f1 (VRChat 권장 버전)
- lilToon 2.x (2.3.4 기준으로 제작) — 마테리얼 일괄 적용에 필요합니다
- Modular Avatar — 본 스케일에서 의상을 맞출 때 필요합니다. 바닥 맞추기는 1.17.0 이상이 필요합니다
- VRChat SDK가 없어도 동작합니다. 오류 찾기는 VRChat SDK·NDMF가 있으면 그 결과도 함께 읽습니다(SDK 3.10.5, NDMF 1.15.0 기준으로 확인)

### 설치
VCC로 설치하면 새 버전이 나왔을 때 VCC에서 바로 올릴 수 있습니다.
1. VCC에 LITTOOL 저장소를 추가합니다. https://a2566564-del.github.io/LITTOOL/ 에서 **Add to VCC**를 누르거나,
   VCC의 **Settings → Packages → Add Repository**에 `https://a2566564-del.github.io/LITTOOL/index.json`을 넣고 **Add**, **I Understand, Add Repository**를 누릅니다.
2. 프로젝트의 **Manage Project**에서 LITTOOL 오른쪽의 **+**를 누릅니다. 예전에 unitypackage로 넣은 `Assets/LITTOOL` 폴더는 이때 자동으로 지워지고, 설정은 그대로 남습니다.
3. 새 버전이 나오면 **Manage Project**에서 LITTOOL의 **Latest Version** 버튼을 누릅니다. 저절로 바뀌지는 않습니다.

unitypackage로 설치하려면 `LITTOOL_v1.3.4.unitypackage`(https://github.com/a2566564-del/LITTOOL/releases)를 Unity 창에 드래그하고 **Import**를 누릅니다.
이전 버전 위에 그대로 덮어 설치하면 됩니다. 이 방법은 새 버전 알림이 없습니다.

두 방법을 섞지 마세요. VCC로 넣은 프로젝트에 unitypackage를 다시 임포트하면 같은 코드가 두 벌이 되어 컴파일 오류가 납니다.

설치한 뒤 메뉴 **Tools > LITTOOL**로 창을 엽니다. 배너 아래 줄에서 기능 탭을 고릅니다.
화면 언어는 창 왼쪽 아래의 🌐 **Language**에서 바꿉니다. 처음에는 컴퓨터 언어(한국어 · 日本語 · 그 밖에는 English)로 열립니다.

### 사용법 (마테리얼 일괄 적용)
1. **기준**: 오른쪽 버튼으로 **마테리얼** 또는 **프리셋**을 고릅니다.
   - 마테리얼: 값을 가져올 lilToon 마테리얼을 넣습니다. (예: 아바타 Body 마테리얼)
   - 프리셋: lilToon 프리셋을 고릅니다. lilToon 기본 프리셋(기본)과 프로젝트에 있는 프리셋(사용자)이 카테고리별로 나오고, 검색할 수 있습니다. 프로젝트 창에서 프리셋 파일을 끌어다 놓아도 됩니다.
2. **적용 항목**: 복사할 항목을 고릅니다. 처음에는 그림자·림쉐이드·Backlight·림 라이트가 들어 있습니다.
   - **+** : 섹션 전체 또는 프로퍼티 하나를 추가합니다. 검색창에서 이름으로 찾을 수 있습니다.
   - **−** : 선택한 항목을 뺍니다. 드래그로 순서를 바꿀 수 있습니다.
   - **기본값** : 처음 목록으로 되돌립니다.
   - 프리셋을 기준으로 쓰면 각 항목 오른쪽에 프리셋에 들어 있는 값의 개수(예: 26/27)가 나옵니다.
3. **적용 대상**: 의상·헤어·액세서리의 루트 오브젝트를 넣습니다. 하위 파츠의 마테리얼을 모두 찾습니다(꺼진 파츠 포함).
   - 파츠와 마테리얼 앞의 체크박스로 적용할 것만 고릅니다. **전체 선택 / 전체 해제** 버튼도 있습니다.
4. **문제 목록**을 확인하고 **적용** 버튼을 누릅니다.

### 텍스처 뺀 복사본 만들기
기준이 마테리얼일 때 `Shader: …` 줄 오른쪽의 **텍스처 뺀 복사본 만들기**를 누르면,
기준 마테리얼을 같은 폴더에 `기준 마테리얼 이름_Ref.mat`으로 복사하고 이미지 슬롯(텍스처, 노멀맵, 마스크 등)을 모두 비웁니다.
수치 값은 그대로이고 원본은 바뀌지 않으며, 만든 복사본이 기준 마테리얼로 지정됩니다.
같은 이름이 있으면 `Body_Ref 1.mat`처럼 번호를 붙입니다. 파일을 새로 만드는 작업이라 Ctrl+Z로는 되돌릴 수 없습니다.

### 알아 둘 점
- **원본 마테리얼을 직접 수정합니다.** 같은 마테리얼을 쓰는 다른 아바타·의상에도 함께 반영됩니다.
- 체크를 뺀 파츠라도, 체크한 다른 파츠와 **같은 마테리얼**을 쓰면 함께 바뀝니다(마테리얼 파일이 하나이기 때문). 이런 줄에는 "공유라 함께 바뀜" 경고가 붙습니다.
- 프리셋에 없는 값은 건너뛰고 대상 마테리얼의 원래 값을 유지합니다. 프리셋의 텍스처, 윤곽선 켜기/끄기, 렌더링 설정은 적용하지 않습니다.
- 적용 후 **Ctrl+Z**로 되돌릴 수 있습니다(Unity를 끄기 전까지). 되돌린 값도 파일에 바로 저장됩니다.
- 적용 항목 목록은 프로젝트의 `ProjectSettings/LITTOOLSettings.asset`에 저장됩니다. 툴을 업데이트해도 유지됩니다.
- 화면 언어(한국어·English·日本語)는 창 왼쪽 아래에서 바꿀 수 있고, 사용자마다 따로 저장됩니다.
- 플레이 모드에서는 적용할 수 없습니다.
- 렌더러에 붙어 있는 마테리얼만 찾습니다. 애니메이션으로 바꿔 끼우는 마테리얼은 따로 적용하세요.
- 렌더링 모드, 아웃라인 켜기/끄기처럼 셰이더 자체를 바꿔야 하는 설정은 복사하지 않습니다.

### 문제 목록
| 구분 | 내용 | 처리 |
|---|---|---|
| 오류 | 셰이더 없음(분홍색), FBX 등에 포함된 읽기 전용 마테리얼, 읽기 전용 패키지·파일 | 건너뜀 |
| 경고 | lilToon이 아닌 마테리얼(Poiyomi 등) | 건너뜀 |
| 경고 | 일부 미지원(lilToon Lite 등), Material Variant에서 잠긴 항목 | 있는 값만 적용 |
| 경고 | 버전 관리(VCS) 체크아웃 필요 | 적용할 때 체크아웃 시도 |
| 경고 | 체크를 뺐지만 체크한 다른 파츠와 같은 마테리얼 | 함께 바뀜 |

항목을 클릭하면 하이어라키에서 해당 오브젝트를 선택합니다.

### 본 스케일
1. **아바타**: 씬에 있는 아바타를 넣습니다(씬에 아바타가 하나뿐이면 자동으로 들어갑니다). 휴머노이드 본과 Breast 본(이름으로)을 찾습니다.
2. **의상**: 아바타 안의 MA Merge Armature 의상이 나옵니다.
   - 체크박스는 씬에서 의상을 실제로 켜고 끕니다(Ctrl+Z 가능). 꺼진 의상도 체형은 계속 맞춥니다.
   - MA 잠금이 켜진 의상은 MA가 따라가고, 잠금이 꺼진 의상에는 같은 비율로 값을 넣습니다.
   - 펼치면 파츠(렌더러)와 그 파츠가 쓰는 본이 보입니다.
   - **새 의상 자동 맞추기**(기본 켜짐): 창이 열려 있는 동안 새로 입힌 의상을 지금 체형에 바로 맞춥니다. 원래 있던 의상이 다르면 "체형 다름"과 **맞추기** 버튼이 나옵니다.
3. **본**: 실루엣의 점이나 목록을 눌러 본을 고르고, 슬라이더(0.5–2)나 숫자로 Scale을 바꿉니다. 숫자는 범위 밖도 됩니다.
   - **좌우 대칭**이 켜져 있으면 왼쪽과 오른쪽이 함께 바뀝니다. **X·Y·Z 따로**로 축별로 정할 수 있습니다.
   - **+** 로 Shoulder, Hand, Foot이나 Armature의 다른 본을 더하고, **−** 로 목록에서 뺍니다(값은 그대로).
   - 본을 고르면 씬 뷰에 그 본이 1.5초 동안 표시됩니다. 값을 바꾸는 동안에는 아무것도 그리지 않습니다.
4. **체형**: **현재 값 저장**으로 이름을 붙여 저장하고, **적용**으로 불러옵니다. **이름 바꾸기**, **삭제**도 있습니다. 이 프로젝트의 다른 아바타에서도 쓸 수 있습니다.
5. **바닥 맞추기**: 켜진 의상의 신발 밑창이 바닥보다 0.5 cm 이상 아래(또는 위)에 있으면 **Floor Adjuster 넣기**가 켜집니다. 누르면 아바타 안에 `LITTOOL Floor`를 만들어 밑창 높이에 MA Floor Adjuster를 놓습니다. 이미 있으면 **위치 다시 맞추기**로 옮깁니다.

- 모든 변경은 바로 반영되고 Ctrl+Z로 되돌릴 수 있습니다. 자식 본은 Unity 기본대로 함께 커집니다.
- 본 목록, 체형, 자동 맞추기 설정은 `ProjectSettings/LITTOOLSettings.asset`에 저장됩니다.
- 하이힐을 벗는 토글처럼 높이가 바뀌는 경우는 따로 처리하지 않습니다. 지금 켜진 신발을 기준으로 합니다.

### 오류 찾기
LITTOOL은 아바타나 설정을 직접 바꾸지 않습니다. 버튼은 위치를 보여 주거나 창을 여는 것뿐이고, 고치는 건 안내를 보고 직접 합니다.
1. 평소처럼 플레이하거나 업로드합니다. **자동으로 모으기**(기본 켜짐)가 켜져 있으면 LITTOOL 창을 닫아 두어도 오류를 모읍니다.
   모으는 단위는 플레이(들어가서 나올 때까지), 업로드(Build & Publish·Build & Test를 누른 때부터 결과까지), 스크립트 컴파일 한 번입니다.
2. 오류가 나면 LITTOOL 창이 열려 있을 때는 **오류 찾기** 탭으로 바뀌고, 닫혀 있으면 씬 뷰에 약 4초 동안 알림이 뜹니다. 그 뒤 **Tools > LITTOOL**을 열면 오류 찾기 탭이 열립니다.
3. 카드는 원인 하나에 하나씩, 심각한 것과 확실한 것부터 놓입니다. 원인 때문에 따라 나온 메시지(예: "Failed to build the Avatar…")는 아래 **결과 메시지** 상자에 접혀 있습니다.
   - **확정**: 오류 목록에 있는 문장입니다.
   - **비슷한 오류 · NN%**: 목록에 똑같은 문장은 없지만 65% 이상 비슷한 오류로 추정한 안내입니다.
   - **목록에 없는 오류**: 오류가 난 위치(스택트레이스, 파일 경로)로 툴을 추정한 일반 안내입니다. 툴이 직접 남긴 메시지가 있으면 **메시지 내용**에 보여 주고, 메시지에 할 일이 적혀 있으면(예: “Run … Setup again”) 그 문장을 해결 순서의 첫 단계로 둡니다. 스택트레이스에 나온 그 툴의 컴포넌트가 씬에 있으면 **선택** 버튼으로 보여 줍니다(오류가 난 바로 그 컴포넌트가 아닐 수도 있습니다).
   - 해결 순서 안의 **선택**은 문제 오브젝트를 하이어라키에서 선택하고, **폴더 보기**는 문제 파일을 프로젝트 창에서 보여 줍니다. **SDK 창 열기**, **NDMF Console 열기**도 있습니다.
4. **다시 검사**는 지금 확인할 수 있는 것(컴파일 오류, SDK 창의 검사 목록, 작업 중에 난 오류)을 다시 읽습니다. 플레이·업로드 중에만 나는 오류는 다시 플레이하거나 업로드해서 확인하세요.

- 안내 문장은 한국어만 있습니다. 탭의 버튼과 제목은 한국어·English·日本語입니다.
- 오류 목록(254개, v7.2, 2026년 10월 기준)은 패키지 안에 들어 있고 인터넷에서 받지 않습니다. 목록은 LITTOOL을 업데이트할 때 바뀝니다.
- **메시지 내용**의 **파파고로 번역**·**구글 번역**은 누를 때만 그 메시지를 번역 사이트로 보내 브라우저에서 엽니다. 이름·경로·VRChat ID는 오류 기록과 같은 방식으로 가리고 보냅니다. 인터넷에 연결되어 있지 않으면 버튼이 꺼집니다.
- 목록과 똑같지 않은 오류는 이 PC의 `문서\LITTOOL\오류 기록\YYYY-MM.txt`에 남깁니다(목록과 맞은 오류는 횟수만 셉니다). 사용자 이름·PC 이름·아바타 이름·VRChat ID·이메일·프로젝트 폴더는 가려서 적고, 어디로도 보내지 않습니다. 탭의 **기록 폴더 열기**로 열 수 있습니다.
- **자동으로 모으기**를 끄면 아무것도 듣지 않고, **다시 검사**를 누를 때 콘솔·NDMF·SDK 검사 목록·컴파일 상태를 한꺼번에 읽습니다. 이때는 Unity 콘솔에 보이는 항목만 읽으므로, 콘솔에서 오류 표시를 꺼 두었거나 검색어를 넣어 두면 그 오류를 읽지 못합니다.
- VRChat SDK나 NDMF가 없거나 버전이 달라 읽지 못해도, 콘솔에 남은 문장으로 계속 동작합니다(탭의 ‘지켜보는 곳’에 상태가 나옵니다).
- Unity가 Safe Mode로 열리면 LITTOOL도 실행되지 않습니다. 그때는 콘솔 맨 위의 빨간 줄(error CS…)부터 확인하세요.
- 자동으로 모으기 설정은 `ProjectSettings/LITTOOLSettings.asset`에 저장됩니다. 찾은 결과는 Unity를 끄면 사라집니다.

### 삭제
`Assets/LITTOOL` 폴더를 삭제합니다(VCC로 넣었다면 Manage Project에서 LITTOOL을 제거합니다). 설정도 지우려면 `ProjectSettings/LITTOOLSettings.asset`도 삭제합니다. 오류 기록은 `문서\LITTOOL` 폴더에 따로 있습니다.

---

## English

LITTOOL is a set of Unity editor tools for making VRChat avatars.
Its first tool, **Material Batch**, copies lilToon settings (Shadow, Rim Light, ...) from a reference material
or a lilToon preset to the lilToon materials of an outfit, hair or accessory at once.
Textures, normal maps and masks of each material are kept as they are.
The second tool, **Bone Scale**, changes the avatar's bone scales (head, neck, breast, arms, legs...) in one place
and keeps the outfits dressed with Modular Avatar and the floor height matching.
The third tool, **Error Finder**, shows what went wrong and the order to fix it when Gesture Manager play or an SDK upload fails.

### Requirements
- Unity 2022.3.22f1 (the version VRChat recommends)
- lilToon 2.x (built against 2.3.4) for Material Batch, installed separately
- Modular Avatar for matching outfits in Bone Scale; the floor feature needs 1.17.0 or later
- The VRChat SDK is not required. When the VRChat SDK and NDMF are installed, Error Finder also reads their results (checked against SDK 3.10.5 and NDMF 1.15.0)

### Install
With the VCC you can update LITTOOL from the VCC when a new version comes out.
1. Add the LITTOOL repository to the VCC: press **Add to VCC** on https://a2566564-del.github.io/LITTOOL/,
   or go to **Settings → Packages → Add Repository** in the VCC, enter `https://a2566564-del.github.io/LITTOOL/index.json`, then press **Add** and **I Understand, Add Repository**.
2. In your project's **Manage Project**, press **+** to the right of LITTOOL. An old `Assets/LITTOOL` folder from the unitypackage is removed at that point; your settings stay.
3. For a new version, press LITTOOL's **Latest Version** button in **Manage Project**. It does not update by itself.

To use the unitypackage instead, drag `LITTOOL_v1.3.4.unitypackage` (https://github.com/a2566564-del/LITTOOL/releases) into Unity and click **Import**.
Import it over an older version. This way gives no notice of new versions.

Do not mix the two: importing the unitypackage into a project that has LITTOOL from the VCC makes two copies of the same code, which fails to compile.

After installing, open the window from **Tools > LITTOOL** and pick a tool in the row under the banner.
Change the UI language with 🌐 **Language** at the bottom left. At first the window follows your computer's language (Korean, Japanese, otherwise English).

### Usage (Material Batch)
1. **Reference**: choose **Material** or **Preset** with the buttons on the right.
   - Material: the lilToon material to copy from (e.g. the avatar's Body material).
   - Preset: a lilToon preset. lilToon's built-in presets and the presets in your project (Custom) are listed by category and can be searched. You can also drag a preset file from the Project window.
2. **Properties to Apply**: what to copy. Shadow, RimShade, Backlight and Rim Light are listed by default.
   - **+** adds a whole section or a single property (searchable).
   - **−** removes the selected entry. Drag to reorder.
   - **Reset** restores the default list.
   - With a preset, each entry shows how many of its values the preset stores (e.g. 26/27).
3. **Target**: the root object of an outfit, hair or accessory. Materials of every child part are found, including disabled ones.
   - Use the checkboxes in front of parts and materials to choose what to apply. **Select all / Select none** are available too.
4. Check the **issue list** and press **Apply**.

### Copy without textures
With a material as the reference, **Copy without textures** (right of the `Shader: …` line) copies the reference material
into the same folder as `<reference name>_Ref.mat` and empties every texture slot (textures, normal maps, masks...).
Values are kept, the original is not changed, and the copy becomes the reference material.
If the name is taken, a number is added (`Body_Ref 1.mat`). Creating a file cannot be undone with Ctrl+Z.

### Notes
- **The original material files are modified.** Other avatars or outfits using the same materials change too.
- An unchecked part still changes if it uses **the same material** as a checked part (it is one file). Such rows are marked "Changes (shared)".
- Values a preset does not store are skipped; the targets keep their own. A preset's textures, outline on/off and rendering settings are never applied.
- **Ctrl+Z** undoes the change while Unity is open; the undone values are saved to disk as well.
- The list is stored in `ProjectSettings/LITTOOLSettings.asset` and survives LITTOOL updates.
- The UI language (한국어 / English / 日本語) is selected at the bottom left and saved per user.
- Applying is disabled in Play Mode.
- Only materials assigned to renderers are found; apply materials swapped in by animations separately.
- Settings that require a different shader (rendering mode, outline on/off, ...) are not copied.

### Issue list
| Level | Meaning | Result |
|---|---|---|
| Error | Missing shader (pink), read-only material inside a model (FBX, ...), read-only package or file | Skipped |
| Warning | Not a lilToon material (Poiyomi, ...) | Skipped |
| Warning | Partially supported (lilToon Lite, ...), properties locked by a Material Variant parent | Available values applied |
| Warning | Version control checkout needed | Checkout attempted when applying |
| Warning | Unchecked, but the same material as a checked part | Changes too |

Click an item to select its object in the Hierarchy.

### Bone Scale
1. **Avatar**: assign the avatar in the scene (filled in automatically when the scene has only one). Humanoid bones and Breast bones (by name) are found.
2. **Outfits**: the outfits with MA Merge Armature under the avatar.
   - The checkbox switches the outfit on or off in the scene (Ctrl+Z works). Outfits that are off still follow the body shape.
   - Outfits with an MA lock are followed by MA; outfits without a lock get the same ratio written.
   - Expand an outfit to see its parts (renderers) and the bones they use.
   - **Auto-match new outfits** (on by default): outfits dressed while the window is open are matched to the current body shape at once. Existing outfits that differ show "Differs" and a **Match** button.
3. **Bones**: pick a bone with a dot on the silhouette or a row of the list, then change its scale with the slider (0.5–2) or the number field (any value).
   - With **Symmetry**, left and right change together. **Separate X·Y·Z** sets each axis.
   - **+** adds Shoulder, Hand, Foot or any other Armature bone; **−** removes a bone from the list (its value stays).
   - Picking a bone marks it in the Scene view for 1.5 seconds. Nothing is drawn while you change values.
4. **Body Shapes**: **Save current** stores the values under a name; **Apply** loads them. **Rename** and **Delete** are there too. Every avatar in the project can use them.
5. **Floor**: when the soles of the outfits that are on sink 0.5 cm or more below the floor (or float above it), **Add Floor Adjuster** creates `LITTOOL Floor` inside the avatar with an MA Floor Adjuster at sole height. If one exists, **Move to the soles** moves it.

- Every change is applied at once and can be undone with Ctrl+Z. Child bones scale with their parent, as in Unity.
- The bone list, body shapes and the auto-match setting are stored in `ProjectSettings/LITTOOLSettings.asset`.
- Height changes from toggles such as taking heels off are not handled; the shoes that are on now are used.

### Error Finder
LITTOOL never changes your avatar or settings. Its buttons only show where something is or open a window; you make the fix yourself by following the guide.
1. Play or upload as usual. With **Collect automatically** (on by default), errors are collected even while the LITTOOL window is closed.
   A unit is one play (enter to exit Play Mode), one upload (from Build & Publish / Build & Test to the result) or one script compilation.
2. When something fails, an open LITTOOL window switches to the **Error Finder** tab; a closed one shows a notification in the Scene view for about 4 seconds, and **Tools > LITTOOL** then opens on the Error Finder tab.
3. There is one card per cause, the most severe and most certain first. Messages that only follow from a cause (e.g. "Failed to build the Avatar…") are folded in the **result messages** box below.
   - **Confirmed**: the message is in the error list.
   - **Similar error · NN%**: not in the list word for word, but 65% or more similar to an entry (an estimate).
   - **Not in the error list**: general guidance for the tool found from where the error happened (stack trace, file path). When the tool wrote its own message, it is shown under **What the message says**, and a sentence in it that says what to do (e.g. "Run … Setup again") becomes the first step. When components of that tool named in the stack trace are in the scene, they get a **Select** button (they may not be the exact component that failed).
   - In the steps, **Select** selects the object in the Hierarchy and **Show folder** shows the file in the Project window. **Open SDK window** and **Open NDMF Console** are there too.
4. **Check again** reads what can be checked now (compile errors, the SDK window's Validations, errors raised while editing). Errors that only happen in Play Mode or during an upload are confirmed by playing or uploading again.

- The guidance itself is in Korean only. The tab's buttons and titles are in Korean, English and Japanese.
- The error list (254 entries, v7.2, October 2026) is inside the package and is never downloaded; it changes when LITTOOL is updated.
- **Translate (Papago)** / **Google Translate** under **What the message says** send that message to the translation site and open it in your browser, only when pressed. Names, paths and VRChat IDs are masked the same way as in the error log. The buttons are disabled without an internet connection.
- Errors that do not exactly match the list are written on this PC to `Documents\LITTOOL\오류 기록\YYYY-MM.txt` (matched errors only raise a count). User and PC names, avatar names, VRChat IDs, e-mail addresses and the project folder are masked, and nothing is sent anywhere. **Open log folder** in the tab opens it.
- With **Collect automatically** off nothing is heard; **Check again** reads the console, NDMF, the SDK Validations and the compile state at once. It reads only what the Unity Console shows, so errors hidden by the Console's filters or search text are missed.
- Without the VRChat SDK or NDMF, or with versions it cannot read, Error Finder keeps working from the console text (see "Watching" in the tab).
- When Unity opens in Safe Mode, LITTOOL does not run either. Then start from the first red line in the Console (error CS…).
- The Collect automatically setting is stored in `ProjectSettings/LITTOOLSettings.asset`. Results are gone when Unity closes.

### Uninstall
Delete the `Assets/LITTOOL` folder (with the VCC, remove LITTOOL in Manage Project instead). To remove the settings too, delete `ProjectSettings/LITTOOLSettings.asset`. The error logs are in the separate `Documents\LITTOOL` folder.

---

## 日本語

LITTOOLはVRChatアバター制作のためのUnityエディターツール集です。
最初の機能 **マテリアル一括適用** は、基準マテリアルまたはlilToonプリセットの設定値(影、リムライトなど)を
衣装・髪・アクセサリー内のlilToonマテリアルに一括でコピーします。
テクスチャ・ノーマルマップ・マスクは各マテリアルのものをそのまま維持します。
2つ目の機能 **ボーンスケール** は、アバターのボーンのScale(頭、首、胸、腕、脚など)を1つの画面で変更し、
Modular Avatarで着せた衣装と床の高さも合わせます。
3つ目の機能 **エラー診断** は、Gesture ManagerでのプレイやSDKアップロードがエラーで止まったとき、何が問題かと直す手順を表示します。

### 動作環境
- Unity 2022.3.22f1 (VRChat推奨バージョン)
- lilToon 2.x (2.3.4基準で制作) — マテリアル一括適用に必要です
- Modular Avatar — ボーンスケールで衣装を合わせるときに必要です。床合わせは1.17.0以上が必要です
- VRChat SDKがなくても動作します。エラー診断はVRChat SDK・NDMFがあればその結果も読みます(SDK 3.10.5、NDMF 1.15.0で確認)

### インストール
VCCでインストールすると、新しいバージョンが出たときにVCCからすぐ更新できます。
1. VCCにLITTOOLのリポジトリを追加します。https://a2566564-del.github.io/LITTOOL/ で **Add to VCC** を押すか、
   VCCの **Settings → Packages → Add Repository** に `https://a2566564-del.github.io/LITTOOL/index.json` を入力し、**Add**、**I Understand, Add Repository** を押します。
2. プロジェクトの **Manage Project** で、LITTOOLの右の **+** を押します。以前unitypackageで入れた `Assets/LITTOOL` フォルダーはこのとき自動で削除され、設定はそのまま残ります。
3. 新しいバージョンが出たら、**Manage Project** でLITTOOLの **Latest Version** ボタンを押します。自動では更新されません。

unitypackageでインストールする場合は、`LITTOOL_v1.3.4.unitypackage`(https://github.com/a2566564-del/LITTOOL/releases)をUnityにドラッグし、**Import** を押します。
旧バージョンの上にそのまま上書きできます。この方法では新しいバージョンのお知らせはありません。

2つの方法を混ぜないでください。VCCで入れたプロジェクトにunitypackageをもう一度インポートすると同じコードが2つになり、コンパイルエラーになります。

インストール後、メニュー **Tools > LITTOOL** からウィンドウを開き、バナーの下の列で機能を選びます。
表示言語はウィンドウ左下の 🌐 **Language** で切り替えます。最初はパソコンの言語(韓国語・日本語、それ以外は英語)で開きます。

### 使い方 (マテリアル一括適用)
1. **基準**: 右のボタンで **マテリアル** または **プリセット** を選びます。
   - マテリアル: 値をコピーする元のlilToonマテリアルを指定します(例: アバターのBodyマテリアル)。
   - プリセット: lilToonプリセットを選びます。lilToon標準プリセットとプロジェクト内のプリセット(ユーザー)がカテゴリー別に表示され、検索もできます。プロジェクトウィンドウからプリセットファイルをドラッグしても指定できます。
2. **適用項目**: コピーする項目を選びます。初期状態では影・リムシェード・逆光ライト・リムライトが入っています。
   - **+** : セクション全体またはプロパティ1つを追加します。検索もできます。
   - **−** : 選択した項目を外します。ドラッグで並べ替えできます。
   - **初期化** : 初期リストに戻します。
   - プリセットを基準にすると、各項目の右にプリセットに含まれる値の数(例: 26/27)が表示されます。
3. **適用対象**: 衣装・髪・アクセサリーのルートオブジェクトを指定します。非表示のパーツも含め、子パーツのマテリアルをすべて検出します。
   - パーツとマテリアルの前のチェックボックスで適用するものだけを選びます。**すべて選択 / すべて解除** もあります。
4. **問題リスト** を確認して **適用** を押します。

### テクスチャなしで複製
基準がマテリアルのとき、`Shader: …` の行の右にある **テクスチャなしで複製** を押すと、
基準マテリアルを同じフォルダーに `基準マテリアル名_Ref.mat` として複製し、画像スロット(テクスチャ、ノーマルマップ、マスクなど)をすべて空にします。
数値はそのまま残り、元のマテリアルは変更されず、作成した複製が基準マテリアルに設定されます。
同じ名前があれば `Body_Ref 1.mat` のように番号を付けます。ファイルを新しく作る操作のため、Ctrl+Zでは元に戻せません。

### 注意
- **元のマテリアルファイルを直接変更します。** 同じマテリアルを使う他のアバター・衣装にも反映されます。
- チェックを外したパーツでも、チェックした他のパーツと **同じマテリアル** を使っていれば一緒に変更されます(マテリアルファイルが1つのため)。その行には「共有のため変更」の警告が付きます。
- プリセットにない値はスキップし、対象マテリアルの元の値を維持します。プリセットのテクスチャ、輪郭線のオン/オフ、描画設定は適用しません。
- 適用後は **Ctrl+Z** で元に戻せます(Unityを閉じるまで)。元に戻した値もファイルに保存されます。
- 適用項目リストは `ProjectSettings/LITTOOLSettings.asset` に保存され、ツールを更新しても保持されます。
- 表示言語(한국어 / English / 日本語)はウィンドウ左下で切り替えられ、ユーザーごとに保存されます。
- プレイモード中は適用できません。
- レンダラーに割り当てられたマテリアルのみ検出します。アニメーションで差し替えるマテリアルは別途適用してください。
- 描画モードや輪郭線のオン/オフなど、シェーダー自体の変更が必要な設定はコピーしません。

### 問題リスト
| 区分 | 内容 | 処理 |
|---|---|---|
| エラー | シェーダーなし(ピンク色)、FBXなどに含まれる読み取り専用マテリアル、読み取り専用パッケージ・ファイル | スキップ |
| 警告 | lilToon以外のマテリアル(Poiyomiなど) | スキップ |
| 警告 | 一部未対応(lilToon Liteなど)、Material Variantの親でロックされた項目 | 存在する値のみ適用 |
| 警告 | バージョン管理のチェックアウトが必要 | 適用時にチェックアウトを試行 |
| 警告 | チェックを外したが、チェックした他のパーツと同じマテリアル | 一緒に変更 |

項目をクリックするとヒエラルキーで該当オブジェクトを選択します。

### ボーンスケール
1. **アバター**: シーン内のアバターを指定します(シーンにアバターが1体だけなら自動で入ります)。ヒューマノイドのボーンとBreastボーン(名前で)を探します。
2. **衣装**: アバター内のMA Merge Armatureの衣装が表示されます。
   - チェックボックスでシーンの衣装を実際にオン/オフします(Ctrl+Z可)。オフの衣装も体型は合わせ続けます。
   - MAロックのある衣装はMAが追従し、ロックのない衣装には同じ比率で値を書き込みます。
   - 展開するとパーツ(レンダラー)と使っているボーンが表示されます。
   - **新しい衣装を自動で合わせる**(初期オン): ウィンドウが開いている間に着せた衣装を今の体型にすぐ合わせます。元からある衣装が違う場合は「体型が違う」と **合わせる** ボタンが出ます。
3. **ボーン**: シルエットの点か一覧でボーンを選び、スライダー(0.5–2)か数値でScaleを変更します。数値は範囲外も入力できます。
   - **左右対称** がオンなら左右が一緒に変わります。**X·Y·Zを個別に** で軸ごとに設定できます。
   - **+** でShoulder・Hand・FootやArmatureの他のボーンを追加し、**−** で一覧から外します(値はそのまま)。
   - ボーンを選ぶとシーンビューにそのボーンが1.5秒表示されます。値を変えている間は何も描きません。
4. **体型**: **今の値を保存** で名前を付けて保存し、**適用** で読み込みます。**名前を変更**・**削除** もあります。プロジェクト内の他のアバターでも使えます。
5. **床合わせ**: オンの衣装の靴底が床より0.5 cm以上下(または上)にあると **Floor Adjusterを追加** が押せます。押すとアバター内に `LITTOOL Floor` を作り、靴底の高さにMA Floor Adjusterを置きます。すでにある場合は **位置を合わせ直す** で移動します。

- すべての変更はすぐに反映され、Ctrl+Zで元に戻せます。子ボーンはUnityの標準どおり一緒に大きさが変わります。
- ボーン一覧・体型・自動合わせの設定は `ProjectSettings/LITTOOLSettings.asset` に保存されます。
- ヒールを脱ぐトグルのような高さの変化は扱いません。今オンの靴を基準にします。

### エラー診断
LITTOOLはアバターや設定を直接変えません。ボタンは場所を示すかウィンドウを開くだけで、修正は案内を見ながらご自身で行います。
1. いつもどおりプレイやアップロードをします。**自動で集める**(初期オン)がオンなら、LITTOOLウィンドウを閉じていてもエラーを集めます。
   集める単位は、プレイ1回(入ってから出るまで)、アップロード1回(Build & Publish・Build & Testを押してから結果まで)、スクリプトのコンパイル1回です。
2. エラーが出ると、LITTOOLウィンドウが開いていれば **エラー診断** タブに切り替わり、閉じていればシーンビューに約4秒間通知が出ます。そのあと **Tools > LITTOOL** を開くとエラー診断タブが開きます。
3. カードは原因1つにつき1枚で、重大で確かなものから並びます。原因から続けて出たメッセージ(例: "Failed to build the Avatar…")は下の **結果メッセージ** にまとめてあります。
   - **確定**: エラーリストにある文です。
   - **似たエラー · NN%**: リストに同じ文はないが、65%以上似たエラーから推定した案内です。
   - **リストにないエラー**: エラーが出た場所(スタックトレース、ファイルパス)からツールを推定した一般的な案内です。ツール自身が書いたメッセージがあれば **メッセージの内容** に表示し、すべきことが書かれていれば(例: “Run … Setup again”)その文を手順の最初に置きます。スタックトレースに出たそのツールのコンポーネントがシーンにあれば **選択** ボタンで示します(エラーが出たまさにそのコンポーネントとは限りません)。
   - 手順の中の **選択** は問題のオブジェクトをヒエラルキーで選択し、**フォルダーを表示** は問題のファイルをプロジェクトウィンドウで示します。**SDKウィンドウを開く**、**NDMF Consoleを開く** もあります。
4. **再チェック** は今確認できるもの(コンパイルエラー、SDKウィンドウのValidations、作業中に出たエラー)を読み直します。プレイ中・アップロード中だけに出るエラーは、もう一度プレイかアップロードして確認してください。

- 案内文は韓国語のみです。タブのボタンとタイトルは韓国語・English・日本語です。
- エラーリスト(254個、v7.2、2026年10月時点)はパッケージに含まれ、インターネットからは取得しません。リストはLITTOOLの更新で変わります。
- **メッセージの内容** の **Papagoで翻訳**・**Google翻訳** は、押したときだけそのメッセージを翻訳サイトに送り、ブラウザで開きます。名前・パス・VRChat IDはエラー記録と同じ方法で伏せて送ります。インターネットに接続されていないとボタンは押せません。
- リストと完全に一致しないエラーは、このPCの `ドキュメント\LITTOOL\오류 기록\YYYY-MM.txt` に残します(一致したエラーは回数だけ数えます)。ユーザー名・PC名・アバター名・VRChat ID・メールアドレス・プロジェクトフォルダーは伏せて書き、どこにも送りません。タブの **記録フォルダーを開く** で開けます。
- **自動で集める** をオフにすると何も聞かず、**再チェック** を押したときにコンソール・NDMF・SDKのValidations・コンパイル状態をまとめて読みます。このときはUnityのコンソールに表示されている項目だけを読むため、コンソールでエラー表示をオフにしていたり検索語を入れていたりすると、そのエラーは読めません。
- VRChat SDKやNDMFがない、またはバージョン違いで読めなくても、コンソールに残った文で動作を続けます(タブの「見ている場所」に状態が出ます)。
- UnityがSafe Modeで開くとLITTOOLも動きません。そのときはコンソールの一番上の赤い行(error CS…)から確認してください。
- 自動で集めるの設定は `ProjectSettings/LITTOOLSettings.asset` に保存されます。見つけた結果はUnityを閉じると消えます。

### アンインストール
`Assets/LITTOOL` フォルダーを削除します(VCCで入れた場合はManage ProjectでLITTOOLを削除します)。設定も消す場合は `ProjectSettings/LITTOOLSettings.asset` も削除します。エラー記録は別の `ドキュメント\LITTOOL` フォルダーにあります。

---

## Credits / License
- LITTOOL: 제작자가 알려 준 사람의 개인 사용만 허락합니다. 다시 배포하거나 다른 곳에 올리지 마세요 / Personal use only, for the people the author shares it with; do not redistribute or re-upload it (see `LICENSE.txt`). 변경 기록은 `CHANGELOG.md`.
- Bone Scale works with Modular Avatar by bd_ (MIT License) through reflection only; no Modular Avatar code is included.
- lilToon section and preset category names are taken from lilToon's translation files — lilToon by lilxyzw, MIT License.
- Error Finder's error list quotes the error messages of the VRChat SDK and of avatar tools (Modular Avatar, NDMF, Avatar Optimizer, VRCFury, VRCQuestTools, lilycalInventory, TexTransTool, lilToon, FaceEmo and others) so that it can recognize them; each entry names where its text and fix come from. The quoted messages belong to their owners; see "Third-party notices" in `LICENSE.txt`. LITTOOL reads the VRChat SDK and NDMF through reflection only; none of their code is included.
- The title image uses the fonts Yellowtail (Apache License 2.0) and Source Sans 3 (SIL Open Font License 1.1).
