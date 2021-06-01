using System;
using System.Collections;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.RenderStreaming
{
    internal class PeerConnection : IDisposable
    {
        public Action<RTCTrackEvent> OnTrack;
        public Action<RTCDataChannel> OnDataChannel;
        public Action<RTCIceCandidate> OnIceCandidate;
        public Action<RTCIceConnectionState> OnIceConnectionChange;
        public Action<RTCSessionDescription> OnSendOffer;
        public Action<RTCSessionDescription> OnSendAnswer;

        public readonly RTCPeerConnection peer;
        public bool waitingAnswer { get; private set; }

        private readonly bool polite;
        private readonly Func<IEnumerator, Coroutine> startCoroutine;

        private bool ignoreOffer;
        private bool srdPending;
        private bool sldPending;

        public PeerConnection(RTCPeerConnection peer, bool polite, Func<IEnumerator, Coroutine> startCoroutine)
        {
            this.peer = peer;
            this.polite = polite;
            this.startCoroutine = startCoroutine;

            this.peer.OnTrack = trackEvent => OnTrack?.Invoke(trackEvent);
            this.peer.OnDataChannel = channel => OnDataChannel?.Invoke(channel);
            this.peer.OnIceCandidate = candidate => OnIceCandidate?.Invoke(candidate);
            this.peer.OnIceConnectionChange = state => OnIceConnectionChange?.Invoke(state);
            this.peer.OnNegotiationNeeded = () => SetLocalDescription(RTCSdpType.Offer);
        }

        public bool IsStable()
        {
            if (waitingAnswer || sldPending || srdPending)
            {
                return false;
            }

            return peer.SignalingState == RTCSignalingState.Stable;
        }

        public void SetRemoteDescription(RTCSdpType sdpType, string sdp, Action end = null)
        {
            startCoroutine(SetRemoteDescriptionCoroutine(sdpType, sdp, end));
        }

        IEnumerator SetRemoteDescriptionCoroutine(RTCSdpType sdpType, string sdp, Action end)
        {
            RTCSessionDescription description;
            description.type = sdpType;
            description.sdp = sdp;

            var isStable = peer.SignalingState == RTCSignalingState.Stable ||
                           (peer.SignalingState == RTCSignalingState.HaveLocalOffer && srdPending);
            ignoreOffer = description.type == RTCSdpType.Offer && !polite && (sldPending || !isStable);

            if (ignoreOffer)
            {
                Debug.LogWarning($"{this} glare - ignoreOffer {nameof(peer.SignalingState)}:{peer.SignalingState}");
                yield break;
            }

            yield return new WaitWhile(() => sldPending || srdPending);

            waitingAnswer = false;
            srdPending = true;

            var remoteDescOp = peer.SetRemoteDescription(ref description);
            yield return remoteDescOp;

            if (remoteDescOp.IsError)
            {
                srdPending = false;
                Debug.LogError($"{this} {remoteDescOp.Error.message}");
                yield break;
            }

            srdPending = false;

            Assert.AreEqual(peer.RemoteDescription.type, description.type, $"{this} SRD worked");
            Assert.AreEqual(peer.SignalingState,
                description.type == RTCSdpType.Offer ? RTCSignalingState.HaveRemoteOffer : RTCSignalingState.Stable,
                $"{this} SignalingState　is incorrect");

            end?.Invoke();
        }

        public void SetLocalDescription(RTCSdpType sdpType)
        {
            startCoroutine(SetLocalDescriptionCoroutine(sdpType));
        }

        IEnumerator SetLocalDescriptionCoroutine(RTCSdpType sdpType)
        {
            yield return new WaitWhile(() => sldPending || srdPending);

            if (sdpType == RTCSdpType.Offer)
            {
                Assert.AreEqual(peer.SignalingState, RTCSignalingState.Stable,
                    $"{this} negotiationneeded always fires in stable state");
                Assert.AreEqual(sldPending, false, $"{this} negotiationneeded not already in progress");
            }

            sldPending = true;
            var localDescOp = peer.SetLocalDescription();
            yield return localDescOp;
            if (localDescOp.IsError)
            {
                sldPending = false;
                Debug.LogError($"{this} {localDescOp.Error.message}");
                yield break;
            }

            Assert.AreEqual(peer.LocalDescription.type, sdpType, $"{this} SLD worked");
            Assert.AreEqual(peer.SignalingState,
                sdpType == RTCSdpType.Offer ? RTCSignalingState.HaveLocalOffer : RTCSignalingState.Stable,
                $"{this} SignalingState　is incorrect");

            sldPending = false;

            if (sdpType == RTCSdpType.Offer)
            {
                waitingAnswer = true;
                OnSendOffer?.Invoke(peer.LocalDescription);
            }
            else
            {
                OnSendAnswer?.Invoke(peer.LocalDescription);
            }
        }

        public void OnGotCandidate(RTCIceCandidate candidate)
        {
            if (!peer.AddIceCandidate(candidate) && !ignoreOffer)
            {
                Debug.LogWarning($"{this} this candidate can't accept current signaling state {peer.SignalingState}.");
            }
        }

        ~PeerConnection()
        {
            Dispose();
        }

        public override string ToString()
        {
            var str = polite ? "polite" : "impolite";
            return $"[{str}-{base.ToString()}]";
        }

        public void Dispose()
        {
            if (peer == null)
            {
                return;
            }

            peer.OnTrack = null;
            peer.OnDataChannel = null;
            peer.OnIceCandidate = null;
            peer.OnNegotiationNeeded = null;
            peer.OnConnectionStateChange = null;
            peer.OnIceConnectionChange = null;
            peer.OnIceGatheringStateChange = null;
            peer.Dispose();
        }
    }
}
