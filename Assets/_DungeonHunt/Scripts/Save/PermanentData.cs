using System;
using System.Collections.Generic;

// 06 §3 영구 진행 데이터. 저장 슬롯 하나에 함께 저장됨.
[Serializable]
public class PermanentData
{
    public int Version = 1;
    public int PermanentCurrency;
    public List<string> CurrencyUnlockedArtifacts = new List<string>();
    public List<string> PendingUnlocks = new List<string>();          // 해금 대기 (진행 조건)
    public List<string> RevealedPendingUnlocks = new List<string>();  // 카드 공개 완료
    public List<string> DiscoveredArtifacts = new List<string>();
    public int GrowthPointsPurchased;                                 // 06-1 구매한 성장 포인트 수
    public List<int> GrowthAllocation = new List<int>();              // 06-1 트리마다 배치된 노드 수
    public int RunClearedCount;
    public int HighestFloorReached;                                   // 1부터
    public List<int> AchievedConditions = new List<int>();            // ProgressCondition 값
    public bool OnboardingComplete;
    public List<string> AppliedSettlementIds = new List<string>();    // 같은 정산 ID는 한 번만

    public PermanentData Clone()
    {
        string json = UnityEngine.JsonUtility.ToJson(this);
        return UnityEngine.JsonUtility.FromJson<PermanentData>(json);
    }
}

// 08 §5.4 설정값. 영구 진행과 별도 파일.
[Serializable]
public class SettingsData
{
    public float MasterVolume = 100f;
    public float BgmVolume = 80f;
    public float SfxVolume = 100f;
    public bool VSync = true;
    public float ScreenShake = 75f;
    public int ResolutionWidth;
    public int ResolutionHeight;
    public int WindowMode = 1; // 0 전체 화면, 1 테두리 없는 전체 화면, 2 창
}
