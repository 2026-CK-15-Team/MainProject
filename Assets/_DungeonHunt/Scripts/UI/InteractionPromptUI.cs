using UnityEngine;

public class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] private Interactor interactor;
    [SerializeField] private GameObject promptRoot; // F 아이콘. 이 스크립트가 붙은 오브젝트와 달라야 함
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.5f, 0f);

    private void Update()
    {
        Transform target = interactor.PromptTarget;

        if (target == null)
        {
            promptRoot.SetActive(false);
            return;
        }

        promptRoot.SetActive(true);
        promptRoot.transform.position = target.position + worldOffset;
    }
}
