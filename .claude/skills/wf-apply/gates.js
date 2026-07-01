/**
 * Hard Constraint Gates for Task Execution
 *
 * Provides actual verification (tests, lint, type-check) instead of
 * relying on model opinion only.
 */

/**
 * Test Gate - Run relevant tests after implementation
 *
 * @param {string[]} filesModified - List of files that were modified
 * @param {string} projectRoot - Root directory of the project
 * @returns {{passed: boolean, summary: string, details: string}}
 */
export function testGate(filesModified, projectRoot = '.') {
  const testCommands = {
    // If test files were modified, run those specific tests
    specific: filesModified
      .filter(f => f.includes('tests/'))
      .map(f => f.replace('tests/', '').replace('.py', ''))
      .map(f => `pytest tests/${f}.py -v`),

    // If source files were modified, run related tests
    related: filesModified
      .filter(f => f.includes('src/'))
      .map(f => f.replace('src/', '').replace('.py', '').replace('/', '.'))
      .map(f => `pytest tests/ -k "${f}" -v`),

    // Default: run all tests
    all: ['pytest tests/ -v']
  }

  const command = testCommands.specific.length > 0
    ? testCommands.specific[0]
    : testCommands.related.length > 0
      ? testCommands.related[0]
      : testCommands.all[0]

  try {
    const result = execSync(command, {
      cwd: projectRoot,
      encoding: 'utf-8',
      timeout: 60000 // 1 minute timeout
    })

    // Parse pytest output for summary
    const lines = result.split('\n')
    const summaryLine = lines.find(l => l.includes('passed'))

    return {
      passed: true,
      summary: summaryLine || 'Tests passed',
      details: result
    }
  } catch (error) {
    return {
      passed: false,
      summary: 'Tests failed',
      details: error.stdout || error.stderr || error.message
    }
  }
}

/**
 * Lint Gate - Run linter on modified files
 *
 * @param {string[]} filesModified - List of files that were modified
 * @param {string} projectRoot - Root directory of the project
 * @returns {{passed: boolean, summary: string, details: string}}
 */
export function lintGate(filesModified, projectRoot = '.') {
  // Filter Python files only
  const pythonFiles = filesModified.filter(f => f.endsWith('.py'))

  if (pythonFiles.length === 0) {
    return { passed: true, summary: 'No Python files to lint', details: '' }
  }

  try {
    const result = execSync(
      `ruff check ${pythonFiles.join(' ')}`,
      {
        cwd: projectRoot,
        encoding: 'utf-8',
        timeout: 30000
      }
    )

    return {
      passed: true,
      summary: 'Lint passed',
      details: result
    }
  } catch (error) {
    return {
      passed: false,
      summary: 'Lint errors found',
      details: error.stdout || error.stderr || error.message
    }
  }
}

/**
 * Type Check Gate - Run mypy on modified files
 *
 * @param {string[]} filesModified - List of files that were modified
 * @param {string} projectRoot - Root directory of the project
 * @returns {{passed: boolean, summary: string, details: string}}
 */
export function typeCheckGate(filesModified, projectRoot = '.') {
  const pythonFiles = filesModified.filter(f => f.endsWith('.py'))

  if (pythonFiles.length === 0) {
    return { passed: true, summary: 'No Python files to type check', details: '' }
  }

  try {
    const result = execSync(
      `mypy ${pythonFiles.join(' ')} --no-error-summary`,
      {
        cwd: projectRoot,
        encoding: 'utf-8',
        timeout: 30000
      }
    )

    return {
      passed: true,
      summary: 'Type check passed',
      details: result
    }
  } catch (error) {
    return {
      passed: false,
      summary: 'Type errors found',
      details: error.stdout || error.stderr || error.message
    }
  }
}

/**
 * Run all gates and return combined result
 *
 * @param {string[]} filesModified - List of files that were modified
 * @param {Object} options - { projectRoot, skipTest, skipLint, skipTypeCheck }
 * @returns {{passed: boolean, gates: Object, failures: string[]}}
 */
export function runAllGates(filesModified, options = {}) {
  const {
    projectRoot = '.',
    skipTest = false,
    skipLint = false,
    skipTypeCheck = false
  } = options

  const gates = {}
  const failures = []

  if (!skipTest && filesModified.length > 0) {
    gates.test = testGate(filesModified, projectRoot)
    if (!gates.test.passed) failures.push('test')
  }

  if (!skipLint) {
    gates.lint = lintGate(filesModified, projectRoot)
    if (!gates.lint.passed) failures.push('lint')
  }

  if (!skipTypeCheck) {
    gates.typeCheck = typeCheckGate(filesModified, projectRoot)
    if (!gates.typeCheck.passed) failures.push('typeCheck')
  }

  return {
    passed: failures.length === 0,
    gates,
    failures
  }
}
