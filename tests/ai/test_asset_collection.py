"""
测试资产采集脚本

使用Mock Provider采集各种场景的trace数据，生成测试资产。
这些资产可用于：
1. 开发调试 - 提供标准响应数据
2. 性能基准 - 建立性能基线
3. 监控验证 - 验证监控系统的正确性
4. 文档示例 - 提供真实的API调用示例
"""

import asyncio
import json
import logging
from pathlib import Path
from typing import Dict, List, Any
from dataclasses import dataclass, asdict
from datetime import datetime
import time

# 设置日志
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


@dataclass
class MockTraceAsset:
    """Mock测试资产数据结构"""
    asset_id: str
    scenario: str
    capability: str
    provider_id: str
    mode: str

    # 输入数据
    input_data: Dict[str, Any]

    # 输出数据
    output_data: Dict[str, Any]

    # 性能数据
    latency_ms: float
    input_tokens: int
    output_tokens: int
    total_tokens: int

    # 追踪数据
    trace_context: Dict[str, Any]
    custom_context: Dict[str, Any]

    # 元数据
    created_at: str
    tags: List[str]
    description: str


class MockTraceCollector:
    """Mock测试资产采集器"""

    def __init__(self, output_dir: str = "tests/ai/assets/traces"):
        self.output_dir = Path(output_dir)
        self.output_dir.mkdir(parents=True, exist_ok=True)
        self.collected_assets: List[MockTraceAsset] = []

    async def collect_scenario(self, scenario_name: str, mock_calls: List[Dict]) -> List[MockTraceAsset]:
        """采集一个场景的trace数据

        Args:
            scenario_name: 场景名称
            mock_calls: Mock调用列表，每个包含配置信息

        Returns:
            采集到的资产列表
        """
        logger.info(f"开始采集场景: {scenario_name}")
        assets = []

        for i, call_config in enumerate(mock_calls):
            try:
                asset = await self._collect_single_call(
                    scenario_name=scenario_name,
                    call_index=i,
                    call_config=call_config
                )
                assets.append(asset)
                logger.info(f"  ✓ 采集调用 {i+1}/{len(mock_calls)}: {call_config['capability']}")

            except Exception as e:
                logger.error(f"  ✗ 采集调用 {i+1} 失败: {e}")

        self.collected_assets.extend(assets)
        logger.info(f"场景 {scenario_name} 采集完成: {len(assets)} 个资产")
        return assets

    async def _collect_single_call(
        self,
        scenario_name: str,
        call_index: int,
        call_config: Dict
    ) -> MockTraceAsset:
        """采集单个调用的trace数据"""

        # 模拟API调用延迟
        latency_ms = call_config.get('latency_ms', 100.0)
        await asyncio.sleep(latency_ms / 1000.0)

        # 模拟token消耗
        input_tokens = call_config.get('input_tokens', 500)
        output_tokens = call_config.get('output_tokens', 300)

        # 生成trace上下文
        trace_context = {
            'span_id': f"{call_config['capability']}_{call_index}_{int(time.time()*1000)}",
            'parent_span_id': call_config.get('parent_span_id'),
            'operation': f"unibrain.{call_config['capability']}",
            'tags': {
                'capability': call_config['capability'],
                'provider_id': call_config['provider_id'],
                'mode': call_config['mode'],
                'scenario': scenario_name,
            }
        }

        # 自定义业务上下文
        custom_context = call_config.get('custom_context', {})

        # 创建资产
        asset = MockTraceAsset(
            asset_id=f"{scenario_name}_{call_config['capability']}_{call_index}",
            scenario=scenario_name,
            capability=call_config['capability'],
            provider_id=call_config['provider_id'],
            mode=call_config['mode'],
            input_data=call_config.get('input_data', {}),
            output_data=call_config.get('output_data', {}),
            latency_ms=latency_ms,
            input_tokens=input_tokens,
            output_tokens=output_tokens,
            total_tokens=input_tokens + output_tokens,
            trace_context=trace_context,
            custom_context=custom_context,
            created_at=datetime.now().isoformat(),
            tags=call_config.get('tags', []),
            description=call_config.get('description', '')
        )

        return asset

    def save_assets(self, format: str = 'json'):
        """保存采集到的资产

        Args:
            format: 保存格式 ('json' 或 'yaml')
        """
        logger.info(f"保存 {len(self.collected_assets)} 个资产到 {self.output_dir}")

        # 按场景分组保存
        scenarios = {}
        for asset in self.collected_assets:
            if asset.scenario not in scenarios:
                scenarios[asset.scenario] = []
            scenarios[asset.scenario].append(asset)

        # 保存每个场景
        for scenario_name, assets in scenarios.items():
            filename = self.output_dir / f"{scenario_name}.{format}"

            if format == 'json':
                data = [asdict(asset) for asset in assets]
                with open(filename, 'w', encoding='utf-8') as f:
                    json.dump(data, f, indent=2, ensure_ascii=False)

            logger.info(f"  ✓ 保存场景: {filename.name} ({len(assets)} 个资产)")

        # 保存总览
        overview_file = self.output_dir / f"overview.{format}"
        overview_data = {
            'total_assets': len(self.collected_assets),
            'scenarios': {name: len(assets) for name, assets in scenarios.items()},
            'created_at': datetime.now().isoformat(),
            'assets_summary': self._generate_summary()
        }

        with open(overview_file, 'w', encoding='utf-8') as f:
            json.dump(overview_data, f, indent=2, ensure_ascii=False)

        logger.info(f"  ✓ 保存总览: {overview_file.name}")

    def _generate_summary(self) -> Dict[str, Any]:
        """生成资产汇总统计"""
        if not self.collected_assets:
            return {}

        # 按capability统计
        capability_stats = {}
        provider_stats = {}
        mode_stats = {}

        total_latency = 0
        total_input_tokens = 0
        total_output_tokens = 0

        for asset in self.collected_assets:
            # Capability统计
            if asset.capability not in capability_stats:
                capability_stats[asset.capability] = {
                    'count': 0,
                    'total_latency': 0,
                    'total_tokens': 0
                }
            capability_stats[asset.capability]['count'] += 1
            capability_stats[asset.capability]['total_latency'] += asset.latency_ms
            capability_stats[asset.capability]['total_tokens'] += asset.total_tokens

            # Provider统计
            if asset.provider_id not in provider_stats:
                provider_stats[asset.provider_id] = {'count': 0, 'total_tokens': 0}
            provider_stats[asset.provider_id]['count'] += 1
            provider_stats[asset.provider_id]['total_tokens'] += asset.total_tokens

            # Mode统计
            if asset.mode not in mode_stats:
                mode_stats[asset.mode] = {'count': 0, 'total_tokens': 0}
            mode_stats[asset.mode]['count'] += 1
            mode_stats[asset.mode]['total_tokens'] += asset.total_tokens

            # 总计
            total_latency += asset.latency_ms
            total_input_tokens += asset.input_tokens
            total_output_tokens += asset.output_tokens

        return {
            'capability_stats': capability_stats,
            'provider_stats': provider_stats,
            'mode_stats': mode_stats,
            'averages': {
                'avg_latency_ms': total_latency / len(self.collected_assets),
                'avg_input_tokens': total_input_tokens / len(self.collected_assets),
                'avg_output_tokens': total_output_tokens / len(self.collected_assets),
                'avg_total_tokens': (total_input_tokens + total_output_tokens) / len(self.collected_assets)
            }
        }


async def collect_standard_scenarios():
    """采集标准测试场景"""

    collector = MockTraceCollector()

    # 场景1: 视觉分析 - 标准设置页面
    logger.info("=" * 60)
    logger.info("场景1: 视觉分析 - 标准设置页面")
    logger.info("=" * 60)

    vision_calls = [
        {
            'capability': 'analyze_visual',
            'provider_id': 'claude',
            'mode': 'vision',
            'latency_ms': 2500.0,
            'input_tokens': 1100,
            'output_tokens': 350,
            'input_data': {
                'image_size': '1080x1920',
                'image_format': 'PNG',
                'app_context': 'Settings main page'
            },
            'output_data': {
                'current_path': ['Home', 'Settings'],
                'page_type': 'menu_list',
                'elements': [
                    {'id': 1, 'name': 'WiFi', 'type': 'menu_item', 'bbox': {'x': 0.1, 'y': 0.2, 'w': 0.8, 'h': 0.1}},
                    {'id': 2, 'name': 'Bluetooth', 'type': 'menu_item', 'bbox': {'x': 0.1, 'y': 0.3, 'w': 0.8, 'h': 0.1}},
                    {'id': 3, 'name': 'Display', 'type': 'menu_item', 'bbox': {'x': 0.1, 'y': 0.4, 'w': 0.8, 'h': 0.1}}
                ],
                'confidence': 0.92
            },
            'custom_context': {
                'traversal_session': 'session_001',
                'current_depth': 2,
                'target_goal': 'configure_wifi'
            },
            'tags': ['vision', 'settings', 'menu_list'],
            'description': '分析设置主页的视觉结构'
        },
        {
            'capability': 'analyze_visual',
            'provider_id': 'claude',
            'mode': 'vision',
            'latency_ms': 2800.0,
            'input_tokens': 1200,
            'output_tokens': 400,
            'input_data': {
                'image_size': '1080x1920',
                'image_format': 'PNG',
                'app_context': 'WiFi settings page'
            },
            'output_data': {
                'current_path': ['Home', 'Settings', 'WiFi'],
                'page_type': 'settings_group',
                'elements': [
                    {'id': 1, 'name': 'WiFi Switch', 'type': 'switch', 'value': 'on'},
                    {'id': 2, 'name': 'Network Name', 'type': 'text', 'value': 'HomeNetwork'},
                    {'id': 3, 'name': 'Security', 'type': 'menu_item', 'value': 'WPA2'}
                ],
                'confidence': 0.88
            },
            'custom_context': {
                'traversal_session': 'session_001',
                'current_depth': 3,
                'target_goal': 'configure_wifi'
            },
            'tags': ['vision', 'settings', 'switch'],
            'description': '分析WiFi设置页面的开关和文本'
        }
    ]

    await collector.collect_scenario('vision_analysis_standard', vision_calls)

    # 场景2: 指令解析 - 自然语言指令
    logger.info("=" * 60)
    logger.info("场景2: 指令解析 - 自然语言指令")
    logger.info("=" * 60)

    instruction_calls = [
        {
            'capability': 'parse_instruction',
            'provider_id': 'deepseek',
            'mode': 'text',
            'latency_ms': 800.0,
            'input_tokens': 25,
            'output_tokens': 180,
            'input_data': {
                'instruction': 'Go to WiFi settings and enable WiFi',
                'app_context': 'Vehicle infotainment system'
            },
            'output_data': {
                'entry_app': 'Settings',
                'root_node': {
                    'node_id': 'root',
                    'name': 'Configure WiFi',
                    'operation': {'action': 'navigate', 'target': {'by': 'text', 'value': 'Settings'}},
                    'children_strategy': {'type': 'dynamic_match', 'expected_types': ['menu_list']}
                },
                'static_nodes': [
                    {
                        'node_id': 'wifi_settings',
                        'name': 'WiFi Settings',
                        'operation': {'action': 'click', 'target': {'by': 'text', 'value': 'WiFi'}},
                        'precondition': {'page_name': 'Connectivity', 'ui_condition': 'wifi_item_visible'}
                    }
                ],
                'mode': 'hybrid',
                'reasoning': 'User wants to access WiFi settings, which is typically under Settings > Network/Connectivity > WiFi',
                'confidence': 0.85
            },
            'custom_context': {
                'user_intent': 'configure_network',
                'complexity': 'medium',
                'requires_navigation': True
            },
            'tags': ['instruction', 'parse', 'wifi'],
            'description': '解析"进入WiFi设置并开启WiFi"指令'
        },
        {
            'capability': 'parse_instruction',
            'provider_id': 'deepseek',
            'mode': 'text',
            'latency_ms': 650.0,
            'input_tokens': 18,
            'output_tokens': 150,
            'input_data': {
                'instruction': 'Check Bluetooth status',
                'app_context': 'Vehicle infotainment system'
            },
            'output_data': {
                'entry_app': 'Settings',
                'root_node': {
                    'node_id': 'root',
                    'name': 'Check Bluetooth',
                    'operation': {'action': 'navigate', 'target': {'by': 'text', 'value': 'Settings'}},
                    'children_strategy': {'type': 'dynamic_match', 'expected_types': ['menu_list', 'settings_group']}
                },
                'static_nodes': [],
                'mode': 'dynamic',
                'reasoning': 'Simple status check, navigate to Settings and find Bluetooth',
                'confidence': 0.90
            },
            'custom_context': {
                'user_intent': 'check_status',
                'complexity': 'low',
                'requires_navigation': True
            },
            'tags': ['instruction', 'parse', 'bluetooth'],
            'description': '解析"检查蓝牙状态"指令'
        }
    ]

    await collector.collect_scenario('instruction_parse_standard', instruction_calls)

    # 场景3: 页面类型验证
    logger.info("=" * 60)
    logger.info("场景3: 页面类型验证")
    logger.info("=" * 60)

    verification_calls = [
        {
            'capability': 'verify_page_type',
            'provider_id': 'deepseek',
            'mode': 'text',
            'latency_ms': 500.0,
            'input_tokens': 200,
            'output_tokens': 80,
            'input_data': {
                'expected_type': 'settings_group',
                'page_analysis': {
                    'current_path': ['Settings', 'WiFi'],
                    'page_type': 'settings_group',
                    'elements': [{'name': 'WiFi Switch', 'type': 'switch'}]
                }
            },
            'output_data': {
                'is_correct': True,
                'confidence': 0.95,
                'reasoning': 'Page contains switch controls and settings elements, consistent with settings_group type',
                'actual_type': 'settings_group'
            },
            'custom_context': {
                'verification_purpose': 'confirm_navigation',
                'expected_behavior': 'settings_controls'
            },
            'tags': ['verification', 'page_type', 'settings'],
            'description': '验证页面是否为settings_group类型'
        }
    ]

    await collector.collect_scenario('page_verification', verification_calls)

    # 场景4: 决策能力
    logger.info("=" * 60)
    logger.info("场景4: 下一步决策")
    logger.info("=" * 60)

    decision_calls = [
        {
            'capability': 'decide_next_action',
            'provider_id': 'deepseek',
            'mode': 'text',
            'latency_ms': 900.0,
            'input_tokens': 350,
            'output_tokens': 200,
            'input_data': {
                'goal': 'Enable WiFi on the device',
                'page_analysis': {
                    'current_path': ['Settings', 'WiFi'],
                    'page_type': 'settings_group',
                    'elements': [
                        {'name': 'WiFi Switch', 'type': 'switch', 'value': 'off'},
                        {'name': 'Network Name', 'type': 'text'}
                    ]
                },
                'context': {
                    'current_path': ['Settings', 'WiFi'],
                    'visited_pages': ['Home', 'Settings']
                }
            },
            'output_data': {
                'action': {
                    'type': 'toggle',
                    'target': {'by': 'text', 'value': 'WiFi Switch'},
                    'expected_state': 'on'
                },
                'reasoning': 'WiFi is currently off, user wants to enable it. Click the WiFi switch to toggle it on.',
                'confidence': 0.92,
                'alternative_actions': []
            },
            'custom_context': {
                'decision_context': 'goal_achievement',
                'goal_progress': 'in_progress'
            },
            'tags': ['decision', 'toggle', 'wifi'],
            'description': '决策：如何开启WiFi'
        }
    ]

    await collector.collect_scenario('decision_making', decision_calls)

    # 场景5: 安全筛选
    logger.info("=" * 60)
    logger.info("场景5: 安全筛选")
    logger.info("=" * 60)

    safety_calls = [
        {
            'capability': 'screen_safety',
            'provider_id': 'deepseek',
            'mode': 'text',
            'latency_ms': 400.0,
            'input_tokens': 150,
            'output_tokens': 100,
            'input_data': {
                'instruction': 'Click on the confirm button',
                'page_elements': [
                    {'name': 'Confirm', 'type': 'button', 'text': 'Confirm'},
                    {'name': 'Cancel', 'type': 'button', 'text': 'Cancel'}
                ]
            },
            'output_data': {
                'is_safe': True,
                'confidence': 0.98,
                'risk_factors': [],
                'reasoning': 'Standard confirmation dialog with Confirm/Cancel buttons. No risky elements detected.',
                'recommendation': 'proceed'
            },
            'custom_context': {
                'safety_check': 'confirmation_dialog',
                'risk_tolerance': 'low'
            },
            'tags': ['safety', 'confirmation', 'low_risk'],
            'description': '安全检查：确认对话框'
        },
        {
            'capability': 'screen_safety',
            'provider_id': 'deepseek',
            'mode': 'text',
            'latency_ms': 450.0,
            'input_tokens': 180,
            'output_tokens': 120,
            'input_data': {
                'instruction': 'Factory reset the device',
                'page_elements': [
                    {'name': 'Factory Reset', 'type': 'button', 'text': 'Factory Reset'},
                    {'name': 'Warning: This action cannot be undone', 'type': 'warning'}
                ]
            },
            'output_data': {
                'is_safe': False,
                'confidence': 0.95,
                'risk_factors': [
                    {'type': 'destructive_action', 'severity': 'high', 'description': 'Factory reset will wipe all data'},
                    {'type': 'irreversible', 'severity': 'high', 'description': 'Cannot be undone'}
                ],
                'reasoning': 'Factory reset is a destructive action that will erase all user data and settings.',
                'recommendation': 'require_confirmation'
            },
            'custom_context': {
                'safety_check': 'destructive_action',
                'risk_tolerance': 'high'
            },
            'tags': ['safety', 'destructive', 'high_risk'],
            'description': '安全检查：恢复出厂设置'
        }
    ]

    await collector.collect_scenario('safety_screening', safety_calls)

    # 场景6: 错误处理
    logger.info("=" * 60)
    logger.info("场景6: 错误处理")
    logger.info("=" * 60)

    error_calls = [
        {
            'capability': 'analyze_visual',
            'provider_id': 'claude',
            'mode': 'vision',
            'latency_ms': 3000.0,
            'input_tokens': 1000,
            'output_tokens': 150,
            'input_data': {
                'image_size': '1080x1920',
                'image_format': 'PNG',
                'image_quality': 'poor'
            },
            'output_data': {
                'error': 'Low confidence due to poor image quality',
                'current_path': ['Unknown'],
                'page_type': 'unknown',
                'elements': [],
                'confidence': 0.45
            },
            'custom_context': {
                'error_context': 'low_quality_image',
                'fallback_needed': True
            },
            'tags': ['error', 'low_confidence', 'image_quality'],
            'description': '错误处理：低质量图片导致低置信度'
        },
        {
            'capability': 'parse_instruction',
            'provider_id': 'deepseek',
            'mode': 'text',
            'latency_ms': 100.0,
            'input_tokens': 5,
            'output_tokens': 50,
            'input_data': {
                'instruction': '',
                'app_context': 'Vehicle system'
            },
            'output_data': {
                'error': 'Empty instruction',
                'reasoning': 'No instruction provided to parse',
                'confidence': 0.0
            },
            'custom_context': {
                'error_context': 'invalid_input',
                'input_validation': 'failed'
            },
            'tags': ['error', 'empty_input', 'validation'],
            'description': '错误处理：空指令'
        }
    ]

    await collector.collect_scenario('error_handling', error_calls)

    # 保存所有资产
    logger.info("=" * 60)
    logger.info("保存测试资产")
    logger.info("=" * 60)

    collector.save_assets(format='json')

    # 输出统计信息
    logger.info("=" * 60)
    logger.info("测试资产采集统计")
    logger.info("=" * 60)

    summary = collector._generate_summary()
    print(json.dumps(summary, indent=2, ensure_ascii=False))

    logger.info(f"\n✅ 测试资产采集完成！")
    logger.info(f"📁 资产保存位置: {collector.output_dir}")
    logger.info(f"📊 总资产数: {len(collector.collected_assets)}")

    return collector


async def analyze_collected_assets():
    """分析已采集的资产"""

    assets_dir = Path("tests/ai/assets/traces")
    if not assets_dir.exists():
        logger.error("资产目录不存在，请先运行采集脚本")
        return

    logger.info("分析已采集的测试资产...")

    # 读取总览文件
    overview_file = assets_dir / "overview.json"
    if overview_file.exists():
        with open(overview_file, 'r', encoding='utf-8') as f:
            overview = json.load(f)

        print("\n" + "=" * 60)
        print("测试资产总览")
        print("=" * 60)
        print(json.dumps(overview, indent=2, ensure_ascii=False))

    # 读取各场景资产
    scenario_files = list(assets_dir.glob("*.json")) - {overview_file}

    print(f"\n发现 {len(scenario_files)} 个场景文件")

    for scenario_file in scenario_files:
        print(f"\n场景: {scenario_file.stem}")
        with open(scenario_file, 'r', encoding='utf-8') as f:
            assets = json.load(f)

        print(f"  资产数量: {len(assets)}")

        # 统计该场景的数据
        capabilities = {}
        providers = {}
        total_latency = 0
        total_tokens = 0

        for asset in assets:
            cap = asset['capability']
            prov = asset['provider_id']

            capabilities[cap] = capabilities.get(cap, 0) + 1
            providers[prov] = providers.get(prov, 0) + 1
            total_latency += asset['latency_ms']
            total_tokens += asset['total_tokens']

        print(f"  能力分布: {capabilities}")
        print(f"  Provider分布: {providers}")
        print(f"  平均延迟: {total_latency/len(assets):.1f}ms")
        print(f"  平均Token: {total_tokens/len(assets):.0f}")


if __name__ == "__main__":
    import sys

    if len(sys.argv) > 1 and sys.argv[1] == "analyze":
        # 分析模式
        asyncio.run(analyze_collected_assets())
    else:
        # 采集模式
        asyncio.run(collect_standard_scenarios())
