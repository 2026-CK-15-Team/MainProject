using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Button이 못 받는 우클릭과 마우스 올림을 코드로 조립한 UI에 붙이는 용도
public class UIPointerHandler : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Action LeftClick, RightClick, Enter, Exit;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) LeftClick?.Invoke();
        else if (eventData.button == PointerEventData.InputButton.Right) RightClick?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData) => Enter?.Invoke();
    public void OnPointerExit(PointerEventData eventData) => Exit?.Invoke();
}
