// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    public class HeadManager : Singleton<HeadManager>
    {
        public GameObject p_Player;

        /// <summary>
        /// Keep a list of the remote heads, indexed by XTools userID
        /// </summary>
        public Dictionary<long, string> currentPlayers = new Dictionary<long, string>();
        
        public bool Started { get; private set; }

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

            Started = true;
        }

        public void Initialize()
        {
            GetPlayerInfo(GameManager.Instance.GetMyID());
        }

        private void Connected(object sender = null, EventArgs e = null)
        {
            SharingStage.Instance.SharingManagerConnected -= Connected;

            SharingStage.Instance.SessionUsersTracker.UserJoined += UserJoinedSession;
            SharingStage.Instance.SessionUsersTracker.UserLeft += UserLeftSession;
        }

        // called 60(?) times a second to check progress of game and switch between game phases
        private void Update()
        {
            // Grab the current head transform and broadcast it to all the other users in the session
            Transform headTransform = Camera.main.transform;

            // Transform the head position and rotation from world space into local space
            Vector3 headPosition = transform.InverseTransformPoint(headTransform.position);
            Quaternion headRotation = Quaternion.Inverse(transform.rotation) * headTransform.rotation;

            string objectID;
            if (currentPlayers.TryGetValue(GameManager.Instance.GetMyID(), out objectID))
            {
                ObjectManager.Instance.PosObject(objectID, headPosition, broadcast: true);
                ObjectManager.Instance.RotObject(objectID, headRotation, broadcast: true);
            }
        }

        protected override void OnDestroy()
        {
            if (SharingStage.Instance != null)
            {
                if (SharingStage.Instance.SessionUsersTracker != null)
                {
                    SharingStage.Instance.SessionUsersTracker.UserJoined -= UserJoinedSession;
                    SharingStage.Instance.SessionUsersTracker.UserLeft -= UserLeftSession;
                }
            }

            base.OnDestroy();
        }

        /// <summary>
        /// Called when a new user is leaving the current session.
        /// </summary>
        /// <param name="user">User that left the current session.</param>
        private void UserLeftSession(User user)
        {
            int userID = user.GetID();
            if (userID != SharingStage.Instance.Manager.GetLocalUser().GetID())
            {
                string objectID;
                if (currentPlayers.TryGetValue(userID, out objectID)) {
                    ObjectManager.Instance.DestroyObject(objectID);
                    currentPlayers.Remove(userID);
                }
            }
        }

        /// <summary>
        /// Called when a user is joining the current session.
        /// </summary>
        /// <param name="user">User that joined the current session.</param>
        private void UserJoinedSession(User user)
        {
            if (user.GetID() != SharingStage.Instance.Manager.GetLocalUser().GetID())
            {
                GetPlayerInfo(user.GetID());
            }
        }

        /// <summary>
        /// Gets the data structure for the remote users' head position.
        /// </summary>
        /// <param name="userId">User ID for which the remote head info should be obtained.</param>
        /// <returns>RemoteHeadInfo for the specified user.</returns>
        public string GetPlayerInfo(long userID)
        {
            string objectID;

            // Get the head info if its already in the list, otherwise add it
            if (!currentPlayers.TryGetValue(userID, out objectID)) 
            {
                
                objectID = CreatePlayer(userID);

                GameObject player;
                if (ObjectManager.Instance.sharedObjects.TryGetValue(objectID, out player))
                    player.GetComponent<Player>().PlayerID = userID;

                currentPlayers.Add(userID, objectID);
            }

            return objectID;
        }

       /* /// <summary>
        /// Called when a remote user sends a head transform.
        /// </summary>
        /// <param name="msg"></param>
        private void HandleUpdatePlayer(NetworkInMessage msg)
        {
            // Parse the message
            long userID = msg.ReadInt64();

            Vector3 playerPos = CustomMessages.Instance.ReadVector3(msg);

            Quaternion playerRot = CustomMessages.Instance.ReadQuaternion(msg);

            PlayerInfo playerInfo = GetPlayerInfo(userID);
            playerInfo.PlayerObject.transform.localPosition = playerPos;
            playerInfo.PlayerObject.transform.localRotation = playerRot;

        }*/

        /// <summary>
        /// Creates a new game object to represent the user's head.
        /// </summary>
        /// <returns></returns>
        ///     
        private string CreatePlayer(long userID)
        {
            string objectID = "player" + userID.ToString();
            ObjectManager.Instance.SpawnObject(objectID, (int)ObjectManager.Prefabs.Player);
            ObjectManager.Instance.ScaleObject(objectID, Vector3.one * 0.2f);
            return objectID;
        }

        /// <summary>
        /// When a user has left the session this will cleanup their
        /// head data.
        /// </summary>
        /// <param name="remoteHeadObject"></param>
        private void RemovePlayer(GameObject remotePlayerObject)
        {
            DestroyImmediate(remotePlayerObject);
        }

        public Dictionary<long, string> getCurrentPlayers()
        {
            return currentPlayers;
        }
    }
}