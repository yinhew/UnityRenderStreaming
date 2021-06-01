using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.RenderStreaming.Signaling;
using Unity.WebRTC;

namespace Unity.RenderStreaming
{
    /// <summary>
    ///
    /// </summary>
    internal struct RenderStreamingDependencies
    {
        /// <summary>
        ///
        /// </summary>
        public ISignaling signaling;

        /// <summary>
        ///
        /// </summary>
        public EncoderType encoderType;

        /// <summary>
        ///
        /// </summary>
        public RTCConfiguration config;

        /// <summary>
        ///
        /// </summary>
        public Func<IEnumerator, Coroutine> startCoroutine;

        /// <summary>
        /// unit is second;
        /// </summary>
        public float resentOfferInterval;
    }

    /// <summary>
    ///
    /// </summary>
    internal class RenderStreamingInternal : IDisposable,
        IRenderStreamingHandler, IRenderStreamingDelegate
    {
        /// <summary>
        ///
        /// </summary>
        public event Action onStart;

        /// <summary>
        ///
        /// </summary>
        public event Action<string> onCreatedConnection;

        /// <summary>
        ///
        /// </summary>
        public event Action<string> onDeletedConnection;

        /// <summary>
        ///
        /// </summary>
        public event Action<string, string> onGotOffer;

        /// <summary>
        ///
        /// </summary>
        public event Action<string, string> onGotAnswer;

        /// <summary>
        ///
        /// </summary>
        public event Action<string> onConnect;

        /// <summary>
        ///
        /// </summary>
        public event Action<string> onDisconnect;

        /// <summary>
        ///
        /// </summary>
        public event Action<string, RTCRtpReceiver> onAddReceiver;

        /// <summary>
        ///
        /// </summary>
        public event Action<string, RTCDataChannel> onAddChannel;

        private bool _disposed;
        private readonly ISignaling _signaling;
        private RTCConfiguration _config;
        private readonly Func<IEnumerator, Coroutine> _startCoroutine;
        private readonly Dictionary<string, PeerConnection> _mapConnectionIdAndPeer =
            new Dictionary<string, PeerConnection>();
        private bool _runningResendCoroutine;
        private float _resendInterval = 1.0f;

        static List<RenderStreamingInternal> s_list = new List<RenderStreamingInternal>();

        /// <summary>
        ///
        /// </summary>
        /// <param name="dependencies"></param>
        public RenderStreamingInternal(ref RenderStreamingDependencies dependencies)
        {
            if (dependencies.signaling == null)
                throw new ArgumentException("Signaling instance is null.");
            if (dependencies.startCoroutine == null)
                throw new ArgumentException("Coroutine action instance is null.");

            if (s_list.Count == 0)
            {
                WebRTC.WebRTC.Initialize(dependencies.encoderType);
            }

            _config = dependencies.config;
            _startCoroutine = dependencies.startCoroutine;
            _resendInterval = dependencies.resentOfferInterval;
            _signaling = dependencies.signaling;
            _signaling.OnStart += OnStart;
            _signaling.OnCreateConnection += OnCreateConnection;
            _signaling.OnDestroyConnection += OnDestroyConnection;
            _signaling.OnOffer += OnOffer;
            _signaling.OnAnswer += OnAnswer;
            _signaling.OnIceCandidate += OnIceCandidate;
            _signaling.Start();

            s_list.Add(this);
            _startCoroutine(WebRTC.WebRTC.Update());
        }

        /// <summary>
        ///
        /// </summary>
        ~RenderStreamingInternal()
        {
            Dispose();
        }

        /// <summary>
        ///
        /// </summary>
        public void Dispose()
        {
            if (this._disposed)
            {
                return;
            }

            _runningResendCoroutine = false;

            _signaling.Stop();
            _signaling.OnStart -= OnStart;
            _signaling.OnCreateConnection -= OnCreateConnection;
            _signaling.OnDestroyConnection -= OnDestroyConnection;
            _signaling.OnOffer -= OnOffer;
            _signaling.OnAnswer -= OnAnswer;
            _signaling.OnIceCandidate -= OnIceCandidate;

            s_list.Remove(this);
            if (s_list.Count == 0)
            {
                WebRTC.WebRTC.Dispose();
            }

            this._disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="connectionId"></param>
        public void CreateConnection(string connectionId)
        {
            _signaling.OpenConnection(connectionId);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="connectionId"></param>
        public void DeleteConnection(string connectionId)
        {
            _signaling.CloseConnection(connectionId);
        }

        public bool ExistConnection(string connectionId)
        {
            return _mapConnectionIdAndPeer.ContainsKey(connectionId);
        }

        public bool IsConnected(string connectionId)
        {
            if (!_mapConnectionIdAndPeer.TryGetValue(connectionId, out var peer))
                return false;

            return peer.peer.ConnectionState == RTCPeerConnectionState.Connected;
        }

        public bool IsStable(string connectionId)
        {
            if (!_mapConnectionIdAndPeer.TryGetValue(connectionId, out var peer))
                return false;

            return peer.IsStable();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="track"></param>
        public RTCRtpTransceiver AddTrack(string connectionId, MediaStreamTrack track)
        {
            var peer = _mapConnectionIdAndPeer[connectionId];
            RTCRtpSender sender = peer.peer.AddTrack(track);
            var transceiver = peer.peer.GetTransceivers().First(t => t.Sender == sender);

            // note:: This line is needed to stream video to other peers with hardware codec.
            // The exchanging SDP is failed if remove the line because the hardware decoder currently is not supported.
            // Please remove the line after supporting the hardware decoder.
            transceiver.Direction = RTCRtpTransceiverDirection.SendOnly;
            return transceiver;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="kind"></param>
        /// <returns></returns>
        public RTCRtpTransceiver AddTrack(string connectionId, TrackKind kind)
        {
            return _mapConnectionIdAndPeer[connectionId].peer.AddTransceiver(kind);
        }


        /// <summary>
        ///
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="track"></param>
        public void RemoveTrack(string connectionId, MediaStreamTrack track)
        {
            var sender = GetSenders(connectionId).First(s => s.Track == track);
            _mapConnectionIdAndPeer[connectionId].peer.RemoveTrack(sender);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public RTCDataChannel CreateChannel(string connectionId, string name)
        {
            RTCDataChannelInit conf = new RTCDataChannelInit();
            return _mapConnectionIdAndPeer[connectionId].peer.CreateDataChannel(name, conf);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="track"></param>
        /// <returns></returns>
        public IEnumerable<RTCRtpSender> GetSenders(string connectionId)
        {
            return _mapConnectionIdAndPeer[connectionId].peer.GetSenders();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="track"></param>
        /// <returns></returns>
        public IEnumerable<RTCRtpReceiver> GetReceivers(string connectionId)
        {
            return _mapConnectionIdAndPeer[connectionId].peer.GetReceivers();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="connectionId"></param>
        public void SendOffer(string connectionId)
        {
            var pc = _mapConnectionIdAndPeer[connectionId];
            if (!IsStable(connectionId))
            {
                if (!pc.waitingAnswer)
                {
                    throw new InvalidOperationException(
                        $"{pc} sendoffer needs in stable state, current state is {pc.peer.SignalingState}");
                }

                _signaling.SendOffer(connectionId, pc.peer.LocalDescription);
                return;
            }

            pc.SetLocalDescription(RTCSdpType.Offer);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="connectionId"></param>
        public void SendAnswer(string connectionId)
        {
            _mapConnectionIdAndPeer[connectionId].SetLocalDescription(RTCSdpType.Answer);
        }

        IEnumerator ResendOfferCoroutine()
        {
            while (_runningResendCoroutine)
            {
                foreach (var pair in _mapConnectionIdAndPeer.Where(x => x.Value.waitingAnswer))
                {
                    _signaling.SendOffer(pair.Key, pair.Value.peer.LocalDescription);
                }

                yield return new WaitForSeconds(_resendInterval);
            }
        }

        void OnStart(ISignaling signaling)
        {
            if (!_runningResendCoroutine)
            {
                _runningResendCoroutine = true;
                _startCoroutine(ResendOfferCoroutine());
            }

            onStart?.Invoke();
        }

        void OnCreateConnection(ISignaling signaling, string connectionId, bool polite)
        {
            CreatePeerConnection(connectionId, polite);
            onCreatedConnection?.Invoke(connectionId);
        }

        void OnDestroyConnection(ISignaling signaling, string connectionId)
        {
            DeletePeerConnection(connectionId);
            onDeletedConnection?.Invoke(connectionId);
        }

        PeerConnection CreatePeerConnection(string connectionId, bool polite)
        {
            if (_mapConnectionIdAndPeer.TryGetValue(connectionId, out var peer))
            {
                peer.Dispose();
            }

            var pc = new RTCPeerConnection(ref _config);
            peer = new PeerConnection(pc, polite, _startCoroutine);
            _mapConnectionIdAndPeer[connectionId] = peer;
            peer.OnDataChannel = channel => { onAddChannel?.Invoke(connectionId, channel); };
            peer.OnIceCandidate = candidate => { _signaling.SendCandidate(connectionId, candidate); };
            peer.OnTrack = trackEvent => { onAddReceiver?.Invoke(connectionId, trackEvent.Receiver); };
            peer.OnIceConnectionChange = state => OnIceConnectionChange(connectionId, state);
            peer.OnSendOffer = description => { _signaling.SendOffer(connectionId, description); };
            peer.OnSendAnswer = description => { _signaling.SendAnswer(connectionId, description); };

            return peer;
        }

        void DeletePeerConnection(string connectionId)
        {
            _mapConnectionIdAndPeer[connectionId].Dispose();
            _mapConnectionIdAndPeer.Remove(connectionId);
        }

        void OnIceConnectionChange(string connectionId, RTCIceConnectionState state)
        {
            switch (state)
            {
                case RTCIceConnectionState.Connected:
                    onConnect?.Invoke(connectionId);
                    break;
                case RTCIceConnectionState.Disconnected:
                    onDisconnect?.Invoke(connectionId);
                    break;
            }
        }

        void OnAnswer(ISignaling signaling, DescData e)
        {
            var connectionId = e.connectionId;
            if (!_mapConnectionIdAndPeer.TryGetValue(connectionId, out var pc))
            {
                Debug.LogError($"connectionId:{connectionId}, peerConnection not exist");
                return;
            }

            pc.SetRemoteDescription(RTCSdpType.Answer, e.sdp, () => onGotAnswer?.Invoke(connectionId, e.sdp));
        }

        void OnIceCandidate(ISignaling signaling, CandidateData e)
        {
            if (!_mapConnectionIdAndPeer.TryGetValue(e.connectionId, out var pc))
            {
                return;
            }

            RTCIceCandidateInit option = new RTCIceCandidateInit
            {
                candidate = e.candidate, sdpMLineIndex = e.sdpMLineIndex, sdpMid = e.sdpMid
            };

            pc.OnGotCandidate(new RTCIceCandidate(option));
        }

        void OnOffer(ISignaling signaling, DescData e)
        {
            var connectionId = e.connectionId;
            if (!_mapConnectionIdAndPeer.TryGetValue(connectionId, out var pc))
            {
                pc = CreatePeerConnection(connectionId, e.polite);
            }

            pc.SetRemoteDescription(RTCSdpType.Offer, e.sdp, () => onGotOffer?.Invoke(connectionId, e.sdp));
        }
    }
}
