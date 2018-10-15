// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using HoloToolkit.Unity;
using UnityEngine;

namespace HoloToolkit.Sharing.Tests
{
    /// <summary>
    /// Test class for demonstrating how to send custom messages between clients.
    /// </summary>
    public class CustomMessages : Singleton<CustomMessages>
    {
        /// <summary>
        /// Message enum containing our information bytes to share.
        /// The first message type has to start with UserMessageIDStart
        /// so as not to conflict with HoloToolkit internal messages.
        /// </summary>
        public enum TestMessageID : byte
        {
            GameMaster = MessageID.UserMessageIDStart,
            TargetCollision,
            InitializeGame,
            TerminateGame,
            StartRound,
            EndPrelude,
            EndInterlude,
            EndRound,
            RoundInterval,
            PlayClip,
            StopClip,
            SpawnObject,
            DestroyObject,
            PosObject,
            RotObject,
            ScaleObject,
            ApplyVelocity,
            ApplyAngVelocity,
            ChangeMaterial,
            LoadMaterial,
            ChangeMesh,
            SetActive,
            ShowGallery,
            ShowLobby,
            Max
        }

        public enum UserMessageChannels
        {
            Anchors = MessageChannel.UserMessageChannelStart
        }

        /// <summary>
        /// Cache the local user's ID to use when sending messages
        /// </summary>
        public long LocalUserID
        {
            get; set;
        }

        public delegate void MessageCallback(NetworkInMessage msg);
        private Dictionary<TestMessageID, MessageCallback> messageHandlers = new Dictionary<TestMessageID, MessageCallback>();
        public Dictionary<TestMessageID, MessageCallback> MessageHandlers
        {
            get
            {
                return messageHandlers;
            }
        }

        /// <summary>
        /// Helper object that we use to route incoming message callbacks to the member
        /// functions of this class
        /// </summary>
        private NetworkConnectionAdapter connectionAdapter;

        /// <summary>
        /// Cache the connection object for the sharing service
        /// </summary>
        private NetworkConnection serverConnection;

        private void Start()
        {
            // SharingStage should be valid at this point, but we may not be connected.
            if (SharingStage.Instance.IsConnected)
            {
                Connected();
            }
            else
            {
                SharingStage.Instance.SharingManagerConnected += Connected;
            }
        }

        private void Connected(object sender = null, EventArgs e = null)
        {
            SharingStage.Instance.SharingManagerConnected -= Connected;
            InitializeMessageHandlers();
        }

        private void InitializeMessageHandlers()
        {
            SharingStage sharingStage = SharingStage.Instance;

            if (sharingStage == null)
            {
                Debug.Log("Cannot Initialize CustomMessages. No SharingStage instance found.");
                return;
            }

            serverConnection = sharingStage.Manager.GetServerConnection();
            if (serverConnection == null)
            {
                Debug.Log("Cannot initialize CustomMessages. Cannot get a server connection.");
                return;
            }

            connectionAdapter = new NetworkConnectionAdapter();
            connectionAdapter.MessageReceivedCallback += OnMessageReceived;

            // Cache the local user ID
            LocalUserID = SharingStage.Instance.Manager.GetLocalUser().GetID();

            for (byte index = (byte)TestMessageID.GameMaster; index < (byte)TestMessageID.Max; index++)
            {
                if (MessageHandlers.ContainsKey((TestMessageID)index) == false)
                {
                    MessageHandlers.Add((TestMessageID)index, null);
                }

                serverConnection.AddListener(index, connectionAdapter);
            }
        }

        private NetworkOutMessage CreateMessage(byte messageType)
        {
            NetworkOutMessage msg = serverConnection.CreateMessage(messageType);
            msg.Write(messageType);
            // Add the local userID so that the remote clients know whose message they are receiving
            msg.Write(LocalUserID);
            return msg;
        }

        public void SendPlayClip(string sourceID, int clip)
        {
            // If we are connected to a session, broadcast our head info
            if (serverConnection != null && serverConnection.IsConnected())
            {
                // Create an outgoing network message to contain all the info we want to send
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.PlayClip);

                msg.Write(new XString(sourceID));
                msg.Write(clip);

                // Send the message as a broadcast, which will cause the server to forward it to all other users in the session.
                serverConnection.Broadcast(
                    msg,
                    MessagePriority.Immediate,
                    MessageReliability.UnreliableSequenced,
                    MessageChannel.Avatar);
            }
        }

        public void SendStopClip(string sourceID)
        {
            // If we are connected to a session, broadcast our head info
            if (serverConnection != null && serverConnection.IsConnected())
            {
                // Create an outgoing network message to contain all the info we want to send
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.StopClip);

                msg.Write(new XString(sourceID));

                // Send the message as a broadcast, which will cause the server to forward it to all other users in the session.
                serverConnection.Broadcast(
                    msg,
                    MessagePriority.Immediate,
                    MessageReliability.UnreliableSequenced,
                    MessageChannel.Avatar);
            }
        }

        public void SendSpawnObject(string objectID, int prefab)
        {
            // If we are connected to a session, broadcast our head info
            if (serverConnection != null && serverConnection.IsConnected())
            {
                // Create an outgoing network message to contain all the info we want to send
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.SpawnObject);

                msg.Write(new XString(objectID));
                msg.Write(prefab);
                // Send the message as a broadcast, which will cause the server to forward it to all other users in the session.
                serverConnection.Broadcast(
                    msg,
                    MessagePriority.Immediate,
                    MessageReliability.UnreliableSequenced,
                    MessageChannel.Avatar);
            }
        }

        public void SendDestroyObject(string objectID)
        {
            // If we are connected to a session, broadcast our head info
            if (serverConnection != null && serverConnection.IsConnected())
            {
                // Create an outgoing network message to contain all the info we want to send
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.DestroyObject);

                msg.Write(new XString(objectID));

                // Send the message as a broadcast, which will cause the server to forward it to all other users in the session.
                serverConnection.Broadcast(
                    msg,
                    MessagePriority.Immediate,
                    MessageReliability.UnreliableSequenced,
                    MessageChannel.Avatar);
            }
        }

        public void SendPosObject(string objectID, Vector3 position)
        {
            // If we are connected to a session, broadcast our head info
            if (serverConnection != null && serverConnection.IsConnected())
            {
                // Create an outgoing network message to contain all the info we want to send
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.PosObject);

                msg.Write(new XString(objectID));
                AppendVector3(msg, position);

                // Send the message as a broadcast, which will cause the server to forward it to all other users in the session.
                serverConnection.Broadcast(
                    msg,
                    MessagePriority.Immediate,
                    MessageReliability.UnreliableSequenced,
                    MessageChannel.Avatar);
            }
        }

        public void SendRotObject(string objectID, Quaternion rotation)
        {
            // If we are connected to a session, broadcast our head info
            if (serverConnection != null && serverConnection.IsConnected())
            {
                // Create an outgoing network message to contain all the info we want to send
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.RotObject);

                msg.Write(new XString(objectID));
                AppendQuaternion(msg, rotation);

                // Send the message as a broadcast, which will cause the server to forward it to all other users in the session.
                serverConnection.Broadcast(
                    msg,
                    MessagePriority.Immediate,
                    MessageReliability.UnreliableSequenced,
                    MessageChannel.Avatar);
            }
        }

        public void SendScaleObject(string objectID, Vector3 scale)
        {
            // If we are connected to a session, broadcast our head info
            if (serverConnection != null && serverConnection.IsConnected())
            {
                // Create an outgoing network message to contain all the info we want to send
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.ScaleObject);

                msg.Write(new XString(objectID));
                AppendVector3(msg, scale);

                // Send the message as a broadcast, which will cause the server to forward it to all other users in the session.
                serverConnection.Broadcast(
                    msg,
                    MessagePriority.Immediate,
                    MessageReliability.UnreliableSequenced,
                    MessageChannel.Avatar);
            }
        }

        public void SendApplyVelocity(string objectID, Vector3 velocity)
        {
            // If we are connected to a session, broadcast our head info
            if (serverConnection != null && serverConnection.IsConnected())
            {
                // Create an outgoing network message to contain all the info we want to send
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.ApplyVelocity);

                msg.Write(new XString(objectID));
                AppendVector3(msg, velocity);

                // Send the message as a broadcast, which will cause the server to forward it to all other users in the session.
                serverConnection.Broadcast(
                    msg,
                    MessagePriority.Immediate,
                    MessageReliability.UnreliableSequenced,
                    MessageChannel.Avatar);
            }
        }

        public void SendApplyAngVelocity(string objectID, Vector3 angvelocity)
        {
            // If we are connected to a session, broadcast our head info
            if (serverConnection != null && serverConnection.IsConnected())
            {
                // Create an outgoing network message to contain all the info we want to send
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.ApplyAngVelocity);

                msg.Write(new XString(objectID));
                AppendVector3(msg, angvelocity);

                // Send the message as a broadcast, which will cause the server to forward it to all other users in the session.
                serverConnection.Broadcast(
                    msg,
                    MessagePriority.Immediate,
                    MessageReliability.UnreliableSequenced,
                    MessageChannel.Avatar);
            }
        }

        public void SendChangeMaterial(string objectID, int mat)
        {
            // If we are connected to a session, broadcast our head info
            if (serverConnection != null && serverConnection.IsConnected())
            {
                // Create an outgoing network message to contain all the info we want to send
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.ChangeMaterial);

                msg.Write(new XString(objectID));
                msg.Write(mat);

                // Send the message as a broadcast, which will cause the server to forward it to all other users in the session.
                serverConnection.Broadcast(
                    msg,
                    MessagePriority.Immediate,
                    MessageReliability.UnreliableSequenced,
                    MessageChannel.Avatar);
            }
        }

        public void SendLoadMaterial(string objectID, string mat)
        {
            // If we are connected to a session, broadcast our head info
            if (serverConnection != null && serverConnection.IsConnected())
            {
                // Create an outgoing network message to contain all the info we want to send
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.LoadMaterial);

                msg.Write(new XString(objectID));
                msg.Write(new XString(mat));

                // Send the message as a broadcast, which will cause the server to forward it to all other users in the session.
                serverConnection.Broadcast(
                    msg,
                    MessagePriority.Immediate,
                    MessageReliability.UnreliableSequenced,
                    MessageChannel.Avatar);
            }
        }

        public void SendChangeMesh(string objectID, int mesh)
        {
            // If we are connected to a session, broadcast our head info
            if (serverConnection != null && serverConnection.IsConnected())
            {
                // Create an outgoing network message to contain all the info we want to send
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.ChangeMesh);

                msg.Write(new XString(objectID));
                msg.Write(mesh);

                // Send the message as a broadcast, which will cause the server to forward it to all other users in the session.
                serverConnection.Broadcast(
                    msg,
                    MessagePriority.Immediate,
                    MessageReliability.UnreliableSequenced,
                    MessageChannel.Avatar);
            }
        }

        public void SendSetActive(string objectID, int active)
        {
            // If we are connected to a session, broadcast our head info
            if (serverConnection != null && serverConnection.IsConnected())
            {
                // Create an outgoing network message to contain all the info we want to send
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.SetActive);

                msg.Write(new XString(objectID));
                msg.Write(active);

                // Send the message as a broadcast, which will cause the server to forward it to all other users in the session.
                serverConnection.Broadcast(
                    msg,
                    MessagePriority.Immediate,
                    MessageReliability.UnreliableSequenced,
                    MessageChannel.Avatar);
            }
        }

        public void SendShowGallery()
        {
            if (serverConnection != null && serverConnection.IsConnected())
            {
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.ShowGallery);

                //msg.Write(userID);
                serverConnection.Broadcast(
                    msg,
                    MessagePriority.Immediate,
                    MessageReliability.UnreliableSequenced,
                    MessageChannel.Avatar);
            }
        }

        public void SendShowLobby()
        {
            if (serverConnection != null && serverConnection.IsConnected())
            {
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.ShowLobby);

                //msg.Write(userID);
                serverConnection.Broadcast(
                    msg,
                    MessagePriority.Immediate,
                    MessageReliability.UnreliableSequenced,
                    MessageChannel.Avatar);
            }
        }

        public void SendGameMaster()
        {
            if (serverConnection != null && serverConnection.IsConnected())
            {
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.GameMaster);

                //msg.Write(userID);
                serverConnection.Broadcast(
                    msg,
                    MessagePriority.Immediate,
                    MessageReliability.UnreliableSequenced,
                    MessageChannel.Avatar);
            }
        }

        public void SendTargetCollision(long playerID)
        {
            if (serverConnection != null && serverConnection.IsConnected())
            {
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.TargetCollision);
                msg.Write(playerID);
                serverConnection.Broadcast(
                    msg,
                    MessagePriority.Immediate,
                    MessageReliability.UnreliableSequenced,
                    MessageChannel.Avatar);
            }
        }

        public void SendInitializeGame()
        {
            NetworkOutMessage msg = CreateMessage((byte)TestMessageID.InitializeGame);

            serverConnection.Broadcast(
                msg,
                MessagePriority.Immediate,
                MessageReliability.UnreliableSequenced,
                MessageChannel.Avatar);
        }

        public void SendTerminateGame()
        {
            NetworkOutMessage msg = CreateMessage((byte)TestMessageID.TerminateGame);

            serverConnection.Broadcast(
                msg,
                MessagePriority.Immediate,
                MessageReliability.UnreliableSequenced,
                MessageChannel.Avatar);
        }

        public void SendStartRound()
        {
            NetworkOutMessage msg = CreateMessage((byte)TestMessageID.StartRound);

            serverConnection.Broadcast(
                msg,
                MessagePriority.Immediate,
                MessageReliability.UnreliableSequenced,
                MessageChannel.Avatar);
        }

        public void SendEndPrelude()
        {
            NetworkOutMessage msg = CreateMessage((byte)TestMessageID.EndPrelude);

            serverConnection.Broadcast(
                msg,
                MessagePriority.Immediate,
                MessageReliability.UnreliableSequenced,
                MessageChannel.Avatar);
        }

        public void SendEndInterlude()
        {
            NetworkOutMessage msg = CreateMessage((byte)TestMessageID.EndInterlude);

            serverConnection.Broadcast(
                msg,
                MessagePriority.Immediate,
                MessageReliability.UnreliableSequenced,
                MessageChannel.Avatar);
        }

        public void SendEndRound()
        {
            NetworkOutMessage msg = CreateMessage((byte)TestMessageID.EndRound);

            serverConnection.Broadcast(
                msg,
                MessagePriority.Immediate,
                MessageReliability.UnreliableSequenced,
                MessageChannel.Avatar);
        }

        public void SendRoundInterval()
        {
            NetworkOutMessage msg = CreateMessage((byte)TestMessageID.RoundInterval);

            serverConnection.Broadcast(
                msg,
                MessagePriority.Immediate,
                MessageReliability.UnreliableSequenced,
                MessageChannel.Avatar);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (serverConnection != null)
            {
                for (byte index = (byte)TestMessageID.GameMaster; index < (byte)TestMessageID.Max; index++)
                {
                    serverConnection.RemoveListener(index, connectionAdapter);
                }
                connectionAdapter.MessageReceivedCallback -= OnMessageReceived;
            }
        }

        private void OnMessageReceived(NetworkConnection connection, NetworkInMessage msg)
        {
            byte messageType = msg.ReadByte();
            MessageCallback messageHandler = MessageHandlers[(TestMessageID)messageType];
            if (messageHandler != null)
            {
                messageHandler(msg);
            }
        }

        #region HelperFunctionsForWriting

        private void AppendTransform(NetworkOutMessage msg, Vector3 position, Quaternion rotation)
        {
            AppendVector3(msg, position);
            AppendQuaternion(msg, rotation);
        }

        private void AppendVector3(NetworkOutMessage msg, Vector3 vector)
        {
            msg.Write(vector.x);
            msg.Write(vector.y);
            msg.Write(vector.z);
        }

        private void AppendQuaternion(NetworkOutMessage msg, Quaternion rotation)
        {
            msg.Write(rotation.x);
            msg.Write(rotation.y);
            msg.Write(rotation.z);
            msg.Write(rotation.w);
        }

        #endregion

        #region HelperFunctionsForReading

        public Vector3 ReadVector3(NetworkInMessage msg)
        {
            return new Vector3(msg.ReadFloat(), msg.ReadFloat(), msg.ReadFloat());
        }

        public Quaternion ReadQuaternion(NetworkInMessage msg)
        {
            return new Quaternion(msg.ReadFloat(), msg.ReadFloat(), msg.ReadFloat(), msg.ReadFloat());
        }

        #endregion
    }
}