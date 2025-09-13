using System;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HKSS_DGLab
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Hollow Knight Silksong.exe")]
    public class Plugin : BaseUnityPlugin
    {
        internal new static ManualLogSource Logger;
        private readonly Harmony _harmony = new(MyPluginInfo.PLUGIN_GUID);

        private DGLabController _dgLabController;
        private GameEventConfig _gameEventConfig;
        private GameEventHandler _gameEventHandler;
        private bool _isInitialized;

        /// <summary>
        ///     获取插件实例（供其他模组使用）
        /// </summary>
        public static Plugin Instance { get; private set; }

        private void Awake()
        {
            // Plugin startup logic
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

            try
            {
                // 初始化配置
                _gameEventConfig = new(Config, Logger);

                // 验证配置
                if (!_gameEventConfig.ValidateConfig()) Logger.LogWarning("配置验证失败，某些功能可能无法正常工作");

                Logger.LogInfo($"配置摘要: {_gameEventConfig.GetConfigSummary()}");

                // 如果插件被禁用，则不初始化其他组件
                if (!_gameEventConfig.EnablePlugin.Value)
                {
                    Logger.LogInfo("插件已在配置中禁用");
                    return;
                }

                // 应用Harmony补丁
                _harmony.PatchAll();
                Logger.LogInfo("Harmony补丁已应用");

                // 异步初始化DGLab组件
                _ = Task.Run(InitializeDGLabAsync);
            }
            catch (Exception ex)
            {
                Logger.LogError($"插件初始化失败: {ex.Message}");
            }
        }

        private void Start()
        {
            Instance = this;
        }

        private void Update()
        {
            // 处理键盘输入进行测试（仅在调试模式下）
            if (_isInitialized && (_gameEventConfig?.EnableDebugLogging.Value ?? false)) HandleDebugInput();
        }

        private void OnDestroy()
        {
            try
            {
                Logger.LogInfo("正在清理DGLab插件资源...");

                // 清理游戏事件处理器
                _gameEventHandler?.Dispose();

                // 清理DGLab控制器
                _dgLabController?.Dispose();

                // 移除Harmony补丁
                _harmony?.UnpatchSelf();

                _isInitialized = false;
                Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is unloaded!");
            }
            catch (Exception ex)
            {
                Logger.LogError($"清理插件资源时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        ///     异步初始化DGLab组件
        /// </summary>
        private async Task InitializeDGLabAsync()
        {
            try
            {
                Logger.LogInfo("开始初始化DGLab组件...");

                // 创建DGLab控制器
                _dgLabController = new(Logger);

                // 初始化DGLab服务器
                var success = await _dgLabController.InitializeAsync(_gameEventConfig.ServerPort.Value);
                if (!success)
                {
                    Logger.LogError("DGLab控制器初始化失败");
                    return;
                }

                // 创建游戏事件处理器
                _gameEventHandler = new(Logger, _dgLabController, _gameEventConfig);
                _gameEventHandler.Initialize();

                _isInitialized = true;
                Logger.LogInfo("DGLab插件初始化完成！");
                Logger.LogInfo("请使用DGLab APP扫描二维码或手动连接到服务器");

                // 显示连接状态
                Logger.LogInfo($"服务器状态: {_dgLabController.GetStatusInfo()}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"DGLab组件初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     处理调试输入
        /// </summary>
        private void HandleDebugInput()
        {
            try
            {
                // 测试按键（需要在游戏中按下）
                if (Input.GetKeyDown(KeyCode.F1))
                {
                    Logger.LogInfo("F1 - 触发受伤测试 (1点伤害)");
                    _gameEventHandler?.OnPlayerTakeDamage(1);
                }
                else if (Input.GetKeyDown(KeyCode.F9))
                {
                    Logger.LogInfo("F9 - 紧急停止所有输出");
                    _ = Task.Run(() => _dgLabController?.EmergencyStopAsync());
                }
                else if (Input.GetKeyDown(KeyCode.F10))
                {
                    Logger.LogInfo("F10 - 显示连接状态");
                    if (_dgLabController == null) return;
                    Logger.LogInfo($"连接状态: {_dgLabController.GetStatusInfo()}");
                    Logger.LogInfo($"已连接APP: {(_dgLabController.HasConnectedApps ? "是" : "否")}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理调试输入时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        ///     获取DGLab控制器（供其他模组使用）
        /// </summary>
        public DGLabController GetDGLabController()
        {
            return _dgLabController;
        }

        /// <summary>
        ///     获取游戏事件处理器（供其他模组使用）
        /// </summary>
        public GameEventHandler GetGameEventHandler()
        {
            return _gameEventHandler;
        }

        /// <summary>
        ///     获取配置（供其他模组使用）
        /// </summary>
        public GameEventConfig GetConfig()
        {
            return _gameEventConfig;
        }

        /// <summary>
        ///     获取二维码文件路径（供游戏内显示使用）
        /// </summary>
        public string GetQRCodePath()
        {
            return _dgLabController?.QRCodePath ?? "";
        }

        /// <summary>
        ///     检查DGLab是否已初始化并有连接的APP
        /// </summary>
        public bool IsReady()
        {
            return _isInitialized && (_dgLabController?.HasConnectedApps ?? false);
        }
    }
}