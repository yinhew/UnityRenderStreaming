using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.RenderStreaming.Samples
{
    public class CameraController : PlayerInputChannel
    {
        [SerializeField] float moveSpeed = 100f;
        [SerializeField] float rotateSpeed = 10f;

        Vector2 inputMovement;
        Vector2 inputLook;

        public void OnMovement(InputAction.CallbackContext value)
        {
            inputMovement = value.ReadValue<Vector2>();
        }

        public void OnLook(InputAction.CallbackContext value)
        {
            inputLook = value.ReadValue<Vector2>();
        }

        private void Update()
        {
            var forwardDirection = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            var moveForward = forwardDirection * new Vector3(inputMovement.x, 0, inputMovement.y);
            transform.position += moveForward * Time.deltaTime * moveSpeed;

            var moveAngles = new Vector3(-inputLook.y, inputLook.x);
            var newAngles = transform.localEulerAngles + moveAngles * Time.deltaTime * rotateSpeed;
            transform.localEulerAngles = new Vector3(Mathf.Clamp(newAngles.x, 0, 45), newAngles.y, 0);
        }
    }
}
