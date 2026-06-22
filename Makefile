.PHONY: build package run clean help

# 默认目标
all: build

# 编译项目
build:
	@echo ">>> 编译项目..."
	@dotnet publish src/VoiceInput --configuration Release --output publish --self-contained true --runtime win-x64 --verbosity minimal

# 打包项目（编译 + 安装包）
package: build
	@echo ">>> 打包项目..."
	@powershell -ExecutionPolicy Bypass -File build.ps1 -Target package

# 运行项目
run: build
	@echo ">>> 启动 VoiceInput..."
	@start publish\VoiceInput.exe

# 清理构建产物
clean:
	@echo ">>> 清理构建产物..."
	@if exist publish rmdir /s /q publish
	@if exist dist rmdir /s /q dist
	@if exist src\VoiceInput\bin rmdir /s /q src\VoiceInput\bin
	@if exist src\VoiceInput\obj rmdir /s /q src\VoiceInput\obj

# 显示帮助
help:
	@echo.
	@echo VoiceInput 构建命令:
	@echo.
	@echo   make build      编译项目
	@echo   make package    打包项目（编译 + 安装包）
	@echo   make run        运行项目
	@echo   make clean      清理构建产物
	@echo   make help       显示此帮助
	@echo.
