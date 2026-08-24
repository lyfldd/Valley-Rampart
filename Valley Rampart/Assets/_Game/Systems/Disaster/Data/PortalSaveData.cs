using System;

// 2_14 步骤14 传送门存档数据（§6.5 / 2_11 联动）
// 只存传送门状态；已在外怪物走 UnitSaveData（同玩家单位，2_11 §3.3），灾害状态走 PortalDisasterTrigger。
[Serializable]
public class PortalSaveData
{
    public int portalGridX;            // 传送门占格左上 X
    public int portalGridY;            // 传送门占格左上 Y
    public int portalHp;               // 剩余 HP
    public int portalSurvivedNights;   // 已存活夜数（烈度递减依据）
    public int portalState;            // (int)PortalState
}