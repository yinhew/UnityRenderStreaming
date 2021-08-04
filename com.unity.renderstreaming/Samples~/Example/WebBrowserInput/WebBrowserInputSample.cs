using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.RenderStreaming.Samples
{
    class WebBrowserInputSample : MonoBehaviour
    {
        [SerializeField] RenderStreaming renderStreaming;
        [SerializeField] Dropdown dropdownCamera;
        [SerializeField] Transform[] cameras;
        [SerializeField] CopyTransform copyTransform;

        [Header("Streaming Settings")] 
        [SerializeField] bool hardwareEncoding;
        [Tooltip("Resolution of the camera RenderTexture")]
        [SerializeField] Vector2Int captureResolution = new Vector2Int(1920, 1080);
        [Tooltip("Resolution of the destination RenderTexture used for video stream")]
        [SerializeField] Vector2Int encodingResolution = new Vector2Int(1920, 1080);
        [Tooltip("Max encoding bitrate in Mbit")]
        [SerializeField] float maxBitrate = 5;
        [Tooltip("Divide resolution by this value. Only works for software encoding.")]
        [SerializeField] double downscale = 1;
        // Setting the FPS doesn't seem to do anything 
        /*[SerializeField]*/ uint encodedFps = 25;

        private readonly List<CameraStreamer> cameraStreamers = new List<CameraStreamer>();

        private void Start()
        {
            dropdownCamera.onValueChanged.AddListener(OnChangeCamera);
            InitializeCameraStreamers();
            StartRenderStreaming();
        }

        private void InitializeCameraStreamers()
        {
            foreach (var cam in cameras)
            {
                var cameraStreamer = cam.GetComponent<CameraStreamer>();
                cameraStreamer.StreamingSize = captureResolution;
                cameraStreamer.EncodingResolution = encodingResolution;
                cameraStreamer.OnStartedStream += id => { StartCoroutine(HandleStreamStarted(cameraStreamer, id)); };
                cameraStreamer.OnStoppedStream += id => HandleStreamStopped(cameraStreamer, id);
                cameraStreamers.Add(cameraStreamer);
            }
        }
        
        private void StartRenderStreaming()
        {
            if (renderStreaming.runOnAwake)
                return;
            
            try         
            {
                renderStreaming.Run(hardwareEncoder: hardwareEncoding, signaling: RenderStreamingSettings.Signaling);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to start WebRTC streaming (HW Encoding: {hardwareEncoding}): {ex.Message}");
                renderStreaming.Stop();

                // Try again without hardware encoding support
                if (hardwareEncoding)
                {
                    Debug.Log("Starting WebRTC with software encoding ...");
                    renderStreaming.Run(hardwareEncoder: false, signaling: RenderStreamingSettings.Signaling);
                }
            }
        }

        void OnChangeCamera(int value)
        {
            copyTransform.SetOrigin(cameras[value]);
        }
        
        private IEnumerator HandleStreamStarted(CameraStreamer cameraStreamer, string connectionId)
        {
            // The parameters are not available right away so wait for them
            yield return new WaitForEndOfFrame();
            Debug.Log($"WebRTC stream connected: {cameraStreamer.name} has {cameraStreamer.Senders.Count} active connections");
            cameraStreamer.ChangeVideoParameters(connectionId, (ulong)(maxBitrate * 1024 * 1024), encodedFps, downscale);
            yield return null;
        }
        
        private void HandleStreamStopped(CameraStreamer cameraStreamer, string connectionId)
        {
            Debug.Log($"WebRTC stream disconnected: {cameraStreamer.name} has {cameraStreamer.Senders.Count} active connections");

            if (cameraStreamer.Senders.Count == 0)
                cameraStreamer.Dispose();
        }
    }
}
