using System;
using HarmonyLib;

namespace HKSS_DGLab
{
    /// <summary>
    ///     Harmony补丁类，监听玩家受伤和死亡事件
    /// </summary>
    [HarmonyPatch]
    public static class HarmonyPatches
    {
        /// <summary>
        ///     Hook PlayerData.TakeHealth 方法来监听玩家掉血事件
        /// </summary>
        /// <param name="amount"></param>
        /// <param name="hasBlueHealth"></param>
        /// <param name="allowFracturedMaskBreak"></param>
        [HarmonyPatch(typeof(PlayerData), "TakeHealth")]
        private static void PrefixTakeHealth(int amount, bool hasBlueHealth, bool allowFracturedMaskBreak)
        {
            // 触发DGLab响应
            try
            {
                var plugin = Plugin.Instance;
                if (plugin == null || !plugin.IsReady())
                    return;

                if (amount <= 0)
                    return; // 只处理正数掉血

                var eventHandler = plugin.GetGameEventHandler();
                eventHandler?.OnPlayerTakeDamage(amount);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"处理掉血事件时发生错误: {ex.Message}");
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