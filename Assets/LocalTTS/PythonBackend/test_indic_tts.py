"""
Standalone test for Hindi and Odia TTS using Facebook MMS-TTS.
Run this OUTSIDE Unity to verify audio generation works.

Usage:
  python test_indic_tts.py

Generates:
  test_hindi.wav
  test_odia.wav
"""
import os
import sys
import json
import time
import numpy as np

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
# SCRIPT_DIR = .../Assets/LocalTTS/PythonBackend
# Go up 3 levels to reach the project root (Vedanta Tire/)
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", "..", ".."))

TESTS = [
    {
        "name": "Hindi",
        "lang": "hi",
        "text": "\u0928\u092e\u0938\u094d\u0924\u0947\u0964 \u092f\u0939 \u0939\u093f\u0928\u094d\u0926\u0940 \u091f\u0947\u0915\u094d\u0938\u094d\u091f \u091f\u0942 \u0938\u094d\u092a\u0940\u091a \u092a\u0930\u0940\u0915\u094d\u0937\u0923 \u0939\u0948\u0964",
        "model_dir": os.path.join(PROJECT_ROOT, "Assets", "LocalTTS", "Models", "MMS_TTS_Hindi"),
        "output": os.path.join(SCRIPT_DIR, "test_hindi.wav"),
    },
    {
        "name": "Odia",
        "lang": "or",
        "text": "\u0b28\u0b2e\u0b38\u0b4d\u0b15\u0b3e\u0b30\u0b4d\u0964 \u0b0f\u0b39\u0b3e \u0b13\u0b21\u0b3c\u0b3f\u0b06 \u0b1f\u0b47\u0b15\u0b4d\u0b38\u0b1f\u0b4d \u0b1f\u0b41 \u0b38\u0b4d\u0b2a\u0b3f\u0b1a\u0b4d \u0b2a\u0b30\u0b40\u0b15\u0b4d\u0b37\u0b3e \u0b05\u0b1f\u0b47\u0b64",
        "model_dir": os.path.join(PROJECT_ROOT, "Assets", "LocalTTS", "Models", "MMS_TTS_Odia"),
        "output": os.path.join(SCRIPT_DIR, "test_odia.wav"),
    },
]

def test_language(test_info):
    from transformers import VitsModel, AutoTokenizer
    import torch
    import soundfile as sf
    
    name = test_info["name"]
    print(f"\n{'='*60}")
    print(f"  TESTING {name.upper()} TTS")
    print(f"{'='*60}")
    
    model_dir = test_info["model_dir"]
    if not os.path.isdir(model_dir):
        print(f"  FAIL: Model directory does not exist: {model_dir}")
        return False
    
    config_path = os.path.join(model_dir, "config.json")
    if not os.path.exists(config_path):
        print(f"  FAIL: config.json not found in {model_dir}")
        return False
    
    # Load model
    print(f"  Loading model from {model_dir}...")
    t0 = time.time()
    model = VitsModel.from_pretrained(model_dir, local_files_only=True)
    tokenizer = AutoTokenizer.from_pretrained(model_dir, local_files_only=True)
    device = "cuda" if torch.cuda.is_available() else "cpu"
    model = model.to(device)
    load_time = time.time() - t0
    print(f"  Model loaded on {device} in {load_time:.1f}s")
    
    # Generate
    text = test_info["text"]
    print(f"  Text: {text}")
    
    t0 = time.time()
    inputs = tokenizer(text, return_tensors="pt").to(device)
    with torch.no_grad():
        output = model(**inputs)
    
    waveform = output.waveform[0].cpu().numpy()
    sample_rate = model.config.sampling_rate
    gen_time = time.time() - t0
    
    # Normalize
    max_val = np.abs(waveform).max()
    if max_val > 0:
        waveform = waveform / max_val * 0.95
    
    # Save
    output_path = test_info["output"]
    sf.write(output_path, waveform, sample_rate)
    
    # Stats
    file_size = os.path.getsize(output_path)
    duration = len(waveform) / sample_rate
    rms = np.sqrt(np.mean(waveform ** 2))
    
    print(f"  Output: {output_path}")
    print(f"  Duration: {duration:.2f}s")
    print(f"  Sample rate: {sample_rate} Hz")
    print(f"  File size: {file_size} bytes")
    print(f"  RMS level: {rms:.6f}")
    print(f"  Generation time: {gen_time:.2f}s")
    
    # Verdict
    if file_size < 1000:
        print(f"  FAIL: File too small ({file_size} bytes)")
        return False
    if rms < 0.001:
        print(f"  FAIL: Audio is near-silent (RMS={rms:.6f})")
        return False
    if duration < 0.5:
        print(f"  FAIL: Audio too short ({duration:.2f}s)")
        return False
    
    print(f"  PASS: {name} TTS working!")
    return True

def main():
    print("Facebook MMS-TTS Standalone Test")
    print("=" * 60)
    
    results = {}
    for test_info in TESTS:
        try:
            results[test_info["name"]] = test_language(test_info)
        except Exception as e:
            print(f"  EXCEPTION: {e}")
            import traceback
            traceback.print_exc()
            results[test_info["name"]] = False
    
    print(f"\n{'='*60}")
    print("  RESULTS SUMMARY")
    print(f"{'='*60}")
    all_pass = True
    for name, passed in results.items():
        status = "PASS" if passed else "FAIL"
        print(f"  {name}: {status}")
        if not passed:
            all_pass = False
    
    if all_pass:
        print(f"\n  ALL TESTS PASSED!")
        print(f"  Play the WAV files to verify they are audible:")
        for test_info in TESTS:
            print(f"    {test_info['output']}")
    else:
        print(f"\n  SOME TESTS FAILED!")
        sys.exit(1)

if __name__ == "__main__":
    main()
