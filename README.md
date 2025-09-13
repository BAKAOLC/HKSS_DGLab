# HKSS_DGLab

Hollow Knight Silksong 的 DGLab 硬件集成插件，在游戏中受伤或死亡时触发相应的硬件反馈。

## 编译安装

1. 确保已安装 [BepInEx](https://github.com/BepInEx/BepInEx)
2. 克隆本仓库并编译项目
3. 将编译后的文件复制到 `BepInEx/plugins/HKSS_DGLab/` 目录
4. 启动游戏

## 使用

1. **启动游戏**：插件会自动在端口 9999 启动 DGLab 服务器
2. **获取二维码**：二维码会生成到 `AppData\LocalLow\Team Cherry\Hollow Knight Silksong\dglab_qr.png`
3. **连接设备**：使用 DGLab APP 扫描二维码或手动连接到服务器
4. **开始游戏**：受伤和死亡时会自动触发相应强度的硬件反馈

## 功能

- **受伤反馈**：根据伤害值发送不同强度的波形（1秒防抖）
- **死亡反馈**：死亡时发送5秒强烈波形
- **自动连接**：游戏启动时自动生成连接二维码

## 配置

插件会在 `BepInEx/config/` 目录下生成配置文件，可调整：
- 服务器端口
- 波形强度映射
- 调试选项

## 许可证

MIT License - Copyright (c) 2025 OLC
