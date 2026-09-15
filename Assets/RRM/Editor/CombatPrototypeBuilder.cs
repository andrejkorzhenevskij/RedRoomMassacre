using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RRM.Editor
{
    public static class CombatPrototypeBuilder
    {
        public const string Root = "Assets/RRM";
        public const string ScenePath = Root + "/Scenes/CombatPrototype.unity";
        public const string RangeScenePath = Root + "/Scenes/CameraTestRange.unity";

        [MenuItem("RRM/Set Starter Cover Height")]
        public static void SetStarterCoverHeight()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            LowerStarterCover(GameObject.Find("Test Room").transform.Find("Low Cover"));
            EditorSceneManager.SaveScene(scene);
        }

        private static void LowerStarterCover(Transform cover)
        {
            if (!cover) throw new InvalidOperationException("Low Cover is missing; the room was not rebuilt.");
            Vector3 size = cover.localScale;
            Vector3 position = cover.localPosition;
            position.y += (0.45f - size.y) * 0.5f;
            size.y = 0.45f;
            cover.localPosition = position;
            cover.localScale = size;
        }

        [MenuItem("RRM/Update Portable Lamps")]
        public static void UpdatePortableLamps()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            string playerPath = Root + "/Prefabs/Player.prefab";
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                ConfigureFists(player);
                PrefabUtility.SaveAsPrefabAsset(player, playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }

            GameObject floor = GameObject.Find("Floor");
            if (!floor.TryGetComponent(out LightLevelPreview preview)) preview = floor.AddComponent<LightLevelPreview>();
            preview.unlitTemplate = LightPreviewMaterial();
            string lampPath = Root + "/Prefabs/PortableLamp.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(lampPath);
            if (!prefab)
            {
                var lamp = new GameObject("Portable Lamp");
                try
                {
                    var item = lamp.AddComponent<PortableLamp>();
                    Material metal = Material("Metal", new Color(0.13f, 0.15f, 0.16f));
                    Visual("Base", lamp.transform, new Vector3(0, 0.06f, 0), new Vector3(0.36f, 0.12f, 0.36f), metal);
                    Visual("Stem", lamp.transform, new Vector3(0, 0.25f, 0), new Vector3(0.09f, 0.3f, 0.09f), metal);
                    Visual("Bulb", lamp.transform, new Vector3(0, 0.47f, 0), new Vector3(0.24f, 0.25f, 0.24f),
                        Material("Weapon", new Color(0.78f, 0.78f, 0.72f)));
                    Visual("Handle", lamp.transform, new Vector3(0, 0.66f, 0), new Vector3(0.3f, 0.07f, 0.08f), metal);
                    var emitter = new GameObject("Light");
                    emitter.transform.SetParent(lamp.transform, false);
                    emitter.transform.localPosition = new Vector3(0, 0.47f, 0.15f);
                    var light = emitter.AddComponent<Light>();
                    light.lightmapBakeType = LightmapBakeType.Realtime;
                    light.color = new Color(1f, 0.94f, 0.82f);
                    light.shadows = LightShadows.Soft;
                    item.lightSource = emitter.AddComponent<LightSource>();
                    item.lightSource.intensity = 85f;
                    item.lightSource.radius = 3.2f;
                    item.lightSource.pointLight = light;
                    item.lightSource.SyncPointLight();
                    prefab = PrefabUtility.SaveAsPrefabAsset(lamp, lampPath);
                }
                finally { Object.DestroyImmediate(lamp); }
            }
            Vector3[] positions = { new Vector3(-3.3f, 0.02f, -3.5f), new Vector3(-6.8f, 0.02f, 2f), new Vector3(3.2f, 0.02f, -2.1f) };
            EditorUtility.SetDirty(prefab.GetComponent<PortableLamp>());
            PrefabUtility.SavePrefabAsset(prefab);
            for (int i = 0; i < positions.Length; i++)
            {
                string name = "Portable Lamp " + (i + 1);
                if (GameObject.Find(name)) continue;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = name;
                instance.transform.position = positions[i];
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath);
            if (Object.FindObjectsByType<PortableLamp>().Length != 3)
                throw new InvalidOperationException("Expected three saved lamps; existing extra items were not removed.");
            Debug.Log("RRM LAMPS SAVED: three instances, fists, transparent light preview; existing room geometry preserved.");
        }

        private static void ConfigureFists(GameObject player)
        {
            var attack = player.GetComponent<MeleeAttack>();
            Transform baton = attack.weaponPivot.Find("Baton");
            if (baton) Object.DestroyImmediate(baton.gameObject);
            attack.weaponTip.localPosition = Vector3.forward * attack.fistReach;
            if (!attack.fistVisual)
            {
                Visual("Fist", attack.weaponTip, Vector3.zero, Vector3.one * 0.18f,
                    Material("Head", new Color(0.65f, 0.67f, 0.64f)));
                attack.fistVisual = attack.weaponTip.Find("Fist").gameObject;
            }
            attack.fistVisual.SetActive(false);
        }

        private static Material LightPreviewMaterial()
        {
            Material material = Material("LightLevelPreview", Color.white, true);
            material.SetFloat("_Surface", 1);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetShaderPassEnabled("DepthOnly", false);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        [MenuItem("RRM/Restore Two Room Prototype")]
        public static void RestoreTwoRoomPrototype()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Transform room = scene.GetRootGameObjects().First(root => root.name == "Test Room").transform;
            if (room.Find("Room Divider North") && room.Find("Room Divider South")) return;
            var layout = EditorSceneManager.OpenScene(RangeScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = layout.GetRootGameObjects();
                Transform sourceRoom = roots.First(root => root.name == "Test Room").transform;
                // Copy layout only: existing actors, evidence and recording references stay in this scene.
                foreach (Transform source in sourceRoom)
                {
                    Transform target = room.Find(source.name);
                    if (!target)
                    {
                        target = Object.Instantiate(source.gameObject, room).transform;
                        target.name = source.name;
                    }
                    target.localPosition = source.localPosition;
                    target.localRotation = source.localRotation;
                    target.localScale = source.localScale;
                }
                foreach (string name in new[] { "Player", "Dummy" })
                {
                    Transform target = scene.GetRootGameObjects().First(root => root.name == name).transform;
                    Transform source = roots.First(root => root.name == name).transform;
                    target.SetPositionAndRotation(source.position, source.rotation);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                }
                scene.GetRootGameObjects().First(root => root.name == "Main Camera").GetComponent<Camera>().orthographicSize =
                    roots.First(root => root.name == "Main Camera").GetComponent<Camera>().orthographicSize;
                LowerStarterCover(room.Find("Low Cover"));
                EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(layout, true); }
            Debug.Log("RRM CombatPrototype now combines the preserved two-room layout with evidence and light sources.");
        }

        [MenuItem("RRM/Update Evidence and Light Sources")]
        public static void AddEvidenceTestAreas() => UpdateEvidenceAndLights(false);

        [MenuItem("RRM/Retune Light Test Layout")]
        public static void RetuneLightTestLayout() => UpdateEvidenceAndLights(true);

        private static void UpdateEvidenceAndLights(bool retuneLights)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            GameObject dummy = GameObject.Find("Dummy");
            if (!dummy.TryGetComponent(out BloodEvidence evidence))
            {
                evidence = dummy.AddComponent<BloodEvidence>();
                evidence.bloodMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Blood.mat");
            }
            AddSource("Light Source West", "Low Light Area", new Vector3(-5.1f, 1f, -3.1f), 100f, 3f);
            AddSource("Light Source North", "Dark Area", new Vector3(-5.6f, 1f, 3.7f), 100f, 3f);
            AddSource("Light Source East", null, new Vector3(2.8f, 1.05f, 0.4f), 100f, 3.5f);
            AddSource("Light Source Doorway", null, new Vector3(-1.9f, 1f, -1.8f), 75f, 2.6f);
            AddSource("Light Source West Mid", null, new Vector3(-6.2f, 1f, 0.5f), 48f, 2.2f);
            AddSource("Light Source East South", null, new Vector3(3f, 1f, -4.5f), 100f, 2.4f);
            AddSource("Light Source East North", null, new Vector3(5.8f, 1f, 4.2f), 90f, 3.2f);
            AddSource("Light Source Platform", null, new Vector3(5.5f, 1.85f, -0.4f), 60f, 2.2f);
            if (!GameObject.Find("Floor").TryGetComponent(out LightLevelPreview preview))
            {
                preview = GameObject.Find("Floor").AddComponent<LightLevelPreview>();
            }
            preview.unlitTemplate = LightPreviewMaterial();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("RRM evidence and logical light sources ready; room layout and prefabs preserved.");

            void AddSource(string name, string oldZoneName, Vector3 position, float intensity, float radius)
            {
                GameObject sourceObject = GameObject.Find(name);
                if (sourceObject && !retuneLights) return;
                if (!sourceObject && oldZoneName != null) sourceObject = GameObject.Find(oldZoneName);
                if (!sourceObject)
                {
                    sourceObject = new GameObject(name);
                    sourceObject.transform.SetParent(GameObject.Find("Test Room").transform, false);
                }
                // The renamed script keeps its GUID; remove only the old zone's preview and trigger.
                if (sourceObject.TryGetComponent(out BoxCollider volume)) Object.DestroyImmediate(volume);
                Transform preview = sourceObject.transform.Find("Area Floor Preview");
                if (preview) Object.DestroyImmediate(preview.gameObject);
                sourceObject.name = name;
                sourceObject.transform.position = position;
                if (!sourceObject.TryGetComponent(out LightSource source)) source = sourceObject.AddComponent<LightSource>();
                source.intensity = intensity;
                source.radius = radius;
            }
        }

        [MenuItem("RRM/Open Camera Test Range")]
        public static void OpenCameraTestRange() => Open();

        public static void CreateCameraTestRange()
        {
            if (File.Exists(RangeScenePath))
            {
                Debug.Log("RRM camera range already exists; edits were preserved.");
                return;
            }
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Transform room = GameObject.Find("Test Room").transform;
            Material wall = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Wall.mat");
            Resize("Floor", new Vector3(0, -0.15f, 0), new Vector3(16, 0.3f, 12));
            Resize("North Wall", new Vector3(0, 1.3f, 6), new Vector3(16.5f, 2.6f, 0.35f));
            Resize("South Wall", new Vector3(0, 0.3f, -6), new Vector3(16.5f, 0.6f, 0.35f));
            Resize("West Wall", new Vector3(-8, 1.3f, 0), new Vector3(0.35f, 2.6f, 12));
            Resize("East Wall", new Vector3(8, 1.3f, 0), new Vector3(0.35f, 2.6f, 12));
            // A 2.4 m opening at z = -2 connects the rooms without a door controller.
            Box("Room Divider North", room, new Vector3(0, 1.2f, 2.6f), new Vector3(0.35f, 2.4f, 6.8f), wall);
            Box("Room Divider South", room, new Vector3(0, 1.2f, -4.6f), new Vector3(0.35f, 2.4f, 2.8f), wall);
            Box("Low Cover", room, new Vector3(-5.5f, 0.225f, -0.6f), new Vector3(2, 0.45f, 1.2f), wall);
            Box("High Cover", room, new Vector3(2.2f, 1.1f, 4.4f), new Vector3(2, 2.2f, 1), wall);
            Box("Blind Corner", room, new Vector3(-4.1f, 1.2f, 3.35f), new Vector3(2.55f, 2.4f, 0.35f), wall);
            Transform player = GameObject.Find("Player").transform;
            Transform dummy = GameObject.Find("Dummy").transform;
            player.position = new Vector3(-4, 0.02f, -3);
            dummy.position = new Vector3(2.1f, 0.02f, 1.1f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(player);
            PrefabUtility.RecordPrefabInstancePropertyModifications(dummy);
            Camera.main.orthographicSize = 7.1f;
            EditorSceneManager.SaveScene(scene, RangeScenePath);
            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Concat(new[] { new EditorBuildSettingsScene(RangeScenePath, true) }).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("RRM two-room camera range created; CombatPrototype and prefabs preserved.");

            void Resize(string name, Vector3 position, Vector3 size)
            {
                Transform item = room.Find(name);
                item.localPosition = position;
                item.localScale = size;
            }
        }

        [MenuItem("RRM/Open Combat Prototype")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else Create();
        }

        public static void Create()
        {
            if (File.Exists(ScenePath))
            {
                Debug.Log("RRM prototype already exists; existing assets were preserved.");
                return;
            }
            foreach (string folder in new[] { "Scenes", "Prefabs", "Materials", "Audio" })
                Directory.CreateDirectory(Root + "/" + folder);
            int actors = EnsureLayer("RRMActors");
            int hurtboxes = EnsureLayer("RRMHurtboxes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.57f);
            RenderSettings.skybox = null;
            Material floor = Material("Floor", new Color(0.23f, 0.26f, 0.26f));
            Material wall = Material("Wall", new Color(0.43f, 0.46f, 0.44f));
            Material playerMat = Material("Player", new Color(0.18f, 0.48f, 0.4f));
            Material dummyMat = Material("Dummy", new Color(0.65f, 0.29f, 0.26f));
            Material skin = Material("Head", new Color(0.65f, 0.67f, 0.64f));
            Material metal = Material("Metal", new Color(0.13f, 0.15f, 0.16f));
            Material weapon = Material("Weapon", new Color(0.78f, 0.78f, 0.72f));
            Material tape = Material("TapeSignal", new Color(0.25f, 0.65f, 0.47f), true);
            Material blood = Material("Blood", new Color(0.55f, 0.025f, 0.035f), true);
            AudioClip sound = MakeImpactSound();

            Transform room = new GameObject("Test Room").transform;
            Box("Floor", room, new Vector3(0, -0.15f, 0), new Vector3(18, 0.3f, 14), floor);
            Box("North Wall", room, new Vector3(0, 1.3f, 7), new Vector3(18.5f, 2.6f, 0.35f), wall);
            Box("South Wall", room, new Vector3(0, 0.3f, -7), new Vector3(18.5f, 0.6f, 0.35f), wall);
            Box("West Wall", room, new Vector3(-9, 1.3f, 0), new Vector3(0.35f, 2.6f, 14), wall);
            Box("East Wall", room, new Vector3(9, 1.3f, 0), new Vector3(0.35f, 2.6f, 14), wall);
            Box("Occlusion Wall", room, new Vector3(-3, 1.2f, 1.6f), new Vector3(0.35f, 2.4f, 3.5f), wall);
            Box("Concrete Block", room, new Vector3(4.4f, 0.65f, 2.5f), new Vector3(2, 1.3f, 1.5f), wall);
            AddHeightGeometry(room, wall);

            var light = new GameObject("Room Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50, -35, 0);
            RenderSettings.sun = light;
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 18, -13);
            camera.transform.LookAt(new Vector3(0, 0, 0.2f));
            camera.orthographic = true;
            camera.orthographicSize = 8.2f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.075f, 0.075f);
            camera.gameObject.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            GameObject dummy = Actor("Dummy", dummyMat, skin, actors, hurtboxes, blood, sound);
            dummy.transform.position = new Vector3(0.4f, 0.02f, 1.2f);
            ConfigureDummyControls(dummy);
            PrefabUtility.SaveAsPrefabAssetAndConnect(dummy, Root + "/Prefabs/Dummy.prefab", InteractionMode.AutomatedAction);

            GameObject player = Actor("Player", playerMat, skin, actors, hurtboxes, blood, sound);
            var attack = player.AddComponent<MeleeAttack>();
            attack.hurtboxMask = 1 << hurtboxes;
            Transform pivot = new GameObject("Weapon Pivot").transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0.45f, 1.15f, 0.08f);
            pivot.localRotation = Quaternion.Euler(0, 15, 0);
            Transform tip = new GameObject("Weapon Tip").transform;
            tip.SetParent(pivot, false);
            tip.localPosition = Vector3.forward * attack.fistReach;
            attack.weaponPivot = pivot;
            attack.weaponTip = tip;

            Transform camcorder = new GameObject("Handheld VHS Camera").transform;
            camcorder.SetParent(player.transform, false);
            camcorder.localPosition = new Vector3(-0.58f, 1.25f, 0.28f);
            camcorder.localRotation = Quaternion.Euler(8, -10, 0);
            Visual("Housing", camcorder, Vector3.zero, new Vector3(0.34f, 0.27f, 0.46f), metal);
            Visual("Viewfinder", camcorder, new Vector3(-0.18f, 0.09f, -0.08f), new Vector3(0.12f, 0.1f, 0.14f), metal);
            Visual("Lens Glass", camcorder, new Vector3(0, 0, 0.255f), new Vector3(0.21f, 0.18f, 0.055f), tape);
            Transform lens = new GameObject("Lens Origin").transform;
            lens.SetParent(camcorder, false);
            lens.localPosition = new Vector3(0, 0, 0.3f);
            var recorder = camcorder.gameObject.AddComponent<CameraRecorder>();
            recorder.lens = lens;
            recorder.playerAttack = attack;
            var cone = new GameObject("Recording Cone").AddComponent<LineRenderer>();
            cone.transform.SetParent(camcorder, false);
            cone.sharedMaterial = tape;
            cone.useWorldSpace = true;
            cone.startWidth = cone.endWidth = 0.022f;
            cone.shadowCastingMode = ShadowCastingMode.Off;
            cone.receiveShadows = false;
            recorder.cone = cone;
            ConfigureLiveCamera(recorder);
            var controller = player.AddComponent<PlayerController>();
            controller.inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (!controller.inputActions) throw new InvalidOperationException("Missing template InputSystem_Actions asset.");
            player.transform.position = new Vector3(-1.1f, 0.02f, -1.9f);
            ConfigureFists(player);
            PrefabUtility.SaveAsPrefabAssetAndConnect(player, Root + "/Prefabs/Player.prefab", InteractionMode.AutomatedAction);
            recorder.subjects = new[] { dummy.GetComponent<Damageable>() };
            controller.viewCamera = camera;
            PrefabUtility.RecordPrefabInstancePropertyModifications(recorder);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
            AssetDatabase.SaveAssets();
            UpdatePortableLamps();
            Debug.Log("RRM prototype created: " + ScenePath);
        }

        [MenuItem("RRM/Add Height Test Platform")]
        public static void AddHeightPlatform()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Transform room = GameObject.Find("Test Room").transform;
            AddHeightGeometry(room, AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Wall.mat"));
            EditorSceneManager.SaveScene(scene);
            Debug.Log("RRM height platform saved; existing room and prefabs preserved.");
        }

        private static void AddHeightGeometry(Transform room, Material material)
        {
            if (!room.Find("Camera Platform"))
                Box("Camera Platform", room, new Vector3(5.5f, 0.45f, -0.5f),
                    new Vector3(3, 0.9f, 2.5f), material);
            if (room.Find("Platform Ramp")) return;
            // The ramp's top starts below the floor and meets the platform without a vertical lip.
            Vector3 start = new Vector3(5.5f, -0.1f, -4.95f);
            Vector3 end = new Vector3(5.5f, 0.9f, -1.75f);
            Quaternion rotation = Quaternion.LookRotation(end - start, Vector3.up);
            GameObject ramp = Box("Platform Ramp", room,
                (start + end) * 0.5f - rotation * Vector3.up * 0.1f,
                new Vector3(2, 0.2f, Vector3.Distance(start, end)), material);
            ramp.transform.localRotation = rotation;
        }

        [MenuItem("RRM/Add Camera Device")]
        public static void AddCameraDevice()
        {
            string path = Root + "/Prefabs/Player.prefab";
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var recorder = contents.GetComponentInChildren<CameraRecorder>();
                if (!recorder.GetComponent<CameraDevice>())
                    recorder.gameObject.AddComponent<CameraDevice>();
                // RequireComponent may already have added it during load; persist it either way.
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            Debug.Log("RRM CameraDevice attached to the existing camera; scene preserved.");
        }

        public static void UpdateInteractivity()
        {
            foreach (string name in new[] { "Player", "Dummy" })
            {
                string path = Root + "/Prefabs/" + name + ".prefab";
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (name == "Player") ConfigureLiveCamera(contents.GetComponentInChildren<CameraRecorder>());
                    else ConfigureDummyControls(contents);
                    ConfigureBloodBurst(contents.GetComponent<HitFeedback>().blood);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            Debug.Log("RRM interactivity updated in existing prefabs; scene layout preserved.");
        }

        private static void ConfigureLiveCamera(CameraRecorder recorder)
        {
            if (!recorder.liveCamera) recorder.liveCamera = recorder.lens.GetComponent<Camera>();
            if (!recorder.liveCamera) recorder.liveCamera = recorder.lens.gameObject.AddComponent<Camera>();
            Camera camera = recorder.liveCamera;
            camera.enabled = false;
            camera.orthographic = false;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 10000f;
            camera.aspect = 4f / 3f;
            camera.fieldOfView = Camera.HorizontalToVerticalFieldOfView(recorder.fieldOfView, camera.aspect);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.075f, 0.075f);
            camera.cullingMask = ~(1 << 2);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            recorder.cone.gameObject.layer = 2; // Ignore Raycast: preview is not part of the filmed scene.
            recorder.conePreviewLength = 50f;
        }

        [MenuItem("RRM/Enable Enemy Prototype")]
        public static void EnableEnemyPrototype()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before configuring the enemy.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string path = Root + "/Prefabs/Dummy.prefab";
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureDummyControls(contents);
                var attack = contents.GetComponent<MeleeAttack>();
                if (!attack) attack = contents.AddComponent<MeleeAttack>();
                Transform visual = contents.GetComponent<HitFeedback>().visual;
                Transform arm = System.Array.Find(contents.GetComponentsInChildren<Hurtbox>(),
                    zone => zone.part == BodyPart.RightArm).transform;
                Transform pivot = visual.Find("Melee Pivot");
                if (!pivot) { pivot = new GameObject("Melee Pivot").transform; pivot.SetParent(visual, false); }
                pivot.localPosition = new Vector3(0.43f, 1.08f, 0);
                pivot.localRotation = Quaternion.Euler(0, 15, 0);
                arm.SetParent(pivot, false);
                arm.localPosition = new Vector3(0, 0, 0.3f);
                arm.localRotation = Quaternion.Euler(90, 0, 0);
                Transform tip = pivot.Find("Contact Point");
                if (!tip) { tip = new GameObject("Contact Point").transform; tip.SetParent(pivot, false); }
                tip.localPosition = new Vector3(0, 0, 0.65f);
                attack.weaponPivot = pivot;
                attack.weaponTip = tip;
                attack.hurtboxMask = 1 << LayerMask.NameToLayer("RRMHurtboxes");
                attack.windup = 0.7f;
                attack.strike = 0.18f;
                attack.recovery = 1.15f;
                attack.damage = 60f;
                contents.GetComponent<HitFeedback>().showWindup = true;
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var player = GameObject.Find("Player").GetComponent<Damageable>();
            var dummy = GameObject.Find("Dummy").GetComponent<PlayerController>();
            dummy.opponent = player;
            PrefabUtility.RecordPrefabInstancePropertyModifications(dummy);
            var recorder = GameObject.Find("Player").GetComponentInChildren<CameraRecorder>();
            recorder.subjects = new[] { dummy.GetComponent<Damageable>(), player };
            PrefabUtility.RecordPrefabInstancePropertyModifications(recorder);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("RRM enemy configured on existing Dummy and right arm; room geometry and camera device preserved.");
        }

        private static void ConfigureDummyControls(GameObject dummy)
        {
            var controller = dummy.GetComponent<PlayerController>();
            if (!controller) controller = dummy.AddComponent<PlayerController>();
            controller.autonomousMovement = true;
            controller.moveSpeed = 1.2f;
            Transform visual = dummy.transform.Find("Body Visual");
            if (!visual.Find("Facing Marker"))
                Visual("Facing Marker", visual, new Vector3(0, 1.62f, 0.25f),
                    new Vector3(0.14f, 0.12f, 0.16f),
                    AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Head.mat"));
        }

        private static GameObject Actor(string name, Material material, Material skin, int actorLayer,
            int hurtboxLayer, Material bloodMaterial, AudioClip sound)
        {
            var actor = new GameObject(name);
            actor.layer = actorLayer;
            var body = actor.AddComponent<Rigidbody>();
            body.mass = 80f;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = name == "Dummy" ? 5f : 0f;
            var collider = actor.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0, 0.85f, 0);
            collider.height = 1.7f;
            collider.radius = 0.32f;
            var health = actor.AddComponent<Damageable>();
            Transform visual = new GameObject("Body Visual").transform;
            visual.SetParent(actor.transform, false);
            BodyZone("Torso", visual, new Vector3(0, 1.05f, 0), new Vector3(0.6f, 0.65f, 0.4f),
                material, health, BodyPart.Torso, 1f, hurtboxLayer);
            BodyZone("Head", visual, new Vector3(0, 1.62f, 0), new Vector3(0.38f, 0.4f, 0.38f),
                skin, health, BodyPart.Head, 2.2f, hurtboxLayer);
            BodyZone("Left Arm", visual, new Vector3(-0.43f, 1.08f, 0), new Vector3(0.22f, 0.6f, 0.24f),
                material, health, BodyPart.LeftArm, 0.7f, hurtboxLayer);
            BodyZone("Right Arm", visual, new Vector3(0.43f, 1.08f, 0), new Vector3(0.22f, 0.6f, 0.24f),
                material, health, BodyPart.RightArm, 0.7f, hurtboxLayer);
            Visual("Left Leg", visual, new Vector3(-0.18f, 0.36f, 0), new Vector3(0.23f, 0.7f, 0.3f), material);
            Visual("Right Leg", visual, new Vector3(0.18f, 0.36f, 0), new Vector3(0.23f, 0.7f, 0.3f), material);
            ConfigureLimbHurtboxes(actor);
            var source = actor.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0.3f;
            source.maxDistance = 30;
            var particles = new GameObject("Blood Feedback").AddComponent<ParticleSystem>();
            particles.transform.SetParent(actor.transform, false);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ConfigureBloodBurst(particles);
            main.gravityModifier = 1.3f;
            main.maxParticles = 100;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.04f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = bloodMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var feedback = actor.AddComponent<HitFeedback>();
            feedback.visual = visual;
            feedback.bodyRenderers = visual.GetComponentsInChildren<Renderer>();
            feedback.blood = particles;
            feedback.impactSound = sound;
            actor.AddComponent<BloodEvidence>().bloodMaterial = bloodMaterial;
            return actor;
        }

        [MenuItem("RRM/Add Actor Blood Evidence")]
        public static void AddActorBloodEvidence()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var actor in Object.FindObjectsByType<Damageable>())
            {
                if (!actor.TryGetComponent(out BloodEvidence evidence))
                    evidence = actor.gameObject.AddComponent<BloodEvidence>();
                if (!evidence.bloodMaterial)
                    evidence.bloodMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Blood.mat");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("RRM ACTOR EVIDENCE SAVED: existing actors share BloodEvidence; no geometry rebuilt.");
        }

        [MenuItem("RRM/Add Limb Hurtboxes")]
        public static void AddLimbHurtboxes()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            foreach (string name in new[] { "Player", "Dummy" })
            {
                string path = Root + "/Prefabs/" + name + ".prefab";
                GameObject actor = PrefabUtility.LoadPrefabContents(path);
                try { ConfigureLimbHurtboxes(actor); PrefabUtility.SaveAsPrefabAsset(actor, path); }
                finally { PrefabUtility.UnloadPrefabContents(actor); }
            }
            Debug.Log("RRM LIMB PREFABS SAVED: existing legs receive Hurtboxes; no scene rebuild.");
        }

        private static void ConfigureLimbHurtboxes(GameObject actor)
        {
            foreach (BodyPart part in new[] { BodyPart.LeftLeg, BodyPart.RightLeg })
            {
                var leg = actor.transform.Find("Body Visual/" + (part == BodyPart.LeftLeg ? "Left Leg" : "Right Leg"));
                var shape = leg.GetComponent<BoxCollider>();
                if (!shape) shape = leg.gameObject.AddComponent<BoxCollider>();
                shape.isTrigger = true;
                leg.gameObject.layer = LayerMask.NameToLayer("RRMHurtboxes");
                var zone = leg.GetComponent<Hurtbox>();
                if (!zone) zone = leg.gameObject.AddComponent<Hurtbox>();
                zone.owner = actor.GetComponent<Damageable>();
                zone.part = part;
                zone.damageMultiplier = 0.7f;
            }
        }

        private static void ConfigureBloodBurst(ParticleSystem particles)
        {
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        }

        private static void BodyZone(string name, Transform parent, Vector3 position, Vector3 size,
            Material material, Damageable owner, BodyPart part, float multiplier, int layer)
        {
            GameObject body = Box(name, parent, position, size, material);
            body.layer = layer;
            body.GetComponent<Collider>().isTrigger = true;
            var zone = body.AddComponent<Hurtbox>();
            zone.owner = owner;
            zone.part = part;
            zone.damageMultiplier = multiplier;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        private static void Visual(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = Box(name, parent, position, scale, material);
            Object.DestroyImmediate(box.GetComponent<Collider>());
        }

        private static Material Material(string name, Color color, bool unlit = false)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP shader missing: " + name);
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = settings.FindProperty("layers");
            for (int i = 8; i < 32; i++)
            {
                if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) continue;
                layers.GetArrayElementAtIndex(i).stringValue = name;
                settings.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
            throw new InvalidOperationException("No free layer for " + name);
        }

        private static AudioClip MakeImpactSound()
        {
            string path = Root + "/Audio/Impact.wav";
            if (!File.Exists(path))
            {
                const int sampleRate = 44100;
                const int samples = 8820;
                var random = new System.Random(17);
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                    writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate);
                    writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
                    for (int i = 0; i < samples; i++)
                    {
                        double t = i / (double)sampleRate;
                        double noise = (random.NextDouble() * 2 - 1) * Math.Exp(-t * 85);
                        double thump = Math.Sin(2 * Math.PI * (100 * t - 150 * t * t)) * Math.Exp(-t * 28);
                        writer.Write((short)(Math.Clamp(noise * 0.4 + thump * 0.55, -1, 1) * short.MaxValue));
                    }
                }
            }
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
