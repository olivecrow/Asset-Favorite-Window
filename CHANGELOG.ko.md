# 변경 기록

[English](CHANGELOG.md) | [한국어](CHANGELOG.ko.md)

## 0.1.0 - 2026-08-28

- 기존 Editor 도구를 `com.olivecrow.asset-favorite-window` embedded UPM 패키지로 분리했습니다.
- 패키지 어셈블리명, namespace와 기존 `.meta` GUID를 유지해 프로젝트 설정 직렬화 호환성을 보존했습니다.
- 썸네일을 `Library/AssetFavoriteWindow/Thumbnails`의 비추적 PNG 캐시에 저장하고 기존 `.asset` 썸네일을 지연 마이그레이션하도록 변경했습니다.
- 렌더 파이프라인별 컴파일 분기를 제거하고 공통 1x MSAA preview target과 GUI 이후 지연 렌더링을 사용하도록 변경했습니다.
- 활성 Renderer bounds가 없는 프리팹의 썸네일 생성을 안전하게 건너뛰도록 수정했습니다.
- prefab import 썸네일 갱신을 지연·일괄 처리하고 사용자 설정으로 끌 수 있게 했습니다.
- 삭제된 경로를 GUID로 역조회하지 않고 즐겨찾기 GUID 인덱스에서 사라진 에셋과 캐시를 정리하도록 수정했습니다.
- 썸네일 렌더 실패 시에도 preview 리소스와 생성 중인 `Texture2D`를 정리하도록 보강했습니다.
- 썸네일 bounds와 PNG 캐시 round-trip을 검증하는 EditMode 테스트를 추가했습니다.
