# JAMod 개발 가이드 - JA모드 설정하기

## [목차로 이동](000-DevelopGuide.md)
1. [JAMod 상속](#1-jamod-%EC%83%81%EC%86%8D)
2. [JAMod 이벤트](#2-jamod-%EC%9D%B4%EB%B2%A4%ED%8A%B8)
3. [JAMod 변수](#3-jamod-%EB%B3%80%EC%88%98)
4. [JAMod 함수](#4-jamod-%ED%95%A8%EC%88%98)
5. [예시 코드](#5-%EC%98%88%EC%8B%9C-%EC%BD%94%EB%93%9C)

---

## 1. JAMod 상속
JALib 프레임워크 기반의 모드를 개발하려면 모드의 메인이 되는 진입점 클래스에 `JAMod`를 상속받아야 합니다.

* **자유로운 클래스명**: 진입점 클래스의 이름은 **꼭 `Main`일 필요가 없으며** 자유롭게 지정할 수 있습니다.
* **Instance 필드 선언 (추천)**: 모드 컨텍스트 전역에서 메인 클래스에 접근할 수 있도록 **`public static [클래스명] Instance;`** 형태의 필드를 반드시 선언하는 것이 좋습니다.
* **자동 인스턴스 주입**: 생성자나 `OnSetup` 내부에서 `Instance = this;`와 같이 직접 대입하는 코드를 작성하지 않아도, **JALib 라이브러리 코어가 런타임에 해당 필드를 찾아 자동으로 현재 인스턴스를 할당**해 줍니다.
```csharp
public class Main : JAMod {
    // 라이브러리가 자동으로 현재 인스턴스를 주입해주므로 필드 선언만 해두면 됩니다.
    public static Main Instance;
}
```

---

## 2. JAMod 이벤트
`JAMod` 메인 클래스를 상속받으면 게임 런타임 및 유니티 모드 매니저(UMM)의 수명 주기에 맞춰 실행되는 다양한 라이프사이클 이벤트를 오버라이드하여 사용할 수 있습니다.

> `JAMod`에서 오버라이드(`override`) 가능한 모든 이벤트 메서드들은 내부적으로 하위 호환성을 위해 준비된 빈 가상 메서드이거나 별도의 필수 베이스 로직을 강제하지 않습니다.
> 
> 따라서 오버라이드하여 구현할 때 **`base.OnEnable();` 또는 `base.OnSetup();` 같은 베이스 메서드는 굳이 호출하지 않아도 완전히 무방합니다.**

### OnSetup
모드가 인스턴스화된 후 초기 환경 설정을 구성하기 위해 가장 먼저 호출되는 이벤트입니다. 주로 **기능 추가(`AddFeature`)**, **모드형 패치 추가(`Patcher.AddPatch(...)`)**, **세팅 데이터 캐스팅**, **필요한 인스턴스 생성** 등의 핵심 초기화 작업들을 진행합니다.

```csharp
protected override void OnSetup() {
    // 세팅 캐스팅 및 필요한 인스턴스 생성
    SettingGUI = new SettingGUI(this);
    Settings = (MyModSetting) Setting;

    // 기능(Feature) 모듈 등록
    AddFeature(new CustomFeature());

    // 모드형 패치 메서드 등록
    Patcher.AddPatch(OnGameStart);
}
```
> 🚨 **CRITICAL WARNING (중요)**
> 
> `OnSetup` 이벤트는 **메인 스레드(Main Thread)가 아닌 환경에서 작동할 수 있습니다.** 
> 
> 따라서 `OnSetup` 내부에서 유니티 API를 직접 호출하거나 유니티 오브젝트 작업(`GameObject` 생성, 컴포넌트 접근, 인게임 UI 직접 조작 등 메인 스레드 전용 작업)을 수행하면 **크래시가 발생**하므로 절대 추가하지 마세요.
> 
> 유니티 엔진과 안전하게 동기화되어야 하는 작업은 [MainThread](007-Tools.md#MainThread)를 이용해 실행하거나 메인 스레드가 보장되는 `OnEnable` 에서 처리해야 합니다.

### OnEnable
모드가 활성화되었을 때 호출됩니다. 메인 스레드가 보장되므로 리소스 로드나 유니티 엔진 관련 메인 작업을 안전하게 작성할 수 있습니다.

```csharp
protected override void OnEnable() {
}
```

### OnDisable
모드가 비활성화되었을 때 호출됩니다. 수정했던 게임 플래그나 리소스를 원상 복구합니다.
```csharp
protected override void OnDisable() {
}
```

### OnUnload
모드가 완전히 언로드(메모리 해제)되었을 때 호출됩니다.
```csharp
protected override void OnUnload() {
}
```

### OnGUI / OnGUIBehind
모드 설정창 내부의 GUI 레이아웃을 그릴 때 실행됩니다. `OnGUIBehind`는 등록된 기능(Feature)들의 GUI가 먼저 그려진 직후에 호출됩니다.
```csharp
protected override void OnGUI() {
}

protected override void OnGUIBehind() {
}
```

### OnShowGUI / OnHideGUI
사용자가 UMM 모드 설정창 레이아웃을 열거나 닫았을 때 각각 호출됩니다.
```csharp
protected override void OnShowGUI() {
}

protected override void OnHideGUI() {
}
```

### OnHideGUI
모드 설정창을 닫았을 때 실행되는 이벤트입니다.
```csharp
protected override void OnHideGUI() {
}
```

### OnUpdate / OnFixedUpdate / OnLateUpdate
유니티 엔진의 프레임 업데이트 타이밍에 맞춰 실시간으로 실행되는 이벤트입니다. `deltaTime` 매개변수를 제공합니다.
```csharp
protected override void OnUpdate(float deltaTime) {
}

protected override void OnFixedUpdate(float deltaTime) {
}

protected override void OnLateUpdate(float deltaTime) {
}
```

### OnLocalizationUpdate
언어 데이터가 업데이트 되었을 때 실행되는 이벤트입니다.
```csharp
protected override void OnLocalizationUpdate() {
}
```

---

## 3. JAMod 변수
`JAMod` 컨텍스트 내부 및 외부에서 접근할 수 있는 주요 필드와 속성(Property) 목록입니다.

`Protected` 등급은 상속받은 메인 클래스 내부 혹은 서브클래스에서만 조작이 가능합니다.

### ModEntry
 * Protected
 * UnityModManager.ModEntry
 * 모드의 ModEntry입니다.

### Logger
 * Public
 * UnityModManager.ModEntry.ModLogger
 * 모드의 Logger입니다.
 * Logger를 불러와서 사용할 수 있지만 JA모드 자체에 로그 함수가 있기 때문에 사용할 필요가 없습니다.

### Name
 * Public
 * string
 * 모드의 이름입니다.

### Version
 * Public
 * Version
 * 모드의 버전입니다.

### Path
 * Public
 * string
 * 모드 폴더의 경로입니다.

### LatestVersion
 * Protected
 * Version
 * 모드의 최신 버전입니다.

### IsLatest
 * Public
 * bool
 * 모드가 최신 버전인지 여부입니다.

### Features
 * Protected
 * List<Feature>
 * 모드의 기능 목록입니다.

### AvailableLanguages
 * Protected
 * SystemLanguage[]
 * 사용 가능한 언어 목록입니다.

### Setting
 * Protected
 * JASetting
 * 모드의 설정입니다.

### Discord
 * Protected
 * 지정 가능
 * string
 * 모드와 관련된 디스코드 링크입니다.

### Enabled
 * Public
 * bool
 * 모드가 활성화 되었는지 여부입니다.

### CustomLanguage
 * Protected
 * 지정 가능
 * SystemLanguage
 * 사용자 정의 언어 입니다.

### Localization
 * Public
 * Localization
 * 모드의 언어 데이터입니다.

---

## 4. JAMod 함수
JAMod에서는 다음과 같은 함수를 지원합니다.

Protected는 JAMod가 상속된 코드에서만 사용할 수 있습니다.

### AddFeature(params Feature[])
 * Protected
 * void
 * 모드에 기능을 추가합니다.
```csharp
AddFeature(new Feature(), new Feature());
```

### Enable()
 * Public
 * void
 * 모드를 활성화 합니다.
```csharp
mod.Enable();
```

### Disable()
 * Public
 * void
 * 모드를 비활성화 합니다.
```csharp
mod.Disable();
```

### Log(Object)
 * Public
 * void
 * 모드의 로그를 출력합니다.
```csharp
mod.Log("Log");
```

### Warning(Object)
 * Public
 * void
 * 모드의 경고 로그를 출력합니다.
```csharp
mod.Warning("Warning");
```

### Error(Object)
 * Public
 * void
 * 모드의 오류 로그를 출력합니다.
```csharp
mod.Error("Error");
```

### Critical(Object)
 * Public
 * void
 * 모드의 치명적인 오류 로그를 출력합니다.
```csharp
mod.Critical("Critical");
```

### NativeLog(Object)
 * Public
 * void
 * 모드의 로그를 출력합니다.
 * 해당 매서드로 출력된 로그는 파일에서만 확인 가능합니다.
```csharp
mod.NativeLog("NativeLog");
```

### LogException(String, Exception)
 * Public
 * void
 * 모드의 예외 로그를 출력합니다.
```csharp
mod.LogException("Fail To Exception", new Exception());
```

### LogException(Exception)
 * Public
 * void
 * 모드의 예외 로그를 출력합니다.
```csharp
mod.LogException(new Exception());
```

### SaveSetting()
 * Public
 * void
 * 모드의 설정을 저장합니다.
```csharp
mod.SaveSetting();
```

---

## 5. 예시 코드
다음은 JAMod를 상속받은 클래스의 예시 코드입니다.
```csharp
namespace JAMod;

public class Main : JAMod {
    protected override void OnSetup() {
        // 모드 생성시 실행시킬 코드
        AddFeature(new Feature());
    }
  
    protected override void OnEnable() {
        // 모드 활성화시 실행시킬 코드
        Log("집에 가고 싶다");
    }
  
    protected override void OnDisable() {
        // 모드 비활성화시 실행시킬 코드
        Log("집에 감");
    }
  
    protected override void OnGUI() {
         // 모드 설정창 GUI를 그릴 때 실행시킬 코드
         GUILayout.Label("집에 가고 싶다");
    }
}
```

## [다음](003-RuntimeEnvironmentSetup.md)
