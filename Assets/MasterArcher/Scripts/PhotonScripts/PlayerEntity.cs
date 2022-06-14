using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;

public class PlayerEntity : MonoBehaviourPunCallbacks
{
    public int currentHP;
    public int maxHP;
    public float moveSpeed;
    public int minDamage;
    public int maxDamage;

    public int maxMoveDistance;
    public int maxAttackDistance;

    public bool usedThisTurn;
    public GameObject selectedVisual;
    public SpriteRenderer spriteVisual;

    [Header("Ui")]
    public Image healthFillImage;
    [Header("Sprite variants")]
    public Sprite leftPlayerSprite;
    public Sprite rightPlayerSprite;

    [PunRPC]
    void Initialize(bool isMine)
    {
        if (isMine)
        {
            PlayerSpawnManager.me.players.Add(this);
        }
        else
        {
            GameController.instance.GetOtherPlayer(PlayerSpawnManager.me).players.Add(this);
        }
    }

}
