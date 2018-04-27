using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HoloToolkit.Unity;
using HoloToolkit.Unity.InputModule;

namespace HoloToolkit.Sharing.Tests
{
    /// <summary>
    /// Broadcasts the head transform of the local user to other users in the session,
    /// and adds and updates the head transforms of remote users.
    /// Head transforms are sent and received in the local coordinate space of the GameObject this component is on.
    /// </summary>
    public class GameManager : Singleton<GameManager>, ISpeechHandler
    {
        private readonly int PRELUDE = 0;
        private readonly int INTERLUDE = 1;
        private readonly int POSTLUDE = 2;

        public class PlayerInfo
        {
            public long UserID;
            public GameObject PlayerObject;
        }

        private long gameMasterID = 0;
        public Material materialGameMaster;
        public Material materialNormal;
        public Material materialSurvived;
        public Material materialDead;
        public Material materialTargetCollided;
        public float targetSpawnRadius;
        public float delayPrelude;
        public float delayPostlude;


        public GameObject p_Target;
        public GameObject p_Player;
        public GameObject infoDisplay;

        public Dictionary<long, PlayerInfo> remotePlayers = new Dictionary<long, PlayerInfo>();
        private Dictionary<string, GameObject> sharedObjects = new Dictionary<string, GameObject>();

        private PlayerInfo localPlayer;
        private bool inGame;
        private int gameState;
        private bool playerOut;
        private int maxRounds;
        private int maxSafePlayers;
        private int completedRounds;
        private int playersTotal;
        private int playersLeft;
        private float delayInterval;
        private List<long> allPlayers;
        private List<long> alivePlayers;
        private List<long> safePlayers;

        private void Start()
        {
            InputManager.Instance.PushModalInputHandler(gameObject);

            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.HeadTransform] = HandleUpdatePlayer;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.GameMaster] = HandleUpdateGameMaster;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.SpawnTarget] = HandleSpawnTarget;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.TargetCollision] = HandleTargetCollision;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.InitializeGame] = HandleInitializeGame;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.StartRound] = HandleStartRound;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.EndPrelude] = HandleEndPrelude;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.EndInterlude] = HandleEndInterlude;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.EndRound] = HandleEndRound;

            // SharingStage should be valid at this point, but we may not be connected.
            if (SharingStage.Instance.IsConnected)
            {
                Connected();
            }
            else
            {
                SharingStage.Instance.SharingManagerConnected += Connected;
            }

            localPlayer = new PlayerInfo();
            localPlayer.UserID = SharingStage.Instance.Manager.GetLocalUser().GetID();
            localPlayer.PlayerObject = CreateRemotePlayer();

            allPlayers = new List<long>();
            alivePlayers = new List<long>();
            safePlayers = new List<long>();

            infoDisplay.GetComponent<Text>().text = "Game not started";
        }

        private void Connected(object sender = null, EventArgs e = null)
        {
            SharingStage.Instance.SharingManagerConnected -= Connected;

            SharingStage.Instance.SessionUsersTracker.UserJoined += UserJoinedSession;
            SharingStage.Instance.SessionUsersTracker.UserLeft += UserLeftSession;
        }

        private void Update()
        {
            // Grab the current head transform and broadcast it to all the other users in the session
            Transform headTransform = Camera.main.transform;

            // Transform the head position and rotation from world space into local space
            Vector3 headPosition = transform.InverseTransformPoint(headTransform.position); //I ADDED THE SPAWNER PART- GameObject.FindGameObjectWithTag("spawner").transform.position
            Quaternion headRotation = Quaternion.Inverse(transform.rotation) * headTransform.rotation;


            CustomMessages.Instance.SendHeadTransform(headPosition, headRotation);

            localPlayer.PlayerObject.transform.position = headPosition;
            localPlayer.PlayerObject.transform.rotation = headRotation;
            if (ImGameMaster())
                localPlayer.PlayerObject.GetComponent<Renderer>().sharedMaterial = materialGameMaster;
            else
                localPlayer.PlayerObject.GetComponent<Renderer>().sharedMaterial = materialNormal;


            

            if (ImGameMaster() && inGame)
            {
                switch (gameState)
                {
                    case 0:
                        if (Time.time > delayInterval)
                            EndPrelude(broadcast: true);
                        break;
                    case 1:
                        if (safePlayers.Count >= maxSafePlayers)
                            EndInterlude(broadcast: true);
                        break;
                    case 2:
                        if (Time.time > delayInterval)
                            EndRound(broadcast: true);
                        break;
                    default:
                        break;
                }
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
            int userId = user.GetID();
            if (userId != SharingStage.Instance.Manager.GetLocalUser().GetID())
            {
                RemovePlayer(remotePlayers[userId].PlayerObject);
                remotePlayers.Remove(userId);
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
        public PlayerInfo GetPlayerInfo(long userId)
        {
            PlayerInfo playerInfo;

            // Get the head info if its already in the list, otherwise add it
            if (!remotePlayers.TryGetValue(userId, out playerInfo))
            {
                playerInfo = new PlayerInfo();
                playerInfo.UserID = userId;
                playerInfo.PlayerObject = CreateRemotePlayer();

                remotePlayers.Add(userId, playerInfo);
            }

            return playerInfo;
        }
        
        /// <summary>
        /// Creates a new game object to represent the user's head.
        /// </summary>
        /// <returns></returns>
        ///     
        private GameObject CreateRemotePlayer()
        {
            GameObject newPlayerObj = Instantiate(p_Player, gameObject.transform);
            newPlayerObj.tag = "player";
            newPlayerObj.transform.parent = gameObject.transform;
            newPlayerObj.transform.localScale = Vector3.one * 0.2f;
            return newPlayerObj;
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

        public void OnSpeechKeywordRecognized(SpeechKeywordRecognizedEventData eventData)
        {
            switch (eventData.RecognizedText.ToLower())
            {
                case "initialize":
                    Debug.Log("Sending message");
                    AssumeGameMaster();
                    break;
                case "start":
                    PlayGame();
                    break;
                default:
                    break;
            }
        }


        private void SpawnTarget()
        {
            if (ImGameMaster())
            {
                Transform headTransform = Camera.main.transform;
                Vector3 spawnPosition = headTransform.position + headTransform.forward * 3;

                spawnPosition = transform.InverseTransformPoint(spawnPosition); //I ADDED THE SPAWNER PART- GameObject.FindGameObjectWithTag("spawner").transform.position
                Quaternion spawnRotation = Quaternion.Inverse(transform.rotation) * headTransform.rotation;

                CustomMessages.Instance.SendSpawnTarget(spawnPosition, spawnRotation);

                GameObject target;
                if (!sharedObjects.TryGetValue("target", out target))
                {
                    target = Instantiate(p_Target, gameObject.transform);
                    //target.transform.localScale = Vector3.one * 0.2f;
                    sharedObjects.Add("target", target);
                }

                target.transform.localPosition = spawnPosition;
                target.transform.localRotation = spawnRotation;
            }
        }

        // TO DO WRITE UP COLLIDER CODE
        public void TargetCollision(long colliderID, bool broadcast=false)
        {
            if (broadcast && ImGameMaster())
                CustomMessages.Instance.SendTargetCollision(colliderID);

            infoDisplay.GetComponent<Text>().text = "Target Collided!";

            GameObject target;
            if (sharedObjects.TryGetValue("target", out target))
            {
                target.GetComponent<Renderer>().sharedMaterial = materialTargetCollided;
            }
            if (alivePlayers.Contains(colliderID) && safePlayers.Count < maxSafePlayers && !safePlayers.Contains(colliderID))
            {
                safePlayers.Add(colliderID);
                if (IDisMine(colliderID))
                {
                    infoDisplay.GetComponent<Text>().text = "You're safe!";
                }
            }
        }

        private void StartRound(bool broadcast=false)
        {
            if (broadcast && ImGameMaster())
                CustomMessages.Instance.SendStartRound();
            //gameState = PRELUDE;
            gameState = INTERLUDE;
            delayInterval = Time.time + delayPrelude;
            maxSafePlayers = maxRounds - completedRounds;
            safePlayers.Clear();

            if (ImGameMaster())
            {
                Vector3 targetPos = GetRandomPos(targetSpawnRadius);
                Quaternion targetRot = GetRandomRot();

                CustomMessages.Instance.SendSpawnTarget(targetPos, targetRot);
                GameObject target;
                if (!sharedObjects.TryGetValue("target", out target))
                {
                    target = Instantiate(p_Target, gameObject.transform);
                    //target.transform.localScale = Vector3.one * 0.2f;
                    sharedObjects.Add("target", target);
                }

                target.transform.localPosition = targetPos;
                target.transform.localRotation = targetRot;
            }

        }

        private void EndPrelude(bool broadcast=false)
        {
            if (broadcast && ImGameMaster())
                CustomMessages.Instance.SendEndPrelude();

            gameState = INTERLUDE;
        }

        private void EndInterlude(bool broadcast=false)
        {
            if (broadcast && ImGameMaster())
                CustomMessages.Instance.SendEndInterlude();

            foreach (long playerID in alivePlayers)
            {
                if (!safePlayers.Contains(playerID))
                {
                    alivePlayers.Remove(playerID);

                    PlayerInfo playerInfo;
                    if (remotePlayers.TryGetValue(playerID, out playerInfo))
                        playerInfo.PlayerObject.GetComponent<Renderer>().sharedMaterial = materialDead;
                    
                
                    if (IDisMine(playerID))
                        infoDisplay.GetComponent<Text>().text = "Dead";
                }
                else
                {
                    PlayerInfo playerInfo;
                    if (remotePlayers.TryGetValue(playerID, out playerInfo))
                        playerInfo.PlayerObject.GetComponent<Renderer>().sharedMaterial = materialSurvived;
                   
                    if (IDisMine(playerID))
                        infoDisplay.GetComponent<Text>().text = "Survived";
                }

            }

            gameState = POSTLUDE;

            delayInterval = Time.time + delayPostlude;

        }

        private void EndRound(bool broadcast=false)
        {
            if (broadcast && ImGameMaster())
                CustomMessages.Instance.SendEndRound();

            GameObject target;

            if (sharedObjects.TryGetValue("target", out target))
            {
                sharedObjects.Remove("target");
                Destroy(target);
            }
            
            foreach(long playerID in alivePlayers)
            {
                PlayerInfo playerInfo;
                if (remotePlayers.TryGetValue(playerID, out playerInfo))
                    playerInfo.PlayerObject.GetComponent<Renderer>().sharedMaterial = materialNormal;
          

                if (IDisMine(playerID))
                    infoDisplay.GetComponent<Text>().text = "In game";
            }

            completedRounds++;

            /*if (ImGameMaster() && completedRounds < maxRounds)
                StartRound(true);
            */
            gameState = -1;
        }

        private void InitializeGame(bool broadcast=false)
        {
            if (broadcast && ImGameMaster())
                CustomMessages.Instance.SendInitializeGame();

            if (inGame)
                return;

            if (remotePlayers.Keys.Count < 2)
                return;

            playersTotal = remotePlayers.Keys.Count;

            allPlayers.Clear();
            alivePlayers.Clear();
            safePlayers.Clear();
            
            foreach(long userID in remotePlayers.Keys)
            {
                allPlayers.Add(userID);
                alivePlayers.Add(userID);
            }

            inGame = true;
            completedRounds = 0;
            playersLeft = playersTotal;
            maxRounds = playersTotal - 1;

            infoDisplay.GetComponent<Text>().text = "In game";
        }

        private void PlayGame()
        {
            if (!ImGameMaster())
                return;

            InitializeGame(broadcast: true);
            StartRound(broadcast: true);

            
        }

        /// <summary>
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

            if (IsGameMaster(userID))
                playerInfo.PlayerObject.GetComponent<Renderer>().sharedMaterial = materialGameMaster;
            else
                playerInfo.PlayerObject.GetComponent<Renderer>().sharedMaterial = materialNormal;

        }

        private void HandleSpawnTarget(NetworkInMessage msg)
        {
            long userID = msg.ReadInt64();

            Vector3 targetPos = CustomMessages.Instance.ReadVector3(msg);

            Quaternion targetRot = CustomMessages.Instance.ReadQuaternion(msg);

            GameObject target;
            if (!sharedObjects.TryGetValue("target", out target))
            {
                target = Instantiate(p_Target, gameObject.transform);
                //target.transform.localScale = Vector3.one * 0.2f;
                sharedObjects.Add("target", target);
            }

            target.transform.localPosition = targetPos;
            target.transform.localRotation = targetRot;
        }

        private void HandleTargetCollision(NetworkInMessage msg)
        {
            msg.ReadInt64();

            TargetCollision(msg.ReadInt64());  
        }


        private void HandleInitializeGame(NetworkInMessage msg)
        {
            InitializeGame();
        }

        private void HandleStartRound(NetworkInMessage msg)
        {
            StartRound();
        }

        private void HandleEndPrelude(NetworkInMessage msg)
        {
            EndPrelude();
        }

        private void HandleEndInterlude(NetworkInMessage msg)
        {
            EndInterlude();
        }

        private void HandleEndRound(NetworkInMessage msg)
        {
            EndRound();
        }

        private void HandleUpdateGameMaster(NetworkInMessage msg)
        {
            Debug.Log("GM RECIEVED");
            long userID = msg.ReadInt64();
            if (IsGameMasterFree())
                gameMasterID = userID;
        }

        private bool IsGameMaster(long userID)
        {
            return userID == gameMasterID;
        }

        public bool ImGameMaster()
        {
            return IDisMine(gameMasterID);
        }

        private bool IDisMine(long userID)
        {
            return SharingStage.Instance.Manager.GetLocalUser().GetID() == userID;
        }
        private bool IsGameMasterFree()
        {
            return gameMasterID == 0;
        }

        private void AssumeGameMaster()
        {
            if (IsGameMasterFree())
            {
                gameMasterID = SharingStage.Instance.Manager.GetLocalUser().GetID();

                CustomMessages.Instance.SendGameMaster();
            }
        }

        private Vector3 GetRandomPos(float radius)
        {
            return new Vector3(UnityEngine.Random.Range(0f, radius), 0, UnityEngine.Random.Range(0f, radius));
        }

        private Quaternion GetRandomRot()
        {
            return Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
        }
    }
}
