using System;
using System.IO;
using System.Linq;
using System.Text;
using AyuoDev.CharCrafter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RRM.Editor
{
    public static class CharCrafterIntegration
    {
        public const string Package = "Assets/CharCrafter - Moduler Low-Poly Character Creation";
        public const string Folder = "Assets/RRM/Characters";
        public const string VisualPath = Folder + "/CharCrafterDummy.prefab";

        public static void IntegrateAndExit()
        {
            try { Integrate(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        [MenuItem("RRM/CharCrafter/Integrate One Dummy")]
        public static void Integrate() => IntegratePrefab("Assets/RRM/Prefabs/Dummy.prefab");

        [MenuItem("RRM/CharCrafter/Integrate Player")]
        public static void IntegratePlayer() => IntegratePrefab("Assets/RRM/Prefabs/Player.prefab");

        public static void IntegratePlayerAndExit()
        {
            try { IntegratePlayer(); IntegratePlayer(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static void IntegratePrefab(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before modifying the character prefab.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/RRM", "Characters");
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath)) CreateVisual();
            var dummy = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (dummy.GetComponent<CharCrafterVisual>()) { Validate(dummy); return; }
                var feedback = dummy.GetComponent<HitFeedback>();
                var zones = dummy.GetComponentsInChildren<Hurtbox>(true);
                if (zones.Length != 6) throw new InvalidOperationException("Expected six character hurtboxes in " + path
                    + ", found " + zones.Length);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath), feedback.visual);
                var animator = model.GetComponent<Animator>();
                var adapter = dummy.AddComponent<CharCrafterVisual>();
                adapter.animator = animator;
                adapter.leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                adapter.rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                foreach (var zone in zones) AttachZone(zone, animator);
                var marker = feedback.visual.Find("Facing Marker");
                if (marker && marker.TryGetComponent(out Renderer markerRenderer)) markerRenderer.enabled = false;
                feedback.bodyRenderers = model.GetComponentsInChildren<Renderer>();
                Validate(dummy);
                PrefabUtility.SaveAsPrefabAsset(dummy, path);
                Debug.Log("RRM CHARCRAFTER INTEGRATED: " + path + "; room scene preserved. Dismemberment is proxy-only.");
            }
            finally { PrefabUtility.UnloadPrefabContents(dummy); }
        }

        private static void CreateVisual()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                // Male.prefab is the package's ready-made plain Shirt/Pants_1 character.
                var model = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(Package + "/Prefabs/Male.prefab"), scene);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = "RRM CharCrafter Dummy";
                string source = "Assets/Prefabs/Characters/" + model.name + ".prefab";
                if (File.Exists(source))
                    throw new InvalidOperationException("Native save destination already exists; preserving it: " + source);
                var animator = model.GetComponent<Animator>();
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.enabled = false;
                var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
                var bounds = new Bounds();
                bool firstVertex = true;
                foreach (var renderer in renderers)
                {
                    var baked = new Mesh();
                    renderer.BakeMesh(baked);
                    foreach (Vector3 vertex in baked.vertices)
                    {
                        Vector3 world = renderer.transform.TransformPoint(vertex);
                        if (firstVertex) { bounds = new Bounds(world, Vector3.zero); firstVertex = false; }
                        else bounds.Encapsulate(world);
                    }
                    Object.DestroyImmediate(baked);
                }
                if (bounds.size.y < 0.1f) throw new InvalidOperationException("Invalid imported model bounds.");
                float scale = 1.8f / bounds.size.y;
                model.transform.localScale = Vector3.one * scale;
                model.transform.position = Vector3.up * (-bounds.min.y * scale);
                foreach (var renderer in renderers)
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    renderer.updateWhenOffscreen = true;
                    string materialName = renderer.name == "Shirt" ? "DarkBrown" : renderer.name == "Pants_1" ? "Black" : null;
                    if (materialName != null)
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(_ =>
                            AssetDatabase.LoadAssetAtPath<Material>(Package + "/Materials/" + materialName + ".mat")).ToArray();
                }
                var avatar = CreateAvatar(model);
                AssetDatabase.CreateAsset(avatar, Folder + "/CharCrafterDummyAvatar.asset");
                animator.avatar = avatar;
                // Use the vendor's actual clean/save operation, then move its output into RRM.
                var container = model.AddComponent<MeshContainer>();
                container.GenderBaseMesh = model;
                var tools = new GameObject("Temporary CharCrafter save tool");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(tools, scene);
                var creator = tools.AddComponent<CharacterCustomization>();
                creator.enabled = false;
                creator.GenderInformation = new[] { container };
                creator.SaveCleanedCharacter();
                string error = AssetDatabase.MoveAsset(source, VisualPath);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void AttachZone(Hurtbox zone, Animator animator)
        {
            HumanBodyBones start, end;
            float width, depth;
            switch (zone.part)
            {
                case BodyPart.Head: start = end = HumanBodyBones.Head; width = depth = 0.28f; break;
                case BodyPart.Torso: start = HumanBodyBones.Hips; end = HumanBodyBones.Neck; width = 0.44f; depth = 0.28f; break;
                case BodyPart.LeftArm: start = HumanBodyBones.LeftUpperArm; end = HumanBodyBones.LeftHand; width = depth = 0.19f; break;
                case BodyPart.RightArm: start = HumanBodyBones.RightUpperArm; end = HumanBodyBones.RightHand; width = depth = 0.19f; break;
                case BodyPart.LeftLeg: start = HumanBodyBones.LeftUpperLeg; end = HumanBodyBones.LeftFoot; width = depth = 0.22f; break;
                default: start = HumanBodyBones.RightUpperLeg; end = HumanBodyBones.RightFoot; width = depth = 0.22f; break;
            }
            Transform bone = animator.GetBoneTransform(start), tip = animator.GetBoneTransform(end);
            if (!bone || !tip) throw new InvalidOperationException("Humanoid bone missing for " + zone.part);
            Vector3 a = bone.position, b = tip.position;
            if (zone.part == BodyPart.Head) b += animator.transform.up * 0.24f;
            zone.transform.SetParent(bone, true);
            zone.transform.SetPositionAndRotation((a + b) * 0.5f,
                Quaternion.FromToRotation(Vector3.up, (b - a).normalized));
            Vector3 size = new Vector3(width, Vector3.Distance(a, b) + 0.09f, depth);
            Vector3 parentScale = bone.lossyScale;
            zone.transform.localScale = new Vector3(size.x / parentScale.x, size.y / parentScale.y, size.z / parentScale.z);
            zone.GetComponent<BoxCollider>().size = Vector3.one;
            zone.GetComponent<BoxCollider>().center = Vector3.zero;
            zone.GetComponent<Renderer>().enabled = false;
            // Retain the existing primitive mesh solely as a detached proxy. Never scale/cut the live skin's bones.
        }

        public static void Validate(GameObject dummy)
        {
            var adapter = dummy.GetComponent<CharCrafterVisual>();
            if (!adapter || !adapter.animator || !adapter.animator.avatar || !adapter.animator.avatar.isHuman
                || !adapter.animator.avatar.isValid || adapter.animator.applyRootMotion)
                throw new InvalidOperationException("Invalid CharCrafter visual/Avatar/root motion configuration.");
            var zones = dummy.GetComponentsInChildren<Hurtbox>(true);
            if (zones.Length != 6 || zones.Select(z => z.part).Distinct().Count() != 6
                || zones.Any(z => z.owner != dummy.GetComponent<Damageable>() || z.GetComponent<Renderer>().enabled
                    || !z.transform.IsChildOf(adapter.animator.transform)))
                throw new InvalidOperationException("Character hurtbox ownership, bone attachment or hidden proxies are invalid.");
            if (dummy.GetComponentsInChildren<Rigidbody>().Length != 1
                || dummy.GetComponentsInChildren<Damageable>().Length != 1
                || dummy.GetComponentsInChildren<CharCrafterVisual>().Length != 1
                || adapter.animator.GetComponentInChildren<MeshContainer>())
                throw new InvalidOperationException("Visual has duplicate gameplay/creator components.");
        }

        public static void InspectAndExit()
        {
            try { Inspect(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        [MenuItem("RRM/CharCrafter/Inspect Imported Character")]
        public static void Inspect()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var model = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(Package + "/Prefabs/Male.prefab"), scene);
            try
            {
                var text = new StringBuilder();
                foreach (var bone in model.GetComponentsInChildren<Transform>())
                    text.AppendLine($"TRANSFORM {AnimationUtility.CalculateTransformPath(bone, model.transform)} position={bone.position:F4} scale={bone.lossyScale:F3}");
                foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh = new Mesh(); renderer.BakeMesh(mesh);
                    text.AppendLine($"MESH {renderer.name}: vertices={mesh.vertexCount}, submeshes={mesh.subMeshCount}, bounds={mesh.bounds}, bones={string.Join(",", renderer.bones.Select(b => b.name))}");
                    foreach (var material in renderer.sharedMaterials)
                        text.AppendLine($"MATERIAL {material.name} shader={material.shader.name} supported={material.shader.isSupported}");
                    Object.DestroyImmediate(mesh);
                }
                var avatar = CreateAvatar(model);
                text.AppendLine($"AVATAR valid={avatar.isValid} human={avatar.isHuman}");
                Object.DestroyImmediate(avatar);
                Directory.CreateDirectory("Verification");
                File.WriteAllText("Verification/charcrafter-inspection.txt", text.ToString());
                Debug.Log(text.ToString());
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static Avatar CreateAvatar(GameObject model)
        {
            // The imported Generic rig has no Avatar; map its inspected bones through Unity's Humanoid API.
            var names = new[] {
                (HumanBodyBones.Hips, "Hip"), (HumanBodyBones.Spine, "Spine"),
                (HumanBodyBones.Chest, "Chest"), (HumanBodyBones.Neck, "Neck"), (HumanBodyBones.Head, "Head"),
                (HumanBodyBones.LeftUpperArm, "Arm_Upper.L"), (HumanBodyBones.LeftLowerArm, "Arm_Lower.L"),
                (HumanBodyBones.LeftHand, "Hand.L"), (HumanBodyBones.RightUpperArm, "Arm_Upper.R"),
                (HumanBodyBones.RightLowerArm, "Arm_Lower.R"), (HumanBodyBones.RightHand, "Hand.R"),
                (HumanBodyBones.LeftUpperLeg, "Leg_Upper.L"), (HumanBodyBones.LeftLowerLeg, "Leg_Lower.L"),
                (HumanBodyBones.LeftFoot, "Foot.L"), (HumanBodyBones.LeftToes, "Toes.L"),
                (HumanBodyBones.RightUpperLeg, "Leg_Upper.R"), (HumanBodyBones.RightLowerLeg, "Leg_Lower.R"),
                (HumanBodyBones.RightFoot, "Foot.R"), (HumanBodyBones.RightToes, "Toes.R")
            };
            var bones = model.GetComponentsInChildren<Transform>();
            foreach (var pair in names)
                if (bones.Count(b => b.name == pair.Item2) != 1)
                    throw new InvalidOperationException("Ambiguous/missing imported bone: " + pair.Item2);
            var description = new HumanDescription {
                human = names.Select(pair => new HumanBone { humanName = HumanTrait.BoneName[(int)pair.Item1],
                    boneName = pair.Item2, limit = new HumanLimit { useDefaultValues = true } }).ToArray(),
                skeleton = bones.Select(b => new SkeletonBone { name = b.name, position = b.localPosition,
                    rotation = b.localRotation, scale = b.localScale }).ToArray(),
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false
            };
            var avatar = AvatarBuilder.BuildHumanAvatar(model, description);
            if (!avatar || !avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException("CharCrafter Humanoid mapping failed; Dummy was not changed.");
            avatar.name = "CharCrafterDummyAvatar";
            return avatar;
        }
    }
}
