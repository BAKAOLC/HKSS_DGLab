using System;
using GlobalEnums;
using HarmonyLib;
using UnityEngine;

namespace HKSS_DGLab
{
    /// <summary>
    ///     Harmony补丁类，监听玩家受伤和死亡事件
    /// </summary>
    [HarmonyPatch]
    public static class HarmonyPatches
    {
        /// <summary>
        ///     Hook HeroController.TakeDamage 方法来监听玩家受伤事件
        /// </summary>
        [HarmonyPatch(typeof(HeroController), "TakeDamage")]
        [HarmonyPrefix]
        private static void PrefixTakeDamage(ref GameObject go, CollisionSide damageSide, int damageAmount,
            HazardType hazardType, DamagePropertyFlags damagePropertyFlags = DamagePropertyFlags.None)
        {
            try
            {
                // 检查插件是否已初始化
                var plugin = Plugin.Instance;
                if (plugin == null || !plugin.IsReady())
                    return;

                if ((damagePropertyFlags & DamagePropertyFlags.Self) != DamagePropertyFlags.None &&
                    InteractManager.BlockingInteractable != null) return;

                // 触发DGLab响应
                var eventHandler = plugin.GetGameEventHandler();
                eventHandler?.OnPlayerTakeDamage(damageAmount);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"处理受伤事件时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        ///     Hook HeroController.Die 方法来监听玩家死亡事件
        /// </summary>
        [HarmonyPatch(typeof(HeroController), "Die")]
        [HarmonyPrefix]
        private static void PrefixDie(bool nonLethal, bool frostDeath)
        {
            try
            {
                // 检查插件是否已初始化
                var plugin = Plugin.Instance;
                if (plugin == null || !plugin.IsReady())
                    return;

                // 触发DGLab死亡响应
                var eventHandler = plugin.GetGameEventHandler();
                eventHandler?.OnPlayerDeath(nonLethal, frostDeath);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"处理死亡事件时发生错误: {ex.Message}");
            }
        }
    }
}