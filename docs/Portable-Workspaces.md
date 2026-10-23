# 便携模式与游戏目录

将客户端放在整合包根目录，例如：

```text
我的整合包/
  PIgHeAD-v0.2.2-beta-win-x64.exe
  .minecraft/
    versions/
    libraries/
    assets/
  java21/
    bin/javaw.exe
  PHL/                         首次打开自动创建
    consent.json               本位置的协议记录
    launcher.json              最后使用的配置
    library.json               目录列表和当前选择
    settings.json              此游戏的设置
    java-path.json             本整合包内 Java 的相对路径
    auth/                      DPAPI 加密的 Microsoft 缓存
    logs/                      此游戏的启动日志
    imports/                   ZIP / mrpack 解压后的独立整合包
```

不必同时携带两个版本的客户端。若电脑安装了 .NET 10 Desktop Runtime x64，可以使用文件名带 `-lite.exe` 的精简版；完整版自带 .NET，体积更大。完整版开启程序集压缩；依赖框架的精简版不支持 SDK 单文件压缩，以省去整套 .NET 运行时来缩小体积。两者保留中英文资源，不裁剪 WPF 或认证依赖以免破坏反射和界面加载。内置的本机库自动缓存到用户临时目录 `.net`，游戏、Java、模组和资源不打进启动器 EXE。

## 首次启动与 Java

首次打开会创建 EXE 旁的 PHL 并要求阅读协议。没有本位置的目录选择记录时，优先识别旁边的 `.minecraft`；也支持 EXE 直接位于包含 versions 的游戏根目录。不会因为旧版 AppData 中选择过别的整合包，就忽略旁边的游戏。没有本地游戏时默认使用旁边的 `.minecraft`。旧配置中的非路径偏好可在没有本地配置时读取，旧目录及认证缓存不会自动搬过来。

Java 路径留空时，会查找游戏和启动器同级的 java / java21 / jdk / jre / runtime 等目录（含一层嵌套的 JDK），依据 release 文件筛选，优先匹配游戏需要的主版本，再使用已安装的运行时、JAVA_HOME 或 PATH。真正启动前仍执行 `java -version` 验证；目录名字不代替实际版本检查。显式填写的 Java 路径优先使用。

放进完整游戏目录后仍需要在 Home 选择身份并点击 Launch Game。缺失的核心、依赖、资源可使用下载或修复补全；不存在元数据的模组不能凭空恢复。Microsoft API 审批限制仍然存在，便携功能不改变正版认证要求。

## 导入与切换

在 Versions 选择“接入游戏目录”，或拖入文件夹、ZIP / mrpack。导入成功后自动加入顶部“游戏文件夹”列表。选择目标目录，再点击“切换文件夹”：读取该游戏 PHL 中的设置，恢复其内存、参数、首选版本，刷新本地版本列表。目录首次登记会建立自己的 PHL；已有设置会保留。切换会放弃当前设置页的未保存草稿，编辑完成后请先保存。

当游戏运行、下载、导入、修复或认证正在进行时，禁止切换目录。目录已被移动或删除时提示重新接入，不删除目录记录，也不将本次切换当作成功。只有一份正在运行的启动器负责写同一 PHL；不要同时用多个启动器修改同一游戏。

当选中 `.minecraft` 时，数据目录是与其并列的 `PHL`；当选中的目录本身直接包含 versions 时，数据目录是其内部的 `PHL`。每个整合包的设置、日志彼此独立；认证缓存与协议属于启动器位置，不会随下拉框切换而复制到其他包。

## 移动与备份

要迁移整个整合包，请同时移动 EXE、`.minecraft`、本地 Java 和 PHL。目录列表中位于启动器目录内的路径以相对路径存储，本地 Java 也保留相对位置记录，整体移动后重新解析；指向其他磁盘、其他目录的游戏或 Java 仍使用绝对路径，需要重新接入或更改设置。单独复制 EXE 到另一个位置会建立该位置自己的 PHL。

这些目录必须允许当前用户写入。不要将可写的整合包放在受保护的系统目录；权限不足时会报告失败，不会悄悄保存到另一处。旧版 `%LOCALAPPDATA%\PIgHeAD\Launcher` 的文件保留，不自动删除。

PHL 可能含有认证缓存、个人路径、协议记录和游戏日志。分享整合包前移除 `PHL/auth`、个人日志与 `consent.json`，不要把自己的登录态分发给别人。DPAPI 缓存只能由原 Windows 用户解密，跨电脑或用户复制后应重新登录；没有明文回退。

## 重建

```powershell
# 完整单文件，包含 .NET
powershell -File tools/Publish-Release.ps1 -SingleFile -OutputDirectory artifacts\portable-full

# 精简单文件，需要 .NET 10 Desktop Runtime x64
powershell -File tools/Publish-Release.ps1 -SingleFile -FrameworkDependent -OutputDirectory artifacts\portable-lite
```

每次使用新的 artifacts 子目录，避免混入旧文件；只分发输出根目录的 EXE 和可选校验文件。准备许可证所用的普通发布目录是构建中间文件，无需携带。发布目标固定 Windows x64。
