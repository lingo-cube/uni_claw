"""Recording and replay mechanism for AI API responses.

This module provides tools to record real API responses for later replay,
enabling zero-cost testing with realistic response data.
"""

import json
import time
from pathlib import Path
from typing import Dict, Any, Optional, List
from dataclasses import dataclass, field
from datetime import datetime
import hashlib


@dataclass
class RecordedCall:
    """A recorded API call with its response."""

    call_id: str
    provider: str
    method: str  # complete_text, complete_vision, complete_multimodal
    timestamp: str

    # Request data
    request_params: Dict[str, Any]

    # Response data
    response_content: str
    input_tokens: int
    output_tokens: int
    latency_ms: float
    success: bool

    # Metadata
    metadata: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for serialization."""
        return {
            "call_id": self.call_id,
            "provider": self.provider,
            "method": self.method,
            "timestamp": self.timestamp,
            "request_params": self.request_params,
            "response_content": self.response_content,
            "input_tokens": self.input_tokens,
            "output_tokens": self.output_tokens,
            "latency_ms": self.latency_ms,
            "success": self.success,
            "metadata": self.metadata,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "RecordedCall":
        """Create from dictionary."""
        return cls(
            call_id=data["call_id"],
            provider=data["provider"],
            method=data["method"],
            timestamp=data["timestamp"],
            request_params=data["request_params"],
            response_content=data["response_content"],
            input_tokens=data["input_tokens"],
            output_tokens=data["output_tokens"],
            latency_ms=data["latency_ms"],
            success=data["success"],
            metadata=data.get("metadata", {}),
        )


class ResponseRecorder:
    """Records AI API responses for later replay."""

    def __init__(self, storage_path: Optional[Path] = None):
        """Initialize the recorder.

        Args:
            storage_path: Path to store recorded responses. If None, uses in-memory storage.
        """
        self.storage_path = storage_path or Path("tests/ai/fixtures/recordings")
        self._recordings: Dict[str, RecordedCall] = {}
        self._session_start = datetime.now().isoformat()

    def _generate_call_id(self, provider: str, method: str, params: Dict[str, Any]) -> str:
        """Generate a unique call ID."""
        # Create a hash based on provider, method, and key params
        hash_input = f"{provider}_{method}_{str(sorted(params.items()))}"
        return hashlib.md5(hash_input.encode()).hexdigest()[:12]

    def record(
        self,
        provider: str,
        method: str,
        request_params: Dict[str, Any],
        response_content: str,
        input_tokens: int,
        output_tokens: int,
        latency_ms: float,
        success: bool = True,
        metadata: Optional[Dict[str, Any]] = None,
    ) -> str:
        """Record an API call and its response.

        Args:
            provider: Provider name (e.g., "deepseek", "claude")
            method: Method name (complete_text, complete_vision, etc.)
            request_params: Request parameters
            response_content: Response content
            input_tokens: Input token count
            output_tokens: Output token count
            latency_ms: Request latency in milliseconds
            success: Whether the call was successful
            metadata: Optional metadata

        Returns:
            str: The call ID
        """
        call_id = self._generate_call_id(provider, method, request_params)

        call = RecordedCall(
            call_id=call_id,
            provider=provider,
            method=method,
            timestamp=datetime.now().isoformat(),
            request_params=request_params,
            response_content=response_content,
            input_tokens=input_tokens,
            output_tokens=output_tokens,
            latency_ms=latency_ms,
            success=success,
            metadata=metadata or {},
        )

        self._recordings[call_id] = call
        return call_id

    def get_recording(self, call_id: str) -> Optional[RecordedCall]:
        """Get a recording by ID."""
        return self._recordings.get(call_id)

    def find_match(
        self, provider: str, method: str, request_params: Dict[str, Any]
    ) -> Optional[RecordedCall]:
        """Find a recording matching the given parameters."""
        call_id = self._generate_call_id(provider, method, request_params)
        return self._recordings.get(call_id)

    def list_recordings(
        self, provider: Optional[str] = None, method: Optional[str] = None
    ) -> List[RecordedCall]:
        """List recordings, optionally filtered by provider and/or method."""
        recordings = list(self._recordings.values())

        if provider:
            recordings = [r for r in recordings if r.provider == provider]
        if method:
            recordings = [r for r in recordings if r.method == method]

        return recordings

    def save(self, filename: Optional[str] = None) -> Path:
        """Save recordings to disk.

        Args:
            filename: Optional filename. If None, generates based on timestamp.

        Returns:
            Path: The path to the saved file
        """
        if filename is None:
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            filename = f"recordings_{timestamp}.jsonl"

        self.storage_path.mkdir(parents=True, exist_ok=True)
        file_path = self.storage_path / filename

        with open(file_path, "w") as f:
            for call in self._recordings.values():
                f.write(json.dumps(call.to_dict()) + "\n")

        return file_path

    def load(self, filename: str) -> int:
        """Load recordings from disk.

        Args:
            filename: Name of the file to load

        Returns:
            int: Number of recordings loaded
        """
        file_path = self.storage_path / filename

        if not file_path.exists():
            raise FileNotFoundError(f"Recording file not found: {file_path}")

        count = 0
        with open(file_path, "r") as f:
            for line in f:
                if line.strip():
                    call = RecordedCall.from_dict(json.loads(line))
                    self._recordings[call.call_id] = call
                    count += 1

        return count

    @property
    def recording_count(self) -> int:
        """Get total number of recordings."""
        return len(self._recordings)

    def clear(self) -> None:
        """Clear all recordings."""
        self._recordings.clear()

    def get_summary(self) -> Dict[str, Any]:
        """Get a summary of recordings."""
        recordings = list(self._recordings.values())

        if not recordings:
            return {"total": 0, "by_provider": {}, "by_method": {}}

        by_provider = {}
        by_method = {}

        for call in recordings:
            by_provider[call.provider] = by_provider.get(call.provider, 0) + 1
            by_method[call.method] = by_method.get(call.method, 0) + 1

        return {
            "total": len(recordings),
            "by_provider": by_provider,
            "by_method": by_method,
            "session_start": self._session_start,
        }


class ResponseReplayer:
    """Replays recorded API responses."""

    def __init__(self, recorder: Optional[ResponseRecorder] = None):
        """Initialize the replayer.

        Args:
            recorder: Optional recorder to use. If None, creates a new one.
        """
        self.recorder = recorder or ResponseRecorder()
        self._playback_count = 0
        self._misses: List[Dict[str, Any]] = []

    def replay(
        self, provider: str, method: str, request_params: Dict[str, Any]
    ) -> Optional[RecordedCall]:
        """Replay a recorded response.

        Args:
            provider: Provider name
            method: Method name
            request_params: Request parameters

        Returns:
            RecordedCall if found, None otherwise
        """
        recording = self.recorder.find_match(provider, method, request_params)

        if recording:
            self._playback_count += 1
            return recording
        else:
            self._misses.append({
                "provider": provider,
                "method": method,
                "params": request_params,
                "timestamp": datetime.now().isoformat(),
            })
            return None

    def load_recordings(self, filename: str) -> int:
        """Load recordings for replay.

        Args:
            filename: Name of the file to load

        Returns:
            int: Number of recordings loaded
        """
        return self.recorder.load(filename)

    @property
    def playback_count(self) -> int:
        """Get total number of successful playbacks."""
        return self._playback_count

    @property
    def miss_count(self) -> int:
        """Get total number of misses (no matching recording)."""
        return len(self._misses)

    def get_misses(self) -> List[Dict[str, Any]]:
        """Get list of misses."""
        return self._misses.copy()

    def clear_misses(self) -> None:
        """Clear miss history."""
        self._misses.clear()

    def get_summary(self) -> Dict[str, Any]:
        """Get a summary of replay activity."""
        return {
            "playback_count": self._playback_count,
            "miss_count": self.miss_count,
            "available_recordings": self.recorder.recording_count,
            "miss_rate": self.miss_count / max(1, self._playback_count + self.miss_count),
        }


# Convenience function for recording sessions
async def record_api_call(
    recorder: ResponseRecorder,
    provider: str,
    method: str,
    api_func,
    **api_kwargs
) -> Any:
    """Record an API call while executing it.

    Args:
        recorder: The recorder to use
        provider: Provider name
        method: Method name
        api_func: The API function to call
        **api_kwargs: Arguments to pass to the API function

    Returns:
        The API response
    """
    import time

    start_time = time.time()
    response = await api_func(**api_kwargs)
    latency_ms = (time.time() - start_time) * 1000

    # Record the call
    # Note: This assumes the response has certain attributes
    # Adjust based on actual response structure
    recorder.record(
        provider=provider,
        method=method,
        request_params=api_kwargs,
        response_content=getattr(response, "content", str(response)),
        input_tokens=getattr(response, "input_tokens", 0),
        output_tokens=getattr(response, "output_tokens", 0),
        latency_ms=latency_ms,
        success=True,
    )

    return response
