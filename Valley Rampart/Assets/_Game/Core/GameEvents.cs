using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public readonly struct PlayerMoveEvent

{

    public readonly Vector3 position;
    public readonly Vector3 moveDir;

    public PlayerMoveEvent(Vector3 pos, Vector3 Dir)
    {
        position = pos;
        moveDir = Dir;
    }

}