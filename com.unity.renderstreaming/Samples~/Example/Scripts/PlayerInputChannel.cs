using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.RenderStreaming.Samples
{
    public class PlayerInputChannel : MonoBehaviour
    {
        [SerializeField] SimplePlayerInput playerInput;
        [SerializeField] InputSystemChannelReceiver receiver;
        [SerializeField] bool useLocalDevices;

        protected virtual void Awake()
        {
            receiver.onDeviceChange += OnDeviceChange;
            if(useLocalDevices)
            {
                PerformPairingWithAllLocalDevices();
            }
        }

        public void PerformPairingWithAllLocalDevices()
        {
            playerInput.PerformPairingWithAllLocalDevices();
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                    {
                        playerInput.PerformPairingWithDevice(device);
                        OnAddedDevice(device);
                        return;
                    }
                case InputDeviceChange.Removed:
                    {
                        playerInput.UnpairDevices(device);
                        OnRemovedDevice(device);
                        return;
                    }
            }
        }

        protected virtual void OnAddedDevice(InputDevice device) {}
        protected virtual void OnRemovedDevice(InputDevice device) {}
    }
}
