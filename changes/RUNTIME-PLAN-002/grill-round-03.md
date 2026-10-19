# Grill 第3轮：遍历图与世界图的关系

日期：2026-10-19。状态：B的引用/派生语义已澄清，Owner指示“下一步”，据此按B继续；Q4见第4轮。

## 已定前提

- D1：图只描述结构，任务进度独立。
- D2：保留当前任务范围内累计已观察、有依据的结构，当前有效性随新观察更新；历史定位不直接成为可执行目标。
- Owner要求先确立遍历图与世界图的关系；既有架构规定World拥有世界belief，Traversal不拥有Belief/Goal/Completion。

## 两种图的关系对照稿

世界图指World拥有的、带依据与版本的实体关系belief，不能因名称把它理解为完整、永远正确的环境地图；元素和状态还需World其他有依据投影。遍历图不能通过重命名复制World事实权威。

推荐关系：World持有页面身份及有依据关系；遍历图按任务范围，从这些结构事实派生为Traversal使用的视图。筛选或整理表达不新增事实。World对某关系作更正/否定时，遍历图必须反映变化，不能以“累计已知”为由继续当作当前成立；保留的旧依据只能按历史使用。任务处理进度依D1独立。

“派生视图”只确定语义关系，不决定即时计算、缓存或物理存储。若现有World投影不能提供必要事实，须明确补该消费缝，不能让Traversal直接从raw observation建立另一套世界事实。

## 当前前沿 Q3

- A：Traversal直接消费世界图，任务范围筛选由遍历算法承担；“遍历图”只是使用中的称呼。
- B：世界图作为结构事实源，另派生任务范围的遍历视图，Traversal消费后者；“遍历图”是这个视图。

推荐B：保留单一事实来源，让Traversal只接触任务需要的结构表达，不强迫它理解World内部完整记录形状。视图可重建，身份与依据沿用World；任务进度、安全许可和终局证明不进入图。

例如同一“设置→网络”关系由World确认，任务甲/乙的遍历视图都引用这段事实，各自任务进度独立；不能在某个视图中把它改成“设置→蓝牙”后反写世界事实。

## 环境事实核对

按grilling的事实调查要求，由只读explorer核对当前dirty checkout，未修改文件、未运行测试：

1. WorldBeliefRevision.WorldGraph实际为subject字符串索引，reconcile追加claim subject，并非页面导航拓扑。不能因同名就把这个字段直接当Traversal Graph。（WorldBeliefRevision.cs:83-96；WorldModel.cs:406-429、475-484。）
2. 结构事实实际由Containers/Relations提供，后者包含Contains/Overlays/NavigatesTo，均有revision及evidence语义；CurrentContainer属于状态事实。（ContainerAssociation.cs:5-50；WorldModel.cs:502-507。）
3. 现有WorldGraphProjection/ProjectWorldGraph(scope)是可复用的navigation-only只读消费缝：边按NavigatesTo与scope筛选，节点仍返回该revision全部Containers，anchor取CurrentContainer。它尚不是完整的任务范围Traversal Graph。（WorldModel.cs:203-235；UniKernel.cs:113-118；WorldObservedNavigationTests.cs:53-86。）
4. 现有projection没有freshness/evidence-validity字段，派生过程未计算节点/边失效传播；revision的FreshnessBasis只是输入表达，不能据此宣布D2所需当前有效性传播已经闭合。（WorldBeliefRevision.cs:16-22、98-105；WorldModel.cs:218-234。）

因此本轮“世界图”采用架构语义，指World的有依据结构关系，必要节点状态来自同一World事实源；不是对WorldGraph同名索引的重新定义。推荐复用已有投影缝并明确缺口，而非另维护结构真值。Q3只定消费关系，不提前决定代码改法。

代码入口：[WorldBeliefRevision](../../src/UniClaw.Kernel/World/WorldBeliefRevision.cs)、[WorldModel](../../src/UniClaw.Kernel/World/WorldModel.cs)、[ContainerAssociation](../../src/UniClaw.Kernel/World/UiRealization/ContainerAssociation.cs)、[UniKernel](../../src/UniClaw.Kernel/UniKernel.cs)、[现有导航测试](../../tests/UniClaw.Kernel.Tests/World/WorldObservedNavigationTests.cs)。

## 答复

Owner先询问“B的意思是遍历图从世界图摘取引用对吗”，答复明确为：从World中摘取任务需要的结构引用，再按遍历需要组织；身份、事实和依据仍由World维护，事实更新/失效须反映，查询或缓存后定。Owner随后指示“下一步”。据此记录D3并按B继续，不把该答复扩大解释为实现授权。Q4转入执行输入边界。
