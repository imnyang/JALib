# JAMod 개발 가이드 - 모드 실행 환경 구축

## [목차로 이동](000-DevelopGuide.md)
1. [모드 설치 경로](#1-%EB%AA%A8%EB%93%9C-%EC%84%A4%EC%B9%98-%EA%B2%BD%EB%A1%9C)
2. [필수 파일 구성](#2-%ED%95%84%EC%88%98-%ED%8C%8C%EC%9D%BC-%EA%B5%AC%EC%84%B1)
3. [Info.json 설정](#3-infojson-%EC%84%A4%EC%A0%95)
4. [JAModInfo.json 설정](#4-jamodinfojson-%EC%84%A4%EC%A0%95)

---

## 1. 모드 설치 경로
JALib 프레임워크 기반으로 빌드된 모드가 게임 런타임에서 정상적으로 로드되고 실행되려면, 반드시 정해진 디렉터리 구조에 맞게 배포되어야 합니다. 모드는 항상 아래의 경로에 폴더 형태로 위치해야 합니다.

```text
[대상 게임 설치 경로]/Mods/[모드 이름]/
```

---

## 2. 필수 파일 구성
모드 폴더(`[모드 이름]/`) 내부에는 모드 로더 및 JALib 부트스트랩이 모드를 인식하고 실행하기 위해 다음과 같은 필수 파일들이 포함되어야 합니다.

1. **`Info.json`**: 유니티 모드 매니저(UMM)가 모드를 인식하고 진입점을 찾기 위한 메타데이터 파일입니다.
2. **`JAMod.Bootstrap.dll`**: JALib의 비동기 초기화 및 환경 조성을 위해 모드 진입 역할을 담당하는 핵심 라이브러리입니다.
3. **`[모드 이름].dll`**: 개발자가 직접 작성한 모드의 메인 로직 어셈블리 파일입니다.
4. **`JAModInfo.json`**: JALib 부트스트랩 프레임워크가 모드를 구성할 때 참조하는 전용 확장 설정 파일입니다.

---

## 3. Info.json 설정
`Info.json`은 유니티 모드 매니저(UMM) 표준 규격을 따르며, **JALib 부트스트랩을 거쳐 모드가 실행되도록 진입점(EntryMethod)과 의존성을 반드시 아래와 같이 지정**해야 합니다.

### 필수 고정 속성
* **`AssemblyName`**: 유니티 모드 매니저가 가장 먼저 로드할 어셈블리로, 개발자 모드 파일이 아닌 **`JAMod.Bootstrap.dll`**을 지정해야 합니다.
* **`EntryMethod`**: 프레임워크 초기화를 담당하는 **`JAMod.Bootstrap.Bootstrap.Setup`**을 진입점으로 고정합니다.
* **`LoadAfter`**: JALib 코어 라이브러리가 먼저 로드된 후 안정적으로 구동될 수 있도록 필수적으로 **`["JALib"]`**를 포함해야 합니다.

### Info.json 작성 가능 속성
* **`Id`** (`string`, **필수**)
    * 모드의 고유 식별자(ID)입니다. 공백 없이 영문과 숫자로 작성하는 것을 권장합니다.
* **`AssemblyName`** (`string`, **필수**)
    * UMM이 가장 먼저 로드해야 할 파일명입니다. 
    * JALib 모드에서는 부트스트랩이 먼저 인젝션되어야 하므로 **무조건 `"JAMod.Bootstrap.dll"`로 고정**해야 합니다.
* **`EntryMethod`** (`string`)
    * 모드가 로드될 때 UMM이 실행할 진입 메서드입니다. 
    * JALib 프레임워크 초기화를 위해 **`"JAMod.Bootstrap.Bootstrap.Setup"`으로 고정**합니다.
* **`LoadAfter`** (`string[]`)
    * 현재 모드보다 먼저 로드되어야 하는 모드 ID 배열입니다. 
    * JALib 코어가 먼저 로드되어 있어야 하므로 **필수적으로 `"JALib"`를 포함**시켜야 합니다.
* **`Version`** (`string`)
    * 모드의 버전입니다.
    * ⚠️ **주의 사항**: 버전에 알파, 베타 같은 postfix를 붙이고 싶다면 **무조건 한 칸 띄어쓰기 후** 적어야 합니다 (예: `"1.0.0 Beta"`). 
    * 또한 revision 번호 정보는 유저 화면(GUI)에 표시되지 않습니다.
* **`DisplayName`** (`string`)
    * 유니티 모드 매니저(UMM) GUI 창에서 유저들에게 실제로 보여줄 모드의 이름입니다.
* **`Author`** (`string`)
    * 모드를 개발한 제작자 또는 팀의 이름입니다.
* **`ManagerVersion`** (`string`)
    * 모드가 작동하기 위해 요구되는 유니티 모드 매니저(UMM)의 최소 버전입니다.
* **`GameVersion`** (`string`)
    * 모드가 정상 작동함을 보장하는 대상 게임의 버전입니다.
* **`Requirements`** (`string[]`)
    * 이 모드가 실행되기 위해 반드시 설치되어 있어야 하는 타 모드 ID 목록입니다.
* **`HomePage`** (`string`)
    * 모드의 공식 홈페이지 또는 안내 페이지의 웹 링크 주소입니다.
* **`Repository`** (`string`)
    * 모드의 소스 코드가 관리되는 GitHub 등의 저장소(Repository) 주소입니다.
* **`ContentType`** (`string`)
    * 모드가 포함하고 있는 콘텐츠의 유형을 명시합니다.

---

## 4. JAModInfo.json 설정
`JAModInfo.json`은 JALib 부트스트랩이 모드를 로드한 후, 실제 모드의 핵심 메인 로직(`[모드 dll]`)을 찾아 인스턴스를 생성하고 실행할 때 참조하는 내부 설정 파일입니다.

### JAModInfo.json 작성 가능 속성
* **`AssemblyPath`** (`string`)
    * 실제 모드 로직이 들어있는 메인 어셈블리(`.dll`) 파일의 경로입니다.
* **`ClassName`** (`string`)
    * `JAMod`를 상속받아 구현한 모드의 진입점 메인 클래스 위치입니다. 
    * Namespace를 포함한 Full Name(예: `MyMod.Main`)으로 적어야 합니다.
* **`AssemblyRequireModPath`** (`bool`)
    * `AssemblyPath`에 적은 파일 경로를 현재 모드 폴더(`[게임]/Mods/[모드명]/`) 기준으로 해석할지 여부입니다. 
    * `true` 설정 시 모드 폴더 내부에서 상대 경로로 탐색합니다.
* **`DependencyPath`** (`string`)
    * 모드가 의존하는 서드파티 의존성 dll 파일들이 모여있는 폴더 경로입니다.
* **`DependencyRequireModPath`** (`bool`)
    * `DependencyPath`에 적은 의존성 폴더 경로를 현재 모드 폴더 기준으로 해석할지 여부입니다. 
    * `true` 설정 시 모드 폴더 내부를 기준으로 탐색합니다.
* **`BootstrapVersion`** (`int`)
    * 모드가 구동되기 위해 필요한 JALib 부트스트랩 프레임워크의 최소 요구 버전입니다.
* **`Gid`** (`int`, 기본값: `-1`)
    * 구글 스프레드시트를 이용한 모드 실시간 Localization(언어 번역) 기능 연동 시 사용되는 스프레드시트 시트 ID(Gid)입니다.
* **`Discord`** (`string`)
    * 모드 커뮤니티, 피드백 및 버그 리포트를 받기 위한 공식 디스코드 초대 링크 주소입니다.
* **`SettingPath`** (`string`)
    * 모드의 설정 데이터 파일(`settings.json` 등)이 저장되고 읽힐 파일 위치를 커스텀하게 지정합니다.
* **`Dependencies`** (`Dictionary<string, string>`)
    * 이 모드가 의존하고 있는 타 JAMod 기반 모드 목록을 `Key(모드 ID): Value(요구 버전)` 쌍으로 정의합니다.
* **`NoChangeAssemblyName`** (`bool`)
    * 런타임 환경에서 어셈블리의 충돌이나 변조 방지를 위한 어셈블리 이름 변경 프로세스를 금지할지 여부입니다.

---

## [다음](004-SetupFeature.md)
