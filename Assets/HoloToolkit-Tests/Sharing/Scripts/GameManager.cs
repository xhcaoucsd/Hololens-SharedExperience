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
    /// 
    /// Performs all functions of 'musical chairs' game by designating a Game Master player and sending messages
    /// between players and the game master. Add to this later
    /// </summary>
    public class GameManager : Singleton<GameManager>, ISpeechHandler
    {
        private readonly int PRELUDE = 0;
        private readonly int INTERLUDE = 1;
        private readonly int POSTLUDE = 2;

        /// <summary>
        /// Contains information on a player in the game. ID used to identify player when actions happen
        /// and PlayerObject contains info on the player's rendered object in Unity (position, prefab, etc)
        /// </summary>
        public class PlayerInfo
        {
            public long UserID;
            public GameObject PlayerObject;
        }

        private long gameMasterID = 0;
        public Material materialGameMaster; // material specifies who is the game master
        public Material materialNormal;
        public Material materialSurvived; // material of a survived player
        public Material materialDead; // material of a dead player
        public Material materialTargetCollided; // material of the collision object (in this case the capsule) when something collides with it
        public float targetSpawnRadius; // radius in which collision object can be spawned
        public float delayPrelude; 
        public float delayPostlude; 


        public GameObject p_Target; 
        public GameObject p_Player;
        public GameObject infoDisplay;

        public Dictionary<long, PlayerInfo> remotePlayers = new Dictionary<long, PlayerInfo>(); // contains list of players connected to server
        private Dictionary<string, GameObject> sharedObjects = new Dictionary<string, GameObject>(); 

        private PlayerInfo localPlayer; // PlayerInfo object for testing on local host (using our computer/unity instead of hololens)
        private bool inGame; 
        private int gameState;
        private bool playerOut;
        private int maxRounds;
        private int maxSafePlayers;
        private int completedRounds;
        private int playersTotal;
        private int playersLeft;
        private float delayInterval;
        private List<long> allPlayers; // list of all players
        private List<long> alivePlayers; // list of players still alive
        private List<long> safePlayers; // list of players who have entered the collision object in a current round

        private bool resourceLocked = false;

        private void Start()
        {
            InputManager.Instance.PushModalInputHandler(gameObject);

            // add handlers for each type of message sent
            // if a message is sent with that specific type, the corresponding handler method will be called
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.HeadTransform] = HandleUpdatePlayer;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.GameMaster] = HandleUpdateGameMaster;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.SpawnTarget] = HandleSpawnTarget;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.TargetCollision] = HandleTargetCollision;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.InitializeGame] = HandleInitializeGame;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.StartRound] = HandleStartRound;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.EndPrelude] = HandleEndPrelude;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.EndInterlude] = HandleEndInterlude;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.EndRound] = HandleEndRound;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.SafeSignal] = HandleSafeSignal;

            // SharingStage should be valid at this point, but we may not be connected.
            if (SharingStage.Instance.IsConnected)
            {
                Connected();
            }
            else
            {
                SharingStage.Instance.SharingManagerConnected += Connected;
            }

            // create PlayerInfo object for local player
            localPlayer = new PlayerInfo();
            // get ID for local player
            localPlayer.UserID = SharingStage.Instance.Manager.GetLocalUser().GetID();
            // create remote player version of local player to add to remotePlayers list
            localPlayer.PlayerObject = CreateRemotePlayer();
            // putting local player in remote players list allows local player to be the same as a player connected wirelessly through the hololens
            remotePlayers.Add(localPlayer.UserID, localPlayer);

            // initialize appropriate lists
            allPlayers = new List<long>();
            alivePlayers = new List<long>();
            safePlayers = new List<long>();

            // each player starts with a display of their own ID (for testing purposes)
            infoDisplay.GetComponent<Text>().text = "Start: " + localPlayer.UserID;
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
            Vector3 headPosition = transform.InverseTransformPoint(headTransform.position); //I ADDED THE SPAWNER PART- GameObject.FindGameObjectWithTag("spawner").transform.position
            Quaternion headRotation = Quaternion.Inverse(transform.rotation) * headTransform.rotation;

            // every player's sends info on their position to update constantly
            CustomMessages.Instance.SendHeadTransform(headPosition, headRotation);

            // update own player's position/rotation
            localPlayer.PlayerObject.transform.position = headPosition;
            localPlayer.PlayerObject.transform.rotation = headRotation;

            // render Game Master as different material from other players
            if (ImGameMaster())
                localPlayer.PlayerObject.GetComponent<Renderer>().sharedMaterial = materialGameMaster;
            else
                localPlayer.PlayerObject.GetComponent<Renderer>().sharedMaterial = materialNormal;


            
            // Game Master checks if game state needs to be changed
            if (ImGameMaster() && inGame && !resourceLocked)
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

        // Used to remove player from session
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
                playerInfo.PlayerObject.GetComponent<Player>().PlayerID = userId;

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
            // only the Game Master can spawn objects (?)
            if (ImGameMaster())
            {
                // get one's own position information
                Transform headTransform = Camera.main.transform;
                // spawn object in front of player by a certain amount
                Vector3 spawnPosition = headTransform.position + headTransform.forward * 3;

                spawnPosition = transform.InverseTransformPoint(spawnPosition); //I ADDED THE SPAWNER PART- GameObject.FindGameObjectWithTag("spawner").transform.position
                Quaternion spawnRotation = Quaternion.Inverse(transform.rotation) * headTransform.rotation;

                // send message to all users that an object was spawned containing object's information
                CustomMessages.Instance.SendSpawnTarget(spawnPosition, spawnRotation);

                GameObject target;
                // if object has not been made yet, create it (?)
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
        
        // what is broadcast
        // Called when player collides with a target
        public void TargetCollision(long colliderID, bool broadcast=false)
        {
            if (gameState != INTERLUDE)
                return;
            if (broadcast && ImGameMaster())
                CustomMessages.Instance.SendTargetCollision(colliderID);

            resourceLocked = true;

            //infoDisplay.GetComponent<Text>().text = "Target Collided!";

            GameObject target;
            // set target object to materialTargetCollided (green color)
            if (sharedObjects.TryGetValue("target", out target))
            {
                target.GetComponent<Renderer>().sharedMaterial = materialTargetCollided;
            }
            Debug.Log("SAFE: " + safePlayers.Count);
            Debug.Log("ALIVE: " + alivePlayers.Count);
            Debug.Log("MAX: " + maxSafePlayers);
            Debug.Log("COLLIDER ID: " + colliderID);
            foreach (long id in alivePlayers)
            {
                Debug.Log("ALIVE ID: " + id);
            }
           
            // Show player is safe if they collided before round ended and are not already safe
            if (alivePlayers.Contains(colliderID) && safePlayers.Count < maxSafePlayers && !safePlayers.Contains(colliderID))
            {
                safePlayers.Add(colliderID);
                if (IDisMine(colliderID))
                    infoDisplay.GetComponent<Text>().text = "You're safe!";
            }

            resourceLocked = false;
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

        // Ends main part of the game and determines which players survived and displays it to their screens
        private void EndInterlude(bool broadcast=false)
        {
            if (broadcast && ImGameMaster())
                CustomMessages.Instance.SendEndInterlude();

            resourceLocked = true;

            // goes through each player that was playing in the round
            
            for (int i = alivePlayers.Count - 1; i >= 0; i--)
            {
                // if not safe, remove player from alive players and set as dead
                if (!safePlayers.Contains(alivePlayers[i]))
                {

                    

                    PlayerInfo playerInfo;
                    if (remotePlayers.TryGetValue(alivePlayers[i], out playerInfo))
                        playerInfo.PlayerObject.GetComponent<Renderer>().sharedMaterial = materialDead;
                    
                    // if my player is dead, display it
                    if (IDisMine(alivePlayers[i]))
                        infoDisplay.GetComponent<Text>().text = "Dead";

                    alivePlayers.RemoveAt(i);
                }
                else
                {
                    
                    // if alive, show they survived
                    PlayerInfo playerInfo;
                    if (remotePlayers.TryGetValue(alivePlayers[i], out playerInfo))
                        playerInfo.PlayerObject.GetComponent<Renderer>().sharedMaterial = materialSurvived;
                    
                    if (IDisMine(alivePlayers[i]))
                        infoDisplay.GetComponent<Text>().text = "Survived";
                }

            }

            gameState = POSTLUDE;

            delayInterval = Time.time + delayPostlude;

            resourceLocked = false;

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

            //infoDisplay.GetComponent<Text>().text = "In game";
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

        private void HandleSafeSignal(NetworkInMessage msg)
        {
            msg.ReadInt64();
            long playerID = msg.ReadInt64();
            if (IDisMine(playerID))
                infoDisplay.GetComponent<Text>().text = "You're safe!";
            
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
