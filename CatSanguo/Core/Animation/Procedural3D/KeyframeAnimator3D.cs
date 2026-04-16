using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Core.Animation.Procedural3D;

/// <summary>
/// 3D关键帧动画 - 预计算16帧(4行x4列)零件变换
/// 行0=Idle, 行1=Walk, 行2=Attack, 行3=Death
/// </summary>
public static class KeyframeAnimator3D
{
    /// <summary>
    /// 获取指定动画行/帧的零件变换增量
    /// </summary>
    /// <param name="row">动画行: 0=Idle, 1=Walk, 2=Attack, 3=Death</param>
    /// <param name="col">帧序号: 0-3</param>
    /// <param name="unitType">兵种(影响攻击动画)</param>
    /// <param name="isGeneral">是否武将</param>
    public static Dictionary<string, Matrix> GetPose(int row, int col, UnitType unitType, bool isGeneral)
    {
        return row switch
        {
            0 => GetIdlePose(col, isGeneral),
            1 => GetWalkPose(col, unitType, isGeneral),
            2 => GetAttackPose(col, unitType, isGeneral),
            3 => GetDeathPose(col),
            _ => new Dictionary<string, Matrix>()
        };
    }

    private static Dictionary<string, Matrix> GetIdlePose(int frame, bool isGeneral)
    {
        var pose = new Dictionary<string, Matrix>();

        // 轻微呼吸起伏
        float[] breathY = { 0, 0.008f, 0, -0.005f };
        pose["torso"] = Matrix.CreateTranslation(0, breathY[frame], 0);

        // 头微晃
        float[] headTilt = { 0, 2f, 0, -1.5f };
        pose["head"] = Matrix.CreateRotationZ(MathHelper.ToRadians(headTilt[frame]));

        // 武器手臂微摆
        float[] armSwing = { 0, -3f, 0, 2f };
        pose["arm_r"] = Matrix.CreateRotationZ(MathHelper.ToRadians(armSwing[frame]));

        if (isGeneral)
        {
            // 披风微摆
            float[] capeSway = { 0, 3f, 0, -2f };
            pose["cape"] = Matrix.CreateRotationY(MathHelper.ToRadians(capeSway[frame]));
        }

        return pose;
    }

    private static Dictionary<string, Matrix> GetWalkPose(int frame, UnitType unitType, bool isGeneral)
    {
        var pose = new Dictionary<string, Matrix>();
        bool isMounted = unitType == UnitType.Cavalry || unitType == UnitType.HeavyCavalry ||
                         unitType == UnitType.LightCavalry;

        // 身体弹跳
        float[] bodyBounce = { 0, 0.015f, 0, 0.015f };
        pose["torso"] = Matrix.CreateTranslation(0, bodyBounce[frame], 0);

        if (!isMounted)
        {
            // 腿交替摆动
            float[] legSwing = { -22f, 0, 22f, 0 };
            pose["leg_r"] = Matrix.CreateRotationX(MathHelper.ToRadians(legSwing[frame]));
            pose["leg_l"] = Matrix.CreateRotationX(MathHelper.ToRadians(-legSwing[frame]));

            // 手臂反向摆
            float[] armSwing = { 15f, 0, -15f, 0 };
            pose["arm_r"] = Matrix.CreateRotationX(MathHelper.ToRadians(armSwing[frame]));
            pose["arm_l"] = Matrix.CreateRotationX(MathHelper.ToRadians(-armSwing[frame]));
        }
        else
        {
            // 骑兵 - 骑手轻微前后摆
            float[] riderLean = { -3f, 0, 3f, 0 };
            pose["torso"] = Matrix.CreateRotationZ(MathHelper.ToRadians(riderLean[frame]))
                * Matrix.CreateTranslation(0, bodyBounce[frame], 0);

            // 马腿交替
            float[] horseLeg = { -20f, 0, 20f, 0 };
            pose["horse_leg_fr"] = Matrix.CreateRotationX(MathHelper.ToRadians(horseLeg[frame]));
            pose["horse_leg_bl"] = Matrix.CreateRotationX(MathHelper.ToRadians(horseLeg[frame]));
            pose["horse_leg_fl"] = Matrix.CreateRotationX(MathHelper.ToRadians(-horseLeg[frame]));
            pose["horse_leg_br"] = Matrix.CreateRotationX(MathHelper.ToRadians(-horseLeg[frame]));
        }

        if (isGeneral)
        {
            float[] capeSway = { 5f, -3f, -5f, 3f };
            pose["cape"] = Matrix.CreateRotationY(MathHelper.ToRadians(capeSway[frame]));
        }

        return pose;
    }

    private static Dictionary<string, Matrix> GetAttackPose(int frame, UnitType unitType, bool isGeneral)
    {
        var pose = new Dictionary<string, Matrix>();

        bool isRanged = unitType == UnitType.Archer || unitType == UnitType.Crossbowman;
        bool isSpear = unitType == UnitType.Spearman || unitType == UnitType.HeavyCavalry;

        if (isRanged)
        {
            // 弓兵/弩手: 拉弓→瞄准→最大→放箭
            float[] armPull = { -20f, -40f, -45f, -10f };
            float[] bodyLean = { 0, -5f, -8f, 3f };
            pose["arm_r"] = Matrix.CreateRotationZ(MathHelper.ToRadians(armPull[frame]));
            pose["arm_l"] = Matrix.CreateRotationZ(MathHelper.ToRadians(armPull[frame] * 0.5f));
            pose["torso"] = Matrix.CreateRotationZ(MathHelper.ToRadians(bodyLean[frame]));
        }
        else if (isSpear)
        {
            // 枪兵: 蓄力→前刺→最远→收回
            float[] thrustX = { 0, -0.08f, -0.15f, -0.03f };
            float[] bodyLean = { 0, -8f, -15f, -3f };
            pose["arm_r"] = Matrix.CreateTranslation(thrustX[frame], 0, 0);
            pose["torso"] = Matrix.CreateRotationZ(MathHelper.ToRadians(bodyLean[frame]));
        }
        else
        {
            // 劈砍: 蓄力→挥出→击中→收回
            float[] swingAngle = { 30f, -10f, -40f, 5f };
            float[] bodyLean = { 5f, -3f, -12f, 0f };
            pose["arm_r"] = Matrix.CreateRotationZ(MathHelper.ToRadians(swingAngle[frame]));
            pose["torso"] = Matrix.CreateRotationZ(MathHelper.ToRadians(bodyLean[frame]));
        }

        // 前踏步
        float[] stepForward = { 0, -8f, -15f, -5f };
        pose["leg_r"] = Matrix.CreateRotationX(MathHelper.ToRadians(stepForward[frame]));
        pose["leg_l"] = Matrix.CreateRotationX(MathHelper.ToRadians(-stepForward[frame] * 0.5f));

        if (isGeneral)
        {
            float[] capeBlow = { -3f, 5f, 12f, 2f };
            pose["cape"] = Matrix.CreateRotationY(MathHelper.ToRadians(capeBlow[frame]));
        }

        return pose;
    }

    private static Dictionary<string, Matrix> GetDeathPose(int frame)
    {
        var pose = new Dictionary<string, Matrix>();

        // 身体逐帧后倾直至倒地
        float[] fallAngle = { 10f, 35f, 65f, 85f };
        float[] fallY = { -0.02f, -0.10f, -0.25f, -0.40f };
        float[] fallX = { 0.02f, 0.08f, 0.15f, 0.20f };

        pose["torso"] = Matrix.CreateRotationZ(MathHelper.ToRadians(fallAngle[frame]))
            * Matrix.CreateTranslation(fallX[frame], fallY[frame], 0);

        // 手臂自然下垂
        float[] armDrop = { 10f, 25f, 40f, 60f };
        pose["arm_r"] = Matrix.CreateRotationZ(MathHelper.ToRadians(armDrop[frame]));
        pose["arm_l"] = Matrix.CreateRotationZ(MathHelper.ToRadians(-armDrop[frame] * 0.7f));

        // 腿弯曲
        float[] legBend = { 5f, 15f, 25f, 35f };
        pose["leg_r"] = Matrix.CreateRotationX(MathHelper.ToRadians(legBend[frame]));
        pose["leg_l"] = Matrix.CreateRotationX(MathHelper.ToRadians(legBend[frame] * 0.8f));

        return pose;
    }
}
