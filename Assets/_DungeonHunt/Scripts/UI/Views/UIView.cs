using UnityEngine;
using UnityEngine.UI;

public enum ModalKind
{
    None,
    TimeStop,     // 일시정지 메뉴, 조작법, 설정 (게임 정지)
    NonStop,      // 확대 미니맵, 보유 아티팩트, 상점, 도박, 아티팩트 획득 (시간 흐름)
    System,       // 포커스 복귀, 저장 실패, 안내 (시스템 중단)
    Screen,       // 첫 화면, 로비, 결과창 같은 전체 화면
}

// 코드로 조립하는 UI 화면 하나. UIManager가 열고 닫고 매 프레임 Tick함.
public abstract class UIView
{
    public GameObject Root { get; private set; }
    public RectTransform Rect { get; private set; }
    public bool IsOpen => Root != null && Root.activeSelf;
    public virtual ModalKind Kind => ModalKind.None;
    // ESC로 닫을 수 있는지
    public virtual bool ClosesOnEsc => true;

    protected UIManager Manager { get; private set; }

    public void Create(UIManager manager, Transform parent, string name)
    {
        Manager = manager;
        RectTransform rect = UIFactory.CreateRect(name, parent);
        UIFactory.Stretch(rect);
        Root = rect.gameObject;
        Rect = rect;
        Build(rect);
        Root.SetActive(false);
    }

    protected abstract void Build(RectTransform root);

    public virtual void Open()
    {
        Root.SetActive(true);
        Root.transform.SetAsLastSibling();
        OnOpened();
    }

    public virtual void Close()
    {
        if (Root == null || !Root.activeSelf) return;
        Root.SetActive(false);
        OnClosed();
    }

    protected virtual void OnOpened() { }
    protected virtual void OnClosed() { }
    public virtual void Tick() { }

    // ESC 처리. 기본은 닫기
    public virtual void OnEscape() => Manager.CloseView(this);

    // 화면 가운데 반투명 배경 + 패널
    protected static RectTransform CenterPanel(RectTransform root, Vector2 size, bool dimBackground = true)
    {
        if (dimBackground)
        {
            Image dim = UIFactory.Panel("Dim", root, new Color(0f, 0f, 0f, 0.55f));
            UIFactory.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
        }
        Image panel = UIFactory.Panel("Panel", root, UIFactory.PanelColor);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        return panel.rectTransform;
    }

    protected static Text Title(RectTransform panel, string text, float y = -30f, int size = 44)
    {
        Text title = UIFactory.Label("Title", panel, text, size, TextAnchor.UpperCenter, UIFactory.AccentColor);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(panel.sizeDelta.x - 40f, 60f));
        return title;
    }
}
