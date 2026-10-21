# Profile v2及职责唯一源

本Change将产品Profile文件从`uniagent.profile/v1`升级为`uniagent.profile/v2`；这是装配文件的Schema版本，产品协议身份`profileVersion: 1`不变。v1不再接受，不能靠缺省或隐式职责继续运行。

迁移：在Profile增加`charter`，声明稳定的`role`以及非空列表`responsibilities`、`boundaries`、`planningPrinciples`、`skillUse`。`modelRoles`仍表示宿主可映射的逻辑模型槽位，不能当Agent职责。charter不授予权限；Task和原Owner继续决定目标、授权、安全与效果。

职责只在charter维护。identity Prompt为其确定性投影；修改声明后先重新生成identity，再更新Prompt revision/hash及Profile revision/引用，保持插件镜像一致。运行装配同时检查hash与charter投影，重算hash不能掩盖职责不一致。领域操作步骤在Product Skill；精确载荷在正式Schema及其指导工件。Profile不承载UI SOP、具体provider/model、端点、当前任务状态或整份能力Schema。

旧部署迁移后重启Host.Dsh及拥有的专用DSH 3081实例，使进程级钉扎装配重载。未经验证的旧Profile或旧Prompt不能降级继续。工具入口及实际指纹在实现后证据中记录。

实际生成入口：`python3 tools/render-profile-identity.py product/profiles/uniagent-prod.yaml product/prompt/uniagent-prod/01-identity.txt`。当前装配为Profile revision25、Prompt revision24、Skill revision4；这些是本Change工件标识，不是modelRoles的语义角色版本。
