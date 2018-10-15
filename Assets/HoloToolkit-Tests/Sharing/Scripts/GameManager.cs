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
    public class GameManager : Singleton<GameManager>, ISpeechHandler, IInputClickHandler
    {
        // CONSTANTS
        private readonly int PRELUDE = 0;
        private readonly int INTERLUDE = 1;
        private readonly int POSTLUDE = 2;
        private readonly int ROUND_INTERVAL = 3;
        private readonly int COUNTDOWN = 3;
        private readonly float MODEL_RANGE = 3.0f;

        private readonly string TARGET_ID = "target";
        private readonly string PLAYER_ID = "player";
        private readonly string WARDROBE_ID = "wardrobe";
        private readonly string BULLET_ID = "bullet";
        private readonly string CAMERA_ID = "camera_main";
        private readonly string GALLERY_ID = "gallery";

        // PUBLIC
        public Material materialTargetCollided; // material of the collision object (in this case the capsule) when something collides with it

        public float targetSpawnRadius = 5f; // radius in which collision object can be spawned (currently 3)
        public float targetRotSpeed = 30f;
        public float targetSpawnHeight = 4f;
        public float targetDropSpeed = 0.8f;
        public float delayPrelude = 5f; //currently 10
        public float delayPostlude = 5f; //currently 10
        public float delayRoundInterval = 5f;
        public float bulletSpeed = 6.0f;

        public AudioClip collisionClip;
        public AudioClip successClip;
        public AudioClip failClip;

        public GameObject p_Target;

        public RawImage displayPic;
        public Image HUDimage;

        public enum Images : int
        {
            Count_1,
            Count_2,
            Count_3,
            Survived,
            Youreout,
            RoundInterlude,
            WinPic,
            LosePic
        }

        public List<Sprite> sprites;

        public bool Started { get; private set; }

        // Used to hold spawned gameobjects. Leave empty in inspector
        public Dictionary<string, GameObject> sharedObjects = new Dictionary<string, GameObject>();

        // LOBBY VARS
        private bool inGallery;

        // GAME VARS
        private int gameState;
        private bool inGame;
        private bool inCountdown;
        private int maxRounds;
        private int maxSafePlayers;
        private int completedRounds;
        private int playersTotal;
        private float delayInterval;
        private long gameMasterID = 0;
        private long objectCount = 1; // used to uniquely identify some spawned objects

        private List<long> allPlayers; // list of all players
        private List<long> alivePlayers; // list of players still alive
        private List<long> safePlayers; // list of players who have entered the collision object in a current round

        

        private void Start()
        {
            InputManager.Instance.PushModalInputHandler(gameObject);

            // add handlers for each type of message sent
            // if a message is sent with that specific type, the corresponding handler method will be called
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.GameMaster] = HandleUpdateGameMaster;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.TargetCollision] = HandleTargetCollision;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.InitializeGame] = HandleInitializeGame;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.TerminateGame] = HandleTerminateGame;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.StartRound] = HandleStartRound;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.EndPrelude] = HandleEndPrelude;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.EndInterlude] = HandleEndInterlude;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.EndRound] = HandleEndRound;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.RoundInterval] = HandleRoundInterval;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.ShowGallery] = HandleShowGallery;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.ShowLobby] = HandleShowLobby;

            // SharingStage should be valid at this point, but we may not be connected.
            if (SharingStage.Instance.IsConnected)
            {
                Connected();
            }
            else
            {
                SharingStage.Instance.SharingManagerConnected += Connected;
            }



            // initialize appropriate lists
            allPlayers = new List<long>();
            alivePlayers = new List<long>();
            safePlayers = new List<long>();

            HUDimage.GetComponent<Image>().enabled = false;
        }

        // called 60(?) times a second to check progress of game and switch between game phases
        private void Update()
        {
            if (!ObjectManager.Instance.Started
                || !AudioManager.Instance.Started
                || !HeadManager.Instance.Started)
                return;
            else if (!Started)
                WorldSetup();

            Vector3 headPos = Camera.main.transform.position;
            Vector3 direction = Camera.main.transform.forward;

            RaycastHit hitInfo;
            if (Physics.Raycast(headPos, direction, out hitInfo))
            {
                if (hitInfo.distance < MODEL_RANGE)
                {
                    GameObject model = hitInfo.collider.gameObject;
                    ModelController mc = model.GetComponentInChildren<ModelController>();
                    if (mc)
                    {
                        mc.Rollover();
                    }
                }
            }


            if (inCountdown)
            {
                HUDimage.GetComponent<Image>().enabled = true;

                int timeLeft = (int)Mathf.Ceil(delayInterval - Time.time); ;
                if (timeLeft == 3)
                {
                    HUDimage.GetComponent<Image>().sprite = sprites[(int)Images.Count_3];
                }
                else if (timeLeft == 2)
                {
                    HUDimage.GetComponent<Image>().sprite = sprites[(int)Images.Count_2];
                }
                else if (timeLeft == 1)
                {
                    HUDimage.GetComponent<Image>().sprite = sprites[(int)Images.Count_1];
                }
                else if (Time.time > delayInterval)
                {
                    inGame = true;
                    inCountdown = false;
                    HUDimage.GetComponent<Image>().enabled = false;
                    if (ImGameMaster())
                        StartRound(broadcast: true);
                }

                return;
            }

            if (!ImGameMaster() || !inGame)
                return;

            GameObject target;

            if (ObjectManager.Instance.sharedObjects.TryGetValue(TARGET_ID, out target))
            {
                target.transform.Rotate(Vector3.up * targetRotSpeed * Time.deltaTime);
                if (inGame && gameState == INTERLUDE)
                    target.transform.localPosition = Vector3.Lerp(target.transform.localPosition, new Vector3(target.transform.localPosition.x, 0f, target.transform.localPosition.z), Time.deltaTime);
                else if (inGame && gameState == POSTLUDE)    
                    target.transform.localPosition = Vector3.Lerp(target.transform.localPosition, new Vector3(target.transform.localPosition.x, targetSpawnHeight, target.transform.localPosition.z), Time.deltaTime);

                ObjectManager.Instance.PosObject(TARGET_ID, target.transform.localPosition, true);
                ObjectManager.Instance.RotObject(TARGET_ID, target.transform.localRotation, true);
                
            }
            
            // Game Master checks if game state needs to be changed

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
                case 3:
                    if (Time.time > delayInterval)
                        RoundInterval(broadcast: true);
                    break;
                default:
                    break;
            }
        }

        private void Connected(object sender = null, EventArgs e = null)
        {
            SharingStage.Instance.SharingManagerConnected -= Connected;
        }

        public void OnSpeechKeywordRecognized(SpeechKeywordRecognizedEventData eventData)
        {
            switch (eventData.RecognizedText.ToLower())
            {
                case "initialize":
                    AssumeGameMaster();
                    break;
                case "start":
                    PlayGame();
                    break;
                case "gallery":
                    ShowGallery(true);
                    break;
                case "lobby":
                    ShowLobby(true);
                    break;
                default:
                    break;
            }
        }

        public void OnInputClicked(InputClickedEventData eventData)
        {
            Vector3 headPos = Camera.main.transform.position;
            Vector3 direction = Camera.main.transform.forward;

            RaycastHit hitInfo;
            if (Physics.Raycast(headPos, direction, out hitInfo))
            {
                if (hitInfo.distance < MODEL_RANGE)
                {
                    GameObject model = hitInfo.collider.gameObject;
                    ModelController mc = model.GetComponentInChildren<ModelController>();
                    if (mc)
                    {
                        mc.Activate();
                    }
                }
            }


            string bulletID = BULLET_ID + objectCount.ToString() + GetMyID().ToString();
            objectCount++;

            Vector3 bulletPos = transform.InverseTransformPoint(Camera.main.transform.TransformPoint(Vector3.forward));
            Vector3 bulletAngvel = new Vector3(
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(0f, 360f));

            ObjectManager.Instance.SpawnObject(bulletID, (int)ObjectManager.Prefabs.Bullet, true);
            ObjectManager.Instance.PosObject(bulletID, bulletPos, true);
            ObjectManager.Instance.ApplyVelocity(bulletID, bulletSpeed * (bulletPos - transform.InverseTransformPoint(Camera.main.transform.position)), true);
            ObjectManager.Instance.ApplyAngVelocity(bulletID, bulletAngvel, true);
            
            
        }

        private void WorldSetup()
        {
            ObjectManager.Instance.Initialize();
            HeadManager.Instance.Initialize();
            ObjectManager.Instance.SpawnObject(WARDROBE_ID, (int)ObjectManager.Prefabs.Wardrobe2, true);
            ObjectManager.Instance.SpawnObject(GALLERY_ID, (int)ObjectManager.Prefabs.Gallery, true);
            inGallery = false;

            ObjectManager.Instance.SetActive(GALLERY_ID, 0);


            Started = true;
        }

        private void ShowGallery(bool broadcast=true)
        {
            if (inGallery || inGame)
                return;

            if (broadcast)
                CustomMessages.Instance.SendShowGallery();

            ObjectManager.Instance.SetActive(GALLERY_ID, 1);
            ObjectManager.Instance.SetActive(WARDROBE_ID, 0);
            //AudioManager.Instance.PlayClip(CAMERA_ID, (int)AudioManager.AudioClips.GalleryBGM);

            inGallery = true;
        }

        private void ShowLobby(bool broadcast = true)
        {
            if (!inGallery || inGame)
                return;

            if (broadcast)
                CustomMessages.Instance.SendShowLobby();

            ObjectManager.Instance.SetActive(GALLERY_ID, 0);
            ObjectManager.Instance.SetActive(WARDROBE_ID, 1);
            AudioManager.Instance.StopClip(CAMERA_ID);

            inGallery = false;
        }


        public void TargetCollision(long colliderID, bool broadcast=false)
        {
           
            if (gameState != INTERLUDE)
                return;
            if (broadcast && ImGameMaster())
                CustomMessages.Instance.SendTargetCollision(colliderID);


            ObjectManager.Instance.ChangeMaterial(TARGET_ID, (int)ObjectManager.Materials.Collided);

            // Show player is safe if they collided before round ended and are not already safe
            if (alivePlayers.Contains(colliderID) && safePlayers.Count < maxSafePlayers && !safePlayers.Contains(colliderID))
            {
                safePlayers.Add(colliderID);
                if (IDisMine(colliderID))
                {
                    AudioManager.Instance.PlayClip(PLAYER_ID + colliderID, (int)AudioManager.AudioClips.Pop);
                    Debug.Log("You are safe");
                }
            }
        }

        private void StartRound(bool broadcast=false)
        {

            if (broadcast && ImGameMaster())
                CustomMessages.Instance.SendStartRound();

            gameState = PRELUDE;
            delayInterval = Time.time + delayPrelude;
            maxSafePlayers = maxRounds - completedRounds;
            HUDimage.GetComponent<Image>().enabled = false;
            inCountdown = false;
            safePlayers.Clear();
            Debug.Log("Round has begun");

            if (ImGameMaster())
            {
                Vector3 targetPos = GetRandomPos();
                Quaternion targetRot = GetRandomRot();

                ObjectManager.Instance.SpawnObject(TARGET_ID, (int)ObjectManager.Prefabs.Target, broadcast: true);
                ObjectManager.Instance.PosObject(TARGET_ID, targetPos, broadcast: true);
                ObjectManager.Instance.RotObject(TARGET_ID, targetRot, broadcast: true);
                ObjectManager.Instance.ScaleObject(TARGET_ID, Vector3.zero, broadcast: true);
                
                AudioManager.Instance.PlayClip(TARGET_ID, (int)AudioManager.AudioClips.Ribbit, broadcast: true);
            }

        }

        private void EndPrelude(bool broadcast=false)
        {
            if (broadcast && ImGameMaster())
                CustomMessages.Instance.SendEndPrelude();

            Debug.Log("Interlude has begun");
            gameState = INTERLUDE;

            ObjectManager.Instance.ScaleObject(TARGET_ID, Vector3.one);
        }
        
        private void EndInterlude(bool broadcast=false)
        {
            if (broadcast && ImGameMaster())
                CustomMessages.Instance.SendEndInterlude();
            
            // goes through each player that was playing in the round
            
            for (int i = alivePlayers.Count - 1; i >= 0; i--)
            {
                // if not safe, remove player from alive players and set as dead
                if (!safePlayers.Contains(alivePlayers[i]))
                {
                    ObjectManager.Instance.ChangeMaterial(PLAYER_ID + alivePlayers[i].ToString(), (int)ObjectManager.Materials.Dead);

                    // if my player is dead, display it
                    if (IDisMine(alivePlayers[i]))
                    {
                        HUDimage.GetComponent<Image>().enabled = true;
                        HUDimage.GetComponent<Image>().sprite = sprites[(int) Images.Youreout];
                        AudioManager.Instance.PlayClip(CAMERA_ID, (int)AudioManager.AudioClips.Fail);
                       // var picTexture = (Texture2D)Pictures.Load("loser")
                    }

                    alivePlayers.RemoveAt(i);
                }
                else
                {
                    // if alive, show they survived
                    ObjectManager.Instance.ChangeMaterial(PLAYER_ID + alivePlayers[i].ToString(), (int)ObjectManager.Materials.Alive);

                    if (IDisMine(alivePlayers[i]))
                    {
                        HUDimage.GetComponent<Image>().enabled = true;
                        HUDimage.GetComponent<Image>().sprite = sprites[(int)Images.Survived];
                        AudioManager.Instance.PlayClip(CAMERA_ID, (int)AudioManager.AudioClips.Success);
                    }
                }

            }

            AudioManager.Instance.StopClip(TARGET_ID);

            gameState = POSTLUDE;

            delayInterval = Time.time + delayPostlude;
            

            Debug.Log("Postlude has begun");

        }

        private void EndRound(bool broadcast = false)
        {
            if (broadcast && ImGameMaster())
                CustomMessages.Instance.SendEndRound();

            ObjectManager.Instance.DestroyObject(TARGET_ID);

            foreach (long playerID in alivePlayers)
            {
                ObjectManager.Instance.ChangeMaterial(PLAYER_ID + playerID, (int)ObjectManager.Materials.Normal);

                if (IDisMine(playerID))
                    Debug.Log("You remain in the game");
            }

            completedRounds++;
            Debug.Log("Completed " + completedRounds + " rounds");
            if (!ImGameMaster())
                return;

            if (completedRounds < maxRounds)
            {
                gameState = ROUND_INTERVAL;

                delayInterval = Time.time + delayRoundInterval;
                HUDimage.GetComponent<Image>().enabled = true;
                HUDimage.GetComponent<Image>().sprite = sprites[(int)Images.RoundInterlude];
            }
            else
            {
                TerminateGame(true);
            }
        }

        private void RoundInterval(bool broadcast=false)
        {
            if (broadcast && ImGameMaster())
                CustomMessages.Instance.SendRoundInterval();

            if (ImGameMaster())
                StartRound(true);
            
        }

        private void InitializeGame(bool broadcast=false)
        {
            if (broadcast && ImGameMaster())
                CustomMessages.Instance.SendInitializeGame();

            if (inGame)
                return;

            ObjectManager.Instance.SetActive(WARDROBE_ID, 0);

            playersTotal = HeadManager.Instance.getCurrentPlayers().Keys.Count;

            allPlayers.Clear();
            alivePlayers.Clear();
            safePlayers.Clear();
            
            foreach(long userID in HeadManager.Instance.getCurrentPlayers().Keys)
            {
                allPlayers.Add(userID);
                alivePlayers.Add(userID);
            }

            inCountdown = true;
            delayInterval = Time.time + COUNTDOWN;
            completedRounds = 0;
            maxRounds = playersTotal - 1;
            
        }

        private void TerminateGame(bool broadcast=false)
        {
            if (broadcast && ImGameMaster())
                CustomMessages.Instance.SendTerminateGame();

            allPlayers.Clear();
            alivePlayers.Clear();
            safePlayers.Clear();
            ObjectManager.Instance.SetActive(WARDROBE_ID, 1);
            inGame = false;
            completedRounds = 0;
            maxRounds = 0;
            HUDimage.GetComponent<Image>().enabled = false;
            gameMasterID = 0;
        }

        private void PlayGame()
        {
            AssumeGameMaster();
            if (!ImGameMaster())
                return;

            if (HeadManager.Instance.getCurrentPlayers().Keys.Count < 2)
                return;

            ObjectManager.Instance.ChangeMaterial(PLAYER_ID + GetMyID().ToString(), (int)ObjectManager.Materials.GM, true);

            InitializeGame(broadcast: true);
        }

        #region Handlers


        private void HandleShowGallery(NetworkInMessage msg)
        {
            msg.ReadInt64();

            ShowGallery();
        }

        private void HandleShowLobby(NetworkInMessage msg)
        {
            msg.ReadInt64();

            ShowLobby();
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

        private void HandleTerminateGame(NetworkInMessage msg)
        {
            TerminateGame();
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

        private void HandleRoundInterval(NetworkInMessage msg)
        {
            RoundInterval();
        }

        private void HandleUpdateGameMaster(NetworkInMessage msg)
        {
            Debug.Log("Designated gamemaster");
            long userID = msg.ReadInt64();
            if (IsGameMasterFree())
                gameMasterID = userID;
        }

        #endregion

        #region Helper methods

        public bool IsGameMaster(long userID)
        {
            return userID == gameMasterID;
        }

        public bool ImGameMaster()
        {
            return IDisMine(gameMasterID);
        }

        public bool IDisMine(long userID)
        {
            return GetMyID() == userID;
        }

        public long GetMyID()
        {
            return SharingStage.Instance.Manager.GetLocalUser().GetID();
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

        private Quaternion GetRandomRot()
        {
            return Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
        }

        private Vector3 GetRandomPos(float maxRange=5.0f, float maxAdjust=0.8f)
        {
            /*
            Quaternion randomRot = Quaternion.Euler(0.0f, UnityEngine.Random.Range(0f, 360f), 0.0f);

            Vector3 randomVec = Vector3.forward.RotateAround(Vector3.up, randomRot);
            randomVec += Vector3.up;
            Vector3 transformOrigin = transform.position + Vector3.up;
            Debug.Log(randomRot);
            Debug.Log(randomVec);
            RaycastHit hitInfo;
            if (!Physics.Raycast(transformOrigin, transform.TransformPoint(randomVec), out hitInfo, maxRange))
                hitInfo.point = transformOrigin + maxRange * (transform.TransformPoint(randomVec) - transformOrigin).normalized;
            Debug.Log(hitInfo.point);
            Vector3 randomPos = (transform.InverseTransformPoint(hitInfo.point) - Vector3.up) * UnityEngine.Random.Range(0f, 1f);

            return randomPos;*/
            
            //return transform.InverseTransformPoint(Camera.main.transform.TransformPoint(Vector3.forward * 2)) + Vector3.up * targetSpawnHeight;
            return new Vector3(UnityEngine.Random.Range(-5.0f, 5.0f), targetSpawnHeight, UnityEngine.Random.Range(-5.0f, 5.0f));
        }

        
        #endregion
    }
}
