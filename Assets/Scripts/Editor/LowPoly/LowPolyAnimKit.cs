using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds Mecanim AnimationClips (by keyframing local transform curves on a primitive
// rig) and the AnimatorControllers that wire them into a simple locomotion state
// machine. Everything is get-or-create / rebuilt in place so re-running the editor
// tool that calls this keeps the same asset GUIDs.
public static class LowPolyAnimKit
{
    public const string AnimationFolder = "Assets/Animations";

    public static AnimationClip GetOrCreateClip(string name, bool loop)
    {
        string path = $"{AnimationFolder}/{name}.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

        if (clip == null)
        {
            clip = new AnimationClip { name = name };
            AssetDatabase.CreateAsset(clip, path);
        }
        else
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationUtility.SetEditorCurve(clip, binding, null);
            }
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorUtility.SetDirty(clip);
        return clip;
    }

    public static void SetEulerCurve(AnimationClip clip, string relativePath, float[] times, Vector3[] eulers)
    {
        AnimationCurve cx = new AnimationCurve();
        AnimationCurve cy = new AnimationCurve();
        AnimationCurve cz = new AnimationCurve();

        for (int i = 0; i < times.Length; i++)
        {
            cx.AddKey(Linear(times[i], eulers[i].x));
            cy.AddKey(Linear(times[i], eulers[i].y));
            cz.AddKey(Linear(times[i], eulers[i].z));
        }

        clip.SetCurve(relativePath, typeof(Transform), "localEulerAngles.x", cx);
        clip.SetCurve(relativePath, typeof(Transform), "localEulerAngles.y", cy);
        clip.SetCurve(relativePath, typeof(Transform), "localEulerAngles.z", cz);
    }

    public static void SetPositionYCurve(AnimationClip clip, string relativePath, float[] times, float[] ys)
    {
        AnimationCurve curve = new AnimationCurve();
        for (int i = 0; i < times.Length; i++) curve.AddKey(Linear(times[i], ys[i]));
        clip.SetCurve(relativePath, typeof(Transform), "localPosition.y", curve);
    }

    private static Keyframe Linear(float time, float value)
    {
        return new Keyframe(time, value, 0f, 0f);
    }

    public static AnimatorController BuildBipedController(
        string name,
        AnimationClip idle,
        string walkStateName,
        AnimationClip walk,
        AnimationClip run,
        string actionParamName,
        AnimationClip action,
        AnimationClip death,
        float runThreshold)
    {
        string path = $"{AnimationFolder}/{name}.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        AnimatorStateMachine root = controller.layers[0].stateMachine;

        foreach (ChildAnimatorState child in root.states) root.RemoveState(child.state);
        foreach (AnimatorStateTransition t in root.anyStateTransitions) root.RemoveAnyStateTransition(t);
        foreach (AnimatorControllerParameter p in controller.parameters) controller.RemoveParameter(p);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter(actionParamName, AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

        AnimatorState idleState = root.AddState("Idle");
        idleState.motion = idle;

        AnimatorState walkState = root.AddState(walkStateName);
        walkState.motion = walk;

        AnimatorState runState = null;
        if (run != null)
        {
            runState = root.AddState("Run");
            runState.motion = run;
        }

        AnimatorState actionState = root.AddState(actionParamName);
        actionState.motion = action;

        AnimatorState deathState = root.AddState("Death");
        deathState.motion = death;

        root.defaultState = idleState;

        AddCondition(idleState.AddTransition(walkState), AnimatorConditionMode.Greater, 0.1f, "Speed", 0.1f);
        AddCondition(walkState.AddTransition(idleState), AnimatorConditionMode.Less, 0.1f, "Speed", 0.1f);

        if (runState != null)
        {
            AddCondition(walkState.AddTransition(runState), AnimatorConditionMode.Greater, runThreshold, "Speed", 0.1f);
            AddCondition(runState.AddTransition(walkState), AnimatorConditionMode.Less, runThreshold, "Speed", 0.1f);
        }

        AnimatorStateTransition toAction = root.AddAnyStateTransition(actionState);
        toAction.hasExitTime = false;
        toAction.duration = 0.05f;
        toAction.canTransitionToSelf = false;
        toAction.AddCondition(AnimatorConditionMode.If, 0f, actionParamName);

        AnimatorStateTransition actionToIdle = actionState.AddTransition(idleState);
        actionToIdle.hasExitTime = true;
        actionToIdle.exitTime = 0.85f;
        actionToIdle.duration = 0.1f;
        actionToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        AnimatorStateTransition actionToWalk = actionState.AddTransition(walkState);
        actionToWalk.hasExitTime = true;
        actionToWalk.exitTime = 0.85f;
        actionToWalk.duration = 0.1f;
        actionToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        AnimatorStateTransition toDeath = root.AddAnyStateTransition(deathState);
        toDeath.hasExitTime = false;
        toDeath.duration = 0.05f;
        toDeath.canTransitionToSelf = false;
        toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Death");

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void AddCondition(AnimatorStateTransition transition, AnimatorConditionMode mode, float threshold, string param, float duration)
    {
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.AddCondition(mode, threshold, param);
    }

    // Same Idle/Walk/Death shape as BuildBipedController, but with N independent one-shot
    // action states (each its own trigger) instead of a single action - the Boss has
    // Attack, GroundSlam, Charge and Roar all wired the same way.
    public static AnimatorController BuildBossController(
        string name,
        AnimationClip idle,
        AnimationClip walk,
        (string paramName, AnimationClip clip)[] actions,
        AnimationClip death)
    {
        string path = $"{AnimationFolder}/{name}.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        AnimatorStateMachine root = controller.layers[0].stateMachine;

        foreach (ChildAnimatorState child in root.states) root.RemoveState(child.state);
        foreach (AnimatorStateTransition t in root.anyStateTransitions) root.RemoveAnyStateTransition(t);
        foreach (AnimatorControllerParameter p in controller.parameters) controller.RemoveParameter(p);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        foreach ((string paramName, AnimationClip _) in actions) controller.AddParameter(paramName, AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

        AnimatorState idleState = root.AddState("Idle");
        idleState.motion = idle;

        AnimatorState walkState = root.AddState("Walk");
        walkState.motion = walk;

        root.defaultState = idleState;

        AddCondition(idleState.AddTransition(walkState), AnimatorConditionMode.Greater, 0.1f, "Speed", 0.1f);
        AddCondition(walkState.AddTransition(idleState), AnimatorConditionMode.Less, 0.1f, "Speed", 0.1f);

        foreach ((string paramName, AnimationClip clip) in actions)
        {
            AnimatorState actionState = root.AddState(paramName);
            actionState.motion = clip;

            AnimatorStateTransition toAction = root.AddAnyStateTransition(actionState);
            toAction.hasExitTime = false;
            toAction.duration = 0.05f;
            toAction.canTransitionToSelf = false;
            toAction.AddCondition(AnimatorConditionMode.If, 0f, paramName);

            AnimatorStateTransition actionToIdle = actionState.AddTransition(idleState);
            actionToIdle.hasExitTime = true;
            actionToIdle.exitTime = 0.85f;
            actionToIdle.duration = 0.15f;
            actionToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            AnimatorStateTransition actionToWalk = actionState.AddTransition(walkState);
            actionToWalk.hasExitTime = true;
            actionToWalk.exitTime = 0.85f;
            actionToWalk.duration = 0.15f;
            actionToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        }

        AnimatorState deathState = root.AddState("Death");
        deathState.motion = death;

        AnimatorStateTransition toDeath = root.AddAnyStateTransition(deathState);
        toDeath.hasExitTime = false;
        toDeath.duration = 0.05f;
        toDeath.canTransitionToSelf = false;
        toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Death");

        EditorUtility.SetDirty(controller);
        return controller;
    }
}
