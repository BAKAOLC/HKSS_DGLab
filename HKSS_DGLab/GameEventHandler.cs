using System;
using BepInEx.Logging;
using DGLabCSharp.Enums;

namespace HKSS_DGLab
{
    /// <summary>
    ///     游戏事件处理器，监听游戏中的各种事件并触发相应的DGLab响应
    /// </summary>
    public class GameEventHandler(ManualLogSource logger, DGLabController dgLabController, GameEventConfig config)
    {
        private const int DamageDebounceMs = 1000; // 1秒防抖间隔

        private readonly GameEventConfig _config = config ?? throw new ArgumentNullException(nameof(config));

        private readonly DGLabController _dgLabController =
            dgLabController ?? throw new ArgumentNullException(nameof(dgLabController));

        private readonly ManualLogSource _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private DateTime _lastDamageTime = DateTime.MinValue;

        /// <summary>
        ///     初始化事件监听
        /// </summary>
        public void Initialize()
        {
            try
            {
                _logger.LogInfo("游戏事件处理器已初始化");
            }
            catch (Exception ex)
            {
                _logger.LogError($"初始化游戏事件处理器失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     玩家受到伤害事件处理
        /// </summary>
        public async void OnPlayerTakeDamage(int damage)
        {
            if (!_dgLabController.IsInitialized)
                return;

            try
            {
                // 防抖检查：如果距离上次受伤时间不足1秒，则忽略
                var currentTime = DateTime.Now;
                var timeSinceLastDamage = (currentTime - _lastDamageTime).TotalMilliseconds;

                if (timeSinceLastDamage < DamageDebounceMs) return;

                // 更新上次受伤时间
                _lastDamageTime = currentTime;

                _logger.LogInfo($"玩家受到 {damage} 点伤害");

                // 根据伤害值选择波形类型和持续时间
                var (waveType, duration) = GetDamageResponse(damage);

                // 发送到所有通道
                await _dgLabController.SendWaveToAllChannelsAsync(waveType, duration);
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理玩家受伤事件时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        ///     玩家死亡事件处理
        /// </summary>
        public async void OnPlayerDeath(bool nonLethal, bool frostDeath)
        {
            if (!_dgLabController.IsInitialized)
                return;

            try
            {
                var deathType = nonLethal ? "非致命死亡" : frostDeath ? "冰霜死亡" : "普通死亡";
                _logger.LogInfo($"玩家死亡: {deathType}");

                // 死亡事件：发送5秒的3级波形
                const WaveType waveType = WaveType.Type3;
                const int duration = 5; // 5秒

                // 发送到所有通道
                await _dgLabController.SendWaveToAllChannelsAsync(waveType, duration);
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理玩家死亡事件时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        ///     根据伤害值计算响应参数
        /// </summary>
        private static (WaveType waveType, int duration) GetDamageResponse(int damage)
        {
            return damage switch
            {
                >= 3 => (WaveType.Type3, 3),
                >= 2 => (WaveType.Type2, 2),
                _ => (WaveType.Type1, 1),
            };
        }


        /// <summary>
        ///     清理资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                _logger.LogInfo("游戏事件处理器已清理");
            }
            catch (Exception ex)
            {
                _logger.LogError($"清理游戏事件处理器时发生错误: {ex.Message}");
            }
        }
    }
}