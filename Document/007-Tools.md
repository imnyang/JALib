# JAMod 개발 가이드 - 유틸리티 시스템 (Tools & Utilities)

### [목차로 이동](DevelopGuide.md)
1. [스레드 및 비동기 제어](#1-%EC%8A%A4%EB%A0%88%EB%93%9C-%EB%B0%8F-%EB%B9%84%EB%8F%99%EA%B8%B0-%EC%A0%9C%EC%96%B4)
2. [간편한 리플렉션 시스템](#2-%EA%B0%84%ED%8E%B8%ED%95%9C-%EB%A6%AC%ED%94%8C%EB%A0%89%EC%85%98-%EC%8B%9C%EC%8A%A4%ED%85%9C)
3. [바이트 스트림 및 직렬화 도구](#3-%EB%B0%94%EC%9D%B4%ED%8A%B8-%EC%8A%A4%ED%8A%B8%EB%A6%BC-%EB%B0%8F-%EC%A7%81%EB%A0%AC%ED%99%94-%EB%8F%84%EA%B5%AC)
4. [인게임 네트워크 클라이언트](#4-%EC%9D%B8%EA%B2%8C%EC%9E%84-%EB%84%A4%ED%8A%B8%EC%9B%8C%ED%81%AC-%ED%81%B4%EB%9D%BC%EC%9D%B4%EC%96%B8%ED%8A%B8)
5. [파일 처리 및 압축 시스템](#5-%ED%8C%8C%EC%9D%BC-%EC%B2%98%EB%A6%AC-%EB%B0%8F-%EC%95%95%EC%B6%95-%EC%8B%9C%EC%8A%A4%ED%85%9C)
6. [게임 모딩 전용 확장 유틸리티](#6-%EA%B2%8C%EC%9E%84-%EB%AA%A8%EB%94%A9-%EC%A0%84%EC%9A%A9-%ED%99%95%EC%9E%A5-%EC%9C%A0%ED%8B%B8%EB%A6%AC%ED%8B%B0)
7. [기타 편의성 도구](#7-%EA%B8%B0%ED%83%80-%ED%8E%B8%EC%9D%98%EC%84%B1-%EB%8F%84%EA%B5%AC)

---

## 1. 스레드 및 비동기 제어

유니티(Unity) 엔진은 단일 스레드(Main Thread) 아키텍처 위에서 모든 렌더링, 물리 연산, 게임 로직이 순차적으로 수행됩니다. 따라서 멀티스레드나 백그라운드 스레드풀에서 유니티 API(예: GameObject 및 Component 조작, `Time.time` 등)에 직접 접근하면 스레드 안전성 예외(`UnityException`)가 발생하며 게임이 멈추거나 오동작하게 됩니다.

JALib은 이러한 멀티스레딩 환경에서 발생할 수 있는 크래시를 방지하고, 백그라운드 연산 및 대리자(Delegate) 실행 과정에서 예외가 발생하더라도 게임이 튕기지 않고 모드 로그 시스템으로 안전하게 가로채어 격리될 수 있도록 `MainThread`, `JATask`, `JAction` 유틸리티를 지원합니다.

### 1.1. 메인 스레드 디스패처 (`MainThread`)

`MainThread` 유틸리티는 백그라운드 워커 스레드(네트워크 패킷 수신, 웹 요청, 파일 입출력 등)에서 완료된 작업 결과나 연산들을 유니티의 **메인 업데이트 루프 프레임 내부로 전달하여 메인 스레드 권한으로 안전하게 실행**되도록 보장하는 컴포넌트입니다.

#### 1) 핵심 API 명세 및 사용법
* **`MainThread.Run(JAction action)`**
  * 예외 격리 처리가 내장된 `JAction` 대리자 객체를 메인 스레드 컨텍스트에서 안전하게 실행하도록 등록합니다.
* **`MainThread.Run(JAMod mod, Action action)`**
  * 특정 모드 인스턴스(`mod`)의 컨텍스트를 지정하여 일반 `Action` 대리자 로직을 메인 스레드 주기에 밀어 넣고 즉시 실행을 유도합니다.
* **`MainThread.IsMainThread()`**
  * 현재 코드가 흐르고 있는 스레드가 유니티 메인 스레드인지 여부를 `bool` 값으로 반환합니다.

#### 2) MainThread 정석 활용 예시
```csharp
using JALib.Tools;

public void ProcessNetworkData(string rawData) {
    // 백그라운드 스레드에서 무거운 문자열 연산이나 파싱을 수행했다고 가정합니다.
    string processedText = CustomParser.Execute(rawData);

    // 🚨 이 지점에서 유니티 UI 컴포넌트나 인게임 매니저를 직접 조작하면 크래시가 발생합니다.
    // 따라서 MainThread.Run을 호출하여 현재 모드(Main.Instance)와 실행할 액션을 메인 스레드로 넘겨줍니다.
    MainThread.Run(Main.Instance, () => {
        if(scrController.instance) {
            // 이제 메인 스레드 권한이 확보되었으므로 안전하게 유니티 API를 조작할 수 있습니다.
            scrController.instance.txtInfo.text = "동기화 완료: " + processedText;
        }
    });
}
```

### 1.2. 백그라운드 태스크 매니저 (`JATask`)

`JATask`는 .NET 표준 `Task` 인프라를 기반으로 작동하는 비동기 태스크 엔진입니다. 게임 플레이 도중 무거운 파일 입출력이나 복잡한 계산 루프가 실행될 때 메인 프레임이 일시적으로 얼어붙는 현상(Freeze)을 막기 위해 사용됩니다.

#### ⚠️ 핵심 매커니즘 (Try-Catch 예외 격리 및 로깅)
`JATask` 내부에서 실행되는 로직은 기존 C# 코드나 비동기 태스크 파이프라인에서 **치명적인 예외(Exception)가 발생했을 때 게임이 튕기거나 본편이 크래시 나지 않도록 차단**하는 안전망 역할을 합니다.

내부적으로 전체 `try-catch` 및 비동기 상태 머신(`IAsyncStateMachine`) 캡슐화 레이어를 감싸고 있어, 에러 발생 시 예외를 안전하게 가로챈(Catch) 뒤 프레임워크 내부의 모드 에러 보고 시스템(`mod.LogReportException`)으로 예외 스택 트레이스를 자동 전송하고 해당 스레드를 부작용 없이 안전하게 소멸시킵니다.

#### 1) 비동기 태스크 개설 API (`JATask.Run`)
`JATask.Run`은 다양한 시그니처 오버로드를 통해 반환값 처리 및 작업 취소 토큰(`CancellationToken`) 연동을 지원합니다.

* **동기 대리자 실행**:
  * `public static Task Run(JAMod mod, Action action)`
  * `public static Task Run(JAMod mod, Action action, CancellationToken cancellationToken)`
* **JAction 스케줄러 실행**:
  * `public static Task Run(JAction action)`
  * `public static Task Run(JAction action, CancellationToken cancellationToken)`
* **비동기 람다 및 반환값(TResult) 처리**:
  * `public static Task Run(JAMod mod, Func<Task> action)`
  * `public static Task<TResult> Run<TResult>(JAMod mod, Func<TResult> action)`
  * `public static Task<TResult> Run<TResult>(JAMod mod, Func<Task<TResult>> action)`

```csharp
using System.Threading;
using JALib.Tools;

public async void StartBackgroundCalculations(CancellationToken token) {
    // 반환값이 존재하는 람다 함수와 취소 토큰을 JATask에 실어서 백그라운드 풀에 인젝션합니다.
    int result = await JATask.Run(Main.Instance, () => {
        // 내부 연산 중 예외가 터져도 JALib 코어가 자동으로 안전하게 catch하여 모드 에러 로그에 남깁니다.
        int scoreResult = ComplexScoreAlgorithm.Execute();
        return scoreResult;
    }, token);
}
```

#### 2) 기존 태스크 예외 추적 및 연결 (`CatchException` 확장 메서드)
이미 외부나 타 시스템에 의해 생성된 일반 `Task` 혹은 `Task<TResult>` 객체가 존재할 때, 여기에 JALib 특유의 안전망과 모드 로그 추적 시스템을 런타임에 동적으로 이어 붙여줄 수 있는 강력한 확장 메서드(Extension Methods) 체인입니다.

* **`task.CatchException(JAMod mod)`** / **`task.CatchException<TResult>(JAMod mod)`**:
  * 해당 태스크를 백그라운드 스레드에서 비동기적으로 관찰하다가 에러(Faulted) 발생 시 예외를 Catch하여 모드 로그에 기록합니다.
* **`task.CatchExceptionSync(JAMod mod)`** / **`task.CatchExceptionSync<TResult>(JAMod mod)`**:
  * 현재 스레드 컨텍스트에서 동기식으로 태스크 결과를 대기 및 소화하며 예외를 격리 처리합니다.

```csharp
// 일반 C# 라이브러리에서 받아온 외부 Task 객체에 모드 에러 로그 안전망을 덧씌웁니다.
Task externalTask = CustomNetworkLibrary.DownloadAssetAsync();

// CatchException을 붙여두면 해당 비동기 작업 중 에러 발생 시 모드 로그로 전송됩니다.
externalTask.CatchException(Main.Instance);
```

#### 3) 태스크 완료 사후 처리 콜백 (`OnCompleted` / `OnCompletedAsync`)
태스크가 성공적으로 끝났거나 에러가 발생하여 멈췄을 때, 연속적으로 실행할 사후 콜백 함수를 지정합니다. `CompleteFlag` 열거형 조합에 따라 태스크 자체의 예외를 잡을지(`TryCatchTask`), 콜백 액션의 예외를 잡을지(`TryCatchAction`) 상세하게 튜닝할 수 있습니다.

* **동기식 순차 콜백 연동**:
  * `public static void OnCompleted(this Task task, JAMod mod, Action<Task> action, CompleteFlag flag = CompleteFlag.All)`
* **비동기식 백그라운드 연속 콜백 연동**:
  * `public static void OnCompletedAsync(this Task task, JAMod mod, Action<Task> action, CompleteFlag flag = CompleteFlag.All)`

```csharp
Task myTask = JATask.Run(Main.Instance, () => DoSomething());

// 태스크 완료 시점(성공/실패 모두 포함)에 가해질 콜백 등록
myTask.OnCompleted(Main.Instance, (t) => {
    if (t.IsFaulted) {
        // 사후 처리 로직 가동
    }
}, JATask.CompleteFlag.All); // Task와 Action 내부의 예외 try-catch 격리 보호막을 모두 활성화
```

### 1.3. 예외 안전 대리자 실행기 (`JAction`)

`JAction`은 C# 표준 `Action` 대리자를 JALib 프레임워크 규격에 맞추어 안전하게 캡슐화한 **예외 격리 실행용 커스텀 클래스**입니다.

일반적인 대리자 실행 방식(`action()`)은 내부 작업 중 에러가 터졌을 때 호출 스택 전체로 예외를 전포시켜 유니티 엔진과 게임 전체를 멈추게 만들 수 있습니다. `JAction`은 이러한 구조를 방어하기 위해 설계되었습니다.

#### ⚠️ 핵심 매커니즘 (Try-Catch 완벽 격리 및 자동 바인딩)
* **`Invoke()` 지점의 완벽한 예외 포획**: `JAction.Invoke()` 메서드가 호출되는 순간 내부적으로 강력한 `try-catch` 안전망이 가동됩니다. 실행 도중 에러가 발생하더라도 게임 본편이 크래시 나거나 주 스레드가 꼬이지 않도록 차단하며, 지정된 모드 인스턴스(`mod`) 또는 JALib 코어 엔진에 실패 함수명과 예외 스트림 데이터를 즉시 보고(`LogReportException`)해 줍니다.
* **암시적 형변환 기법 (`implicit operator`)**: 일반 `Action` 타입을 `JAction` 매개변수 자리에 대입하면 **컴파일러 단에서 별도 변환 생성자 없이 자동으로 `new JAction(null, action)`을 호출하여 변환**해 주므로 코드 가독성과 범용성이 극대화되어 있습니다.

#### 1) 핵심 프로퍼티 명세
* **`Method`** (`MethodInfo`): 감싸져 있는 대상 원본 메서드의 리플렉션 메타데이터를 추적합니다.
* **`Target`** (`object`): 대리자가 바인딩되어 있는 클래스의 인스턴스 타깃 주소를 역추적합니다.

#### 2) JAction 정석 구현 및 활용 예시
```csharp
using JALib.Tools;

public class MyModMenu {
    // JAction을 인자로 선언해 두면 유저는 일반 Action 람다식을 그냥 집어넣어도 매핑됩니다.
    public void RegisterMenuClickEvent(JAction onClickCallback) {
        // ... 메뉴 등록 처리 ...

        // 이벤트 실행 지점에서 안전하게 Invoke를 수행합니다.
        // onClickCallback 내부 로직 중 NullReferenceException 등이 발생하더라도,
        // 이 실행기 메뉴 클래스와 게임 본편은 완벽하게 예외 격리되어 안전을 유지합니다.
        onClickCallback.Invoke();
    }

    public void Init() {
        RegisterMenuClickEvent(new JAction(Main.Instance, () => {
            string text = scrController.instance.txtInfo.text; // 잠재적 에러 유발 지점
            Main.Instance.Log("메뉴 클릭 이벤트가 안전하게 실행됨: " + text);
        }));
    }
}
```

---

> 💡 **스레딩 및 대리자 개발 요약**
> * **`MainThread.Run(mod, action)`**을 사용하여 안전하게 메인 스레드로 진입하여 유니티 객체를 핸들링하세요.
> * **`JATask`**와 **`JAction`**은 단순 백그라운드 및 대리자 호출을 넘어, 실행 도중 예외가 터져도 게임이 종료되지 않게 `try-catch` 보호막을 치고 모드 로그로 에러 트레이스를 안전하게 넘겨주는 강력한 복구 인프라입니다.

---

## 2. 간편한 리플렉션 시스템

게임 오리지널 소스 코드의 `private` 또는 `internal` 필드, 메서드, 프로퍼티, 생성자에 접근할 때 일반적인 닷넷 표준 리플렉션(`GetField`, `Invoke`)을 호출하면 코드의 가독성이 떨어지고 매번 긴 탐색 구문을 작성해야 하는 번거로움이 있습니다.

JALib은 하모니의 `AccessTools.all` 내부 마스킹을 활용한 **C# 확장 메서드 레이어(`SimpleReflect.cs`)**와 복잡한 캐스팅을 우회할 수 있도록 벼려진 **중간 레이어(`SimpleUnsafeReflect.il`)**를 지원합니다. 이를 통해 매우 간단하고 직관적인 코드로 게임 내부의 은닉된 요소들을 자유롭게 다룰 수 있습니다.

### 2.1. 간단한 리플렉션 API (`SimpleReflect` C# 확장 메서드)

`SimpleReflect` 클래스는 모든 핵심 리플렉션 탐색 루틴을 닷넷 내부의 `Type`, `FieldInfo`, `PropertyInfo`, `MethodInfo` 및 일반 `object` 인스턴스에 대한 **확장 메서드(Extension Methods)** 형식으로 구현하여 코드를 획득하기 매우 편리하게 만들어 줍니다.

#### 1) 필드(Field) 및 프로퍼티(Property) 가져오기
* **`type.Field(string name)`** / **`type.Fields()`**: public/private 구분 없이 이름만으로 상주 필드를 바로 찾아옵니다.
* **`type.Property(string name)`** / **`type.Properties()`**: 은닉된 프로퍼티 및 캡슐화 속성 데이터를 타겟팅합니다.
* **`property.Getter()`** / **`property.Setter()`**: 프로퍼티 내부의 실질적인 `get`/`set` 접근자 메서드(`MethodInfo`)를 간편하게 확보합니다.

#### 2) 메서드(Method) 및 생성자(Constructor) 확보
* **`type.Method(string name)`** / **`type.Method(string name, params Type[] types)`**: 오버로딩 시그니처 매개변수 매칭을 지원하여 복잡한 함수를 간단히 탐색합니다.
* **`type.Constructor()`** / **`type.Constructors()`**: 객체 인스턴스를 강제 동적 생성하기 위한 private 생성자 포인터를 확보합니다.

#### 3) 인스턴스 및 타입 직통 연산 제어 규칙
리플렉션 인스턴스 정보(`MethodInfo`, `FieldInfo`)를 사전에 따로 보관하지 않고, **일반 `object` 변수 자체에서 실시간으로 필드값을 대입하거나 가로챌 수 있는 단축 메서드 목록**입니다. 필드가 탐색되지 않을 경우 자동으로 동일 명칭의 프로퍼티(`PropertyInfo`)를 역추적하여 교정해 줍니다.

* **`obj.GetValue(string name)`** / **`obj.GetValue<T>(string name)`**: 필드 또는 프로퍼티의 내부 데이터를 한 줄의 코드로 간단히 획득합니다.
* **`obj.SetValue(string name, object value)`**: 은닉 전역 변수나 프로퍼티의 값을 실시간으로 세팅합니다.
* **`obj.Invoke(string name, params object[] objects)`** / **`obj.Invoke<T>(...)`**: 객체의 인스턴스 private 메서드를 즉시 호출합니다.

```csharp
using JALib.Tools;

public void TweakControllerFields() {
    // 1. 인스턴스 타깃 확장 메서드 형태 (매우 간결하고 직관적임)
    // 리플렉션 코드를 길게 적지 않아도 private 필드 chosenPlanet을 간단히 탈취합니다.
    var currentPlanet = scrController.instance.GetValue("chosenPlanet");

    // private 프로퍼티나 필드 상관없이 내부 상태를 간단하게 변경합니다.
    scrController.instance.SetValue("state", States.PlayerControl);

    // 2. Type 기반 직통 제어 형태 (정적 클래스나 특정 데이터 파싱용)
    // scrConductor 내부에 은닉된 private static void 함수를 즉시 Invoke 트리거합니다.
    typeof(scrConductor).Invoke("UpdateAwakeConductor");
}
```

### 2.2. 직관적인 확장 캐스팅 레이어 (`SimpleUnsafeReflect` 내부 형식 우회)

C# 소스 코드 상에서 리플렉션을 거쳐 반환된 `object` 데이터를 다시 내가 원하는 원래 타입으로 매번 캐스팅(`(float)field.GetValue(obj)`)하는 작업은 다소 번거롭습니다. JALib은 이를 내부 IL 연산 레벨에서 자연스럽게 제어할 수 있도록 `SimpleUnsafeReflect` 세션을 결합하고 있습니다.

이 시스템은 형식 검증 단계를 가볍게 통과하며, 반환 타입을 개발자가 원하는 제네릭 구조(`<T>`)로 명시하면 **번거로운 캐스팅 단계를 거치지 않고 내 모드 코드로 바로 데이터 타입을 맞추어 리턴**해 줍니다.

#### 1) 핵심 확장 유틸리티 인터페이스 사양
* **`field.GetValueUnsafe<T>(object o = null)`** (`where T : class`): 필드 데이터를 제네릭 형태로 즉시 맞추어 가져옵니다. 내부 IL 사양 제약상 **참조 형식(Class 타입)만 지원**하므로 값 형식(struct, 자료형 등) 추출 시에는 일반 `GetValue<T>` 메서드를 활용해야 합니다.
* **`methodInfo.InvokeUnsafe<T>(object o, object[] parameters)`** (`where T : class`): 함수 실행 후 최종 결과물을 지정한 `<T>` 참조 타입으로 단번에 통일하여 수령합니다.
* **`type.NewUnsafe<T>()`** / **`type.NewUnsafeValue<T>()`**: `Activator` 오버헤드를 쓰지 않고 private 생성자가 적용된 클래스 또는 값 형식 구조체(`ValueType`)의 인스턴스를 즉시 새로 개설하여 깔끔하게 제공합니다.

```csharp
using System.Reflection;
using JALib.Tools;

public void ConvenientReflectionLoop() {
    // 1. GetValueUnsafe는 참조 형식(Class) 데이터 추출 시 캐스팅 없이 깔끔하게 가져올 수 있습니다.
    FieldInfo currentBallField = typeof(scrController).Field("chosenPlanet");
    GameObject ballObject = currentBallField.GetValueUnsafe<GameObject>(scrController.instance);

    // 2. 값 형식(float, int 등) 변수 데이터는 일반 GetValue<T> 확장 메서드를 사용하는 것이 올바른 구조입니다.
    FieldInfo speedField = typeof(scrController).Field("autoplaySpeed");
    float speed = speedField.GetValue<float>(scrController.instance);
}
```

### 2.3. 인게임 모드 및 어셈블리 정밀 탐색 유틸리티

`SimpleReflect`는 유니티 게임 본편 및 결합된 모드 시스템들(`UnityModManager`)과의 연동 구조를 유기적으로 파싱할 수 있는 특수 어셈블리 헬퍼 메서드들을 탑재하고 있습니다.

* **`SimpleReflect.GetType(string typeName)`**
    * 오리지널 게임 어셈블리(`ADOBase`)뿐만 아니라, `AssemblyLoader`에 업로드된 타사 모든 모드의 독립된 라이브러리 및 네임스페이스 영역, 그리고 `UnityModManager`에 등록된 활성화 루프 주기(`OnToggle`, `OnGUI` 등) 내부의 선언 어셈블리 스트림 전체를 실시간 전수 동적 스캔하여, 오타나 난독화 환경에서도 매칭되는 문자열의 **C# `Type`을 완벽하게 찾아내어 반환**해 줍니다.
* **`assembly.GetMod()`** / **`type.GetMod()`**
    * 지정한 어셈블리나 특정 클래스 타입이 어떤 유니티 모드 엔트리(`ModEntry`)의 소유에 속해 있는지 부모 인스턴스를 명확하게 추적합니다.
* **`assembly.GetJAMod()`** / **`type.GetJAMod()`**
    * 현재 런타임에 인젝션되어 메모리에 상주 중인 JALib 계열의 커스텀 모드 객체(`JAMod`)의 인스턴스 레퍼런스 주소를 실시간으로 연동해 줍니다.

```csharp
using JALib.Tools;

public void ModIntercommunication() {
    // 1. 타사 모드의 네임스페이스 내 클래스 타입을 문자열 하나로 간단히 탐색 수행
    Type thirdPartyType = SimpleReflect.GetType("ThirdPartyMod.Core.CustomManager");

    if(thirdPartyType != null) {
        // 2. 해당 타입을 구동하고 있는 부모 유니티 모드 엔트리를 간단히 가져옵니다.
        UnityModManager.ModEntry modEntry = thirdPartyType.GetMod();
        JALogger.LogInternal($"대상 모드 탐색 완료: {modEntry.Info.DisplayName}");
    }
}
```

---

> 💡 **리플렉션 설계 요약**
> * 일반적인 초기화나 가독성이 중요시되는 시점에는 간결한 **`obj.GetValue("fieldName")`** 또는 **`obj.Invoke("methodName")`** 확장 체인을 사용하여 간단하게 리플렉션을 해결하세요.
> * 복잡한 캐스팅 코드나 데이터 타입 통일이 까다로울 때는 미리 필드와 메서드를 static 변수에 캐싱해 둔 후, 제네릭 기반의 **`GetValueUnsafe<T>()`**와 **`InvokeUnsafe<T>()`** 레이어를 사용하여 깔끔하게 구현하는 것이 모드 최적화 아키텍처의 정석입니다.

---

## 3. 바이트 스트림 및 직렬화 도구

네트워크 패킷이나 세이브파일 등의 데이터를 저장하고 전송할 때, 크기가 큰 JSON 문자열이나 XML 대신 구조적 이진 바이트 스트림(`byte[]`)으로 파싱하면 디스크 용량을 극적으로 아끼고 직렬화/역직렬화 연산 성능을 최적화할 수 있습니다.

JALib은 리플렉션과 커스텀 데이터 마킹 기법을 결합한 **바이너리 가공 레이어(`ByteTools.cs`, `StreamTool.cs`)**와 어셈블리 규격을 정밀하게 설계할 수 있는 **직렬화 제어 어트리뷰트 세트**를 제공합니다.

### 3.1. 구조적 바이트 직렬화 제어 어트리뷰트 (Attributes)

모드 내부의 설정 데이터 클래스나 패킷 구조체의 필드 상단에 부착하여 바이너리 변환 및 바이트 스트림 파싱 규칙을 정의하는 직렬화 전용 속성입니다.

* **`[DataAttribute]`**
  * 직렬화/역직렬화 프로세스의 대상이 되는 핵심 데이터 모델 클래스 또는 구조체 상단에 반드시 명시해야 하는 최상위 마커 속성입니다.
* **`[DataIncludeAttribute]` / `[DataExcludeAttribute]`**
  * 특정 변수나 프로퍼티 필드를 바이너리 변조 스트림 변환 대상에 명시적으로 포함하거나 과감히 제외시킬 때 활용합니다.
* **`[IncludeClassAttribute]`**
  * 클래스 내부에 중첩 선언된 하위 참조 클래스 인스턴스 멤버까지 바이트 스트림 파싱 세션에 연쇄 포함시키고자 할 때 선언합니다.
* **`[CastAttribute]` / `[FirstCast]`**
  * 메모리 공간을 극도로 절약하기 위해, C# 데이터 형식(예: `int`)을 스트림에 밀어 넣거나 읽어올 때 더 작은 바이트 크기(예: `byte`, `short`)로 강제 하향 변환(Casting)하여 입출력하도록 지시합니다.
* **`[IgnoreArrayAttribute]`**
  * 배열이나 리스트 구조 데이터를 다룰 때 일괄적인 배열 변환 직렬화 수집 대상에서 제외합니다.
* **`[VersionAttribute]`**
  * 모드의 대규모 기능 업데이트로 인해 데이터 패킷 구조나 세이브파일 세션 필드가 확장되었을 때, 구버전 보관 데이터와의 하위 호환성을 정밀하게 분기 유지하기 위한 리비전 버지닝 제어 속성입니다.
* **`[DeclearingAttribute]` / `[DummyAttribute]`**
  * 데이터 정렬 및 스트림 패딩을 위해 더미 바이트 영역을 확보하거나 선언 컨텍스트를 제어할 때 사용됩니다.

### 3.2. 바이트 변환 및 가공 유틸리티 (`ByteTools` & `StreamTool`)

`ByteTools`와 `StreamTool`은 메모리 스트림(`MemoryStream`) 및 생 바이트 배열(`byte[]`)을 다룰 때 기본 자료형(정수, 실수, 불리언)과 인코딩된 문자열 데이터를 매우 안전하고 일관되게 밀어 넣고 꺼낼 수 있는 저수준 도구 세트입니다.

#### 구조적 바이너리 직렬화 모델 설계 정석 예시
```csharp
using JALib.Tools.ByteTool;

// 1. [DataAttribute]를 선언하여 이 클래스가 바이너리 직렬화 명세 모델임을 명시합니다.
[Data]
public class UserSyncPacket {
    [DataInclude]
    public string PlayerName { get; set; }

    [DataInclude]
    [Cast(typeof(byte))] // 내부적으론 int 형식이지만 바이트 스트림에는 1바이트(byte) 크기로 압축하여 보관합니다.
    public int PlayerLevel { get; set; }

    [DataExclude]
    public int temporaryRuntimeIndex; // 이 필드는 바이너리 직렬화 변환에서 완전히 제외됩니다.

    [Version(MinVersion = 2)]
    [DataInclude]
    public float customPitchOffset; // 모드 버전 2 이상에서만 직렬화 스트림에 결합되는 신규 수치 필드
}
```

### 3.3. 진행률 공유 스트림 (`ProgressStream`)

`ProgressStream`은 대용량의 에셋번들, 커스텀 리소스 오디오 팩, 혹은 네트워크로부터 전송되는 파일 바이너리 스트림 데이터를 읽고 쓸 때, **현재 전송 및 처리 완료된 데이터의 실시간 진행 비율(Progress %)을 동적으로 가로채어 공유하는 확장 데이터 스트림**입니다.

중단 없는 대용량 데이터 복사 도중 유니티 메인 화면에 캘리브레이션 팝업이나 진행 바(Progress Bar)용 진행도 콜백 트리거를 연동할 때 필수적인 편의 인터페이스를 제공합니다.

```csharp
using System.IO;
using JALib.Tools;

public void CopyLargeAssetWithProgress(string sourcePath, string destPath) {
    using var sourceStream = new System.IO.FileStream(sourcePath, FileMode.Open);
    using var destinationStream = new System.IO.FileStream(destPath, FileMode.Create);

    // 원본 스트림을 ProgressStream으로 래핑하고 총 크기와 실시간 업데이트 콜백 핸들러를 바인딩합니다.
    using var progressStream = new ProgressStream(sourceStream, sourceStream.Length);
    
    progressStream.OnProgress += (totalBytesRead, percentage) => {
        // 백그라운드나 업데이트 주기에서 연동되는 실시간 진행률 수신 지점
        // MainThread와 안전 결합하여 유니티 UI 컴포넌트의 게이지를 밀어줍니다.
        MainThread.Run(Main.Instance, () => {
            scrCustomLoadingUI.Instance.UpdateProgressBar(percentage); // 예: 0.0 ~ 1.0
        });
    };

    // 스트림 복사 진행 (복사가 이루어지는 동안 OnProgress 이벤트가 주기적으로 호출됨)
    progressStream.CopyTo(destinationStream);
}
```

---

> 💡 **바이너리/직렬화 설계 요약**
> * 모드의 세이브파일 데이터나 멀티플레이용 패킷 구조체를 설계할 때는 **`[DataAttribute]`** 군의 마킹 규칙을 조합하여 패킷의 직렬화 레이아웃 크기를 효율적으로 다듬으세요.
> * 파일 입출력이나 스트림 가공 과정에서 긴 로딩 시간 동안 사용자 경험(UX)을 유지하기 위해, **`ProgressStream`**을 중간에 브릿지로 래핑하여 진척도를 가로채어 시각화하는 것이 고품질 모드 아키텍처의 기본 원칙입니다.

---

## 4. 인게임 네트워크 클라이언트

모드 고유의 중앙 인증 서버 연동, 스코어보드 데이터 동기화, 디스코드 OAuth2 토큰 갱신, 혹은 저지연 실시간 멀티플레이 패킷 교환 인프라를 구축할 때 외부 노드와의 무중단 세션 수립을 담당하는 통합 네트워크 패키지입니다.

JALib은 유니티의 메인 프레임 스레드를 블로킹(Freeze)하지 않도록 .NET 표준 비동기 소켓 세션을 바인딩하고, C# 확장 기능을 적극적으로 활용한 웹 요청 및 저수준 이진 스트림 파싱 함수들을 지원합니다.

### 4.1. 경량 HTTP 통신 클라이언트 (`SimpleHttp`)

`SimpleHttp`는 .NET의 `HttpClient` 및 구형 `WebClient` 아키텍처에 간결한 데이터 교환용 C# **확장 메서드(Extension Methods)**를 주입하여 REST API 및 외부 웹 서버와 비동기로 데이터를 GET, POST, PUT, DELETE 하도록 지원하는 유틸리티입니다.

#### 1) 핵심 인터페이스 및 데이터 리드 규칙
모든 웹 데이터 수신 메서드는 원시 바이트 배열 형식(`Task<byte[]>`) 또는 문자열 인코딩 형식(`Task<string>`) 중 필요한 규격을 명시적으로 지목하여 호출할 수 있도록 쌍을 이루어 오버로딩되어 있습니다.
* **데이터 요청 및 전송**:
  * `client.Get(url)` / `client.GetString(url)`
  * `client.Post(url, data)` / `client.PostString(url, data)` *(매개변수: `byte[]`, `string`, `HttpContent` 지원)*
  * `client.Put(url, data)` / `client.PutString(url, data)`
  * `client.Delete(url)` / `client.DeleteString(url)`
* **`client.SetupUserAgent(appName, appVersion)`**:
  * 플랫폼 서버의 유저 데이터 수집 및 보안 필터링을 통과할 수 있도록 브라우저 규격의 User-Agent 헤더 문자열을 자동으로 빌드하여 요청 컨텍스트에 설정합니다.
* **`SimpleHttp.GetOSInfo()`**:
  * 현재 게임이 실행 중인 사용자 컴퓨터의 운영체제 빌드(Windows NT 리비전, Linux 아키텍처, macOS, Android, iOS 등)를 정규식(`Regex`)으로 안전하게 파싱 및 분석하여 표준 에이전트 문자열을 추출합니다.

```csharp
using System.Net.Http;
using JALib.Tools;

public async Task FetchLeaderboardAsync() {
    HttpClient myClient = new HttpClient();

    // 모드 식별 정보에 맞춰 User-Agent 헤더를 간단히 세팅합니다.
    myClient.SetupUserAgent("TestMod", "1.4.0");

    try {
        // 비동기로 문자열 결과 수령 (백그라운드에서 실행되므로 프레임 드랍이 없습니다)
        string jsonResponse = await myClient.GetString("https://api.modserver.com/scores");
        ParseAndApplyScores(jsonResponse);
    } catch (HttpRequestException e) {
        Main.Instance.LogReportException("웹 요청 실패", e);
    }
}
```

### 4.2. 저지연 TCP 소켓 패킷 클라이언트 (`JATcpClient`)

`JATcpClient`는 .NET 표준 `TcpClient`를 기반으로 모딩 환경의 핵심 동기화 요소들을 유기적으로 통합한 확장 클라이언트입니다. 게임 독립형 멀티 세션에 접속하여 지속성 패킷 통신을 정밀하게 처리할 수 있는 기반을 개설합니다.

#### 1) DNS SRV 레코드를 이용한 지능형 접속 연동
* 일반적인 IP/Port 지정 접속 외에도 **`Connect(string host, string service)`** 체인을 내장하여, 클라우드 플레어나 자체 DNS 서버에 기록된 SRV 서비스 도메인(`_service._tcp.host`)을 자동 질의(`DnsClient.LookupClient`)해 줍니다. 이 기능 덕분에 서버 측 포트가 수시로 변경되더라도 모드 바이너리를 수정할 필요 없이 유연한 백엔드 포트 스위칭이 가능합니다.
* 생성자나 커넥트 지점에 `autoConnect = true`를 선언하면 접속이 해제되거나 에러가 났을 때 독립 워커 백그라운드 스레드를 생성하여 **60초 주기로 자동 재접속(Auto Reconnect)을 수행**합니다. *(단, 유니티 메인 루프 굳음 현상을 방지하기 위해 메인 스레드 상에서의 autoConnect 루프 동작은 문법적으로 금지됩니다.)*

#### 2) 이벤트 및 상태 결합 속성
* **`SetConnectAction(JAction action)`**: 원격지 소켓 커넥션이 완전하게 수립 및 동기화된 지점에 가해질 연결 이벤트 콜백을 지정합니다.
* **`SetCloseAction(JAction action)`**: 서버 측에서 연결을 끊었거나 회선 문제로 접속이 단절되었을 때 이를 포착할 사후 처리 콜백 핸들러를 결합합니다. `read` 핸들러 루프가 도는 독립 스레드 종료 시점과 맞물려 완벽히 구동됩니다.

#### 3) 동기 및 비동기 스트림 입출력(I/O) API 명세
원시 네트워크 스트림을 직접 파싱하는 오버헤드를 막기 위해, 바이너리 규격에 대응하는 다량의 직통 데이터 읽기/쓰기 헬퍼를 지원합니다. 모든 Async 함수는 스레드를 방해하지 않는 테스크 파이프라인으로 동작합니다.

* **기본 자료형 읽기**: `ReadByte()`, `ReadInt()`, `ReadLong()`, `ReadFloat()`, `ReadBoolean()` 등
* **문자열 읽기 (`ReadUTF()`)**: [4바이트 크기 헤더 + UTF-8 문자열 바이트]로 정형화된 스트림 패킷을 파싱하여 리턴합니다.
* **원시 바이트 읽기 (`ReadBytes(int count, bool force = true)`)**: `force = true` 설정 시 지정된 패킷 바이트 목표치 크기(`count`)에 도달할 때까지 네트워크 스트림 내부에서 오프셋을 전진시키며 패킷 데이터를 누적하여 누수 없이 온전히 확보해 줍니다.
* **비동기 읽기 체인**: `ReadAsyncInt()`, `ReadAsyncUTF()`, `ReadAsyncBytes(count, force)`
* **동기/비비동기 쓰기 엔진**: `WriteInt(value)`, `WriteUTF(value)`, `WriteAsyncUTF(value)`, `WriteAsyncBytes(data)`

### 4.3. 실시간 웹소켓 연동 클라이언트 (`JAWebSocketClient`)

`JAWebSocketClient`는 웹 기반의 프로토콜 세션 및 디스코드 채널 웹훅 릴레이, 혹은 실시간 데이터 브로드캐스팅 노드와 양방향 이벤트를 무중단으로 교환하고자 할 때 사용하는 **고안정성 .NET `ClientWebSocket` 래퍼 컴포넌트**입니다.

#### 1) 비동기 상태 머신 (`IAsyncStateMachine`) 커넥션 수립
* 소켓 연결 시 내부적으로 비동기 전용 상태 머신 구조체인 `AsyncConnect` 세션을 초기화하여 작동합니다. 이 상태 머신 레이어는 커넥션 타임아웃이나 핸드셰이크 협상 단계가 완료될 때까지 비동기 작업 대기를 유지하다가, 연결이 확정되는 순간 모드에 이벤트 알림을 보내고 수신 독립 스레드를 즉시 포크(Fork)하여 깨웁니다.
* 네트워크 회선 단절 등으로 연결에 실패하더라도 `autoConnect = true` 구성 시 비동기 대기 타이머(`Task.Delay`)를 예약하여 상태 머신 메서드를 재진입(`MoveNext`) 시키는 방식으로 끊임없이 재접속을 시도합니다.

#### 2) 웹소켓 패킷 및 가변 메모리 스트림 파싱
웹소켓 프레임은 고정된 크기 단위로 데이터가 쪼개져 들어오는 가변 특징을 지닙니다. 이를 제어하기 위한 두 가지 형태의 데이터 리드 기법을 지원합니다.
* **`ReadBytes(int count, bool force = true)`**:
  * 소켓 어셈블리로부터 지정된 개수만큼의 버퍼 단위 데이터를 동기식으로 슬라이스하여 가져옵니다.
* **`ReadStream()` / `ReadBytes()` (전체 가변 패킷 일괄 병합)**:
  * 서버가 한 프레임에 전송한 데이터의 크기가 가변적이거나 매우 클 때 활용합니다. 내부적으로 **`MemoryStream`을 개설하고 256바이트 세그먼트 단위로 순환 스캔**을 수행하며 데이터 조각을 받아 모읍니다. 이후 최종 프레임 플래그인 **`result.EndOfMessage`**가 참(`true`)이 되는 즉시 스트림의 포지션을 초기화하여 온전한 하나의 패킷 덩어리로 안전하게 조립해 리턴합니다.

```csharp
using System;
using JALib.Tools;

public class MyWebSocketSession {
    private JAWebSocketClient _wsClient;

    public void Initialize() {
        // 읽기 루프 콜백 핸들러를 JAction 대리자로 감싸 소켓 생성자에 주입합니다.
        _wsClient = new JAWebSocketClient("wss://match.modserver.com/v1/connect", 
            onClickCallback: () => HandleIncomingPacket());
    }

    private void HandleIncomingPacket() {
        if (_wsClient.Connected) {
            // EndOfMessage 분기를 내부적으로 해결하여 조립된 온전한 패킷 바이트 배열을 추출합니다.
            byte[] completePacket = _wsClient.ReadBytes();
            ProcessMatchData(completePacket);
        }
    }
}
```

---

> 💡 **네트워크 개발 설계 요약**
> * 단순 웹 API 데이터 파싱, 토큰 인증, 데이터 업로드는 간편한 확장 기능이 부여된 **`SimpleHttp`**를 기반으로 처리하세요.
> * 전용 서버 환경과의 저지연 패킷 송수신은 **`JATcpClient`**를 구동하되, 인프라의 동적 변화에 유연하게 대응할 수 있도록 **SRV 도메인 연동 기능**을 적극 결합하는 것이 아키텍처 상 대단히 유리합니다.
> * 웹소켓 데이터 처리 시 크기가 가변적인 프레임 메시지는 스트림 누수를 방지하기 위해 **`EndOfMessage` 차단 메커니즘**이 내장된 **`ReadBytes()`**나 **`ReadStream()`** API를 사용하여 파싱하는 것이 안정성 면에서 정석입니다.

---

## 5. 파일 처리 및 압축 시스템

게임 모드가 비대해짐에 따라 리소스 팩, 에셋번들, 커스텀 사운드나 통계 아카이브 등 대용량 데이터를 관리해야 할 필요성이 커집니다. 유니티가 내부적으로 점유하는 에셋 파이프라인 외에, 디스크 공간을 효율적으로 활용하고 데이터 누수를 방지하기 위해 JALib은 **메모리 및 디스크 압축을 지원하는 `Zipper`** 시스템과 **OS 파일 점유 크래시를 방지하는 고안정성 `RawFile`** 엔진을 제공합니다.

### 5.1. 초고속 압축 및 해제 도구 (`Zipper`)

`Zipper` 클래스는 .NET 표준 `System.IO.Compression` 인프라를 확장하여, 게임 실행 중에 실시간으로 파일 또는 디렉터리를 `.zip` 파일로 아카이브하거나 압축을 해제(Unzip)하는 고성능 유틸리티입니다. 특히 디스크 입출력 없이 메모리 스트림 상에서 직접 압축 바이트를 가공하는 고도화된 스캔 기능도 함께 제공합니다.

#### 1) 핵심 API 명세
* **`Zipper.Zip(string sourceDirectory, string zipPath)`**
  * 지정한 소스 디렉터리 내의 모든 구조와 파일 스트림을 하나의 고압축 zip 파일로 아카이브합니다.
* **`Zipper.Unzip(string zipPath, string extractPath)`**
  * 디스크에 존재하는 zip 파일의 압축을 타깃 경로에 원본 폴더 트리 그대로 복원 및 동기화합니다.
* **`Zipper.Unzip(byte[] zipData, string extractPath)`**
  * 웹 서버 등으로부터 다운로드받은 **원시 바이트 배열(`byte[]`) 형태의 zip 데이터를 디스크에 임시 파일로 쓰지 않고, 메모리상에서 즉시 파싱하여 대상 디렉터리에 해제**해 주는 강력한 메모리 직통 API입니다.

```csharp
using JALib.Tools;

public void InstallDownloadedResourcePack(byte[] downloadedBytes) {
    string targetFolder = Path.Combine(Main.Instance.Path, "ResourcePacks");

    // 메모리 스트림 상에서 다운로드된 바이너리를 즉시 zip 해제하여 배치합니다.
    // 임시 파일을 만들고 지우는 불필요한 디스크 IO 및 권한 에러를 완벽히 우회합니다.
    Zipper.Unzip(downloadedBytes, targetFolder);
    
    Main.Instance.Log("리소스 팩 압축 해제 및 실시간 에셋 로드 완료!");
}
```

### 5.2. 고안정성 디스크 파일 입출력 (`RawFile`)

유니티가 백동작하는 환경에서 일반적인 C# `System.IO.File` 메서드를 무분별하게 사용하면, OS가 다른 프로세스에 의해 파일 권한을 임시 점유(Lock)하고 있거나 폴더 경로가 누락되었을 때 여지없이 `IOException`이나 `UnauthorizedAccessException` 크래시가 터지며 게임이 강제 종료됩니다.

`RawFile`은 JALib 프레임워크가 자랑하는 **방어적 파일 시스템 래퍼(Wrapper)**로, 입출력 도중 발생하는 모든 OS 레벨 예외를 격리 캡슐화하고 모드 전용 비동기 예외 안전망과 직접 연결합니다.

#### 1) 핵심 매커니즘 및 편의성
* **자동 디렉터리 빌드**: 파일을 쓰거나 생성하는 모든 메서드(`WriteAllBytes`, `WriteAllText` 등) 실행 시, **지정한 대상 경로의 부모 폴더(Directory)가 누락되어 있다면 예외를 터뜨리지 않고 프레임워크가 런타임에 폴더 트리를 자동으로 생성**한 뒤 파일 스트림을 전개합니다.
* **비동기 IO 결합 (`Task`)**: 대용량 파일 가공 시 게임이 끊기는 현상을 완벽히 방어할 수 있도록 `~Async` 계열의 비동기 스트림 파이프라인을 완전하게 제공합니다.

#### 2) 주요 API 명세
* **동기식 파일 가공**:
  * `RawFile.WriteAllBytes(string path, byte[] bytes)`
  * `RawFile.WriteAllText(string path, string contents)`
  * `RawFile.ReadAllBytes(string path)` / `RawFile.ReadAllText(string path)`
* **비동기식 파일 가공 (`JATask` 보호망 연동)**:
  * `RawFile.WriteAllBytesAsync(string path, byte[] bytes)`
  * `RawFile.WriteAllTextAsync(string path, string contents)`
  * `RawFile.ReadAllBytesAsync(string path)` / `RawFile.ReadAllTextAsync(string path)`

```csharp
using JALib.Data;
using JALib.Tools;

public void SaveModSettingsConfiguration(string jsonConfig) {
    string configPath = Path.Combine(Main.Instance.Path, "Config", "Settings.json");

    // JATask 비동기 풀 안에서 RawFile의 Async 필드를 결합하여 사용합니다.
    JATask.Run(this.Mod, async () => {
        // 'Config' 라는 하위 폴더가 디스크에 없더라도 RawFile이 자동으로 폴더를 생성하고 안전하게 씁니다.
        // 입출력 도중 락(Lock) 충돌 등의 에러가 발생해도 JAction/JATask 레이어에서 에러를 안전하게 catch합니다.
        await RawFile.WriteAllTextAsync(configPath, jsonConfig);
    });
}
```

---

> 💡 **파일/압축 시스템 설계 요약**
> * 외부 웹 세션으로부터 에셋 아카이브를 갱신할 때는 디스크에 잔여 찌꺼기를 남기지 않도록 **`Zipper.Unzip(byte[], path)`**의 메모리 직통 해제 API를 활용하세요.
> * 수시로 가동되는 데이터 로드 및 세이브 플로우에서는 표준 `System.IO` 문법을 지양하고, 경로 자동 빌더와 예외 격리 보호막이 부여된 **`RawFile`** 엔진을 적용하는 것이 사용자 컴퓨터 환경(샌드박스 보안, 디렉터리 권한 누락 등)에 구애받지 않는 고안정성 모드를 구축하는 지름길입니다.

---

## 6. 게임 모딩 전용 확장 유틸리티

유니티(Unity) 엔진 기반의 게임을 모딩할 때는 타사 모드들과의 로딩 컴파일 경합을 조율하거나, 게임 내부 매니저 객체들의 런타임 상태를 유저가 직관적으로 확인하고 제어할 수 있는 특수한 확장 유틸리티가 필수적입니다.

JALib은 현재 게임 세션 상태를 스캔하는 `ModTools`, 로딩 우선순위를 강제 보정하는 `ForceApplyMod`, 그리고 복잡한 세팅 데이터를 인게임 레이아웃 UI로 간단히 시각화하는 `SettingGUI` 컴포넌트를 지원합니다.

### 6.1. 인게임 분석 및 상태 관찰 (`ModTools`)

`ModTools` 클래스는 현재 구동 중인 게임의 전반적인 빌드 컨텍스트, 활성화된 현재 게임 스테이지 세션 세부 정보, 특정 핵심 인게임 매니저 객체들의 유효한 수명 주기 인스턴스 할당 상태 등을 런타임에 정밀 관찰 및 조율할 수 있는 모더 전용 편의 헬퍼 클래스입니다.

* **기능 및 가치**: 난독화된 게임 내부 리플렉션 필드를 한 번 더 캡슐화하여 제공하거나, 현재 활성화된 컨트롤러 객체가 메모리에 정상 할당되어 유효한 상태인지 분기 판단용 속성들을 포함합니다. 게임 엔진 내부 깊숙한 데이터에 직접 접근하는 파이프라인 역할을 수행합니다.

### 6.2. 런타임 강제 주입 시스템 (`ForceApplyMod`)

유니티 모드 매니저 환경에서는 여러 모드가 동시에 동일한 자원(Asset)이나 메서드를 가공하려고 시도할 때 로딩 순서 경합(Race Condition)이 발생하여 내 모드의 설정이 덮어씌워지거나 런타임에 누락될 수 있습니다. `ForceApplyMod`는 이러한 타이밍 이슈를 해결하기 위한 주입 보정 도구입니다.

* **동작 원리**: 어셈블리가 메모리에 완전히 고정되거나 타사 모드의 로딩 시퀀스가 끝난 직후, 내 모드가 소유한 독립 에셋이나 변조 패치 인젝션을 **런타임 강제 순위 최상단으로 강제 예약 및 오버라이드**하여 시스템 구조 내에 완벽하게 우회 주입되도록 유도합니다.

### 6.3. 통합 인게임 유저 세팅 창 빌더 (`SettingGUI`)

`SettingGUI`는 모드 내부 구조로 관리하는 복잡한 데이터 변수나 세팅 필드들을 일반 사용자가 게임을 켠 상태에서 직관적으로 조정할 수 있도록 도와주는 구성 도구입니다.

유니티의 레거시 OnGUI 렌더러 인터페이스 주기를 프레임워크 단에서 밀접하게 바인딩하여, 까다로운 레이아웃 드로우 연산 코드 없이 드롭다운 버튼, 토글 스위치, 슬라이더 바 등 정형화된 유저 컴포넌트 GUI 창을 간단히 디자인할 수 있습니다.

#### 1) SettingGUI 활용 예시
```csharp
using JALib.Tools;
using UnityEngine;

public class MyModSettingsMenu {
    private bool _enableFeature = true;
    private float _calibrationValue = 1.0f;

    // 유니티의 OnGUI 렌더링 주기 내부에서 결합되어 호출되는 지점입니다.
    public void DrawSettingsLayout() {
        GUILayout.BeginVertical("모드 상세 세부 설정", GUI.skin.window);

        // 1. 토글 컴포넌트를 간단히 그리며 실시간 실시간 변수 동기화 수행
        _enableFeature = SettingGUI.DrawToggle("고급 보정 기능 활성화", _enableFeature);

        GUILayout.Space(10);

        // 2. 수치형 슬라이더 컴포넌트 매핑 및 그리기
        GUILayout.Label($"현재 판정 오프셋 보정치: {_calibrationValue:F2}");
        _calibrationValue = SettingGUI.DrawSlider(_calibrationValue, 0.0f, 2.0f);

        GUILayout.EndVertical();
    }
}
```

---

> 💡 **모딩 도구 설계 요약**
> * 인게임 데이터 흐름이나 핵심 컨트롤러 인스턴스 검증이 필요할 때는 **`ModTools`** 내부 래퍼 속성을 경유하여 안전성을 확보하세요.
> * 로딩 순서 경합으로 인해 모드 기능이 간헐적으로 해제되는 현상이 관찰된다면 **`ForceApplyMod`** 인젝터를 적용하여 순위를 보정하는 것이 안전합니다.
> * 유저 편의성을 위한 실시간 인게임 메뉴 옵션을 레이아웃할 때는 무분별한GUILayout 배치 대신 **`SettingGUI`** 가 제안하는 컴포넌트 래핑 함수를 활용하는 것이 간결한 코드 유지보수 면에서 대단히 유리합니다.

---
## 7. 기타 편의성 도구

모드의 호환성과 데이터 무결성을 유지하고, C# 문법의 한계를 우회하여 메모리 연산 성능을 극한으로 끌어올리기 위한 시스템 제어 및 로우레벨 치트키 유틸리티 세트입니다.

### 7.1. 로우레벨 메모리 형변환 엔진 (`Unsafe` 일명 '언매니지드 치트키')

`Unsafe` 클래스는 C# 컴파일러가 안전성(Type Safety)을 이유로 차단하는 **강제 메모리 주소 매핑 및 박싱 해제 연산**을 로우레벨 IL 명령어로 직접 구현한 확장 메서드 집합입니다.

모든 메서드에는 **`aggressiveinlining` (무조건 인라인화)** 속성이 부여되어 있어, 함수 호출에 따른 오버헤드가 단 1프레임도 발생하지 않고 CPU 명령어 레벨에서 다이렉트로 전개됩니다.

#### 1) 핵심 API 명세 및 사용법
* **`object.AsUnsafe<T>()`** (참조 형식 강제 캐스팅)
  * 상속 관계가 전혀 없는 완전히 다른 두 클래스 객체 간의 메모리 참조 주소를 강제로 덮어씌워 형변환합니다. 일반적인 `as` 캐스팅과 달리 검증 과정을 거치지 않으므로 연산 속도가 무결합니다.
* **`TFrom.AsUnsafe<TFrom, TTo>()`** (값 형식 구조체 강제 캐스팅)
  * 동일한 메모리 크기(Byte Size)를 가진 서로 다른 두 값 형식 구조체(`ValueType`)의 비트 배열을 강제로 변환합니다. (예: `int` 데이터를 그대로 `float` 메모리 구조로 해석하여 리턴)
* **`object.UnboxUnsafe<T>()`** (초고속 언박싱 레퍼런스 취득)
  * `object`로 박싱된 값 형식 데이터를 일반적인 언박싱 오버헤드와 복사본 생성 없이, **물리적 메모리 데이터 자체의 관리형 포인터 참조(`ref T`)를 직접 반환**합니다.
* **`object.AsPointer()`** / **`object.AsUnsignedPointer()`**
  * 객체의 실제 힙(Heap) 메모리 관리 주소를 관리형 시스템이 제어할 수 있는 부호 있는/부호 없는 **원시 포인터 주소(`native int` / `IntPtr`)**로 즉시 치환하여 전달합니다.

```csharp
using JALib.Tools;

public void LowLevelMemoryHack() {
    object boxedScore = (int)5000;

    // 1. UnboxUnsafe를 이용해 복사 오버헤드 없이 박싱 데이터 내부의 raw 포인터 주소(&)를 참조합니다.
    ref int rawScore = ref boxedScore.UnboxUnsafe<int>();
    rawScore = 9999; // 참조를 직접 수정했으므로 boxedScore 내부의 값도 9999로 함께 변조됩니다.

    // 2. 객체의 순수 힙 메모리 원시 포인터 주소(IntPtr)를 간단히 획득합니다.
    nint objectAddress = scrController.instance.AsPointer();
}
```

### 7.2. 예측 불가능한 독립 시드 난수 생성기 (`JARandom`)

`JARandom`은 유니티 내부의 표준 `UnityEngine.Random`이나 .NET 표준 `System.Random`과 완전히 격리된 고유의 시드(Seed) 상태를 관리하는 고성능 난수 생성기입니다.

#### ⚠️ 모딩 환경에서 독립 난수가 필요한 이유
* **동기화 불일치(Desync) 방지**: 멀티플레이 세션이나 프레임 동기화가 중요한 모드 로직에서 다른 모드나 게임 본편이 `UnityEngine.Random.InitState()`를 흔들어버리면, 난수 테이블 순서가 꼬여 세션 간 동기화가 깨지는 현상이 발생합니다. `JARandom`은 전역 난수 상태를 오염시키지 않는 독립 인스턴스를 제공하여 이를 차단합니다.
* **예측 공격(Hack) 방어**: 시드가 외부에 노출되어 다음 판정(무작위 요소, 드롭 확률 등)을 예측하고 가로채는 클라이언트 변조 공격을 방어하기 위해 암호학적으로 견고한 의사 난수 스트림을 구성합니다.

```csharp
using JALib.Tools;

public class ModDropManager {
    // 모드만의 고유한 시드를 기반으로 격리된 난수 생성기 인스턴스를 개설합니다.
    private readonly JARandom _modRandom = new JARandom(1337);

    public bool RollDiceForBonusItem() {
        // 게임 본편의 UnityEngine.Random 상태와 무관하게 
        // 독립적으로 보장된 무작위 난수 백분율(0.0 ~ 1.0)을 계산합니다.
        float roll = _modRandom.NextFloat();
        return roll < 0.05f; // 5% 확률 판정
    }
}
```

### 7.3. 런타임 버전 체크 및 호환성 컨트롤러 (`VersionControl`)

`VersionControl`은 현재 유저 환경에 설치되어 구동 중인 게임 본편의 빌드 리비전 버전 번호와, 내 모드 어셈블리가 지원하는 호환성 규칙 범위를 런타임 진입 초입에 엄격하게 대조·검증하는 버전 제어 컴포넌트입니다.

#### ⚠️ 핵심 매커니즘 및 튕김 방지 (Crash Prevention)
* **런타임 호환성 화동 검사**: 대규모 게임 업데이트 시 데이터 구조나 함수 시그니처가 통째로 바뀌면, 구버전 모드 코드가 강제 주입되는 과정에서 심각한 크래시(튕김 현상)나 세이브파일 오염을 유발합니다.
* **안전장치(Safe Mode) 전환**: `[JAPatch]` 시스템의 `MinVersion` 및 `MaxVersion` 속성과 연동되어, 현재 게임의 릴리스 넘버가 모드의 허용 한계를 벗어났다고 판단되면 **프레임워크 단에서 해당 패치들의 인젝션을 자동으로 취소(Disable)하거나 모드를 안전 모드로 격리 진입**시켜 게임이 비정상 종료되는 상황을 사전에 완벽하게 차단합니다.

```csharp
using JALib.Tools;

public class MyModEntry {
    public static void Initialize() {
        // 현재 가동된 게임 빌드 리비전 번호를 확인합니다.
        int currentGameVersion = VersionControl.ReleaseNumber;

        // 예시: 특정 리비전 미달 버전에서 실행 시 경고를 띄우고 안전 조치를 취합니다.
        if(currentGameVersion < 500) {
            Main.Instance.Warning("지원 종료 대상인 구버전 게임 환경이 감지되었습니다. 일부 하모니 패치가 안전을 위해 비활성화됩니다.");
        }
    }
}
```

---

> 💡 **편의성 도구 설계 요약**
> * 형식 안전성 비용 및 복사 연산 비용을 극한으로 줄여서 메모리를 가공하려면 `try-catch` 스레드 보호막 내에서 **`Unsafe`**의 포인터 및 인라인화 메서드를 결합해 제어하세요.
> * 다른 모드의 난수 테이블 간섭으로 인한 멀티플레이 싱크 단절을 막으려면 독립형 **`JARandom`** 객체를 지목하여 제어하는 것이 옳습니다.
> * 패치 인젝션 실패에 따른 강제 튕김 현상을 유연하게 조율 및 선제 제어하려면 **`VersionControl`**을 모듈 초입에 동기화해 두는 것이 완벽합니다.
