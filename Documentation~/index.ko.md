# Asset Favorite Window 패키지 구조

[English](index.md) | [한국어](index.ko.md) | [README](../README.ko.md)

## 목적

자주 사용하는 프로젝트 에셋을 카테고리와 계층 노드로 정리하고, 전용 창에서 선택·열기·드래그하거나 프리팹 썸네일을 관리하는 Editor 전용 패키지입니다.

## 어셈블리와 진입점

- `Editor/FavoriteAssetsWindow.Editor.asmdef`: 실제 어셈블리명은 직렬화 호환성을 위해 `FavoriteAssetsWindow`로 유지합니다.
- `Editor/FavoriteAssetsWindow.cs`: `Window > Favorite Assets` 창, Undo/Redo와 manager 조정 흐름을 소유합니다.
- `Editor/Managers/`: 카테고리 탭, 계층, 에셋 그리드와 상세 패널을 관리합니다.
- `Editor/Postprocessor.cs`: 삭제된 에셋 데이터를 정리하고 선택적으로 즐겨찾기 프리팹 썸네일 갱신을 예약합니다.

## 저장 경계

- `ProjectSettings/FavoriteAssetsData.asset`: 카테고리, 계층, 에셋 GUID와 설명을 프로젝트 단위로 공유합니다.
- `EditorPrefs`: 창 표시, 정렬, 확대와 썸네일 생성 설정을 사용자 단위로 저장합니다.
- `Library/AssetFavoriteWindow/Thumbnails`: 생성된 PNG 썸네일을 로컬 캐시로 보관합니다.

썸네일 캐시는 패키지나 프로젝트의 원본 에셋이 아니며 소스 제어에 포함하지 않습니다. 기존 `Assets/Editor/FavoriteAssetsThumbnails` 참조는 실제로 표시되는 시점에 PNG로 복사한 뒤 직렬화 참조만 제거합니다. 기존 에셋 파일 자체는 자동 삭제하지 않습니다.

패키지 저장소에는 소비 프로젝트의 `ProjectSettings/FavoriteAssetsData.asset`, `Library` 캐시 또는 기존 `.asset` 썸네일을 포함하지 않습니다.

## 썸네일 흐름

1. 프리팹의 활성 Renderer bounds를 계산합니다.
2. 임시 preview scene에 프리팹, 카메라와 조명을 생성합니다.
3. Built-in·URP·HDRP 공통의 resolved 1x RenderTexture에 렌더링합니다.
4. PNG를 `Library` 캐시에 저장하고 메모리 `Texture2D` 캐시를 갱신합니다.
5. preview scene, RenderTexture와 임시 오브젝트를 `finally`에서 정리합니다.

Renderer bounds가 없거나 유효하지 않은 프리팹은 생성하지 않고 Unity 기본 에셋 preview로 대체합니다. import 후 자동 갱신은 지연 호출에서 중복 GUID를 모아 처리하며 썸네일 설정 창에서 끌 수 있습니다.

## 직렬화 호환성

기존 `ProjectSettings/FavoriteAssetsData.asset`은 스크립트 GUID와 `FavoriteAssetsWindow` 어셈블리·namespace를 저장합니다. 기존 데이터 호환성을 유지하려면 다음을 변경하지 않습니다.

- `FavoriteAssetsWindow` 어셈블리명
- `FavoriteAssetsWindow` namespace
- 기존 C#과 asmdef `.meta` GUID

기존 `Assets` 복사본과 UPM 패키지 복사본을 동시에 두지 않습니다.

## 검증

- 패키지 설치와 Editor 컴파일
- `Tests/Editor` EditMode 테스트
- 카테고리·노드·에셋 추가, 이동, 삭제와 Undo/Redo
- domain reload와 Editor 재실행 뒤 데이터 복원
- Built-in·URP·HDRP의 프리팹 썸네일 생성
- 반복 생성과 prefab reimport 시 Console 오류 및 preview 리소스 누수 여부
