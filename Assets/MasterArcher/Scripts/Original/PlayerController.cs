using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class PlayerController : MonoBehaviourPunCallbacks
{

    /// <summary>
    /// Main player controller class.
    /// This class is responsible for player inputs, rotation, health-management, shooting arrows and helper-dots creation.
    /// </summary>

    [Header("Public GamePlay settings")]
    public bool useHelper = true;                   //use helper dots when player is aiming to shoot
    public int baseShootPower = 30;                 //base power. edit with care.
    public int playerHealth = 100;                  //starting (full) health. can be edited.
    private int minShootPower = 15;                 //powers lesser than this amount are ignored. (used to cancel shoots)
    internal int playerCurrentHealth;               //real-time health. not editable.
    public static bool isPlayerDead;                //flag for gameover event

    [Header("Linked GameObjects")]
    //Reference to game objects (childs and prefabs)
    public GameObject arrow;
    public GameObject trajectoryHelper;
    public GameObject playerTurnPivot;
    public GameObject playerShootPosition;
    // public GameObject infoPanel;
    // public GameObject UiDynamicPower;
    // public GameObject UiDynamicDegree;
    //Hidden gameobjects
    // private GameObject gc;  //game controller object
    private GameObject cam; //main camera

    [Header("Audio Clips")]
    public AudioClip[] shootSfx;
    public AudioClip[] hitSfx;


    //private settings
    private Vector2 icp;                            //initial Click Position
    private Ray inputRay;
    private RaycastHit hitInfo;
    private float inputPosX;
    private float inputPosY;
    private Vector2 inputDirection;
    private float distanceFromFirstClick;
    private float shootPower;
    private float shootDirection;
    // private Vector3 shootDirectionVector;

    //helper trajectory variables
    private float helperCreationDelay = 0.12f;
    private bool canCreateHelper;
    private float helperShowDelay = 0.2f;
    private float helperShowTimer;
    private bool helperDelayIsDone;

    public int reverse;

    /// <summary>
    /// Init
    /// </summary>
    void Awake()
    {
        icp = new Vector2(0, 0);
        // infoPanel.SetActive (false);
        // shootDirectionVector = new Vector3(0, 0, 0);
        playerCurrentHealth = playerHealth;
        isPlayerDead = false;

        // gc = GameObject.FindGameObjectWithTag("GameController");
        cam = GameObject.FindGameObjectWithTag("MainCamera");

        canCreateHelper = true;
        helperShowTimer = 0;
        helperDelayIsDone = false;
    }


    /// <summary>
    /// FSM
    /// </summary>
    void Update()
    {

        //if the game has not started yet, or the game is finished, just return
        if (!GameController.gameIsStarted || GameController.gameIsFinished)
            return;

        //Check if this object is dead or alive
        if (playerCurrentHealth <= 0)
        {
            print("Player is dead...");
            playerCurrentHealth = 0;
            isPlayerDead = true;
            return;
        }

        //if this is not our turn, just return
        // if (!GameController.playersTurn)
        //     return;

        // Debug.LogError(GameController.instance.currentTargetSpawn.photonView.IsMine + " :photonView of current target");
        // Debug.Log(GameController.instance.currentTargetSpawn.players[0].photonView.IsMine + " : photonview of player");

        if (!GameController.instance.currentTargetSpawn.photonView.IsMine) return;

        //if we already have an arrow in scene, we can not shoot another one!
        if (GameController.isArrowInScene)
            return;

        if (!PauseManager.enableInput)
            return;

        //Player pivot turn manager
        if (Input.GetMouseButton(0))
        {

            // Debug.Log("GetMouseButton in update");
            turnPlayerBody();

            //only show shot info when we are fighting with an enemy
            if (GameModeController.isEnemyRequired())
                // infoPanel.SetActive (true);

                helperShowTimer += Time.deltaTime;
            if (helperShowTimer >= helperShowDelay)
                helperDelayIsDone = true;
        }

        //register the initial Click Position
        if (Input.GetMouseButtonDown(0))
        {
            icp = new Vector2(inputPosX, inputPosY);
            // print("icp: " + icp);
            // print("icp magnitude: " + icp.magnitude);
        }

        //clear the initial Click Position
        if (Input.GetMouseButtonUp(0))
        {

            //only shoot if there is enough power applied to the shoot
            if (shootPower >= minShootPower)
            {
                shootArrow();
            }
            else
            {
                //reset body rotation
                StartCoroutine(resetBodyRotation());
            }

            //reset variables
            icp = new Vector2(0, 0);
            // infoPanel.SetActive (false);
            helperShowTimer = 0;
            helperDelayIsDone = false;
        }
    }


    /// <summary>
    /// This function will be called when this object is hit by an arrow. It will check if this is still alive after the hit.
    /// if ture, changes the turn. if not, this is dead and game should finish.
    /// </summary>
    // public void changeTurns()
    // {

    //     print("playerCurrentHealth: " + playerCurrentHealth);

    //     if (playerCurrentHealth > 0)
    //         StartCoroutine(GameController.instance.roundTurnManager());
    //     else
    //         GameController.noMoreShooting = true;

    // }


    /// <summary>
    /// When player is aiming, we need to turn the body of the player based on the angle of icp and current input position
    /// </summary>
    void turnPlayerBody()
    {
        inputRay = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(inputRay, out hitInfo, 50))
        {
            // determine the position on the screen
            inputPosX = this.hitInfo.point.x;
            inputPosY = this.hitInfo.point.y;
            // set the bow's angle to the arrow
            if (GameController.instance.currentTargetSpawn == GameController.instance.player1Spawn)
                inputDirection = new Vector2(icp.x - inputPosX, icp.y - inputPosY);
            if (GameController.instance.currentTargetSpawn == GameController.instance.player2Spawn)
                inputDirection = new Vector2(-(icp.x - inputPosX), -(icp.y - inputPosY));

            shootDirection = Mathf.Atan2(inputDirection.y, inputDirection.x) * Mathf.Rad2Deg;

            //for an optimal experience, we need to limit the rotation to 0 ~ 90 euler angles.
            //so...
            if (GameController.instance.currentTargetSpawn == GameController.instance.player1Spawn)
            {
                if (shootDirection > 90)
                    shootDirection = 90;
                if (shootDirection < 0)
                    shootDirection = 0;
            }
            if (GameController.instance.currentTargetSpawn == GameController.instance.player2Spawn)
            {
                if (shootDirection < -90)
                    shootDirection = -90;
                if (shootDirection > 0)
                    shootDirection = 0;
            }

            //apply the rotation
            playerTurnPivot.transform.eulerAngles = new Vector3(0, 0, shootDirection);
            photonView.RPC("TurnBodyInBothGame", RpcTarget.Others, shootDirection);

            //calculate shoot power
            distanceFromFirstClick = inputDirection.magnitude / 4;
            shootPower = Mathf.Clamp(distanceFromFirstClick, 0, 1) * 100;

            // if (useHelper)
            // {
            //     //create trajectory helper points, while preventing them to show when we start to click/touch
            //     if (shootPower > minShootPower && helperDelayIsDone)
            //         StartCoroutine(shootTrajectoryHelper());
            // }
        }
    }

    [PunRPC]
    private void TurnBodyInBothGame(float shootDirectionRPCParam)
    {
        playerTurnPivot.transform.eulerAngles = new Vector3(0, 0, shootDirectionRPCParam);
    }



    /// <summary>
    /// Shoot sequence.
    /// For the player controller, we just need to instantiate the arrow object, apply the shoot power to it, and watch is fly.
    /// There is no AI involved with player arrows. It just fly based on the initial power and angle.
    /// </summary>
    void shootArrow()
    {

        //set the unique flag for arrow in scene.
        GameController.isArrowInScene = true;

        //play shoot sound
        playSfx(shootSfx[Random.Range(0, shootSfx.Length)]);

        //add to shoot counter
        GameController.playerArrowShot++;


        // GameObject arr = Instantiate(arrow, GameController.instance.currentTargetSpawn.transform.position - new Vector3(0, 4, 0), Quaternion.Euler(0, 180, shootDirection * -1)) as GameObject;
        // GameObject initiatedShootArrow = InitiateShootArrow();
        photonView.RPC("InitiateShootArrow", RpcTarget.All, inputDirection, shootPower);

        // shootDirectionVector = Vector3.Normalize(inputDirection);
        // if (GameController.instance.currentTargetSpawn == GameController.instance.player1Spawn)
        // {
        //     shootDirectionVector = new Vector3(Mathf.Clamp(shootDirectionVector.x, 0, 1), Mathf.Clamp(shootDirectionVector.y, 0, 1), shootDirectionVector.z);
        //     initiatedShootArrow.GetComponent<MainLauncherController>().playerShootVector = shootDirectionVector * ((shootPower + baseShootPower) / 50);
        // }
        // if (GameController.instance.currentTargetSpawn == GameController.instance.player2Spawn)
        // {
        //     shootDirectionVector = new Vector3(Mathf.Clamp(shootDirectionVector.x, 0, 1), Mathf.Clamp(shootDirectionVector.y, -1, 0), shootDirectionVector.z);
        //     initiatedShootArrow.GetComponent<MainLauncherController>().playerShootVector = -(shootDirectionVector * ((shootPower + baseShootPower) / 50));
        // }
        // // print("shootPower: " + shootPower + " --- " + "shootDirectionVector: " + shootDirectionVector);

        // cam.GetComponent<CameraController>().targetToFollow = initiatedShootArrow;

        //reset body rotation
        StartCoroutine(resetBodyRotation());
    }

    // public void InitiateShootArrow(Vector2 inputDirectionParam, float shootPowerParam)
    // {
    //     StartCoroutine(InitiateShootArrowCoroutine(inputDirectionParam, shootPowerParam));
    // }

    private void transferArrowOwnership(GameObject arrow)
    {
        if (arrow.GetComponent<PhotonView>().Owner.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            arrow.GetPhotonView().TransferOwnership(2);
        }
        else
        {

        }
    }

    [PunRPC]
    public void InitiateShootArrow(Vector2 inputDirectionParam, float shootPowerParam)
    {
        // Debug.LogError(photonView.IsMine + " player mine");

        // GameObject initiatedShootArrow = PhotonNetwork.Instantiate(arrow.name, new Vector3(-3.08f, -1.49000001f, 0), Quaternion.Euler(0, 180, shootDirection * -1)) as GameObject;

        // Debug.LogError(initiatedShootArrow.GetPhotonView().IsMine + " arrow mine");


        GameObject initiatedShootArrow = PhotonNetwork.Instantiate(arrow.name, GameController.instance.currentTargetSpawn.players[0].GetComponent<PlayerController>().playerShootPosition.transform.position, Quaternion.Euler(0, 180, shootDirection * -1)) as GameObject;

        Vector3 shootDirectionVector = Vector3.Normalize(inputDirectionParam);

        Debug.LogError(initiatedShootArrow.GetPhotonView().ViewID + " view id and actor number" + initiatedShootArrow.GetPhotonView().Owner.ActorNumber);

        if (GameController.instance.currentTargetSpawn == GameController.instance.player1Spawn)
        {
            // initiatedShootArrow.GetPhotonView().TransferOwnership(2);

            initiatedShootArrow.GetPhotonView().TransferOwnership(1);
            // if (initiatedShootArrow.GetComponent<PhotonView>().Owner.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
            // {
            //     initiatedShootArrow.GetPhotonView().TransferOwnership(2);
            //     Debug.Log("transferring1");
            // }
            Debug.LogError("player1 " + initiatedShootArrow.GetPhotonView().IsMine);
            shootDirectionVector = new Vector3(Mathf.Clamp(shootDirectionVector.x, 0, 1), Mathf.Clamp(shootDirectionVector.y, 0, 1), shootDirectionVector.z);
            // Debug.LogError(shootPowerParam + " shootarrow -- inputDirectionParam " + inputDirectionParam);
            initiatedShootArrow.GetComponent<MainLauncherController>().playerShootVector = reverse * (shootDirectionVector * ((shootPowerParam + baseShootPower) / 50));

        }
        if (GameController.instance.currentTargetSpawn == GameController.instance.player2Spawn)
        {
            initiatedShootArrow.GetPhotonView().TransferOwnership(2);
            // if (initiatedShootArrow.GetComponent<PhotonView>().Owner.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
            // {
            //     initiatedShootArrow.GetPhotonView().TransferOwnership(1);
            //     Debug.Log("transferring2");
            // }
            // initiatedShootArrow.GetPhotonView().TransferOwnership(1);

            Debug.LogError("player2 " + initiatedShootArrow.GetPhotonView().IsMine);
            shootDirectionVector = new Vector3(Mathf.Clamp(shootDirectionVector.x, 0, 1), Mathf.Clamp(shootDirectionVector.y, -1, 0), shootDirectionVector.z);
            initiatedShootArrow.GetComponent<MainLauncherController>().playerShootVector = reverse * (shootDirectionVector * ((shootPower + baseShootPower) / 50));
        }

        cam.GetComponent<CameraController>().targetToFollow = initiatedShootArrow;
    }


    /// <summary>
    /// tunr player body to default rotation
    /// </summary>
    IEnumerator resetBodyRotation()
    {

        //yield return new WaitForSeconds(1.5f);
        //playerTurnPivot.transform.eulerAngles = new Vector3(0, 0, 0);

        yield return new WaitForSeconds(0.25f);
        float currentRotationAngle = playerTurnPivot.transform.eulerAngles.z;
        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * 3;
            playerTurnPivot.transform.rotation = Quaternion.Euler(0, 0, Mathf.SmoothStep(currentRotationAngle, 0, t));
            yield return 0;
        }

    }


    /// <summary>
    /// Create helper dots that shows the possible fly path of the actual arrow
    /// </summary>
    // IEnumerator shootTrajectoryHelper()
    // {

    //     if (!canCreateHelper)
    //         yield break;

    //     canCreateHelper = false;

    //     GameObject t = Instantiate(trajectoryHelper, playerShootPosition.transform.position, Quaternion.Euler(0, 180, shootDirection * 1)) as GameObject;

    //     shootDirectionVector = Vector3.Normalize(inputDirection);

    //     if (GameController.instance.currentTargetSpawn == GameController.instance.player1Spawn)
    //         shootDirectionVector = new Vector3(Mathf.Clamp(shootDirectionVector.x, 0, 1), Mathf.Clamp(shootDirectionVector.y, 0, 1), shootDirectionVector.z);

    //     if (GameController.instance.currentTargetSpawn == GameController.instance.player2Spawn)
    //         shootDirectionVector = -(new Vector3(Mathf.Clamp(shootDirectionVector.x, 0, 1), Mathf.Clamp(shootDirectionVector.y, -1, 0), shootDirectionVector.z));

    //     //print("shootPower: " + shootPower + " --- " + "shootDirectionVector: " + shootDirectionVector);

    //     t.GetComponent<Rigidbody>().AddForce(shootDirectionVector * ((shootPower + baseShootPower) / 50), ForceMode.Impulse);

    //     yield return new WaitForSeconds(helperCreationDelay);
    //     canCreateHelper = true;
    // }


    /// <summary>
    /// Plays the sfx.
    /// </summary>
    void playSfx(AudioClip _clip)
    {
        GetComponent<AudioSource>().clip = _clip;
        if (!GetComponent<AudioSource>().isPlaying)
        {
            GetComponent<AudioSource>().Play();
        }
    }


    /// <summary>
    /// Play a sfx when player is hit by an arrow
    /// </summary>
    public void playRandomHitSound()
    {

        int rndIndex = Random.Range(0, hitSfx.Length);
        playSfx(hitSfx[rndIndex]);
    }

}

