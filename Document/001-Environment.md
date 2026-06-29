# JAMod 개발 가이드 - 개발 환경 구축

## [목차로 이동](000-DevelopGuide.md)
1. [Templete 설정](#1-templete-%EC%84%A4%EC%A0%95)
2. [라이브러리 의존성 설정](#2-%EB%9D%BC%EC%9D%B4%EB%B8%8C%EB%9F%AC%EB%A6%AC-%EC%9D%98%EC%A1%B4%EC%84%B1-%EC%84%A4%EC%A0%95)

---

## 1. Templete 설정
JALib을 이용한 모드를 쉽게 만들기 위해서는 배포된 프로젝트 Templete을 사용하는 것을 추천합니다.

해당 방법은 **JetBrains Rider**를 기준으로 작성되었습니다.

### [Templete 링크](https://github.com/Jongye0l/JAMod-Templete)

다음 방법을 통해 Templete를 설정할 수 있습니다.

1. Rider를 실행하고 **New Solution**을 클릭합니다.
2. 좌측 하단의 파란색 글씨로 된 **Manage Templates...**을 클릭합니다.
3. 아래 사진과 같은 창이 뜨면 **Install Template...**을 클릭합니다.
![Install Templete](https://github.com/Jongye0l/JALib/raw/main/Document/Img/AddTemplete.png?raw=true)
4. 다운로드한 Templete 파일(`.zip` 또는 폴더)을 선택하고 **Open**을 클릭합니다.
5. 창을 닫고 다시 **New Solution**을 클릭하면 목록에 `JAMod` 템플릿이 추가된 것을 확인할 수 있습니다.

---

## 2. 라이브러리 의존성 설정
JALib을 사용하기 위해서는 빌드 대상 프로젝트에 참조(Reference)를 추가해야 합니다.

![Add Dependency](https://github.com/Jongye0l/JALib/raw/main/Document/Img/AddDependencies.png?raw=true)
1. 위에 사진처럼 Dependencies를 우클릭한다.
2. References...를 클릭한다.
3. Add From...을 클릭한 후 JALib.dll을 추가한다.

이제 JALib을 사용할 준비가 되었습니다.

## [다음](002-SetupJAMod.md)
