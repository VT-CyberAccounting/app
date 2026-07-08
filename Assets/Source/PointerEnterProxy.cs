using UnityEngine;
using UnityEngine.EventSystems;

public class PointerEnterProxy : MonoBehaviour, IPointerEnterHandler
{
    public System.Action OnEnter;

    public void OnPointerEnter(PointerEventData eventData) => OnEnter?.Invoke();
}
