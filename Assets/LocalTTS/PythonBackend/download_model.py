"""
Download Facebook MMS-TTS models for Hindi and Odia.
These are non-gated, lightweight, single-language TTS models.
"""
from transformers import VitsModel, AutoTokenizer
import os
import sys

MODELS = {
    "hindi": {
        "repo": "facebook/mms-tts-hin",
        "local_dir": "d:/unity cli/Vedanta Tire/Assets/LocalTTS/Models/MMS_TTS_Hindi",
    },
    "odia": {
        "repo": "facebook/mms-tts-ory",
        "local_dir": "d:/unity cli/Vedanta Tire/Assets/LocalTTS/Models/MMS_TTS_Odia",
    },
}

def download_model(lang, info):
    print(f"\n{'='*60}")
    print(f"Downloading {lang} model: {info['repo']}")
    print(f"Target: {info['local_dir']}")
    print(f"{'='*60}")
    
    os.makedirs(info["local_dir"], exist_ok=True)
    
    # Download and save model
    print(f"  Downloading model weights...")
    model = VitsModel.from_pretrained(info["repo"])
    model.save_pretrained(info["local_dir"])
    print(f"  Model saved.")
    
    # Download and save tokenizer
    print(f"  Downloading tokenizer...")
    tokenizer = AutoTokenizer.from_pretrained(info["repo"])
    tokenizer.save_pretrained(info["local_dir"])
    print(f"  Tokenizer saved.")
    
    # Verify files exist
    config_path = os.path.join(info["local_dir"], "config.json")
    if os.path.exists(config_path):
        size_mb = sum(
            os.path.getsize(os.path.join(info["local_dir"], f))
            for f in os.listdir(info["local_dir"])
            if os.path.isfile(os.path.join(info["local_dir"], f))
        ) / (1024 * 1024)
        print(f"  SUCCESS: {lang} model downloaded ({size_mb:.1f} MB)")
    else:
        print(f"  ERROR: config.json not found after download!", file=sys.stderr)
        return False
    return True

def main():
    success = True
    for lang, info in MODELS.items():
        try:
            if not download_model(lang, info):
                success = False
        except Exception as e:
            print(f"  ERROR downloading {lang}: {e}", file=sys.stderr)
            success = False
    
    if success:
        print(f"\n{'='*60}")
        print("ALL MODELS DOWNLOADED SUCCESSFULLY")
        print(f"{'='*60}")
    else:
        print(f"\nSome downloads failed.", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
