# HyperboreaCN

以相对安全的方式加载并探索任意区域，主要用于在原本难以抵达的地点拍摄 GPose。

![Hyperborea UI preview](https://github.com/kawaii/Hyperborea/assets/12242877/54b5588b-9dd7-4b5d-8238-710aae65cf68)

HyperboreaCN 在启用时会使用数据包过滤的方式工作。除了维持与游戏服务器连接、让角色仍然保持在线状态所需的“保活”数据包之外，其余内容都会被过滤掉，例如移动、聊天、技能动作等。对其他玩家来说，你看起来只会像是站在原地待机或 AFK。

[![Hyperborea v1.0.0.4](https://markdown-videos-api.jorgenkh.no/url?url=https%3A%2F%2Fyoutu.be%2FxpX5UT7vSE0)](https://youtu.be/xpX5UT7vSE0)
[![Hyperborea v1.0.0.0](https://markdown-videos-api.jorgenkh.no/url?url=https%3A%2F%2Fyoutu.be%2FTilHuQlsNg4)](https://youtu.be/TilHuQlsNg4)

## 下载与安装

1. 打开 Dalamud 设置，可以在插件安装器底部点击按钮，或者在聊天框输入 `/xlsettings`。
2. 在 `Experimental` 标签页的 `Custom Plugin Repositories` 中，填入：
   `https://raw.githubusercontent.com/endfish/DalamudPlugins/main/repo.json`
3. 点击 `+` 按钮。
4. 保存并关闭设置窗口。
5. 打开插件安装器，在 `All Plugins` 中搜索 `HyperboreaCN`。

## 使用命令
/hyper 打开主界面
/hyper settings 打开设置
/hyper debug 打开调试
/hyper editor 打开编辑器
/hyper log 打开日志。

## 注意事项

- 很多地图如果直接使用默认的 `0,0` 坐标进入，会把角色刷到地图模型下方。插件内置了覆盖出生点和传送的小工具，可以稍微缓解这个问题。
- 试图步行穿过地图切换边界时，例如主城里一排蓝色光球那种换图边界，游戏可能会直接卡死，需要重启。
- 某些地图，尤其是副本和讨伐战区域，可能会出现内容不完整、场景异常或事件缺失。游戏本身会依赖各种事件、演出和场景切换技巧来驱动这些区域，目前插件还不能完整模拟全部行为。

## English

Load and explore any zone in relative safety, mainly for taking GPoses in now inaccessible locations.

HyperboreaCN operates by employing a packet filter while enabled. Only packets required to maintain your connection to the game server in such a way that you still appear online are passed through (known as "keepalive" packets). Anything else, such as movement, chat, and actions, is filtered from your client. To any observer you would just appear to be idle or AFK.

## Download & Install

1. Open the Dalamud Settings menu, either from the button at the bottom of the plugin installer or by typing `/xlsettings` in chat.
2. Under `Custom Plugin Repositories` in the `Experimental` tab, add:
   `https://puni.sh/api/repository/kawaii`
3. Click the `+` button.
4. Save and close the settings menu.
5. Search for `HyperboreaCN` in the `All Plugins` section of the Plugin Installer.

## Warnings

- Many zones will spawn you under the world geometry if you use the default `0,0` coordinates. There is a built-in feature to override your position and teleport around to alleviate this somewhat.
- Attempting to walk through a zone loading border, such as the string of blue orbs seen in city states that take you from one zone to the next, can softlock your game and require a restart.
- Some zones, particularly raids and trials, may appear broken or incomplete. The game uses various events, scenes, and other tricks to accomplish transitions and similar behavior, and the plugin cannot fully reproduce all of that yet.
