.PHONY: gh-build gh-publish vsc-build help

# Grasshopper plugin commands
gh-build:
	dotnet build gh-plugin/GHCodeSync.csproj --configuration Release

gh-publish:
	./gh-plugin/yak/publish.sh

# VSCode plugin commands
vsc-build:
	cd vscode-plugin && npm run compile

vsc-package:
	cd vscode-plugin && vsce package

vsc-publish:
	cd vscode-plugin && vsce publish


help:
	@echo "Available commands:"
	@echo ""
	@echo "Grasshopper plugin:"
	@echo "  gh-build   - Build Grasshopper plugin"
	@echo "  gh-publish - Publish Grasshopper plugin"
	@echo ""
	@echo "VSCode plugin:"
	@echo "  vsc-build    - Build VSCode plugin"
	@echo "  vsc-package  - Package VSCode plugin"
	@echo "  vsc-publish  - Publish VSCode plugin"
	@echo ""