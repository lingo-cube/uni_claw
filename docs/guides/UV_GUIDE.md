# UV Package Manager Guide

## What is UV?

UV is a modern Python package manager written in Rust that's much faster than pip and provides better dependency management.

## Benefits

- **⚡ Performance**: 10-100x faster than pip
- **🔒 Lock Files**: Precise dependency version control
- **🎯 Reliability**: Better conflict resolution
- **🛠️ Simplicity**: Single command for most operations

## Installation

### Quick Install

```bash
# Install UV using pip
pip install uv

# Or using the official installer (Linux/macOS)
curl -LsSf https://astral.sh/uv/install.sh | sh

# Or using PowerShell (Windows)
powershell -c "irm https://astral.sh/uv/install.ps1 | iex"
```

### Verify Installation

```bash
uv --version
```

## Basic Usage

### Install Dependencies

```bash
# Install all dependencies from pyproject.toml
uv sync

# Install with development dependencies
uv sync --all-groups
```

### Adding Dependencies

```bash
# Add runtime dependency
uv add <package-name>

# Add development dependency
uv add --dev <package-name>

# Add specific version
uv add "package>=1.0.0"
```

### Running Commands

```bash
# Run Python in virtual environment
uv run python script.py

# Run tests
uv run pytest

# Run any command
uv run <command>
```

### Virtual Environment Management

```bash
# Create virtual environment
uv venv

# Remove virtual environment
uv venv --clear
```

## Migration from requirements.txt

This project has migrated from `requirements.txt` to UV-based dependency management:

**Old way:**
```bash
pip install -r requirements.txt
```

**New way:**
```bash
uv sync
```

## Troubleshooting

### UV Not Found

If you get `uv: command not found`:

1. Make sure UV is installed: `pip install uv`
2. Check your PATH includes the UV installation location
3. Restart your terminal

### Fallback to pip

If UV is not available, you can still use pip:

```bash
# Install project in editable mode
pip install -e .

# Install with development dependencies
pip install -e ".[dev]"
```

## Common Commands Reference

| Command | Description |
|----------|-------------|
| `uv sync` | Install all dependencies |
| `uv add package` | Add new dependency |
| `uv remove package` | Remove dependency |
| `uv lock` | Update lock file |
| `uv run python script.py` | Run script in virtual environment |
| `uv run pytest` | Run tests |
| `uv venv` | Create virtual environment |

## Why This Project Uses UV

1. **Single Developer Optimization**: Fast setup across multiple machines
2. **Clean Project Structure**: One `pyproject.toml` instead of multiple requirement files
3. **Better Dependency Management**: Automatic conflict resolution
4. **Modern Python Standards**: Follows current Python packaging best practices

## More Information

- [Official UV Documentation](https://github.com/astral-sh/uv)
- [Python Packaging Guide](https://packaging.python.org/)