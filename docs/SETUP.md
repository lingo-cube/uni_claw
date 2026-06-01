# Setup Instructions for Uni-claw

## Prerequisites

- Python 3.10+
- Android device (or use `--mock` flag for testing)
- ADB installed and in PATH
- Anthropic API key

## Installation Steps

1. **Create virtual environment**
   ```bash
   python -m venv venv
   source venv/bin/activate  # On Windows: venv\Scripts\activate
   ```

2. **Install dependencies**
   ```bash
   pip install -r requirements.txt
   ```

3. **Configure environment**
   ```bash
   cp .env.example .env
   # Edit .env and add your ANTHROPIC_API_KEY
   ```

## Running the Demo

### Mock Mode (No Device Required)

```bash
python run.py "VehicleSettings" --mock
```

### Real Device Mode

1. **Connect device via ADB**
   ```bash
   adb devices
   # Should show your device
   ```

2. **Run traversal**
   ```bash
   python run.py "VehicleSettings"
   # Or specify device if multiple connected:
   python run.py "VehicleSettings" --device <device_id>
   ```

## Testing

After installing dependencies:

```bash
# Run all tests
pytest tests/ -v

# Run specific test module
pytest tests/test_state.py -v

# With coverage report
pytest tests/ --cov=src --cov-report=html
```

## Project Structure Verification

```bash
# Verify all modules are present
find src -name "*.py"
find tests -name "*.py"
```

## Troubleshooting

### Import Errors

If you see `ModuleNotFoundError`, ensure you:
1. Activated the virtual environment
2. Ran `pip install -r requirements.txt`
3. Are running from the project root directory

### ADB Connection Issues

```bash
# Check ADB is installed
which adb

# Restart ADB server
adb kill-server
adb start-server

# Check device connection
adb devices -l
```

### API Key Issues

Ensure your `.env` file contains:
```
ANTHROPIC_API_KEY=sk-ant-...
```
