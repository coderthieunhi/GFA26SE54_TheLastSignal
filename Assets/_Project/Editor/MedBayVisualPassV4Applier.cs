#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using TheLastSignal.Entities.Environment;

public static class MedBayVisualPassV4Applier
{
    private const string ScenePath = "Assets/Scenes/SampleScene_VisualPass_MedBay_v4.unity";

    [MenuItem("Tools/Apply MedBay Visual Pass v4")]
    public static void ApplyPass()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != ScenePath)
        {
            activeScene = EditorSceneManager.OpenScene(ScenePath);
        }

        Debug.Log("[MedBayVisualPassV4Applier] Starting Pass v4 modifications...");

        // 1. Disable problematic additions from v3/previous turn
        DisableGameObjectByName("VP5_Wall_Baseboard_North");
        DisableGameObjectByName("VP5_Prop_Wall_Emergency_Board");
        DisableGameObjectByName("VP5_Prop_Damaged_Pipe_Rust");
        DisableGameObjectByName("VP3_WestDoor_ShadowBlocker");
        DisableGameObjectByName("VP3_EastDoor_ShadowBlocker");
        DisableGameObjectByName("VP3_Prop_MedicalChart_Papers");
        DisableGameObjectByName("VP3_Prop_ScatteredScalpels");

        // 2. Adjust Contact Shadows at West wall
        GameObject aoWest = GameObject.Find("VP3_AO_Wall_West_Top");
        if (aoWest != null)
        {
            SpriteRenderer sr = aoWest.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color;
                c.a = 0.10f; // very soft blend
                sr.color = c;
            }
        }

        // 3. Mount Fluorescent Light cleanly on North wall plate
        GameObject flGo = GameObject.Find("VP5_Prop_Wall_Fluorescent");
        if (flGo != null)
        {
            flGo.transform.position = new Vector3(-14.0f, 31.85f, 0f);
            Light2D flLight = flGo.GetComponentInChildren<Light2D>();
            if (flLight != null)
            {
                flLight.color = new Color(0.78f, 0.88f, 0.95f, 1f);
                flLight.intensity = 0.55f;
                flLight.pointLightOuterRadius = 2.4f;
            }
        }

        // 4. Update Doorway floor tiles (replace dark fDiamond with fDark)
        Grid grid = Object.FindAnyObjectByType<Grid>();
        if (grid != null)
        {
            Tilemap floorMap = null;
            Tilemap[] tms = grid.GetComponentsInChildren<Tilemap>();
            foreach (var tm in tms)
            {
                if (tm.name.ToLower().Contains("floor"))
                {
                    floorMap = tm;
                    break;
                }
            }

            if (floorMap != null)
            {
                Tile fDark = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Sprites/Environments/MedBay/Tiles/MedBay_Floor_Dark.asset");
                if (fDark != null)
                {
                    // West doorway tiles
                    floorMap.SetTile(new Vector3Int(-20, 24, 0), fDark);
                    floorMap.SetTile(new Vector3Int(-20, 25, 0), fDark);
                    // East doorway tiles
                    floorMap.SetTile(new Vector3Int(-12, 27, 0), fDark);
                    floorMap.SetTile(new Vector3Int(-12, 28, 0), fDark);
                    Debug.Log("[MedBayVisualPassV4Applier] Replaced dark diamond threshold tiles with modular steel floor tiles.");
                }
            }
        }

        // 5. Door Appearance and Materials (SAFE: through Unity API, no YAML corruption)
        Material defaultLitMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_Sprite_Lit_PixelSmooth.mat")
            ?? AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");

        Sprite doorClosed = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/MedBay/Props/Door_MedBay_Vertical_Closed.png");
        Sprite doorOpen = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/MedBay/Props/Door_MedBay_Vertical_Open.png");
        Sprite doorQuarantine = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/MedBay/Props/Door_MedBay_Vertical_Quarantine.png");

        // West Door
        GameObject doorWest = GameObject.Find("Door_MedBay_West");
        if (doorWest != null)
        {
            SpriteRenderer sr = doorWest.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (defaultLitMat != null) sr.sharedMaterial = defaultLitMat;
                if (doorClosed != null) sr.sprite = doorClosed;
                sr.color = Color.white;
            }
            DoorController dc = doorWest.GetComponent<DoorController>();
            if (dc != null && doorClosed != null && doorOpen != null)
            {
                dc.SetSprites(doorClosed, doorOpen);
            }
        }

        // East Door
        GameObject doorEast = GameObject.Find("Door_MedBay");
        if (doorEast != null)
        {
            SpriteRenderer sr = doorEast.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (defaultLitMat != null) sr.sharedMaterial = defaultLitMat;
                Sprite chosenSprite = (doorQuarantine != null) ? doorQuarantine : doorClosed;
                if (chosenSprite != null) sr.sprite = chosenSprite;
                sr.color = Color.white;
            }
            DoorController dc = doorEast.GetComponent<DoorController>();
            if (dc != null && doorOpen != null)
            {
                Sprite chosenSprite = (doorQuarantine != null) ? doorQuarantine : doorClosed;
                dc.SetSprites(chosenSprite, doorOpen);
            }
        }

        // Adjust Indicator Lights: reduce outer radius and intensity so light does not spill across whole floor
        GameObject indEast = GameObject.Find("Indicator_Door_MedBay");
        if (indEast != null)
        {
            Light2D l2d = indEast.GetComponent<Light2D>();
            if (l2d != null)
            {
                l2d.intensity = 0.45f;
                l2d.pointLightOuterRadius = 1.3f;
                l2d.pointLightInnerRadius = 0.15f;
            }
        }

        GameObject indWest = GameObject.Find("Indicator_Door_MedBay_West");
        if (indWest != null)
        {
            Light2D l2d = indWest.GetComponent<Light2D>();
            if (l2d != null)
            {
                l2d.intensity = 0.45f;
                l2d.pointLightOuterRadius = 1.3f;
                l2d.pointLightInnerRadius = 0.15f;
            }
        }

        // 6. Blood Drag Decal
        GameObject oldDrag = GameObject.Find("VP5_Decal_BloodDrag_SE_Pixel");
        if (oldDrag != null) oldDrag.SetActive(false);

        GameObject longDrag = GameObject.Find("VP5_Decal_BloodDrag_Long");
        if (longDrag == null)
        {
            longDrag = new GameObject("VP5_Decal_BloodDrag_Long");
            GameObject root = GameObject.Find("VP5_VisualPass_Elements");
            if (root != null) longDrag.transform.SetParent(root.transform);
        }
        longDrag.SetActive(true);
        // Position: body bag is at (-14.5, 25.2), door is at (-10.5, 28.0)
        // Midpoint = (-12.5, 26.6)
        longDrag.transform.position = new Vector3(-12.5f, 26.6f, 0f);
        longDrag.transform.localScale = Vector3.one;
        SpriteRenderer dragSr = longDrag.GetComponent<SpriteRenderer>();
        if (dragSr == null) dragSr = longDrag.AddComponent<SpriteRenderer>();
        dragSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/MedBay/Props/VP5_Decal_BloodDrag_Long.png");
        dragSr.sortingLayerName = "Floor";
        dragSr.sortingOrder = 2;

        // Add Clipboard Prop replacing noise papers
        GameObject clip = GameObject.Find("VP5_Prop_Medical_Clipboard");
        if (clip == null)
        {
            clip = new GameObject("VP5_Prop_Medical_Clipboard");
            GameObject root = GameObject.Find("VP5_VisualPass_Elements");
            if (root != null) clip.transform.SetParent(root.transform);
        }
        clip.SetActive(true);
        clip.transform.position = new Vector3(-17.8f, 25.4f, 0f);
        clip.transform.localScale = Vector3.one;
        SpriteRenderer clipSr = clip.GetComponent<SpriteRenderer>();
        if (clipSr == null) clipSr = clip.AddComponent<SpriteRenderer>();
        clipSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/MedBay/Props/VP5_Prop_Medical_Clipboard.png");
        clipSr.sortingLayerName = "Floor";
        clipSr.sortingOrder = 3;

        // 7. Overturned Cart: orthographic orthogonal alignment (no rotation shearing)
        GameObject cart = GameObject.Find("VP5_Prop_Overturned_Surgical_Cart");
        if (cart != null)
        {
            cart.transform.rotation = Quaternion.identity;
            cart.transform.position = new Vector3(-13.0f, 24.6f, 0f);
        }

        // 8. Oxygen Rack fill light
        GameObject o2Light = GameObject.Find("VP5_Light_South_Fill");
        if (o2Light != null)
        {
            Light2D l2d = o2Light.GetComponent<Light2D>();
            if (l2d != null)
            {
                l2d.intensity = 0.70f;
                l2d.pointLightOuterRadius = 5.5f;
                l2d.pointLightInnerRadius = 0.5f;
                l2d.color = new Color(0.45f, 0.55f, 0.62f, 1f);
            }
        }

        // 9. Post-processing Vignette
        var volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var v in volumes)
        {
            if (v.profile != null && v.profile.TryGet<Vignette>(out var vig))
            {
                vig.intensity.value = 0.20f;
                vig.smoothness.value = 0.25f;
                Debug.Log($"[MedBayVisualPassV4Applier] Set vignette intensity to {vig.intensity.value}");
            }
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log("[MedBayVisualPassV4Applier] Pass v4 modifications applied and saved cleanly through Unity API!");
    }

    private static void DisableGameObjectByName(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null)
        {
            go.SetActive(false);
            Debug.Log($"[MedBayVisualPassV4Applier] Disabled {name}");
        }
    }
}
#endif
