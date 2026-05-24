using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SetupPlayerAnimator
{
    private const string ControllerPath = "Assets/Main Character/PlayerAnimator.controller";
    private const string AnimationFolder = "Assets/Main Character/Animations";

    private static readonly Vector3 IdlePosition = new Vector3(210f, 10f, 0f);
    private static readonly Vector3 WalkPosition = new Vector3(210f, 100f, 0f);
    private static readonly Vector3 RunPosition = new Vector3(210f, 200f, 0f);

    private static readonly ActionState[] ActionStates =
    {
        new ActionState("Jump", "Jump", "Jump", AnimatorControllerParameterType.Trigger, new Vector3(210f, 300f, 0f), false),
        new ActionState("Crawl", "Crawl", "Crawl", AnimatorControllerParameterType.Bool, new Vector3(210f, 400f, 0f), true),
        new ActionState("Power", "Power", "Power", AnimatorControllerParameterType.Trigger, new Vector3(540f, 10f, 0f), false),
        new ActionState("Slash_01", "Slash_01", "Slash", AnimatorControllerParameterType.Trigger, new Vector3(540f, 100f, 0f), false),
        new ActionState("Slash_spin", "Slash_spin", "SlashSpin", AnimatorControllerParameterType.Trigger, new Vector3(540f, 190f, 0f), false),
        new ActionState("Block_01", "Block_01", "Block", AnimatorControllerParameterType.Bool, new Vector3(540f, 280f, 0f), true),
    };

    [MenuItem("Null Saint/Setup Player Animator")]
    public static void Setup()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"Could not find Animator Controller at {ControllerPath}");
            return;
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
        foreach (ActionState action in ActionStates)
        {
            EnsureParameter(controller, action.ParameterName, action.ParameterType);
        }

        AnimatorState idle = EnsureState(stateMachine, "Idle", "Idle", IdlePosition);
        AnimatorState walk = EnsureState(stateMachine, "Walk", "Walk", WalkPosition);
        AnimatorState run = EnsureState(stateMachine, "Run", "Run", RunPosition);
        stateMachine.defaultState = idle;

        EnsureLocomotionTransitions(idle, walk, run);

        foreach (ActionState action in ActionStates)
        {
            AnimatorState state = EnsureState(stateMachine, action.StateName, action.ClipName, action.Position);
            RemoveAnyStateTransitionsTo(stateMachine, state);
            RemoveStateTransitions(state);
            EnsureAnyStateTransition(stateMachine, state, action);

            if (action.IsHeld)
            {
                EnsureBoolReturnTransition(state, idle, action.ParameterName);
            }
            else
            {
                EnsureExitReturnTransition(state, idle);
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Player Animator setup complete.");
    }

    private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        AnimatorControllerParameter existing = controller.parameters.FirstOrDefault(parameter => parameter.name == name);
        if (existing != null)
        {
            if (existing.type != type)
            {
                Debug.LogWarning($"Animator parameter '{name}' already exists as {existing.type}; expected {type}. Delete it manually if this is wrong.");
            }

            return;
        }

        controller.AddParameter(name, type);
    }

    private static AnimatorState EnsureState(AnimatorStateMachine stateMachine, string stateName, string clipName, Vector3 position)
    {
        ChildAnimatorState child = stateMachine.states.FirstOrDefault(state => state.state.name == stateName);
        AnimatorState stateObject;

        if (child.state != null)
        {
            stateObject = child.state;
            SetStatePosition(stateMachine, stateObject, position);
        }
        else
        {
            stateObject = stateMachine.AddState(stateName, position);
        }

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimationFolder}/{clipName}.fbx");
        if (clip == null)
        {
            Debug.LogWarning($"Could not find animation clip {AnimationFolder}/{clipName}.fbx");
        }
        else
        {
            stateObject.motion = clip;
        }

        stateObject.writeDefaultValues = false;
        return stateObject;
    }

    private static void RemoveAnyStateTransitionsTo(AnimatorStateMachine stateMachine, AnimatorState destination)
    {
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.ToArray())
        {
            if (transition.destinationState == destination)
            {
                stateMachine.RemoveAnyStateTransition(transition);
            }
        }
    }

    private static void RemoveStateTransitions(AnimatorState state)
    {
        foreach (AnimatorStateTransition transition in state.transitions.ToArray())
        {
            state.RemoveTransition(transition);
        }
    }

    private static void SetStatePosition(AnimatorStateMachine stateMachine, AnimatorState state, Vector3 position)
    {
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state == state)
            {
                states[i].position = position;
                stateMachine.states = states;
                return;
            }
        }
    }

    private static void EnsureLocomotionTransitions(AnimatorState idle, AnimatorState walk, AnimatorState run)
    {
        EnsureFloatTransition(idle, walk, "Speed", AnimatorConditionMode.Greater, 0.1f, false);
        EnsureFloatTransition(walk, idle, "Speed", AnimatorConditionMode.Less, 0.1f, false);
        EnsureFloatTransition(walk, run, "Speed", AnimatorConditionMode.Greater, 1.1f, false);
        EnsureFloatTransition(run, walk, "Speed", AnimatorConditionMode.Less, 1.1f, false);
    }

    private static void EnsureFloatTransition(
        AnimatorState source,
        AnimatorState destination,
        string parameter,
        AnimatorConditionMode mode,
        float threshold,
        bool hasExitTime)
    {
        AnimatorStateTransition transition = FindTransition(source.transitions, destination, parameter);
        if (transition == null)
        {
            transition = source.AddTransition(destination);
        }

        ConfigureTransition(transition, hasExitTime);
        ReplaceConditions(transition, new AnimatorCondition
        {
            mode = mode,
            parameter = parameter,
            threshold = threshold,
        });
    }

    private static void EnsureAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState destination, ActionState action)
    {
        AnimatorStateTransition transition = FindTransition(stateMachine.anyStateTransitions, destination, action.ParameterName);
        if (transition == null)
        {
            transition = stateMachine.AddAnyStateTransition(destination);
        }

        ConfigureTransition(transition, false);
        transition.canTransitionToSelf = false;

        AnimatorConditionMode mode = action.ParameterType == AnimatorControllerParameterType.Bool
            ? AnimatorConditionMode.If
            : AnimatorConditionMode.If;

        ReplaceConditions(transition, new AnimatorCondition
        {
            mode = mode,
            parameter = action.ParameterName,
            threshold = 0f,
        });
    }

    private static void EnsureBoolReturnTransition(AnimatorState source, AnimatorState idle, string parameter)
    {
        AnimatorStateTransition transition = FindTransition(source.transitions, idle, parameter);
        if (transition == null)
        {
            transition = source.AddTransition(idle);
        }

        ConfigureTransition(transition, false);
        ReplaceConditions(transition, new AnimatorCondition
        {
            mode = AnimatorConditionMode.IfNot,
            parameter = parameter,
            threshold = 0f,
        });
    }

    private static void EnsureExitReturnTransition(AnimatorState source, AnimatorState idle)
    {
        AnimatorStateTransition transition = source.transitions.FirstOrDefault(candidate => candidate.destinationState == idle && candidate.conditions.Length == 0);
        if (transition == null)
        {
            transition = source.AddTransition(idle);
        }

        ConfigureTransition(transition, true);
        transition.exitTime = 0.85f;
        ReplaceConditions(transition);
    }

    private static AnimatorStateTransition FindTransition(IEnumerable<AnimatorStateTransition> transitions, AnimatorState destination, string parameter)
    {
        return transitions.FirstOrDefault(transition =>
            transition.destinationState == destination &&
            transition.conditions.Any(condition => condition.parameter == parameter));
    }

    private static void ConfigureTransition(AnimatorStateTransition transition, bool hasExitTime)
    {
        transition.hasExitTime = hasExitTime;
        transition.hasFixedDuration = true;
        transition.duration = 0.1f;
        transition.offset = 0f;
        transition.interruptionSource = TransitionInterruptionSource.None;
        transition.orderedInterruption = true;
    }

    private static void ReplaceConditions(AnimatorStateTransition transition, params AnimatorCondition[] conditions)
    {
        foreach (AnimatorCondition condition in transition.conditions.ToArray())
        {
            transition.RemoveCondition(condition);
        }

        foreach (AnimatorCondition condition in conditions)
        {
            transition.AddCondition(condition.mode, condition.threshold, condition.parameter);
        }
    }

    private readonly struct ActionState
    {
        public ActionState(
            string stateName,
            string clipName,
            string parameterName,
            AnimatorControllerParameterType parameterType,
            Vector3 position,
            bool isHeld)
        {
            StateName = stateName;
            ClipName = clipName;
            ParameterName = parameterName;
            ParameterType = parameterType;
            Position = position;
            IsHeld = isHeld;
        }

        public string StateName { get; }
        public string ClipName { get; }
        public string ParameterName { get; }
        public AnimatorControllerParameterType ParameterType { get; }
        public Vector3 Position { get; }
        public bool IsHeld { get; }
    }
}
