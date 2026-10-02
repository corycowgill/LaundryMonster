using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LaundryMonster.EditorTools
{
    /// <summary>
    /// Imports the retargeted mocap clips and builds the hero's Animator Controller.
    ///
    /// The clips come from NVIDIA Kimodo (text -> BVH) retargeted onto our rig in Blender,
    /// one FBX per clip using Unity's "model@clip" naming. Each one ships the mesh too,
    /// which we ignore - only the AnimationClip inside is used, bound to the Avatar that
    /// person.fbx generates.
    ///
    /// Kimodo always returns a fixed-length take (250 frames at 30fps), so most of every
    /// clip is the character standing around before and after the motion. The trims below
    /// are the frames where the action actually happens, read off rendered contact sheets.
    /// </summary>
    public static class AnimationSetup
    {
        const string ModelDir = "Assets/Models/person";
        const string BaseModel = ModelDir + "/person.fbx";
        const string ControllerPath = "Assets/Animation/Hero.controller";

        struct ClipDef
        {
            public string Name;
            public int First, Last;
            public bool Loop;

            public ClipDef(string name, int first, int last, bool loop)
            {
                Name = name; First = first; Last = last; Loop = loop;
            }
        }

        // Trim ranges verified by rendering the frames, not by trusting the prompt.
        static readonly ClipDef[] Clips =
        {
            new ClipDef("idle",      2,  250, true),
            new ClipDef("walk",      2,  250, true),
            new ClipDef("carry",     2,  250, true),
            new ClipDef("grab",     44,   96, false),
            new ClipDef("celebrate", 8,  130, false),
        };

        [MenuItem("Laundry Monster/Rebuild Hero Animation")]
        public static void Rebuild()
        {
            var avatar = LoadAvatar();
            if (avatar == null)
            {
                Debug.LogError($"[AnimationSetup] no Avatar on {BaseModel}; is it set to Generic?");
                return;
            }

            var clips = new Dictionary<string, AnimationClip>();
            foreach (var def in Clips)
            {
                var clip = ImportClip(def, avatar);
                if (clip != null) clips[def.Name] = clip;
                else Debug.LogWarning($"[AnimationSetup] clip '{def.Name}' missing - skipped");
            }

            if (!clips.ContainsKey("idle"))
            {
                Debug.LogError("[AnimationSetup] no idle clip; controller not built");
                return;
            }

            BuildController(clips);
            AssetDatabase.SaveAssets();
            int fixedUp = RepointSceneAnimators();
            Debug.Log($"[AnimationSetup] {clips.Count} clips -> {ControllerPath} "
                    + $"({fixedUp} scene animator(s) repointed)");
        }

        static Avatar LoadAvatar()
        {
            var imp = AssetImporter.GetAtPath(BaseModel) as ModelImporter;
            if (imp == null) return null;

            // Generic alone does not produce an Avatar - the rig also has to be told to
            // build one from this model, which is what every clip then copies.
            if (imp.animationType != ModelImporterAnimationType.Generic ||
                imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                imp.animationType = ModelImporterAnimationType.Generic;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAllAssetsAtPath(BaseModel).OfType<Avatar>().FirstOrDefault();
        }

        /// <summary>
        /// Configure one clip FBX's importer and return the AnimationClip it produces.
        /// Reusing the base model's Avatar is what makes the clip bind to the character
        /// in the scene rather than to the throwaway mesh inside the clip file.
        /// </summary>
        static AnimationClip ImportClip(ClipDef def, Avatar avatar)
        {
            var path = $"{ModelDir}/person@{def.Name}.fbx";
            if (!File.Exists(path)) return null;

            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) return null;

            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            imp.sourceAvatar = avatar;
            imp.importAnimation = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            imp.resampleCurves = true;

            // Blender writes the armature at FBX centimetre scale, so every clip carries a
            // constant scale curve of 100. Unity corrects the model's own scale on import
            // but plays that curve back verbatim, which blows the hero up 100x the instant
            // any state starts. The curves are constant and carry no animation, so drop them.
            imp.removeConstantScaleCurves = true;

            imp.clipAnimations = new[]
            {
                new ModelImporterClipAnimation
                {
                    name = def.Name,
                    takeName = imp.defaultClipAnimations.Length > 0
                               ? imp.defaultClipAnimations[0].takeName : null,
                    firstFrame = def.First,
                    lastFrame = def.Last,
                    loopTime = def.Loop,
                    // The takes do not start and end on the same pose, so without pose
                    // matching a looping clip visibly pops every cycle.
                    loopPose = def.Loop,
                    lockRootHeightY = true,
                    keepOriginalPositionY = true,
                },
            };
            imp.SaveAndReimport();

            return AssetDatabase.LoadAllAssetsAtPath(path)
                                .OfType<AnimationClip>()
                                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        /// <summary>
        /// Re-assign the freshly built controller to any Animator in the open scene.
        ///
        /// The controller asset is deleted and recreated rather than edited in place, so it
        /// comes back with a new GUID and every scene reference to it is left dangling. The
        /// hero would keep its Animator and quietly stop animating, which looks like a
        /// retargeting bug and is not one.
        /// </summary>
        static int RepointSceneAnimators()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (ctrl == null) return 0;

            int n = 0;
            foreach (var anim in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include,
                                                                   FindObjectsSortMode.None))
            {
                // Only ours: anything else in the scene keeps whatever it had.
                if (anim.GetComponentInParent<PlayerController>() == null) continue;
                anim.runtimeAnimatorController = ctrl;
                EditorUtility.SetDirty(anim);
                n++;
            }

            if (n > 0)
                UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            return n;
        }

        static void BuildController(Dictionary<string, AnimationClip> clips)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath));
            AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Carrying", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Grab", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Celebrate", AnimatorControllerParameterType.Trigger);

            // Playback rate for the locomotion blend trees. The hero moves far faster than
            // a real walk, so the clip is sped up to keep the feet roughly under him.
            var loco = new AnimatorControllerParameter
            {
                name = "LocoSpeed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f,
            };
            ctrl.AddParameter(loco);

            var sm = ctrl.layers[0].stateMachine;
            var idle = clips["idle"];

            var move = MakeLocomotion(ctrl, "Locomotion", idle,
                                      clips.TryGetValue("walk", out var w) ? w : idle);
            var carry = MakeLocomotion(ctrl, "LocomotionCarry", idle,
                                       clips.TryGetValue("carry", out var c) ? c : w ?? idle);

            sm.defaultState = move;
            sm.AddState(move, new Vector3(260f, 0f, 0f));

            // Hands full / hands empty.
            var toCarry = move.AddTransition(carry);
            toCarry.hasExitTime = false;
            toCarry.duration = 0.18f;
            toCarry.AddCondition(AnimatorConditionMode.If, 0f, "Carrying");

            var toMove = carry.AddTransition(move);
            toMove.hasExitTime = false;
            toMove.duration = 0.18f;
            toMove.AddCondition(AnimatorConditionMode.IfNot, 0f, "Carrying");

            // One-shots. They return to Locomotion, and the Carrying transition above
            // immediately forwards to the carry tree if the hands are full - one extra
            // 0.18s blend, which reads as a single motion.
            AddOneShot(ctrl, sm, move, clips, "grab", "Grab", 0.08f, 0.80f);
            AddOneShot(ctrl, sm, move, clips, "celebrate", "Celebrate", 0.15f, 0.90f);
        }

        /// <summary>A 1D blend from standing to moving, played at LocoSpeed.</summary>
        static AnimatorState MakeLocomotion(AnimatorController ctrl, string name,
                                            AnimationClip still, AnimationClip moving)
        {
            var state = ctrl.CreateBlendTreeInController(name, out var tree);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(still, 0f);
            tree.AddChild(moving, 1f);

            state.speedParameterActive = true;
            state.speedParameter = "LocoSpeed";
            return state;
        }

        static void AddOneShot(AnimatorController ctrl, AnimatorStateMachine sm,
                               AnimatorState returnTo, Dictionary<string, AnimationClip> clips,
                               string clipName, string trigger, float blendIn, float exitAt)
        {
            if (!clips.TryGetValue(clipName, out var clip)) return;

            var state = sm.AddState(clipName);
            state.motion = clip;

            var enter = sm.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = blendIn;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);

            var exit = state.AddTransition(returnTo);
            exit.hasExitTime = true;
            exit.exitTime = exitAt;
            exit.duration = 0.18f;
        }
    }
}
