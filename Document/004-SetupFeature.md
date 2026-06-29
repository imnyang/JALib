# JAMod 개발 가이드 - 기능 구현

## [목차로 이동](000-DevelopGuide.md)
1. [Feature 상속 및 생성자](#1-feature-%EC%83%81%EC%86%8D-%EB%B0%8F-%EC%83%9D%EC%84%B1%EC%9E%90)
2. [Feature 이벤트](#2-feature-%EC%9D%B4%EB%B2%A4%ED%8A%B8)
3. [Feature 변수](#3-feature-%EB%B3%80%EC%88%98)
4. [MultiFeature 활용하기](#4-multifeature-%EB%B0%8F-%EC%9E%90%EB%8F%99-%ED%8C%A8%EC%B9%98-%EC%8B%9C%EC%8A%A4%ED%85%9C)
5. [통합 예시 코드](#5-%ED%86%B5%ED%95%A9-%EC%98%88%EC%8B%9C-%EC%BD%94%EB%93%9C)

---

## 1. Feature 상속 및 생성자
모드 내부의 특정 기능 단위를 모듈식으로 분리하여 관리하기 위해 메인이 되는 클래스에 `Feature`를 상속받아야 합니다.

```csharp
public class MyFeature : Feature
```

`Feature`는 상속받은 개별 모드의 `OnSetup()` 등에서 직접 `new` 키워드로 인스턴스를 생성하므로, 생성자의 접근 제한자나 형태는 자유롭게 구성할 수 있습니다.

`base` 생성자를 호출할 때 전달할 수 있는 인자는 다음과 같습니다. (`필수` 항목을 제외하고는 필요한 경우에만 추가할 수 있습니다.)

* **`JAMod mod`** (**필수**)
    * 기능이 소속될 메인 모드 인스턴스입니다. 모드의 메인 클래스에 선언된 `Instance` 필드를 활용해 전달하는 것을 권장합니다.
* **`string name`** (**필수**)
    * 유저 인터페이스(GUI) 및 내부 식별에 사용될 기능의 고유 이름입니다.
* **`bool canEnable`**
    * 유저가 UI 상에서 해당 기능의 활성화 상태를 임의로 토글(끄고 켜기)할 수 있는지 여부입니다. 기본값은 `true`입니다.
* **`Type patchClass`**
    * 기능과 연결될 전용 패치 클래스 타입을 지정합니다.
* **`Type settingType`**
    * 기능 전용으로 사용할 설정(Setting) 클래스 타입을 지정합니다.

---

## 2. Feature 이벤트
`Feature` 클래스를 상속받으면 모드 상태 및 유저 GUI 조작에 맞춰 수명 주기가 동기화되는 라이프사이클 이벤트를 오버라이드하여 사용할 수 있습니다.

> `Feature`에서 오버라이드(`override`) 가능한 모든 이벤트 메서드들은 내부적으로 하위 호환성을 위해 준비된 빈 가상 메서드이거나 별도의 필수 베이스 로직을 강제하지 않습니다.
> 
> 따라서 오버라이드하여 구현할 때 **`base.OnEnable();` 같은 베이스 메서드는 굳이 호출하지 않아도 완전히 무방합니다.**

* **`OnEnable()`**: 기능이 활성화(Active)되었을 때 실행되는 이벤트입니다. 이 시점부터 실시간 처리나 기능 패치가 유효해집니다.
* **`OnDisable()`**: 기능이 비활성화되었을 때 실행되는 이벤트입니다. 활성화 단계에서 수정했던 플래그나 리소스를 안전하게 원상복구해야 합니다.
* **`OnUnload()`**: 기능이 메모리에서 완전히 언로드될 때 실행됩니다.
* **`OnGUI()`**: 해당 기능 전용 설정창의 GUI 레이아웃을 그릴 때 매 프레임 실행되는 이벤트입니다.
* **`OnShowGUI() / OnHideGUI()`**: 기능 설정 GUI 창이 유저 화면에 열리거나 닫히는 타이밍에 각각 호출됩니다.

---

## 3. Feature 변수
`Feature` 컨텍스트 내부 및 외부에서 상태 제어를 위해 접근할 수 있는 주요 필드와 속성(Property) 목록입니다.

* **`Enabled`** (`Public` / `지정 가능` / `bool`)
    * 유저나 시스템에 의해 기능 토글이 켜져 있는 상태인지를 정의합니다. 코드로 이 값을 변경해 기능을 제어할 수 있습니다.
* **`Active`** (`Public` / `bool`)
    * 기능이 실시간으로 런타임에 작동하고 있는지 나타내는 읽기 전용 플래그입니다. 모드가 켜져 있고 `Enabled`가 `true`일 때 활성화됩니다.
* **`CanEnable`** (`Public` / `지정 가능(Protected)` / `bool`)
    * 기능의 활성화 상태를 유저가 임의로 토글할 수 있는지 여부입니다. 이 값이 `false`이면 설정창에서 토글 UI가 비활성화되며 항상 활성화된 상태로 유지됩니다.
* **`Setting`** (`Public` / `JASetting`)
    * 이 기능에 바인딩된 직렬화 데이터 설정 인스턴스입니다.
* **`Mod`** (`Public` / `JAMod`)
    * 해당 기능 모듈을 소유하고 있는 부모 메인 모드 객체입니다. `Mod.Log(...)` 등을 활용해 부모 모드의 로거로 출력을 수행할 수 있습니다.

---

## 4. MultiFeature 및 자동 패치 시스템

**`MultiFeature`**는 **여러 개의 독립적인 기능(`Feature`)들이 하나의 패치 클래스나 공통 수명 주기 로직을 유기적으로 공유**할 수 있도록 묶어주는 시스템입니다.

### 1) 작동 원리 및 특징
* **중복 패치 방지**: 동일한 게임 메서드를 여러 기능에서 후킹해야 할 때, 중복 패치 오버헤드를 막고 단 한 번만 적용(`Patch()`)되도록 패치를 공유합니다.
* **참조 카운팅 수명 주기**: 공유 중인 서브 기능 중 **최소 하나라도 켜지면 공통 패치가 적용**되고, 연결된 **모든 기능이 전부 꺼질 때만 패치가 안전하게 해제(`Unpatch()`)**되도록 관리됩니다.
* **일반 클래스 자동 패치 지원**: `AddMultiFeatures` 호출 시 `MultiFeature`를 상속받지 않은 일반 클래스 타입을 넘겨주면, 프레임워크가 **해당 클래스 내부에 정의된 JAPatch 메서드들을 감지하여 자동으로 패치 및 언패치(모든 기능 비활성화 시)를 제어**해 줍니다.

### 2) 등록 방법
개별 `Feature` 클래스의 **생성자(Constructor) 내부**에서 다음과 같이 호출하여 공유할 패치 대상을 등록합니다.
'''csharp
AddMultiFeatures(typeof(공유할_패치_또는_MultiFeature_클래스));
'''

---

## 5. 통합 예시 코드

공통 패치 및 리소스를 관리하는 `MultiFeature`를 구성하고, 개별 기능들의 생성자에서 `AddMultiFeatures`로 연동하여 자원을 효율적으로 공유하는 정석 예시입니다.

### 1) 공유 패치 및 데이터를 관리할 MultiFeature 정의
```csharp
using JALib.Core;
using JALib.Core.Patch;

namespace MyCustomMod;

// 여러 기능이 판정 데이터 및 하모니 패치를 공유할 수 있는 MultiFeature입니다.
public class GameTimingSharedLogic : MultiFeature {
    public static List<float> HitTimings;

    // 부모 모드 인스턴스를 base 생성자에 전달합니다.
    public GameTimingSharedLogic() : base(Main.Instance) {
        // 이 MultiFeature가 켜질 때 적용할 하모니 패치 클래스 지정
        Patcher.AddPatch(typeof(GameTimingSharedLogic));
    }

    protected override void OnEnable() {
        // 첫 기능이 켜지는 시점에 공유 자원 할당
        HitTimings = new List<float>();
    }

    protected override void OnDisable() {
        // 모든 서브 기능이 꺼져 패치가 Unpatch된 후 안전하게 리소스 해제
        HitTimings.Clear();
        HitTimings = null;
    }

    // [JAPatch] 어트리뷰트를 활용한 게임 핵심 메서드 후킹 예시
    [JAPatch(typeof(scrController), "TogglePauseGame", PatchType.Postfix, false)]
    public static void ResetDataOnPause() {
        Mod.Log("게임 일시정지로 인해 데이터를 리셋합니다.");
    }
}
```

### 2) Feature 생성자에서 AddMultiFeatures를 이용한 연동
```csharp
using JALib.Core;

namespace MyCustomMod;

// 첫 번째 기능: 인게임 판정 팝업 GUI 기능
public class TimingPopupFeature : Feature {
    public TimingPopupFeature() : base(Main.Instance, nameof(TimingPopupFeature)) {
        // 생성자 단에서 공유할 MultiFeature 로직 클래스를 등록합니다.
        AddMultiFeatures(typeof(GameTimingSharedLogic));
    }

    protected override void OnEnable() {
        Mod.Log("판정 팝업 기능이 활성화되었습니다. (공유 패치 카운트 +1)");
    }
}

// 두 번째 기능: 실시간 판정 파일 로깅 기능
public class TimingFileLoggerFeature : Feature {
    public TimingFileLoggerFeature() : base(Main.Instance, nameof(TimingFileLoggerFeature)) {
        // 동일한 클래스를 등록하여 중복 패치 없이 가볍게 판정 자원을 공유합니다.
        AddMultiFeatures(typeof(GameTimingSharedLogic));
    }

    protected override void OnEnable() {
        Mod.Log("판정 로그 기록 기능이 활성화되었습니다. (공유 패치 카운트 +1)");
    }
}
```

## [다음](005-SetupSetting.md)
