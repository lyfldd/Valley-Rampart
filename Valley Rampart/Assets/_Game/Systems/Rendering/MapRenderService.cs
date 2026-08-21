using UnityEngine;

/// <summary>
    /// 2_10 渲染与摄像机 · 步骤2「MapRenderService 核心」的等轴投影纯逻辑切片。
    ///
    /// 铁律：本篇只渲染、**不产生任何逻辑坐标副作用**；逻辑层一律正交格坐标（doc 1 §1.6），
    /// 等轴投影只在本类作用于渲染层。
    ///
    /// 等轴 2:1（菱形）约定（对齐 Unity Tilemap Isometric / Cell Size (1.28,0.64,1)）：
    ///   isoX = (gx - gy) * cellW * 0.5
    ///   isoY = (gx + gy) * cellH * 0.5
    /// 即沿 +x 走一格 → ( +cellW/2, +cellH/2 )，沿 +y 走一格 → ( -cellW/2, +cellH/2 )。
    /// cellSize 读 GridSystem.Config（PPU=100：128×64px → (1.28,0.64)），无网格时回退默认。
    ///
    /// 本切片交付：GridToIso / IsoToCell 纯投影（确定性、可独立验证，Editor 空网格可算）。
    /// RenderMap / UpdateCell（Tilemap 铺格 + 2_12 废墟/施工态刷新）依赖编辑器场景，留步骤1/8 落地。
    /// 屏幕拾取串联（ScreenToGrid = Camera.ScreenToWorldPoint → IsoToCell）随步骤3 CameraRig 补全。
    /// </summary>
    public class MapRenderService : Singleton<MapRenderService>
    {
        /// <summary>1 小区块=128×64px @PPU100 → cellSize (1.28, 0.64)。</summary>
        public static readonly Vector2 DefaultCellSize = new Vector2(1.28f, 0.64f);

        /// <summary>取逻辑网格 cellSize；Editor 空网格（GridSystem 未激活）回退默认，保证纯投影可算。</summary>
        private static Vector2 CellSize()
        {
            if (GridSystem.Instance != null && GridSystem.Instance.Config != null)
                return GridSystem.Instance.Config.cellSize;
            return DefaultCellSize;
        }

        /// <summary>逻辑格 → 等轴渲染世界坐标（仅渲染层用）。</summary>
        public static Vector2 GridToIso(GridCoord cell)
        {
            Vector2 cs = CellSize();
            float halfW = cs.x * 0.5f;
            float halfH = cs.y * 0.5f;
            return new Vector2((cell.x - cell.y) * halfW, (cell.x + cell.y) * halfH);
        }

        /// <summary>
        /// 等轴世界坐标 → 逻辑格（逆投影，ScreenToGrid 底座；floor 取含点所在的菱形格）。
        /// 纯数学逆变换不校验越界，调用方（步骤3 CameraRig/ScreenToGrid）自行 clamp。
        /// </summary>
        public static GridCoord IsoToCell(Vector2 iso)
        {
            Vector2 cs = CellSize();
            float halfW = cs.x * 0.5f;
            float halfH = cs.y * 0.5f;
            // 由 isoX=(x-y)*hw, isoY=(x+y)*hh 反解：
            float gx = iso.x / halfW * 0.5f + iso.y / halfH * 0.5f;
            float gy = iso.y / halfH * 0.5f - iso.x / halfW * 0.5f;
            return new GridCoord(Mathf.FloorToInt(gx), Mathf.FloorToInt(gy));
        }

        /// <summary>垂直向量（世界码→世界屏幕用），供单位/悬浮物按等轴深度参与 Y-sort 的辅助（预留）。</summary>
        public static float IsoDepth(GridCoord cell)
        {
            Vector2 cs = CellSize();
            return (cell.x + cell.y) * cs.y * 0.5f; // 同 GridToIso 的 isoY，随行增即深度增
        }
    }