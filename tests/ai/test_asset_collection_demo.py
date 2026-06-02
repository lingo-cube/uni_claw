"""
Test Asset Collection Script - Enhanced Version

Add real-time observability to show trace simulation waiting and analysis effects.
"""

import asyncio
import json
import logging
from pathlib import Path
from typing import Dict, List, Any
from dataclasses import dataclass, asdict
from datetime import datetime
import time
from collections import defaultdict

# Setup detailed logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)


@dataclass
class MockTraceAsset:
    """Mock test asset data structure"""
    asset_id: str
    scenario: str
    capability: str
    provider_id: str
    mode: str

    # Input data
    input_data: Dict[str, Any]

    # Output data
    output_data: Dict[str, Any]

    # Performance data
    latency_ms: float
    input_tokens: int
    output_tokens: int
    total_tokens: int

    # Trace data
    trace_context: Dict[str, Any]
    custom_context: Dict[str, Any]

    # Metadata
    created_at: str
    tags: List[str]
    description: str


class MockTraceCollector:
    """Mock test asset collector - Enhanced version"""

    def __init__(self, output_dir: str = "tests/ai/assets/traces", verbose: bool = True):
        self.output_dir = Path(output_dir)
        self.output_dir.mkdir(parents=True, exist_ok=True)
        self.collected_assets: List[MockTraceAsset] = []
        self.verbose = verbose

        # Real-time statistics
        self.scenario_stats = defaultdict(lambda: {
            'total_calls': 0,
            'total_latency': 0.0,
            'total_tokens': 0,
            'by_capability': defaultdict(int),
            'by_provider': defaultdict(int)
        })

    def _print_progress(self, message: str, level: str = "INFO"):
        """Print progress information"""
        if self.verbose:
            timestamp = datetime.now().strftime("%H:%M:%S.%f")[:-3]
            print(f"[{timestamp}] {level}: {message}")

    def _print_trace_simulation(self, asset: MockTraceAsset):
        """Print trace simulation details"""
        print("\n" + "=" * 70)
        print(f"[TRACE] SIMULATION: {asset.asset_id}")
        print("=" * 70)

        print(f"[SCENARIO] {asset.scenario}")
        print(f"[CAPABILITY] {asset.capability}")
        print(f"[PROVIDER] {asset.provider_id} ({asset.mode} mode)")

        print(f"\n[PERFORMANCE]")
        print(f"   Latency: {asset.latency_ms:.1f}ms")
        print(f"   Tokens: {asset.input_tokens} + {asset.output_tokens} = {asset.total_tokens}")

        print(f"\n[INPUT DATA]")
        input_preview = json.dumps(asset.input_data, indent=2, ensure_ascii=False)[:200]
        print(f"   {input_preview}...")

        print(f"\n[OUTPUT DATA]")
        if isinstance(asset.output_data, dict) and 'error' in asset.output_data:
            print(f"   [ERROR] {asset.output_data.get('error', 'Unknown error')}")
        else:
            output_preview = str(asset.output_data)[:200]
            print(f"   [SUCCESS] {output_preview}...")

        print(f"\n[TRACE CONTEXT]")
        print(f"   Span ID: {asset.trace_context['span_id']}")
        print(f"   Operation: {asset.trace_context['operation']}")
        print(f"   Tags: {list(asset.trace_context['tags'].keys())}")

        print(f"\n[CUSTOM CONTEXT]")
        custom_context_str = json.dumps(asset.custom_context, indent=2, ensure_ascii=False)
        print(f"   {custom_context_str}")

        print(f"\n[TAGS] {', '.join(asset.tags)}")
        print(f"[DESCRIPTION] {asset.description}")

        print("=" * 70)

    async def collect_scenario(self, scenario_name: str, mock_calls: List[Dict]) -> List[MockTraceAsset]:
        """Collect trace data for a scenario - Enhanced version"""

        self._print_progress(f"\n{'='*70}", "INFO")
        self._print_progress(f"[SCENARIO] Starting collection: {scenario_name}", "INFO")
        self._print_progress(f"{'='*70}", "INFO")

        assets = []
        scenario_stats = {
            'start_time': time.time(),
            'calls': []
        }

        for i, call_config in enumerate(mock_calls):
            call_start = time.time()

            # Show call start
            self._print_progress(f"\n[{i+1}/{len(mock_calls)}] Preparing call: {call_config['capability']}", "INFO")

            try:
                # Simulate API call waiting
                latency_ms = call_config.get('latency_ms', 100.0)

                # Show waiting process
                self._print_progress(f"[SIMULATING] API call delay: {latency_ms}ms...", "INFO")
                await asyncio.sleep(latency_ms / 1000.0)  # Convert to seconds

                # Collect asset
                asset = await self._collect_single_call(
                    scenario_name=scenario_name,
                    call_index=i,
                    call_config=call_config
                )

                assets.append(asset)

                # Show trace simulation details
                self._print_trace_simulation(asset)

                # Update statistics
                self.scenario_stats[scenario_name]['total_calls'] += 1
                self.scenario_stats[scenario_name]['total_latency'] += asset.latency_ms
                self.scenario_stats[scenario_name]['total_tokens'] += asset.total_tokens
                self.scenario_stats[scenario_name]['by_capability'][asset.capability] += 1
                self.scenario_stats[scenario_name]['by_provider'][asset.provider_id] += 1

                call_duration = (time.time() - call_start) * 1000
                scenario_stats['calls'].append(call_duration)

                self._print_progress(f"[SUCCESS] Call completed (actual time: {call_duration:.1f}ms)", "SUCCESS")

            except Exception as e:
                self._print_progress(f"[ERROR] Call failed: {e}", "ERROR")

        # Scenario statistics
        total_duration = (time.time() - scenario_stats['start_time']) * 1000
        avg_latency = self.scenario_stats[scenario_name]['total_latency'] / len(assets) if assets else 0
        avg_tokens = self.scenario_stats[scenario_name]['total_tokens'] / len(assets) if assets else 0

        print(f"\n[SCENARIO STATISTICS] {scenario_name}")
        print(f"   Total calls: {len(assets)}")
        print(f"   Total duration: {total_duration:.1f}ms")
        print(f"   Average latency: {avg_latency:.1f}ms")
        print(f"   Average tokens: {avg_tokens:.0f}")

        self.collected_assets.extend(assets)
        return assets

    async def _collect_single_call(
        self,
        scenario_name: str,
        call_index: int,
        call_config: Dict
    ) -> MockTraceAsset:
        """Collect trace data for a single call"""

        latency_ms = call_config.get('latency_ms', 100.0)
        input_tokens = call_config.get('input_tokens', 500)
        output_tokens = call_config.get('output_tokens', 300)

        # Generate trace context
        trace_context = {
            'span_id': f"{call_config['capability']}_{call_index}_{int(time.time()*1000)}",
            'parent_span_id': call_config.get('parent_span_id'),
            'operation': f"unibrain.{call_config['capability']}",
            'tags': {
                'capability': call_config['capability'],
                'provider_id': call_config['provider_id'],
                'mode': call_config['mode'],
                'scenario': scenario_name,
                'timestamp': datetime.now().isoformat()
            }
        }

        # Custom business context
        custom_context = call_config.get('custom_context', {})

        # Create asset
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
        """Save collected assets"""

        print(f"\n{'='*70}")
        print(f"[SAVING] Test assets to: {self.output_dir}")
        print(f"{'='*70}")

        # Group by scenario for saving
        scenarios = {}
        for asset in self.collected_assets:
            if asset.scenario not in scenarios:
                scenarios[asset.scenario] = []
            scenarios[asset.scenario].append(asset)

        # Save each scenario
        for scenario_name, assets in scenarios.items():
            filename = self.output_dir / f"{scenario_name}.{format}"

            if format == 'json':
                data = [asdict(asset) for asset in assets]
                with open(filename, 'w', encoding='utf-8') as f:
                    json.dump(data, f, indent=2, ensure_ascii=False)

            file_size = filename.stat().st_size if filename.exists() else 0
            print(f"   [OK] {filename.name} ({len(assets)} assets, {file_size} bytes)")

        # Save overview
        overview_file = self.output_dir / f"overview.{format}"
        overview_data = {
            'total_assets': len(self.collected_assets),
            'scenarios': {name: len(assets) for name, assets in scenarios.items()},
            'created_at': datetime.now().isoformat(),
            'assets_summary': self._generate_summary()
        }

        with open(overview_file, 'w', encoding='utf-8') as f:
            json.dump(overview_data, f, indent=2, ensure_ascii=False)

        print(f"   [OK] {overview_file.name} (overview)")

    def _generate_summary(self) -> Dict[str, Any]:
        """Generate asset summary statistics"""
        if not self.collected_assets:
            return {}

        # Statistics by capability
        capability_stats = {}
        provider_stats = {}
        mode_stats = {}

        total_latency = 0
        total_input_tokens = 0
        total_output_tokens = 0

        for asset in self.collected_assets:
            # Capability statistics
            if asset.capability not in capability_stats:
                capability_stats[asset.capability] = {
                    'count': 0,
                    'total_latency': 0.0,
                    'total_tokens': 0,
                    'avg_latency': 0.0,
                    'avg_tokens': 0.0
                }
            capability_stats[asset.capability]['count'] += 1
            capability_stats[asset.capability]['total_latency'] += asset.latency_ms
            capability_stats[asset.capability]['total_tokens'] += asset.total_tokens

            # Provider statistics
            if asset.provider_id not in provider_stats:
                provider_stats[asset.provider_id] = {
                    'count': 0,
                    'total_tokens': 0,
                    'avg_tokens': 0.0
                }
            provider_stats[asset.provider_id]['count'] += 1
            provider_stats[asset.provider_id]['total_tokens'] += asset.total_tokens

            # Mode statistics
            if asset.mode not in mode_stats:
                mode_stats[asset.mode] = {
                    'count': 0,
                    'total_tokens': 0,
                    'avg_tokens': 0.0
                }
            mode_stats[asset.mode]['count'] += 1
            mode_stats[asset.mode]['total_tokens'] += asset.total_tokens

            # Total
            total_latency += asset.latency_ms
            total_input_tokens += asset.input_tokens
            total_output_tokens += asset.output_tokens

        # Calculate averages
        for cap_stats in capability_stats.values():
            if cap_stats['count'] > 0:
                cap_stats['avg_latency'] = cap_stats['total_latency'] / cap_stats['count']
                cap_stats['avg_tokens'] = cap_stats['total_tokens'] / cap_stats['count']

        for prov_stats in provider_stats.values():
            if prov_stats['count'] > 0:
                prov_stats['avg_tokens'] = prov_stats['total_tokens'] / prov_stats['count']

        for mode_stats_item in mode_stats.values():
            if mode_stats_item['count'] > 0:
                mode_stats_item['avg_tokens'] = mode_stats_item['total_tokens'] / mode_stats_item['count']

        return {
            'capability_stats': capability_stats,
            'provider_stats': provider_stats,
            'mode_stats': mode_stats,
            'averages': {
                'avg_latency_ms': total_latency / len(self.collected_assets),
                'avg_input_tokens': total_input_tokens / len(self.collected_assets),
                'avg_output_tokens': total_output_tokens / len(self.collected_assets),
                'avg_total_tokens': (total_input_tokens + total_output_tokens) / len(self.collected_assets)
            },
            'total_latency': total_latency,
            'total_tokens': total_input_tokens + total_output_tokens
        }

    def print_final_summary(self):
        """Print final summary report"""
        print(f"\n{'='*70}")
        print(f"[FINAL SUMMARY] Test Asset Collection Complete")
        print(f"{'='*70}")

        summary = self._generate_summary()

        print(f"\n[OVERALL STATISTICS]")
        print(f"   Total assets: {len(self.collected_assets)}")
        print(f"   Total scenarios: {len(self.scenario_stats)}")

        if summary:
            averages = summary['averages']
            print(f"\n[PERFORMANCE METRICS]")
            print(f"   Average latency: {averages['avg_latency_ms']:.1f}ms")
            print(f"   Average input tokens: {averages['avg_input_tokens']:.0f}")
            print(f"   Average output tokens: {averages['avg_output_tokens']:.0f}")
            print(f"   Average total tokens: {averages['avg_total_tokens']:.0f}")

            print(f"\n[CAPABILITY DISTRIBUTION]")
            for capability, stats in summary['capability_stats'].items():
                print(f"   {capability}: {stats['count']} calls, "
                      f"avg latency {stats['avg_latency']:.1f}ms, "
                      f"avg tokens {stats['avg_tokens']:.0f}")

            print(f"\n[PROVIDER DISTRIBUTION]")
            for provider, stats in summary['provider_stats'].items():
                print(f"   {provider}: {stats['count']} calls, avg tokens {stats['avg_tokens']:.0f}")

            print(f"\n[MODE DISTRIBUTION]")
            for mode, stats in summary['mode_stats'].items():
                print(f"   {mode}: {stats['count']} calls, avg tokens {stats['avg_tokens']:.0f}")

        print(f"\n[SAVED TO] {self.output_dir}")
        print(f"{'='*70}")


async def collect_standard_scenarios():
    """Collect standard test scenarios - Enhanced version"""

    print("[STARTING] Test Asset Collection System")
    print("[TIME] Collection time:", datetime.now().strftime("%Y-%m-%d %H:%M:%S"))
    print("[NOTICE] This is Mock collection - NO API costs will be incurred")

    collector = MockTraceCollector()

    # Scenario 1: Vision Analysis
    vision_calls = [
        {
            'capability': 'analyze_visual',
            'provider_id': 'claude',
            'mode': 'vision',
            'latency_ms': 2500.0,  # Simulate vision analysis requires longer time
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
                    {'id': 1, 'name': 'WiFi', 'type': 'menu_item'},
                    {'id': 2, 'name': 'Bluetooth', 'type': 'menu_item'},
                    {'id': 3, 'name': 'Display', 'type': 'menu_item'}
                ],
                'confidence': 0.92
            },
            'custom_context': {
                'traversal_session': 'session_001',
                'current_depth': 2,
                'target_goal': 'configure_wifi'
            },
            'tags': ['vision', 'settings', 'menu_list'],
            'description': 'Analyze visual structure of settings main page'
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
                    {'id': 2, 'name': 'Network Name', 'type': 'text', 'value': 'HomeNetwork'}
                ],
                'confidence': 0.88
            },
            'custom_context': {
                'traversal_session': 'session_001',
                'current_depth': 3,
                'target_goal': 'configure_wifi'
            },
            'tags': ['vision', 'settings', 'switch'],
            'description': 'Analyze switches and text on WiFi settings page'
        }
    ]

    await collector.collect_scenario('vision_analysis_standard', vision_calls)

    # Scenario 2: Instruction Parsing (text mode, shorter latency)
    instruction_calls = [
        {
            'capability': 'parse_instruction',
            'provider_id': 'deepseek',
            'mode': 'text',
            'latency_ms': 800.0,  # Text processing is faster
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
                    'operation': {'action': 'navigate', 'target': {'by': 'text', 'value': 'Settings'}}
                },
                'mode': 'hybrid',
                'reasoning': 'User wants to access WiFi settings',
                'confidence': 0.85
            },
            'custom_context': {
                'user_intent': 'configure_network',
                'complexity': 'medium'
            },
            'tags': ['instruction', 'parse', 'wifi'],
            'description': 'Parse instruction: Go to WiFi settings and enable WiFi'
        }
    ]

    await collector.collect_scenario('instruction_parse_standard', instruction_calls)

    # Scenario 3: Page Verification
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
                'page_analysis': {'page_type': 'settings_group'}
            },
            'output_data': {
                'is_correct': True,
                'confidence': 0.95,
                'actual_type': 'settings_group'
            },
            'custom_context': {
                'verification_purpose': 'confirm_navigation'
            },
            'tags': ['verification', 'page_type'],
            'description': 'Verify page type'
        }
    ]

    await collector.collect_scenario('page_verification', verification_calls)

    # Scenario 4: Error Handling (show low confidence case)
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
                'confidence': 0.45  # Low confidence
            },
            'custom_context': {
                'error_context': 'low_quality_image',
                'fallback_needed': True
            },
            'tags': ['error', 'low_confidence'],
            'description': 'Error handling: Poor image quality causes low confidence'
        }
    ]

    await collector.collect_scenario('error_handling', error_calls)

    # Save and show results
    collector.save_assets(format='json')
    collector.print_final_summary()

    print("\n[SUCCESS] Test asset collection completed!")
    print("[TIP] You can now view the generated JSON files, or run with 'analyze' argument to analyze assets")

    return collector


async def analyze_collected_assets():
    """Analyze collected assets - Enhanced version"""

    assets_dir = Path("tests/ai/assets/traces")
    if not assets_dir.exists():
        print("[ERROR] Assets directory does not exist, please run collection script first")
        return

    print("[ANALYZING] Collected test assets...")

    # Read overview file
    overview_file = assets_dir / "overview.json"
    if overview_file.exists():
        with open(overview_file, 'r', encoding='utf-8') as f:
            overview = json.load(f)

        print("\n" + "="*70)
        print("[OVERVIEW] Test Assets Summary")
        print("="*70)
        print(json.dumps(overview, indent=2, ensure_ascii=False))

    # Read scenario assets
    scenario_files = [f for f in assets_dir.glob("*.json") if f != overview_file]

    print(f"\n[FOUND] {len(scenario_files)} scenario files")

    for scenario_file in scenario_files:
        print(f"\n{'='*70}")
        print(f"[SCENARIO] {scenario_file.stem}")
        print(f"{'='*70}")

        with open(scenario_file, 'r', encoding='utf-8') as f:
            assets = json.load(f)

        print(f"[COUNT] {len(assets)} assets")

        # Statistics for this scenario
        capabilities = {}
        providers = {}
        confidence_scores = []

        total_latency = 0
        total_tokens = 0

        for i, asset in enumerate(assets):
            cap = asset['capability']
            prov = asset['provider_id']

            capabilities[cap] = capabilities.get(cap, 0) + 1
            providers[prov] = providers.get(prov, 0) + 1
            total_latency += asset['latency_ms']
            total_tokens += asset['total_tokens']

            # Extract confidence (if available)
            if 'output_data' in asset and isinstance(asset['output_data'], dict):
                confidence = asset['output_data'].get('confidence')
                if confidence is not None:
                    confidence_scores.append(confidence)

            # Show detailed info for first 3 assets
            if i < 3:
                print(f"\n  [{i+1}] {asset['capability']} via {asset['provider_id']}")
                print(f"      Latency: {asset['latency_ms']:.1f}ms")
                print(f"      Tokens: {asset['total_tokens']}")
                print(f"      Description: {asset['description']}")

        print(f"\n[STATISTICS]")
        print(f"  Capability distribution: {dict(capabilities)}")
        print(f"  Provider distribution: {dict(providers)}")
        print(f"  Average latency: {total_latency/len(assets):.1f}ms")
        print(f"  Average tokens: {total_tokens/len(assets):.0f}")

        if confidence_scores:
            avg_confidence = sum(confidence_scores) / len(confidence_scores)
            print(f"  Average confidence: {avg_confidence:.2f}")


if __name__ == "__main__":
    import sys

    if len(sys.argv) > 1 and sys.argv[1] == "analyze":
        print("[MODE] Asset Analysis Mode")
        asyncio.run(analyze_collected_assets())
    else:
        print("[MODE] Asset Collection Mode")
        asyncio.run(collect_standard_scenarios())
