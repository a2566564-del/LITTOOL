# LITTOOL

VRChat 아바타 제작을 돕는 Unity 에디터 툴입니다.
사용법은 [패키지 README](Packages/com.litt.littool/README.md), 변경 기록은 [CHANGELOG](Packages/com.litt.littool/CHANGELOG.md)에 있습니다.

- **VCC로 설치**: https://a2566564-del.github.io/LITTOOL/ 에서 **Add to VCC** → 프로젝트의 **Manage Project**에서 LITTOOL **+**.
  저장소 주소: `https://a2566564-del.github.io/LITTOOL/index.json`. 새 버전은 Manage Project의 **Latest Version** 버튼으로 올립니다.
- **unitypackage**: [Releases](https://github.com/a2566564-del/LITTOOL/releases)의 `LITTOOL_v<버전>.unitypackage`.

**라이선스**: 소스는 공개되어 있지만 오픈소스가 아닙니다. 제작자가 알려 준 사람의 개인 사용만 허락하며,
다시 배포·판매·다른 곳에 업로드,수정 배포는 할 수 없습니다 — [LICENSE.txt](Packages/com.litt.littool/LICENSE.txt).

## 구성
- `Packages/com.litt.littool/` — 패키지 본체(VCC 패키지). 코드, 사용 설명, 변경 기록, 라이선스.
- `tools/make_unitypackage.py` — 같은 GUID로 `Assets/LITTOOL`에 설치되는 `.unitypackage`를 만듭니다.
- `source.json`, `Website/` — VCC 저장소 목록과 그 페이지(GitHub Pages).
- `.github/workflows/` — 릴리스(**Build Release**)와 목록(**Build Repo Listing**).
- 나머지(`Assets`, `ProjectSettings`, `Packages/com.vrchat.core.bootstrap`)는 VRChat template-package에서 온 것입니다.
  이 폴더를 Unity 2022.3.22f1로 열면 개발용 프로젝트가 됩니다.

## 새 버전 내기
1. 코드를 고친 뒤 `Packages/com.litt.littool/package.json`의 `version`·`url`과 `Editor/Core/LittoolInfo.cs`의 `Version`을 같이 올리고 `CHANGELOG.md`에 적습니다.
2. 새 파일에는 `.meta`가 있어야 합니다(Unity로 프로젝트를 열면 만들어 줍니다).
   그림 세 장과 `ErrorFinder/ErrorCatalog.json`의 GUID는 `LittoolInfo.cs`에 적힌 값과 같아야 하므로 바꾸지 않습니다.
3. main에 푸시하면 **Build Release**가 그 버전의 릴리스(`com.litt.littool-<버전>.zip`, `LITTOOL_v<버전>.unitypackage`, `package.json`)를 만들고,
   이어서 **Build Repo Listing**이 모든 릴리스로 VCC 목록을 다시 만들어 GitHub Pages에 올립니다.
   이미 릴리스(태그)가 있는 버전이면 Build Release는 아무것도 하지 않습니다.
4. 손으로 돌리려면 Actions에서 각 워크플로의 **Run workflow**를 누릅니다. 릴리스를 손으로 지우거나 고친 뒤에는 Build Repo Listing을 돌립니다.

처음 한 번: **Settings → Pages → Build and deployment → Source**를 **GitHub Actions**로 둡니다.
