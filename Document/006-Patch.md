# JAMod 개발 가이드 - 패치

## [목차로 이동](000-DevelopGuide.md)
1. [패치 메서드 생성 및 구조](#1-%ED%8C%A8%EC%B9%98-%EB%A9%94%EC%84%9C%EB%93%9C-%EC%83%9D%EC%84%B1-%EB%B0%8F-%EA%B5%AC%EC%A1%B0)
2. [Prefix](#2-prefix)
3. [Postfix](#3-postfix)
4. [Transpiler](#4-transpiler)
5. [Finalizer](#5-finalizer)
6. [Replace](#6-replace)
7. [ReversePatch](#7-reversepatch)
8. [OverridePatch](#8-overridepatch)
9. [JAPatcher 고급 제어 및 이벤트](#9-japatcher-%EA%B3%A0%EA%B8%89-%EC%A0%9C%EC%96%B4-%EB%B0%8F-%EC%9D%B4%EB%B2%A4%ED%8A%B8)

---

## 1. 패치 메서드 생성 및 구조

JALib의 패치 시스템은 게임이 실행 중인 메모리 어셈블리에 실시간으로 개입하여 코드를 가공하거나 가로채는 강력한 기능을 제공합니다.

모든 패치 로직은 프레임워크가 런타임에 안정적으로 탐색하고 바인딩할 수 있도록 정해진 구조적 규칙을 엄격하게 준수해야 합니다.

기본적으로 [Harmony](https://harmony.pardeike.net/index.html)에 패치를 의존하기 때문에 더 자세한 내용이
궁금하다면 [Harmony 사이트](https://harmony.pardeike.net/index.html)에서 확인해주세요.

### 1.1. 패치 메서드의 필수 선언 규칙

패치 대상이 되는 메서드는 반드시 다음 조건을 충족해야 합니다.

* **`static` 선언 필수**: 인스턴스 메서드는 하모니 인젝션 타깃으로 직접 지정할 수 없습니다. 반드시 정적(`static`) 메서드로 선언해야 합니다.
* **어트리뷰트(Attribute) 선언**: 해당 메서드가 어떤 게임 클래스의 어떤 함수를, 어떤 타이밍에 변조할 것인지 목적에 맞는 어트리뷰트(`[JAPatch]`, `[JAOverridePatch]`,
  `[JAReversePatch]`)를 명시해야 합니다.

```csharp
// 정석적인 패치 메서드 선언 구조
[JAPatch(typeof(scnGame), "Play", PatchType.Postfix, false)]
private static void OnGameStart() {
    // 게임 시작 직후 가해질 모드 로직
}
```

### 1.2. 다중 타깃 패치 (Multiple Patch Targets)

`[JAPatch]`와 `[JAOverridePatch]` 어트리뷰트는 `AllowMultiple = true`가 설정되어 있어, 단일 패치 바디 메서드에 **여러 개의 어트리뷰트를 중복하여 부착하는 다중 타깃 패치
**를 완전히 지원합니다. 이를 통해 서로 다른 게임 이벤트가 호출될 때 중복 코드 없이 동일한 모드 로직을 실행하도록 묶을 수 있습니다.

```csharp
[JAPatch(typeof(scnGame), "Play", PatchType.Postfix, false)]
[JAPatch(typeof(scrPressToStart), "ShowText", PatchType.Postfix, false)]
private static void OnCommonGameEvent() {
    // 두 메서드가 호출되었을 때 중복 코드 없이 이 단 하나의 메서드가 공통으로 실행됩니다.
}
```

### 1.3. 패치 어트리뷰트별 생성자 상세 사양

JALib은 용도에 따라 3가지 메인 패치 어트리뷰트를 지원하며, 각각 문자열 탐색형과 타입 지목형 생성자를 기본적으로 제공합니다.

#### ① [JAPatch] (일반 패치용)

가장 보편적으로 사용하는 패치 어트리뷰트입니다.

* `public JAPatchAttribute(Type classType, string methodName, PatchType patchType, bool disable)`
* `public JAPatchAttribute(string className, string methodName, PatchType patchType, bool disable)`
  *(참고: `MethodBase`나 `Delegate`를 직접 주입받는 생성자는 런타임 동적 등록 전용이므로 어트리뷰트 구문 문법으로는 사용할 수 없습니다.)*

#### ② [JAOverridePatch] (비가상 메서드 오버라이드 제어용)

virtual이 선언되지 않은 게임 내 상속 메서드 체인을 재정의할 때 사용합니다. 클래스명만 지정하거나 메서드명까지 지정하는 등 유연한 생성자를 지원합니다.

* `public JAOverridePatchAttribute(Type classType, string methodName)` /
  `public JAOverridePatchAttribute(string className, string methodName)`
* `public JAOverridePatchAttribute(Type classType)` / `public JAOverridePatchAttribute(string className)`
* `public JAOverridePatchAttribute()` // 매개변수 없는 기본 생성자 지원

#### ③ [JAReversePatch] (게임 로직 역복사 호출용)

게임 원본 코드를 모드의 비어있는 스텁 함수로 복사해 올 때 사용합니다.

* `public JAReversePatchAttribute(Type classType, string methodName, ReversePatchType patchType)`
* `public JAReversePatchAttribute(string className, string methodName, ReversePatchType patchType)`

### 1.4. 패치 순서 제어 및 세부 튜닝 필드 (최신 추가 사양)

`[JAPatch]` 시스템에서 타사 모드 또는 프레임워크 내 다른 패치들과의 실행 우선순위를 정밀하게 조율하기 위해 추가된 최신 사양 필드입니다.

* **`Priority`** (`int`, 기본값: `-1`)
    * 패치의 실행 우선순위를 숫자로 직접 지정합니다. 값이 높을수록 먼저 실행되거나 하모니 패치 체인의 우선권을 잡습니다.
* **`Before`** (`string[]`)
    * 여기에 명시된 특정 모드 ID 또는 패치 식별자보다 **먼저** 이 패치가 실행되도록 순서를 강제합니다.
* **`After`** (`string[]`)
    * 여기에 명시된 특정 모드 ID 또는 패치 식별자가 실행된 **직후에** 이 패치가 끼어들도록 순서를 구성합니다.

---

### 1.5. 오버라이드 패치 전용 특수 필드 (`[JAOverridePatch]`)

`[JAOverridePatch]` 선언 시 가상 우회 체인을 정밀하게 커스텀하기 위해 정의할 수 있는 전용 옵션입니다.

* **`IgnoreBasePatch`** (`bool`, 기본값: `true`)
    * 부모 클래스에 걸려있는 기존 패치 레이어들을 무시하고 현재 상속 타깃의 순수 오버라이드 로직에 집중할지 여부입니다.
* **`targetType`** (`Type`) / **`targetTypeName`** (`string`)
    * 오버라이드 패치를 정확하게 적용할 구체적인 하위 대상 클래스 타입을 명시적으로 지정합니다.
* **`checkType`** (`bool`, 기본값: `true`)
    * 실행 시점에 대상 하위 인스턴스의 실제 런타임 타입을 엄격하게 대조하여 패치 분기를 실행할지 결정합니다.

### 1.6. 공통 기반 필드 및 정밀 식별 옵션 (`JAPatchBaseAttribute`)

모든 패치 어트리뷰트가 공통적으로 상속받아 사용하는 최상위 옵션들로, 게임 버전 불일치나 난독화 오버로딩 환경을 완벽하게 제어합니다.

* **`MinVersion` / `MaxVersion`** (`int`, 기본값: `VersionControl.releaseNumber`)
    * 패치가 활성화될 수 있는 대상 게임의 최소/최대 리비전 버전 번호 제한입니다. 기본적으로 현재 프로젝트의 릴리스 넘버와 동기화되나, 직접 범위를 지정하여 게임 업데이트 시 패치가 자동 차단되도록 안전장치를
      구성할 수 있습니다.
* **`ArgumentTypes`** (`string[]`) / **`ArgumentTypesType`** (`Type[]`)
    * 오버로드된 원본 메서드 중 내가 원하는 대상을 명확히 식별하기 위해, 대상 매개변수들의 타입 이름(문자열 배열) 또는 리플렉션 `Type` 배열을 정의합니다. (예:
      `ArgumentTypesType = [typeof(int), typeof(bool)]`)
* **`GenericName`** (`string[]`) / **`GenericType`** (`Type[]`)
    * 패치하려는 대상 원본 메서드가 제네릭 함수 형식일 때, 매핑할 제네릭 인자 명칭과 바인딩할 타입 배열을 지정합니다.
* **`TryingCatch`** (`bool`, 기본값: `true`)
    * 패치 실행 도중 예외가 발생하더라도 게임 크래시를 방지하고 JALib 단에서 안전하게 Catch하여 로그 세션으로 넘길지 여부입니다.
* **`TryCatchChildren`** (`bool`, `[JAReversePatch]` 전용, 기본값: `true`)
    * 역패치되어 가져온 하부 자식 노드 메서드 스트림 실행 중 발생하는 예외까지 안전하게 캡슐화하여 처리할지 지정합니다.
* **`Debug`** (`bool`, 기본값: `false`)
    * `true` 설정 시, 해당 패치가 인젝션되는 하모니 내부 상태와 로우레벨 IL 로그 출력을 활성화하여 상세 디버깅 모드로 진입합니다.

### 1.7. 패치 등록 및 실행 방법 (Adding & Applying Patches)

`[JAPatch]` 또는 `[JAOverridePatch]` 등으로 어트리뷰트 선언을 마쳤거나, 런타임 동적 패치를 구성했다면 프레임워크의 패치 관리 도구인 **`Patcher`** 인스턴스를 통해 패치셋에
등록하고 최종적으로 게임 엔진 어셈블리에 주입해야 합니다. JALib은 개발 스타일과 상황에 맞춰 총 5가지의 유연한 패치 등록 방식을 제공합니다.

#### 방식 ①: 클래스 단위 일괄 자동 등록

클래스 내부에 정의된 모든 메서드를 전수 조사하여, `[JAPatch]` 시리즈 어트리뷰트가 붙은 모든 패치 메서드를 한 번에 패처에 자동으로 긁어모아 추가합니다.

```csharp
// MyPatches 클래스 내부에 선언된 모든 패치 어트리뷰트 메서드가 일괄 등록됩니다.
Patcher.AddPatch(typeof(MyPatches));
```

#### 방식 ②: MethodInfo 지목 등록

특정 클래스 내에서 단 하나의 패치 메서드만 콕 집어 명시적으로 등록하고 싶을 때 리플렉션 확장 메서드인 `.Method()`를 결합하여 추가합니다.

```csharp
Patcher.AddPatch(typeof(MyPatches).Method("OnGameStart"));
```

#### 방식 ③: 대리자(Delegate) 지목 등록

문자열 이름 대신 정적 메서드의 함수 포인터(대리자)를 직접 전달하여 컴파일 타임에 오타를 원천 차단하며 명시적으로 추가합니다.

```csharp
Patcher.AddPatch(MyPatches.OnGameStart);
```

#### 방식 ④: 어트리뷰트 없는 메서드에 동적 어트리뷰트 주입 등록

코드 바디 메서드 위에 `[JAPatch]` 어트리뷰트를 직접 물리적으로 적지 않았더라도, 런타임에 동적으로 속성을 할당하여 특정 게임 코드를 타겟팅하도록 강제 매핑할 수 있습니다.

```csharp
// 생성자 초기화 방식을 이용한 정밀 추가
Patcher.AddPatch(new JAPatchAttribute(typeof(scnGame), "Play", PatchType.Postfix, false) {
    Method = typeof(MyPatches).Method("OnGameStart")
});

// 더 직관적이고 간결한 파라미터 오버로드 형태
Patcher.AddPatch(
    typeof(MyPatches).Method("OnGameStart"),
    new JAPatchAttribute(typeof(scnGame), "Play", PatchType.Postfix, false)
);
```

#### 방식 ⑤: 대리자(Delegate) 기반 동적 어트리뷰트 주입 등록

가장 직관적이고 가독성이 높은 방식으로, 대리자 포인터와 동적 패치 속성을 결합하여 바인딩합니다.

```csharp
Patcher.AddPatch(
    MyPatches.OnGameStart,
    new JAPatchAttribute(typeof(scnGame), "Play", PatchType.Postfix, false)
);
```

### 1.8. 패치 최종 실행 및 컴인젝션

위의 `AddPatch` 함수들을 통해 원하는 패치셋을 모두 선언 및 적재했다면, **반드시 수명 주기 이벤트 내에서 최종적으로 `Patch()` 메서드를 명시적으로 실행**해주어야 물리적인 코드 변조가 비로소
완성됩니다.

```csharp
// 누적적재된 모든 하모니 및 오버라이드 패치셋을 게임 런타임 메모리에 물리적으로 주입(인젝션)합니다.
Patcher.Patch();
```

> 💡 **안정적인 아키텍처 워크플로우 팁**
>
> `OnSetup()` 타이밍에 `Patcher.AddPatch(typeof(내패치클래스))`를 통해 패치할 목록들을 모두 예약 및 수집해 둔 뒤, `OnSetup`이 끝나는 시점이나 메인 스레드가 완전히 확보되는
`OnEnable()` 내부에서 `Patcher.Patch()`를 호출해 주는 것이 JALib 프레임워크가 권장하는 가장 안전하고 정석적인 패치 처리 방식입니다.
>
>  기본적으로 JAMod나 Feature, MultiFeature에 존재하는 JAPatcher는 따로 Patch()를 호출할 필요가 없습니다.

---

## 2. Prefix

`Prefix`는 지정한 원본 메서드가 실행되기 직전에 가장 먼저 인젝션되어 구동되는 패치 타입입니다. 원본 메서드로 들어오는 매개변수를 사전에 필터링하거나, 특정 조건에서 원래 메서드가 실행되지 않도록 차단하는 등
강력한 실행 흐름 제어권을 가집니다.

### 2.1. 작동 원리 모사

Prefix 패치가 원본 메서드 안에서 어떻게 작동하는지 가상 코드로 표현하면 다음과 같습니다.

```csharp
public void OriginalMethod() {
    // 원본 코드가 실행되기 전에 Prefix 패치가 가장 먼저 실행됩니다.
    // 만약 Prefix 패치 함수가 false를 반환하면 아래의 OriginalCode()는 통째로 건너뜁니다.
    if(!PatchPrefixMethod()) return;

    OriginalCode();
}
```

### 2.2. 핵심 특징 및 반환값 활용 규칙

Prefix 패치 메서드는 반환 타입에 따라 원본 메서드의 운명을 다르게 제어할 수 있습니다. 반환값의 종류는 크게 `void`와 `bool`로 나뉩니다.

#### ① `void` 반환형

* **동작**: 단순 가로채기 모드입니다. 원본 메서드가 실행되기 전에 무조건 실행되며, 원본 메서드의 실행 흐름을 절대 방해하거나 중단시키지 않습니다.
* **용도**: 게임 핵심 함수가 실행되기 전의 상태 로깅, 단순 카운터 증가, 필드 변수 사전 관찰 등에 적합합니다.

#### ② `bool` 반환형 (흐름 제어 / Skip 기능)

* **`true` 반환**: Prefix 로직을 실행한 뒤, 원래 게임의 원본 메서드를 정상적으로 이어서 실행합니다.
* **`false` 반환**: **원본 메서드의 실행을 완전히 취소(Skip)**시키고 즉시 리턴하게 만듭니다. 만약 해당 원본 메서드에 연결된 다른 타사의 Prefix 패치들이 더 존재하더라도, 내 패치에서
  `false`를 리턴하는 순간 이후의 Prefix들과 원본 본문 코드가 모두 생략됩니다. *(단, Postfix 패치는 원본 실행 여부와 무관하게 정상 호출됩니다.)*

### 2.3. 매개변수 가로채기 및 변조 규칙

Prefix 패치 메서드는 원본 메서드가 가지고 있는 인자(Arguments)와 객체 내부 필드를 자유롭게 받아와 조작할 수 있는 특수 파라미터 네임 매핑 규칙을 제공합니다.

* **매개변수 이름 일치**: 원본 메서드가 받는 매개변수와 **완전히 동일한 이름과 타입**으로 패치 메서드의 파라미터를 선언하면 해당 값이 자동으로 바인딩됩니다.
* **`ref` 키워드를 통한 인자값 수정**: 매개변수 앞에 `ref` 키워드를 붙여서 받아오면, Prefix 내부에서 그 값을 수정했을 때 **수정된 값이 원본 메서드의 매개변수로 그대로 전달**됩니다.
* **`__instance`**: 현재 이 메서드를 실행하고 있는 클래스의 인스턴스(객체 자신)를 가져옵니다.
* **`___fieldName` (언더바 3개)**: 원본 객체 내부에 숨겨진 `private` 전역 변수나 필드를 리플렉션 없이 다이렉트로 가져오며, `ref`로 받으면 실시간 변조가 가능합니다.

### 2.4. Prefix 구현 예시

#### 예시 ①: 단순 필드 변조 및 원본 실행 (`true` 반환)

게임의 오토플레이 속도를 가로채 강제로 고정하고 원본 Awake 함수를 마저 실행하는 구조입니다.

```csharp
[JAPatch(typeof(scrController), "Awake", PatchType.Prefix, false)]
private static bool ControllerAwakePrefix(ref float ___autoplaySpeed) {
    // 원본 클래스의 private 필드인 autoplaySpeed를 가져와 2배속으로 변조합니다.
    ___autoplaySpeed = 2.0f;

    // true를 반환하므로 원래 게임의 Awake 코드가 변조된 속도 값을 가지고 이어서 실행됩니다.
    return true; 
}
```

#### 예시 ②: 특정 조건에서 원본 실행 차단 (`bool` 조건부 Skip)

플레이어가 '오토 모드'를 켜두었거나 특정 타일 상태일 때, 판정 매니저가 점수를 추가하는 원래 로직을 아예 실행하지 못하도록 스킵하는 구조입니다.

```csharp
[JAPatch(typeof(scrHitManager), "AddScore", PatchType.Prefix, false)]
private static bool SkipScoreOnAutoMode(int rawScore, ref int ___comboCount) {
    // 만약 오토플레이 보정이 실행 중인 조건이라면
    if(RDC.auto || scrController.instance.currFloor.nextfloor?.auto == true) {
        // 원래 실행되어야 할 점수 추가 및 콤보 로직을 완전히 무효화(Skip)합니다.
        return false;
    }

    // 일반 플레이 상황이면 점수를 100점 추가 보정하고 원래 함수를 실행합니다.
    return true;
}
```

### 2.5. 고급: 원본 메서드가 반환값이 있을 때의 Prefix 제어 (`__result`)

만약 원본 메서드가 `public string GetPlayerName()`과 같이 반환값(Return Value)이 존재하는 함수인데 Prefix에서 `false`를 리턴하여 원본을 스킵하려고 한다면, *
*`ref [반환타입] __result`** 매개변수를 사용하여 **원본 메서드가 반환해야 할 가짜 결과값을 내가 직접 채워주어야 합니다.**

```csharp
[JAPatch(typeof(scrMisc), "GetClearTitle", PatchType.Prefix, false)]
private static bool CustomClearTitlePrefix(ref string __result) {
    if(MyFeature.Enabled) {
        // 원본 메서드를 실행하지 않고, 내가 원하는 문자열을 최종 결과값으로 대입합니다.
        __result = "JAMod Custom Clear!";

        // false를 반환하여 원래 게임의 GetClearTitle 내부 코드는 실행되지 않도록 차단합니다.
        return false; 
    }
    return true;
}
```
---

> 💡 **추가 참고 정보**
>
> Prefix 패치는 원본 로직을 완전히 제어할 수 있다는 강력한 장점이 있지만, `false`를 반환하여 원본을 자주 스킵하게 되면 동일한 메서드를 패치하는 타사 모드들과 심각한 충돌을 일으킬 수 있습니다.
> 따라서 단순 변수 관찰이나 결과값 변조가 목적이라면 Prefix 대신 `Postfix` 타입을 사용하는 것이 아키텍처 상 훨씬 안전합니다.
>
> 보다 깊이 있는 하모니 하부 엔진의 패치 명세가 궁금하다면
> 공식 [Harmony Prefix 가이드 문서](https://harmony.pardeike.net/articles/patching-prefix.html)를 함께 참고해 주세요.

---

## 3. Postfix

`Postfix`는 지정한 원본 메서드가 에러 없이 안전하게 실행을 완료하고 마친 직후에 실행되는 패치 타입입니다. 원본 메서드의 실행 결과물(반환값)을 검사하여 최종적으로 위조하거나, 원본 메서드가 끝난 직후
연쇄적으로 다른 작업을 트리거해야 할 때 가장 유용하고 안전하게 활용됩니다.

### 3.1. 작동 원리 모사

Postfix 패치가 원본 메서드 안에서 어떻게 작동하는지 가상 코드로 표현하면 다음과 같습니다.

```csharp
public void OriginalMethod() {
    OriginalCode(); // 원래 게임의 코드가 완전히 끝난 직후에

    PatchPostfixMethod(); // Postfix 패치가 실행됩니다.
}
```

### 3.2. 핵심 특징 및 안전성 가이드라인

* **높은 안전성 (가장 추천됨)**: 원본 메서드가 실행되는 도중의 제어 흐름을 절대 방해하거나 가로막지 않으므로, 모드 간의 충돌 가능성이 가장 낮고 프레임워크 아키텍처 상 가장 권장되는 패치 형태입니다.
* **원본 실행 여부와 무관한 호출**: 다른 모드나 Prefix 패치에서 `false`를 반환하여 원본 메서드(OriginalCode)를 본래 실행하지 않고 건너뛰었더라도, Postfix 패치는 영향을 받지 않고
  무조껀 정상 호출됩니다.
* **반환 타입 제약**: Postfix 패치 메서드 자체의 반환 타입은 항상 **`void`**여야 하거나, 원본 메서드의 반환 타입과 완벽히 일치하는 형태를 가져야 합니다. 일반적으로는 `void`로 선언하여
  사용합니다.

### 3.3. 반환값 및 매개변수 제어 규칙 (`__result`)

Postfix 패치는 원본 메서드가 끝난 시점의 상태를 다루기 때문에 다음과 같은 강력한 특수 파라미터 매핑 규칙을 제공합니다.

#### ① `__result`를 통한 반환값 가로채기 및 최종 위조

원본 메서드가 반환값(Return Value)을 가지는 함수일 경우, 패치 메서드의 인자에 **`__result`**라는 이름으로 매개변수를 선언하면 원본이 리턴한 최종 값을 확보할 수 있습니다.

* **값 읽기**: `[반환타입] __result` 형태로 받으면 원본의 결과값을 검사할 수 있습니다.
* **값 위조**: **`ref [반환타입] __result`** 형태로 `ref` 키워드를 붙여 받아오면, Postfix 내부에서 이 값을 수정했을 때 **최종적으로 게임 엔진이 수령하는 반환값 자체가 내가
  위조한 값으로 바뀌게 됩니다.**

#### ② 인자 및 인스턴스 참조

* **매개변수 일치**: 원본 메서드가 전달받았던 매개변수 명칭과 타입을 그대로 패치 메서드 인자에 적으면, 원본 실행 당시 사용된 파라미터 값을 그대로 읽어올 수 있습니다.
* **`__instance`**: 현재 이 메서드를 실행한 원본 클래스 객체의 인스턴스 자신입니다.
* **`___fieldName` (언더바 3개)**: 원본 객체 내부에 은닉된 `private` 필드 데이터를 가져오며, `ref`로 명시할 시 실시간 수정이 가능합니다.

### 3.4. Postfix 구현 예시

#### 예시 ①: 단순 연쇄 작업 트리거 및 인자 관찰 (`void` 반환)

게임의 상태가 변경 완료된 직후, 바뀐 상태를 로그에 기록하고 콤보 카운터를 초기화하는 연쇄 작업 구조입니다.

```csharp
[JAPatch(typeof(StateBehaviour), "ChangeState", PatchType.Postfix, false)]
private static void OnChangeStatePostfix(Enum newState) {
    // 원본 메서드가 사용했던 인자(newState)를 그대로 읽어와 분기 처리합니다.
    if((States) newState == States.Start) {
        // 게임이 시작 상태로 넘어갔으므로 커스텀 타이밍 리스트를 초기화합니다.
        Main.Instance.Log("게임이 시작되었습니다. 데이터 기록 세션을 개설합니다.");
        MyFeature.Timings.Clear();
    }
}
```

#### 예시 ②: ref __result를 이용한 안전한 데이터 보정 및 복구 기능

유저가 인게임 UI 설정을 변경하여 팅길 수 있는 잘못된 수치(예: 음수 크기 등)를 입력하고 리턴할 때, 안전장치 데이터 필터링을 거쳐 시스템 안정성을 유지하는 구조입니다.

```csharp
[JAPatch(typeof(CustomLayoutManager), "GetUIElementScale", PatchType.Postfix, false)]
private static void GetUIElementScalePostfix(float baseScale, ref float __resultRef) {
    // 원본 시스템이 반환한 UI 크기 수치(__resultRef)가 유효 범위를 벗어났는지 검사합니다.
    if(__resultRef <= 0f) {
        // 오류 레이아웃 방지를 위해 시스템 안전 기본값(1.0f)으로 최종 보정하여 반환합니다.
        __resultRef = 1.0f;
        Main.Instance.Warning("잘못된 UI 스케일 감지: 안전 기본값(1.0)으로 자동 복구되었습니다.");
    }
}
```

---

> 💡 **추가 참고 정보**
>
> Postfix는 게임의 상태나 변수를 안전하게 관찰하고 마지막 결과만 살짝 위조하기에 최적인 도구입니다. 그러나 원본 메서드 자체가 아예 실행되지 않아야 하는 환경(예: 특정 UI 팝업 차단)을 설계해야 한다면
> Postfix 대신 `Prefix`에서 `false`를 리턴하는 구조를 채택해야 합니다.
>
> 보다 깊이 있는 하모니 하부 엔진의 패치 명세가 궁금하다면
> 공식 [Harmony Postfix 가이드 문서](https://harmony.pardeike.net/articles/patching-postfix.html)를 함께 참고해 주세요.

---

## 4. Transpiler

`Transpiler`는 원본 메서드의 C# 소스 코드 레이어가 아닌, 컴파일러가 생성한 내부의 **IL(Intermediate Language) 어셈블리 명령코드(OpCode) 스트림 자체를 실시간으로
가공하여 코드를 완전히 수정, 삽입, 혹은 삭제**하는 고성능 로우레벨 패치 타입입니다.

### 4.1. 작동 원리 모사

Transpiler 패치가 원본 메서드의 명령 구조를 어떻게 가공하는지 가상 코드로 표현하면 다음과 같습니다.

```csharp
public void OriginalMethod() {
    OriginalCode1();
    OriginalCode2();
    // Transpiler가 원본의 특정 명령 스트림(OriginalCode3)을 탐색하여 지우거나 가로채고,
    // 개발자가 원하는 커스텀 명령 코드(CreatedCode)로 조립하여 완전히 대체합니다.
    // [OriginalCode3(); 무효화]
    CreatedCode();
    OriginalCode4();
}
```

### 4.2. 핵심 특징 및 사용 이유

* **극단적인 고성능 및 정밀성**: 원본 메서드 전체를 덮어쓰는 `Replace`와 달리, 수백 줄짜리 게임 로직 함수 중간의 단 한 줄, 혹은 특정 연산자(예: 값 비교, 수치 계산 등)만 콕 집어서 바꿀 수
  있습니다. 이 덕분에 메모리 오버헤드가 매우 적고 타사 모드와의 병합 안정성이 높습니다.
* **IL 어셈블리 지식 요구**: Transpiler는 C# 문법이 아닌 닷넷 가상 머신이 이해하는 명령 어셈블리 단위(`OpCodes`)를 루프 돌며 제어하므로, 스택 기반의 IL 구조에 대한 이해가
  필수적입니다.
* **반환 타입 제약**: Transpiler 패치 메서드의 반환 타입은 무조건 **`IEnumerable<CodeInstruction>`**이어야 하며, 첫 번째 매개변수로 원본의 IL 스트림인
  `IEnumerable<CodeInstruction> instructions`를 주입받아야 합니다.

### 4.3. 주요 OpCode 및 CodeInstruction 조작 규칙

하모니 패치 엔진 내에서 한 줄의 IL 명령은 `CodeInstruction` 객체로 표현됩니다. 이 객체는 크게 두 가지 핵심 필드를 다룹니다.

1. **`opcode`** (`OpCode`): 수행할 연산의 종류입니다. (예: `OpCodes.Ldc_R4` - float 배치, `OpCodes.Call` - 함수 호출 등)
2. **`operand`** (`object`): 연산의 대상이 되는 상수 값, 필드 정보, 또는 메서드 정보(`MethodInfo`)입니다.

### 4.4. Transpiler 구현 예시

#### 예시 ①: 특정 상수(Literal) 값 탐색 및 실시간 변조

게임의 특정 계산식 내부에서 사용되는 고정 상수 수치(예: 100.0f)를 찾아 모드 설정에 맞춰 다른 수치(50.0f)로 안전하게 가공하는 구조입니다.

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using JALib.Core.Patch;

[JAPatch(typeof(scrController), "Update", PatchType.Transpiler, false)]
private static IEnumerable<CodeInstruction> UpdateTranspiler(IEnumerable<CodeInstruction> instructions) {
    // 원본 IL 명령셋을 제어하기 쉽게 리스트로 복사합니다.
    List<CodeInstruction> codes = new List<CodeInstruction>(instructions);

    // 명령 스트림을 하나씩 순회하며 탐색합니다.
    for(int i = 0; i < codes.Count; i++) {
        // 1. float 상수를 로드하는 명령(Ldc_R4)인지 확인하고
        // 2. 그 로드하려는 원본 값이 정확히 100f인지 대조합니다.
        if(codes[i].opcode == OpCodes.Ldc_R4 && (float) codes[i].operand == 100f) {
            // 조건을 만족하는 타깃 OpCode의 피연산자(operand)를 50f로 교체합니다.
            codes[i].operand = 50f;
            BetterCalibrationMod.Instance.Log("Transpiler 성공: 특정 연산 상수(100 -> 50) 보정 완료");
            break; // 원하는 지점을 수정했다면 루프를 탈출합니다.
        }
    }
    
    // 수정이 완료된 최종 IL 스트림 열거형을 반환합니다.
    return codes.AsEnumerable();
}
```

#### 예시 ②: 특정 메서드 호출(Call) 차단 또는 우회

게임 시스템이 화면을 강제로 흔들거나 특정 이벤트를 호출하는 코드를 중간에 만나면, 이를 건너뛰거나 아무 일도 안 하는 무효 명령(`OpCodes.Nop`)으로 지워버리는 구조입니다.

```csharp
[JAPatch(typeof(scrEffectManager), "TriggerEffects", PatchType.Transpiler, false)]
private static IEnumerable<CodeInstruction> DisableScreenShakeTranspiler(IEnumerable<CodeInstruction> instructions) {
var codes = new List<CodeInstruction>(instructions);

    for (int i = 0; i < codes.Count; i++) {
        // 함수 호출(Call 또는 Callvirt) 명령 중, 대상 메서드 이름이 "ScreenShake"인 지점을 검색합니다.
        if ((codes[i].opcode == OpCodes.Call || codes[i].opcode == OpCodes.Callvirt) && 
            codes[i].operand is MethodInfo method && method.Name == "ScreenShake") {
            
            // 해당 함수를 호출하기 위해 스택에 인자값들을 쌓았던 앞선 opcode들의 컨텍스트를 고려하여,
            // 호출 지점 자체를 Nop(No Operation - 아무것도 하지 않음) 명령으로 덮어써서 함수 실행을 무효화합니다.
            codes[i].opcode = OpCodes.Nop;
            codes[i].operand = null;
            BetterCalibrationMod.Instance.Log("Transpiler 성공: 화면 흔들림 오리지널 메서드 호출 스트림 차단");
        }
    }
    return codes.AsEnumerable();
}
```

### 4.5. Transpiler 사용 시 주의 사항 (Best Practices)

1. **난독화 및 패치 취약성 주의**: Transpiler는 인덱스(몇 번째 줄)나 고정된 OpCode 순서에 극도로 의존하여 짜면 게임이 조금만 업데이트되어도 패치가 먹통이 되거나 엉뚱한 코드를 찔러 크래시를
   유발할 수 있습니다. 수치를 찾을 땐 반드시 고유한 상수 패턴이나 메서드 이름을 기준으로 조건 검사를 수행해야 합니다.
2. **스택 균형(Stack Balance) 유지**: 값을 로드(`Ldloc`, `Ldc`)했으면 반드시 소모(`Stloc`, `Call`, `Pop`)해야 합니다. 연산 후 가상 머신의 평가 스택에 찌꺼기
   데이터가 남거나 부족하면 게임 실행 시점에 `InvalidProgramException`과 함께 엔진이 다운됩니다.
3. **디버깅 팁**: 내가 가공한 IL 어셈블리가 온전한 형태인지 확인하고 싶다면, `JAPatchBaseAttribute`에서 제공하는 **`Debug = true`** 속성을 어트리뷰트 인자에 추가하세요.
   인젝션 과정에서 가공된 로우레벨 IL 로그 스트림을 콘솔 창을 통해 한눈에 추적할 수 있습니다.

> 💡 **추가 참고 정보**
> Transpiler는 강력하지만 그만큼 난이도가 높은 작업입니다. 단순한 필드 데이터 읽기/쓰기나 최종 결과 보정이 목적이라면 Transpiler보다는 `Prefix`나 `Postfix` 레이어에서 캡슐화된 변수
> 매핑(`___fieldName`, `__result`) 구조를 다루는 것이 유지보수 면에서 훨씬 유리합니다.
>
> 보다 깊이 있는 하모니 하부 엔진의 IL 변조 명세가 궁금하다면
> 공식 [Harmony Transpiler 가이드 문서](https://harmony.pardeike.net/articles/patching-transpiler.html)를 함께 참고해 주세요.

---

## 5. Finalizer

`Finalizer`는 원본 메서드가 실행되는 도중에 Catch되지 않은 치명적인 **예외(Exception)가 발생하여 게임이 오동작 할 때, 이를 마지막 소생 지점에서 가로채어 구출하는 예외 처리 전용 패치**입니다.

유니티 엔진 특성상 특정 프레임에서 NullReferenceException 등이 발생해 전체 루프가 마비되는 것을 방지하고 시스템 안정성을 유지하기 위해 활용됩니다.

### 5.1. 작동 원리 모사
Finalizer 패치가 원본 메서드 내부의 예외 스트림에 어떻게 개입하는지 가상 코드로 표현하면 다음과 같습니다.

```csharp
public void OriginalMethod() {
    try {
        OriginalCode(); // 원래 게임 코드가 실행됩니다.
    } catch (Exception e) {
        // 원본 코드에서 예외가 터져 게임이 크래시 나기 직전,
        // Finalizer 패치가 에러를 가로채어 처리한 후 다시 예외를 제어합니다.
        throw PatchFinalizerMethod(e);
    }
}
```

### 5.2. 핵심 특징 및 반환값 활용 규칙

* **크래시 방지 및 복구**: 원본 메서드가 어떤 에러를 뿜었는지에 관계없이 Finalizer가 개입하여 예외 상황을 정상 로깅하고 스레드를 비동기적으로 복구할 수 있습니다.
* **원본 실행 여부와 무관한 실행**: 원본 메서드가 정상적으로 실행을 마쳤을 때도 실행되지만, 에러가 발생했을 때 진가를 발휘합니다.
* **반환 타입 제약**: Finalizer 패치 메서드는 무조건 **`Exception`** 타입을 반환값으로 가지거나, 예외를 그대로 전포할 경우 특정 시그니처 형식을 맞춰야 합니다. 일반적으로는 예외 흐름을 제어하기 위해 `Exception` 반환형을 채택합니다.

#### 반환값에 따른 예외 제어 규칙:
1. **`null` 반환**: **발생한 예외를 완전히 무시(소멸) 처리합니다.** 에러가 없었던 것처럼 스레드를 통과시키므로 게임 크래시를 완벽하게 차단합니다.
2. **`__exception` 변수 그대로 반환**: 가로챈 예외를 가공하거나 관찰만 한 뒤, 원래대로 예외가 상위 시스템으로 터져 나가도록(Throw) 놔둡니다.
3. **새로운 `Exception` 반환**: 원래 발생한 에러 대신, 내가 커스텀하게 정의한 새로운 예외 인스턴스를 생성하여 위로 던집니다.

### 5.3. 매개변수 바인딩 규칙 (`__exception`)

Finalizer 패치는 에러 컨텍스트를 다루기 위해 다음과 같은 특수 매개변수 매핑 규칙을 지원합니다.

* **`__exception`**: 원본 메서드 실행 중 발생한 `Exception` 객체 인스턴스가 이 매개변수로 주입됩니다. 만약 원본 메서드가 아무런 에러 없이 정상적으로 끝났다면 이 값은 **`null`**이 됩니다. 따라서 반드시 `if (__exception != null)` 조건문으로 예외 발생 여부를 체크해야 합니다.
* **`__instance`**: 현재 이 예외가 터진 원본 클래스 객체의 인스턴스입니다. 에러가 발생한 시점의 필드 상태값들을 추적할 수 있습니다.
* **`___fieldName` (언더바 3개)**: 에러가 발생한 객체 내부의 숨겨진 `private` 필드 데이터를 가져와서 에러 사후 복구를 위해 수치를 강제 재조정할 수 있습니다.

### 5.4. Finalizer 구현 예시

#### 예시 ①: 발생한 예외를 무효화하여 게임 크래시 방지 (`null` 반환)
게임 오디오나 백그라운드 변수 업데이트 도중 NullReferenceException이 발생하더라도, 유저 화면이 멈추거나 튕기지 않도록 에러를 무시하고 정상 스레드로 돌려보내는 구조입니다.

```csharp
using System;
using JALib.Core.Patch;

[JAPatch(typeof(scrConductor), "UpdateVariables", PatchType.Finalizer, false)]
private static Exception UpdateVariablesFinalizer(Exception __exception) {
// 1. 원본 메서드 실행 중 실제 예외가 터졌는지 검사합니다.
if (__exception != null) {
// 부모 모드의 경고 로그 시스템에 안전하게 에러 메시지를 기록합니다.
BetterCalibrationMod.Instance.Warning($"Conductor 수치 업데이트 중 내부 예외 억제 및 무효화 완료: {__exception.Message}");

        // 2. null을 반환함으로써 발생한 Exception을 완전히 소멸시킵니다.
        // 게임은 크래시 나지 않고 다음 코드를 계속 실행하게 됩니다.
        return null; 
    }
    
    // 에러가 발생하지 않은 정상 상황이라면 null을 그대로 토스합니다.
    return null; 
}
```

#### 예시 ②: 예외 발생 시 사후 복구 및 예외 재전파 (`__exception` 반환)
파일을 저장하거나 네트워크 패킷을 처리하다가 예외가 발생했을 때, 깨진 내부 변수를 안전한 상태로 롤백(Rollback)한 뒤 시스템 분석을 위해 에러는 원래대로 위로 던져주는 구조입니다.

```csharp
[JAPatch(typeof(CustomLayoutManager), "SaveLayoutConfig", PatchType.Finalizer, false)]
private static Exception SaveLayoutFinalizer(Exception __exception, ref bool ___isSavingProcess) {
if (__exception != null) {
BetterCalibrationMod.Instance.Error("레이아웃 저장 중 치명적 파일 입출력 예외 감지! 안전 롤백을 시작합니다.", __exception);

        // private 필드인 '저장중' 플래그를 false로 강제 복구하여 
        // 다음 프레임에 시스템이 무한 교착 상태(Deadlock)에 빠지는 것을 사전에 방지합니다.
        ___isSavingProcess = false; 
        
        // 가로챈 에러 인스턴스를 그대로 반환하여 상위 유니티 시스템에서 예외가 처리되도록 넘겨줍니다.
        return __exception; 
    }
    return null;
}
```

---

### 5.5. Finalizer 사용 시 주의 사항 (Best Practices)

1. **무분별한 null 반환 자제**: 에러가 발생했을 때 `null`을 반환하면 크래시는 막을 수 있지만, 원본 메서드 중간에 코드가 튕겨져 나간 상태이기 때문에 데이터 불일치(중간 변수가 할당되지 않는 등)의 사이드 이펙트가 생길 수 있습니다. 따라서 예외 무효화 후에는 오동작이 없도록 관련 상태 필드(`___변수`)들을 안전값으로 세팅해 주는 코드를 결합하는 것이 좋습니다.
2. **Finalizer 자체 에러 주의**: Finalizer 패치 메서드 내부에서 또다시 에러(예: NullReferenceException)가 발생하면, 하모니 엔진이 이를 더 이상 보호해 주지 못해 게임이 즉시 강제 종료됩니다. 패치 바디 내부 코드는 철저하게 방어적으로 작성해야 합니다.

> 💡 **추가 참고 정보**
> Finalizer는 모드의 치명적인 예외 안전망을 설계할 때 최고의 도구입니다. 단순한 데이터 가로채기나 제어 흐름 수정이 목적이라면 Finalizer보다는 `Prefix`나 `Postfix` 레이어를 다루는 것이 깔끔합니다.
>
> 보다 깊이 있는 하모니 하부 엔진의 예외 처리 명세가 궁금하다면 공식 [Harmony Finalizer 가이드 문서](https://harmony.pardeike.net/articles/patching-finalizer.html)를 함께 참고해 주세요.

---

## 6. Replace

**`Replace`**는 지정한 원본 메서드의 본문(Body) 전체 코드를 메모리 상에서 **완전히 삭제하고, 내가 새로 작성한 패치 메서드의 로직으로 100% 통째로 전면 교체**해버리는 가장 강력하고 파괴적인 패치 타입입니다. 원본 메서드의 설계를 완전히 무시하고 대안 알고리즘을 강제로 주입해야 할 때 사용됩니다.

### 6.1. 작동 원리 모사
Replace 패치가 원본 메서드의 제어 흐름에 어떻게 개입하는지 가상 코드로 표현하면 다음과 같습니다.

```csharp
public void OriginalMethod() {
    // 원래 게임의 코드는 메모리상에서 완전히 무시되고 실행되지 않습니다.
    // [OriginalCode1(); ~ OriginalCode4(); 무효화]

    // 오직 개발자가 새로 작성한 Replace 패치 코드 바디만 독점적으로 실행됩니다.
    PatchMethodCode1();
    PatchMethodCode2();
    PatchMethodCode3();
    PatchMethodCode4();
}
```

### 6.2. 핵심 특징 및 반환 타입 규칙

* **원본 코드의 완전한 격리**: 원본 메서드가 호출되는 즉시 내 패치 메서드로 점프하므로, 원본 메서드 내부에 존재하던 에러 코드나 비효율적인 연산을 완전하게 배제할 수 있습니다.
* **반환 타입 일치**: Replace 패치 메서드가 반환하는 데이터 타입은 **원본 메서드가 기존에 반환하던 데이터 타입과 완벽히 일치**해야 합니다. 원본이 `string`을 반환한다면 패치 메서드도 무조건 `string`을 반환해야 합니다.

### 6.3. 인자 및 파라미터 매핑의 자유도

Replace 패치는 일반 하모니 매핑보다 훨씬 높은 자유도를 제공합니다.

1. **인자 순서 및 생략의 자유**: 패치 메서드의 매개변수를 선언할 때, 원본 메서드가 가진 매개변수들의 **순서와 똑같이 맞추지 않아도 상관없습니다.** 또한, 패치 로직 내에서 사용하지 않을 매개변수는 파라미터 정의에서 아예 빼버리고 적지 않아도 프레임워크가 이름을 기준으로 필요한 값만 정확하게 매핑해 줍니다.
2. **`object[] __args`를 통한 일괄 파싱**: 원본 메서드로 유저가 넘겼던 전체 매개변수들을 순서대로 한 번에 배열 형태로 확보하고 싶다면 `object[] __args`를 파라미터에 추가하면 됩니다.

### 6.4. 특수 변수 활용 규칙 (`__instance` 와 `___fieldName`)

원본 객체의 상태를 제어하기 위해 Replace 패치 전용 특수 변수 규칙을 명확히 이해해야 합니다.

* **`__instance`**: 현재 이 메서드를 실행하고 있는 타깃 클래스의 객체 인스턴스 자신을 가져옵니다. 이를 통해 인스턴스의 public 속성이나 함수에 접근할 수 있습니다.
* **`___fieldName` (언더바 3개)**: 원본 객체 내부에 은닉된 `private` 또는 `protected` 전역 변수나 필드를 리플렉션 없이 다이렉트로 가로챕니다.
    * ⚠️ **동작 특징**: Prefix나 Postfix와 달리, 변수명 앞에 **`ref` 키워드를 붙여서 가져오지 않고 일반 변수 형태로 대입 연산을 수행하더라도 원본 클래스 객체의 전역 변수 메모리 값이 실시간으로 직접 변경**됩니다.

### 6.5. Replace 구현 예시

#### 예시 ①: 가상 원본 클래스 구조 (게임 내부 코드 예시)
```csharp
public class OriginalType {
    private int scoreMultiplier = 2;

    public string CalculateTotalScore(bool applyBonus, int rawScore, string playerKey) {
        OriginalCode1(applyBonus);
        scoreMultiplier = OriginalCode2(rawScore);
        OriginalCode3(playerKey);
        return OriginalCode4(); // 기존 최종 문자열 반환
    }
}
```

#### 예시 ②: JALib Replace 시스템을 이용한 완전 재정의 패치
원본의 로직을 완전히 무시하고, 필드 직접 변경 및 파라미터 순서를 내 입맛대로 바꾸어 새롭게 로직을 빌드하는 구조입니다.

```csharp
using JALib.Core.Patch;

[JAPatch(typeof(OriginalType), "CalculateTotalScore", PatchType.Replace, false)]
private static string CustomCalculateScore(
    int rawScore,
    bool applyBonus,
    OriginalType __instance,
    int ___scoreMultiplier,
    object[] __args
) {
    // 1. 인자 순서를 원래(applyBonus, rawScore...)와 다르게 내 마음대로 배치하여 사용합니다.
    // 2. playerKey 인자는 패치 내부에서 쓰지 않으므로 파라미터 선언에서 과감히 생략했습니다.

    // 3. ___scoreMultiplier 변수에 ref 없이 값을 바로 대입해도 원본 객체의 private 변수 값이 수정됩니다.
    ___scoreMultiplier = 4; 
    
    int finalScore = rawScore * ___scoreMultiplier;
    if(applyBonus) {
        finalScore += 1000;
    }
    
    // 4. object[] __args를 열어보면 [0]에 applyBonus, [1]에 rawScore, [2]에 playerKey 값이 순서대로 들어있습니다.
    string currentKey = __args[2] as string;
    Main.Instance.Log($"Replace 실행 완료 - 플레이어({currentKey}) 점수 정산 제어");

    // 5. 원본 메서드의 반환 타입 규칙(string)에 맞춰 최종 결과 스트림을 리턴합니다.
    return $"[JAMod Patched Score] {finalScore}";
}
```

### 6.6. Replace 사용 시 주의 사항 (Best Practices)

1. **사후 상태 불일치 방지**: 원본 메서드 본문 전체가 날아갔기 때문에, 원래 메서드가 내부에서 수행하던 중요한 부하 작업(예: 메모리 해제, 파일 클로즈, 필수 플래그 갱신 등)까지 같이 누락되어 시스템 고장을 유발할 수 있습니다. 
원래 메서드가 하던 필수 사후 작업이 있다면 내 패치 바디 안에서 명시적으로 모사해 주어야 안전합니다.

---

## 7. ReversePatch

**`JAReversePatch`**는 일반적인 패치(게임 코드가 실행될 때 내 코드를 끼워 넣는 방식)와는 정반대로 작동하는 역방향 패치 시스템입니다. **게임 내부의 원본 메서드 로직(또는 다른 모드가 결합된 변조 메서드)의 알맹이 코드를 복사해 와서, 내 모드 클래스에 선언한 비어 있는 스텁(Stub) 메서드 내부에 강제로 주입하여 일반 함수처럼 직접 호출**하고 싶을 때 사용합니다.

이를 통해 `private` 메서드나 복잡한 게임 내부 연산을 리플렉션 오버헤드 없이 고성능으로 직접 실행할 수 있습니다.

### 7.1. 작동 원리 및 작성 규칙

역패치 시스템을 사용하기 위해 정의하는 모드 메서드는 내부 본문(Body)을 구현하지 않고 **`throw new NotImplementedException();`** 구문으로만 비워두어야 합니다. 런타임에 JALib 프레임워크 코어가 지정된 원본 메서드의 코드를 탐색하여 이 스텁 메서드의 내부를 복사된 어셈블리 로직으로 강제 매핑 및 변조합니다.

```csharp
[JAReversePatch(typeof(Main), "OnEnable", ReversePatchType.Original)]
private static void OnEnableReverse(Main __instance) => throw new NotImplementedException();
```

### 7.2. ReversePatchType 플래그 사양

복사해 올 대상 메서드의 소스 코드 범위를 지정하는 비트 플래그(Flag) 조합 속성입니다. 다른 모드나 패치 레이어가 어디까지 병합된 상태의 코드를 복사해 올지 정밀하게 제어할 수 있습니다.

* **`Original`** (`0`): 어떠한 모드 변조나 패치도 가해지지 않은 순수한 게임 원본 상태의 코드만 그대로 복사해 옵니다.
* **`PrefixCombine`** (`1`) / **`PostfixCombine`** (`2`) / **`FinalizerCombine`** (`8`) / **`OverrideCombine`** (`32`): 각각 원본 메서드에 결합되어 있는 Prefix, Postfix, Finalizer, Override 패치 레이어까지 추적하여 함께 복사 영역에 병합합니다.
* **`TranspilerCombine`** (`4`) / **`ReplaceCombine`** (`16`) / **`ReplaceTranspilerCombine`** (`64`) / **`ILManipulateCombine`** (`128`): 내부 코드가 가공된 레이어들을 결합합니다.
* **`AllInsidePatchCombine`**: `TranspilerCombine | ReplaceCombine | ReplaceTranspilerCombine | ILManipulateCombine` 조합으로, 메서드 본문 내부가 가공된 변조 로직들을 일괄 결합하여 가져옵니다.
* **`AllCombine`**: 프레임워크가 결합할 수 있는 타사 모드 및 모든 형태의 패치가 온전히 조화된 최종 런타임 결과물 상태의 코드 전체를 복사합니다.
* **`DontUpdate`** (`0x40000000`): 역패치 세션의 동기화 및 갱신 대상에서 해당 메서드를 배제합니다.

### 7.3. Transpiler를 이용한 역패치 가공 (Advanced Reverse Transpiler)

역패치 시스템의 가장 강력한 기능 중 하나는 **게임 원본 코드를 내 함수로 복사해 오는 도중에, Transpiler 패턴을 적용하여 원하는 IL 코드 형태로 수정한 뒤 가져오는 것**이 가능하다는 점입니다.

원본 메서드의 일부분만 살짝 바꾼 특수한 복사본 함수를 내 모드 내부에 독립적으로 개설하고 싶을 때 매우 유용합니다.

#### 작동 원리 및 구조
역패치 어트리뷰트가 붙은 스텁 메서드와 **동일한 클래스 내부**에 `Transpiler`라는 이름을 가진 메서드를 구현하면, 하모니 엔진이 역패치 스트림을 복사하는 과정에서 해당 Transpiler를 거쳐 코드를 변환합니다.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using JALib.Core.Patch;

public class MyCustomReverseGroup {
    // 1. 역패치 타깃 스텁 함수를 선언합니다.
    [JAReversePatch(typeof(scrController), "ResetToTitle", ReversePatchType.Original)]
    public static void CustomResetToTitle(scrController __instance) => throw new NotImplementedException();

    // 2. 위의 스텁 함수와 '같은 클래스 내부'에 'Transpiler' 메서드를 작성하면 역패치 진행 시 코드가 가공됩니다.
    // 본래의 ResetToTitle 내부 코드 중 특정 수치나 함수 호출을 변경하여 내 스텁 함수에 채워 넣습니다.
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
        var codes = new List<CodeInstruction>(instructions);
        for (int i = 0; i < codes.Count; i++) {
            // 원본 로직을 복사해 오는 도중, 특정 OpCode 스트림을 탐색하여 가공하는 예시
            if (codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == 1.0f) {
                codes[i].operand = 0.5f; // 복사본 함수 내부에서만 0.5f로 작동하도록 변조하여 가져옴
            }
        }
        return codes.AsEnumerable();
    }
}
```

### 7.4. JAReversePatch 전용 확장 필드

`[JAReversePatch]` 어트리뷰트 사용 시 캡슐화 및 안전성을 위해 제어할 수 있는 전용 옵션입니다.

* **`TryCatchChildren`** (`bool`, 기본값: `true`): 역패치로 복사해 와서 실행 중인 하부 자식 노드 메서드 스트림 내부에서 발생하는 예외(Exception) 상황까지 JALib 코어 단에서 안전하게 캡슐화하여 무력화 및 예외 처리를 수행할지 지정합니다.

---

> 💡 **추가 참고 정보**
> 역패치 시스템은 게임 원본의 기능을 내 모드 내부에서 안전하고 독립적으로 활용할 수 있게 해주는 핵심 유틸리티입니다.
>
> 보다 깊이 있는 하모니 하부 엔진의 역패치 메커니즘 명세가 궁금하다면 공식 [Harmony Reverse Patching 문서](https://harmony.pardeike.net/articles/reverse-patching.html)를 함께 참고해 주세요.
 
---

## 8. OverridePatch

**`JAOverridePatch`**는 상속 구조가 형성되어 있는 클래스 체계 내에서, 가상 메서드로 열려있지 않은 **비가상 메서드(`non-virtual method`)들을 파생 타입 클래스(`extends`) 컨텍스트 안에서 일반적인 가상 함수 오버라이드처럼 완벽하게 재정의**하고자 할 때 사용하는 다형성 확장 패치 시스템입니다.

보통 닷넷 및 게임 내부 시스템에서 `virtual`이 지정되지 않아 하위 클래스에서 정상적인 `override` 문법을 쓸 수 없고 `new` 키워드를 통한 메서드 숨기기(Method Shadowing)만 가능할 때, 프레임워크가 런타임 우회 가상 체인을 형성하여 완벽한 다형성 가로채기를 보장해 줍니다.

### 8.1. 핵심 특징 및 선언 규칙
* **인스턴스 메서드 및 프로퍼티 바인딩**: 일반적인 하모니 패치 메서드들은 정적(`static`) 함수로 선언해야 하지만, `JAOverridePatch`는 **정적으로 선언하지 않습니다.** 상속받아 인스턴스 멤버를 제어하는 자식 클래스 내부의 일반 메서드나 프로퍼티의 `get`/`set` 단에 직접 부착하여 사용합니다.
* **매개변수 없는 선언 지원**: 자식 클래스에서 메서드 이름과 매개변수 시그니처가 부모 클래스의 비가상 메서드 규격과 일치한다면, 별도의 인자 명시 없이 **`[JAOverridePatch]` 단독 선언만으로 부모 메서드를 자동 추적하여 안전하게 덮어씁니다.**

> 🚨 **CRITICAL WARNING (base 메서드 호출 금지)**
> 현재 JALib 프레임워크는 오버라이드 패치 내부에서 부모 클래스의 원본 메서드를 상속 형태로 호출하는 **`base.Method()` 구문에 대한 대응 로직이 구현되어 있지 않습니다.** 만약 패치 메서드 본문 내부에서 부모의 base 코드를 호출하게 되면 런타임에 **무한 루프(StackOverflowException)나 의도치 않은 메모리 크래시가 발생하므로 절대 호출해서는 안 됩니다.**

### 8.2. JAOverridePatchAttribute 세부 옵션 필드
어트리뷰트에 정밀 필터링 옵션을 추가하여 다형성 런타임 검사 컨텍스트를 정교하게 튜닝할 수 있습니다.

* **`IgnoreBasePatch`** (`bool`, 기본값: `true`): 상위 부모 레벨에 기정의되었거나 타 모드가 결합해 둔 일반 하모니 패치 레이어들을 이 상속 맥락에서 완전히 격리 및 무시하고, 내가 재정의한 파생 타입 본연의 로직에만 집중하여 오버라이드를 수행할지 결정합니다.
* **`targetType`** (`Type`) / **`targetTypeName`** (`string`): 이 오버라이드 패치가 실행되어 덮어씌워질 명확한 하위 구체 파생 클래스 타입을 명시적으로 지정합니다.
* **`checkType`** (`bool`, 기본값: `true`): 런타임 환경에서 인스턴스가 호출될 때, 전달된 객체의 실제 런타임 파생 타입을 엄격하게 실시간 대조하여 오버라이드 코드를 격리 실행할지 검증합니다.

### 8.3. 오버라이드 패치 구현 예시

게임 시스템이나 기본 라이브러리의 비가상 컬렉션 클래스(`Dictionary<string, bool>`)를 상속받은 뒤, `new` 키워드로 감춰진 숨김 메서드들과 인덱서 프로퍼티를 `JAOverridePatch`로 온전하게 하이재킹하여 독점적 커스텀 로직을 구축하는 프로덕션 코드 작성 예시입니다.

```csharp
using System.Collections.Generic;
using System.Linq;
using JALib.Core.Patch;

namespace MyCustomMod;

// 게임 시스템 기본 컬렉션을 상속받아 고성능 내부 값 필터링 리스트를 재조립합니다.
public class EventDisableList : Dictionary<string, bool> {
    private string[] _disabledEventValues = [];

    // ① 일반 비가상 메서드 오버라이드 매핑
    // new 키워드로 감춘 자식 메서드 위에 [JAOverridePatch]를 선언하면 
    // 부모 Dictionary의 Clear()가 가동되는 지점까지 가상 함수 형태로 일괄 추적 후 덮어씁니다.
    [JAOverridePatch]
    public new void Clear() {
        // 🚨 base.Clear(); <- 절대 호출 금지 (StackOverflow 방지)
        _disabledEventValues = [];
        BetterCalibrationMod.Instance.Log("EventDisableList 데이터가 청소되었습니다.");
    }

    [JAOverridePatch]
    public new bool ContainsKey(string key) => _disabledEventValues.Contains(key);

    [JAOverridePatch]
    public new void Add(string key, bool value) {
        if (ContainsKey(key) == value) return;
        _disabledEventValues = value 
            ? _disabledEventValues.Append(key).ToArray() 
            : _disabledEventValues.Where(v => v != key).ToArray();
    }

    // ② 프로퍼티 인덱서(this[]) 가로채기 매핑
    // 프로퍼티 내부에 존재하는 getter와 setter 단에 개별적으로 어트리뷰트 선언이 가능합니다.
    public new bool this[string key] {
        [JAOverridePatch]
        get => ContainsKey(key);
        
        [JAOverridePatch]
        set => Add(key, value);
    }

    [JAOverridePatch]
    public new bool Remove(string key) {
        if (!ContainsKey(key)) return false;
        _disabledEventValues = _disabledEventValues.Where(v => v != key).ToArray();
        return true;
    }
}
```

### 8.4. 사용 시 주의 사항

1. **상속 컨텍스트 독립화**: `base.Method()`를 불러올 수 없으므로, 자식 메서드 내부에서는 부모 클래스의 상태에 의존하지 않고 해당 객체 본연의 독립적인 상태와 필드(`_disabledEventValues` 등)를 바탕으로 로직이 온전하게 완결되도록 설계해야 합니다.
2. **이벤트성 컨텍스트 활용 추천**: `checkType` 분기 검증 오버헤드를 고려하여, 매 프레임 수만 번 연산되는 노드 갱신 로직보다는 설정 반영, 데이터 컬렉션 제어, 일회성 데이터 가공 체인의 비가상 상속 관계를 다형성 구조로 다룰 때 적극적으로 사용하는 것이 최적화 구조 상 대단히 유리합니다.

---

## 9. JAPatcher 고급 제어 및 이벤트

모드 진입점이나 기능별 기능 모듈(`Feature`) 내에서 로드 타임 패치 워크플로우를 고도화할 때, JALib 프레임워크 코어의 `JAPatcher` 인스턴스 멤버 속성들과 실패 상태 추적 메커니즘을 연동하여 정밀하게 제어할 수 있습니다.

### 9.1. 핵심 상태 및 최적화 속성 제어

* **`patched`** (`bool`, `{ get; private set; }`)
    * 현재 이 `JAPatcher` 인스턴스가 보관 및 관리하고 있는 패치 리스트(`patchData`) 세트들이 게임 실행 엔진의 어셈블리에 물리적으로 주입(인젝션)되어 **가동 중인 상태인지** 여부를 나타냅니다.
    * 이미 `patched`가 `true`일 때 중복으로 `Patch()`가 가해지는 현상을 자동으로 방어해 줍니다.
* **`usingWaiting`** (`bool`, 기본값: `true`)
    * 플래그를 `true`로 설정하면 JALib 프레임워크의 **패치 최적화 대기 및 일괄 인젝션 시스템**이 활동합니다.
    * `Patch()`를 호출했을 때 즉시 하모니 컴파일 및 물리 변조를 찌르지 않고, 결합되는 타사 모드들의 어셈블리 및 패치 적재 시퀀스가 모두 끝날 때까지 큐에 담아 대기하다가 **해당 프레임의 마지막 부분(End of Frame)**에 단 한 번에 인젝션을 일괄 진행하여 연산 오버헤드를 줄입니다.
    * 로딩 시점이 아닌 동적 실시간 패치가 즉시 필요하다면 `false`로 제어해야 합니다.

### 9.2. OnFailPatch 이벤트를 통한 실시간 오류 추적

패치가 적용되는 런타임 도중, 게임의 대규모 업데이트로 인한 타깃 메서드 소멸, 혹은 타사 모드 간의 심각한 시그니처 충돌로 인해 특정 패치 주입이 실패할 경우 이를 실시간 포착할 수 있는 안전트리거 이벤트 대리자입니다.

#### 1) 델리게이트 명세 및 이벤트 구조
```csharp
public delegate void FailPatch(string patchId, bool disabled);
public event FailPatch OnFailPatch;
```
* `patchId`: 실패가 감지된 패치 어트리뷰트의 풀 네임 유니크 식별자 키 스트링입니다.
* `disabled`: 해당 패치의 어트리뷰트에 `disable = true` 필드 제한 속성이 선언되어 있어 발생한 고의적인 누락 실패 시나리오인지 여부를 공유합니다.

#### 2) OnFailPatch 콜백 구독 및 방어 코드 구축 예시
```csharp
public class MyFeatureModule : Feature {
    public MyFeatureModule() : base(Main.Instance, nameof(MyFeatureModule)) {
        // 패치 적용 전, 실패 처리 이벤트를 구독하여 모드의 자가 진단 안전망을 개설합니다.
        Patcher.OnFailPatch += HandlePatchFailure;
    }

    private void HandlePatchFailure(string patchId, bool disabled) {
        Mod.Error($"[자가진단] 치명적 오류 - 패치 실패 식별자: {patchId}");
        
        if (disabled) {
            // 주입 실패에 따른 대체용 로우레벨 리플렉션 캘리브레이션 모듈 활성화 분기 처리
            Mod.Warning("하모니 코드 결합에 실패하여, 대체용 백업 연산 엔진으로 자동 전환합니다.");
            MyFallbackEngine.Activate();
        }
    }
}
```

### 9.3. 패치의 역적용 및 수명 주기 제어 (`Unpatch` 와 `Dispose`)

`JAPatcher` 클래스는 **`IDisposable` 인터페이스**를 온전히 상속받고 있으므로, 유저가 인게임 UI 메뉴에서 특정 모드 기능을 비활성화(`Disable`)하거나 리로드할 때 수명 주기를 깨끗하게 롤백할 수 있는 메모리 환원 함수를 탑재하고 있습니다.

* **`Unpatch()` 메서드**
    * 가동 중이던 현재 `JAPatcher` 내부의 모든 `[JAPatch]`, `[JAReversePatch]`, `[JAOverridePatch]` 내역을 게임 엔진 메모리 맵에서 즉시 일제히 도려내어 **순수 원본 또는 패치 이전 상태의 게임 어셈블리로 완전히 복구 및 원상복귀** 시킵니다.
* **`Dispose()` 메서드**
    * 객체 파괴자로 진입할 때 실행되는 함수로, 내부적으로 **`Unpatch()`를 연쇄 실행**하여 메모리 변조 찌꺼기를 완전 소멸시킨 뒤 GC(가비지 컬렉터) 수집 대상으로 패치셋 데이터를 할당 반환합니다.

```csharp
// 모드 비활성화 주기 상태에서 완전 자원 수거 정석 구조
public override void OnDisable() {
    if (_myPatcher != null) {
        // Unpatch()를 호출하거나 Dispose()를 수행하여 원본 게임 코드로 완벽 복구합니다.
        _myPatcher.Dispose();
        _myPatcher = null;
    }
}
```

---

## [다음](007-Tools.md)
