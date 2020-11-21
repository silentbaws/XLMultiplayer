using UnityEngine;
using UnityEngine.EventSystems;

namespace XLMultiplayerUI {
    public class ChatDrag : MonoBehaviour, IBeginDragHandler, IDragHandler {
        public Canvas parentCanvas;

        private Vector3 mouseStartPosition;
        private Vector3 chatStartPosition;

        public void OnBeginDrag(PointerEventData eventData) {
            mouseStartPosition = Input.mousePosition;
            chatStartPosition = this.transform.parent.position;
        }

        public void OnDrag(PointerEventData eventData) {
            Transform chatTransform = this.transform.parent;
            Rect chatRect = this.transform.parent.GetComponent<RectTransform>().rect;
            float scale = parentCanvas.scaleFactor;

            chatTransform.position = chatStartPosition + Input.mousePosition - mouseStartPosition;

            //0.165 extra height on bottom(message box)   ----   0.05 extra height on top(drag bar)

            chatTransform.position = new Vector3(Mathf.Clamp(chatTransform.position.x, (chatRect.width * scale) / 2, Screen.width - (chatRect.width * scale) / 2),
                Mathf.Clamp(chatTransform.position.y, ((chatRect.height * 1.330f) * scale) / 2, Screen.height - (((chatRect.height * 1.1f) * scale) / 2)),
                this.transform.parent.position.z);
        }
    }
}
