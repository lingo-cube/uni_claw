#!/bin/bash
# Start dashboard server
# Usage: ./dashboards/start.sh [simple|analysis]

DASHBOARD_TYPE=${1:-simple}

case $DASHBOARD_TYPE in
    simple)
        echo "🚀 Starting Simple Dashboard on http://127.0.0.1:8002"
        python dashboards/simple_dashboard.py
        ;;
    analysis)
        echo "🚀 Starting Analysis Dashboard on http://127.0.0.1:8000"
        python scripts/analysis_dashboard.py
        ;;
    *)
        echo "Usage: $0 [simple|analysis]"
        echo "  simple   - Start simple dashboard (port 8002)"
        echo "  analysis - Start analysis dashboard (port 8000)"
        exit 1
        ;;
esac
