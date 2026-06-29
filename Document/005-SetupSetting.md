# JAMod 개발 가이드 - 설정 만들기

## [목차로 이동](000-DevelopGuide.md)
1. [JASetting 상속 및 생성자](#1-jasetting-%EC%83%81%EC%86%8D-%EB%B0%8F-%EC%83%9D%EC%84%B1%EC%9E%90)
2. [설정(Setting) 필드 추가 방법](#2-%EC%84%A4%EC%A0%95setting-%ED%95%84%EB%93%9C-%EC%B6%94%EA%B0%80-%EB%B0%A9%EB%B2%95)
3. [Setting 전용 어트리뷰트(Attribute)](#3-setting-%EC%A0%84%EC%9A%A9-%EC%96%B4%ED%8A%B8%EB%A6%AC%EB%B7%B0%ED%8A%B8attribute)
4. [JASetting 주요 내장 함수](#4-jasetting-%EC%A3%BC%EC%9A%94-%EB%82%B4%EC%9E%A5-%ED%95%A8%EC%88%98)

---

## 1. JASetting 상속 및 생성자

모드 전역이나 개별 기능(Feature)에 필요한 설정 데이터를 영구적으로 저장하고 직렬화하기 위해, 설정 데이터 메인 클래스에 `JASetting`을 상속받아 구현합니다.
```csharp
public class MySetting : JASetting
```

`JASetting` 생성자는 다음과 같은 규격으로 정의해야 합니다. 외부 인스턴스 생성을 제한하기 위해 접근 제한자는 `private`으로 두는 것을 추천합니다.

```csharp
private MySetting(JAMod mod, JObject jsonObject = null) : base(mod, jsonObject)
```

---

## 2. 설정(Setting) 필드 추가 방법

원하는 설정 옵션 항목을 추가하려면, 상속받은 클래스 내부에 인스턴스 필드를 자유롭게 선언하시면 됩니다.

```csharp
public class MyModSetting : JASetting {
    public int ComboSize;
    public string CustomNickName;
    public bool IsPopupEnabled;
}
````

### ⚠️ 필드 추가 규칙 및 특징
* **기본 동작**: 별도의 설정이 없으면 **`public` 인스턴스 필드만** 자동으로 수집되어 JSON 파일로 직렬화(저장)됩니다.
* **자동 제외**: `static`으로 선언된 필드는 영구 설정 데이터 대상에서 자동으로 생략됩니다.
* **2중 중첩 설정 지원**: `JASetting`을 구조적으로 분리하여 관리할 수 있도록, 다른 `JASetting` 상속 클래스를 자식 필드로 선언해 중첩하여 조작할 수 있습니다.

```csharp
public class MyModSetting : JASetting {
    public int MainOption;
    public AdvancedSetting DetailOptions; // 2중 설정 레이어 구조 지원
}

public class AdvancedSetting : JASetting {
    public float AccuracyOffset;
    public string LogFilePath;
}
````
---

## 3. Setting 전용 어트리뷰트(Attribute)

필드를 선언할 때 선언형 어트리뷰트를 붙여 직렬화 형태나 저장 이름, 데이터 정밀도 등을 손쉽게 제어할 수 있습니다.

### [SettingInclude]
* **설명**: 기본적으로 무시되는 `private` 또는 `protected` 등 public이 아닌 필드를 강제로 설정 파일에 포함하여 저장하고 싶을 때 사용합니다.
```csharp
[SettingInclude]
private string privateToken;
```

### [SettingIgnore]
* **설명**: `public` 필드 중에서 임시 데이터나 런타임 변수용으로만 사용하고, 설정 파일에는 영구 저장하지 않아야 할 때 부착합니다.
```csharp
[SettingIgnore]
public float temporaryRuntimeTimer;
```

### [SettingName]
* **설명**: JSON 파일이나 환경 UI에 저장될 키(Key) 이름을 변수명과 다르게 커스텀하게 지정할 때 사용합니다. 공백이나 특수문자가 필요할 때 유용합니다.
```csharp
[SettingName("Display Mode Type")]
public int displayMode;
````

### [SettingCast]
* **설명**: 런타임에 사용하는 변수 타입과 파일 시스템에 직렬화되어 저장될 때의 데이터 타입을 다르게 지정하여 강제로 캐스팅할 때 사용합니다.
```csharp
[SettingCast(typeof(int))]
public long highPrecisionId;
```

### [SettingRound]
* **설명**: 소수점 데이터가 있는 `float` 혹은 `double` 데이터를 저장할 때, 기록될 최대 소수점 자리수를 정형화하여 제한합니다.
```csharp
[SettingRound(2)] // 소수점 2째자리까지 반올림하여 저장
public float calibratedOffset;
```

---

## 4. JASetting 주요 내장 함수

`JASetting` 인스턴스는 필드 데이터 외에도 하부 데이터 토큰을 동적으로 직접 다루거나 생수명 주기를 제어하기 위한 유틸리티 함수들을 제공합니다.

* **`this[string key]`** (`Public` / `JToken`)
    * 클래스 필드로 정의되지 않은 원시 데이터 키(Key)에 해당하는 raw 값을 Json 토큰 객체로 직접 가져옵니다.
* **`Get<T>(string key, out T value)`** (`Public` / `bool`)
    * 지정한 문자열 키에 들어있는 데이터를 특정 제네릭 타입(`T`)으로 역직렬화하여 안전하게 파싱합니다. 파싱 성공 여부를 반환합니다.
* **`Set(string key, object value)`** (`Public` / `void`)
    * 클래스 필드 영역이 아닌 데이터 매핑 영역에 동적으로 직접 특정 키와 오브젝트 값을 설정합니다.
* **`Remove(string key)`** (`Public` / `void`)
    * 매핑 영역에서 지정된 키의 임의 값을 명시적으로 제거합니다.
* **`PutFieldData()`** / **`RemoveFieldData()`** (`Public` / `void`)
    * 런타임 실시간 필드 변수 상태 값을 JSON 데이터 레이어에 강제로 커밋하거나 삭제 연동을 진행합니다.
* **`Dispose()`** (`Public` / `void`)
    * 설정 인스턴스 메모리를 정리하고 연결된 자원 할당을 완전히 해제합니다.

---

## [다음](006-Patch.md)
