using System;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace HKSS_DGLab
{
    /// <summary>
    ///     游戏事件配置类，管理DGLab响应的基本设置
    /// </summary>
    public class GameEventConfig
    {
        private readonly ConfigFile _config;
        private readonly ManualLogSource _logger;

        public GameEventConfig(ConfigFile config, ManualLogSource logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            InitializeConfig();
        }

        // 基础设置
        public ConfigEntry<bool> EnablePlugin { get; private set; }
        public ConfigEntry<int> ServerPort { get; private set; }
        public ConfigEntry<bool> EnableDebugLogging { get; private set; }

        /// <summary>
        ///     初始化配置项
        /// </summary>
        private void InitializeConfig()
        {
            // 基础设置
            EnablePlugin = _config.Bind("基础设置", "启用插件", true,
                "是否启用DGLab插件功能");

            ServerPort = _config.Bind("基础设置", "服务器端口", 9999,
                new ConfigDescription("DGLab WebSocket服务器监听端口",
                    new AcceptableValueRange<int>(1024, 65535)));

            // 高级设置
            EnableDebugLogging = _config.Bind("高级设置", "启用调试日志", false,
                "是否输出详细的调试信息");

            _logger.LogInfo("游戏事件配置已加载");
        }

        /// <summary>
        ///     保存配置到文件
        /// </summary>
        public void Save()
        {
            try
            {
                _config.Save();
                _logger.LogInfo("配置已保存");
            }
            catch (Exception ex)
            {
                _logger.LogError($"保存配置失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     重新加载配置
        /// </summary>
        public void Reload()
        {
            try
            {
                _config.Reload();
                _logger.LogInfo("配置已重新加载");
            }
            catch (Exception ex)
            {
                _logger.LogError($"重新加载配置失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     验证配置的有效性
        /// </summary>
        public bool ValidateConfig()
        {
            if (ServerPort.Value is >= 1024 and <= 65535) return true;
            _logger.LogWarning("服务器端口应该在1024-65535范围内");

            return false;
        }

        /// <summary>
        ///     获取配置摘要信息
        /// </summary>
        public string GetConfigSummary()
        {
            return $"插件启用: {EnablePlugin.Value}, " +
                   $"服务器端口: {ServerPort.Value}, " +
                   $"调试日志: {EnableDebugLogging.Value}";
        }
    }
}