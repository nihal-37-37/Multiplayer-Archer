using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class PlayerSpawnManager : MonoBehaviourPunCallbacks
{
    public Player player;
    public GameObject playerPrefab;
    public Transform playerSpawnLocation;

    [HideInInspector] public List<PlayerEntity> players = new List<PlayerEntity>();
    private PlayerEntity activePlayer;

    public static PlayerSpawnManager me;
    public static PlayerSpawnManager enemy;

    // bool toggle = false;
    GameObject playerInstance;

    [PunRPC]
    private void Initialize(Player player)
    {
        this.player = player;
        if (player.IsLocal)
        {
            me = this;
            SpawnPlayers();
        }
        else enemy = this;
    }

    private void SpawnPlayers()
    {
        playerInstance = PhotonNetwork.Instantiate(playerPrefab.name, playerSpawnLocation.position, Quaternion.identity);
        playerInstance.GetPhotonView().RPC("Initialize", RpcTarget.Others, false);
        playerInstance.GetPhotonView().RPC("Initialize", this.player, true);
    }

    public void BeginTurn()
    {
        foreach (PlayerEntity playerEntity in players)
            playerEntity.usedThisTurn = false;
    }

    private void Update()
    {
        if (!photonView.IsMine) return;

        if (Input.GetKeyUp(KeyCode.T))
        {
            // GameManager.instance.photonView.RPC("SetNextTurn", RpcTarget.All);

            // CameraManager.instance.ChangeCameraAsPerTurn();
        }
    }
}
