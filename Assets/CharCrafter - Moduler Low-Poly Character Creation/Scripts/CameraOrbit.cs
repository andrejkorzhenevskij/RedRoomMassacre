using UnityEngine;
using UnityEngine.EventSystems;

namespace AyuoDev.CharCrafter
{
    public class CameraOrbit : MonoBehaviour
    {
        public Transform target;
        public float distance = 3f;
        public float zoomSpeed = 2f;
        public float minDistance = 1.5f;
        public float maxDistance = 6f;

        public float xSpeed = 120f;
        public float ySpeed = 80f;

        public float yMinLimit = -20f;
        public float yMaxLimit = 80f;

        public float smoothTime = 0.1f;

        private float x = 0f;
        private float y = 0f;

        private Quaternion targetRotation;
        private Quaternion currentRotation;
        private Vector3 currentVelocity;

        private bool Active;

        void Start()
        {
            Vector3 angles = transform.eulerAngles;
            x = angles.y;
            y = angles.x;

            targetRotation = transform.rotation;
            currentRotation = transform.rotation;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void LateUpdate()
        {
            if (Input.GetMouseButtonDown(0) && !IsPointerOverUI()) Active = true;
            if (Input.GetMouseButtonUp(0)) Active = false;

            if (Active)
            {
                x += Input.GetAxis("Mouse X") * xSpeed * Time.deltaTime;
                y -= Input.GetAxis("Mouse Y") * ySpeed * Time.deltaTime;
                y = Mathf.Clamp(y, yMinLimit, yMaxLimit);
            }
            targetRotation = Quaternion.Euler(y, x, 0);
            currentRotation = Quaternion.Slerp(currentRotation, targetRotation, smoothTime);
            Vector3 targetPosition = currentRotation * new Vector3(0, 0, -distance) + target.position;

            transform.rotation = currentRotation;
            transform.position = Vector3.Lerp(transform.position, targetPosition, smoothTime);
        }
        public bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
