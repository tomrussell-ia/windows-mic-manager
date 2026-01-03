@echo off
call "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Auxiliary\Build\vcvars64.bat" >nul 2>&1
cd /d D:\repos\local\windows-microphone-manager\mic-manager-rs
cargo build --release 2>&1
