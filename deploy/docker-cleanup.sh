#!/usr/bin/env bash
# Marine Insight 部署后磁盘空间优化脚本。
# 在 `docker compose up -d --build` 成功、站点冒烟验证通过后执行。
# 安全边界：不删除运行中容器、命名卷、正在使用的镜像与网络；
# 仅清理悬空镜像、无用构建缓存、已退出容器与无用网络。
set -euo pipefail

echo "[1/4] 清理悬空镜像（dangling images）"
docker image prune -f

echo "[2/4] 清理无用构建缓存（build cache）"
docker builder prune -f

echo "[3/4] 清理已退出容器（exited containers）"
docker container prune -f

echo "[4/4] 清理无用网络（unused networks）"
docker network prune -f

echo "清理完成，当前磁盘占用："
docker system df
