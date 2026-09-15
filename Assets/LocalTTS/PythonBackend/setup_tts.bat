@echo off
REM ============================================================
REM  TTS Setup Script for Vedanta VR Training
REM  Installs Python venv, dependencies, and downloads models.
REM ============================================================
setlocal

set SCRIPT_DIR=%~dp0
set PYTHON_SYS=C:\Users\ADMIN\AppData\Local\Programs\Python\Python310\python.exe
set VENV_DIR=%SCRIPT_DIR%.venv
set VENV_PYTHON=%VENV_DIR%\Scripts\python.exe
set VENV_PIP=%VENV_DIR%\Scripts\pip.exe

echo ============================================================
echo  Vedanta VR Training - TTS Setup
echo ============================================================

REM Step 1: Check system Python
echo.
echo [1/4] Checking Python installation...
if not exist "%PYTHON_SYS%" (
    echo ERROR: Python not found at %PYTHON_SYS%
    echo Please install Python 3.10+ first.
    pause
    exit /b 1
)
"%PYTHON_SYS%" --version
echo OK.

REM Step 2: Create venv
echo.
echo [2/4] Creating virtual environment...
if not exist "%VENV_PYTHON%" (
    "%PYTHON_SYS%" -m venv "%VENV_DIR%"
    if errorlevel 1 (
        echo ERROR: Failed to create virtual environment.
        pause
        exit /b 1
    )
    echo Virtual environment created.
) else (
    echo Virtual environment already exists.
)

REM Step 3: Install dependencies
echo.
echo [3/4] Installing dependencies...
"%VENV_PIP%" install --upgrade pip
"%VENV_PIP%" install torch torchaudio --index-url https://download.pytorch.org/whl/cpu
"%VENV_PIP%" install transformers soundfile numpy huggingface_hub
if errorlevel 1 (
    echo ERROR: Failed to install dependencies.
    pause
    exit /b 1
)
echo Dependencies installed.

REM Step 4: Download models
echo.
echo [4/4] Downloading MMS-TTS models...
set PYTHONIOENCODING=utf-8
"%VENV_PYTHON%" "%SCRIPT_DIR%download_model.py"
if errorlevel 1 (
    echo ERROR: Model download failed.
    pause
    exit /b 1
)

echo.
echo ============================================================
echo  SETUP COMPLETE!
echo  You can now run Hindi and Odia TTS in the training module.
echo ============================================================
echo.
echo To test, run:
echo   "%VENV_PYTHON%" "%SCRIPT_DIR%test_indic_tts.py"
echo.
pause
