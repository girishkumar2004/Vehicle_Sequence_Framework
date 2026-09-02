"""
Offline TTS Backend for Unity VR Training
==========================================
Supports:
  - English: Facebook MMS-TTS-ENG (VITS model, offline)
  - Hindi:   Facebook MMS-TTS-HIN (VITS model, offline)
  - Odia:    Facebook MMS-TTS-ORY (VITS model, offline)

Usage:
  python tts_backend.py <request_json_path>

Request JSON format:
{
    "text": "...",
    "language": "en" | "hi" | "or",
    "output_path": "path/to/output.wav"
}

The script reads UTF-8 JSON to avoid Windows command-line Unicode corruption.
"""
import json
import sys
import os
import time
import numpy as np

# Paths to local model directories
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
# SCRIPT_DIR = .../Assets/LocalTTS/PythonBackend
# Go up 3 levels to reach the project root (Vedanta Tire/)
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", "..", ".."))

MODEL_PATHS = {
    "en": os.path.join(PROJECT_ROOT, "Assets", "LocalTTS", "Models", "MMS_TTS_English"),
    "hi": os.path.join(PROJECT_ROOT, "Assets", "LocalTTS", "Models", "MMS_TTS_Hindi"),
    "or": os.path.join(PROJECT_ROOT, "Assets", "LocalTTS", "Models", "MMS_TTS_Odia"),
}

def load_model(lang_code):
    """Load the MMS-TTS model and tokenizer for the given language."""
    from transformers import VitsModel, AutoTokenizer
    import torch

    model_path = MODEL_PATHS.get(lang_code)
    if not model_path or not os.path.isdir(model_path):
        print(f"[TTS ERROR] Model directory not found for '{lang_code}': {model_path}", file=sys.stderr)
        sys.exit(1)

    config_file = os.path.join(model_path, "config.json")
    if not os.path.exists(config_file):
        print(f"[TTS ERROR] config.json not found in {model_path}. Model not downloaded.", file=sys.stderr)
        sys.exit(1)

    print(f"[TTS] Loading model from: {model_path}")
    t0 = time.time()
    
    model = VitsModel.from_pretrained(model_path, local_files_only=True)
    tokenizer = AutoTokenizer.from_pretrained(model_path, local_files_only=True)
    
    # Use GPU if available
    device = "cuda" if torch.cuda.is_available() else "cpu"
    model = model.to(device)
    
    elapsed = time.time() - t0
    print(f"[TTS] Model loaded on {device} in {elapsed:.1f}s")
    
    return model, tokenizer, device

def generate_speech(model, tokenizer, device, text, output_path):
    """Generate speech from text and save as WAV."""
    import torch
    import soundfile as sf

    print(f"[TTS] Generating speech for: {text[:60]}...")
    t0 = time.time()
    
    # Tokenize
    inputs = tokenizer(text, return_tensors="pt").to(device)
    
    # Generate
    with torch.no_grad():
        output = model(**inputs)
    
    # Extract waveform
    waveform = output.waveform[0].cpu().numpy()
    
    # Get sample rate from model config
    sample_rate = model.config.sampling_rate
    
    # Normalize to prevent clipping
    max_val = np.abs(waveform).max()
    if max_val > 0:
        waveform = waveform / max_val * 0.95
    
    # Save as WAV
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    sf.write(output_path, waveform, sample_rate)
    
    elapsed = time.time() - t0
    file_size = os.path.getsize(output_path)
    duration = len(waveform) / sample_rate
    
    print(f"[TTS] Generated: {output_path}")
    print(f"[TTS] Duration: {duration:.2f}s, Size: {file_size} bytes, Time: {elapsed:.2f}s")
    print(f"[TTS] Sample rate: {sample_rate} Hz")
    
    # Verify the file is not silent
    rms = np.sqrt(np.mean(waveform ** 2))
    print(f"[TTS] RMS level: {rms:.6f}")
    if rms < 0.001:
        print("[TTS WARNING] Audio appears to be near-silent!", file=sys.stderr)
        return False
    
    print("[TTS] SUCCESS")
    return True

def main():
    if len(sys.argv) < 2:
        print("Usage: python tts_backend.py <request_json_path>", file=sys.stderr)
        sys.exit(1)
    
    request_path = sys.argv[1]
    
    # Read UTF-8 JSON request
    if not os.path.exists(request_path):
        print(f"[TTS ERROR] Request file not found: {request_path}", file=sys.stderr)
        sys.exit(1)
    
    with open(request_path, "r", encoding="utf-8-sig") as f:
        request = json.load(f)
    
    text = request.get("text", "")
    language = request.get("language", "")
    output_path = request.get("output_path", "")
    
    if not text:
        print("[TTS ERROR] 'text' is empty in request JSON.", file=sys.stderr)
        sys.exit(1)
    if not language:
        print("[TTS ERROR] 'language' is empty in request JSON.", file=sys.stderr)
        sys.exit(1)
    if not output_path:
        print("[TTS ERROR] 'output_path' is empty in request JSON.", file=sys.stderr)
        sys.exit(1)
    
    if language not in MODEL_PATHS:
        print(f"[TTS ERROR] Unsupported language '{language}'. Supported: {list(MODEL_PATHS.keys())}", file=sys.stderr)
        sys.exit(1)
    
    print(f"[TTS] Request: lang={language}, output={output_path}")
    print(f"[TTS] Text: {text}")
    
    model, tokenizer, device = load_model(language)
    success = generate_speech(model, tokenizer, device, text, output_path)
    
    if not success:
        sys.exit(1)

if __name__ == "__main__":
    main()
