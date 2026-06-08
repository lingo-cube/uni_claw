/**
 * Uni-Claw 规则引擎
 *
 * 用于验证生成的测试是否符合项目规则
 */

import { load } from 'js-yaml';

// ============================================================================
// 规则加载器
// ============================================================================

class RuleEngine {
  constructor(rulesPath) {
    this.rules = this.loadRules(rulesPath);
    this.violations = [];
  }

  loadRules(path) {
    try {
      const fs = require('fs');
      const content = fs.readFileSync(path, 'utf8');
      return load(content);
    } catch (error) {
      console.error(`Failed to load rules from ${path}:`, error);
      return {};
    }
  }

  // ============================================================================
  // 生成Prompt约束
  // ============================================================================

  generateConstraints(category) {
    const rules = this.rules[category];
    if (!rules) return '';

    const sections = [];

    for (const [key, value] of Object.entries(rules)) {
      sections.push(this.formatRule(key, value, 0));
    }

    return `
# ${category.toUpperCase()} 规则

${sections.join('\n\n')}

必须严格遵守以上规则。
`;
  }

  formatRule(key, value, indent = 0) {
    const prefix = '  '.repeat(indent);

    if (typeof value === 'boolean') {
      return value ? `${prefix}✅ ${key}` : `${prefix}❌ ${key}`;
    } else if (typeof value === 'number') {
      return `${prefix}📊 ${key}: ${value}`;
    } else if (typeof value === 'string') {
      return `${prefix}📝 ${key}: "${value}"`;
    } else if (Array.isArray(value)) {
      if (value.length === 0) return `${prefix}${key}: []`;
      if (typeof value[0] === 'object') {
        // 复杂数组（对象数组）
        return value.map((item, i) => {
          if (typeof item === 'object') {
            const itemStr = Object.entries(item)
              .map(([k, v]) => `    - ${k}: ${v}`)
              .join('\n');
            return `${prefix}- ${key} ${i + 1}:\n${itemStr}`;
          }
          return `${prefix}- ${item}`;
        }).join('\n');
      }
      return `${prefix}📋 ${key}:\n${value.map(v => `${prefix}  - ${v}`).join('\n')}`;
    } else if (typeof value === 'object' && value !== null) {
      // 嵌套对象
      const entries = Object.entries(value).map(([k, v]) =>
        this.formatRule(k, v, indent + 1)
      );
      return `${prefix}${key}:\n${entries.join('\n')}`;
    }
    return `${prefix}${key}: ${value}`;
  }

  // ============================================================================
  // 验证规则
  // ============================================================================

  validate(category, data) {
    this.violations = [];
    const rules = this.rules[category];

    if (!rules) {
      return { valid: true, violations: [], warnings: [] };
    }

    // 根据不同类别执行不同的验证
    switch (category) {
      case 'assertions':
        return this.validateAssertions(data, rules);
      case 'naming':
        return this.validateNaming(data, rules);
      case 'coverage':
        return this.validateCoverage(data, rules);
      case 'quality_gates':
        return this.validateQualityGates(data, rules);
      default:
        return { valid: true, violations: [], warnings: [] };
    }
  }

  // 验证断言
  validateAssertions(testCode, rules) {
    const violations = [];

    // 检查最少断言数
    const assertCount = (testCode.match(/assert/g) || []).length;
    if (assertCount < rules.min_per_test) {
      violations.push({
        rule: 'min_per_test',
        expected: rules.min_per_test,
        actual: assertCount,
        message: `断言数量不足: ${assertCount} < ${rules.min_per_test}`
      });
    }

    // 检查避免的断言模式
    for (const badPattern of rules.quality_rules.avoid) {
      if (testCode.includes(badPattern)) {
        violations.push({
          rule: 'avoid_pattern',
          pattern: badPattern,
          message: `避免使用: ${badPattern}`
        });
      }
    }

    return {
      valid: violations.length === 0,
      violations,
      warnings: []
    };
  }

  // 验证命名
  validateNaming(testInfo, rules) {
    const violations = [];
    const warnings = [];

    // 检查文件命名
    if (testInfo.fileName && !testInfo.fileName.match(/^test_\w+\.py$/)) {
      violations.push({
        rule: 'file_naming',
        pattern: rules.file.pattern,
        actual: testInfo.fileName,
        message: '文件命名不符合规范'
      });
    }

    // 检查类命名
    if (testInfo.className && !testInfo.className.match(/^Test\w+$/)) {
      violations.push({
        rule: 'class_naming',
        pattern: rules.class.pattern,
        actual: testInfo.className,
        message: '类命名不符合规范'
      });
    }

    return {
      valid: violations.length === 0,
      violations,
      warnings
    };
  }

  // 验证覆盖率
  validateCoverage(coverageData, rules) {
    const violations = [];
    const warnings = [];

    const moduleType = coverageData.moduleType || 'core_modules';
    const minCoverage = rules.minimum[moduleType] || 80;

    if (coverageData.percent < minCoverage) {
      violations.push({
        rule: 'minimum_coverage',
        expected: minCoverage,
        actual: coverageData.percent,
        message: `覆盖率不足: ${coverageData.percent}% < ${minCoverage}%`
      });
    } else if (coverageData.percent < minCoverage + 5) {
      warnings.push({
        rule: 'coverage_warning',
        message: `覆盖率接近下限: ${coverageData.percent}%`
      });
    }

    return {
      valid: violations.length === 0,
      violations,
      warnings
    };
  }

  // 验证质量门禁
  validateQualityGates(testResult, rules) {
    const violations = [];
    const warnings = [];

    // 检查阻止条件
    for (const blockingCondition of rules.blocking) {
      if (this.evaluateCondition(blockingCondition, testResult)) {
        violations.push({
          rule: 'blocking_condition',
          condition: blockingCondition,
          message: `质量门禁阻止: ${blockingCondition}`
        });
      }
    }

    // 检查警告条件
    for (const warningCondition of rules.warnings) {
      if (this.evaluateCondition(warningCondition, testResult)) {
        warnings.push({
          rule: 'warning_condition',
          condition: warningCondition,
          message: `质量警告: ${warningCondition}`
        });
      }
    }

    return {
      valid: violations.length === 0,
      violations,
      warnings
    };
  }

  // 评估条件（简化版）
  evaluateCondition(condition, data) {
    // 简化的条件评估
    if (condition.includes('测试失败数 > 0')) {
      return (data.failed || 0) > 0;
    }
    if (condition.includes('测试错误数 > 0')) {
      return (data.errors || 0) > 0;
    }
    if (condition.includes('覆盖率下降 > 5%')) {
      return (data.coverageDrop || 0) > 5;
    }
    return false;
  }

  // ============================================================================
  // 生成测试建议
  // ============================================================================

  generateSuggestions(category, context) {
    const suggestions = [];

    switch (category) {
      case 'test_scenarios':
        suggestions.push(...this.generateScenarioSuggestions(context));
        break;
      case 'test_fixtures':
        suggestions.push(...this.generateFixtureSuggestions(context));
        break;
      default:
        break;
    }

    return suggestions;
  }

  generateScenarioSuggestions(context) {
    const suggestions = [];
    const { moduleName, methods } = context;

    // 基于V6特定规则的建议
    if (moduleName === 'state_machine') {
      const v6Rules = this.rules.v6_specific;
      if (v6Rules) {
        for (const fix of v6Rules.critical_fixes) {
          if (methods.includes(fix.method)) {
            suggestions.push({
              priority: 'HIGH',
              scenario: fix.scenarios[0],
              reason: `V6.9.5核心修复验证`
            });
          }
        }
      }
    }

    return suggestions;
  }

  generateFixtureSuggestions(context) {
    const suggestions = [];
    const commonFixtures = this.rules.fixtures?.common_fixtures || [];

    for (const fixture of commonFixtures) {
      if (context.dependencies?.includes(fixture.description.split(' ')[1])) {
        suggestions.push({
          type: 'reuse_fixture',
          name: fixture.name,
          description: fixture.description
        });
      }
    }

    return suggestions;
  }

  // ============================================================================
  // 生成质量报告
  // ============================================================================

  generateQualityReport(validationResults) {
    const report = {
      timestamp: new Date().toISOString(),
      summary: {
        total: validationResults.length,
        valid: validationResults.filter(r => r.valid).length,
        invalid: validationResults.filter(r => !r.valid).length
      },
      details: validationResults
    };

    return report;
  }
}

// ============================================================================
// 导出
// ============================================================================

module.exports = RuleEngine;
