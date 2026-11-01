#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using TheLastSignal.Entities.Environment;

public static class MedBayArchitecturalPassApplier
{
    private const string ScenePath = "Assets/Scenes/SampleScene_VisualPass_MedBay_v4.unity";
    private const string TileDir = "Assets/_Project/Tiles/MedBay";
    private const string PropsDir = "Assets/_Project/Sprites/Environments/MedBay/Props";

    [MenuItem("Tools/Apply MedBay Architectural Overhaul")]
    public static void ApplyPass()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != ScenePath)
        {
            activeScene = EditorSceneManager.OpenScene(ScenePath);
        }

        Debug.Log("[MedBayArchitecturalPassApplier] Starting architectural overhaul...");

        Material defaultLitMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_Sprite_Lit_PixelSmooth.mat")
            ?? AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");

        GameObject vpRoot = GameObject.Find("VP5_VisualPass_Elements");
        if (vpRoot == null) vpRoot = new GameObject("VP5_VisualPass_Elements");

        // =========================================================================
        // 1. TILEMAP & LAYER ARCHITECTURE FIXES
        // =========================================================================
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

        // Apply across all Tilemaps that represent floor in the scene
        foreach (var floorMap in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!floorMap.name.ToLower().Contains("floor")) continue;

            // 1. CLEAR row 31 (Row 31 is the vertical North wall, NOT horizontal floor!)
            for (int x = -22; x <= -10; x++)
            {
                floorMap.SetTile(new Vector3Int(x, 31, 0), null);
            }

            // 2. Re-paint 8x7 floor grid: X in [-19, -12], Y in [24, 30]
            for (int x = -19; x <= -12; x++)
            {
                for (int y = 24; y <= 30; y++)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);

                    // --- 5-COLUMN PERFECTLY CENTERED HAZARD (Center X = -16.00) ---
                    if (y == 28 && x == -18) floorMap.SetTile(pos, cTL);
                    else if (y == 28 && x == -17) floorMap.SetTile(pos, cT);
                    else if (y == 28 && x == -16) floorMap.SetTile(pos, cTLabel); // SPECIMEN CONTAINMENT at table center
                    else if (y == 28 && x == -15) floorMap.SetTile(pos, cT);
                    else if (y == 28 && x == -14) floorMap.SetTile(pos, cTR);

                    else if (y == 27 && x == -18) floorMap.SetTile(pos, cL);
                    else if (y == 27 && (x == -17 || x == -16)) floorMap.SetTile(pos, fGrate); // Single clean recessed grate
                    else if (y == 27 && x == -15) floorMap.SetTile(pos, fDark);
                    else if (y == 27 && x == -14) floorMap.SetTile(pos, cR);

                    else if (y == 26 && x == -18) floorMap.SetTile(pos, cBL);
                    else if (y == 26 && (x == -17 || x == -16 || x == -15)) floorMap.SetTile(pos, cB);
                    else if (y == 26 && x == -14) floorMap.SetTile(pos, cBR);

                    // Conduit maintenance hatch
                    else if (x == -19 && y == 27) floorMap.SetTile(pos, fHatch);

                    // Organic stains
                    else if (x == -19 && y == 28) floorMap.SetTile(pos, sNW);
                    else if (x == -13 && y == 25) floorMap.SetTile(pos, sSE);

                    // Micro-variations across open floor to break repetition
                    else if (x == -19 && y == 29) floorMap.SetTile(pos, fScratched);
                    else if (x == -19 && y == 25) floorMap.SetTile(pos, fStained);
                    else if (x == -19 && y == 24) floorMap.SetTile(pos, fRusted);
                    else if (x == -13 && y == 29) floorMap.SetTile(pos, fRusted);
                    else if (x == -13 && y == 26) floorMap.SetTile(pos, fScratched);
                    else if (x == -12 && y == 25) floorMap.SetTile(pos, fStained);
                    else if (x == -17 && y == 24) floorMap.SetTile(pos, fRusted);
                    else if (x == -15 && y == 24) floorMap.SetTile(pos, fScratched);
                    else if (x == -17 && y == 29) floorMap.SetTile(pos, fScratched);
                    else if (x == -15 && y == 29) floorMap.SetTile(pos, fStained);
                    else if (x == -16 && y == 30) floorMap.SetTile(pos, fScratched);
                    else if (x == -14 && y == 30) floorMap.SetTile(pos, fStained);
                    else
                    {
                        floorMap.SetTile(pos, fDark);
                    }
                }
            }

            // Doorway thresholds: dark steel
            floorMap.SetTile(new Vector3Int(-11, 27, 0), fDark);
            floorMap.SetTile(new Vector3Int(-11, 28, 0), fDark);
            floorMap.SetTile(new Vector3Int(-20, 24, 0), fDark);
            floorMap.SetTile(new Vector3Int(-20, 25, 0), fDark);
            floorMap.SetTile(new Vector3Int(-21, 24, 0), fDark);
            floorMap.SetTile(new Vector3Int(-21, 25, 0), fDark);
        }

        // =========================================================================
        // 2. WALL-TRIM BASEBOARD, AO SHADOW & VERTICAL STEEL PANELS
        // =========================================================================
        // Vertical steel panels on North wall: 96px panel seams offset from 128px floor grid
        // Height 1.375 units (176px at 128 PPU), center at Y = 31.68
        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP6_Wall_Vertical_Steel_Panels.png");
        CreateOrUpdateProp(vpRoot, "VP6_Wall_Vertical_Steel_Panels", new Vector3(-16.00f, 31.68f, 0f), panelSprite, "Obstacles", -1, defaultLitMat);

        // Dedicated Wall-Trim / Baseboard: clean industrial steel kickplate along junction (Y = 31.08)
        Sprite baseboardSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP6_Wall_Baseboard_Trim.png");
        CreateOrUpdateProp(vpRoot, "VP6_Wall_Baseboard_Trim", new Vector3(-16.00f, 31.08f, 0f), baseboardSprite, "Obstacles", 1, defaultLitMat);

        // Dark semi-transparent Ambient Occlusion shadow tile (opacity ~50%) casting downward onto top floor row
        Sprite aoSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP6_Wall_AO_Shadow.png");
        CreateOrUpdateProp(vpRoot, "VP6_Wall_AO_Shadow", new Vector3(-16.00f, 30.86f, 0f), aoSprite, "Floor", 15, defaultLitMat);

        // Disable old north AO
        DisableGameObjectByName("VP3_AO_Wall_North");

        // Autopsy Station plaque: top center of wall under north pipes
        GameObject plaque = GameObject.Find("VP3_Sign_Autopsy_Station");
        if (plaque != null)
        {
            plaque.SetActive(true);
            plaque.transform.position = new Vector3(-16.00f, 32.10f, 0f);
            SpriteRenderer sr = plaque.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingLayerName = "Obstacles";
                sr.sortingOrder = 10;
                if (defaultLitMat != null) sr.sharedMaterial = defaultLitMat;
            }
        }

        // =========================================================================
        // 3. PERSPECTIVE & PROPS STANDARDIZATION (3/4 TOP-DOWN)
        // =========================================================================
        // Side doors in 3/4 cutaway side-profile perspective
        Sprite doorWestSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/Door_MedBay_SideProfile_West.png");
        Sprite doorEastSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/Door_MedBay_SideProfile_East.png");

        GameObject doorWest = GameObject.Find("Door_MedBay_West");
        if (doorWest != null)
        {
            doorWest.transform.position = new Vector3(-20.50f, 25.00f, 0f);
            doorWest.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
            SpriteRenderer sr = doorWest.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (doorWestSprite != null) sr.sprite = doorWestSprite;
                sr.sortingLayerName = "Obstacles";
                sr.sortingOrder = 5;
                if (defaultLitMat != null) sr.sharedMaterial = defaultLitMat;
            }
            DoorController dc = doorWest.GetComponent<DoorController>();
            if (dc != null && doorWestSprite != null) dc.SetSprites(doorWestSprite, doorWestSprite);
        }

        GameObject frameWest = GameObject.Find("VP6_Frame_West");
        if (frameWest != null)
        {
            frameWest.transform.position = new Vector3(-20.50f, 25.00f, 0f);
            frameWest.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
        }

        GameObject doorEast = GameObject.Find("Door_MedBay");
        if (doorEast != null)
        {
            doorEast.transform.position = new Vector3(-10.60f, 28.00f, 0f);
            doorEast.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
            SpriteRenderer sr = doorEast.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (doorEastSprite != null) sr.sprite = doorEastSprite;
                sr.sortingLayerName = "Obstacles";
                sr.sortingOrder = 5;
                if (defaultLitMat != null) sr.sharedMaterial = defaultLitMat;
            }
            DoorController dc = doorEast.GetComponent<DoorController>();
            if (dc != null && doorEastSprite != null) dc.SetSprites(doorEastSprite, doorEastSprite);
        }

        GameObject frameEast = GameObject.Find("VP6_Frame_East");
        if (frameEast != null)
        {
            frameEast.transform.position = new Vector3(-10.60f, 28.00f, 0f);
            frameEast.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
        }

        // Rescaled Clipboard: < 1/4 of a floor tile
        GameObject clip = GameObject.Find("VP5_Prop_Medical_Clipboard");
        if (clip != null)
        {
            clip.transform.position = new Vector3(-18.00f, 25.15f, 0f);
            clip.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            SpriteRenderer sr = clip.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingLayerName = "Floor";
                sr.sortingOrder = 3;
            }
        }

        // CRT Monitor: Scaled down to match wash sink and autopsy table
        GameObject crt = GameObject.Find("Prop_MedBay_EKG_Console");
        if (crt != null)
        {
            crt.transform.position = new Vector3(-11.55f, 31.40f, 0f);
            crt.transform.localScale = new Vector3(0.50f, 0.50f, 1f);
        }
        RepositionProp("VP3_ContactShadow_EKG", new Vector3(-11.55f, 30.95f, 0f));

        // Medical Cabinet: Base sits strictly on wall line (Y >= 31.00)
        GameObject cabinet = GameObject.Find("Prop_MedBay_Medicine_Cabinet");
        if (cabinet != null)
        {
            cabinet.transform.position = new Vector3(-19.20f, 31.50f, 0f);
            cabinet.transform.localScale = new Vector3(0.68f, 0.68f, 1f);
        }
        RepositionProp("VP3_ContactShadow_Cabinet", new Vector3(-19.20f, 30.95f, 0f));

        // Center Wash Sink: Aligned with wall line
        GameObject sink = GameObject.Find("Prop_MedBay_Wash_Station");
        if (sink != null)
        {
            sink.transform.position = new Vector3(-16.00f, 31.45f, 0f);
            sink.transform.localScale = new Vector3(0.70f, 0.70f, 1f);
        }
        RepositionProp("VP3_ContactShadow_WashStation", new Vector3(-16.00f, 30.95f, 0f));

        // Autopsy Table & Surgical Cart
        RepositionProp("Prop_MedBay_Operating_Table", new Vector3(-16.00f, 27.35f, 0f));
        RepositionProp("Prop_MedBay_Surgical_Cart", new Vector3(-14.65f, 27.35f, 0f));

        // Overturned Cart in matching 3/4 perspective
        Sprite ocSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP6_Prop_Overturned_Cart_3D.png");
        GameObject cart = GameObject.Find("VP5_Prop_Overturned_Surgical_Cart");
        if (cart != null)
        {
            cart.transform.position = new Vector3(-13.40f, 24.80f, 0f);
            cart.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            cart.transform.rotation = Quaternion.identity;
            SpriteRenderer sr = cart.GetComponent<SpriteRenderer>();
            if (sr != null && ocSprite != null)
            {
                sr.sprite = ocSprite;
                sr.sortingLayerName = "Obstacles";
                sr.sortingOrder = 2;
                if (defaultLitMat != null) sr.sharedMaterial = defaultLitMat;
            }
        }

        // =========================================================================
        // 4. CLEANING SPRITE ARTIFACTS & OUT-OF-BOUNDS PROPS
        // =========================================================================
        // Remove long vertical black line on far-left (CargoHold Shadow_West leak!)
        GameObject shadowWest = GameObject.Find("CargoHold_Wall_Contact_Shadows/Shadow_West");
        if (shadowWest != null)
        {
            shadowWest.transform.localScale = new Vector3(0.70f, 0.75f, 1.0f);
            shadowWest.transform.position = new Vector3(-19.65f, -4.50f, 0.0f);
        }

        DisableGameObjectByName("VP3_AO_Wall_West_Top");
        DisableGameObjectByName("VP3_WestDoor_ShadowBlocker");
        DisableGameObjectByName("VP3_AO_Wall_East_Top");
        DisableGameObjectByName("VP3_EastDoor_ShadowBlocker");

        // Remove orphaned shadows of disabled props
        DisableGameObjectByName("VP4_Shadow_BiohazardBin");
        DisableGameObjectByName("VP4_Shadow_EquipmentCrate");

        // Remove floating grey cylinder (lon nho) and triangular shadow on bottom-left
        DisableGameObjectByName("VP3_Prop_WasteBin");
        DisableGameObjectByName("VP3_Prop_OverturnedStool");

        // Remove rectangular grey patch and brown circle cutting over bottom hazard stripe
        DisableGameObjectByName("VP3_Prop_OverturnedTray");

        // Remove protruding red valve on outer-left margin
        DisableGameObjectByName("VP6_Prop_Valve_West");

        // Remove flat yellow circles on right pipe border
        DisableGameObjectByName("VP6_Prop_Gauges_East");
        DisableGameObjectByName("VP4_Decal_O2_Stencil");

        // Remove overlapping double grate decal with grey circles
        DisableGameObjectByName("VP6_Decal_Grate_Drainage_Rim");
        DisableGameObjectByName("VP3_Decal_Grate_Grime_Table");

        // Remove old unused baseboard objects
        DisableGameObjectByName("VP5_Wall_Baseboard_North");
        DisableGameObjectByName("VP6_Prop_Wall_Baseboard");

        // =========================================================================
        // 5. LIGHTING & ATMOSPHERE (UNITY 2D LIGHTS)
        // =========================================================================
        // 1. Surgical Spotlight: Focused cold beam on autopsy table
        GameObject surgLight = GameObject.Find("Light_MedBay_Surgical_Lamp");
        if (surgLight != null)
        {
            surgLight.transform.position = new Vector3(-16.00f, 27.50f, 0f);
            Light2D l2d = surgLight.GetComponent<Light2D>();
            if (l2d != null)
            {
                l2d.color = new Color(0.85f, 0.94f, 1.0f, 1f);
                l2d.intensity = 1.35f;
                l2d.pointLightInnerRadius = 0.7f;
                l2d.pointLightOuterRadius = 4.8f;
            }
        }

        // 2. Fluorescent Wall Light: Soft downward illumination
        GameObject flGo = GameObject.Find("VP5_Prop_Wall_Fluorescent");
        if (flGo != null)
        {
            flGo.transform.position = new Vector3(-13.80f, 31.85f, 0f);
            Light2D flLight = flGo.GetComponentInChildren<Light2D>();
            if (flLight != null)
            {
                flLight.color = new Color(0.80f, 0.90f, 0.98f, 1f);
                flLight.intensity = 0.60f;
                flLight.pointLightInnerRadius = 0.3f;
                flLight.pointLightOuterRadius = 2.8f;
            }
        }

        // 3. Room Ambient & Fill
        GameObject ambLight = GameObject.Find("Light_MedBay_Room_Ambient");
        if (ambLight != null)
        {
            Light2D l2d = ambLight.GetComponent<Light2D>();
            if (l2d != null)
            {
                l2d.color = new Color(0.40f, 0.50f, 0.58f, 1f);
                l2d.intensity = 1.15f;
                l2d.pointLightInnerRadius = 2.0f;
                l2d.pointLightOuterRadius = 10.5f;
            }
        }

        // 4. Door Indicators: Soft glowing red lights
        GameObject indEast = GameObject.Find("Indicator_Door_MedBay");
        if (indEast != null)
        {
            indEast.transform.position = new Vector3(-10.60f, 29.15f, 0f);
            Light2D l2d = indEast.GetComponent<Light2D>();
            if (l2d != null)
            {
                l2d.intensity = 0.55f;
                l2d.pointLightInnerRadius = 0.15f;
                l2d.pointLightOuterRadius = 1.6f;
            }
        }
        GameObject indWest = GameObject.Find("Indicator_Door_MedBay_West");
        if (indWest != null)
        {
            indWest.transform.position = new Vector3(-20.50f, 26.15f, 0f);
            Light2D l2d = indWest.GetComponent<Light2D>();
            if (l2d != null)
            {
                l2d.intensity = 0.55f;
                l2d.pointLightInnerRadius = 0.15f;
                l2d.pointLightOuterRadius = 1.6f;
            }
        }

        // 5. Balanced corner vignetting
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (v.profile != null && v.profile.TryGet<Vignette>(out var vig))
            {
                vig.intensity.value = 0.16f;
                vig.smoothness.value = 0.25f;
            }
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log("[MedBayArchitecturalPassApplier] === ALL ISSUES RESOLVED AND SCENE SAVED SUCCESSFULLY! ===");
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
        if (go != null) go.transform.position = pos;
    }

    private static void DisableGameObjectByName(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null) go.SetActive(false);
    }

    private static void CreateOrUpdateProp(GameObject parent, string name, Vector3 pos, Sprite sprite, string sortingLayer, int sortingOrder, Material mat, Vector3? scale = null)
    {
        if (sprite == null) return;
        GameObject go = GetOrCreateChild(parent, name);
        go.SetActive(true);
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
