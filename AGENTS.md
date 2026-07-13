# Marine Insight Agent 执行入口

## 强制启动流程

每次处理本仓库任务前，必须先完整读取 [`docs/AGENT-GUIDE.md`](./docs/AGENT-GUIDE.md)，并按其中的“会话启动协议、文档路由、任务状态、任务恢复、会话收尾协议”执行。

- 当前用户的最新明确指令优先级最高。
- 若台账存在 `IN_PROGRESS`、`PAUSED` 或 `BLOCKED` 任务，先判断本次指令是否要求继续该任务；匹配时必须从记录的恢复点继续，不得重复已经完成的工作。
- 开始修改前登记任务状态；结束会话前更新任务状态、恢复点、验证结果和会话记录。
- 不得覆盖或回退用户已有的未提交改动；先检查工作区并在现状上继续。

## 文档与代码发现

- 文档导航以 [`docs/README.md`](./docs/README.md) 为准，只读取与当前任务相关的设计文档。
- 若 `codebase-memory-mcp` 可用，代码发现优先使用 `search_graph`、`trace_path`、`get_code_snippet`、`query_graph`、`get_architecture`。
- 字符串、配置和非代码文件搜索，或图谱信息不足时，再使用 `rg` / `rg --files`。

## 文件规范

- 本次新增或修改的文本文件，除 JSON、YAML 等现代配置文件外，收尾前统一为 UTF-8 with BOM 和 CRLF。
- 在关键业务规则、第三方差异、单位/时间语义和降级逻辑处添加必要注释；简单逻辑不添加复述代码的注释。

