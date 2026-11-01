#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using TheLastSignal.Entities.Environment;

public static class MedBayComprehensiveRedesignApplier
{
    private const string ScenePath = "Assets/Scenes/SampleScene_VisualPass_MedBay_v4.unity";
    private const string TileDir = "Assets/_Project/Tiles/MedBay";
    private const string PropsDir = "Assets/_Project/Sprites/Environments/MedBay/Props";

    [MenuItem("Tools/Apply MedBay Comprehensive Redesign")]
    public static void ApplyRedesign()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != ScenePath)
        {
            activeScene = EditorSceneManager.OpenScene(ScenePath);
        }

        Debug.Log("[MedBayComprehensiveRedesignApplier] Starting Comprehensive Environment Redesign...");

        Material defaultLitMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_Sprite_Lit_PixelSmooth.mat")
            ?? AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");

        GameObject vpRoot = GameObject.Find("VP5_VisualPass_Elements");
        if (vpRoot == null) vpRoot = new GameObject("VP5_VisualPass_Elements");

        // =========================================================================
        // 1. FLOOR TILEMAP & REFINED CONTAINMENT BOUNDARY
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

        foreach (var floorMap in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!floorMap.name.ToLower().Contains("floor")) continue;

            // Clear row 31 (Row 31 is vertical North wall)
            for (int x = -22; x <= -10; x++)
            {
                floorMap.SetTile(new Vector3Int(x, 31, 0), null);
            }

            // 8x7 walking floor grid
            for (int x = -19; x <= -12; x++)
            {
                for (int y = 24; y <= 30; y++)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);

                    // 5-Column Centered Containment Perimeter
                    if (y == 28 && x == -18) floorMap.SetTile(pos, cTL);
                    else if (y == 28 && x == -17) floorMap.SetTile(pos, cT);
                    else if (y == 28 && x == -16) floorMap.SetTile(pos, cTLabel); // Biohazard center label
                    else if (y == 28 && x == -15) floorMap.SetTile(pos, cT);
                    else if (y == 28 && x == -14) floorMap.SetTile(pos, cTR);

                    else if (y == 27 && x == -18) floorMap.SetTile(pos, cL);
                    else if (y == 27 && (x == -17 || x == -16)) floorMap.SetTile(pos, fGrate); // Recessed drainage grate
                    else if (y == 27 && x == -15) floorMap.SetTile(pos, fDark);
                    else if (y == 27 && x == -14) floorMap.SetTile(pos, cR);

                    else if (y == 26 && x == -18) floorMap.SetTile(pos, cBL);
                    else if (y == 26 && (x == -17 || x == -16 || x == -15)) floorMap.SetTile(pos, cB);
                    else if (y == 26 && x == -14) floorMap.SetTile(pos, cBR);

                    // Maintenance hatch
                    else if (x == -19 && y == 27) floorMap.SetTile(pos, fHatch);

                    // Controlled floor micro-variations
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

            // Door thresholds
            floorMap.SetTile(new Vector3Int(-11, 27, 0), fDark);
            floorMap.SetTile(new Vector3Int(-11, 28, 0), fDark);
            floorMap.SetTile(new Vector3Int(-20, 24, 0), fDark);
            floorMap.SetTile(new Vector3Int(-20, 25, 0), fDark);
            floorMap.SetTile(new Vector3Int(-21, 24, 0), fDark);
            floorMap.SetTile(new Vector3Int(-21, 25, 0), fDark);
        }

        // =========================================================================
        // 2. SPECIALIZED AUTOPSY TABLE & OVERHEAD SURGICAL LAMP
        // =========================================================================
        // Central Autopsy Workstation with Draped Specimen
        Sprite tableSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/Prop_MedBay_Operating_Table.png");
        GameObject tableGo = GameObject.Find("Prop_MedBay_Operating_Table");
        if (tableGo != null)
        {
            tableGo.transform.position = new Vector3(-16.00f, 27.35f, 0f);
            tableGo.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            SpriteRenderer sr = tableGo.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (tableSprite != null) sr.sprite = tableSprite;
                sr.sortingLayerName = "Obstacles";
                sr.sortingOrder = 2;
                if (defaultLitMat != null) sr.sharedMaterial = defaultLitMat;
            }
            BoxCollider2D bc = tableGo.GetComponent<BoxCollider2D>();
            if (bc != null)
            {
                bc.offset = new Vector2(0f, -0.15f);
                bc.size = new Vector2(3.2f, 1.4f);
            }
        }

        // Dedicated Ceiling-Suspended Articulated Surgical Lamp
        Sprite lampSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/Prop_MedBay_Surgical_Lamp_Overhead.png");
        CreateOrUpdateProp(vpRoot, "Prop_MedBay_Surgical_Lamp_Overhead", new Vector3(-16.15f, 28.05f, 0f), lampSprite, "Obstacles", 5, defaultLitMat, new Vector3(0.55f, 0.55f, 1f));

        // Task-Focused Cold Spotlight from Surgical Lamp
        GameObject surgLight = GameObject.Find("Light_MedBay_Surgical_Lamp");
        if (surgLight != null)
        {
            surgLight.transform.position = new Vector3(-16.00f, 27.35f, 0f);
            Light2D l2d = surgLight.GetComponent<Light2D>();
            if (l2d != null)
            {
                l2d.color = new Color(0.85f, 0.94f, 1.0f, 1f);
                l2d.intensity = 1.45f;
                l2d.pointLightInnerRadius = 0.75f;
                l2d.pointLightOuterRadius = 4.2f;
                l2d.falloffIntensity = 0.75f;
            }
        }

        // Stainless Steel Surgical Mayo Stand / Cart beside the table
        GameObject cart = GameObject.Find("Prop_MedBay_Surgical_Cart");
        if (cart != null)
        {
            cart.transform.position = new Vector3(-14.65f, 27.35f, 0f);
            cart.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            SpriteRenderer sr = cart.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingLayerName = "Obstacles";
                sr.sortingOrder = 3;
                if (defaultLitMat != null) sr.sharedMaterial = defaultLitMat;
            }
        }

        // =========================================================================
        // 3. ENVIRONMENTAL STORYTELLING: REFINED BLOOD TRAILS
        // =========================================================================
        Sprite bloodDragSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP5_Decal_BloodDrag_Long.png");
        GameObject dragGo = GameObject.Find("VP5_Decal_BloodDrag_Long");
        if (dragGo != null)
        {
            dragGo.transform.position = new Vector3(-12.80f, 26.50f, 0f);
            dragGo.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
            SpriteRenderer sr = dragGo.GetComponent<SpriteRenderer>();
            if (sr != null && bloodDragSprite != null)
            {
                sr.sprite = bloodDragSprite;
                sr.sortingLayerName = "Floor";
                sr.sortingOrder = 2;
                if (defaultLitMat != null) sr.sharedMaterial = defaultLitMat;
            }
        }

        Sprite bloodPoolSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP4_Decal_BloodSplatter_NW.png");
        GameObject poolGo = GameObject.Find("VP4_Decal_BloodSplatter_NW");
        if (poolGo != null)
        {
            poolGo.transform.position = new Vector3(-19.00f, 28.00f, 0f);
            poolGo.transform.localScale = new Vector3(0.90f, 0.90f, 1f);
            SpriteRenderer sr = poolGo.GetComponent<SpriteRenderer>();
            if (sr != null && bloodPoolSprite != null)
            {
                sr.sprite = bloodPoolSprite;
                sr.sortingLayerName = "Floor";
                sr.sortingOrder = 3;
                if (defaultLitMat != null) sr.sharedMaterial = defaultLitMat;
            }
        }

        // =========================================================================
        // 4. WALL ARCHITECTURE & BASEBOARD TRIM
        // =========================================================================
        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP6_Wall_Vertical_Steel_Panels.png");
        CreateOrUpdateProp(vpRoot, "VP6_Wall_Vertical_Steel_Panels", new Vector3(-16.00f, 31.68f, 0f), panelSprite, "Obstacles", -1, defaultLitMat);

        // Side Walls Modular Steel Cladding (West and East)
        Sprite sideWestSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP7_Wall_Side_Steel_Panels_West.png");
        CreateOrUpdateProp(vpRoot, "VP7_Wall_Side_Steel_Panels_West", new Vector3(-21.00f, 28.00f, 0f), sideWestSprite, "Obstacles", -2, defaultLitMat);

        Sprite sideEastSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP7_Wall_Side_Steel_Panels_East.png");
        CreateOrUpdateProp(vpRoot, "VP7_Wall_Side_Steel_Panels_East", new Vector3(-11.00f, 28.00f, 0f), sideEastSprite, "Obstacles", -2, defaultLitMat);

        // Heavy Corner Bulkhead Columns (Top-Left and Top-Right)
        Sprite pillarTLSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP7_Prop_Corner_Bulkhead_Pillar_TL.png");
        CreateOrUpdateProp(vpRoot, "VP7_Prop_Corner_Bulkhead_Pillar_TL", new Vector3(-20.50f, 32.18f, 0f), pillarTLSprite, "Obstacles", 3, defaultLitMat);

        Sprite pillarTRSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP7_Prop_Corner_Bulkhead_Pillar_TR.png");
        CreateOrUpdateProp(vpRoot, "VP7_Prop_Corner_Bulkhead_Pillar_TR", new Vector3(-11.50f, 32.18f, 0f), pillarTRSprite, "Obstacles", 3, defaultLitMat);

        // =========================================================================
        // 4.1 ASYMMETRIC INDUSTRIAL PIPEWORK OVERHAUL (Zero Repetitive Gauges)
        // =========================================================================
        Sprite pipeNorthSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP8_Wall_Pipes_North_Bulkhead.png");
        CreateOrUpdateProp(vpRoot, "VP8_Wall_Pipes_North_Bulkhead", new Vector3(-15.50f, 32.50f, 0f), pipeNorthSprite, "Obstacles", 1, defaultLitMat);

        Sprite pipeSouthSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP8_Wall_Pipes_South_Trunk.png");
        CreateOrUpdateProp(vpRoot, "VP8_Wall_Pipes_South_Trunk", new Vector3(-15.50f, 23.50f, 0f), pipeSouthSprite, "Obstacles", 1, defaultLitMat);

        Sprite pipeWestSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP8_Wall_Pipes_West_Decon.png");
        CreateOrUpdateProp(vpRoot, "VP8_Wall_Pipes_West_Decon", new Vector3(-20.50f, 28.00f, 0f), pipeWestSprite, "Obstacles", 1, defaultLitMat);

        Sprite pipeEastSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP8_Wall_Pipes_East_Oxygen.png");
        CreateOrUpdateProp(vpRoot, "VP8_Wall_Pipes_East_Oxygen", new Vector3(-10.50f, 28.00f, 0f), pipeEastSprite, "Obstacles", 1, defaultLitMat);

        DisableGameObjectByName("VP6_Flange_West_Top");
        DisableGameObjectByName("VP6_Flange_West_Bottom");
        DisableGameObjectByName("VP6_Flange_East_Top");
        DisableGameObjectByName("VP6_Flange_East_Bottom");

        // Replace repetitive tilemap pipe sprites with invisible collision tiles (preserves Grid colliders 100%)
        var invisTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/MedBay/MedBay_Wall_Collision_Invisible.asset");
        var cornerTLTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/MedBay/MedBay_Wall_Corner_TL.asset");
        var cornerTRTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/MedBay/MedBay_Wall_Corner_TR.asset");
        var cornerBLTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/MedBay/MedBay_Wall_Corner_BL.asset");
        var cornerBRTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/MedBay/MedBay_Wall_Corner_BR.asset");

        var wallTm = GameObject.Find("Wall_Tilemap")?.GetComponent<Tilemap>();
        if (wallTm != null && invisTile != null)
        {
            // North & South Wall Runs: replace repetitive pipe tiles with invisible collision tiles
            for (int x = -20; x <= -12; x++)
            {
                wallTm.SetTile(new Vector3Int(x, 32, 0), invisTile);
                wallTm.SetTile(new Vector3Int(x, 23, 0), invisTile);
            }
            // Clear stray stubs outside MedBay room at x = -22
            wallTm.SetTile(new Vector3Int(-22, 23, 0), null);
            wallTm.SetTile(new Vector3Int(-22, 26, 0), null);

            // West Wall Run: replace with invisible collision tiles (leaving door opening at y = 24, 25)
            for (int y = 26; y <= 31; y++)
            {
                wallTm.SetTile(new Vector3Int(-21, y, 0), invisTile);
            }
            // East Wall Run: replace with invisible collision tiles (leaving door opening at y = 27, 28)
            for (int y = 24; y <= 26; y++)
            {
                wallTm.SetTile(new Vector3Int(-11, y, 0), invisTile);
            }
            for (int y = 29; y <= 31; y++)
            {
                wallTm.SetTile(new Vector3Int(-11, y, 0), invisTile);
            }

            // Ensure 4 Corners use the updated corner tiles
            if (cornerTLTile != null) wallTm.SetTile(new Vector3Int(-21, 32, 0), cornerTLTile);
            if (cornerTRTile != null) wallTm.SetTile(new Vector3Int(-11, 32, 0), cornerTRTile);
            if (cornerBLTile != null) wallTm.SetTile(new Vector3Int(-21, 23, 0), cornerBLTile);
            if (cornerBRTile != null) wallTm.SetTile(new Vector3Int(-11, 23, 0), cornerBRTile);

            EditorUtility.SetDirty(wallTm);
        }

        Sprite baseboardSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP6_Wall_Baseboard_Trim.png");
        CreateOrUpdateProp(vpRoot, "VP6_Wall_Baseboard_Trim", new Vector3(-16.00f, 31.08f, 0f), baseboardSprite, "Obstacles", 1, defaultLitMat);

        Sprite aoSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP6_Wall_AO_Shadow.png");
        CreateOrUpdateProp(vpRoot, "VP6_Wall_AO_Shadow", new Vector3(-16.00f, 30.86f, 0f), aoSprite, "Floor", 15, defaultLitMat);

        DisableGameObjectByName("VP3_AO_Wall_North");

        // Autopsy Station Plaque
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

        // Wall Props
        RepositionProp("Prop_MedBay_Medicine_Cabinet", new Vector3(-19.20f, 31.50f, 0f));
        RepositionProp("VP3_ContactShadow_Cabinet", new Vector3(-19.20f, 30.95f, 0f));

        RepositionProp("Prop_MedBay_Wash_Station", new Vector3(-16.00f, 31.45f, 0f));
        RepositionProp("VP3_ContactShadow_WashStation", new Vector3(-16.00f, 30.95f, 0f));

        RepositionProp("Prop_MedBay_EKG_Console", new Vector3(-11.55f, 31.40f, 0f));
        RepositionProp("VP3_ContactShadow_EKG", new Vector3(-11.55f, 30.95f, 0f));

        // =========================================================================
        // 4.2 X-RAY LIGHTBOX & NARRATIVE WALL CONTAMINATION
        // =========================================================================
        // Anomalous Specimen X-Ray Lightbox (Backlit medical illuminator)
        Sprite xraySprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP9_Prop_Wall_XRay_Lightbox.png");
        CreateOrUpdateProp(vpRoot, "VP9_Prop_Wall_XRay_Lightbox", new Vector3(-14.80f, 31.55f, 0f), xraySprite, "Obstacles", 2, defaultLitMat);

        // Dedicated Cyan Backlight for X-Ray Lightbox
        GameObject xrayLight = GameObject.Find("Light_MedBay_XRay_Viewer");
        if (xrayLight == null)
        {
            GameObject xrayGo = GameObject.Find("VP9_Prop_Wall_XRay_Lightbox");
            if (xrayGo != null)
            {
                xrayLight = new GameObject("Light_MedBay_XRay_Viewer");
                xrayLight.transform.SetParent(xrayGo.transform);
                xrayLight.transform.localPosition = new Vector3(0f, 0f, 0f);
                var l2d = xrayLight.AddComponent<Light2D>();
                l2d.lightType = Light2D.LightType.Point;
                l2d.color = new Color(0.48f, 0.88f, 1.0f, 1f); // #7CE0FF
                l2d.intensity = 0.85f;
                l2d.pointLightInnerRadius = 0.20f;
                l2d.pointLightOuterRadius = 2.4f;
                l2d.falloffIntensity = 0.70f;
            }
        }

        // Bloody Handprint Wall Slide near Decontamination Sink
        Sprite bloodSlideSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP9_Decal_BloodHand_Slide.png");
        CreateOrUpdateProp(vpRoot, "VP9_Decal_BloodHand_Slide", new Vector3(-15.40f, 31.35f, 0f), bloodSlideSprite, "Obstacles", 2, defaultLitMat);

        // Deep Claw Scrapes gouging the North Wall steel plate
        Sprite clawScrapesSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP9_Decal_Claw_Scrapes.png");
        CreateOrUpdateProp(vpRoot, "VP9_Decal_Claw_Scrapes_North", new Vector3(-17.60f, 31.60f, 0f), clawScrapesSprite, "Obstacles", 2, defaultLitMat);

        // Secondary Claw Scrapes on West Wall near Biohazard Drain
        CreateOrUpdateProp(vpRoot, "VP9_Decal_Claw_Scrapes_West", new Vector3(-20.45f, 28.80f, 0f), clawScrapesSprite, "Obstacles", 2, defaultLitMat, new Vector3(0.85f, 0.85f, 1f));

        // Task Location: Marker stays exactly at Task location (-11.55, 31.15)
        GameObject taskLoc = GameObject.Find("TaskLoc_MedBay");
        if (taskLoc != null)
        {
            taskLoc.transform.position = new Vector3(-11.55f, 31.15f, 0f);
        }

        // =========================================================================
        // 5. SIDE DOORS (3/4 CUTAWAY PERSPECTIVE) & FIX SHADOW LEAKS
        // =========================================================================
        Sprite doorWestSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/Door_MedBay_SideProfile_West.png");
        Sprite doorEastSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/Door_MedBay_SideProfile_East.png");

        GameObject doorWest = GameObject.Find("Door_MedBay_West");
        if (doorWest != null)
        {
            doorWest.transform.position = new Vector3(-20.50f, 25.00f, 0f);
            doorWest.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
            SpriteRenderer sr = doorWest.GetComponent<SpriteRenderer>();
            if (sr != null && doorWestSprite != null) sr.sprite = doorWestSprite;
            DoorController dc = doorWest.GetComponent<DoorController>();
            if (dc != null && doorWestSprite != null) dc.SetSprites(doorWestSprite, doorWestSprite);
        }

        GameObject doorEast = GameObject.Find("Door_MedBay");
        if (doorEast != null)
        {
            doorEast.transform.position = new Vector3(-10.60f, 28.00f, 0f);
            doorEast.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
            SpriteRenderer sr = doorEast.GetComponent<SpriteRenderer>();
            if (sr != null && doorEastSprite != null) sr.sprite = doorEastSprite;
            DoorController dc = doorEast.GetComponent<DoorController>();
            if (dc != null && doorEastSprite != null) dc.SetSprites(doorEastSprite, doorEastSprite);
        }

        // Fix CargoHold Shadow_West leak
        GameObject shadowWest = GameObject.Find("CargoHold_Wall_Contact_Shadows/Shadow_West");
        if (shadowWest != null)
        {
            shadowWest.transform.localScale = new Vector3(0.70f, 0.75f, 1.0f);
            shadowWest.transform.position = new Vector3(-19.65f, -4.50f, 0.0f);
        }

        // Disable orphaned or conflicting overlays
        DisableGameObjectByName("VP3_AO_Wall_West_Top");
        DisableGameObjectByName("VP3_WestDoor_ShadowBlocker");
        DisableGameObjectByName("VP3_AO_Wall_East_Top");
        DisableGameObjectByName("VP3_EastDoor_ShadowBlocker");
        DisableGameObjectByName("VP4_Shadow_BiohazardBin");
        DisableGameObjectByName("VP4_Shadow_EquipmentCrate");
        DisableGameObjectByName("VP3_Prop_WasteBin");
        DisableGameObjectByName("VP3_Prop_OverturnedStool");
        DisableGameObjectByName("VP3_Prop_OverturnedTray");
        DisableGameObjectByName("VP6_Prop_Valve_West");
        DisableGameObjectByName("VP6_Prop_Gauges_East");
        DisableGameObjectByName("VP4_Decal_O2_Stencil");
        DisableGameObjectByName("VP6_Decal_Grate_Drainage_Rim");
        DisableGameObjectByName("VP3_Decal_Grate_Grime_Table");
        DisableGameObjectByName("VP3_Decal_BodyBag_Breach");
        DisableGameObjectByName("VP5_Wall_Baseboard_North");
        DisableGameObjectByName("VP6_Prop_Wall_Baseboard");

        // Rescaled Clipboard
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

        // 3/4 Overturned Cart
        Sprite ocSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsDir}/VP5_Prop_Overturned_Cart.png");
        GameObject toppledCart = GameObject.Find("VP5_Prop_Overturned_Surgical_Cart");
        if (toppledCart != null)
        {
            toppledCart.transform.position = new Vector3(-13.40f, 24.80f, 0f);
            toppledCart.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            toppledCart.transform.rotation = Quaternion.identity;
            SpriteRenderer sr = toppledCart.GetComponent<SpriteRenderer>();
            if (sr != null && ocSprite != null)
            {
                sr.sprite = ocSprite;
                sr.sortingLayerName = "Obstacles";
                sr.sortingOrder = 2;
                if (defaultLitMat != null) sr.sharedMaterial = defaultLitMat;
            }
        }

        // =========================================================================
        // 6. ATMOSPHERIC LIGHTING & COLOR BALANCE
        // =========================================================================
        // Room Ambient: Dark neutral graphite fill
        GameObject ambLight = GameObject.Find("Light_MedBay_Room_Ambient");
        if (ambLight != null)
        {
            Light2D l2d = ambLight.GetComponent<Light2D>();
            if (l2d != null)
            {
                l2d.color = new Color(0.38f, 0.46f, 0.52f, 1f);
                l2d.intensity = 1.10f;
                l2d.pointLightInnerRadius = 2.0f;
                l2d.pointLightOuterRadius = 10.5f;
            }
        }

        // EKG Console subtle green emission
        GameObject ekgLight = GameObject.Find("Light_MedBay_EKG_Monitor");
        if (ekgLight == null)
        {
            GameObject ekgGo = GameObject.Find("Prop_MedBay_EKG_Console");
            if (ekgGo != null)
            {
                ekgLight = new GameObject("Light_MedBay_EKG_Monitor");
                ekgLight.transform.SetParent(ekgGo.transform);
                ekgLight.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                var l2d = ekgLight.AddComponent<Light2D>();
                l2d.lightType = Light2D.LightType.Point;
                l2d.color = new Color(0.31f, 0.73f, 0.44f, 1f); // #4FBA70
                l2d.intensity = 0.65f;
                l2d.pointLightInnerRadius = 0.2f;
                l2d.pointLightOuterRadius = 2.2f;
            }
        }

        // Fluorescent Tube soft downward wall illumination
        GameObject flGo = GameObject.Find("VP5_Prop_Wall_Fluorescent");
        if (flGo != null)
        {
            Light2D flLight = flGo.GetComponentInChildren<Light2D>();
            if (flLight != null)
            {
                flLight.color = new Color(0.80f, 0.90f, 0.98f, 1f);
                flLight.intensity = 0.55f;
                flLight.pointLightInnerRadius = 0.3f;
                flLight.pointLightOuterRadius = 2.8f;
            }
        }

        // SE Corner: Oxygen Rack Illumination so cylinders and rack base are clearly visible
        GameObject o2Light = GameObject.Find("Light_MedBay_SE_Oxygen");
        if (o2Light == null)
        {
            o2Light = new GameObject("Light_MedBay_SE_Oxygen");
            o2Light.transform.SetParent(vpRoot.transform);
            o2Light.transform.position = new Vector3(-12.80f, 24.20f, 0f);
            var l2d = o2Light.AddComponent<Light2D>();
            l2d.lightType = Light2D.LightType.Point;
            l2d.color = new Color(0.75f, 0.82f, 0.90f, 1f);
            l2d.intensity = 0.65f;
            l2d.pointLightInnerRadius = 0.4f;
            l2d.pointLightOuterRadius = 3.5f;
        }
        else
        {
            var l2d = o2Light.GetComponent<Light2D>();
            if (l2d != null)
            {
                l2d.color = new Color(0.75f, 0.82f, 0.90f, 1f);
                l2d.intensity = 0.65f;
                l2d.pointLightInnerRadius = 0.4f;
                l2d.pointLightOuterRadius = 3.5f;
            }
        }

        // Vignette
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
        Debug.Log("[MedBayComprehensiveRedesignApplier] === COMPREHENSIVE REDESIGN APPLIED AND SAVED SUCCESSFULLY! ===");
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
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var go in all)
        {
            if (go.name == name && go.scene.isLoaded)
            {
                go.SetActive(false);
            }
        }
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
