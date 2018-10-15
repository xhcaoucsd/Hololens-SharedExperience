
using System;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;

namespace HoloToolkit.Sharing.Tests
{
    /// <summary>
    /// Broadcasts the head transform of the local user to other users in the session,
    /// and adds and updates the head transforms of remote users.
    /// Head transforms are sent and received in the local coordinate space of the GameObject this component is on.
    /// </summary>
    public class AudioManager : Singleton<AudioManager>
    {
        public bool Started {get; private set;}

        public enum AudioClips: int
        {
            Pop,
            Success,
            Fail,
            Ribbit,
            Dink
        }

        public List<AudioClip> audioClips;

        private void Start()
        {
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.PlayClip] = HandlePlayClip;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.StopClip] = HandleStopClip;

            // SharingStage should be valid at this point, but we may not be connected.
            if (SharingStage.Instance.IsConnected)
            {
                Connected();
            }
            else
            {
                SharingStage.Instance.SharingManagerConnected += Connected;
            }

            Started = true;
        }

        public void Connected(object sender = null, EventArgs e = null)
        {
            SharingStage.Instance.SharingManagerConnected -= Connected;
        }

        public void PlayClip(string sourceID, int clip, bool broadcast=false)
        {
            if (broadcast)
                CustomMessages.Instance.SendPlayClip(sourceID, clip);

            GameObject source;

            if (ObjectManager.Instance.sharedObjects.TryGetValue(sourceID, out source)
                && source.GetComponent<AudioSource>())
            {
                source.GetComponent<AudioSource>().clip = audioClips[clip];
                source.GetComponent<AudioSource>().Play();
            }
        }

        public void StopClip(string sourceID, bool broadcast = false)
        {
            if (GameManager.Instance.ImGameMaster() && broadcast)
                CustomMessages.Instance.SendStopClip(sourceID);

            GameObject source;

            if (ObjectManager.Instance.sharedObjects.TryGetValue(sourceID, out source)
                && source.GetComponent<AudioSource>()) 
                source.GetComponent<AudioSource>().Stop();
        }

        public void HandlePlayClip(NetworkInMessage msg)
        {
            // Parse the message
            long senderID = msg.ReadInt64();
            string sourceID = msg.ReadString().ToString();
            int clip = msg.ReadInt32();

            PlayClip(sourceID, clip);
        }

        public void HandleStopClip(NetworkInMessage msg)
        {
            // Parse the message
            long senderID = msg.ReadInt64();
            string sourceID = msg.ReadString().ToString();

            StopClip(sourceID);
        }
    }
}