# Asset Favorite Window

[English](README.md) | [한국어](README.ko.md)

프로젝트 에셋을 카테고리와 계층 노드로 정리하고, 전용 Editor 창에서 빠르게 선택하거나 프리팹 썸네일을 관리하는 UPM 패키지입니다.

## 설치

Unity Package Manager의 **Install package from git URL**에 다음 주소를 입력합니다.

```text
https://github.com/olivecrow/Asset-Favorite-Window.git#v0.1.0
```

저장소를 직접 내려받은 경우 `package.json`을 지정해 로컬 패키지로 설치할 수도 있습니다.

## 사용 방법

1. `Window > Favorite Assets`를 열거나 `Shift+W` 단축키를 사용합니다.
2. 카테고리와 계층 노드를 만들고 Project 창의 에셋을 원하는 노드에 등록합니다.
3. 그리드에서 에셋을 선택하거나 프리팹 썸네일을 생성·갱신합니다.

## 데이터와 캐시

- 패키지 저장소 자체에는 소비 프로젝트의 즐겨찾기 데이터나 생성 썸네일을 포함하지 않습니다.
- 공유할 즐겨찾기 구조와 에셋 GUID는 소비 프로젝트의 `ProjectSettings/FavoriteAssetsData.asset`에 저장됩니다.
- 사용자별 UI와 썸네일 설정은 `EditorPrefs`에 저장됩니다.
- 생성한 썸네일은 소비 프로젝트의 `Library/AssetFavoriteWindow/Thumbnails`에 PNG 캐시로 저장되며 Git에 포함하지 않습니다.
- 이전 `Assets/Editor/FavoriteAssetsThumbnails`의 `.asset` 썸네일 참조는 창에서 처음 사용될 때 PNG 캐시로 복사되고 프로젝트 설정의 기존 참조가 제거됩니다.

## 의존성과 호환성

- `FavoriteAssetsWindow` 어셈블리는 Editor 전용이며 명시적인 외부 assembly reference가 없습니다.
- Built-in, URP, HDRP에서 공통으로 사용할 수 있도록 1x MSAA preview target을 사용합니다.
- 현재 지원 기준 Unity 버전은 `6000.3`입니다. 다른 Unity 버전의 호환성은 해당 버전에서 별도로 확인해야 합니다.

## 개발과 검증

- Editor 테스트는 `Tests/Editor`에 있습니다.
- 패키지 변경 후에는 Unity 컴파일, EditMode 테스트, 창 재실행, Undo/Redo, 에셋 이동·삭제, 프리팹 재import와 썸네일 갱신을 확인합니다.
- 상세 구조와 제약은 [패키지 문서](Documentation~/index.ko.md)를 참고합니다.
- 릴리스 이력은 [변경 기록](CHANGELOG.ko.md)을 참고합니다.

## 라이선스

MIT License를 사용합니다. 자세한 내용은 [LICENSE.md](LICENSE.md)를 참고합니다.
