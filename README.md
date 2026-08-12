# 🐈‍⬛ BlackCatRabbitServer

> 基于 .NET 8 的游戏服务器端 / 通信中间件
b站视频地址：https://www.bilibili.com/video/BV1Y4uq6QEs2/?spm_id_from=333.1387.homepage.video_card.click
## 📖 项目简介

这是一个用 C# 开发的游戏服务器后端程序。主要负责处理 [玩家登录、数据持久化、战斗逻辑转发等]。项目基于 .NET 8 构建，利用 Protobuf 进行高效的数据序列化通信。

## ✨ 核心功能

- 🚀 **网络通信**：基于异步 Socket 。
- 📦 **Protobuf 协议**：使用 `Google.Protobuf` 进行消息编解码，保证跨语言兼容性。
- 🗄️ **数据持久化**：支持 MySQL 数据存储]。

## 📋 环境要求

在运行本项目之前，请确保你的机器上已安装以下环境：

- **[.NET 8 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0)** 或更高版本（必须）
- **Visual Studio 2022** （推荐，用于调试）
- **操作系统**：Windows

## 🚀 快速开始（本地运行）

按照以下步骤，你可以在 5 分钟内把服务器跑起来：

1. **克隆项目**
   ```bash
   git clone https://github.com/BlackCatRabbit/BlackCatRabbitServer.git
   cd BlackCatRabbitServer
