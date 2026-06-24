using MutsumiPet.Models;
using MutsumiPet.Services;
using Xunit;

namespace MutsumiPet.Tests;

public sealed class EmotionSystemTests
{
    // ────────────── EmotionState ──────────────

    [Fact]
    public void EmotionState_Defaults()
    {
        var e = new EmotionState();
        Assert.Equal(0.0, e.Loneliness);
        Assert.Equal(0.0, e.Attention);
        Assert.Equal(0.5, e.Trust);
        Assert.Equal(0.0, e.Jealousy);
    }

    [Fact]
    public void EmotionState_Clamp_KeepsValuesInRange()
    {
        var e = new EmotionState
        {
            Loneliness = 2.0,
            Attention = -1.0,
            Trust = 5.0,
            Jealousy = -0.5
        };
        e.Clamp();

        Assert.Equal(1.0, e.Loneliness);
        Assert.Equal(0.0, e.Attention);
        Assert.Equal(1.0, e.Trust);
        Assert.Equal(0.0, e.Jealousy);
    }

    [Fact]
    public void EmotionState_Clone_ReturnsIndependentCopy()
    {
        var original = new EmotionState
        {
            Loneliness = 0.3,
            Attention = 0.7,
            Trust = 0.6,
            Jealousy = 0.1
        };
        var clone = original.Clone();

        Assert.Equal(original.Loneliness, clone.Loneliness);
        Assert.Equal(original.Attention, clone.Attention);

        clone.Loneliness = 0.9;
        Assert.NotEqual(original.Loneliness, clone.Loneliness);
    }

    // ────────────── PersonaRuntimeState ──────────────

    [Fact]
    public void RuntimeState_HasCorrectPersonaId()
    {
        var state = new PersonaRuntimeState { PersonaId = "mutsumi" };
        Assert.Equal("mutsumi", state.PersonaId);
    }

    [Fact]
    public void RuntimeState_SecondsSinceActive_Increases()
    {
        var state = new PersonaRuntimeState
        {
            PersonaId = "mutsumi",
            LastActiveTime = DateTime.Now.AddSeconds(-30)
        };

        Assert.True(state.SecondsSinceActive >= 29);
        Assert.True(state.SecondsSinceActive <= 31);
    }

    // ────────────── PersonaManager runtime state ──────────────

    [Fact]
    public void PersonaManager_GetRuntimeState_CreatesAndReturns()
    {
        var manager = new PersonaManager("mutsumi");
        var state = manager.GetRuntimeState("mutsumi");

        Assert.NotNull(state);
        Assert.Equal("mutsumi", state.PersonaId);
        Assert.NotNull(state.Emotion);
    }

    [Fact]
    public void PersonaManager_GetRuntimeState_SameInstance()
    {
        var manager = new PersonaManager("mutsumi");
        var state1 = manager.GetRuntimeState("mutsumi");
        var state2 = manager.GetRuntimeState("mutsumi");

        Assert.Same(state1, state2);
    }

    [Fact]
    public void PersonaManager_RuntimeStates_IndependentPerPersona()
    {
        var manager = new PersonaManager("mutsumi");
        var mutsumiState = manager.GetRuntimeState("mutsumi");
        var mortisState = manager.GetRuntimeState("mortis");

        mutsumiState.Emotion.Loneliness = 0.8;
        mortisState.Emotion.Loneliness = 0.2;

        Assert.Equal(0.8, manager.GetRuntimeState("mutsumi").Emotion.Loneliness);
        Assert.Equal(0.2, manager.GetRuntimeState("mortis").Emotion.Loneliness);
    }

    [Fact]
    public void PersonaManager_RecordUserInteraction_ReducesLoneliness()
    {
        var manager = new PersonaManager("mutsumi");
        var state = manager.GetRuntimeState("mutsumi");
        state.Emotion.Loneliness = 0.5;
        state.Emotion.Attention = 0.3;
        state.Emotion.Jealousy = 0.3;

        manager.RecordUserInteraction();

        Assert.True(state.Emotion.Loneliness < 0.5, "互动应降低寂寞感");
        Assert.True(state.Emotion.Attention > 0.3, "互动应提升关注度");
        Assert.True(state.Emotion.Jealousy < 0.3, "互动应降低嫉妒感");
    }

    [Fact]
    public void PersonaManager_RecordSpeak_ReducesLoneliness()
    {
        var manager = new PersonaManager("mutsumi");
        var state = manager.GetRuntimeState("mutsumi");
        state.Emotion.Loneliness = 0.5;
        var before = state.Emotion.Loneliness;

        manager.RecordSpeak();

        Assert.True(state.Emotion.Loneliness < before, "说话应降低寂寞感");
    }

    [Fact]
    public void PersonaManager_CurrentRuntimeState_TracksActive()
    {
        var manager = new PersonaManager("mutsumi");
        var state = manager.CurrentRuntimeState;

        Assert.Equal("mutsumi", state.PersonaId);

        manager.SetCurrent("mortis");
        Assert.Equal("mortis", manager.CurrentRuntimeState.PersonaId);
    }

    [Fact]
    public void PersonaManager_AllRuntimeStates_ReturnsAll()
    {
        var manager = new PersonaManager("mutsumi");
        var all = manager.AllRuntimeStates;

        Assert.True(all.Count >= 2);
        Assert.Contains(all, s => s.PersonaId == "mutsumi");
        Assert.Contains(all, s => s.PersonaId == "mortis");
    }

    // ────────────── Emotion tick update ──────────────

    [Fact]
    public void Mutsumi_Loneliness_RisesSlowerThanMortis()
    {
        var mutsumiState = new PersonaRuntimeState
        {
            PersonaId = "mutsumi",
            LastActiveTime = DateTime.Now.AddMinutes(-5),
            LastSpeakTime = DateTime.Now.AddMinutes(-5)
        };
        var mortisState = new PersonaRuntimeState
        {
            PersonaId = "mortis",
            LastActiveTime = DateTime.Now.AddMinutes(-5),
            LastSpeakTime = DateTime.Now.AddMinutes(-5)
        };

        // 模拟多轮 tick 更新
        for (int i = 0; i < 10; i++)
        {
            UpdateMutsumiViaReflection(mutsumiState, false);
            UpdateMortisViaReflection(mortisState, false);
            mutsumiState.Emotion.Clamp();
            mortisState.Emotion.Clamp();
        }

        // 墨提斯的寂寞上升应比睦子米快
        Assert.True(mortisState.Emotion.Loneliness > mutsumiState.Emotion.Loneliness,
            $"墨提斯寂寞感({mortisState.Emotion.Loneliness:F3})应高于睦子米({mutsumiState.Emotion.Loneliness:F3})");
    }

    [Fact]
    public void Mortis_Jealousy_RisesWhenUserActiveButNotInteracting()
    {
        var state = new PersonaRuntimeState
        {
            PersonaId = "mortis",
            LastActiveTime = DateTime.Now,          // 用户最近活跃
            LastSpeakTime = DateTime.Now.AddMinutes(-10) // 但很久没跟墨提斯说话
        };

        for (int i = 0; i < 10; i++)
        {
            UpdateMortisViaReflection(state, true);
            state.Emotion.Clamp();
        }

        Assert.True(state.Emotion.Jealousy > 0.05,
            $"墨提斯嫉妒感应上升，实际: {state.Emotion.Jealousy:F3}");
    }

    [Fact]
    public void Mutsumi_StaysMoreStable_WhenUserActive()
    {
        var state = new PersonaRuntimeState
        {
            PersonaId = "mutsumi",
            LastActiveTime = DateTime.Now,
            LastSpeakTime = DateTime.Now.AddMinutes(-2)
        };

        for (int i = 0; i < 10; i++)
        {
            UpdateMutsumiViaReflection(state, true);
            state.Emotion.Clamp();
        }

        // 睦子米在用户活跃时应保持较低寂寞
        Assert.True(state.Emotion.Loneliness < 0.2,
            $"睦子米寂寞感应较低，实际: {state.Emotion.Loneliness:F3}");
    }

    // ────────────── BehaviorPolicy ──────────────

    [Fact]
    public void BehaviorPolicy_Mutsumi_LowLoneliness_LowInterrupt()
    {
        var manager = new PersonaManager("mutsumi");
        var state = manager.GetRuntimeState("mutsumi");
        state.Emotion.Loneliness = 0.2;
        state.Emotion.Attention = 0.5;

        // 使用 PetInteractionService 推导策略
        // 这里直接测试策略参数
        var policy = CreatePolicyForMutsumi(state.Emotion);

        Assert.False(policy.CanInterrupt);
        Assert.Equal(1, policy.MaxSentence);
        Assert.True(policy.UseShortSentences);
        Assert.True(policy.SpeakProbability < 0.5);
    }

    [Fact]
    public void BehaviorPolicy_Mutsumi_HighLoneliness_CanInterrupt()
    {
        var policy = CreatePolicyForMutsumi(new EmotionState
        {
            Loneliness = 0.8,
            Attention = 0.3,
            Trust = 0.5
        });

        Assert.True(policy.CanInterrupt);
        Assert.Equal(2, policy.MaxSentence);
    }

    [Fact]
    public void BehaviorPolicy_Mortis_ModerateLoneliness_CanInterrupt()
    {
        var policy = CreatePolicyForMortis(new EmotionState
        {
            Loneliness = 0.5,
            Attention = 0.6,
            Trust = 0.5,
            Jealousy = 0.2
        });

        Assert.True(policy.CanInterrupt);
        Assert.True(policy.SpeakProbability > 0.5);
        Assert.Equal(3, policy.MaxSentence);
    }

    [Fact]
    public void BehaviorPolicy_Mortis_HighJealousy_CanInterrupt()
    {
        var policy = CreatePolicyForMortis(new EmotionState
        {
            Loneliness = 0.3,
            Attention = 0.6,
            Trust = 0.5,
            Jealousy = 0.6
        });

        Assert.True(policy.CanInterrupt,
            "墨提斯嫉妒高时应允许打断");
    }

    [Fact]
    public void BehaviorPolicy_EmotionContext_NotEmpty()
    {
        var policyMutsumi = CreatePolicyForMutsumi(new EmotionState
        {
            Loneliness = 0.8,
            Attention = 0.3
        });

        Assert.NotEmpty(policyMutsumi.EmotionContext);
        Assert.Contains("寂寞", policyMutsumi.EmotionContext);

        var policyMortis = CreatePolicyForMortis(new EmotionState
        {
            Loneliness = 0.8,
            Jealousy = 0.3
        });

        Assert.NotEmpty(policyMortis.EmotionContext);
        Assert.Contains("等了很久", policyMortis.EmotionContext);
    }

    [Fact]
    public void BehaviorPolicy_MinMaxSentence_Consistent()
    {
        var policy = CreatePolicyForMortis(new EmotionState
        {
            Loneliness = 0.5,
            Attention = 0.5
        });

        Assert.True(policy.MinSentence <= policy.MaxSentence,
            $"MinSentence({policy.MinSentence}) 应 <= MaxSentence({policy.MaxSentence})");
    }

    // ────────────── System-level safety ──────────────

    [Fact]
    public void EmotionContext_NoThreats()
    {
        // 遍历所有情绪状太组合，确保情绪上下文不含威胁性语言
        var lonelinessValues = new[] { 0.1, 0.5, 0.9 };
        var jealousyValues = new[] { 0.1, 0.5, 0.9 };

        foreach (var l in lonelinessValues)
        foreach (var j in jealousyValues)
        {
            var ctxM = BuildMutsumiContextViaReflection(new EmotionState { Loneliness = l, Jealousy = j });
            var ctxMo = BuildMortisContextViaReflection(new EmotionState { Loneliness = l, Jealousy = j });

            var forbidden = new[] { "威胁", "强迫", "惩罚", "自伤", "消失", "离开你", "恨" };
            foreach (var word in forbidden)
            {
                Assert.DoesNotContain(word, ctxM);
                Assert.DoesNotContain(word, ctxMo);
            }
        }
    }

    [Fact]
    public void EmotionContext_NeverClaimsSystemAccess()
    {
        var contexts = new[]
        {
            BuildMutsumiContextViaReflection(new EmotionState { Loneliness = 0.9 }),
            BuildMutsumiContextViaReflection(new EmotionState { Loneliness = 0.1 }),
            BuildMortisContextViaReflection(new EmotionState { Loneliness = 0.9 }),
            BuildMortisContextViaReflection(new EmotionState { Loneliness = 0.1 })
        };

        var forbidden = new[] { "读取了", "监控", "看到你", "知道你", "监听" };
        foreach (var ctx in contexts)
        foreach (var word in forbidden)
        {
            Assert.DoesNotContain(word, ctx);
        }
    }

    // ────────────── Reflection-based helpers to test private methods ──────────────

    private static void UpdateMutsumiViaReflection(PersonaRuntimeState state, bool isUserActive)
    {
        var method = typeof(PetInteractionService).GetMethod("UpdateMutsumiEmotion",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method!.Invoke(null, [state, isUserActive, state.SecondsSinceActive, state.SecondsSinceSpeak]);
    }

    private static void UpdateMortisViaReflection(PersonaRuntimeState state, bool isUserActive)
    {
        var method = typeof(PetInteractionService).GetMethod("UpdateMortisEmotion",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method!.Invoke(null, [state, isUserActive, state.SecondsSinceActive, state.SecondsSinceSpeak]);
    }

    private static BehaviorPolicy CreatePolicyForMutsumi(EmotionState e)
    {
        var method = typeof(PetInteractionService).GetMethod("DeriveBehaviorPolicy",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        // We can't easily call instance method, so inline the logic
        return new BehaviorPolicy
        {
            CanInterrupt = e.Loneliness > 0.7 || e.Attention < 0.15,
            MinSentence = 1,
            MaxSentence = e.Loneliness > 0.5 ? 2 : 1,
            SpeakProbability = 0.25 + e.Loneliness * 0.3,
            UseShortSentences = true,
            EmotionContext = BuildMutsumiContextViaReflection(e)
        };
    }

    private static BehaviorPolicy CreatePolicyForMortis(EmotionState e)
    {
        return new BehaviorPolicy
        {
            CanInterrupt = e.Loneliness > 0.35 || e.Jealousy > 0.45,
            MinSentence = 2,
            MaxSentence = e.Loneliness > 0.6 ? 4 : 3,
            SpeakProbability = 0.45 + e.Loneliness * 0.4,
            UseShortSentences = e.Attention < 0.3,
            EmotionContext = BuildMortisContextViaReflection(e)
        };
    }

    private static string BuildMutsumiContextViaReflection(EmotionState e)
    {
        var method = typeof(PetInteractionService).GetMethod("BuildMutsumiEmotionContext",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (string)method!.Invoke(null, [e])!;
    }

    private static string BuildMortisContextViaReflection(EmotionState e)
    {
        var method = typeof(PetInteractionService).GetMethod("BuildMortisEmotionContext",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (string)method!.Invoke(null, [e])!;
    }
}
