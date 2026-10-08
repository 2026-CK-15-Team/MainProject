// 런타임 핵심 오브젝트 참조 모음. 각 컴포넌트가 Awake에서 자기를 등록함.
// FindObjectOfType를 여기저기 부르지 않으려는 용도.
public static class GameServices
{
    public static GameConfig Config;
    public static PlayerController Player;
    public static RunManager Run;
    public static FloorDirector Floor;
    public static CameraController Camera;
    public static ArtifactSystem Artifacts;
    public static RunWallet Wallet;
    public static UIManager UI;
    public static SaveService Save;
    public static FeedbackService Feedback;

    public static GameBalance Balance => Config != null ? Config.Balance : null;
}
