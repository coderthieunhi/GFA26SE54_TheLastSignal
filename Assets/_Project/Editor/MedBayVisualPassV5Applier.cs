#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using TheLastSignal.Entities.Environment;

public static class MedBayVisualPassV5Applier
{
    private const string ScenePath = "Assets/Scenes/SampleScene_VisualPass_MedBay_v4.unity";
    private const string TileDir = "Assets/_Project/Tiles/MedBay";
    private const string PropsDir = "Assets/_Project/Sprites/Environments/MedBay/Props";

    [MenuItem("Tools/Apply Full MedBay Overhaul")]
    public static void ApplyOverhaul()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != ScenePath)
        {
            activeScene = EditorSceneManager.OpenScene(ScenePath);
        }

        Debug.Log("[MedBayVisualPassV5Applier] === STARTING FULL MEDBAY OVERHAUL ===");

        Material defaultLitMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_Sprite_Lit_PixelSmooth.mat")
            ?? AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");

        GameObject vpRoot = GameObject.Find("VP5_VisualPass_Elements");
        if (vpRoot == null) vpRoot = new GameObject("VP5_VisualPass_Elements");

        // ==========================================
        // 1. FLOOR TILES & 5-COLUMN CENTERED HAZARD
        // ==========================================
        Grid grid = Object.FindAnyObjectByType<Grid>();
        if (grid != null)
        {
            Tilemap floorMap = null;
            foreach (var tm in grid.GetComponentsInChildren<Tilemap>())
            {
                if (tm.name.ToLower().Contains("floor")) { floorMap = tm; break; }
            }

            if (floorMap != null)
            {
                Tile fDark = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/MedBay_Floor_Dark.asset");
                Tile fScratched = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/MedBay_Floor_Dark_Scratched.asset") ?? fDark;
                Tile fStained = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/MedBay_Floor_Dark_Stained.asset") ?? fDark;
                Tile fRusted = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/MedBay_Floor_Dark_Rusted.asset") ?? fDark;

                Tile cTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/MedBay_Floor_Containment_TL.asset");
                Tile cT = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/MedBay_Floor_Containment_T.asset");
                Tile cTLabel = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/MedBay_Floor_Containment_T_Label.asset");
                Tile cTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/MedBay_Floor_Containment_TR.asset");
                Tile cL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/MedBay_Floor_Containment_L.asset");
                Tile cR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/MedBay_Floor_Containment_R.asset");
                Tile cBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/MedBay_Floor_Containment_BL.asset");
                Tile cB = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/MedBay_Floor_Containment_B.asset");
                Tile cBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/MedBay_Floor_Containment_BR.asset");

                Tile fGrate = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/MedBay_Floor_Grate.asset") ?? fDark;
                Tile fHatch = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/MedBay_Floor_Subfloor_Hatch.asset") ?? fDark;
                Tile sNW = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/MedBay_Floor_Spill_NW.asset") ?? fDark;
                Tile sSE = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/MedBay_Floor_Spill_SE.asset") ?? fDark;

                for (int x = -20; x <= -12; x++)
                {
                    for (int y = 24; y <= 31; y++)
                    {
                        Vector3Int pos = new Vector3Int(x, y, 0);

                        // --- 5-COLUMN PERFECTLY CENTERED HAZARD PERIMETER (X in [-18, -14], Center X = -16.00) ---
                        // Top row (Y = 28):
                        if (y == 28 && x == -18) floorMap.SetTile(pos, cTL);
                        else if (y == 28 && x == -17) floorMap.SetTile(pos, cT);
                        else if (y == 28 && x == -16) floorMap.SetTile(pos, cTLabel); // SPECIMEN CONTAINMENT LABEL exactly at room & table center
                        else if (y == 28 && x == -15) floorMap.SetTile(pos, cT);
                        else if (y == 28 && x == -14) floorMap.SetTile(pos, cTR);

                        // Mid row (Y = 27):
                        else if (y == 27 && x == -18) floorMap.SetTile(pos, cL);
                        else if (y == 27 && (x == -17 || x == -16)) floorMap.SetTile(pos, fGrate); // Dual drainage grates under table
                        else if (y == 27 && x == -15) floorMap.SetTile(pos, fDark); // Space for surgical cart
                        else if (y == 27 && x == -14) floorMap.SetTile(pos, cR);

                        // Bottom row (Y = 26):
                        else if (y == 26 && x == -18) floorMap.SetTile(pos, cBL);
                        else if (y == 26 && (x == -17 || x == -16 || x == -15)) floorMap.SetTile(pos, cB);
                        else if (y == 26 && x == -14) floorMap.SetTile(pos, cBR);

                        // Sub-floor conduit hatch:
                        else if (x == -19 && y == 27) floorMap.SetTile(pos, fHatch);

                        // Biological stains:
                        else if (x == -19 && y == 28) floorMap.SetTile(pos, sNW);
                        else if (x == -13 && y == 25) floorMap.SetTile(pos, sSE);

                        // Micro-variations on open steel floor:
                        else if (x == -19 && y == 29) floorMap.SetTile(pos, fScratched);
                        else if (x == -19 && y == 25) floorMap.SetTile(pos, fStained);
                        else if (x == -13 && y == 29) floorMap.SetTile(pos, fRusted);
                        else if (x == -13 && y == 26) floorMap.SetTile(pos, fScratched);
                        else if (x == -12 && y == 25) floorMap.SetTile(pos, fStained);
                        else if (x == -17 && y == 24) floorMap.SetTile(pos, fRusted);
                        else if (x == -15 && y == 24) floorMap.SetTile(pos, fScratched);
                        else
                        {
                            floorMap.SetTile(pos, fDark);
                        }
                    }
                }

                // Thresholds
                floorMap.SetTile(new Vector3Int(-11, 27, 0), fDark);
                floorMap.SetTile(new Vector3Int(-11, 28, 0), fDark);
                floorMap.SetTile(new Vector3Int(-20, 24, 0), fDark);
                floorMap.SetTile(new Vector3Int(-20, 25, 0), fDark);
                floorMap.SetTile(new Vector3Int(-21, 24, 0), fDark);
                floorMap.SetTile(new Vector3Int(-21, 25, 0), fDark);

                Debug.Log("[MedBayVisualPassV5Applier] Floor tilemap updated: 5-column hazard, uniform steel floor, micro-variations applied.");
            }
        }

        // ==========================================
        // 2. DRAINAGE GRATE HIGHLIGHT OVERLAY
        // ==========================================
        GameObject grateOverlay = GetOrCreateChild(vpRoot, "VP6_Decal_Grate_Drainage_Rim");
        grateOverlay.transform.position = new Vector3(-16.50f, 27.05f, 0f);
        grateOverlay.transform.localScale = Vector3.one;
        SpriteRenderer gsr = grateOverlay.GetComponent<SpriteRenderer>();
        if (gsr == null) gsr = grateOverlay.AddComponent<SpriteRenderer>();
        gsr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP6_Decal_Grate_Drainage_Rim.png");
        gsr.sortingLayerName = "Floor";
        gsr.sortingOrder = 2;
        if (defaultLitMat != null) gsr.sharedMaterial = defaultLitMat;

        // ==========================================
        // 3. NORTH WALL BULKHEAD & HEAVY BASEBOARD
        // ==========================================
        GameObject baseboardGo = GetOrCreateChild(vpRoot, "VP6_Prop_Wall_Baseboard");
        baseboardGo.transform.position = new Vector3(-16.00f, 31.75f, 0f);
        baseboardGo.transform.localScale = Vector3.one;
        SpriteRenderer bsr = baseboardGo.GetComponent<SpriteRenderer>();
        if (bsr == null) bsr = baseboardGo.AddComponent<SpriteRenderer>();
        bsr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP6_Prop_Wall_Baseboard.png");
        bsr.sortingLayerName = "Floor";
        bsr.sortingOrder = 4;
        if (defaultLitMat != null) bsr.sharedMaterial = defaultLitMat;

        GameObject aoNorth = GameObject.Find("VP3_AO_Wall_North");
        if (aoNorth != null)
        {
            aoNorth.transform.position = new Vector3(-16.00f, 31.72f, 0f);
            aoNorth.transform.localScale = new Vector3(9.5f, 0.7f, 1f);
            SpriteRenderer aosr = aoNorth.GetComponent<SpriteRenderer>();
            if (aosr != null) aosr.color = new Color(0f, 0f, 0f, 0.45f);
        }

        // Reposition Wall-Mounted Props securely against wall
        RepositionProp("Prop_MedBay_Medicine_Cabinet", new Vector3(-19.20f, 31.45f, 0f));
        RepositionProp("Prop_MedBay_Wash_Station", new Vector3(-16.00f, 31.80f, 0f));
        RepositionProp("Prop_MedBay_EKG_Console", new Vector3(-11.55f, 31.45f, 0f));
        RepositionProp("VP3_Sign_Autopsy_Station", new Vector3(-16.00f, 32.35f, 0f));

        GameObject flGo = GameObject.Find("VP5_Prop_Wall_Fluorescent");
        if (flGo != null)
        {
            flGo.transform.position = new Vector3(-13.80f, 31.85f, 0f);
            Light2D flLight = flGo.GetComponentInChildren<Light2D>();
            if (flLight != null)
            {
                flLight.intensity = 0.65f;
                flLight.pointLightOuterRadius = 2.6f;
                flLight.color = new Color(0.78f, 0.88f, 0.95f, 1f);
            }
        }

        // ==========================================
        // 4. PIPE TERMINATION FLANGES & DOOR FRAMES
        // ==========================================
        Sprite flangeSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP6_Prop_Pipe_Flange_Cap.png");
        Sprite doorFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP6_Prop_Recessed_Door_Frame.png");

        // West Door: (-20.5, 25.0)
        CreateOrUpdateProp(vpRoot, "VP6_Flange_West_Top", new Vector3(-21.0f, 26.05f, 0f), flangeSprite, "Obstacles", 6, defaultLitMat);
        CreateOrUpdateProp(vpRoot, "VP6_Flange_West_Bottom", new Vector3(-21.0f, 23.95f, 0f), flangeSprite, "Obstacles", 6, defaultLitMat);
        CreateOrUpdateProp(vpRoot, "VP6_Frame_West", new Vector3(-20.5f, 25.0f, 0f), doorFrameSprite, "Obstacles", 4, defaultLitMat, new Vector3(0.75f, 0.75f, 1f));

        // East Door: (-10.5, 28.0)
        CreateOrUpdateProp(vpRoot, "VP6_Flange_East_Top", new Vector3(-11.0f, 29.05f, 0f), flangeSprite, "Obstacles", 6, defaultLitMat);
        CreateOrUpdateProp(vpRoot, "VP6_Flange_East_Bottom", new Vector3(-11.0f, 26.95f, 0f), flangeSprite, "Obstacles", 6, defaultLitMat);
        CreateOrUpdateProp(vpRoot, "VP6_Frame_East", new Vector3(-10.5f, 28.0f, 0f), doorFrameSprite, "Obstacles", 4, defaultLitMat, new Vector3(0.75f, 0.75f, 1f));

        // ==========================================
        // 5. MECHANICAL RHYTHM BREAKERS
        // ==========================================
        Sprite valveSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP6_Prop_Valve_Wheel.png");
        Sprite gaugeSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP6_Prop_Dual_Gauges.png");
        CreateOrUpdateProp(vpRoot, "VP6_Prop_Valve_West", new Vector3(-20.95f, 28.50f, 0f), valveSprite, "Obstacles", 4, defaultLitMat);
        CreateOrUpdateProp(vpRoot, "VP6_Prop_Gauges_East", new Vector3(-11.05f, 25.50f, 0f), gaugeSprite, "Obstacles", 4, defaultLitMat);

        // ==========================================
        // 6. PROPS POSITIONING & BALANCING
        // ==========================================
        RepositionProp("Prop_MedBay_Operating_Table", new Vector3(-16.00f, 27.35f, 0f));
        RepositionProp("Prop_MedBay_Surgical_Cart", new Vector3(-14.65f, 27.35f, 0f)); // Cleanly inside column -15/-14
        RepositionProp("VP5_Prop_Overturned_Surgical_Cart", new Vector3(-13.40f, 24.80f, 0f)); // Generous breathing room from O2 rack
        RepositionProp("Prop_MedBay_Oxygen_Rack", new Vector3(-11.75f, 24.62f, 0f));
        RepositionProp("Prop_MedBay_Body_Bag", new Vector3(-14.50f, 25.20f, 0f));

        // Blood drag connection
        GameObject bloodDrag = GameObject.Find("VP5_Decal_BloodDrag_Long");
        if (bloodDrag != null)
        {
            bloodDrag.transform.position = new Vector3(-12.50f, 26.60f, 0f);
            bloodDrag.SetActive(true);
        }

        // Southwest corner balancing
        RepositionProp("VP5_Prop_Medical_Clipboard", new Vector3(-18.00f, 25.20f, 0f));
        SetGameObjectActiveAndPos("VP3_Prop_WasteBin", true, new Vector3(-19.20f, 25.50f, 0f));
        SetGameObjectActiveAndPos("VP3_Prop_OverturnedStool", true, new Vector3(-18.70f, 24.60f, 0f));
        SetGameObjectActiveAndPos("VP4_Prop_Equipment_Crate", false, Vector3.zero); // Keep floor clean & uncluttered

        // ==========================================
        // 7. LIGHTING REBALANCE & ANTI-CRUSHED-BLACKS
        // ==========================================
        // Room Ambient: lifted to reveal 4 corner pipes, van gauges, and props
        GameObject ambLight = GameObject.Find("Light_MedBay_Room_Ambient");
        if (ambLight != null)
        {
            Light2D l2d = ambLight.GetComponent<Light2D>();
            if (l2d != null)
            {
                l2d.color = new Color(0.42f, 0.52f, 0.60f, 1f);
                l2d.intensity = 1.25f;
                l2d.pointLightInnerRadius = 2.5f;
                l2d.pointLightOuterRadius = 11.0f;
            }
        }

        // Surgical Lamp: balanced focal brightness without burning table
        GameObject surgLight = GameObject.Find("Light_MedBay_Surgical_Lamp");
        if (surgLight != null)
        {
            Light2D l2d = surgLight.GetComponent<Light2D>();
            if (l2d != null)
            {
                l2d.color = new Color(0.85f, 0.94f, 1.0f, 1f);
                l2d.intensity = 1.40f;
                l2d.pointLightInnerRadius = 0.8f;
                l2d.pointLightOuterRadius = 5.2f;
            }
        }

        // Door Indicators: illuminate door frames and thresholds
        GameObject indEast = GameObject.Find("Indicator_Door_MedBay");
        if (indEast != null)
        {
            Light2D l2d = indEast.GetComponent<Light2D>();
            if (l2d != null)
            {
                l2d.intensity = 0.60f;
                l2d.pointLightInnerRadius = 0.2f;
                l2d.pointLightOuterRadius = 1.8f;
            }
        }
        GameObject indWest = GameObject.Find("Indicator_Door_MedBay_West");
        if (indWest != null)
        {
            Light2D l2d = indWest.GetComponent<Light2D>();
            if (l2d != null)
            {
                l2d.intensity = 0.60f;
                l2d.pointLightInnerRadius = 0.2f;
                l2d.pointLightOuterRadius = 1.8f;
            }
        }

        // South Fill: crisp visibility for O2 tanks, turned cart, and body bag
        GameObject southFill = GameObject.Find("VP5_Light_South_Fill");
        if (southFill != null)
        {
            Light2D l2d = southFill.GetComponent<Light2D>();
            if (l2d != null)
            {
                l2d.color = new Color(0.48f, 0.58f, 0.65f, 1f);
                l2d.intensity = 0.75f;
                l2d.pointLightInnerRadius = 0.8f;
                l2d.pointLightOuterRadius = 5.8f;
            }
        }

        // Ceiling West: balanced ambient fill
        GameObject westCeil = GameObject.Find("Light_MedBay_Ceiling_West");
        if (westCeil != null)
        {
            Light2D l2d = westCeil.GetComponent<Light2D>();
            if (l2d != null)
            {
                l2d.color = new Color(0.70f, 0.82f, 0.90f, 1f);
                l2d.intensity = 0.90f;
                l2d.pointLightInnerRadius = 0.8f;
                l2d.pointLightOuterRadius = 4.2f;
            }
        }

        // Vignette tuning: open up the corners
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (v.profile != null && v.profile.TryGet<Vignette>(out var vig))
            {
                vig.intensity.value = 0.18f;
                vig.smoothness.value = 0.25f;
            }
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log("[MedBayVisualPassV5Applier] === FULL MEDBAY OVERHAUL COMPLETE & SAVED! ===");
    }

    private static GameObject GetOrCreateChild(GameObject parent, string name)
    {
        Transform t = parent.transform.Find(name);
        if (t != null) return t.gameObject;
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        return go;
    }

    private static void RepositionProp(string name, Vector3 pos)
    {
        GameObject go = GameObject.Find(name);
        if (go != null)
        {
            go.transform.position = pos;
        }
    }

    private static void SetGameObjectActiveAndPos(string name, bool active, Vector3 pos)
    {
        GameObject go = GameObject.Find(name);
        if (go != null)
        {
            go.SetActive(active);
            if (active) go.transform.position = pos;
        }
    }

    private static void CreateOrUpdateProp(GameObject parent, string name, Vector3 pos, Sprite sprite, string sortingLayer, int sortingOrder, Material mat, Vector3? scale = null)
    {
        if (sprite == null) return;
        GameObject go = GetOrCreateChild(parent, name);
        go.transform.position = pos;
        go.transform.localScale = scale.HasValue ? scale.Value : Vector3.one;
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder;
        if (mat != null) sr.sharedMaterial = mat;
    }
}
#endif
